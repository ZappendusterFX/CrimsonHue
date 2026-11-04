using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrimsonHue.Core;

internal static class TuyaStreamingTests
{
    private const string Key = "synthetic-key123";
    private static TuyaLamp Lamp(string id = "lamp-a", string host = "127.0.0.1") =>
        new(id, "Synthetic lamp", host, "device-" + id, new(0, 1, 0));
    private static void Assert(bool value, string message = "Tuya streaming assertion failed")
    { if (!value) throw new Exception(message); }
    private static void Reject(Action action)
    { try { action(); } catch (CrimsonHueException) { return; } throw new Exception("Expected Tuya configuration rejection"); }
    private static async Task Until(Func<bool> condition, int timeout = 5000)
    {
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            if (clock.ElapsedMilliseconds > timeout) throw new Exception("Timed out waiting for synthetic Tuya operation");
            await Task.Delay(10);
        }
    }

    public static void Register(Action<string, Action> add, Action<string, Func<Task>> asyncTest)
    {
        add("Tuya layout is opt-in, roundtrips without credentials and validates lamp identity/connection/DPS", () =>
        {
            var defaults = new TuyaLayout();
            Assert(!defaults.Enabled && defaults.Brightness == .25 && defaults.SourceRadiusDegrees == 90 && defaults.BackgroundAmount == .25);
            var lamp = Lamp();
            var config = new TuyaLayout(true, .6, [lamp], 125, .4);
            var json = JsonSerializer.Serialize(config);
            var loaded = JsonSerializer.Deserialize<TuyaLayout>(json)!; loaded.Validate();
            Assert(loaded.Devices!.Single() == lamp && loaded.Brightness == .6 && !json.Contains("Key"));
            foreach (var invalid in new[] {
                config with { Devices = [lamp, lamp] },
                config with { Devices = [lamp, lamp with { Id = "other", Host = "127.0.0.2" }] },
                config with { Devices = [lamp, lamp with { Id = "other", DeviceId = "other" }] },
                config with { Devices = [lamp with { Host = "http://localhost/path" }] },
                config with { Devices = [lamp with { Host = "::1" }] },
                config with { Devices = [lamp with { Protocol = "3.4" }] },
                config with { Devices = [lamp with { Port = 0 }] },
                config with { Devices = [lamp with { PowerDps = 0 }] },
                config with { Devices = [lamp with { ColorDps = lamp.ModeDps }] },
                config with { Devices = [lamp with { Position = new(double.NaN, 0, 0) }] },
                config with { Brightness = double.NaN }, config with { SourceRadiusDegrees = 181 },
                config with { BackgroundAmount = -1 } }) Reject(invalid.Validate);
            var lamps = Enumerable.Range(1, 33).Select(i => Lamp("lamp-" + i, "lamp" + i + ".example")).ToArray();
            (config with { Devices = lamps.Take(32).ToArray() }).Validate();
            Reject((config with { Devices = lamps }).Validate);
        });
        add("Tuya room mapping uses its own positions and caps menu brightness without double scaling", () =>
        {
            var left = Lamp("left") with { Position = new(-1, 0, 0) };
            var right = Lamp("right", "127.0.0.2") with { Position = new(1, 0, 0), Enabled = false };
            var config = new TuyaLayout(true, .25, [right, left], 90, .25);
            var shared = new MappingSettings(Brightness: .4, AmbientOutput: .8, AmbientFloor: .2);
            var mapping = config.Mapping(shared); var area = TuyaLayout.Area(config.Targets());
            Assert(area.Channels.Count == 1 && area.Channels[0].Position == left.Position);
            Assert(mapping.Brightness == .25 && mapping.AmbientOutput == .2 && mapping.AmbientFloor == .05);
            Assert(shared.Brightness == .4 && shared.AmbientOutput == .8);
            var rgb = MenuLightingPolicy.Apply(new(null, null, true, true), [], area, mapping,
                new(Brightness: .8, Color: new(1, .5, .25))).Single().Rgb;
            var command = TuyaStreamingSession.FromOutput(rgb);
            Assert(command.Power && command.Rgb == new Vec3(1, .5, .25) && Math.Abs(command.Brightness - .2) < 1e-12);
            Assert(command.Rgb * command.Brightness == rgb);
            Assert(!TuyaStreamingSession.FromOutput(default).Power);
        });
        add("Tuya deduplication respects RGB8/255 and two-point brightness boundaries and always sends power changes", () =>
        {
            var color = new TuyaColorCommand(true, new(1, .5, 0), .5);
            Assert(!TuyaStreamingSession.SignificantChange(color, color with { Rgb = new(1, .5 + 7.0 / 255, 0), Brightness = .519 }));
            Assert(TuyaStreamingSession.SignificantChange(color, color with { Rgb = new(1, .5 + 8.0 / 255, 0) }));
            Assert(TuyaStreamingSession.SignificantChange(color, color with { Brightness = .52 }));
            Assert(TuyaStreamingSession.SignificantChange(color, new(false, default, 0)));
            Assert(!TuyaStreamingSession.SignificantChange(new(false, default, 0), new(false, new(1, 1, 1), .5)));
        });
        asyncTest("Tuya disabled, missing-key and no-fresh-input starts never query or write lamps", async () =>
        {
            foreach (var mode in new[] { "disabled", "key", "input" })
            {
                var fake = new Fake(); var session = fake.Session(); var activity = new ActivityState();
                if (mode != "input") activity.Accept(MenuLightingTests.Activity());
                using var cancel = new CancellationTokenSource(180);
                await session.RunAsync(new(), new(), () => new(), cancel.Token,
                    () => new(Enabled: mode != "disabled", Devices: [Lamp()]), _ => mode == "key" ? null : Key, activity);
                Assert(fake.Reads == 0 && fake.Writes.IsEmpty && fake.Restores == 0, "Unauthorized startup touched a lamp.");
            }
        });
        asyncTest("Tuya starts on fresh paused activity, deduplicates steady menu RGB and restores still-owned prior state", async () =>
        {
            var fake = new Fake(); var session = fake.Session(); var activity = new ActivityState();
            using var stop = new CancellationTokenSource(8000); using var publish = new CancellationTokenSource();
            var feed = MenuLightingTests.Publish(activity, publish.Token);
            var run = session.RunAsync(new(), new(), () => new(Brightness: .4), stop.Token,
                () => new(true, .25, [Lamp()]), _ => Key, activity, () => new(Brightness: .8));
            try
            {
                await Until(() => fake.Writes.Count == 1);
                var command = fake.Writes.Single().Color;
                Assert(command.Rgb == new Vec3(1, .65, .35) && Math.Abs(command.Brightness - .2) < 1e-12);
                await Task.Delay(700);
                Assert(fake.Writes.Count == 1 && session.State.Sending && session.State.Status.Contains("menu light"));
            }
            finally { stop.Cancel(); publish.Cancel(); await feed; await run; }
            Assert(fake.Restores == 1 && session.State.DeviceStatuses!["lamp-a"].Contains("prior state restored"));
        });
        asyncTest("Tuya activity loss clears output and resumed gameplay cannot reuse pre-menu local or Ambient frames", async () =>
        {
            var fake = new Fake(); var session = fake.Session(); var activity = new ActivityState();
            var telemetry = new TelemetryState(); var ambient = new AmbientState(); var mode = "paused";
            telemetry.Accept(Fixtures.Snapshot()); ambient.Accept(Fixtures.Ambient());
            using var stop = new CancellationTokenSource(8000); using var publish = new CancellationTokenSource();
            var feed = MenuLightingTests.Publish(activity, publish.Token, () => Volatile.Read(ref mode));
            var run = session.RunAsync(telemetry, ambient, () => new(SmoothingMs: 1000, AmbientSmoothingMs: 1000), stop.Token,
                () => new(true, .25, [Lamp()]), _ => Key, activity, () => new(ResumeBlendMs: 0));
            try
            {
                await Until(() => fake.Writes.Count == 1);
                Volatile.Write(ref mode, "running"); await Until(() => activity.Read(out _)?.State == "running");
                await Task.Delay(120);
                Assert(fake.Writes.Count == 1, "Resumed a cached pre-menu frame.");
                var empty = JsonNode.Parse(Fixtures.Snapshot(2))!; empty["lights"]!["rendered"]!["sources"] = new JsonArray();
                telemetry.Accept(JsonSerializer.SerializeToUtf8Bytes(empty));
                await Until(() => fake.Writes.Any(w => !w.Color.Power));
                Assert(fake.Writes.Where(w => w.Color.Power).Count() == 1, "Old Ambient or local history survived pause.");
                Volatile.Write(ref mode, "paused"); await Until(() => fake.Writes.Count(w => w.Color.Power) == 2);
                publish.Cancel(); await feed; activity.Reset("synthetic disconnect");
                await Until(() => fake.Writes.Count(w => !w.Color.Power) == 2);
            }
            finally { stop.Cancel(); publish.Cancel(); await feed; await run; }
        });
        asyncTest("Tuya replaces intermediate colors and spaces writes at least 300 ms apart", async () =>
        {
            var fake = new Fake { BeforeWrite = async (_, _, token) =>
            { await Task.Delay(400, token); return TuyaWriteResult.Acknowledged; } };
            var session = fake.Session(); var activity = new ActivityState();
            var menu = new MenuLightingSettings(Color: new(1, 0, 0));
            using var stop = new CancellationTokenSource(8000); using var publish = new CancellationTokenSource();
            var feed = MenuLightingTests.Publish(activity, publish.Token);
            var run = session.RunAsync(new(), new(), () => new(), stop.Token, () => new(true, Devices: [Lamp()]), _ => Key,
                activity, () => Volatile.Read(ref menu));
            try
            {
                await Until(() => fake.Writes.Count == 1);
                Volatile.Write(ref menu, menu with { Color = new(0, 1, 0) }); await Task.Delay(80);
                Volatile.Write(ref menu, menu with { Color = new(0, 0, 1) });
                await Until(() => fake.Writes.Count == 2);
                var commands = fake.Writes.ToArray();
                Assert(commands[1].Color.Rgb == new Vec3(0, 0, 1), "Queued an obsolete intermediate color.");
                Assert(Stopwatch.GetElapsedTime(commands[0].At, commands[1].At).TotalMilliseconds >= 695,
                    "A slow acknowledgement compressed the next write's minimum interval.");
                await Until(() => session.State.Frames >= 2);
            }
            finally { stop.Cancel(); publish.Cancel(); await feed; await run; }
        });
        asyncTest("Tuya failed lamp backs off independently while another lamp continues receiving latest colors", async () =>
        {
            var failed = new ConcurrentQueue<long>();
            var fake = new Fake { BeforeWrite = async (lamp, _, token) =>
            {
                if (lamp.Id != "broken") return TuyaWriteResult.Acknowledged;
                await Task.Delay(500, token); failed.Enqueue(Stopwatch.GetTimestamp());
                throw new IOException("synthetic private detail must never reach state");
            } };
            var session = fake.Session(); var activity = new ActivityState();
            var menu = new MenuLightingSettings(Color: new(1, 0, 0));
            using var stop = new CancellationTokenSource(9000); using var publish = new CancellationTokenSource();
            var feed = MenuLightingTests.Publish(activity, publish.Token);
            var run = session.RunAsync(new(), new(), () => new(), stop.Token,
                () => new(true, Devices: [Lamp("broken"), Lamp("healthy", "127.0.0.2")]), _ => Key, activity,
                () => Volatile.Read(ref menu));
            try
            {
                await Until(() => fake.Writes.Any(w => w.Id == "healthy"));
                Volatile.Write(ref menu, menu with { Color = new(0, 0, 1) });
                await Until(() => fake.Writes.Count(w => w.Id == "healthy") == 2, 1500);
                await Until(() => failed.Count >= 2);
                var attempts = fake.Writes.Where(w => w.Id == "broken").ToArray();
                Assert(Stopwatch.GetElapsedTime(failed.First(), attempts[1].At).TotalMilliseconds >= 2450);
                Assert(!JsonSerializer.Serialize(session.State).Contains("private detail"));
            }
            finally { stop.Cancel(); publish.Cancel(); await feed; await run; }
        });
        asyncTest("Tuya unacknowledged writes avoid immediate retry and remain eligible for checked restoration", async () =>
        {
            var fake = new Fake { BeforeWrite = (_, _, _) => Task.FromResult(TuyaWriteResult.Unacknowledged) };
            var session = fake.Session(); var activity = new ActivityState();
            var menu = new MenuLightingSettings(Color: new(1, 0, 0));
            using var stop = new CancellationTokenSource(8000); using var publish = new CancellationTokenSource();
            var feed = MenuLightingTests.Publish(activity, publish.Token);
            var run = session.RunAsync(new(), new(), () => new(), stop.Token, () => new(true, Devices: [Lamp()]), _ => Key,
                activity, () => Volatile.Read(ref menu));
            try
            {
                await Until(() => fake.Writes.Count == 1); await Task.Delay(150);
                Volatile.Write(ref menu, menu with { Color = new(0, 0, 1) });
                await Task.Delay(650); Assert(fake.Writes.Count == 1);
                await Until(() => fake.Writes.Count == 2);
                var sent = fake.Writes.ToArray();
                Assert(Stopwatch.GetElapsedTime(sent[0].At, sent[1].At).TotalMilliseconds >= 2450);
            }
            finally { stop.Cancel(); publish.Cancel(); await feed; await run; }
            Assert(fake.Restores == 1);
        });
        asyncTest("Tuya skips restoration after an external edit and reports retained state when query is unsupported", async () =>
        {
            foreach (var unsupported in new[] { false, true })
            {
                var fake = new Fake { ReadUnavailable = unsupported }; var session = fake.Session(); var activity = new ActivityState();
                using var stop = new CancellationTokenSource(8000); using var publish = new CancellationTokenSource();
                var feed = MenuLightingTests.Publish(activity, publish.Token);
                var run = session.RunAsync(new(), new(), () => new(), stop.Token,
                    () => new(true, Devices: [Lamp()]), _ => Key, activity);
                try
                {
                    await Until(() => fake.Writes.Count == 1);
                    if (!unsupported) fake.ExternalEdit(Lamp(), new(true, new(0, 1, 0), .8));
                }
                finally { stop.Cancel(); publish.Cancel(); await feed; await run; }
                Assert(fake.Restores == 0 && session.State.Status.Contains("retained"));
                Assert(session.State.DeviceStatuses!["lamp-a"].Contains(unsupported ? "prior state unavailable" : "ownership changed"));
            }
        });
        asyncTest("Tuya Stop after a fully sent unacknowledged command still checks ownership and restores with pacing", async () =>
        {
            var fake = new Fake { BeforeWrite = async (_, _, token) =>
            {
                try { await Task.Delay(Timeout.Infinite, token); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                return TuyaWriteResult.Unacknowledged;
            } };
            var session = fake.Session(); var activity = new ActivityState(); activity.Accept(MenuLightingTests.Activity());
            using var stop = new CancellationTokenSource(8000);
            var run = session.RunAsync(new(), new(), () => new(), stop.Token,
                () => new(true, Devices: [Lamp()]), _ => Key, activity);
            await Until(() => fake.Writes.Count == 1); stop.Cancel(); await run;
            Assert(fake.Restores == 1 && session.State.Unacknowledged == 1);
            Assert(Stopwatch.GetElapsedTime(fake.Writes.Single().At, fake.RestoredAt).TotalMilliseconds >= 295);
        });
        asyncTest("Tuya rechecks expired menu authorization after a slow handshake before its first control packet", async () =>
        {
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var fake = new Fake { BeforeHandshake = (_, token) => release.Task.WaitAsync(token) };
            var session = fake.Session(); var activity = new ActivityState(); activity.Accept(MenuLightingTests.Activity());
            using var stop = new CancellationTokenSource(8000);
            var run = session.RunAsync(new(), new(), () => new(), stop.Token,
                () => new(true, Devices: [Lamp()]), _ => Key, activity);
            try
            {
                await Until(() => fake.Handshakes == 1); await Task.Delay(600); release.SetResult();
                await Until(() => fake.Skipped == 1);
                Assert(fake.Writes.IsEmpty, "A color whose pause authorization expired during handshake was sent.");
            }
            finally { stop.Cancel(); release.TrySetResult(); await run; }
            Assert(fake.Restores == 0);
        });
        asyncTest("Tuya resamples a new menu epoch after handshake and records the exact sent color for restoration", async () =>
        {
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var fake = new Fake { BeforeHandshake = (_, token) => release.Task.WaitAsync(token) };
            var session = fake.Session(); var activity = new ActivityState(); activity.Accept(MenuLightingTests.Activity());
            var menu = new MenuLightingSettings(Color: new(1, 0, 0));
            using var stop = new CancellationTokenSource(8000);
            var run = session.RunAsync(new(), new(), () => new(), stop.Token,
                () => new(true, Devices: [Lamp()]), _ => Key, activity, () => Volatile.Read(ref menu));
            try
            {
                await Until(() => fake.Handshakes == 1); await Task.Delay(600);
                Volatile.Write(ref menu, menu with { Color = new(0, 0, 1) });
                activity.Accept(MenuLightingTests.Activity(2, epoch: 2)); await Task.Delay(100);
                release.SetResult(); await Until(() => fake.Writes.Count == 1);
                Assert(fake.Writes.Single().Color.Rgb == new Vec3(0, 0, 1), "Sent the pre-handshake menu color.");
            }
            finally { stop.Cancel(); release.TrySetResult(); await run; }
            Assert(fake.Restores == 1 && session.State.DeviceStatuses!["lamp-a"].Contains("prior state restored"),
                "Ownership bookkeeping used the old proposed command instead of the command actually sent.");
        });
    }

    private sealed record Write(string Id, TuyaColorCommand Color, long At);
    private sealed class Fake
    {
        private readonly object gate = new();
        private readonly Dictionary<string, Dictionary<int, JsonElement>> current = [];
        public readonly ConcurrentQueue<Write> Writes = new();
        public int Reads, Restores, Handshakes, Skipped;
        public long RestoredAt;
        public bool ReadUnavailable;
        public Func<TuyaLamp, CancellationToken, Task>? BeforeHandshake;
        public Func<TuyaLamp, TuyaColorCommand, CancellationToken, Task<TuyaWriteResult>>? BeforeWrite;
        public TuyaStreamingSession Session() => new(Send, Read, Restore);
        private Dictionary<int, JsonElement> Get(TuyaLamp lamp)
        {
            if (!current.TryGetValue(lamp.Id, out var values))
                current[lamp.Id] = values = new() { [lamp.PowerDps] = JsonSerializer.SerializeToElement(false),
                    [lamp.ModeDps] = JsonSerializer.SerializeToElement("white"),
                    [lamp.ColorDps] = JsonSerializer.SerializeToElement("000003e801f4"),
                    [lamp.WhiteBrightnessDps] = JsonSerializer.SerializeToElement(100),
                    [lamp.ColorTemperatureDps] = JsonSerializer.SerializeToElement(400) };
            return values;
        }
        public void ExternalEdit(TuyaLamp lamp, TuyaColorCommand command)
        { lock (gate) foreach (var pair in TuyaProtocol.BuildDps(lamp, command)) Get(lamp)[pair.Key] = pair.Value; }
        private async Task<TuyaSendResult> Send(TuyaLamp lamp, string key, Func<TuyaColorCommand?> latest, CancellationToken token)
        {
            Assert(key == Key); Interlocked.Increment(ref Handshakes);
            if (BeforeHandshake != null) await BeforeHandshake(lamp, token);
            var color = latest();
            if (color == null) { Interlocked.Increment(ref Skipped); return new(null, null); }
            Writes.Enqueue(new(lamp.Id, color, Stopwatch.GetTimestamp()));
            var result = BeforeWrite == null ? TuyaWriteResult.Acknowledged : await BeforeWrite(lamp, color, token);
            ExternalEdit(lamp, color); return new(result, color);
        }
        private Task<TuyaDeviceState> Read(TuyaLamp lamp, string key, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); Interlocked.Increment(ref Reads);
            if (ReadUnavailable) throw new IOException("synthetic unsupported state query");
            lock (gate) return Task.FromResult(new TuyaDeviceState(new Dictionary<int, JsonElement>(Get(lamp))));
        }
        private Task<TuyaWriteResult> Restore(TuyaLamp lamp, string key, TuyaDeviceState prior, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); Interlocked.Increment(ref Restores);
            RestoredAt = Stopwatch.GetTimestamp();
            lock (gate) current[lamp.Id] = new(prior.Dps);
            return Task.FromResult(TuyaWriteResult.Acknowledged);
        }
    }
}
