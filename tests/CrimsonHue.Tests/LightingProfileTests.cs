using CrimsonHue.Core;
using System.Text.Json;

static class LightingProfileTests
{
    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Reject(Action action)
    {
        try { action(); } catch (CrimsonHueException) { return; }
        throw new Exception("Expected invalid preferences to be rejected");
    }
    private static TelemetryFrame Frame(Vec3? source = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new(1, 1, now, now, 0, default,
            new(default, new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)),
            source is { } position ? [new(position, new(.8, .2, .1))] : []);
    }
    private static AmbientFrame Ambient(double level = 4) =>
        new(1, DateTimeOffset.UtcNow, 0, 0, level, new(level, level, level), new(level, level, level), 1);
    private static EntertainmentArea Area(params Vec3[] directions) => new(Fixtures.Id, "Synthetic profile room", false,
        directions.Select((d, i) => new EntertainmentChannel((byte)i, d, [new("profile-" + i, 0)], "Synthetic")).ToArray());
    private static double Peak(ChannelColor c) => Math.Max(c.Rgb.X, Math.Max(c.Rgb.Y, c.Rgb.Z));

    public static void Register(Action<string, Action> add)
    {
        add("Recommended lighting profile has neutral defaults and explicit response presets", () =>
        {
            var preferences = new LightingPreferences();
            Assert(preferences.Recommended && preferences.Revision == 1 && preferences.Response == LightingResponse.Balanced,
                "New installations must use the versioned balanced profile");
            var manual = new MappingSettings(AmbientCutoff: 50, AmbientFloor: .8, OutputGamma: 3,
                RedGain: 4, HueShiftDegrees: 80, CameraYawOffset: 23);
            var effective = LightingProfile.Resolve(preferences, manual);
            Assert(effective == new MappingSettings(Spread: 4, SourceDiscRadiusDegrees: 30, SourceDiscSoftness: .2,
                SmoothingMs: 250, AmbientSmoothingMs: 600, CameraYawOffset: 23),
                "Recommended profile inherited manual thresholds, color edits or other hidden controls");
            foreach (var (response, local, ambient) in new[] {
                (LightingResponse.Quick, 100.0, 400.0), (LightingResponse.Balanced, 250.0, 600.0), (LightingResponse.Calm, 500.0, 1000.0) })
            {
                var result = LightingProfile.Resolve(preferences with { Response = response }, manual);
                Assert(result.SmoothingMs == local && result.AmbientSmoothingMs == ambient, "Response preset changed");
            }
        });
        add("Recommended footprint separates nearby colored zones without changing the mixer", () =>
        {
            Vec3 Direction(double yaw) => new(Math.Sin(yaw * Math.PI / 180), Math.Cos(yaw * Math.PI / 180), 0);
            var area = Area(Direction(-20), Direction(20));
            var left = Direction(-30); var right = Direction(30);
            var frame = Frame() with { Lights = [
                new(new(left.X * 5, 0, left.Y * 5), new(1, 0, 0)),
                new(new(right.X * 5, 0, right.Y * 5), new(0, 0, 1))] };
            var mapping = LightingProfile.Resolve(new(), new()) with { SmoothingMs = 0 };
            var colors = new LightMapper().Map(frame, area, mapping, 1);
            var broad = new LightMapper().Map(frame, area, mapping with {
                SourceDiscRadiusDegrees = 60, SourceDiscSoftness = .25, Spread = 2 }, 1);
            Assert(broad[0].Rgb.Z > .01 && broad[1].Rgb.X > .01, "Control did not reproduce overlapping zones");
            Assert(colors[0].Rgb.X > .1 && colors[0].Rgb.Z == 0 &&
                colors[1].Rgb.Z > .1 && colors[1].Rgb.X == 0, "Recommended footprint mixed neighboring color zones");
        });
        add("Custom lighting and mode round trips preserve every manual value", () =>
        {
            var manual = new MappingSettings(Brightness: .42, Gain: .8, Radius: 77, Spread: 7, LocalStrength: 3,
                AmbientOutput: .7, AmbientCutoff: 1, AmbientFloor: .08, CameraYawOffset: -45,
                SourceDiscRadiusDegrees: 120, SourceDiscSoftness: .9, SmoothingMs: 731, AmbientSmoothingMs: 2300);
            var custom = LightingProfile.FromLegacy(manual);
            Assert(!custom.Recommended && custom.Brightness == .42 && custom.LocalAmount == 1 && custom.BackgroundAmount == .4,
                "Legacy preferences did not keep the cap and independent recommended amounts");
            Assert(ReferenceEquals(LightingProfile.Resolve(custom, manual), manual), "Custom mode rewrote manual settings");
            _ = LightingProfile.Resolve(custom with { Recommended = true }, manual);
            Assert(ReferenceEquals(LightingProfile.Resolve(custom, manual), manual) && manual.LocalStrength == 3 && manual.SmoothingMs == 731,
                "Returning to custom lost its independent manual values");
        });
        add("Lighting preferences persist separately and legacy custom settings require explicit opt-in", () =>
        {
            var manual = new MappingSettings(Brightness: .36, LocalStrength: 0, AmbientOutput: 0, AmbientSensitivity: 0,
                AmbientCutoff: 50, SmoothingMs: 0, CameraYawOffset: 17);
            var preferences = LightingProfile.FromLegacy(manual);
            var saved = new AppSettings(Mapping: manual, Lighting: preferences);
            var loaded = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(saved))!;
            Assert(loaded.Mapping == manual && loaded.Lighting == preferences, "Persistence merged manual and recommended settings");
            Assert(LightingProfile.Resolve(loaded.Lighting!, loaded.Mapping!) == manual, "Loading legacy manual choices changed output settings");
            var recommended = LightingProfile.Resolve(loaded.Lighting! with { Recommended = true }, loaded.Mapping!);
            Assert(recommended.Brightness == .36 && recommended.LocalStrength == 1 && recommended.AmbientOutput == .4 &&
                recommended.AmbientSensitivity == 1 && recommended.AmbientCutoff == 0 && recommended.CameraYawOffset == 17,
                "Explicit recommendation retained manual silencing or changed the personal cap/alignment");
            Assert(loaded.Mapping == manual, "Explicit opt-in mutated the stored custom configuration");
            var legacy = JsonSerializer.Deserialize<AppSettings>("{\"Mapping\":{\"Brightness\":0.42,\"AmbientOutput\":0}}")!;
            Assert(legacy.Lighting is null && !LightingProfile.FromLegacy(legacy.Mapping!).Recommended,
                "Old settings silently selected the recommended profile");
            var fresh = JsonSerializer.Deserialize<AppSettings>("{}")!;
            Assert(fresh.Mapping is null && fresh.Lighting is null && new LightingPreferences().Recommended,
                "Fresh settings cannot select the new recommended defaults");
        });
        add("Lighting preferences reject invalid scalars, response and revision", () =>
        {
            var baseline = new LightingPreferences();
            foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -.001 })
            {
                Reject(() => LightingProfile.Validate(baseline with { Brightness = invalid }));
                Reject(() => LightingProfile.Validate(baseline with { LocalAmount = invalid }));
                Reject(() => LightingProfile.Validate(baseline with { BackgroundAmount = invalid }));
            }
            Reject(() => LightingProfile.Validate(baseline with { Brightness = 1.001 }));
            Reject(() => LightingProfile.Validate(baseline with { LocalAmount = 2.001 }));
            Reject(() => LightingProfile.Validate(baseline with { BackgroundAmount = 1.001 }));
            Reject(() => LightingProfile.Validate(baseline with { Response = (LightingResponse)9 }));
            Reject(() => LightingProfile.Validate(baseline with { Revision = 0 }));
            Reject(() => LightingProfile.Validate(baseline with { Revision = 2 }));
            LightingProfile.Validate(baseline with { Brightness = 0, LocalAmount = 0, BackgroundAmount = 0 });
            LightingProfile.Validate(baseline with { Brightness = 1, LocalAmount = 2, BackgroundAmount = 1 });
        });
        add("Recommended mapping preserves black and removes unavailable Ambient", () =>
        {
            var mapping = LightingProfile.Resolve(new(), new()); var area = Area(new Vec3(0, 1, 0));
            var mapper = new LightMapper();
            Assert(Peak(mapper.Map(Frame(), area, mapping, 1, Ambient(0))[0]) == 0, "Profile invents a black-scene floor");
            Assert(Peak(mapper.Map(Frame(), area, mapping, 1, Ambient())[0]) > 0, "Fresh Ambient is missing");
            Assert(Peak(mapper.Map(Frame(), area, mapping, 1, null)[0]) == 0, "Missing Ambient retained old fill");
            Assert(LightingDiagnostics.Assess(null, Ambient(), mapping, [], true, false).Code == "local-unavailable",
                "Missing local frame was not diagnosed independently of fresh Ambient");
            Assert(LightingDiagnostics.Assess(Frame(), null, mapping, [], true, false).Code == "ambient-unavailable",
                "Missing Ambient was not diagnosed");
        });
        add("Recommended profile keeps all axis crossings continuous", () =>
        {
            var mapping = LightingProfile.Resolve(new(), new()) with { SmoothingMs = 0 };
            foreach (var (left, right, before, after) in new[] {
                (new Vec3(-.4, .8, 0), new Vec3(.4, .8, 0), new Vec3(-.0001, 1, 0), new Vec3(.0001, 1, 0)),
                (new Vec3(.8, -.4, 0), new Vec3(.8, .4, 0), new Vec3(1, -.0001, 0), new Vec3(1, .0001, 0)),
                (new Vec3(0, .8, -.4), new Vec3(0, .8, .4), new Vec3(0, 1, -.0001), new Vec3(0, 1, .0001)) })
            {
                var area = Area(left, right);
                var center = (before + after) / 2;
                var previousDelta = double.PositiveInfinity;
                // Sharper spatial gradients can exceed the old fixed difference
                // bound. A continuous handover must converge as the step shrinks,
                // without temporal smoothing concealing a hard axis boundary.
                foreach (var scale in new[] { 1.0, .1, .01 })
                {
                    var from = center + (before - center) * scale;
                    var to = center + (after - center) * scale;
                    var a = new LightMapper().Map(Frame(new(from.X, from.Z, from.Y)), area, mapping, 1);
                    var b = new LightMapper().Map(Frame(new(to.X, to.Z, to.Y)), area, mapping, 1);
                    var delta = a.Zip(b).Max(p => (p.First.Rgb - p.Second.Rgb).Length);
                    Assert(a.Concat(b).All(c => Peak(c) > 0) && delta < .01 && delta <= previousDelta * .2,
                        "Recommended profile introduced a discontinuous axis cutoff");
                    previousDelta = delta;
                }
            }
        });
        add("Basic lighting amounts are monotonic and honor the brightness cap", () =>
        {
            var area = Area(new Vec3(0, 1, 0));
            double priorAmbient = -1, priorLocal = -1;
            for (var i = 0; i <= 10; i++)
            {
                var mapping = LightingProfile.Resolve(new(Brightness: .3, BackgroundAmount: i / 10.0, LocalAmount: i / 5.0), new());
                var ambient = new LightMapper().Map(Frame(), area, mapping, 1, Ambient(100))[0];
                var local = new LightMapper().Map(Frame(new(0, 0, 1)), area, mapping, 1)[0];
                var mixed = new LightMapper().Map(Frame(new(0, 0, 1)), area, mapping, 1, Ambient(100))[0];
                Assert(Peak(ambient) >= priorAmbient && Peak(local) >= priorLocal, "Raising an isolated amount reduced its output");
                Assert(Peak(ambient) <= .3 && Peak(local) <= .3 && Peak(mixed) <= .3, "Profile exceeded the master cap");
                priorAmbient = Peak(ambient); priorLocal = Peak(local);
            }
        });
        add("Lighting diagnostics prioritize evidence without claiming lamp delivery", () =>
        {
            var mapping = new MappingSettings(); var frame = Frame(); var ambient = Ambient();
            ChannelColor[] colors = [new(0, new(.1, .2, .3))];
            LightingAssessment Check(MappingSettings? m = null, AmbientFrame? a = null, bool streaming = true, bool demo = false) =>
                LightingDiagnostics.Assess(frame, a ?? ambient, m ?? mapping, colors, streaming, demo);
            Assert(Check(mapping with { Brightness = 0 }, demo: true).Code == "demo", "Demo was presented as physical output");
            Assert(Check(mapping with { Brightness = 0, AmbientCutoff = 5 }).Code == "master-zero", "Master black was hidden behind a secondary cause");
            Assert(Check(mapping with { AmbientSensitivity = 0, AmbientCutoff = 5 }).Code == "ambient-disabled", "Explicit disable was misdiagnosed");
            Assert(Check(mapping with { AmbientOutput = 0 }).Code == "ambient-disabled", "Zero amount was misdiagnosed");
            var cutoff = Check(mapping with { AmbientCutoff = 4 });
            Assert(cutoff.Code == "ambient-cutoff" && cutoff.Warning, "Threshold-equal sample was not identified");
            var blockedSky = Ambient(0) with { SkyRgb = new(5, 4, 3), SkyVisibility = 0 };
            Assert(Check(a: blockedSky).Code == "sky-visibility-zero", "Zero visibility was conflated with a dark sky");
            Assert(Check(a: Ambient(0)).Code == "ambient-zero", "Zero upstream signal was misdiagnosed");
            Assert(Check(streaming: false).Code == "preview" && Check(streaming: false).Detail.Contains("Preview only"), "Preview claimed physical output");
            Assert(Check().Code == "output-active" && Check().Detail.Contains("do not confirm lamp delivery"), "Calculated values became a delivery claim");
            Assert(LightingDiagnostics.Assess(frame, ambient, mapping, [], true, false).Code == "no-output", "Empty channel output was reported healthy");
            Assert(LightingDiagnostics.Assess(frame, ambient, mapping, [new(0, default)], true, false).Code == "output-zero", "Black output was reported lit");
        });
    }
}
