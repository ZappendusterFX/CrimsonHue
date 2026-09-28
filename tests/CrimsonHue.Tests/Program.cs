using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrimsonHue.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

// Explicit read-only diagnostic: use the paired user's protected store in memory.
// Never print credentials, full API responses or stream ownership tokens.
if (args.Contains("--inspect-layout"))
{
    var store = new SettingsStore();
    var credentials = store.LoadCredentials() ?? throw new Exception("Pair CrimsonHue first.");
    using var bridge = new BridgeClient(credentials);
    foreach (var layout in await bridge.GetAreasAsync())
    {
        Console.WriteLine($"Area: {layout.Name}; active: {layout.Active}");
        foreach (var channel in layout.Channels)
            Console.WriteLine($"  Channel {channel.Id}: {channel.Label}; position {channel.Position}");
    }
    return 0;
}

var tests = new List<(string Name, Func<Task> Run)>();
void Add(string name, Action run) => tests.Add((name, () => { run(); return Task.CompletedTask; }));
void Async(string name, Func<Task> run) => tests.Add((name, run));
void Assert(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
void Reject(Action action) { try { action(); } catch { return; } throw new Exception("Expected rejection"); }
async Task RejectAsync(Func<Task> action) { try { await action(); } catch { return; } throw new Exception("Expected rejection"); }
var area = Fixtures.Area();
var rearArea = area with { Channels = area.Channels.Select(c => c with { Position = new(c.Position.X, -c.Position.Y, c.Position.Z) }).ToArray() };
Add("Parse current rendered feed without depending on authored availability", () =>
{
    var frame = TelemetryParser.Parse(Fixtures.Snapshot(), out _);
    Assert(frame?.Lights.Count == 2);
    Assert(frame!.Camera.Position == new Vec3(0, 2, 0));
});
Add("Accept additive schema fields and ignore authored RGB", () =>
{
    var node = JsonNode.Parse(Fixtures.CurrentSnapshot())!;
    node["schemaVersion"] = "1.9"; node["futureAmbientField"] = 123;
    node["lights"]!["sources"] = new JsonArray(new JsonObject { ["colorLinear"] = new JsonObject { ["x"] = 99999, ["y"] = 99999, ["z"] = 99999 } });
    Assert(TelemetryParser.Parse(Encoding.UTF8.GetBytes(node.ToJsonString()), out _)!.Lights.Count == 1);
});
foreach (var (name, change) in new (string, Action<JsonNode>)[]
{
    ("Unknown schema major", n => n["schemaVersion"] = "2.0"),
    ("Old schema without rendered lights", n => n["schemaVersion"] = "1.1"),
    ("Unavailable game", n => n["game"]!["state"] = "loading"),
    ("Invalid axes", n => n["coordinateSystem"]!["upAxis"] = "z"),
    ("Missing capability", n => n["capabilities"] = new JsonArray()),
    ("Unavailable light feed", n => n["lights"]!["rendered"]!["status"] = "unavailable"),
    ("Producer age beyond 500 ms", n => n["lights"]!["rendered"]!["ageMilliseconds"] = 501),
    ("Invalid camera basis", n => n["lights"]!["rendered"]!["camera"]!["right"]!["x"] = 0),
    ("Negative RGB", n => n["lights"]!["rendered"]!["sources"]![0]!["colorLinear"]!["x"] = -1),
    ("Negative sequence", n => n["sequence"] = -1)
}) Add("Reject: " + name, () =>
{
    var node = JsonNode.Parse(Fixtures.Snapshot())!; change(node);
    Assert(TelemetryParser.Parse(Encoding.UTF8.GetBytes(node.ToJsonString()), out _) == null);
});
Add("Valid empty feed remains available", () =>
{
    var node = JsonNode.Parse(Fixtures.Snapshot())!; node["lights"]!["rendered"]!["sources"] = new JsonArray();
    Assert(TelemetryParser.Parse(Encoding.UTF8.GetBytes(node.ToJsonString()), out _)?.Lights.Count == 0);
});
Add("Schema 1.6 uses paired all-around lights and only fresh clear physics verdicts", () =>
{
    var frame = TelemetryParser.Parse(Fixtures.CurrentSnapshot(), out _)!;
    Assert(frame.Feed == "all-around" && frame.SourceCount == 4 && frame.Lights.Count == 1);
    Assert(frame.Lights[0].Position == new Vec3(0, 2, -8), "Behind-camera clear source was lost");
    Assert(frame.Lights[0].VisibilityAgeMs == 100);
    var mapped = new LightMapper().Map(frame, rearArea, new(Radius: 35, SmoothingMs: 0), 0.1);
    Assert(mapped.Any(c => c.Rgb.Length > 0), "All-around source did not reach the mapper");
    Assert(new LightMapper().Map(frame, area, new(), 0.1).All(c => c.Rgb == default), "Rear source reached the front-only layout");
});
Add("Schema 1.6 never falls back to rendered when all-around input is unavailable", () =>
{
    var node = JsonNode.Parse(Fixtures.CurrentSnapshot())!;
    node["lights"]!["upstream"]!["status"] = "unavailable";
    Assert(TelemetryParser.Parse(Encoding.UTF8.GetBytes(node.ToJsonString()), out _) == null);
});
Add("Schema 1.6 rejects unpaired all-around captures", () =>
{
    var node = JsonNode.Parse(Fixtures.CurrentSnapshot())!;
    node["lights"]!["upstream"]!["frameNumber"] = 56;
    Assert(TelemetryParser.Parse(Encoding.UTF8.GetBytes(node.ToJsonString()), out _) == null);
});
Add("Confirmed visibility expires independently of a fresh light capture", () =>
{
    var node = JsonNode.Parse(Fixtures.CurrentSnapshot())!;
    node["lights"]!["upstream"]!["sources"]![0]!["sourceVisibility"]!["volumeAgeMillisecondsAtCapture"] = 490;
    var state = new TelemetryState(); state.Accept(Encoding.UTF8.GetBytes(node.ToJsonString()));
    Assert(state.Read(out _)!.Lights.Count == 1);
    Assert(state.Read(out var message, DateTimeOffset.UtcNow.AddMilliseconds(40))!.Lights.Count == 0);
    Assert(message.Contains("0/4 confirmed visible"));
});
Add("Schema 1.6 treats missing or non-physics visibility as unconfirmed", () =>
{
    var node = JsonNode.Parse(Fixtures.CurrentSnapshot())!;
    node["lights"]!["upstream"]!["sources"]![0]!["sourceVisibility"] = null;
    Assert(TelemetryParser.Parse(Encoding.UTF8.GetBytes(node.ToJsonString()), out _)!.Lights.Count == 0);
    node["lights"]!["upstream"]!["sources"]![0]!["sourceVisibility"] = new JsonObject
    {
        ["status"] = "clear", ["method"] = "unrecognized", ["volumeAgeMillisecondsAtCapture"] = 0
    };
    Assert(TelemetryParser.Parse(Encoding.UTF8.GetBytes(node.ToJsonString()), out _)!.Lights.Count == 0);
});
Add("Confirmed-visible mapping drops hidden color without an EMA tail", () =>
{
    var frame = TelemetryParser.Parse(Fixtures.CurrentSnapshot(), out _)!;
    var mapper = new LightMapper();
    var clear = mapper.Map(frame, rearArea, new(), 0.01);
    Assert(clear.Any(c => c.Rgb.X > 0));
    var hidden = mapper.Map(frame with { Lights = [] }, rearArea, new(), 0.01);
    Assert(hidden.All(c => c.Rgb.Length == 0));
    var blue = mapper.Map(frame with { Lights = [new(frame.Lights[0].Position, new(0, 0, 2), 100)] }, rearArea, new(), 0.01);
    Assert(blue.All(c => c.Rgb.X == 0) && blue.Any(c => c.Rgb.Z > 0));
});
Add("Ambient contract requires a fresh camera-local estimate", () =>
{
    var sample = AmbientParser.Parse(Fixtures.Ambient(), out _)!;
    Assert(sample.WorkingLevel == 8 && sample.CaptureSequence == 42);
    Assert(sample.WorkingRgb == new Vec3(6, 8, 10) && sample.SkyRgb == new Vec3(24, 32, 40) && sample.SkyVisibility == 0.25,
        "Raw working RGB, sky RGB or visibility was not preserved");
    var node = JsonNode.Parse(Fixtures.Ambient())!;
    node["units"] = "display-nits";
    Assert(AmbientParser.Parse(Encoding.UTF8.GetBytes(node.ToJsonString()), out _) == null);
    node = JsonNode.Parse(Fixtures.Ambient())!;
    node["localEnvironmentAmbientEstimateWorking"]!["available"] = false;
    Assert(AmbientParser.Parse(Encoding.UTF8.GetBytes(node.ToJsonString()), out var status) == null && status.Contains("local sky visibility unavailable"));
    node["visibility"] = null;
    Assert(AmbientParser.Parse(Encoding.UTF8.GetBytes(node.ToJsonString()), out status) == null && status.Contains("local sky visibility unavailable"));
});
Add("Ambient sky and visibility expire independently without retaining daylight", () =>
{
    var state = new AmbientState();
    state.Accept(Fixtures.Ambient(at: DateTimeOffset.UtcNow.AddMilliseconds(-100), skyAge: 100, visibilityAge: 1480));
    Assert(state.Read(out _) != null);
    Assert(state.Read(out _, DateTimeOffset.UtcNow.AddMilliseconds(40)) == null);
    state.Accept(Fixtures.Ambient(at: DateTimeOffset.UtcNow.AddSeconds(-2), skyAge: 0));
    Assert(state.Read(out _) == null);
});
Add("Ambient provides room fill without erasing a nearby local light", () =>
{
    var frame = TelemetryParser.Parse(Fixtures.CurrentSnapshot(), out _)!;
    var settings = new MappingSettings(Brightness: 1, SmoothingMs: 0);
    var daylight = new LightMapper();
    IReadOnlyList<ChannelColor> bright = [];
    for (var i = 0; i < 3; i++) bright = daylight.Map(frame, rearArea, settings, 1, new(42, DateTimeOffset.UtcNow, 0, 0, 8));
    Assert(bright.All(c => c.Rgb.Y > 0.5), "Daylight fill is too dim");
    var dark = new LightMapper().Map(frame, rearArea, settings, 1, new(42, DateTimeOffset.UtcNow, 0, 0, 0.02));
    Assert(dark.Any(c => c.Rgb.X > c.Rgb.Y * 1.5), "Dark surroundings lost the local red light");
    var hidden = daylight.Map(frame with { Lights = [] }, rearArea, settings, 1, new(42, DateTimeOffset.UtcNow, 0, 0, 8));
    Assert(hidden.All(c => c.Rgb.Y > 0.5 && Math.Abs(c.Rgb.X - c.Rgb.Y) < 1e-9), "Hidden red leaked into the ambient baseline");
    Assert(bright.Any(c => c.Rgb.X > hidden.First(h => h.Id == c.Id).Rgb.X + 0.08), "Daylight erased nearby local contrast");
    var disabled = new LightMapper().Map(frame, rearArea, settings with { AmbientSensitivity = 0 }, 1,
        new(42, DateTimeOffset.UtcNow, 0, 0, 8));
    var localOnly = new LightMapper().Map(frame, rearArea, settings, 1);
    Assert(disabled.Zip(localOnly).All(pair => pair.First.Rgb == pair.Second.Rgb), "Zero influence changed local-only output");
    for (var i = 0; i < 5; i++) bright = daylight.Map(frame, rearArea, settings, 1);
    Assert(bright.Zip(localOnly).All(pair => (pair.First.Rgb - pair.Second.Rgb).Length < 0.001), "Stale ambient was retained indefinitely");
});
Add("Unmodified orange source follows camera yaw while zero Ambient stays black", () =>
{
    var layout = new EntertainmentArea(Fixtures.Id, "Three-lamp room", false,
    [
        new(0, new(-0.9, 1, -0.8), [new("left", 0)], "Front left"),
        new(1, new(0.6, 1, -0.4), [new("right", 0)], "Front right"),
        new(2, new(0.3, -1, 0.5), [new("rear", 0)], "Rear")
    ]);
    var frame = TelemetryParser.Parse(Fixtures.CurrentSnapshot(), out _)! with
    {
        Player = default,
        Camera = new(default, new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)),
        Lights = [new(new(-3, 0, 5), new(1.58, 0.63, 0.17))]
    };
    var settings = new MappingSettings(Brightness: 0.6, SmoothingMs: 0);
    var night = new AmbientFrame(42, DateTimeOffset.UtcNow, 0, 0, 0);
    IReadOnlyList<ChannelColor> ahead = [];
    var mapper = new LightMapper();
    for (var i = 0; i < 3; i++) ahead = mapper.Map(frame, layout, settings, 1, night);
    var unlit = mapper.Map(frame with { Lights = [] }, layout, settings, 1, night);
    Assert(unlit.All(c => c.Rgb.Length == 0), "A black Ambient estimate lit the empty night scene");
    Assert(ahead[0].Rgb.X > ahead[2].Rgb.X + 0.25, "Fire did not concentrate on the left-front lamp");
    Assert(ahead[1].Rgb == default && ahead[2].Rgb == default, "Left-front fire crossed a room boundary");
    double Decode(double v) => v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    Assert(Math.Abs(Decode(ahead[0].Rgb.Y / settings.Brightness) / Decode(ahead[0].Rgb.X / settings.Brightness) - 0.63 / 1.58) < 1e-9,
        "Neutral color controls changed the orange source's linear RGB ratio");
    var facingAway = frame with { Camera = frame.Camera with { Right = new(-1, 0, 0), Forward = new(0, 0, -1) } };
    var behind = new LightMapper();
    IReadOnlyList<ChannelColor> turned = [];
    for (var i = 0; i < 3; i++) turned = behind.Map(facingAway, layout, settings, 1, night);
    Assert(turned[2].Rgb.X > turned[0].Rgb.X + 0.25, "Yaw did not transfer fire to the rear channel");
    var pureRed = new LightMapper().Map(frame with { Lights = [new(new(-3, 0, 5), new(1.58, 0, 0))] },
        layout, settings with { AmbientSensitivity = 0 }, 1, night);
    Assert(pureRed.All(c => c.Rgb.Y == 0), "Neutral controls altered pure red");
});
Add("Zero signal stays black and a night cutoff only applies when configured", () =>
{
    var empty = TelemetryParser.Parse(Fixtures.CurrentSnapshot(), out _)! with { Lights = [] };
    var settings = new MappingSettings(SmoothingMs: 0, AmbientSmoothingMs: 0);
    var zero = new AmbientFrame(42, DateTimeOffset.UtcNow, 0, 0, 0);
    var night = zero with { WorkingLevel = 0.001 };
    Assert(new LightMapper().Map(empty, area, settings, 1, zero).All(c => c.Rgb.Length == 0));
    var faint = new LightMapper().Map(empty, area, settings, 1, night);
    Assert(faint.All(c => c.Rgb.X is > 0 and < 0.002), "Near-zero raw Ambient acquired a hidden floor or cutoff");
    Assert(new LightMapper().Map(empty, area, settings with { AmbientCutoff = 0.01 }, 1, night).All(c => c.Rgb.Length == 0));
    Assert(new LightMapper().Map(empty, area, settings with { AmbientFloor = 0.1 }, 1, zero).All(c => c.Rgb.X > 0.2),
        "Explicit floor has no effect");
    Assert(new LightMapper().Map(empty, area, settings with { AmbientFloor = 0.1 }, 1).All(c => c.Rgb.Length == 0),
        "Missing Ambient allowed synthetic fill");
    Assert(new LightMapper().Map(empty, area, settings, 1, zero with { WorkingLevel = 8 })
        .All(c => c.Rgb.X > 0.3 && c.Rgb.X == c.Rgb.Y), "Daylight Ambient disappeared with no local lights");
});
Add("Ambient strength, reference, tint and smoothing respond to settings", () =>
{
    var empty = TelemetryParser.Parse(Fixtures.CurrentSnapshot(), out _)! with { Lights = [] };
    var settings = new MappingSettings(Brightness: 1, SmoothingMs: 0, AmbientSmoothingMs: 0);
    var ambient = new AmbientFrame(42, DateTimeOffset.UtcNow, 0, 0, 2);
    Vec3 Sample(MappingSettings s) => new LightMapper().Map(empty, area, s, 0.1, ambient)[0].Rgb;
    var baseline = Sample(settings);
    Assert(Sample(settings with { AmbientOutput = 0 }).Length == 0);
    Assert(Sample(settings with { AmbientOutput = 0.8 }).X > baseline.X);
    Assert(Sample(settings with { AmbientReferenceLevel = 8 }).X < baseline.X);
    Assert(Sample(settings with { AmbientSensitivity = 2 }).X > baseline.X);
    var blue = Sample(settings with { AmbientTintHue = 240, AmbientTintSaturation = 1 });
    Assert(blue.Z > 0 && blue.X == 0 && blue.Y == 0);
    Assert(Sample(settings with { AmbientSensitivity = 0, AmbientFloor = 0.5 }).Length == 0);
    var smoothed = Sample(settings with { AmbientSmoothingMs = 1000 });
    Assert(smoothed.X > 0 && smoothed.X < baseline.X);
    var mapper = new LightMapper();
    mapper.Map(empty, area, settings with { AmbientSmoothingMs = 1000 }, 1, ambient);
    Assert(mapper.Map(empty, area, settings, 0.01, ambient with { WorkingLevel = 0 }).All(c => c.Rgb.Length == 0),
        "Zero smoothing did not immediately follow zero input");
    mapper.Map(empty, area, settings, 1, ambient);
    Assert(mapper.Map(empty, area, settings, 0.01).All(c => c.Rgb.Length == 0), "Stale Ambient persisted");
});
Add("Malformed JSON is unavailable", () => Assert(TelemetryParser.Parse("{"u8.ToArray(), out _) == null));
Add("Envelope duplicates cannot refresh light freshness", () =>
{
    var state = new TelemetryState(); var first = Fixtures.Snapshot(10);
    state.Accept(first); Assert(state.Read(out _) != null);
    state.Accept(Fixtures.Snapshot(10, DateTimeOffset.UtcNow.AddSeconds(20)));
    Assert(state.Read(out _) != null, "Duplicate replaced valid frame");
    Assert(state.Read(out _, DateTimeOffset.UtcNow.AddSeconds(1)) == null);
});
Add("Unavailable sequence prevents replay of older valid data", () =>
{
    var state = new TelemetryState(); state.Accept(Fixtures.Snapshot(10));
    var unavailable = JsonNode.Parse(Fixtures.Snapshot(12))!; unavailable["game"]!["state"] = "loading";
    state.Accept(Encoding.UTF8.GetBytes(unavailable.ToJsonString())); state.Accept(Fixtures.Snapshot(11));
    Assert(state.Read(out _) == null);
    state.Reset("new host"); state.Accept(Fixtures.Snapshot(0)); Assert(state.Read(out _) != null);
});
Add("Reject stale and future wall timestamps", () =>
{
    foreach (var offset in new[] { -10, 10 })
    {
        var state = new TelemetryState(); state.Accept(Fixtures.Snapshot(1, DateTimeOffset.UtcNow.AddSeconds(offset))); Assert(state.Read(out _) == null);
    }
});
Add("Hue layout preserves channel IDs, segments and height", () =>
{
    Assert(area.Channels.Count == 2 && area.LampCount == 1);
    Assert(area.Channels[1].Id == 5 && area.Channels[1].Members[0].SegmentIndex == 1);
});
Add("Reject duplicate channels and invalid coordinates", () =>
{
    var node = JsonNode.Parse(Fixtures.AreaJson())!;
    node["channels"]![1]!["channel_id"] = 2;
    Reject(() => BridgeClient.ParseArea(JsonSerializer.SerializeToElement(node)));
    node["channels"]![1]!["channel_id"] = 5; node["channels"]![0]!["position"]!["x"] = 100;
    Reject(() => BridgeClient.ParseArea(JsonSerializer.SerializeToElement(node)));
});
Add("Directional mapping keeps left red and right blue", () =>
{
    var frame = TelemetryParser.Parse(Fixtures.Snapshot(), out _)!;
    var colors = new LightMapper().Map(frame, area, new(1, 1, 35, 8, 0), 0.1);
    Assert(colors[0].Rgb.X > 0 && colors[0].Rgb.Z == 0);
    Assert(colors[1].Rgb.Z > 0 && colors[1].Rgb.X == 0);
});
Add("Camera yaw keeps off-screen sources on matching rear channels", () =>
{
    var frame = TelemetryParser.Parse(Fixtures.Snapshot(), out _)!;
    var reversed = frame with { Camera = frame.Camera with { Right = new(-1, 0, 0), Forward = new(0, 0, -1) } };
    var rear = area with { Channels = area.Channels.Select(c => c with { Position = new(c.Position.X, -c.Position.Y, c.Position.Z) }).ToArray() };
    var colors = new LightMapper().Map(reversed, rear, new(1, 1, 35, 8, 0), 0.1);
    Assert(colors[0].Rgb.Z > 0 && colors[0].Rgb.X == 0);
});
Add("Strict boundaries keep every opposite quadrant exactly black while the camera turns", () =>
{
    var layout = new EntertainmentArea(Fixtures.Id, "Four corners", false,
    [
        new(0, new(-1, 1, -0.7), [], "Front left"), new(1, new(1, 1, 0.2), [], "Front right"),
        new(2, new(1, -1, 0.8), [], "Rear right"), new(3, new(-1, -1, -0.3), [], "Rear left")
    ]);
    var frame = TelemetryParser.Parse(Fixtures.Snapshot(), out _)! with
    {
        Camera = new(default, new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)),
        Lights = [new(new(-5, 0, 5), new(5, 1, .1))]
    };
    foreach (var focus in new[] { 0.0, 2, 16 })
    foreach (var normalization in new[] { 0.0, 1 })
    {
        var mapper = new LightMapper();
        var settings = new MappingSettings(Spread: focus, DirectionNormalization: normalization, SmoothingMs: 1000, OutputGamma: 4);
        for (var corner = 0; corner < 4; corner++)
        {
            var yaw = -corner * Math.PI / 2;
            var camera = frame.Camera with { Right = new(Math.Cos(yaw), 0, -Math.Sin(yaw)), Forward = new(Math.Sin(yaw), 0, Math.Cos(yaw)) };
            var colors = mapper.Map(frame with { Camera = camera }, layout, settings, .001);
            Assert(colors[corner].Rgb.X > 0, "Allowed corner lost its source");
            Assert(colors.Where((_, i) => i != corner).All(c => c.Rgb == default), "Excluded corner retained color after camera rotation");
        }
    }
});
Add("Recorded left brazier has zero right and rear contribution at the owner's broad focus", () =>
{
    // Read-only CDT capture from the reported forest scene; replay is synthetic evidence.
    var layout = new EntertainmentArea(Fixtures.Id, "Recorded room", false,
    [
        new(0, new(.31291532926158294, -.9982087856556487, .5052717571578222), [], "Rear"),
        new(1, new(.6081610035675666, 1, -.3940484143872305), [], "Front right"),
        new(2, new(-.9011899756747147, 1, -.8866089323712685), [], "Front left")
    ]);
    var frame = TelemetryParser.Parse(Fixtures.CurrentSnapshot(), out _)! with
    {
        Player = new(-10496.66, 605.3306, -4445.196),
        Camera = new(new(-10500.002, 609.24884, -4440.6255), new(-.88250196, 0, -.47030807),
            new(.12906933, .9616054, -.2421901), new(.45225078, -.27443567, -.84861875)),
        Lights = [new(new(-10492.55, 606.8549, -4446.793), new(.3535532, .108926825, .026889712)),
            new(new(-10492.526, 606.91095, -4446.793), new(1.9406164, .59905326, .14772636))]
    };
    var settings = new MappingSettings(Brightness: .6, Radius: 20, FadeStart: 6, FadeExponent: 1.2);
    var strict = new LightMapper().Map(frame, layout, settings, 1);
    var broad = new LightMapper().Map(frame, layout, settings with { SeparateLeftRight = false, SeparateFrontRear = false,
        SourceDiscRadiusDegrees = 89, SourceDiscSoftness = 0, DirectionOriginBlend = 1 }, 1);
    Assert(strict[0].Rgb == default && strict[1].Rgb == default, "Recorded left fire still reaches right/rear");
    Assert(strict[2].Rgb == broad[2].Rgb && strict[2].Rgb.X > .5, "Boundary unnecessarily changed left fire output");
    Assert(broad[1].Rgb.X > .5, "Replay no longer reproduces the reported broad spill");
});
Add("Direction origin interpolates player and camera positions exactly and preserves camera axes", () =>
{
    var frame = TelemetryParser.Parse(Fixtures.Snapshot(), out _)! with
    {
        Player = new(10, 3, 20),
        Camera = new(new(4, 7, 12), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1))
    };
    foreach (var blend in new[] { 0.0, .25, .5, 1 })
    {
        var settings = new MappingSettings(DirectionOriginBlend: blend);
        var expected = new Vec3(10 - 6 * blend, 3 + 4 * blend, 20 - 8 * blend);
        Assert(LightMapper.DirectionOrigin(frame, settings) == expected, "Origin left the player-camera line");
        var direction = LightMapper.SourceDirection(expected + new Vec3(2, 0, 5), frame, settings);
        Assert((direction - new Vec3(2, 5, 0).Unit).Length < 1e-12);
        var turned = frame with { Camera = frame.Camera with { Right = new(-1, 0, 0), Forward = new(0, 0, -1) } };
        Assert(LightMapper.DirectionOrigin(turned, settings) == expected, "Camera orientation moved the origin");
        Assert((LightMapper.SourceDirection(expected + new Vec3(2, 0, 5), turned, settings) + direction).Length < 1e-12);
    }
    foreach (var invalid in new[] { -.01, 1.01, double.PositiveInfinity })
        Reject(() => LightMapper.Validate(new(DirectionOriginBlend: invalid)));
});
Add("Recorded nearby fires switch front or rear with the chosen player-camera origin at 15 GU", () =>
{
    // These fires are behind the player, but between the player and camera lens.
    var layout = new EntertainmentArea(Fixtures.Id, "Recorded room", false,
    [
        new(0, new(.31291532926158294, -.9982087856556487, .5052717571578222), [], "Rear right"),
        new(1, new(.6081610035675666, 1, -.3940484143872305), [], "Front right"),
        new(2, new(-.9011899756747147, 1, -.8866089323712685), [], "Front left")
    ]);
    var frame = TelemetryParser.Parse(Fixtures.CurrentSnapshot(), out _)! with
    {
        Player = new(-10606.647, 607.5385, -4421.96),
        Camera = new(new(-10612.477, 610.5437, -4423.1084), new(.05939758, 3.7252903e-9, -.99823433),
            new(.11824696, .99295926, .007036004), new(.9912061, -.11845611, .058979392)),
        Lights = [new(new(-10612.612, 608.2134, -4421.1597), new(.3418959, .10573153, .026059404)),
            new(new(-10612.609, 608.2771, -4421.1597), new(1.3013425, .4003111, .09886421)),
            new(new(-10611.344, 607.69556, -4425.8354), new(.36165032, .11177812, .027556013)),
            new(new(-10611.355, 607.7641, -4425.8354), new(1.1991158, .36700568, .090834536))]
    };
    var settings = new MappingSettings(Brightness: .6, Radius: 15, FadeStart: 6, FadeExponent: 1.2);
    var mapper = new LightMapper();
    var lens = mapper.Map(frame, layout, settings with { DirectionOriginBlend = 1 }, .001);
    Assert(lens[0].Rgb == default && lens[1].Rgb.X > .5 && lens[2].Rgb.X > .5, "Lens endpoint did not reproduce the reported front output");
    var player = mapper.Map(frame, layout, settings with { DirectionOriginBlend = 0 }, .001);
    Assert(player[0].Rgb.X > .5 && player.Skip(1).All(c => c.Rgb == default), "Player endpoint did not move the source to the rear");
    var quarter = mapper.Map(frame, layout, settings with { DirectionOriginBlend = .25 }, .001);
    Assert(quarter[0].Rgb.X > 0 && quarter.Skip(1).All(c => c.Rgb == default));
    var half = mapper.Map(frame, layout, settings, .001);
    Assert(half.All(c => c.Rgb.IsFinite) && half.Skip(1).All(c => c.Rgb == default));
    var translatedCamera = frame with { Camera = frame.Camera with { Position = new(0, 500, 0) } };
    var fixedPlayer = mapper.Map(translatedCamera, layout, settings with { DirectionOriginBlend = 0 }, .001);
    Assert(player.SequenceEqual(fixedPlayer), "Camera translation changed output at the player endpoint");
});
Add("A missing matching lamp never redirects sources across a boundary", () =>
{
    var frame = TelemetryParser.Parse(Fixtures.Snapshot(), out _)! with
    {
        Camera = new(default, new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)),
        Lights = [new(new(-2, 0, 5), new(1e8, 1e7, 1e6))]
    };
    var rightOnly = area with { Channels = [area.Channels[1]] };
    foreach (var focus in new[] { 0.0, 16 })
    {
        var colors = new LightMapper().Map(frame, rightOnly, new(Spread: focus, OutputGamma: 4, LocalStrength: 4), 1);
        Assert(colors.Single().Rgb == default, "Normalization revived a forbidden channel");
    }
    Assert(new LightMapper().Map(frame, area with { Channels = [] }, new(), 1).Count == 0);
});
Add("Center lines are shared but sources immediately either side are strictly separated", () =>
{
    var frame = TelemetryParser.Parse(Fixtures.Snapshot(), out _)! with
    {
        Camera = new(default, new(1, 0, 0), new(0, 1, 0), new(0, 0, 1))
    };
    var layout = area with { Channels = [.. area.Channels, new(8, new(0, 1, 0), [], "Center")] };
    foreach (var x in new[] { -1e-12, 0, 1e-12 })
    {
        var colors = new LightMapper().Map(frame with { Lights = [new(new(x, 0, 5), new(1, 0, 0))] }, layout, new(Spread: 0), 1);
        Assert((colors[0].Rgb.X > 0) == (x <= 0) && (colors[1].Rgb.X > 0) == (x >= 0));
        Assert(colors[2].Rgb.X > 0, "A lamp exactly on the center line lost an adjacent source");
    }
});
Add("Boundary switches and room offset act immediately without altering Ambient or raw input", () =>
{
    var layout = new EntertainmentArea(Fixtures.Id, "Corners", false,
    [
        new(0, new(-1, 1, 0), [], "Front left"), new(1, new(1, 1, 0), [], "Front right"),
        new(2, new(-1, -1, 0), [], "Rear left"), new(3, new(1, -1, 0), [], "Rear right")
    ]);
    var frame = TelemetryParser.Parse(Fixtures.Snapshot(), out _)! with
    {
        Camera = new(default, new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)),
        Lights = [new(new(-2, 0, 5), new(1, 0, 0))]
    };
    var settings = new MappingSettings(Spread: 0, AmbientSmoothingMs: 0, SourceDiscRadiusDegrees: 89, SourceDiscSoftness: 0);
    var mapper = new LightMapper();
    var broad = mapper.Map(frame, layout, settings with { SeparateLeftRight = false, SeparateFrontRear = false }, 1);
    Assert(broad.Take(2).All(c => c.Rgb.X > 0));
    var strict = mapper.Map(frame, layout, settings, .001);
    Assert(strict[0].Rgb.X > 0 && strict.Skip(1).All(c => c.Rgb == default), "Enabling boundaries retained previous spread");
    var frontOnly = mapper.Map(frame, layout, settings with { SeparateLeftRight = false }, .001);
    Assert(frontOnly.Take(2).All(c => c.Rgb.X > 0) && frontOnly.Skip(2).All(c => c.Rgb == default));
    var beside = frame with { Lights = [new(new(-5, 0, 2), new(1, 0, 0))] };
    var leftOnly = mapper.Map(beside, layout, settings with { SeparateFrontRear = false }, .001);
    Assert(leftOnly[0].Rgb.X > 0 && leftOnly[2].Rgb.X > 0 && leftOnly[1].Rgb == default && leftOnly[3].Rgb == default);
    var offset = mapper.Map(frame, layout, settings with { CameraYawOffset = -90 }, .001);
    Assert(offset[1].Rgb.X > 0 && offset.Where((_, i) => i != 1).All(c => c.Rgb == default));
    var ambient = new AmbientFrame(42, DateTimeOffset.UtcNow, 0, 0, .01);
    var mixed = mapper.Map(frame, layout, settings, .001, ambient);
    var ambientOnly = mapper.Map(frame with { Lights = [] }, layout, settings, .001, ambient);
    Assert(mixed.Skip(1).Zip(ambientOnly.Skip(1)).All(p => p.First.Rgb == p.Second.Rgb), "Forbidden local light changed the Ambient baseline");
    Assert(frame.Lights.Single() == new LightContribution(new(-2, 0, 5), new(1, 0, 0)), "Mapping changed raw CDT data");
});
Add("The source footprint is a bounded 2D circle with equal horizontal and vertical radius", () =>
{
    Vec3 Ray(double angle, double around)
    {
        var a = angle * Math.PI / 180; var b = around * Math.PI / 180;
        return new(Math.Sin(a) * Math.Cos(b), Math.Cos(a), Math.Sin(a) * Math.Sin(b));
    }
    var layout = new EntertainmentArea(Fixtures.Id, "Disc probes", false,
        new[] { Ray(30, 0), Ray(30, 90), Ray(30, 180), Ray(30, 270), Ray(45, 0), Ray(60, 0) }
            .Select((p, i) => new EntertainmentChannel((byte)i, p, [], "Probe " + i)).ToArray());
    var frame = TelemetryParser.Parse(Fixtures.Snapshot(), out _)! with
    {
        Camera = new(default, new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)),
        Lights = [new(new(0, 0, 5), new(1, 0, 0))]
    };
    var settings = new MappingSettings(Spread: 0, SourceDiscRadiusDegrees: 40, SourceDiscSoftness: 0);
    var small = new LightMapper().Map(frame, layout, settings, 1);
    Assert(small.Take(4).All(c => c.Rgb == small[0].Rgb && c.Rgb.X > 0), "Footprint is not circular in its projection plane");
    Assert(small.Skip(4).All(c => c.Rgb == default), "Disc has an infinite tail");
    var larger = new LightMapper().Map(frame, layout, settings with { SourceDiscRadiusDegrees = 50 }, 1);
    Assert(larger[4].Rgb.X > 0 && larger[5].Rgb == default, "Visible source radius did not resize its footprint");
    var soft = new LightMapper().Map(frame, layout, settings with { SourceDiscRadiusDegrees = 50, SourceDiscSoftness = 1 }, 1);
    Assert(soft[4].Rgb.X > 0 && soft[4].Rgb.X < larger[4].Rgb.X, "Compensation undid the soft disc edge");
    var single = layout with { Channels = [layout.Channels[4]] };
    Assert(new LightMapper().Map(frame, single, settings with { SourceDiscRadiusDegrees = 50, SourceDiscSoftness = 1 }, 1)[0].Rgb == soft[4].Rgb,
        "A lone edge channel was boosted to full intensity");
});
Add("An enlarged off-screen source disc is still clipped at every enabled side boundary", () =>
{
    var layout = new EntertainmentArea(Fixtures.Id, "Side probes", false,
    [new(0, new(-1, .1, 0), [], "Left front"), new(1, new(-1, -.1, 0), [], "Left rear"), new(2, new(1, .1, 0), [], "Right front")]);
    var frame = TelemetryParser.Parse(Fixtures.Snapshot(), out _)! with
    {
        Camera = new(default, new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)),
        Lights = [new(new(-5, 0, .1), new(1, 0, 0))]
    };
    var settings = new MappingSettings(Spread: 0, SourceDiscRadiusDegrees: 89, SourceDiscSoftness: 0);
    var strict = new LightMapper().Map(frame, layout, settings, 1);
    Assert(strict[0].Rgb.X > 0 && strict.Skip(1).All(c => c.Rgb == default), "Off-screen disc crossed a boundary");
    var withoutFrontRear = new LightMapper().Map(frame, layout, settings with { SeparateFrontRear = false }, 1);
    Assert(withoutFrontRear[1].Rgb.X > 0, "Boundary switch did not expose the adjacent part of the disc");
});
Add("Hue axes preserve front/back and height independently", () =>
{
    // Hue x = right, y = front, z = up; game camera basis is right/up/forward.
    // Explicit independent cardinal pairs catch both mirroring and y/z swaps.
    var directions = new (Vec3 Hue, Vec3 World)[]
    {
        (new(1, 0, 0), new(5, 0, 0)), (new(-1, 0, 0), new(-5, 0, 0)),
        (new(0, 1, 0), new(0, 0, 5)), (new(0, -1, 0), new(0, 0, -5)),
        (new(0, 0, 1), new(0, 5, 0)), (new(0, 0, -1), new(0, -5, 0))
    };
    var layout = new EntertainmentArea(Fixtures.Id, "Cardinal axes", false,
        directions.Select((d, i) => new EntertainmentChannel((byte)i, d.Hue, [new("test-" + i, 0)], "Axis " + i)).ToArray());
    var source = TelemetryParser.Parse(Fixtures.Snapshot(), out _)!;
    for (var i = 0; i < directions.Length; i++)
    {
        var frame = source with { Camera = source.Camera with { Position = default },
            Lights = [new(directions[i].World, new(1, 0, 0))] };
        var colors = new LightMapper().Map(frame, layout, new(1, 1, 35, 8, 0), 0.1);
        Assert(colors[i].Rgb.X > 0.5);
        Assert(colors.Where((_, j) => j != i).All(c => c.Rgb.X < colors[i].Rgb.X * 0.1));
    }
});
Add("Empty feed, cutoff and brightness cap", () =>
{
    var frame = TelemetryParser.Parse(Fixtures.Snapshot(), out _)!;
    Assert(new LightMapper().Map(frame with { Lights = [] }, area, new(), 1).All(c => c.Rgb.Length == 0));
    Assert(new LightMapper().Map(frame, area, new(Radius: 1), 1).All(c => c.Rgb.Length == 0));
    Assert(new LightMapper().Map(frame, area, new(10, 0.25, 100, 0.1, 0), 1).All(c => Math.Max(c.Rgb.X, Math.Max(c.Rgb.Y, c.Rgb.Z)) <= 0.250001));
});
Add("Mapper reset clears smoothing history", () =>
{
    var mapper = new LightMapper(); var frame = TelemetryParser.Parse(Fixtures.Snapshot(), out _)!;
    mapper.Map(frame, area, new(), 1); mapper.Reset();
    Assert(mapper.Map(frame with { Lights = [] }, area, new(), 0.1).All(c => c.Rgb.Length == 0));
    Reject(() => LightMapper.Validate(new(Gain: double.NaN)));
});
var fadeArea = new EntertainmentArea(Fixtures.Id, "Distance fixture", false,
    [new(9, default, [new("synthetic-distance", 0)], "Center")]);
TelemetryFrame DistanceFrame(double distance, Vec3 rgb) => TelemetryParser.Parse(Fixtures.Snapshot(), out _)! with
    { Player = default, Lights = [new(new(distance, 0, 0), rgb)] };
var fadeSettings = new MappingSettings(Gain: 1, Brightness: 1, Radius: 15, SmoothingMs: 0, FadeStart: 5);
Add("Distance fade has a full near zone, smooth tail and exact cutoff", () =>
{
    Assert(LightMapper.DistanceWeight(0, fadeSettings) == 1);
    Assert(LightMapper.DistanceWeight(5, fadeSettings) == 1);
    Assert(LightMapper.DistanceWeight(10, fadeSettings) == 0.25);
    Assert(LightMapper.DistanceWeight(15, fadeSettings) == 0);
    Assert(LightMapper.DistanceWeight(999, fadeSettings) == 0);
    var weights = Enumerable.Range(0, 1501).Select(i => LightMapper.DistanceWeight(i / 100.0, fadeSettings)).ToArray();
    Assert(weights.All(w => w is >= 0 and <= 1));
    Assert(weights.Zip(weights.Skip(1)).All(p => p.First >= p.Second));
    Assert(1 - LightMapper.DistanceWeight(5.001, fadeSettings) < 1e-6);
    Assert(LightMapper.DistanceWeight(14.999, fadeSettings) < 1e-12);
});
Add("Approach and departure use the same continuous distance envelope", () =>
{
    double Sample(double d) => new LightMapper().Map(DistanceFrame(d, new(2, 0.5, 0.1)), fadeArea, fadeSettings, 0.1)[0].Rgb.X;
    var distances = Enumerable.Range(0, 31).Select(i => i * 0.5).ToArray();
    var outward = distances.Select(Sample).ToArray();
    var inward = distances.Reverse().Select(Sample).Reverse().ToArray();
    Assert(outward.SequenceEqual(inward));
    Assert(outward.Zip(outward.Skip(1)).All(p => p.First >= p.Second));
});
Add("Very bright HDR sources cannot cancel their individual distance fade", () =>
{
    Vec3 Sample(double d) => new LightMapper().Map(DistanceFrame(d, new(1e8, 0, 0)), fadeArea, fadeSettings, 0.1)[0].Rgb;
    Assert(Sample(5).X > 0.99);
    Assert(Sample(10).X is > 0.53 and < 0.54, "HDR compression undid the half-distance fade");
    Assert(Sample(14.5).X < 0.001);
    Assert(Sample(15).Length == 0 && Sample(100).Length == 0);
});
Add("A distant bright color cannot overpower an equally bright nearby source", () =>
{
    var frame = DistanceFrame(2, new(1e8, 0, 0));
    frame = frame with { Lights = [.. frame.Lights, new(new(14, 0, 0), new(0, 0, 1e8))] };
    var rgb = new LightMapper().Map(frame, fadeArea, fadeSettings, 0.1)[0].Rgb;
    Assert(rgb.X > 0.99 && rgb.Z < 0.02);
});
Add("Distance is measured from the player independently of camera position", () =>
{
    var near = DistanceFrame(2, new(1, 0, 0));
    near = near with { Camera = near.Camera with { Position = new(100, 0, 0) } };
    Assert(new LightMapper().Map(near, fadeArea, fadeSettings, 0.1)[0].Rgb.X > 0.5);
    var far = near with { Player = new(100, 0, 0), Camera = near.Camera with { Position = new(2, 0, 0) } };
    Assert(new LightMapper().Map(far, fadeArea, fadeSettings, 0.1)[0].Rgb.Length == 0);
});
Add("Fade slider settings take effect without restarting the mapper", () =>
{
    var mapper = new LightMapper(); var frame = DistanceFrame(10, new(1, 0.2, 0.1));
    Assert(mapper.Map(frame, fadeArea, fadeSettings, 0.1)[0].Rgb.Length > 0);
    Assert(mapper.Map(frame, fadeArea, fadeSettings with { Radius = 8 }, 0.1)[0].Rgb.Length == 0);
    Assert(mapper.Map(frame, fadeArea, fadeSettings with { FadeStart = 12 }, 0.1)[0].Rgb.X > 0.8);
});
Add("Neutral controls preserve all source color ratios including orange", () =>
{
    double Decode(double v) => v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    foreach (var raw in new[] { new Vec3(1, 2, 4), new(1.122, 0.3401, 0.08453), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1), new(0.5, 0.5, 0.5) })
    {
        var rgb = new LightMapper().Map(DistanceFrame(10, raw), fadeArea, fadeSettings, 0.1)[0].Rgb;
        var decoded = new Vec3(Decode(rgb.X), Decode(rgb.Y), Decode(rgb.Z));
        Assert((decoded.Unit - raw.Unit).Length < 1e-9, $"Neutral controls changed {raw}");
    }
});
Add("General color and output controls are adjustable without changing raw frames", () =>
{
    var frame = DistanceFrame(2, new(1, 0, 0));
    Vec3 Sample(MappingSettings s, TelemetryFrame? f = null) => new LightMapper().Map(f ?? frame, fadeArea, s, 1)[0].Rgb;
    var red = Sample(fadeSettings);
    var green = Sample(fadeSettings with { HueShiftDegrees = 120 });
    Assert(green.X == 0 && green.Y > 0 && green.Z == 0);
    Assert(frame.Lights[0].Rgb == new Vec3(1, 0, 0), "Mapping mutated CDT input");
    var neutral = Sample(fadeSettings with { Saturation = 0 });
    Assert(neutral.X > 0 && neutral.X == neutral.Y && neutral.Y == neutral.Z);
    Assert(Sample(fadeSettings with { RedGain = 0 }).Length == 0);
    var mixedFrame = DistanceFrame(2, new(1, 0.2, 0.1));
    Assert(Sample(fadeSettings with { GreenGain = 2 }, mixedFrame).Y > Sample(fadeSettings, mixedFrame).Y);
    Assert(Sample(fadeSettings with { BlueGain = 2 }, mixedFrame).Z > Sample(fadeSettings, mixedFrame).Z);
    Assert(Sample(fadeSettings with { LocalStrength = 0 }).Length == 0);
    Assert(Sample(fadeSettings with { LocalStrength = 0.5 }).X < red.X);
    Assert(Sample(fadeSettings with { Gain = 0 }).Length == 0);
    Assert(Sample(fadeSettings with { OutputGamma = 2 }).X > red.X);
    Assert(Sample(fadeSettings with { OutputGamma = 2, Brightness = 0.2 }).X <= 0.2);
    var black = DistanceFrame(2, default);
    Assert(Sample(fadeSettings with { HueShiftDegrees = 120, Saturation = 0, RedGain = 4, OutputGamma = 4 }, black).Length == 0);
});
Add("Spatial normalization, yaw offset and fade curve are explicit controls", () =>
{
    var frame = DistanceFrame(5, new(1, 0, 0)) with
    {
        Camera = new(default, new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)),
        Lights = [new(new(-3, 0, 5), new(1, 0, 0))]
    };
    IReadOnlyList<ChannelColor> Sample(MappingSettings s) => new LightMapper().Map(frame, area,
        s with { SourceDiscRadiusDegrees = 89, SourceDiscSoftness = 0 }, 1);
    var focused = Sample(fadeSettings with { Spread = 4 });
    var absolute = Sample(fadeSettings with { Spread = 4, DirectionNormalization = 0 });
    Assert(focused.Max(c => c.Rgb.X) > absolute.Max(c => c.Rgb.X));
    var unfocused = Sample(fadeSettings with { Spread = 0, SeparateLeftRight = false });
    Assert(unfocused[0].Rgb == unfocused[1].Rgb);
    var rotated = Sample(fadeSettings with { Spread = 4, CameraYawOffset = -90 });
    Assert(focused[0].Rgb.X > focused[1].Rgb.X && rotated[1].Rgb.X > rotated[0].Rgb.X);
    Assert(LightMapper.DistanceWeight(10, fadeSettings with { FadeExponent = 1 }) > LightMapper.DistanceWeight(10, fadeSettings));
    foreach (var exponent in new[] { 0.1, 0.5, 2.0, 8.0 })
    {
        var edge = LightMapper.DistanceWeight(Math.BitDecrement(fadeSettings.FadeEnd), fadeSettings with { FadeExponent = exponent });
        Assert(double.IsFinite(edge) && edge is >= 0 and <= 1, "Fractional distance falloff failed near the cutoff");
    }
    var ambient = new AmbientFrame(42, DateTimeOffset.UtcNow, 0, 0, 8);
    Vec3 Day(double strength) => new LightMapper().Map(frame, fadeArea,
        fadeSettings with { AmbientOutput = 0, AmbientSmoothingMs = 0, DaylightLocalStrength = strength }, 1, ambient)[0].Rgb;
    Assert(Day(0).X < Day(1).X && Day(2).X > Day(1).X);
});
Add("Every mapping parameter rejects NaN", () =>
{
    foreach (var property in typeof(MappingSettings).GetProperties().Where(p => p.CanWrite && p.PropertyType == typeof(double)))
    {
        var settings = new MappingSettings();
        property.SetValue(settings, double.NaN);
        Reject(() => LightMapper.Validate(settings));
    }
});
Add("Invalid fade intervals are rejected and invalid distances contribute nothing", () =>
{
    foreach (var start in new[] { -1, 15, 16, double.NaN, double.PositiveInfinity })
        Reject(() => LightMapper.Validate(fadeSettings with { FadeStart = start }));
    foreach (var distance in new[] { -1, double.NaN, double.PositiveInfinity })
        Assert(LightMapper.DistanceWeight(distance, fadeSettings) == 0);
    LightMapper.Validate(fadeSettings with { FadeStart = 0 });
});
Add("Smoothing releases the previous color after leaving the fade range", () =>
{
    var mapper = new LightMapper(); var smooth = fadeSettings with { SmoothingMs = 100, SeparateLeftRight = false, SeparateFrontRear = false };
    var near = mapper.Map(DistanceFrame(2, new(1e8, 0, 0)), fadeArea, smooth, 1)[0].Rgb.X;
    var released = mapper.Map(DistanceFrame(15, new(1e8, 0, 0)), fadeArea, smooth, 0.1)[0].Rgb.X;
    Assert(released > 0 && released < near);
    for (var i = 0; i < 10; i++) released = mapper.Map(DistanceFrame(15, new(1e8, 0, 0)), fadeArea, smooth, 0.1)[0].Rgb.X;
    Assert(released < 0.0001);
});
Add("Legacy radius migrates to fade end and new fade settings round-trip", () =>
{
    const string legacy = "{\"BridgeAddress\":\"192.168.2.1\",\"AreaId\":\"saved-area\",\"Mapping\":{\"Gain\":2,\"Brightness\":0.4,\"Radius\":47.5,\"Spread\":3,\"SmoothingMs\":250}}";
    var saved = JsonSerializer.Deserialize<AppSettings>(legacy)!;
    Assert(saved.Mapping is { FadeStart: 0, FadeEnd: 47.5, Gain: 2, Brightness: 0.4, Spread: 3, SmoothingMs: 250, AmbientSensitivity: 1 });
    Assert(saved.Mapping is { SeparateLeftRight: true, SeparateFrontRear: true, SourceDiscRadiusDegrees: 60, SourceDiscSoftness: 0.25, DirectionOriginBlend: 0.5 },
        "Older settings must load with the visible strict boundaries and bounded disc defaults");
    var directory = Path.Combine(Path.GetTempPath(), "CrimsonHue-fade-test-" + Guid.NewGuid().ToString("N"));
    var store = new SettingsStore(directory);
    try
    {
        store.SaveSettings(saved with { Mapping = saved.Mapping! with { FadeStart = 5.5, Radius = 12.5, AmbientSensitivity = 1.8 } });
        var restored = store.LoadSettings();
        Assert(restored.AreaId == "saved-area" && restored.Mapping is { FadeStart: 5.5, FadeEnd: 12.5, AmbientSensitivity: 1.8 });
        Assert(!File.ReadAllText(Path.Combine(directory, "settings.json")).Contains("FadeEnd"), "Alias was serialized instead of the compatible Radius key");
    }
    finally { File.Delete(Path.Combine(directory, "settings.json")); Directory.Delete(directory); }
});
Add("Saved settings retain every explicit control and do not silently migrate brightness", () =>
{
    var directory = Path.Combine(Path.GetTempPath(), "CrimsonHue-brightness-test-" + Guid.NewGuid().ToString("N"));
    var store = new SettingsStore(directory);
    Directory.CreateDirectory(directory);
    try
    {
        var oldDefault = new MappingSettings(Brightness: 0.6);
        File.WriteAllText(Path.Combine(directory, "settings.json"), JsonSerializer.Serialize(new AppSettings(Mapping: oldDefault)));
        var restored = store.LoadSettings();
        Assert(restored.Mapping is { Brightness: 0.6 } && restored.MappingRevision == 0, "Legacy brightness changed silently");
        var custom = oldDefault with { AmbientOutput = 0.25, AmbientFloor = 0.05, AmbientCutoff = 0.02, DaylightLocalStrength = 0.3,
            LocalStrength = 2, AmbientReferenceLevel = 5, AmbientSmoothingMs = 700, HueShiftDegrees = -15, Saturation = 0.8,
            RedGain = 0.9, GreenGain = 1.2, BlueGain = 0.7, OutputGamma = 1.3, DirectionNormalization = 0.5,
            CameraYawOffset = 20, FadeExponent = 1.6, AmbientTintHue = 210, AmbientTintSaturation = 0.2,
            SeparateLeftRight = false, SeparateFrontRear = false, SourceDiscRadiusDegrees = 42, SourceDiscSoftness = 0.6, DirectionOriginBlend = .37 };
        store.SaveSettings(restored with { Mapping = custom });
        Assert(store.LoadSettings().Mapping == custom, "An explicit mapping value did not round-trip");
        File.WriteAllText(Path.Combine(directory, "settings.json"), JsonSerializer.Serialize(new AppSettings(Mapping: oldDefault with { Radius = 50 })));
        Assert(store.LoadSettings().Mapping is { Brightness: 0.6, Radius: 50 }, "Custom legacy settings were changed");
    }
    finally { File.Delete(Path.Combine(directory, "settings.json")); Directory.Delete(directory); }
});
Add("HueStream v2 wire golden: UUID, RGB and noncontiguous channels", () =>
{
    var bytes = EntertainmentPacket.Build(area.Id, 254, [new(2, new(1, 0, 0.5)), new(5, new(0, 1, 0))]);
    Assert(bytes.Length == 66 && Encoding.ASCII.GetString(bytes, 0, 9) == "HueStream");
    Assert(bytes[9] == 2 && bytes[10] == 0 && bytes[11] == 254 && bytes[14] == 0);
    Assert(Encoding.ASCII.GetString(bytes, 16, 36) == area.Id);
    Assert(bytes.AsSpan(52).SequenceEqual(new byte[] { 2, 255, 255, 0, 0, 128, 0, 5, 0, 0, 255, 255, 0, 0 }));
    Reject(() => EntertainmentPacket.Build(area.Id, 0, [new(1, new(double.NaN, 0, 0))]));
    Reject(() => EntertainmentPacket.Build(area.Id, 0, [new(1, default), new(1, default)]));
});
Add("Local endpoint restrictions", () =>
{
    Assert(BridgeClient.NormalizeAddress("192.168.2.109") == "https://192.168.2.109");
    Reject(() => BridgeClient.NormalizeAddress("http://192.168.2.109"));
    Reject(() => BridgeClient.NormalizeAddress("https://example.com"));
    Reject(() => BridgeClient.NormalizeAddress("https://192.168.2.109/path"));
    Reject(() => TelemetryClient.ValidateEndpoint("ws://192.168.2.109/v1/stream"));
    Assert(TelemetryClient.ValidateEndpoint("ws://127.0.0.1:27311/v1/stream").IsLoopback);
});
Async("HTTPS pairing: pin trust, link-button error, client key and channel discovery", async () =>
{
    await using var mock = await MockBridge.StartAsync(true);
    var probe = await BridgeClient.ProbeAsync(mock.Address);
    Assert(probe.CertificateSha256?.Length == 64);
    await RejectAsync(() => BridgeClient.PairAsync(probe));
    mock.LinkButton = true;
    var credentials = await BridgeClient.PairAsync(probe);
    Assert(credentials.ClientKey == MockBridge.ClientKey && mock.RequestedClientKey);
    using var bridge = new BridgeClient(credentials);
    var areas = await bridge.GetAreasAsync(); Assert(areas[0].Channels[0].Label == "Test gradient");
    using var wrong = new BridgeClient(credentials with { CertificateSha256 = new string('0', 64) });
    await RejectAsync(() => wrong.GetAreasAsync());
    using var unauthorized = new BridgeClient(credentials with { ApplicationKey = "incorrect" });
    await RejectAsync(() => unauthorized.GetAreasAsync());
});
Async("Session starts, sends, stops and restores selected light only", async () =>
{
    await using var mock = await MockBridge.StartAsync();
    using var bridge = new BridgeClient(mock.Credentials);
    var transport = new SpyTransport(); var session = new StreamingSession(bridge, () => transport);
    var state = new TelemetryState(); state.Accept(Fixtures.Snapshot());
    using var cancel = new CancellationTokenSource(10000);
    var running = session.RunAsync(area, state, () => new(), cancel.Token);
    await Fixtures.WaitUntil(() => transport.Packets.Count > 1);
    cancel.Cancel(); await running;
    Assert(mock.Starts == 1 && mock.Stops == 1 && mock.Restored == 1);
    Assert(transport.Disposed && transport.Packets.Last().Skip(52).Where((_, i) => i % 7 != 0).All(v => v == 0));
});
Async("Failed DTLS handshake releases REST lease and restores lights", async () =>
{
    await using var mock = await MockBridge.StartAsync(); using var bridge = new BridgeClient(mock.Credentials);
    var state = new TelemetryState(); state.Accept(Fixtures.Snapshot());
    using var cancel = new CancellationTokenSource(10000);
    await RejectAsync(() => new StreamingSession(bridge, () => new SpyTransport { FailConnect = true }).RunAsync(area, state, () => new(), cancel.Token));
    Assert(mock.Starts == 1 && mock.Stops == 1 && mock.Restored == 1);
});
Async("Busy Entertainment area is never taken over", async () =>
{
    await using var mock = await MockBridge.StartAsync(); mock.Active = true; mock.Owner = "other-app";
    using var bridge = new BridgeClient(mock.Credentials); var state = new TelemetryState(); state.Accept(Fixtures.Snapshot());
    await RejectAsync(() => new StreamingSession(bridge, () => new SpyTransport()).RunAsync(area, state, () => new(), default));
    Assert(mock.Starts == 0 && mock.Stops == 0 && mock.Restored == 0);
});
Async("Stop during bridge activation reconciles and releases the pending lease", async () =>
{
    await using var mock = await MockBridge.StartAsync(); mock.StartDelayMs = 250;
    using var bridge = new BridgeClient(mock.Credentials); var state = new TelemetryState(); state.Accept(Fixtures.Snapshot());
    using var cancel = new CancellationTokenSource(10000);
    var run = new StreamingSession(bridge, () => new SpyTransport()).RunAsync(area, state, () => new(), cancel.Token);
    await Fixtures.WaitUntil(() => mock.Starts == 1); cancel.Cancel(); await run;
    Assert(mock.Stops == 1 && mock.Restored == 1 && !mock.Active);
});
Async("Stale telemetry clears colors then releases area", async () =>
{
    await using var mock = await MockBridge.StartAsync(); using var bridge = new BridgeClient(mock.Credentials);
    var state = new TelemetryState(); state.Accept(Fixtures.Snapshot()); var transport = new SpyTransport();
    using var cancel = new CancellationTokenSource(10000);
    var run = new StreamingSession(bridge, () => transport).RunAsync(area, state, () => new(), cancel.Token);
    await Fixtures.WaitUntil(() => mock.Stops > 0, 5000); cancel.Cancel(); await run;
    Assert(mock.Restored == 1 && transport.Disposed);
});
Async("Other app takeover prevents cleanup from overwriting it", async () =>
{
    await using var mock = await MockBridge.StartAsync(); using var bridge = new BridgeClient(mock.Credentials);
    var state = new TelemetryState(); var transport = new SpyTransport();
    using var cancel = new CancellationTokenSource(10000);
    var producer = Fixtures.Produce(state, cancel.Token);
    var session = new StreamingSession(bridge, () => transport);
    var run = session.RunAsync(area, state, () => new(), cancel.Token);
    await Fixtures.WaitUntil(() => transport.Packets.Count > 1); mock.Owner = "other-app";
    await RejectAsync(() => run); cancel.Cancel(); await producer;
    Assert(mock.Stops == 0 && mock.Restored == 0);
});
Async("Fragmented large WebSocket messages and close invalidation", async () =>
{
    var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
    await using var app = builder.Build(); app.UseWebSockets();
    var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    app.Map("/v1/stream", async context =>
    {
        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        var node = JsonNode.Parse(Fixtures.Snapshot())!; node["futureData"] = new string('x', 100000);
        var data = Encoding.UTF8.GetBytes(node.ToJsonString());
        await ws.SendAsync(data.AsMemory(0, 50000), WebSocketMessageType.Text, false, default);
        await ws.SendAsync(data.AsMemory(50000), WebSocketMessageType.Text, true, default); sent.SetResult();
        await release.Task;
        await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "done", default);
    });
    await app.StartAsync(); using var client = new ClientWebSocket(); using var cancel = new CancellationTokenSource(10000);
    await client.ConnectAsync(new Uri(app.Urls.Single().Replace("http:", "ws:") + "/v1/stream"), cancel.Token);
    var state = new TelemetryState(); var receiving = TelemetryClient.ReceiveAsync(client, state, cancel.Token);
    await sent.Task; await Fixtures.WaitUntil(() => state.Read(out _) != null);
    release.SetResult(); await receiving; Assert(state.Read(out _) == null);
});
Async("Real DTLS-PSK interoperability against OpenSSL", async () =>
{
    var openssl = args.SkipWhile(x => x != "--openssl").Skip(1).FirstOrDefault() ?? @"C:\Program Files\Git\usr\bin\openssl.exe";
    if (!File.Exists(openssl)) throw new Exception("OpenSSL required for DTLS interoperability test; pass --openssl path.");
    using var portSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    portSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0)); var port = ((IPEndPoint)portSocket.LocalEndPoint!).Port; portSocket.Close();
    var start = new ProcessStartInfo(openssl) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
    foreach (var a in new[] { "s_server", "-dtls1_2", "-psk", MockBridge.ClientKey, "-psk_identity", "test-app", "-nocert", "-accept", "127.0.0.1:" + port, "-quiet", "-cipher", "PSK-AES128-GCM-SHA256" }) start.ArgumentList.Add(a);
    using var process = Process.Start(start)!;
    var error = process.StandardError.ReadToEndAsync();
    try
    {
        await Task.Delay(350); using var cancel = new CancellationTokenSource(10000);
        using var transport = new EntertainmentTransport(port);
        await transport.ConnectAsync(new("http://127.0.0.1", "test-app", MockBridge.ClientKey, null), cancel.Token);
        var packet = EntertainmentPacket.Build(area.Id, 8, [new(2, new(1, 0.25, 0)), new(5, new(0, 0, 1))]);
        transport.Send(packet);
        var actual = new byte[packet.Length]; await process.StandardOutput.BaseStream.ReadExactlyAsync(actual, cancel.Token);
        Assert(packet.SequenceEqual(actual), "OpenSSL decrypted bytes differ from packet");
    }
    finally { if (!process.HasExited) process.Kill(); await process.WaitForExitAsync(); var stderr = await error; if (process.ExitCode != 0 && !stderr.Contains("unexpected eof")) { /* server terminated by test */ } }
});
Add("DPAPI secrets roundtrip without plaintext in settings", () =>
{
    var directory = Path.Combine(Path.GetTempPath(), "CrimsonHue-tests-" + Guid.NewGuid().ToString("N"));
    var store = new SettingsStore(directory);
    var credentials = new BridgeCredentials("https://192.168.2.109", "synthetic-app-key", MockBridge.ClientKey, new string('A', 64));
    store.SaveCredentials(credentials); store.SaveSettings(new());
    Assert(store.LoadCredentials() == credentials);
    Assert(!Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(directory, "bridge.secrets"))).Contains("synthetic-app-key"));
    Assert(!File.ReadAllText(Path.Combine(directory, "settings.json")).Contains("synthetic-app-key"));
    // Only delete the exact synthetic files created by this test.
    File.Delete(Path.Combine(directory, "bridge.secrets")); File.Delete(Path.Combine(directory, "settings.json")); Directory.Delete(directory);
});
if (args.Contains("--live") || args.Contains("--live-bridge")) Async("LIVE read-only bridge identity", async () =>
{
    var probe = await BridgeClient.ProbeAsync("192.168.2.109");
    Console.WriteLine($"  Bridge: {probe.Name}; HTTPS certificate received: {probe.CertificateSha256?.Length == 64}");
});
if (args.Contains("--live") || args.Contains("--live-telemetry")) Async("LIVE progressing CDT WebSocket and mapping", async () =>
{
    var state = new TelemetryState(); using var cancel = new CancellationTokenSource(10000);
    var task = new TelemetryClient(state).RunAsync(TelemetryClient.ValidateEndpoint("ws://127.0.0.1:27311/v1/stream"), cancel.Token);
    var captures = new HashSet<long>(); var sequences = new HashSet<long>(); var maxLights = 0; var allAroundFrames = 0;
    for (var i = 0; i < 40; i++)
    {
        await Task.Delay(100); var f = state.Read(out _); if (f == null) continue;
        captures.Add(f.CaptureSequence); sequences.Add(f.Sequence); maxLights = Math.Max(maxLights, f.Lights.Count);
        if (f.Feed == "all-around") allAroundFrames++;
        var mapped = new LightMapper().Map(f, area, new(), 0.1); Assert(mapped.All(c => c.Rgb.IsFinite));
    }
    cancel.Cancel(); await task;
    Console.WriteLine($"  Live control: {sequences.Count} envelopes; {captures.Count} captures; {allAroundFrames} all-around frames; up to {maxLights} confirmed clear lights");
    Assert(captures.Count > 10 && sequences.Count > 10 && allAroundFrames > 0, "No progressing all-around control");
});
if (args.Contains("--live") || args.Contains("--live-ambient")) Async("LIVE progressing CDT Ambient WebSocket", async () =>
{
    var state = new AmbientState(); using var cancel = new CancellationTokenSource(10000);
    var endpoint = AmbientClient.FromTelemetryEndpoint(TelemetryClient.ValidateEndpoint("ws://127.0.0.1:27311/v1/stream"));
    var task = new AmbientClient(state).RunAsync(endpoint, cancel.Token);
    var captures = new HashSet<long>(); var usable = 0; var maximumLevel = 0.0;
    for (var i = 0; i < 40; i++)
    {
        await Task.Delay(100); var frame = state.Read(out _); if (frame == null) continue;
        captures.Add(frame.CaptureSequence); usable++; maximumLevel = Math.Max(maximumLevel, frame.WorkingLevel);
    }
    cancel.Cancel(); await task;
    Console.WriteLine($"  Ambient control: {usable} fresh reads; {captures.Count} sky captures; maximum relative level {maximumLevel:F2}");
    Assert(usable > 10 && captures.Count >= 3, "No progressing Ambient control");
});
if (args.Contains("--live-mapping")) Async("LIVE read-only saved-layout mapping and camera yaw", async () =>
{
    var store = new SettingsStore();
    var credentials = store.LoadCredentials() ?? throw new Exception("Pair CrimsonHue first.");
    var saved = store.LoadSettings();
    using var bridge = new BridgeClient(credentials);
    var layout = (await bridge.GetAreasAsync()).Single(a => a.Id == saved.AreaId);
    var telemetry = new TelemetryState();
    var ambient = new AmbientState();
    using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    var endpoint = TelemetryClient.ValidateEndpoint(saved.TelemetryAddress);
    var telemetryTask = new TelemetryClient(telemetry).RunAsync(endpoint, cancel.Token);
    var ambientTask = new AmbientClient(ambient).RunAsync(AmbientClient.FromTelemetryEndpoint(endpoint), cancel.Token);
    try
    {
        await Fixtures.WaitUntil(() => telemetry.Read(out _) is { Feed: "all-around" } && ambient.Read(out _) != null, 5000);
        var frame = telemetry.Read(out _)!;
        var ambientFrame = ambient.Read(out _)!;
        var mapping = saved.Mapping ?? new();
        IReadOnlyList<ChannelColor> Map(TelemetryFrame f, bool includeAmbient = true, double? originBlend = null)
        {
            var mapper = new LightMapper();
            IReadOnlyList<ChannelColor> output = [];
            var options = originBlend is { } blend ? mapping with { DirectionOriginBlend = blend } : mapping;
            for (var i = 0; i < 3; i++) output = mapper.Map(f, layout, options, 1, includeAmbient ? ambientFrame : null);
            return output;
        }
        var ahead = Map(frame);
        var turned = Map(frame with { Camera = frame.Camera with { Right = frame.Camera.Right * -1, Forward = frame.Camera.Forward * -1 } });
        var baseline = Map(frame with { Lights = [] });
        var localOnly = Map(frame, includeAmbient: false);
        var playerOrigin = Map(frame, includeAmbient: false, originBlend: 0);
        var cameraOrigin = Map(frame, includeAmbient: false, originBlend: 1);
        Console.WriteLine($"  {frame.Lights.Count} clear sources; raw Ambient W={ambientFrame.WorkingLevel:G9}; max brightness={mapping.Brightness:P0}");
        Console.WriteLine($"  Strict left/right={mapping.SeparateLeftRight}; front/rear={mapping.SeparateFrontRear}; disc radius={mapping.SourceDiscRadiusDegrees:G} deg; soft edge={mapping.SourceDiscSoftness:P0}");
        Console.WriteLine($"  Direction origin={mapping.DirectionOriginBlend:P0} camera; world origin={LightMapper.DirectionOrigin(frame, mapping)}; distance cutoff={mapping.FadeEnd:G} GU (saved settings)");
        Console.WriteLine($"  Raw Ambient working RGB={ambientFrame.WorkingRgb}; sky RGB={ambientFrame.SkyRgb}; visibility={ambientFrame.SkyVisibility:G9}");
        for (var i = 0; i < layout.Channels.Count; i++)
        {
            Console.WriteLine($"  CH {layout.Channels[i].Id}: actual {ahead[i].Rgb}, yaw +180° {turned[i].Rgb}, no local lights {baseline[i].Rgb}, local only {localOnly[i].Rgb}");
            Console.WriteLine($"  CH {layout.Channels[i].Id}: local origin 0% {playerOrigin[i].Rgb}; local origin 100% {cameraOrigin[i].Rgb}");
        }
        Assert(ahead.All(c => c.Rgb.IsFinite) && turned.All(c => c.Rgb.IsFinite));
    }
    finally { await cancel.CancelAsync(); await telemetryTask; await ambientTask; }
});
if (args.Contains("--live-lamps")) Async("LIVE bounded Entertainment output and restoration", async () =>
{
    var store = new SettingsStore();
    var credentials = store.LoadCredentials() ?? throw new Exception("Pair CrimsonHue first.");
    var saved = store.LoadSettings();
    using var bridge = new BridgeClient(credentials);
    var areas = await bridge.GetAreasAsync();
    Assert(!areas.Any(a => a.Active), "An Entertainment area is already active; refusing takeover.");
    var area = areas.SingleOrDefault(a => a.Id == saved.AreaId) ?? throw new Exception("Saved Entertainment area is unavailable.");
    var telemetry = new TelemetryState();
    var ambient = new AmbientState();
    using var telemetryCancel = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    var telemetryTask = new TelemetryClient(telemetry).RunAsync(TelemetryClient.ValidateEndpoint(saved.TelemetryAddress), telemetryCancel.Token);
    var ambientTask = new AmbientClient(ambient).RunAsync(
        AmbientClient.FromTelemetryEndpoint(TelemetryClient.ValidateEndpoint(saved.TelemetryAddress)), telemetryCancel.Token);
    using var streamCancel = new CancellationTokenSource();
    var session = new StreamingSession(bridge);
    Task? streamTask = null;
    var sawOutput = false;
    var sawNeutralOutput = false;
    try
    {
        await Fixtures.WaitUntil(() => telemetry.Read(out _) is { Feed: "all-around" }, 5000);
        await Fixtures.WaitUntil(() => ambient.Read(out _) != null, 5000);
        var mapping = new MappingSettings(Gain: 1, Brightness: 0.25, Radius: 100, SmoothingMs: 0);
        streamTask = session.RunAsync(area, telemetry, () => mapping, streamCancel.Token, ambient);
        await Fixtures.WaitUntil(() => session.Sending || streamTask.IsCompleted, 10000);
        Assert(session.Sending, "Entertainment stream did not start.");
        for (var i = 0; i < 50 && !streamTask.IsCompleted; i++)
        {
            sawOutput |= session.Colors.Any(c => c.Rgb.Length > 0);
            sawNeutralOutput |= session.Colors.Any(c => c.Rgb.X > 0.1 && c.Rgb.Y > 0.1 && c.Rgb.Z > 0.1 &&
                Math.Max(c.Rgb.X, Math.Max(c.Rgb.Y, c.Rgb.Z)) < 1.2 * Math.Min(c.Rgb.X, Math.Min(c.Rgb.Y, c.Rgb.Z)));
            await Task.Delay(100);
        }
    }
    finally
    {
        await streamCancel.CancelAsync();
        try { if (streamTask != null) await streamTask; }
        finally { await telemetryCancel.CancelAsync(); await telemetryTask; await ambientTask; }
    }
    Assert(!(await bridge.GetAreasAsync()).Any(a => a.Active), "Entertainment area was not released.");
    Console.WriteLine($"  Bounded lamp run: stream started, nonzero output: {sawOutput}; neutral ambient output: {sawNeutralOutput}; area released and light states restored");
});

var failures = 0;
foreach (var test in tests)
{
    try { await test.Run(); Console.WriteLine("PASS " + test.Name); }
    catch (Exception ex) { failures++; Console.WriteLine("FAIL " + test.Name + "\n" + ex); }
}
Console.WriteLine($"{tests.Count - failures}/{tests.Count} passed");
return failures == 0 ? 0 : 1;

static class Fixtures
{
    public const string Id = "12345678-1234-1234-1234-123456789abc";
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public static byte[] Snapshot(long sequence = 1, DateTimeOffset? at = null)
    {
        var now = at ?? DateTimeOffset.UtcNow;
        var pose = new { position = new Vec3(0, 2, 0), right = new Vec3(1, 0, 0), up = new Vec3(0, 1, 0), forward = new Vec3(0, 0, 1) };
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = "1.4", sequence, capturedAt = now, game = new { state = "playing" },
            coordinateSystem = new { unit = "game-unit", handedness = "right", upAxis = "y" },
            capabilities = new[] { "lights.rendered", "player.position", "camera.transform" }, player = new { position = new Vec3(0, 0, 0) },
            camera = new { position = new Vec3(999, 999, 999) },
            lights = new { status = "unavailable", rendered = new { status = "available", source = "filtered-manylights", captureSequence = sequence,
                capturedAt = now, ageMilliseconds = 0, camera = pose,
                sources = new[] { new { position = new Vec3(-5, 2, 5), colorLinear = new Vec3(2, 0, 0) }, new { position = new Vec3(5, 2, 5), colorLinear = new Vec3(0, 0, 2) } } } }
        }, Options);
    }
    public static byte[] CurrentSnapshot()
    {
        var node = JsonNode.Parse(Snapshot())!;
        node["schemaVersion"] = "1.6";
        node["capabilities"] = new JsonArray("lights.rendered", "lights.upstream", "player.position", "camera.transform");
        var rendered = node["lights"]!["rendered"]!;
        rendered["frameNumber"] = 55;
        JsonObject Light(Vec3 position, string status, string? method = "physics-ray-fan") => new()
        {
            ["position"] = JsonSerializer.SerializeToNode(position, Options),
            ["colorLinear"] = JsonSerializer.SerializeToNode(new Vec3(2, 0.2, 0.1), Options),
            ["sourceVisibility"] = new JsonObject
            {
                ["status"] = status, ["method"] = method, ["volumeAgeMillisecondsAtCapture"] = 100
            }
        };
        node["lights"]!["upstream"] = new JsonObject
        {
            ["status"] = "available", ["source"] = "manylights-input",
            ["captureSequence"] = rendered["captureSequence"]!.DeepClone(),
            ["frameNumber"] = rendered["frameNumber"]!.DeepClone(),
            ["capturedAt"] = rendered["capturedAt"]!.DeepClone(), ["ageMilliseconds"] = 0,
            ["sources"] = new JsonArray(Light(new(0, 2, -8), "clear"), Light(new(0, 2, 8), "blocked"),
                Light(new(4, 2, 5), "unknown"), Light(new(-4, 2, 5), "clear", "other-method"))
        };
        return Encoding.UTF8.GetBytes(node.ToJsonString());
    }
    public static byte[] Ambient(DateTimeOffset? at = null, int skyAge = 0, int visibilityAge = 0)
    {
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = "1.0", status = "available", source = "precompute-ambient-sky",
            scope = "global-upper-hemisphere-sky", units = "relative-shader-units",
            captureSequence = 42, capturedAt = at ?? DateTimeOffset.UtcNow,
            ageMilliseconds = skyAge, sky = new { upperHemisphereMeanWorking = new[] { 24.0, 32.0, 40.0 } },
            visibility = new { valueWorking = 0.25, frameNumber = 55, ageMilliseconds = visibilityAge },
            localEnvironmentAmbientEstimateWorking = new { available = true, stale = false, rgbWorking = new[] { 6.0, 8.0, 10.0 } }
        }, Options);
    }
    public static string AreaJson(bool active = false, string owner = "test-app") => JsonSerializer.Serialize(new
    {
        id = Id, metadata = new { name = "Test area" }, status = active ? "active" : "inactive", active_streamer = new { rid = owner },
        channels = new[] {
            new { channel_id = 2, position = new Vec3(-0.8, 0.8, 0.2), members = new[] { new { index = 0, service = new { rid = "ent-light" } } } },
            new { channel_id = 5, position = new Vec3(0.8, 0.8, 0.2), members = new[] { new { index = 1, service = new { rid = "ent-light" } } } }
        }
    }, Options);
    public static EntertainmentArea Area() { using var doc = JsonDocument.Parse(AreaJson()); return BridgeClient.ParseArea(doc.RootElement); }
    public static async Task WaitUntil(Func<bool> test, int ms = 3000)
    {
        var watch = Stopwatch.StartNew(); while (!test()) { if (watch.ElapsedMilliseconds > ms) throw new Exception("Condition timed out"); await Task.Delay(20); }
    }
    public static async Task Produce(TelemetryState state, CancellationToken token)
    {
        long sequence = 1;
        while (!token.IsCancellationRequested) { state.Accept(Snapshot(sequence++)); try { await Task.Delay(50, token); } catch (OperationCanceledException) { } }
    }
}

sealed class SpyTransport : IEntertainmentTransport
{
    public System.Collections.Concurrent.ConcurrentQueue<byte[]> Packets { get; } = new();
    public bool FailConnect { get; init; }
    public bool Disposed { get; private set; }
    public Task ConnectAsync(BridgeCredentials credentials, CancellationToken token) => FailConnect ? Task.FromException(new IOException("Synthetic handshake failure")) : Task.CompletedTask;
    public void Send(byte[] packet) => Packets.Enqueue(packet);
    public void Dispose() => Disposed = true;
}

sealed class MockBridge : IAsyncDisposable
{
    public const string ClientKey = "00112233445566778899AABBCCDDEEFF";
    public bool LinkButton; public bool RequestedClientKey; public bool Active;
    public string Owner = "test-app";
    public int Starts; public int Stops; public int Restored;
    public int StartDelayMs;
    public string Address = "";
    public BridgeCredentials Credentials => new(Address, "test-app", ClientKey, null);
    private WebApplication app = null!;
    private X509Certificate2? certificate;
    public static async Task<MockBridge> StartAsync(bool https = false)
    {
        var mock = new MockBridge(); var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders();
        if (https)
        {
            using var rsa = RSA.Create(2048); var request = new CertificateRequest("CN=CrimsonHue-test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            mock.certificate = new X509Certificate2(generated.Export(X509ContentType.Pfx));
            builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, 0, l => l.UseHttps(mock.certificate)));
        }
        else builder.WebHost.UseUrls("http://127.0.0.1:0");
        mock.app = builder.Build();
        mock.app.Run(async ctx =>
        {
            ctx.Response.ContentType = "application/json";
            var path = ctx.Request.Path.Value!;
            if (path == "/api/config") { await ctx.Response.WriteAsJsonAsync(new { name = "Test bridge", bridgeid = "test-bridge" }); return; }
            if (path == "/api")
            {
                using var body = await JsonDocument.ParseAsync(ctx.Request.Body);
                mock.RequestedClientKey = body.RootElement.GetProperty("generateclientkey").GetBoolean();
                if (!mock.LinkButton) await ctx.Response.WriteAsync("[{\"error\":{\"type\":101}}]");
                else await ctx.Response.WriteAsJsonAsync(new[] { new { success = new { username = "test-app", clientkey = ClientKey } } });
                return;
            }
            if (ctx.Request.Headers["hue-application-key"] != "test-app") { ctx.Response.StatusCode = 403; return; }
            if (path.EndsWith("entertainment_configuration/" + Fixtures.Id) && ctx.Request.Method == "PUT")
            {
                using var body = await JsonDocument.ParseAsync(ctx.Request.Body);
                mock.Active = body.RootElement.GetProperty("action").GetString() == "start";
                if (mock.Active) { mock.Starts++; mock.Owner = "test-app"; } else mock.Stops++;
                if (mock.Active && mock.StartDelayMs > 0) await Task.Delay(mock.StartDelayMs);
                await ctx.Response.WriteAsync("{\"data\":[],\"errors\":[]}"); return;
            }
            if (path.EndsWith("/light/light-id") && ctx.Request.Method == "PUT")
            {
                using var body = await JsonDocument.ParseAsync(ctx.Request.Body);
                if (body.RootElement.GetProperty("on").GetProperty("on").GetBoolean()) throw new Exception("Incorrect restore state");
                mock.Restored++; await ctx.Response.WriteAsync("{\"data\":[],\"errors\":[]}"); return;
            }
            string data;
            if (path.Contains("entertainment_configuration")) data = Fixtures.AreaJson(mock.Active, mock.Owner);
            else if (path.EndsWith("/entertainment")) data = "{\"id\":\"ent-light\",\"renderer_reference\":{\"rid\":\"light-id\"}}";
            else if (path.EndsWith("/light")) data = "{\"id\":\"light-id\",\"metadata\":{\"name\":\"Test gradient\"},\"on\":{\"on\":false},\"dimming\":{\"brightness\":42},\"color\":{\"xy\":{\"x\":0.3,\"y\":0.3}}}";
            else { ctx.Response.StatusCode = 404; return; }
            await ctx.Response.WriteAsync("{\"errors\":[],\"data\":[" + data + "]}");
        });
        await mock.app.StartAsync(); mock.Address = mock.app.Urls.Single(); return mock;
    }
    public async ValueTask DisposeAsync() { await app.DisposeAsync(); certificate?.Dispose(); }
}
