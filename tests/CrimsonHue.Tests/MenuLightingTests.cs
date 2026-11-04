using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrimsonHue.Core;

internal static class MenuLightingTests
{
    private static void Assert(bool condition, string message = "Menu lighting assertion failed")
    { if (!condition) throw new Exception(message); }
    private static long At(long start, double milliseconds) => start + (long)(Stopwatch.Frequency * milliseconds / 1000);
    internal static byte[] Activity(long sequence = 1, string state = "paused", long epoch = 1,
        DateTimeOffset? at = null, string session = "synthetic-session", double? age = 0, bool? paused = null) =>
        JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = "1.0", sequence, timestampUtc = at ?? DateTimeOffset.UtcNow,
            sessionId = session, state, source = "simulation-tick", observationAgeMilliseconds = age,
            acquisitionPaused = paused ?? state == "paused", epoch, reason = "synthetic" });
    private static ActivityFrame Frame(string state, DateTimeOffset utc, long epoch = 1, string session = "test") =>
        new(1, utc, session, state, 0, state == "paused", epoch, "synthetic");
    internal static async Task Publish(ActivityState state, CancellationToken token, Func<string>? mode = null)
    {
        long sequence = 0;
        while (!token.IsCancellationRequested)
        {
            state.Accept(Activity(++sequence, mode?.Invoke() ?? "paused"));
            try { await Task.Delay(40, token); } catch (OperationCanceledException) { }
        }
    }

    public static void Register(Action<string, Action> add, Action<string, Func<Task>> asyncTest)
    {
        add("Menu settings roundtrip preserves existing setup and protected pairing; old settings retain defaults", () =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "CrimsonHue-menu-test-" + Guid.NewGuid().ToString("N"));
            var store = new SettingsStore(directory);
            var menu = new MenuLightingSettings(false, new(.3, .4, .5), .17, 250);
            var saved = new AppSettings(BridgeAddress: "127.0.0.1", AreaId: "synthetic-area", Mapping: new(Brightness: .31),
                Shortcut: new('J'), Lighting: new(), OpenRgb: new(Enabled: false, Port: 12345), MenuLighting: menu);
            var credentials = new BridgeCredentials("127.0.0.1", "synthetic-menu-test-key", MockBridge.ClientKey, new string('B', 64));
            try
            {
                store.SaveCredentials(credentials); var protectedBefore = File.ReadAllBytes(Path.Combine(directory, "bridge.secrets"));
                store.SaveSettings(saved); Assert(store.LoadSettings() == saved);
                Assert(store.LoadCredentials() == credentials && File.ReadAllBytes(Path.Combine(directory, "bridge.secrets")).SequenceEqual(protectedBefore));
                Assert(!File.ReadAllText(Path.Combine(directory, "settings.json")).Contains(credentials.ApplicationKey));
                var legacy = JsonSerializer.Deserialize<AppSettings>("{\"BridgeAddress\":\"127.0.0.1\",\"AreaId\":\"old-area\"}")!;
                Assert(legacy.MenuLighting == null && (legacy.MenuLighting ?? new()).EffectiveColor == new Vec3(1, .65, .35));
                Assert((legacy.MenuLighting ?? new()).Enabled && (legacy.MenuLighting ?? new()).Brightness == .2);
            }
            finally
            {
                File.Delete(Path.Combine(directory, "settings.json")); File.Delete(Path.Combine(directory, "bridge.secrets")); Directory.Delete(directory);
            }
        });
        add("Activity contract validates schema, source, state, identity and finite observation age", () =>
        {
            Assert(ActivityParser.Parse(Activity(), out _) is { State: "paused", AcquisitionPaused: true });
            foreach (var change in new Action<JsonNode>[] {
                n => n["schemaVersion"] = "2.0", n => n["source"] = "screen",
                n => n["sequence"] = -1, n => n["epoch"] = -1, n => n["state"] = "menu",
                n => n["sessionId"] = "", n => n["observationAgeMilliseconds"] = -1 })
            {
                var n = JsonNode.Parse(Activity())!; change(n);
                Assert(ActivityParser.Parse(JsonSerializer.SerializeToUtf8Bytes(n), out _) == null);
            }
            var build = JsonNode.Parse(Activity())!; build["gameBuild"] = "unrecognized-diagnostic-label";
            Assert(ActivityParser.Parse(JsonSerializer.SerializeToUtf8Bytes(build), out _) != null);
        });
        add("Activity freshness combines producer age and receipt age; replay cannot extend authorization", () =>
        {
            var utc = DateTimeOffset.UtcNow; var tick = Stopwatch.GetTimestamp(); var state = new ActivityState();
            state.Accept(Activity(at: utc, age: 100), utc, tick);
            Assert(state.Read(out _, utc.AddMilliseconds(400), At(tick, 400)) != null);
            state.Accept(Activity(at: utc), utc.AddMilliseconds(400), At(tick, 400));
            Assert(state.Read(out _, utc.AddMilliseconds(401), At(tick, 401)) == null);
            state.Accept(Activity(2, at: utc.AddMilliseconds(600)), utc.AddMilliseconds(600), At(tick, 600));
            Assert(state.Read(out _, utc.AddMilliseconds(600), At(tick, 600)) != null);
            state.Reset("disconnect"); Assert(state.Read(out _) == null);
            state.Accept(Activity(age: null)); Assert(state.Read(out _) == null);
        });
        add("Activity rejects future/stale time and epoch regressions; new session can restart sequences", () =>
        {
            var utc = DateTimeOffset.UtcNow; var tick = Stopwatch.GetTimestamp(); var state = new ActivityState();
            state.Accept(Activity(10, epoch: 5, at: utc), utc, tick);
            state.Accept(Activity(11, "running", 4, utc), utc, tick);
            Assert(state.Read(out _, utc, tick)?.State == "paused");
            state.Accept(Activity(12, "unknown", 0, utc), utc, tick);
            Assert(state.Read(out _, utc, tick)?.State == "unknown", "Unknown publication could not revoke higher-epoch pause.");
            state.Accept(Activity(13, "paused", 4, utc), utc, tick);
            Assert(state.Read(out _, utc, tick)?.State == "unknown", "Unknown publication reset the epoch floor.");
            state.Accept(Activity(0, "running", 0, utc, "new-session"), utc, tick);
            Assert(state.Read(out _, utc, tick)?.State == "running");
            state.Accept(Activity(1, at: utc.AddSeconds(1), session: "new-session"), utc, tick);
            Assert(state.Read(out _, utc, tick) == null);
            state.Accept(Activity(2, at: utc.AddMilliseconds(-501), session: "new-session"), utc, tick);
            Assert(state.Read(out _, utc, tick) == null);
        });
        add("Menu policy clears pre-pause data, holds for fresh resume and blends without old Ambient", () =>
        {
            var utc = DateTimeOffset.UtcNow; var tick = Stopwatch.GetTimestamp();
            var local = TelemetryParser.Parse(Fixtures.Snapshot(at: utc.AddMilliseconds(-10)), out _)!;
            var ambient = AmbientParser.Parse(Fixtures.Ambient(utc.AddMilliseconds(-10)), out _)!;
            var policy = new MenuLightingPolicy(); var config = new MenuLightingSettings();
            var entered = policy.Evaluate(Frame("paused", utc), local, ambient, config, utc, tick);
            Assert(entered.UseMenuLight && entered.ResetMapper && entered.Frame == null && entered.Ambient == null);
            Assert(!policy.Evaluate(Frame("paused", utc), local, ambient, config, utc, tick).ResetMapper);
            var resumeUtc = utc.AddMilliseconds(100);
            var running = Frame("running", resumeUtc, 2);
            var waiting = policy.Evaluate(running, local, ambient, config, resumeUtc, At(tick, 100));
            Assert(waiting.UseMenuLight && waiting.ResetMapper && waiting.Frame == null);
            var freshLocal = local with { LightCapturedAt = resumeUtc.AddMilliseconds(1) };
            var started = policy.Evaluate(running, freshLocal, ambient, config, resumeUtc, At(tick, 200));
            Assert(!started.UseMenuLight && started.Frame != null && started.Ambient == null && started.GameplayBlend == 0);
            var half = policy.Evaluate(running, freshLocal, ambient, config, resumeUtc, At(tick, 450));
            Assert(Math.Abs(half.GameplayBlend - .5) < .00001);
            var complete = policy.Evaluate(running, freshLocal, ambient, config, resumeUtc, At(tick, 700));
            Assert(complete.GameplayBlend == 1);
        });
        add("Verified resume fixed hold is bounded to one second; authorization loss clears immediately", () =>
        {
            var utc = DateTimeOffset.UtcNow; var tick = Stopwatch.GetTimestamp();
            var policy = new MenuLightingPolicy(); var menu = new MenuLightingSettings();
            policy.Evaluate(Frame("paused", utc), null, null, menu, utc, tick);
            var running = Frame("running", utc.AddMilliseconds(1), 2);
            Assert(policy.Evaluate(running, null, null, menu, utc, At(tick, 10)).UseMenuLight);
            Assert(policy.Evaluate(running, null, null, menu, utc, At(tick, 1009)).UseMenuLight);
            Assert(!policy.Evaluate(running, null, null, menu, utc, At(tick, 1010)).CanOutput);
            policy.Evaluate(Frame("paused", utc, 3), null, null, menu, utc, tick);
            var disconnected = policy.Evaluate(null, null, null, menu, utc, tick);
            Assert(!disconnected.CanOutput && disconnected.ResetMapper);
            policy.Evaluate(Frame("paused", utc, 4), null, null, menu, utc, tick);
            Assert(!policy.Evaluate(Frame("unknown", utc, 5), null, null, menu, utc, tick).CanOutput);
        });
        add("Old endpoint and disabled menu preserve normal data; unconfirmed pause cannot authorize light", () =>
        {
            var local = TelemetryParser.Parse(Fixtures.Snapshot(), out _)!; var policy = new MenuLightingPolicy();
            Assert(policy.Evaluate(null, local, null, new()).Frame == local);
            Assert(policy.Evaluate(Frame("paused", DateTimeOffset.UtcNow) with { AcquisitionPaused = false }, null, null, new()).CanOutput == false);
            Assert(policy.Evaluate(Frame("paused", DateTimeOffset.UtcNow), local, null, new(Enabled: false)).Frame == local);
        });
        add("Session or epoch changes discard pre-transition captures even when pause was missed", () =>
        {
            var utc = DateTimeOffset.UtcNow; var local = TelemetryParser.Parse(Fixtures.Snapshot(at: utc.AddMilliseconds(-1)), out _)!;
            var policy = new MenuLightingPolicy(); var menu = new MenuLightingSettings();
            Assert(!policy.Evaluate(Frame("running", utc, 1), local, null, menu).CanOutput,
                "First known activity reused a cached capture older than its observation.");
            var changed = policy.Evaluate(Frame("running", utc, 3), local, null, menu);
            Assert(changed.ResetMapper && !changed.CanOutput && !changed.UseMenuLight);
            Assert(policy.Evaluate(Frame("running", utc, 0, "replacement"), local, null, menu).ResetMapper);
        });
        add("Menu color respects shared brightness, channel balance, OpenRGB limit and blend endpoints", () =>
        {
            var area = Fixtures.Area(); var menu = new MenuLightingSettings(Brightness: .8, Color: new(1, .5, .25));
            var shared = new MappingSettings(Brightness: .1); var decision = new MenuLightingDecision(null, null, true, true, 0);
            var colors = MenuLightingPolicy.Apply(decision, [], area, shared, menu);
            Assert(colors.All(c => Math.Abs(c.Rgb.X - .08) < 1e-9 && c.Rgb.Y == c.Rgb.X / 2));
            var rgbMapping = new OpenRgbLayout(Brightness: .02).Mapping(shared);
            Assert(MenuLightingPolicy.Apply(decision, [], area, rgbMapping, menu).All(c => c.Rgb.X <= .016000001));
            Assert(MenuLightingPolicy.Apply(decision, [], area, shared with { Brightness = 0 }, menu).All(c => c.Rgb == default));
            var normal = area.Channels.Select(c => new ChannelColor(c.Id, new(.02, .02, .02))).ToArray();
            var local = TelemetryParser.Parse(Fixtures.Snapshot(), out _)!;
            var half = MenuLightingPolicy.Apply(decision with { Frame = local, UseMenuLight = false, GameplayBlend = .5 }, normal, area, shared, menu);
            Assert(half.All(c => Math.Abs(c.Rgb.X - .05) < 1e-9));
            Assert(MenuLightingPolicy.Apply(decision with { Frame = local, UseMenuLight = false, GameplayBlend = 1 }, normal, area, shared, menu).SequenceEqual(normal));
            Assert(MenuLightingPolicy.Apply(new(null, null, false, true), normal, area, shared, menu).All(c => c.Rgb == default),
                "Unavailable output retained the caller's previous mapped colors.");
        });
        asyncTest("Hue resumes only new captures and discards pre-menu local and Ambient mapper history", async () =>
        {
            await using var mock = await MockBridge.StartAsync(); using var bridge = new BridgeClient(mock.Credentials);
            var activity = new ActivityState(); var telemetry = new TelemetryState(); var ambient = new AmbientState();
            var transport = new SpyTransport(); var session = new StreamingSession(bridge, () => transport);
            using var stop = new CancellationTokenSource(10000); using var publish = new CancellationTokenSource();
            var mode = "running"; var feed = Publish(activity, publish.Token, () => Volatile.Read(ref mode));
            telemetry.Accept(Fixtures.Snapshot()); ambient.Accept(Fixtures.Ambient());
            var task = session.RunAsync(Fixtures.Area(), telemetry, () => new(SmoothingMs: 1000, AmbientSmoothingMs: 1000), stop.Token,
                ambient, activity: activity, menuLighting: () => new(ResumeBlendMs: 0));
            try
            {
                await Fixtures.WaitUntil(() => session.Diagnostics is { LocalFresh: true, AmbientFill: > 0 });
                Volatile.Write(ref mode, "paused");
                await Fixtures.WaitUntil(() => session.Status.Contains("menu light"));
                Volatile.Write(ref mode, "running");
                await Fixtures.WaitUntil(() => activity.Read(out _)?.State == "running");
                await Task.Delay(50);
                Assert(session.Status.Contains("menu light"), "Resumed a cached pre-pause frame.");
                var empty = JsonNode.Parse(Fixtures.Snapshot(2))!; empty["lights"]!["rendered"]!["sources"] = new JsonArray();
                telemetry.Accept(JsonSerializer.SerializeToUtf8Bytes(empty));
                await Fixtures.WaitUntil(() => session.Diagnostics.LocalFresh && !session.Status.Contains("menu light"));
                Assert(session.Colors.All(c => c.Rgb == default) && !session.Diagnostics.AmbientFresh && session.Diagnostics.AmbientFill == 0,
                    "Pre-pause source or Ambient smoothing survived resume.");
            }
            finally { stop.Cancel(); publish.Cancel(); await feed; await task; }
        });
        asyncTest("Hue starts while paused, holds without capture, clears on activity loss and restores ownership", async () =>
        {
            await using var mock = await MockBridge.StartAsync(); using var bridge = new BridgeClient(mock.Credentials);
            var activity = new ActivityState(); var transport = new SpyTransport(); var session = new StreamingSession(bridge, () => transport);
            using var stop = new CancellationTokenSource(10000); using var publish = new CancellationTokenSource();
            var feed = Publish(activity, publish.Token);
            var task = session.RunAsync(Fixtures.Area(), new(), () => new(Brightness: .5), stop.Token,
                activity: activity, menuLighting: () => new(Brightness: .4));
            try
            {
                await Fixtures.WaitUntil(() => session.Colors.Count > 0 && session.Status.Contains("menu light"));
                Assert(session.Colors.All(c => Math.Abs(c.Rgb.X - .2) < 1e-9));
                await Task.Delay(1100);
                Assert(mock.Starts == 1 && mock.Stops == 0 && session.Sending, "Paused stream entered stale clear/reconnect loop.");
                Assert(session.Diagnostics is { MenuLightActive: true, MenuLightFrames: > 0, LocalClearedFrames: 0 },
                    "Menu colors were falsely counted as cleared output.");
                publish.Cancel(); await feed; activity.Reset("synthetic disconnect");
                await Fixtures.WaitUntil(() => session.Colors.Count > 0 && session.Colors.All(c => c.Rgb == default));
                await Fixtures.WaitUntil(() => mock.Stops == 1 && !session.Sending);
            }
            finally { stop.Cancel(); publish.Cancel(); await feed; await task; }
            Assert(mock.Restored == 1 && transport.Disposed);
        });
        asyncTest("Hue fresh menu activity does not bypass another Entertainment owner's lease", async () =>
        {
            await using var mock = await MockBridge.StartAsync(); mock.Active = true; mock.Owner = "other-app";
            using var bridge = new BridgeClient(mock.Credentials); var activity = new ActivityState(); activity.Accept(Activity());
            var transport = new SpyTransport(); var rejected = false;
            try { await new StreamingSession(bridge, () => transport).RunAsync(Fixtures.Area(), new(), () => new(), default, activity: activity); }
            catch (CrimsonHueException) { rejected = true; }
            Assert(rejected && mock.Starts == 0 && mock.Stops == 0 && mock.Restored == 0 && transport.Packets.IsEmpty);
        });
        asyncTest("Missing activity endpoint preserves old telemetry operation without authorizing menu output", async () =>
        {
            await using var mock = await MockBridge.StartAsync(); var activity = new ActivityState();
            var endpoint = ActivityClient.FromTelemetryEndpoint(new UriBuilder(mock.Address) { Scheme = "ws", Path = "/v1/stream" }.Uri);
            using var stop = new CancellationTokenSource(300);
            await new ActivityClient(activity).RunAsync(endpoint, stop.Token);
            Assert(activity.Read(out _) == null);
            var local = TelemetryParser.Parse(Fixtures.Snapshot(), out _)!;
            Assert(new MenuLightingPolicy().Evaluate(activity.Read(out _), local, null, new()).Frame == local);
        });
    }
}
