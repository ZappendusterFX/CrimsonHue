using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;

namespace CrimsonHue.Core;

public sealed record ActivityFrame(long Sequence, DateTimeOffset TimestampUtc, string SessionId,
    string State, double? ObservationAgeMilliseconds, bool AcquisitionPaused, long Epoch, string Reason);

public static class ActivityParser
{
    public static ActivityFrame? Parse(ReadOnlyMemory<byte> json, out string status)
    {
        status = "Activity unavailable";
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
            var r = document.RootElement;
            if (r.GetProperty("schemaVersion").GetString() != "1.0" ||
                r.GetProperty("source").GetString() != "simulation-tick") throw new FormatException();
            var sequence = r.GetProperty("sequence").GetInt64();
            var timestamp = r.GetProperty("timestampUtc").GetDateTimeOffset();
            var session = r.GetProperty("sessionId").GetString();
            var state = r.GetProperty("state").GetString();
            var epoch = r.GetProperty("epoch").GetInt64();
            var ageValue = r.GetProperty("observationAgeMilliseconds");
            double? age = ageValue.ValueKind == JsonValueKind.Null ? null : ageValue.GetDouble();
            var paused = r.GetProperty("acquisitionPaused").GetBoolean();
            var reason = r.GetProperty("reason").GetString();
            if (sequence < 0 || epoch < 0 || string.IsNullOrWhiteSpace(session) || session.Length > 256 ||
                state is not ("running" or "paused" or "unknown") || string.IsNullOrEmpty(reason) || reason.Length > 1024 ||
                age is { } value && (!double.IsFinite(value) || value < 0)) throw new FormatException();
            status = "Activity · " + state;
            return new(sequence, timestamp, session, state!, age, paused, epoch, reason);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        { status = "Invalid activity telemetry"; return null; }
    }
}

// Independent of light capture: duplicate publications never refresh authorization.
public sealed class ActivityState
{
    private readonly object gate = new();
    private ActivityFrame? frame;
    private string? session;
    private long sequence = -1;
    private long epoch = -1;
    private long received;
    private double ageAtReceive;
    private string status = "Activity unavailable · normal lighting";

    public void Reset(string reason)
    {
        lock (gate) { frame = null; session = null; sequence = epoch = -1; status = reason; }
    }

    public void Accept(ReadOnlyMemory<byte> json, DateTimeOffset? utcNow = null, long? monotonicTimestamp = null)
    {
        var parsed = ActivityParser.Parse(json, out var message);
        lock (gate)
        {
            if (parsed == null) { frame = null; status = message; return; }
            if (parsed.SessionId == session && parsed.Sequence <= sequence) return;
            if (parsed.SessionId == session && parsed.Epoch < epoch && parsed.State != "unknown") return;
            // A fresh unknown publication can carry no native epoch. It still revokes
            // menu authorization immediately, while preserving the known epoch floor.
            epoch = parsed.SessionId == session ? Math.Max(epoch, parsed.Epoch) : parsed.Epoch;
            session = parsed.SessionId; sequence = parsed.Sequence;
            frame = parsed; status = message;
            received = monotonicTimestamp ?? Stopwatch.GetTimestamp();
            ageAtReceive = Math.Max(0, ((utcNow ?? DateTimeOffset.UtcNow) - parsed.TimestampUtc).TotalMilliseconds);
        }
    }

    public ActivityFrame? Read(out string message, DateTimeOffset? utcNow = null, long? monotonicTimestamp = null)
    {
        lock (gate)
        {
            message = status;
            if (frame == null) return null;
            var elapsed = Stopwatch.GetElapsedTime(received, monotonicTimestamp ?? Stopwatch.GetTimestamp()).TotalMilliseconds;
            var wallAge = ((utcNow ?? DateTimeOffset.UtcNow) - frame.TimestampUtc).TotalMilliseconds;
            var transportAge = Math.Max(elapsed + ageAtReceive, Math.Max(0, wallAge));
            if (elapsed < 0 || elapsed > 500 || wallAge < -100 || transportAge > 500 ||
                frame.ObservationAgeMilliseconds is not { } age || age + transportAge > 500)
            { message = "Activity stale or unknown · normal safe fallback"; return null; }
            return frame;
        }
    }
}

public sealed class ActivityClient(ActivityState state)
{
    public const int MaximumMessageBytes = 16 * 1024;
    public static Uri FromTelemetryEndpoint(Uri endpoint)
    {
        TelemetryClient.ValidateEndpoint(endpoint.ToString());
        return new UriBuilder(endpoint) { Path = "/v1/activity/stream", Query = "", Fragment = "" }.Uri;
    }
    public async Task RunAsync(Uri endpoint, CancellationToken token)
    {
        TelemetryClient.ValidateEndpoint(endpoint.ToString());
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var socket = new ClientWebSocket();
                using var connect = CancellationTokenSource.CreateLinkedTokenSource(token);
                connect.CancelAfter(TimeSpan.FromSeconds(5));
                await socket.ConnectAsync(endpoint, connect.Token);
                state.Reset("Activity connected · waiting for observation");
                await ReceiveAsync(socket, state, token);
            }
            catch (Exception ex) when (ex is WebSocketException or HttpRequestException or OperationCanceledException or IOException)
            { state.Reset("Activity unavailable · normal safe fallback"); }
            if (!token.IsCancellationRequested)
                try { await Task.Delay(1500, token); } catch (OperationCanceledException) { }
        }
        state.Reset("Activity stopped");
    }
    public static async Task ReceiveAsync(WebSocket socket, ActivityState state, CancellationToken token)
    {
        var buffer = new byte[4096];
        using var message = new MemoryStream();
        try
        {
            while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(buffer.AsMemory(), token);
                if (result.MessageType == WebSocketMessageType.Close) break;
                if (result.MessageType != WebSocketMessageType.Text || message.Length + result.Count > MaximumMessageBytes)
                    throw new IOException("Invalid or oversized activity message.");
                message.Write(buffer, 0, result.Count);
                if (result.EndOfMessage)
                {
                    state.Accept(message.GetBuffer().AsMemory(0, checked((int)message.Length)));
                    message.SetLength(0);
                }
            }
        }
        finally { state.Reset("Activity disconnected · normal safe fallback"); }
    }
}
