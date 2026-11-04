using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;

namespace CrimsonHue.Core;

// Fresh TCP/session per operation, as in the proven LEDVANCE backend. No global
// discovery, cloud requests, logging or retries; the output scheduler owns pacing.
public sealed class TuyaClient
{
    private readonly TimeSpan timeout;
    public TuyaClient(TimeSpan? timeout = null)
    {
        this.timeout = timeout ?? TimeSpan.FromSeconds(2);
        if (this.timeout < TimeSpan.FromMilliseconds(50) || this.timeout > TimeSpan.FromSeconds(10))
            throw new ArgumentOutOfRangeException(nameof(timeout));
    }

    public async Task<TuyaWriteResult> SendAsync(TuyaLamp lamp, string localKey, TuyaColorCommand command, CancellationToken token)
    {
        var dps = TuyaProtocol.BuildDps(lamp, command);
        var result = await WriteAsync(lamp, localKey, () => new(dps, command), token);
        return result.Result!.Value;
    }

    public async Task<TuyaWriteResult> RestoreAsync(TuyaLamp lamp, string localKey, TuyaDeviceState state, CancellationToken token)
    {
        var dps = TuyaProtocol.RestoreDps(lamp, state);
        var result = await WriteAsync(lamp, localKey, () => new(dps, null), token);
        return result.Result!.Value;
    }

    // Resolve current authorization and RGB only after connection/key negotiation.
    // Returning null sends no control packet. Ownership bookkeeping receives the
    // actual sent command, which can differ from the pre-handshake desired color.
    public Task<TuyaSendResult> SendLatestAsync(TuyaLamp lamp, string localKey, Func<TuyaColorCommand?> latest,
        CancellationToken token) => WriteAsync(lamp, localKey, () =>
        {
            var command = latest();
            return command == null ? null : new(TuyaProtocol.BuildDps(lamp, command), command);
        }, token);

    private sealed record WritePayload(IReadOnlyDictionary<int, JsonElement> Dps, TuyaColorCommand? Command);

    public async Task<bool> ProbeAsync(TuyaLamp lamp, CancellationToken token)
    {
        lamp.Validate();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(timeout);
        using var socket = new TcpClient(AddressFamily.InterNetwork);
        try { await ConnectAsync(socket, lamp, deadline.Token); return true; }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return false; }
        catch (Exception e) when (e is SocketException or IOException) { return false; }
    }

    private async Task<TuyaSendResult> WriteAsync(TuyaLamp lamp, string localKey, Func<WritePayload?> prepare,
        CancellationToken token)
    {
        lamp.Validate();
        var key = TuyaProtocol.KeyBytes(localKey);
        byte[]? session = null;
        var sent = false;
        TuyaColorCommand? sentCommand = null;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(timeout);
        using var socket = new TcpClient(AddressFamily.InterNetwork);
        try
        {
            await ConnectAsync(socket, lamp, deadline.Token);
            var stream = socket.GetStream();
            session = await NegotiateAsync(stream, key, deadline.Token);
            deadline.Token.ThrowIfCancellationRequested();
            var prepared = prepare();
            if (prepared == null) return new(null, null);
            var payload = TuyaProtocol.ControlPayload(prepared.Dps, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            await stream.WriteAsync(TuyaProtocol.Pack(3, 13, payload, session), deadline.Token);
            sent = true; sentCommand = prepared.Command;
            for (var messages = 0; messages < 16; ++messages)
            {
                var packet = TuyaProtocol.Unpack(await TuyaProtocol.ReadPacketAsync(stream, deadline.Token), session);
                // 3.5 devices have their own global reply sequence; matching the
                // request's sequence would incorrectly reject valid acknowledgements.
                if (packet.Command != 13) continue;
                var response = TuyaProtocol.ResponsePayload(packet);
                if (response.Length != 0)
                {
                    using var json = JsonDocument.Parse(response, new JsonDocumentOptions { MaxDepth = 16 });
                    if (json.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException();
                    if (json.RootElement.TryGetProperty("Error", out _) || json.RootElement.TryGetProperty("error", out _) ||
                        (json.RootElement.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.False))
                        throw new CrimsonHueException("Tuya device rejected the command.");
                }
                return new(TuyaWriteResult.Acknowledged, sentCommand);
            }
            return new(TuyaWriteResult.Unacknowledged, sentCommand);
        }
        catch (OperationCanceledException) when (sent)
        {
            // Cancellation cannot retract a complete command already on the
            // wire. Let the owner record the possible change before cleanup.
            return new(TuyaWriteResult.Unacknowledged, sentCommand);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new CrimsonHueException("Tuya connection or handshake timed out.");
        }
        catch (Exception e) when (e is SocketException or IOException or InvalidDataException or CryptographicException or JsonException)
        {
            if (sent) return new(TuyaWriteResult.Unacknowledged, sentCommand);
            throw new CrimsonHueException("Tuya connection or authenticated handshake failed.");
        }
        finally { CryptographicOperations.ZeroMemory(key); if (session != null) CryptographicOperations.ZeroMemory(session); }
    }

    // TinyTuya v3.5 DP_QUERY_NEW is command16 with empty JSON, without the3.5
    // protocol header. A status reply can use command8. Querying never writes DPS.
    public async Task<TuyaDeviceState> ReadStateAsync(TuyaLamp lamp, string localKey, CancellationToken token)
    {
        lamp.Validate();
        var key = TuyaProtocol.KeyBytes(localKey);
        byte[]? session = null;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(timeout);
        using var socket = new TcpClient(AddressFamily.InterNetwork);
        try
        {
            await ConnectAsync(socket, lamp, deadline.Token);
            var stream = socket.GetStream();
            session = await NegotiateAsync(stream, key, deadline.Token);
            await stream.WriteAsync(TuyaProtocol.Pack(3, 16, "{}"u8.ToArray(), session), deadline.Token);
            for (var messages = 0; messages < 16; ++messages)
            {
                var packet = TuyaProtocol.Unpack(await TuyaProtocol.ReadPacketAsync(stream, deadline.Token), session);
                if (packet.Command is not (8 or 16)) continue;
                var response = TuyaProtocol.ResponsePayload(packet);
                if (response.Length == 0) continue;
                return TuyaProtocol.ParseState(lamp, response);
            }
            throw new CrimsonHueException("Tuya device did not report a usable state.");
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        { throw new CrimsonHueException("Tuya state query timed out."); }
        catch (Exception e) when (e is SocketException or IOException or InvalidDataException or CryptographicException or JsonException or InvalidOperationException)
        { throw new CrimsonHueException("Tuya state query failed."); }
        finally { CryptographicOperations.ZeroMemory(key); if (session != null) CryptographicOperations.ZeroMemory(session); }
    }

    private static async Task ConnectAsync(TcpClient client, TuyaLamp lamp, CancellationToken token)
    {
        var addresses = await Dns.GetHostAddressesAsync(lamp.Host, AddressFamily.InterNetwork, token);
        if (addresses.Length == 0) throw new IOException();
        client.NoDelay = true;
        await client.ConnectAsync(addresses, lamp.Port, token);
    }

    private static async Task<byte[]> NegotiateAsync(NetworkStream stream, byte[] key, CancellationToken token)
    {
        var nonce = TuyaProtocol.LocalNonce;
        await stream.WriteAsync(TuyaProtocol.Pack(1, 3, nonce, key), token);
        var reply = TuyaProtocol.Unpack(await TuyaProtocol.ReadPacketAsync(stream, token), key);
        if (reply.Command != 4) throw new InvalidDataException("Invalid Tuya handshake response.");
        var payload = TuyaProtocol.ResponsePayload(reply, handshake: true);
        if (payload.Length != 48 || !CryptographicOperations.FixedTimeEquals(HMACSHA256.HashData(key, nonce), payload.AsSpan(16)))
            throw new CryptographicException();
        var remote = payload[..16];
        await stream.WriteAsync(TuyaProtocol.Pack(2, 5, HMACSHA256.HashData(key, remote), key), token);
        return TuyaProtocol.DeriveSessionKey(key, nonce, remote);
    }
}
