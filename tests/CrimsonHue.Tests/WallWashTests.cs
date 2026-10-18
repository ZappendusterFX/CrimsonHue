using System.Buffers.Binary;
using System.Diagnostics;
using System.Text.Json;
using CrimsonHue.Core;

static class WallWashTests
{
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Reject(Action action) { try { action(); } catch { return; } throw new Exception("Expected rejection"); }
    private static async Task RejectAsync(Func<Task> action) { try { await action(); } catch { return; } throw new Exception("Expected rejection"); }
    private static readonly MappingSettings Settings = new(Brightness: 1, Gain: 10, FadeStart: 5, SmoothingMs: 0,
        SourceDiscRadiusDegrees: 10, SourceDiscSoftness: 0, DirectionNormalization: 1);
    private static EntertainmentArea Area => new(Fixtures.Id, "Two washers", false,
        Enumerable.Range(0, 6).Select(i => new EntertainmentChannel((byte)i, new(i < 3 ? -.4 : .4, .8, (i % 3 - 1) * .4),
            [new(i < 3 ? "left" : "right", i % 3)], i < 3 ? "Left" : "Right")).ToArray());
    private static WallWashLayout Layout => new([
        new(Fixtures.Id, "left", true, -60, 60, [new(0, Elevation: -30), new(1), new(2, Elevation: 30)]),
        new(Fixtures.Id, "right", true, 60, 60, [new(0, Elevation: -30), new(1), new(2, Elevation: 30)])]);
    private static TelemetryFrame Frame(double yaw, double elevation = 0)
    {
        var direction = WallWashLayout.Direction(yaw, elevation);
        return new(1, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 0, default,
            new(default, new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)), [new(new(direction.X, direction.Z, direction.Y), new(1e8, 0, 0))]);
    }
    public static void Register(Action<string, Action> add, Action<string, Func<Task>> asyncTest)
    {
        add("Horizontal washer zones map to distinct side segments using existing calibration", () =>
        {
            var horizontal = new WallWashLayout(Layout.Washers.Select(w => w with { HorizontalSpan = 10,
                Segments = [new(0, YawOffset: -20, VerticalSpan: 10), new(1, VerticalSpan: 10), new(2, YawOffset: 20, VerticalSpan: 10)] }).ToArray());
            foreach (var (yaw, index) in new[] {(-80,0),(-60,1),(-40,2),(40,3),(60,4),(80,5)})
            {
                var colors = new LightMapper().Map(Frame(yaw), Area, Settings with {SourceDiscRadiusDegrees=3}, 1, wallWash: horizontal);
                Assert(colors[index].Rgb.X > .1 && colors.Where((_,i)=>i!=index).All(c=>c.Rgb==default), "Horizontal zones mixed or changed channel order");
            }
        });
        add("Wall areas receive sideways light at visible wall rather than emitter position", () =>
        {
            var area = Area; var mapper = new LightMapper(); var frame = Frame(-85);
            Assert(mapper.Map(frame, area, Settings, 1).All(c => c.Rgb == default), "Point mapping unexpectedly reached the side wall");
            var output = mapper.Map(frame, area, Settings, 1, wallWash: Layout);
            Assert(output[1].Rgb.X > .1 && output.Skip(3).All(c => c.Rgb == default), "Outward left mapping missed or leaked right");
            Assert(mapper.Map(Frame(0), area, Settings, 1, wallWash: Layout).All(c => c.Rgb == default), "Narrow source filled the central wall gap");
            Assert(mapper.Map(Frame(150), area, Settings, 1, wallWash: Layout).All(c => c.Rgb == default), "Rear source leaked through strict boundary");
        });
        add("Wall area overlap stays proportional under strongest-channel compensation", () =>
        {
            // A uniform angular field isolates geometric coverage from focus.
            var settings = Settings with { Spread = 0 };
            var output = new LightMapper().Map(Frame(-60), Area, settings, 1, wallWash: Layout);
            // A +/-10-degree source covers one third of a 60-degree wall.
            var expected = 1.055 * Math.Pow(1.0 / 3, 1 / 2.4) - .055;
            Assert(Math.Abs(output[1].Rgb.X - expected) < 1e-10, "Normalization amplified a partial wall intersection");
            var right = new LightMapper().Map(Frame(60), Area, settings, 1, wallWash: Layout);
            Assert(Math.Abs(right[4].Rgb.X - expected) < 1e-10 && right.Take(3).All(c => c.Rgb == default), "Left/right response is asymmetric");
        });
        add("Segment calibration preserves channel IDs and independently maps height and side offsets", () =>
        {
            var layout = Layout.Set(Layout.Washers[0] with { HorizontalSpan = 0,
                Segments = [new(0, YawOffset: 30, Elevation: 30), new(1), new(2, Elevation: -30)] });
            var output = new LightMapper().Map(Frame(-30, 30), Area, Settings, 1, wallWash: layout);
            Assert(output[0].Rgb.X > .99 && output.Skip(1).All(c => c.Rgb == default), "Explicit segment placement was ignored");
            Assert(output.Select(c => c.Id).SequenceEqual(Area.Channels.Select(c => c.Id)), "Calibration changed wire channel IDs");
            Assert(Area.Channels[0].Position == new Vec3(-.4, .8, -.4), "Imported geometry was mutated");
        });
        add("Narrow sources cannot fall through wide wall sampling and wrapped rear areas remain continuous", () =>
        {
            var mapper = new LightMapper();
            for (var yaw = -89.0; yaw < -30; yaw += .7)
            {
                var colors = mapper.Map(Frame(yaw), Area, Settings with { SourceDiscRadiusDegrees = 1 }, 1, wallWash: Layout);
                Assert(colors[1].Rgb.X > 0 && colors.Skip(3).All(c => c.Rgb == default), "Narrow source fell through the wall or crossed sides");
            }
            var rear = Layout.Set(Layout.Washers[0] with { Yaw = 179, HorizontalSpan = 20 });
            foreach (var yaw in new[] { 179.0, -179.0, 175.0, -175.0 })
                Assert(mapper.Map(Frame(yaw), Area, Settings with { SeparateLeftRight = false, SourceDiscRadiusDegrees = 1 }, 1,
                    wallWash: rear)[1].Rgb.X > 0, "Rear wall broke at the -180/+180 seam");
        });
        add("Disabled and unrelated wall calibrations leave legacy point mapping identical", () =>
        {
            var frame = Frame(-30); var area = Area; var mapper = new LightMapper();
            var expected = mapper.Map(frame, area, Settings, 1);
            foreach (var layout in new[] { WallWashLayout.Empty, new(Layout.Washers.Select(w => w with { Enabled = false }).ToArray()),
                new(Layout.Washers.Select(w => w with { AreaId = Guid.NewGuid().ToString() }).ToArray()),
                new(Layout.Washers.Select(w => w with { ServiceId = "other-" + w.ServiceId }).ToArray()) })
                Assert(expected.SequenceEqual(mapper.Map(frame, area, Settings, 1, wallWash: layout)), "Unrelated room setup changed output");
        });
        add("A mixed room keeps ordinary point lights and calibrated washer segments independent", () =>
        {
            var point = new EntertainmentChannel(9, WallWashLayout.Direction(60, 0), [new("bulb", 0)], "Ordinary bulb");
            var area = Area with { Channels = Area.Channels.Take(3).Append(point).ToArray() };
            var layout = new WallWashLayout([Layout.Washers[0]]);
            var mapper = new LightMapper();
            var pointOutput = mapper.Map(Frame(60), area, Settings, 1, wallWash: layout);
            Assert(pointOutput.Single(c => c.Id == 9).Rgb.X > .99 && pointOutput.Take(3).All(c => c.Rgb == default),
                "Ordinary bulb was treated as a washer or leaked to the opposite wall");
            var wallOutput = mapper.Map(Frame(-85), area, Settings, 1, wallWash: layout);
            Assert(wallOutput.Single(c => c.Id == 1).Rgb.X > .1 && wallOutput.Single(c => c.Id == 9).Rgb == default,
                "Washer footprint was lost in a mixed area or leaked to the ordinary bulb");
            Assert(wallOutput.Select(c => c.Id).SequenceEqual(new byte[] { 0, 1, 2, 9 }), "Mixed mapping changed Hue channel identities");
        });
        add("Changed Hue segmentation preserves calibrated segments and holds new wall segments for review", () =>
        {
            var added = new EntertainmentChannel(9, WallWashLayout.Direction(-60, 0), [new("left", 3)], "Left");
            var area = Area with { Channels = Area.Channels.Append(added).ToArray() };
            var layout = Layout;
            var resolved = layout.GetOrCreate(area, "left");
            Assert(resolved.Enabled && resolved.Yaw == layout.Washers[0].Yaw && resolved.HorizontalSpan == layout.Washers[0].HorizontalSpan,
                "Membership refresh changed the existing wall setup");
            Assert(resolved.Segments.Take(3).SequenceEqual(layout.Washers[0].Segments) && resolved.Segments[3].NeedsReview,
                "Membership refresh lost calibrated values or silently accepted a new segment");
            Assert(layout.Washers[0].Segments.Count == 3, "Resolving membership mutated the saved layout");
            var samples = layout.Samples(area, added);
            Assert(samples is { Count: 0 }, "A new wall segment fell back to an imported point before review");
            var frame = Frame(-60);
            Assert(new LightMapper().Map(frame, area, Settings, 1).Single(c => c.Id == 9).Rgb.X > .99,
                "Regression fixture does not expose the imported-point fallback");
            var mapped = new LightMapper().Map(frame, area, Settings, 1, wallWash: layout);
            Assert(mapped.Single(c => c.Id == 9).Rgb == default, "Unreviewed segment received local light");
            Assert(mapped.Take(6).SequenceEqual(new LightMapper().Map(frame, Area, Settings, 1, wallWash: layout)),
                "Adding an unreviewed segment changed established channel output");
            Assert(mapped.SequenceEqual(new LightMapper().Map(frame, area, Settings, 1, wallWash: layout.Set(resolved))),
                "Live mapping and the resolved UI calibration disagree");
            Assert(mapped.Select(c => c.Id).SequenceEqual(area.Channels.Select(c => c.Id)), "Membership refresh changed channel identities");
        });
        add("Wall segment review gates local output while preserving Ambient and explicit point mode", () =>
        {
            var channel = new EntertainmentChannel(9, WallWashLayout.Direction(-60, 0), [new("left", 3)], "Left");
            var area = Area with { Channels = [channel] };
            var washer = Layout.GetOrCreate(area, "left") with { HorizontalSpan = 0 };
            var pending = Layout.Set(washer);
            Assert(washer.Segments.Single().NeedsReview, "Changed segment did not require review");
            var settings = Settings with { AmbientSensitivity = 1, AmbientSmoothingMs = 0, AmbientFloor = .1,
                AmbientOutput = .2, AmbientTintHue = 120, AmbientTintSaturation = 1 };
            var ambient = new AmbientFrame(42, DateTimeOffset.UtcNow, 0, 0, 1);
            var withAmbient = new LightMapper().Map(Frame(-60), area, settings, 1, ambient, pending).Single().Rgb;
            var ambientOnly = new LightMapper().Map(Frame(-60) with { Lights = [] }, area, settings, 1, ambient, pending).Single().Rgb;
            Assert(withAmbient == ambientOnly && withAmbient.Y > 0 && withAmbient.X == 0,
                "Review suppressed independent Ambient or let unconfirmed local output through");
            var confirmed = pending.Set(washer with { Segments = washer.Segments.Select(s => s with { NeedsReview = false }).ToArray() });
            Assert(confirmed.Samples(area, channel) is { Count: > 0 } &&
                new LightMapper().Map(Frame(-60), area, Settings, 1, wallWash: confirmed).Single().Rgb.X > .99,
                "Explicit segment confirmation did not enable calibrated mapping");
            var point = pending.Set(washer with { Enabled = false });
            Assert(point.GetOrCreate(area, "left").Segments.Single().NeedsReview && point.Samples(area, channel) is null,
                "Point mode erased review state or retained wall sampling");
            Assert(new LightMapper().Map(Frame(-60), area, Settings, 1, wallWash: point)
                .SequenceEqual(new LightMapper().Map(Frame(-60), area, Settings, 1)), "Point mode did not retain imported-position behavior");
        });
        add("Removed segments and replacement services cannot inherit unrelated wall calibration", () =>
        {
            var layout = Layout.Set(Layout.Washers[0] with { Segments = Layout.Washers[0].Segments.Select(s => s.Index == 1
                ? s with { YawOffset = 12, NeedsReview = true } : s).ToArray() });
            var reduced = Area with { Channels = Area.Channels.Where(c => c.Id is 1 or 2).ToArray() };
            var resolved = layout.GetOrCreate(reduced, "left");
            Assert(resolved.Segments.Select(s => s.Index).SequenceEqual(new[] { 1, 2 }) &&
                resolved.Segments.SequenceEqual(layout.GetOrCreate(Area, "left").Segments.Skip(1)),
                "Removed segments persisted in the active setup or surviving calibration/review state was lost");
            var replacement = reduced with { Channels = [new(1, WallWashLayout.Direction(30, 0), [new("replacement", 1)], "Replacement")] };
            var created = layout.GetOrCreate(replacement, "replacement");
            Assert(!created.Enabled && created.Segments.All(s => !s.NeedsReview) && Math.Abs(created.Yaw - 30) < 1e-10,
                "A replacement service inherited the old channel's wall geometry or review state");
            Assert(layout.Samples(replacement, replacement.Channels[0]) is null &&
                new LightMapper().Map(Frame(30), replacement, Settings, 1, wallWash: layout).Single().Rgb.X > .99,
                "An unrelated service was hijacked by saved wall calibration");
            var otherArea = Area with { Id = Guid.NewGuid().ToString() };
            var inOtherArea = layout.GetOrCreate(otherArea, "left");
            Assert(!inOtherArea.Enabled && inOtherArea.Segments.All(s => !s.NeedsReview),
                "A different entertainment area inherited local review state");
        });
        add("Wall calibration validates finite angles, keys, segments and exact axis directions", () =>
        {
            Layout.Validate(); var washer = Layout.Washers[0];
            Reject(() => new WallWashLayout([washer, washer]).Validate());
            Reject(() => new WallWashLayout([washer with { HorizontalSpan = -1 }]).Validate());
            Reject(() => new WallWashLayout([washer with { Yaw = double.NaN }]).Validate());
            Reject(() => new WallWashLayout([washer with { Segments = [new(0), new(0)] }]).Validate());
            Reject(() => new WallWashLayout([washer with { Segments = [new(0, VerticalSpan: double.PositiveInfinity)] }]).Validate());
            Assert(WallWashLayout.Direction(90, 0) == new Vec3(1, 0, 0), "Side axis acquired a false front/rear sign");
            var vertical = Layout.Set(washer with { HorizontalSpan = 0, Segments = [new(1, VerticalSpan: 90)] });
            var output = new LightMapper().Map(Frame(-60), Area, Settings with { SourceDiscRadiusDegrees = 60 }, 1, wallWash: vertical);
            Assert(output.All(c => c.Rgb.IsFinite && c.Rgb.X is >= 0 and <= 1), "Vertical footprint produced invalid output");
            var tiny = Layout.Set(washer with { HorizontalSpan = 1e-300, Segments = [new(1, VerticalSpan: 1e-300)] });
            var tinyOutput = new LightMapper().Map(Frame(-60), Area, Settings, 1, wallWash: tiny);
            Assert(tinyOutput[1].Rgb.IsFinite && tinyOutput[1].Rgb.X > .99, "Subprecision extent did not behave as a finite point");
        });
        add("Wall calibration round-trips locally, stays out of presets and migrates legacy settings", () =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "CrimsonHue-wall-test-" + Guid.NewGuid().ToString("N"));
            var store = new SettingsStore(directory);
            try
            {
                var layout = Layout.Set(Layout.Washers[0] with { Segments = Layout.Washers[0].Segments.Select(s => s.Index == 2
                    ? s with { NeedsReview = true } : s).ToArray() });
                store.SaveSettings(new(WallWash: layout, Shortcut: new(Key: 75), Mapping: Settings));
                var saved = store.LoadSettings(); saved.WallWash!.Validate();
                Assert(JsonSerializer.Serialize(saved.WallWash) == JsonSerializer.Serialize(layout) && saved.Shortcut!.Key == 75, "Local calibration did not persist");
                Assert(saved.WallWash.GetOrCreate(Area, "left").Segments.Single(s => s.Index == 2).NeedsReview,
                    "Reload or membership refresh silently confirmed an unreviewed segment");
                Assert(!JsonSerializer.Deserialize<WallWashSegment>("{\"Index\":2,\"YawOffset\":10,\"Elevation\":20,\"VerticalSpan\":5}")!.NeedsReview,
                    "Legacy calibrated segments unexpectedly require confirmation");
                var preset = MappingPreset.Export("Shared", saved.Mapping!);
                Assert(!preset.Contains("WallWash", StringComparison.OrdinalIgnoreCase) && !preset.Contains("ServiceId", StringComparison.OrdinalIgnoreCase), "Preset exposed local layout");
                var applied = saved with { Mapping = MappingPreset.Import(System.Text.Encoding.UTF8.GetBytes(preset)).ApplyTo(new(), PresetGroups.All) };
                Assert(applied.WallWash == saved.WallWash, "Preset changed room calibration");
                Assert(JsonSerializer.Deserialize<AppSettings>("{}")!.WallWash == null, "Legacy settings implicitly enabled wall mapping");
            }
            finally { File.Delete(Path.Combine(directory, "settings.json")); Directory.Delete(directory); }
        });
        asyncTest("Live session uses calibrated areas without changing bridge layout or packet channel IDs", async () =>
        {
            await using var mock = await MockBridge.StartAsync(); using var bridge = new BridgeClient(mock.Credentials);
            var transport = new SpyTransport(); var state = new TelemetryState(); state.Accept(Fixtures.Snapshot());
            var area = Fixtures.Area();
            var layout = new WallWashLayout([new(area.Id, "ent-light", true, -60, 60, [new(0), new(1)])]);
            var expectedFrame = TelemetryParser.Parse(Fixtures.Snapshot(), out _)!;
            var expected = EntertainmentPacket.Build(area.Id, 0, new LightMapper().Map(expectedFrame, area, Settings, 1, wallWash: layout));
            using var cancel = new CancellationTokenSource(10000);
            var run = new StreamingSession(bridge, () => transport).RunAsync(area, state, () => Settings, cancel.Token, wallWash: () => layout);
            await Fixtures.WaitUntil(() => transport.Packets.Count > 0); cancel.Cancel(); await run;
            Assert(transport.Packets.First().AsSpan(52).SequenceEqual(expected.AsSpan(52)), "Session ignored local wall calibration");
            Assert(mock.Stops == 1 && mock.Restored == 1, "Calibrated session failed cleanup or bridge layout validation");
        });
        asyncTest("Segment test is bounded, isolates one channel at 10%, clears and restores", async () =>
        {
            await using var mock = await MockBridge.StartAsync(); using var bridge = new BridgeClient(mock.Credentials);
            var transport = new SpyTransport(); var watch = Stopwatch.StartNew();
            await new StreamingSession(bridge, () => transport).IdentifyAsync(Fixtures.Area(), 5, default);
            Assert(watch.Elapsed.TotalSeconds is >= 1.9 and < 5, "Segment test duration was not bounded");
            var packets = transport.Packets.ToArray();
            Assert(packets.Length > 10 && mock.Starts == 1 && mock.Stops == 1 && mock.Restored == 1 && transport.Disposed, "Segment test lifecycle failed");
            foreach (var packet in packets.Take(packets.Length - 1))
                for (var offset = 52; offset < packet.Length; offset += 7)
                    for (var color = 1; color <= 5; color += 2)
                        Assert(BinaryPrimitives.ReadUInt16BigEndian(packet.AsSpan(offset + color, 2)) == (packet[offset] == 5 ? 6554 : 0), "Test illuminated another channel or exceeded 10%");
            Assert(packets.Last().Skip(52).Where((_, i) => i % 7 != 0).All(v => v == 0), "Test did not clear output");
        });
        asyncTest("Segment test refuses busy areas and restores after handshake failure or cancellation", async () =>
        {
            await using var mock = await MockBridge.StartAsync(); using var bridge = new BridgeClient(mock.Credentials);
            mock.Active = true; mock.Owner = "other-app";
            await RejectAsync(() => new StreamingSession(bridge).IdentifyAsync(Fixtures.Area(), 2, default));
            Assert(mock.Starts == 0 && mock.Stops == 0 && mock.Restored == 0, "Test took over another app");
            mock.Active = false;
            await RejectAsync(() => new StreamingSession(bridge, () => new SpyTransport { FailConnect = true }).IdentifyAsync(Fixtures.Area(), 2, default));
            Assert(mock.Stops == 1 && mock.Restored == 1, "Failed test handshake leaked its area");
            mock.StartDelayMs = 250; using var cancel = new CancellationTokenSource();
            var run = new StreamingSession(bridge, () => new SpyTransport()).IdentifyAsync(Fixtures.Area(), 2, cancel.Token);
            await Fixtures.WaitUntil(() => mock.Starts == 2); cancel.Cancel(); await run;
            Assert(mock.Stops == 2 && mock.Restored == 2 && !mock.Active, "Stop during activation leaked test ownership");
        });
        asyncTest("Segment test stops on takeover without restoring over another app", async () =>
        {
            await using var mock = await MockBridge.StartAsync(); using var bridge = new BridgeClient(mock.Credentials);
            var transport = new SpyTransport();
            var run = new StreamingSession(bridge, () => transport).IdentifyAsync(Fixtures.Area(), 2, default);
            await Fixtures.WaitUntil(() => transport.Packets.Count > 1); mock.Owner = "other-app";
            await RejectAsync(() => run);
            Assert(mock.Stops == 0 && mock.Restored == 0 && transport.Disposed, "Test overwrote another stream owner");
        });
    }
}
