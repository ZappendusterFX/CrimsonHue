using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;

namespace CrimsonHue.Core;

public static class TelemetryParser
{
    public static TelemetryFrame? Parse(ReadOnlyMemory<byte> json, out string status)
        => Parse(json, out status, out _);
    public static TelemetryFrame? Parse(ReadOnlyMemory<byte> json, out string status, out long sequence)
    {
        status = "Telemetry unavailable";
        sequence = -1;
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
            var root = doc.RootElement;
            sequence = root.GetProperty("sequence").GetInt64();
            if (sequence < 0) throw new FormatException("Invalid sequence.");
            var version = root.GetProperty("schemaVersion").GetString()?.Split('.');
            if (version is not { Length: >= 2 } || version[0] != "1" || !int.TryParse(version[1], out var minor) || minor < 4)
                throw new FormatException("Lighting schema 1.4 or compatible 1.x required.");
            var state = root.GetProperty("game").GetProperty("state").GetString();
            if (state != "playing") { status = $"Game: {state}"; return null; }
            var axes = root.GetProperty("coordinateSystem");
            if (axes.GetProperty("unit").GetString() != "game-unit" || axes.GetProperty("upAxis").GetString() != "y" || axes.GetProperty("handedness").GetString() != "right")
                throw new FormatException("Unsupported coordinate system.");
            var capabilities = root.GetProperty("capabilities").EnumerateArray().Select(x => x.GetString()).ToHashSet();
            if (!capabilities.Contains("lights.rendered") || !capabilities.Contains("camera.transform") || !capabilities.Contains("player.position"))
                throw new FormatException("Required telemetry capabilities missing.");
            var player = Vec3.Read(root.GetProperty("player").GetProperty("position"));
            var rendered = root.GetProperty("lights").GetProperty("rendered");
            if (rendered.GetProperty("status").GetString() != "available")
            {
                status = "Lights: " + (rendered.TryGetProperty("unavailableReason", out var reason) ? reason.GetString() : "unavailable");
                return null;
            }
            if (rendered.GetProperty("source").GetString() != "filtered-manylights") throw new FormatException("Unsupported light feed.");
            // Use the capture-paired camera for both the origin and orientation of rendered lights.
            var camera = rendered.GetProperty("camera");
            var pose = new CameraPose(Vec3.Read(camera.GetProperty("position")), Vec3.Read(camera.GetProperty("right")),
                Vec3.Read(camera.GetProperty("up")), Vec3.Read(camera.GetProperty("forward")));
            if (new[] { pose.Right, pose.Up, pose.Forward }.Any(v => Math.Abs(v.Length - 1) > 0.05) ||
                Math.Abs(pose.Right.Dot(pose.Forward)) > 0.05 || Math.Abs(pose.Up.Dot(pose.Forward)) > 0.05 || Math.Abs(pose.Right.Dot(pose.Up)) > 0.05)
                throw new FormatException("Invalid camera basis.");
            var age = rendered.GetProperty("ageMilliseconds").GetDouble();
            if (!double.IsFinite(age) || age < 0 || age > 500) throw new FormatException("Light capture is stale.");
            var sources = rendered.GetProperty("sources");
            if (sources.GetArrayLength() > 32768) throw new FormatException("Light sample exceeds capacity.");
            var lights = new List<LightContribution>(sources.GetArrayLength());
            foreach (var source in sources.EnumerateArray())
            {
                var rgb = Vec3.Read(source.GetProperty("colorLinear"));
                if (rgb.X < 0 || rgb.Y < 0 || rgb.Z < 0) throw new FormatException("Negative light RGB.");
                lights.Add(new(Vec3.Read(source.GetProperty("position")), rgb));
            }
            var capture = rendered.GetProperty("captureSequence").GetInt64();
            if (sequence < 0 || capture < 0) throw new FormatException("Invalid sequence.");
            status = $"Live · {lights.Count} contributions";
            return new(sequence, capture, root.GetProperty("capturedAt").GetDateTimeOffset(),
                rendered.GetProperty("capturedAt").GetDateTimeOffset(), age, player, pose, lights);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        {
            status = "Invalid telemetry: " + (ex is FormatException ? ex.Message : "incomplete or malformed snapshot");
            return null;
        }
    }
}

// One latest snapshot, no replay backlog. Duplicate envelopes never extend freshness.
public sealed class TelemetryState
{
    private readonly object gate = new();
    private TelemetryFrame? frame;
    private long lastSequence = -1;
    private long received;
    private string status = "Waiting for CrimsonDesertTelemetry";
    public void Reset(string reason)
    {
        lock (gate) { frame = null; lastSequence = -1; status = reason; }
    }
    public void Accept(ReadOnlyMemory<byte> json)
    {
        var parsed = TelemetryParser.Parse(json, out var message, out var sequence);
        lock (gate)
        {
            if (sequence >= 0 && sequence <= lastSequence) return;
            if (sequence >= 0) lastSequence = sequence;
            if (parsed == null) { frame = null; status = message; return; }
            frame = parsed;
            received = Stopwatch.GetTimestamp();
            status = message;
        }
    }
    public TelemetryFrame? Read(out string message, DateTimeOffset? now = null)
    {
        lock (gate)
        {
            message = status;
            if (frame == null) return null;
            var elapsed = Stopwatch.GetElapsedTime(received).TotalMilliseconds;
            var utc = now ?? DateTimeOffset.UtcNow;
            var envelopeAge = (utc - frame.CapturedAt).TotalMilliseconds;
            var lightAge = (utc - frame.LightCapturedAt).TotalMilliseconds;
            if (elapsed > 1500 || envelopeAge > 1500 || envelopeAge < -1000 || lightAge < -1000 || lightAge > 500 || frame.ProducerAgeMs + elapsed > 500)
            {
                message = "Telemetry stale · output paused";
                return null;
            }
            return frame;
        }
    }
}

public sealed class TelemetryClient(TelemetryState state)
{
    public const int MaximumMessageBytes = 16 * 1024 * 1024;
    public static Uri ValidateEndpoint(string address)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme != "ws" || !uri.IsLoopback || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new CrimsonHueException("Telemetry must use a local ws://127.0.0.1:port/v1/stream address.");
        return uri;
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
                state.Reset("Connected · waiting for live telemetry");
                await ReceiveAsync(socket, state, cancellationToken);
            }
            catch (Exception ex) when (ex is WebSocketException or HttpRequestException or OperationCanceledException or IOException)
            {
                state.Reset(cancellationToken.IsCancellationRequested ? "Telemetry stopped" : "Telemetry disconnected · retrying");
            }
            if (!cancellationToken.IsCancellationRequested)
                try { await Task.Delay(1500, cancellationToken); } catch (OperationCanceledException) { }
        }
        state.Reset("Telemetry stopped");
    }
    public static async Task ReceiveAsync(WebSocket socket, TelemetryState state, CancellationToken token)
    {
        var buffer = new byte[65536];
        using var message = new MemoryStream();
        while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
        {
            var result = await socket.ReceiveAsync(buffer.AsMemory(), token);
            if (result.MessageType == WebSocketMessageType.Close) break;
            if (result.MessageType != WebSocketMessageType.Text || message.Length + result.Count > MaximumMessageBytes)
                throw new IOException("Invalid or oversized telemetry message.");
            message.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                state.Accept(message.GetBuffer().AsMemory(0, checked((int)message.Length)));
                message.SetLength(0);
            }
        }
        state.Reset("Telemetry disconnected · retrying");
    }
}
