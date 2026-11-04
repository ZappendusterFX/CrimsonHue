using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CrimsonHue.Core;

public sealed record TuyaStreamState(string Status, bool Sending = false, int Devices = 0,
    long Frames = 0, long Acknowledged = 0, long Unacknowledged = 0, long Failures = 0,
    IReadOnlyDictionary<string, Vec3>? DeviceColors = null,
    IReadOnlyDictionary<string, string>? DeviceStatuses = null);

// Mapping publishes one replaceable desired value per lamp. Every lamp has its own
// bounded transport/cleanup worker; a slow socket never queues old colors or holds
// the mapper, UI, another lamp, Hue or OpenRGB behind its timeout.
public sealed class TuyaStreamingSession
{
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromMilliseconds(300);
    public static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(2500);
    private readonly Func<TuyaLamp, string, Func<TuyaColorCommand?>, CancellationToken, Task<TuyaSendResult>> send;
    private readonly Func<TuyaLamp, string, CancellationToken, Task<TuyaDeviceState>> read;
    private readonly Func<TuyaLamp, string, TuyaDeviceState, CancellationToken, Task<TuyaWriteResult>> restore;
    private TuyaStreamState state = new("Tuya · stopped");
    private int running;
    public TuyaStreamState State => Volatile.Read(ref state);

    public TuyaStreamingSession()
    {
        var client = new TuyaClient();
        send = client.SendLatestAsync; read = client.ReadStateAsync; restore = client.RestoreAsync;
    }

    // All three operations must be supplied together; fake writes must never cause
    // an accidental real-network state query or restoration in synthetic tests.
    public TuyaStreamingSession(
        Func<TuyaLamp, string, Func<TuyaColorCommand?>, CancellationToken, Task<TuyaSendResult>> send,
        Func<TuyaLamp, string, CancellationToken, Task<TuyaDeviceState>> read,
        Func<TuyaLamp, string, TuyaDeviceState, CancellationToken, Task<TuyaWriteResult>> restore)
    { this.send = send; this.read = read; this.restore = restore; }

    public async Task RunAsync(TelemetryState telemetry, AmbientState ambient, Func<MappingSettings> settings,
        CancellationToken token, Func<TuyaLayout> layout, Func<string, string?> localKey,
        ActivityState? activity = null, Func<MenuLightingSettings>? menuLighting = null)
    {
        if (Interlocked.Exchange(ref running, 1) != 0) throw new InvalidOperationException("Tuya session already used.");
        var workers = new Dictionary<string, LampWorker>(StringComparer.Ordinal);
        var mapper = new LightMapper(); var menuPolicy = new MenuLightingPolicy();
        var lastMap = Stopwatch.GetTimestamp(); string? previousLayout = null;
        var lastTargets = 0; var retiredRetained = 0;
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
            while (!token.IsCancellationRequested)
            {
                var config = layout(); config.Validate();
                var targets = config.Enabled ? config.Targets() : [];
                lastTargets = targets.Count;
                foreach (var entry in workers.ToArray())
                {
                    var current = targets.FirstOrDefault(l => l.Id == entry.Key);
                    if (current != null && entry.Value.Lamp.SameConnection(current)) continue;
                    entry.Value.Stop();
                    if (!entry.Value.Task.IsCompleted) continue;
                    if (entry.Value.Snapshot.Retained) retiredRetained++;
                    entry.Value.Dispose(); workers.Remove(entry.Key);
                }
                foreach (var lamp in targets)
                    if (!workers.ContainsKey(lamp.Id) && workers.Count < TuyaLayout.MaximumDevices &&
                        !workers.Values.Any(w => w.Lamp.DeviceId == lamp.DeviceId ||
                            (w.Lamp.Port == lamp.Port && string.Equals(w.Lamp.Host, lamp.Host, StringComparison.OrdinalIgnoreCase))))
                        workers.Add(lamp.Id, new(lamp, localKey, send, read, restore, StillAuthorized, token));

                var now = Stopwatch.GetTimestamp();
                var menu = menuLighting?.Invoke() ?? new();
                var observedActivity = activity?.Read(out _);
                var decision = menuPolicy.Evaluate(observedActivity, telemetry.Read(out _), ambient.Read(out _), menu);
                // A changed set/order/position must not inherit another channel's history.
                var layoutKey = string.Join("|", targets.Select(l => $"{l.Id}:{l.Position}"));
                if (decision.ResetMapper || previousLayout != layoutKey) mapper.Reset();
                previousLayout = layoutKey;
                var mapping = config.Mapping(settings()); var area = TuyaLayout.Area(targets);
                IReadOnlyList<ChannelColor> colors = decision.Frame == null ? [] : mapper.Map(decision.Frame, area,
                    mapping, Stopwatch.GetElapsedTime(lastMap, now).TotalSeconds, decision.Ambient);
                if (!decision.CanOutput) mapper.Reset();
                colors = MenuLightingPolicy.Apply(decision, colors, area, mapping, menu);
                for (var i = 0; i < targets.Count; i++)
                    if (workers.TryGetValue(targets[i].Id, out var worker) && worker.Lamp.SameConnection(targets[i]))
                        worker.Publish(new(FromOutput(colors[i].Rgb), decision.CanOutput, now,
                            observedActivity, decision.Frame, decision.UseMenuLight));
                lastMap = now;
                PublishState(!config.Enabled ? "Tuya · disabled" : targets.Count == 0 ? "Tuya · no lamps selected" :
                    decision.UseMenuLight ? "Tuya · menu light" : decision.CanOutput ? "Tuya · room lighting" :
                    "Tuya · waiting for fresh CDT; active output cleared", workers.Values, lastTargets);
                await timer.WaitForNextTickAsync(token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally
        {
            foreach (var worker in workers.Values) worker.Stop();
            PublishState("Tuya · stopping and checking prior states", workers.Values, lastTargets);
            await Task.WhenAll(workers.Values.Select(w => w.Task));
            var retained = retiredRetained + workers.Values.Count(w => w.Snapshot.Retained);
            PublishState(retained > 0 ? $"Tuya stopped · {retained} lamp state(s) retained; see device status" :
                "Tuya stopped · prior states restored when still owned", workers.Values, lastTargets);
            foreach (var worker in workers.Values) worker.Dispose();
        }

        // The transport invokes this again after connecting/authenticating. A
        // pause/resume/session transition revokes the old mapped color even if the
        // 50 ms producer has not yet processed that activity publication.
        bool StillAuthorized(Desired value)
        {
            var currentActivity = activity?.Read(out _);
            if (value.Activity is { } original)
            {
                if (currentActivity == null || currentActivity.SessionId != original.SessionId ||
                    currentActivity.Epoch != original.Epoch || currentActivity.State != original.State ||
                    currentActivity.AcquisitionPaused != original.AcquisitionPaused) return false;
            }
            else if (currentActivity != null) return false;
            if (value.Menu) return currentActivity != null;
            if (value.Frame is not { } frame || telemetry.Read(out _) == null) return false;
            var age = (DateTimeOffset.UtcNow - frame.LightCapturedAt).TotalMilliseconds;
            return age is >= -1000 and <= 500 && frame.ProducerAgeMs + Stopwatch.GetElapsedTime(value.At).TotalMilliseconds <= 500;
        }
    }

    // Mapper RGB already includes all output limits. Extract value once so the
    // protocol's explicit HSV brightness does not multiply that brightness twice.
    public static TuyaColorCommand FromOutput(Vec3 rgb)
    {
        if (!rgb.IsFinite) throw new CrimsonHueException("Invalid Tuya output color.");
        rgb = new(Math.Clamp(rgb.X, 0, 1), Math.Clamp(rgb.Y, 0, 1), Math.Clamp(rgb.Z, 0, 1));
        var value = Math.Max(rgb.X, Math.Max(rgb.Y, rgb.Z));
        return value <= 0 ? new(false, default, 0) : new(true, rgb / value, value);
    }

    public static bool SignificantChange(TuyaColorCommand? previous, TuyaColorCommand next) => previous == null ||
        previous.Power != next.Power || (next.Power &&
        (Math.Abs(previous.Brightness - next.Brightness) >= .02 - 1e-12 ||
         Math.Max(Math.Abs(previous.Rgb.X - next.Rgb.X), Math.Max(Math.Abs(previous.Rgb.Y - next.Rgb.Y),
             Math.Abs(previous.Rgb.Z - next.Rgb.Z))) >= 8.0 / 255 - 1e-12));

    private void PublishState(string status, IEnumerable<LampWorker> workers, int devices)
    {
        var values = workers.Select(w => (w.Lamp.Id, State: w.Snapshot)).ToArray();
        Volatile.Write(ref state, new(status, values.Any(w => w.State.Sending), devices,
            values.Sum(w => w.State.Acknowledged + w.State.Unacknowledged), values.Sum(w => w.State.Acknowledged),
            values.Sum(w => w.State.Unacknowledged), values.Sum(w => w.State.Failures),
            values.ToDictionary(w => w.Id, w => w.State.Color, StringComparer.Ordinal),
            values.ToDictionary(w => w.Id, w => w.State.Status, StringComparer.Ordinal)));
    }

    private sealed record Desired(TuyaColorCommand Color, bool Authorized, long At,
        ActivityFrame? Activity, TelemetryFrame? Frame, bool Menu);
    private sealed record LampState(string Status, bool Sending = false, Vec3 Color = default,
        long Acknowledged = 0, long Unacknowledged = 0, long Failures = 0, bool Retained = false);

    private sealed class LampWorker : IDisposable
    {
        private readonly CancellationTokenSource stop;
        private readonly Func<Desired, bool> stillAuthorized;
        private Desired? desired;
        private LampState state = new("Tuya · waiting for fresh CDT");
        public TuyaLamp Lamp { get; }
        public Task Task { get; }
        public LampState Snapshot => Volatile.Read(ref state);
        public void Publish(Desired value) => Volatile.Write(ref desired, value);
        public void Stop() => stop.Cancel();
        public void Dispose() => stop.Dispose();

        public LampWorker(TuyaLamp lamp, Func<string, string?> localKey,
            Func<TuyaLamp, string, Func<TuyaColorCommand?>, CancellationToken, Task<TuyaSendResult>> send,
            Func<TuyaLamp, string, CancellationToken, Task<TuyaDeviceState>> read,
            Func<TuyaLamp, string, TuyaDeviceState, CancellationToken, Task<TuyaWriteResult>> restore,
            Func<Desired, bool> stillAuthorized, CancellationToken token)
        {
            Lamp = lamp; this.stillAuthorized = stillAuthorized; stop = CancellationTokenSource.CreateLinkedTokenSource(token);
            Task = System.Threading.Tasks.Task.Run(() => RunAsync(localKey, send, read, restore));
        }

        private Desired? Current()
        {
            var next = Volatile.Read(ref desired);
            return next != null && (Stopwatch.GetElapsedTime(next.At).TotalMilliseconds > 500 || !stillAuthorized(next)) ?
                next with { Color = new(false, default, 0), Authorized = false } : next;
        }

        private async Task RunAsync(Func<string, string?> localKey,
            Func<TuyaLamp, string, Func<TuyaColorCommand?>, CancellationToken, Task<TuyaSendResult>> send,
            Func<TuyaLamp, string, CancellationToken, Task<TuyaDeviceState>> read,
            Func<TuyaLamp, string, TuyaDeviceState, CancellationToken, Task<TuyaWriteResult>> restore)
        {
            var token = stop.Token; TuyaColorCommand? previous = null;
            TuyaDeviceState? prior = null; Dictionary<int, JsonElement>? expected = null;
            var captureAttempted = false; var wrote = false; var ownershipLost = false;
            long lastAttempt = 0, retryAfter = 0, nextOwnershipCheck = 0;
            string? lastKey = null;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await System.Threading.Tasks.Task.Delay(25, token);
                    var next = Current(); var now = Stopwatch.GetTimestamp();
                    if (next == null || (!next.Authorized && !wrote) || now < retryAfter ||
                        (lastAttempt != 0 && Stopwatch.GetElapsedTime(lastAttempt, now) < MinimumInterval) ||
                        !SignificantChange(previous, next.Color)) continue;
                    try
                    {
                        var key = localKey(Lamp.Id);
                        if (key == null || Encoding.UTF8.GetByteCount(key) is not (16 or 24 or 32))
                        {
                            Set(Snapshot with { Status = "Tuya · local key missing or invalid", Sending = false });
                            retryAfter = After(RetryDelay); continue;
                        }
                        if (!captureAttempted)
                        {
                            captureAttempted = true;
                            try
                            {
                                var captured = await read(Lamp, key, token);
                                if (captured.Dps.ContainsKey(Lamp.PowerDps))
                                { prior = captured; expected = new(captured.Dps); }
                            }
                            catch (Exception ex) when (Recoverable(ex) && !token.IsCancellationRequested) { }
                        }
                        if (wrote && expected != null && now >= nextOwnershipCheck)
                        {
                            var actual = await read(Lamp, key, token);
                            if (!Matches(actual, expected))
                            {
                                ownershipLost = true;
                                Set(Snapshot with { Status = "Tuya stopped · another writer changed the lamp", Sending = false, Retained = true });
                                break;
                            }
                            nextOwnershipCheck = After(TimeSpan.FromSeconds(1));
                        }
                        // Queries may take time. Send the newest authorized color,
                        // never the one captured before waiting on a socket.
                        next = Current(); token.ThrowIfCancellationRequested();
                        if (next == null || (!next.Authorized && !wrote) || !SignificantChange(previous, next.Color)) continue;
                        lastAttempt = Stopwatch.GetTimestamp();
                        Desired? selected = null;
                        var sent = await send(Lamp, key, () =>
                        {
                            token.ThrowIfCancellationRequested();
                            selected = Current();
                            return selected == null || (!selected.Authorized && !wrote) ||
                                !SignificantChange(previous, selected.Color) ? null : selected.Color;
                        }, token);
                        if (sent.SentCommand == null || sent.Result == null) continue;
                        // Completion is conservative pacing: an arbitrarily slow
                        // handshake/ACK cannot compress the next physical write.
                        lastAttempt = Stopwatch.GetTimestamp();
                        wrote = true; lastKey = key; previous = sent.SentCommand;
                        var dps = TuyaProtocol.BuildDps(Lamp, sent.SentCommand);
                        if (expected != null) foreach (var value in dps) expected[value.Key] = value.Value;
                        var ambiguous = sent.Result == TuyaWriteResult.Unacknowledged;
                        Set(Snapshot with { Status = ambiguous ? "Tuya · command sent; acknowledgement unavailable" :
                            selected?.Authorized != true ? "Tuya · stale output cleared" : prior == null ?
                            "Tuya · streaming; prior state unavailable" : "Tuya · streaming",
                            Sending = selected?.Authorized == true, Color = sent.SentCommand.Rgb * sent.SentCommand.Brightness,
                            Acknowledged = Snapshot.Acknowledged + (ambiguous ? 0 : 1),
                            Unacknowledged = Snapshot.Unacknowledged + (ambiguous ? 1 : 0) });
                        if (ambiguous) retryAfter = After(RetryDelay);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                    catch (Exception ex) when (Recoverable(ex))
                    {
                        Set(Snapshot with { Status = "Tuya · retrying after transport failure", Sending = false,
                            Failures = Snapshot.Failures + 1 });
                        retryAfter = After(RetryDelay);
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            finally
            {
                if (!wrote) Set(Snapshot with { Status = "Tuya stopped · no lamp state changed", Sending = false });
                else if (ownershipLost) { }
                else if (prior == null || expected == null || lastKey == null)
                    Set(Snapshot with { Status = "Tuya stopped · last state retained; prior state unavailable", Sending = false, Retained = true });
                else
                {
                    try
                    {
                        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(6));
                        var remaining = MinimumInterval - Stopwatch.GetElapsedTime(lastAttempt);
                        if (remaining > TimeSpan.Zero)
                            await System.Threading.Tasks.Task.Delay(remaining, cleanup.Token);
                        var actual = await read(Lamp, lastKey, cleanup.Token);
                        if (!Matches(actual, expected))
                            Set(Snapshot with { Status = "Tuya stopped · last state retained; ownership changed", Sending = false, Retained = true });
                        else
                        {
                            var restored = await restore(Lamp, lastKey, prior, cleanup.Token);
                            Set(Snapshot with { Status = restored == TuyaWriteResult.Acknowledged ?
                                "Tuya stopped · prior state restored" : "Tuya stopped · restoration unconfirmed",
                                Sending = false, Retained = restored != TuyaWriteResult.Acknowledged });
                        }
                    }
                    catch (Exception ex) when (Recoverable(ex))
                    { Set(Snapshot with { Status = "Tuya stopped · last state retained; restoration unavailable", Sending = false, Retained = true }); }
                }
            }
        }

        private void Set(LampState value) => Volatile.Write(ref state, value);
        private static long After(TimeSpan delay) => Stopwatch.GetTimestamp() + (long)(delay.TotalSeconds * Stopwatch.Frequency);
        private static bool Matches(TuyaDeviceState actual, IReadOnlyDictionary<int, JsonElement> expected) =>
            expected.All(pair => actual.Dps.TryGetValue(pair.Key, out var value) && value.GetRawText() == pair.Value.GetRawText());
        private static bool Recoverable(Exception ex) => ex is IOException or SocketException or CrimsonHueException or
            CryptographicException or ArgumentException or InvalidOperationException or OperationCanceledException or UnauthorizedAccessException;
    }
}
