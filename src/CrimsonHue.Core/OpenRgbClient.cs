using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace CrimsonHue.Core;

/// <summary>Loopback-only, bounded SDK 4–6 connection for the opt-in live development test.</summary>
public sealed class OpenRgbClient : IAsyncDisposable
{
    private readonly TcpClient client = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly SemaphoreSlim writes = new(1), requests = new(1);
    private readonly object gate = new();
    private NetworkStream? stream;
    private Task? reader;
    private TaskCompletionSource<byte[]>? pending;
    private uint pendingPacket, pendingDevice;
    private Exception? failure;
    public uint ServerVersion { get; private set; }
    public uint ProtocolVersion { get; private set; }
    public bool Healthy { get { lock (gate) return failure is null && stream is not null; } }

    public async Task ConnectAsync(int port = 6742, CancellationToken token = default)
    {
        if (port is < 1 or > 65535 || stream != null) throw new ArgumentException("Invalid OpenRGB connection.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(2000);
        await client.ConnectAsync(IPAddress.Loopback, port, timeout.Token);
        client.NoDelay = true; stream = client.GetStream(); reader = ReceiveAsync();
        var version = await RequestAsync(40, 0, BitConverter.GetBytes(OpenRgbProtocol.Version), token);
        if (version.Length != 4) throw new IOException("Invalid OpenRGB protocol reply.");
        ServerVersion = BinaryPrimitives.ReadUInt32LittleEndian(version);
        ProtocolVersion = Math.Min(ServerVersion, OpenRgbProtocol.Version);
        if (ProtocolVersion < OpenRgbProtocol.MinimumVersion) throw new IOException("This test requires OpenRGB SDK protocol 4 or later.");
    }

    public async Task<IReadOnlyList<OpenRgbDevice>> InventoryAsync(CancellationToken token = default)
    {
        var raw = await RequestAsync(0, 0, [], token);
        if (raw.Length < 4) throw new IOException("Invalid OpenRGB controller count.");
        var count = BinaryPrimitives.ReadUInt32LittleEndian(raw);
        if (count > OpenRgbProtocol.MaximumDevices) throw new IOException("Too many OpenRGB devices for this preview.");
        if (raw.Length != (ProtocolVersion >= 6 ? 4 + count * 4 : 4)) throw new IOException("Invalid OpenRGB controller IDs.");
        var ids = Enumerable.Range(0, (int)count).Select(i => ProtocolVersion >= 6 ?
            BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(4 + i * 4)) : (uint)i).ToArray();
        if (ids.Distinct().Count() != ids.Length) throw new IOException("Duplicate OpenRGB controller IDs.");
        var result = new List<OpenRgbDevice>();
        foreach (var id in ids) result.Add(await DeviceAsync(id, token));
        return result;
    }

    public async Task<OpenRgbDevice> DeviceAsync(uint id, CancellationToken token = default) =>
        OpenRgbProtocol.ParseDevice(id, await RequestAsync(1, id,
            ProtocolVersion >= 6 ? [] : BitConverter.GetBytes(ProtocolVersion), token), ProtocolVersion);

    public Task SetModeAsync(OpenRgbDevice device, int mode, CancellationToken token = default)
    {
        if (mode < 0 || mode >= device.Modes.Count || device.Modes[mode].AutomaticSave)
            throw new IOException("Preview refuses modes that automatically save to the device.");
        return WriteCommandAsync(1101, device.Id, OpenRgbProtocol.ModePacket(mode, device.Modes[mode]), token);
    }
    public Task SetColorsAsync(uint id, IReadOnlyList<uint> colors, CancellationToken token = default) =>
        WriteCommandAsync(1050, id, OpenRgbProtocol.ColorsPacket(colors), token);

    private Task WriteCommandAsync(uint packet, uint device, byte[] body, CancellationToken token) =>
        ProtocolVersion >= 6 ? RequestAsync(packet, device, body, token) : SendAsync(packet, device, body, token);

    private async Task<byte[]> RequestAsync(uint packet, uint device, byte[] body, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(2000);
        await requests.WaitAsync(timeout.Token);
        try
        {
            var completion = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (gate) { ThrowIfFailed(); pending = completion; pendingPacket = packet; pendingDevice = device; }
            try
            {
                await SendAsync(packet, device, body, timeout.Token);
                return await completion.Task.WaitAsync(timeout.Token);
            }
            catch { Fail(new IOException("OpenRGB request did not complete; connection invalidated.")); throw; }
            finally { lock (gate) pending = null; }
        }
        finally { requests.Release(); }
    }

    private async Task SendAsync(uint packet, uint device, byte[] body, CancellationToken token)
    {
        if (packet is not (0 or 1 or 40 or 1050 or 1101) || body.Length > OpenRgbProtocol.MaximumPacketBytes)
            throw new IOException("Unsupported OpenRGB preview command.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(1000);
        await writes.WaitAsync(timeout.Token);
        try
        {
            lock (gate) ThrowIfFailed();
            var bytes = new byte[16 + body.Length]; "ORGB"u8.CopyTo(bytes);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), device);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), packet);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), (uint)body.Length);
            body.CopyTo(bytes, 16);
            await stream!.WriteAsync(bytes, timeout.Token);
        }
        catch (Exception ex) { Fail(new IOException("OpenRGB write failed.", ex)); throw; }
        finally { writes.Release(); }
    }

    private async Task ReceiveAsync()
    {
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                var header = new byte[16]; await stream!.ReadExactlyAsync(header, lifetime.Token);
                if (!header.AsSpan(0, 4).SequenceEqual("ORGB"u8)) throw new IOException("Invalid OpenRGB header.");
                var device = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4));
                var packet = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8));
                var size = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12));
                if (size > OpenRgbProtocol.MaximumPacketBytes) throw new IOException("Oversized OpenRGB packet.");
                if (packet is 100 or 101 or 102 or 103) throw new IOException("OpenRGB device list changed; output stopped before reusing its snapshot.");
                var body = new byte[checked((int)size)]; await stream.ReadExactlyAsync(body, lifetime.Token);
                // SDK 6 sends server metadata and asynchronous controller updates.
                // Ownership is checked with explicit snapshots, never inferred from
                // these notifications (which also echo our own color writes).
                if (packet is 51 or 53 or 1150) continue;
                lock (gate)
                {
                    if (packet == 10)
                    {
                        if (body.Length != 8) throw new IOException("Invalid OpenRGB acknowledgement.");
                        var command = BinaryPrimitives.ReadUInt32LittleEndian(body);
                        var status = BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(4));
                        if (status != 0) throw new IOException($"OpenRGB rejected command {command}: SDK status {status}.");
                        if (command is 0 or 1 or 40) continue; // Read replies carry their own data; their ACK may arrive later.
                        if (pending is null || command != pendingPacket || device != pendingDevice || !pending.TrySetResult([]))
                            throw new IOException("Unexpected OpenRGB acknowledgement.");
                        continue;
                    }
                    if (pending is null || pendingPacket != packet || (packet == 1 && pendingDevice != device))
                        throw new IOException("Unexpected OpenRGB response.");
                    if (!pending.TrySetResult(body)) throw new IOException("Duplicate OpenRGB response.");
                }
            }
        }
        catch (Exception ex) { Fail(ex); }
    }

    private void ThrowIfFailed()
    {
        if (failure != null) throw new IOException("OpenRGB connection is no longer usable.", failure);
        if (stream is null) throw new IOException("OpenRGB is not connected.");
    }
    private void Fail(Exception error)
    {
        lock (gate) { failure ??= error; pending?.TrySetException(error); }
        lifetime.Cancel(); client.Dispose();
    }
    public async ValueTask DisposeAsync()
    {
        Fail(new ObjectDisposedException(nameof(OpenRgbClient)));
        if (reader != null) await reader;
        lifetime.Dispose();
    }
}
