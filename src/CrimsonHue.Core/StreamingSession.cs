using System.Diagnostics;
using System.Text.Json;

namespace CrimsonHue.Core;

public sealed class StreamingSession(BridgeClient bridge, Func<IEntertainmentTransport>? transportFactory = null)
{
    private string status = "Stopped";
    private ChannelColor[] colors = [];
    public string Status => Volatile.Read(ref status);
    public IReadOnlyList<ChannelColor> Colors => Volatile.Read(ref colors);
    public bool Sending { get; private set; }
    public async Task RunAsync(EntertainmentArea area, TelemetryState telemetry, Func<MappingSettings> settings, CancellationToken token)
    {
        if (area.Channels.Count == 0) throw new CrimsonHueException("Choose an Entertainment area with lights.");
        IEntertainmentTransport? transport = null;
        Dictionary<string, JsonElement>? original = null;
        string? owner = null;
        var started = false;
        var mapper = new LightMapper();
        byte sequence = 0;
        var invalidSince = 0L;
        var lastCheck = 0L;
        Task<JsonElement>? stateCheck = null;
        CancellationTokenSource? checkCancel = null;
        try
        {
            while (!token.IsCancellationRequested)
            {
                var frame = telemetry.Read(out _);
                if (frame == null)
                {
                    colors = area.Channels.Select(c => new ChannelColor(c.Id, default)).ToArray();
                    mapper.Reset();
                    if (transport != null)
                    {
                        transport.Send(EntertainmentPacket.Build(area.Id, sequence++, colors));
                        if (invalidSince == 0) invalidSince = Stopwatch.GetTimestamp();
                        if (Stopwatch.GetElapsedTime(invalidSince).TotalSeconds >= 1)
                        {
                            await ReleaseAsync();
                            status = "Armed · waiting for fresh telemetry";
                        }
                        else status = "Stale telemetry · output cleared";
                    }
                    else status = "Armed · waiting for fresh telemetry";
                }
                else
                {
                    invalidSince = 0;
                    if (transport == null)
                    {
                        status = "Connecting Entertainment stream…";
                        var areas = await bridge.GetAreasAsync(token);
                        if (areas.Any(a => a.Active)) throw new CrimsonHueException("An Entertainment stream is already active. Stop the other sync app before starting CrimsonHue.");
                        var current = areas.FirstOrDefault(a => a.Id == area.Id) ?? throw new CrimsonHueException("The selected area was removed. Refresh the area list.");
                        if (Layout(current) != Layout(area)) throw new CrimsonHueException("The area's layout changed. Refresh it before starting.");
                        original = await bridge.CaptureLightStatesAsync(area, token);
                        // Complete bounded mutating requests even if Stop is clicked in flight.
                        started = true;
                        await bridge.SetStreamingAsync(area.Id, true, CancellationToken.None);
                        var state = await bridge.GetAreaStateAsync(area.Id, CancellationToken.None);
                        owner = GetOwner(state);
                        if (state.GetProperty("status").GetString() != "active") throw new CrimsonHueException("Bridge did not activate the Entertainment area.");
                        token.ThrowIfCancellationRequested();
                        transport = transportFactory?.Invoke() ?? new EntertainmentTransport();
                        await transport.ConnectAsync(bridge.Credentials, token);
                        Sending = true;
                        checkCancel = CancellationTokenSource.CreateLinkedTokenSource(token);
                        lastCheck = Stopwatch.GetTimestamp();
                        mapper.Reset();
                    }
                    if (stateCheck?.IsCompleted == true)
                    {
                        var state = await stateCheck;
                        stateCheck = null;
                        if (state.GetProperty("status").GetString() != "active" || GetOwner(state) != owner)
                        {
                            started = false; // Another app/user stopped or took over; do not overwrite its state.
                            original = null;
                            throw new CrimsonHueException("Streaming was stopped or taken over at the bridge.");
                        }
                        if (Layout(BridgeClient.ParseArea(state)) != Layout(area)) throw new CrimsonHueException("Area layout changed during streaming. Refresh it before restarting.");
                        lastCheck = Stopwatch.GetTimestamp();
                    }
                    if (stateCheck == null && Stopwatch.GetElapsedTime(lastCheck).TotalSeconds >= 2)
                        stateCheck = bridge.GetAreaStateAsync(area.Id, checkCancel!.Token);
                    // Handshakes/HTTP checks can take seconds. Re-read freshness before every datagram.
                    frame = telemetry.Read(out _);
                    colors = frame == null ? area.Channels.Select(c => new ChannelColor(c.Id, default)).ToArray() : mapper.Map(frame, area, settings(), 1.0 / 30).ToArray();
                    transport.Send(EntertainmentPacket.Build(area.Id, sequence++, colors));
                    status = "Streaming · 30 updates/s";
                }
                await Task.Delay(33, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally
        {
            await ReleaseAsync();
            colors = [];
        }

        async Task ReleaseAsync()
        {
            // REST is authoritative for ownership; only restore when we still own this stream.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            try
            {
                if (checkCancel != null)
                {
                    await checkCancel.CancelAsync();
                    if (stateCheck != null) try { await stateCheck; } catch (Exception ex) when (ex is not OutOfMemoryException) { }
                    stateCheck = null;
                    checkCancel.Dispose(); checkCancel = null;
                }
                if (started)
                {
                    var current = await bridge.GetAreaStateAsync(area.Id, cleanup.Token);
                    var currentOwner = GetOwner(current);
                    if (current.GetProperty("status").GetString() == "active" &&
                        (owner != null ? owner == currentOwner : currentOwner == bridge.Credentials.ApplicationKey))
                    {
                        try { transport?.Send(EntertainmentPacket.Build(area.Id, sequence++, area.Channels.Select(c => new ChannelColor(c.Id, default)).ToArray())); }
                        catch (Exception ex) when (ex is IOException or System.Net.Sockets.SocketException or ObjectDisposedException) { }
                        transport?.Dispose(); transport = null;
                        await bridge.SetStreamingAsync(area.Id, false, cleanup.Token);
                        // Recheck other sessions before restoring normal light states.
                        var areas = await bridge.GetAreasAsync(cleanup.Token);
                        if (original != null && !areas.Any(a => a.Active)) await bridge.RestoreLightStatesAsync(original, cleanup.Token);
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException or CrimsonHueException or JsonException)
            {
                throw new CrimsonHueException("Stream closed, but bridge cleanup could not be confirmed. Check the area and lights in the Hue app.");
            }
            finally
            {
                transport?.Dispose(); transport = null;
                Sending = false; started = false; original = null; owner = null;
            }
        }
    }
    private static string? GetOwner(JsonElement state) => state.TryGetProperty("active_streamer", out var s) && s.TryGetProperty("rid", out var rid) ? rid.GetString() : null;
    private static string Layout(EntertainmentArea area) => string.Join(";", area.Channels.Select(c => $"{c.Id}:{c.Position}:{c.Brightness}:{string.Join(',', c.Members)}"));
}
