using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;

namespace CrimsonHue.Core;

public static class AmbientParser
{
    public static AmbientFrame? Parse(ReadOnlyMemory<byte> json, out string status)
    {
        status = "Ambient unavailable";
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
            var root = doc.RootElement;
            if (root.GetProperty("schemaVersion").GetString() != "1.0" ||
                root.GetProperty("source").GetString() != "precompute-ambient-sky" ||
                root.GetProperty("scope").GetString() != "global-upper-hemisphere-sky" ||
                root.GetProperty("units").GetString() != "relative-shader-units")
                throw new FormatException("Unsupported ambient contract.");
            if (root.GetProperty("status").GetString() != "available")
            {
                status = "Ambient: " + (root.TryGetProperty("reason", out var reason) ? reason.GetString() : "unavailable");
                return null;
            }
            var sequence = root.GetProperty("captureSequence").GetInt64();
            var capturedAt = root.GetProperty("capturedAt").GetDateTimeOffset();
            var skyAge = root.GetProperty("ageMilliseconds").GetDouble();
            var visibility = root.GetProperty("visibility");
            var estimate = root.GetProperty("localEnvironmentAmbientEstimateWorking");
            if (visibility.ValueKind != JsonValueKind.Object || estimate.ValueKind != JsonValueKind.Object ||
                !estimate.GetProperty("available").GetBoolean() || estimate.GetProperty("stale").GetBoolean())
            {
                status = "Ambient: local sky visibility unavailable · local-only fallback";
                return null;
            }
            var visibilityValue = visibility.GetProperty("valueWorking").GetDouble();
            var visibilityAge = visibility.GetProperty("ageMilliseconds").GetDouble();
            if (sequence <= 0 || !double.IsFinite(skyAge) || skyAge is < 0 or > 1500 ||
                !double.IsFinite(visibilityAge) || visibilityAge is < 0 or > 1500 ||
                !double.IsFinite(visibilityValue) || visibilityValue is < 0 or > 1)
                throw new FormatException("Ambient sample is unavailable or stale.");
            var rgb = estimate.GetProperty("rgbWorking");
            var sky = root.GetProperty("sky").GetProperty("upperHemisphereMeanWorking");
            if (rgb.GetArrayLength() != 3 || sky.GetArrayLength() != 3) throw new FormatException("Invalid ambient RGB.");
            var channels = rgb.EnumerateArray().Select(x => x.GetDouble()).ToArray();
            var skyChannels = sky.EnumerateArray().Select(x => x.GetDouble()).ToArray();
            if (channels.Concat(skyChannels).Any(x => !double.IsFinite(x) || Math.Abs(x) > 1e12) ||
                channels.Where((x, i) => Math.Abs(x - skyChannels[i] * visibilityValue) >
                    Math.Max(0.001, Math.Abs(skyChannels[i] * visibilityValue) * 0.001)).Any())
                throw new FormatException("Invalid ambient RGB.");
            // The AP1-like working channels are a relative brightness proxy only.
            var level = channels.Select(x => Math.Max(0, x)).Average();
            status = $"Ambient live · relative level {level:G6}";
            return new(sequence, capturedAt, skyAge, visibilityAge, level,
                new(channels[0], channels[1], channels[2]),
                new(skyChannels[0], skyChannels[1], skyChannels[2]), visibilityValue);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        {
            status = "Invalid ambient telemetry";
            return null;
        }
    }
}

public sealed class AmbientState
{
    private readonly object gate = new();
    private AmbientFrame? frame;
    private long received;
    private string status = "Waiting for CDT Ambient";

    public void Reset(string reason)
    {
        lock (gate) { frame = null; status = reason; }
    }

    public void Accept(ReadOnlyMemory<byte> json)
    {
        var parsed = AmbientParser.Parse(json, out var message);
        lock (gate)
        {
            frame = parsed;
            received = Stopwatch.GetTimestamp();
            status = message;
        }
    }

    public AmbientFrame? Read(out string message, DateTimeOffset? now = null)
    {
        lock (gate)
        {
            message = status;
            if (frame == null) return null;
            var elapsed = Stopwatch.GetElapsedTime(received).TotalMilliseconds;
            var skyWallAge = ((now ?? DateTimeOffset.UtcNow) - frame.CapturedAt).TotalMilliseconds;
            var transportAge = Math.Max(elapsed, Math.Max(0, skyWallAge - frame.SkyAgeMs));
            if (elapsed > 1500 || skyWallAge is > 1500 or < -1000 ||
                frame.SkyAgeMs + transportAge > 1500 || frame.VisibilityAgeMs + transportAge > 1500)
            {
                message = "Ambient stale · local-only fallback";
                return null;
            }
            return frame;
        }
    }
}

public sealed class AmbientClient(AmbientState state)
{
    public const int MaximumMessageBytes = 1024 * 1024;

    public static Uri FromTelemetryEndpoint(Uri endpoint)
    {
        if (endpoint.Scheme != "ws" || !endpoint.IsLoopback || endpoint.UserInfo.Length != 0)
            throw new CrimsonHueException("Ambient must use a local CDT WebSocket.");
        return new UriBuilder(endpoint) { Path = "/v1/ambient/stream", Query = "", Fragment = "" }.Uri;
    }

    public async Task RunAsync(Uri endpoint, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var socket = new ClientWebSocket();
                using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                connectTimeout.CancelAfter(TimeSpan.FromSeconds(5));
                await socket.ConnectAsync(endpoint, connectTimeout.Token);
                state.Reset("Connected · waiting for Ambient");
                await ReceiveAsync(socket, state, cancellationToken);
            }
            catch (Exception ex) when (ex is WebSocketException or HttpRequestException or OperationCanceledException or IOException)
            {
                state.Reset(cancellationToken.IsCancellationRequested ? "Ambient stopped" : "Ambient disconnected · local-only fallback");
            }
            if (!cancellationToken.IsCancellationRequested)
                try { await Task.Delay(1500, cancellationToken); } catch (OperationCanceledException) { }
        }
        state.Reset("Ambient stopped");
    }

    public static async Task ReceiveAsync(WebSocket socket, AmbientState state, CancellationToken token)
    {
        var buffer = new byte[65536];
        using var message = new MemoryStream();
        while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
        {
            var result = await socket.ReceiveAsync(buffer.AsMemory(), token);
            if (result.MessageType == WebSocketMessageType.Close) break;
            if (result.MessageType != WebSocketMessageType.Text || message.Length + result.Count > MaximumMessageBytes)
                throw new IOException("Invalid or oversized ambient message.");
            message.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                state.Accept(message.GetBuffer().AsMemory(0, checked((int)message.Length)));
                message.SetLength(0);
            }
        }
        state.Reset("Ambient disconnected · local-only fallback");
    }
}
