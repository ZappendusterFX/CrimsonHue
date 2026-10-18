using CrimsonHue.Core;
using System.Text.Json;
using System.Text.Json.Nodes;

static class PlayerLanternTests
{
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    static TelemetryFrame Frame()
    {
        var at = DateTimeOffset.UtcNow;
        var position = new Vec3(.45, 1.05, .45);
        return new(1, 1, at, at, 0, default, new(default, new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)),
            [new(position, new(2.5, 1, .25), Metadata: new("standalone", "spot", new(0, 0, 1), 31.137793)),
             new(position, new(1, .4, .1), Metadata: new("standalone", "spot", new(0, 0, 1), 26.885372)),
             new(new(0, 1, 4), new(.03, .04, .06), Metadata: new("standalone", "spot", new(0, -1, 0), 26.997278))],
            Feed: "all-around", GameBuild: "25477059", PlayerForward: new(0, 0, 1));
    }
    public static void Register(Action<string, Action> add)
    {
        add("Point and spot weights affect their own kinds including groups and preserve untyped light", () =>
        {
            var f = Frame();
            var area = new EntertainmentArea(Fixtures.Id, "Typed point", false, [new(0, new(0, 1, 0), [], "Point")]);
            var settings = new MappingSettings(Brightness:1, SmoothingMs:0, Spread:0, SourceDiscRadiusDegrees:180);
            foreach (var type in new[] { "standalone", "group" })
            {
                var point = new LightContribution(new(0,1,4), new(.1,0,0), Metadata:new(type,"point",null,null));
                var spot = new LightContribution(new(0,1,4), new(0,0,.1), Metadata:new(type,"spot",null,null));
                var unknown = new LightContribution(new(0,1,4), new(0,.1,0));
                var frame = f with { Lights = [point,spot,unknown] };
                var blue = new LightMapper().Map(frame,area,settings with {PointLightStrength=0},1)[0].Rgb;
                var red = new LightMapper().Map(frame,area,settings with {SpotLightStrength=0},1)[0].Rgb;
                var green = new LightMapper().Map(frame,area,settings with {PointLightStrength=0,SpotLightStrength=0},1)[0].Rgb;
                Assert(blue.X==0 && blue.Z>0 && red.X>0 && red.Z==0 && green.X==0 && green.Y>0 && green.Z==0,
                    "Type weights changed the wrong kind or suppressed untyped data");
                Assert(frame.Lights.Count==3 && point.Rgb.X==.1, "Raw light changed with its weight");
            }
        });
        add("Own lantern strength scales continuously after spotlight weighting without changing other spots", () =>
        {
            var f = Frame(); var area = new EntertainmentArea(Fixtures.Id,"Point",false,[new(0,new(0,1,0),[],"Point")]);
            var settings = new MappingSettings(Brightness:1,SmoothingMs:0,Spread:0,SourceDiscRadiusDegrees:180,SpotLightStrength:1.5);
            var pair = f with {Lights=f.Lights.Take(2).Select(l=>l with {Rgb=l.Rgb*.01}).ToArray()};
            double Decode(double encoded) => encoded<=.04045 ? encoded/12.92 : Math.Pow((encoded+.055)/1.055,2.4);
            var full = new LightMapper().Map(pair,area,settings,1)[0].Rgb.X;
            var dim = new LightMapper().Map(pair,area,settings with {PlayerLanternStrength=.1},1)[0].Rgb.X;
            Assert(Math.Abs(Decode(dim)/Decode(full)-.1)<1e-10, "Lantern weight is not a continuous additional multiplier");
            Assert(new LightMapper().Map(pair,area,settings with {PlayerLanternStrength=0},1)[0].Rgb==default,"Zero did not remove the pair");
            var blue = f with {Lights=[f.Lights[2]]};
            Assert(new LightMapper().Map(blue,area,settings,1).SequenceEqual(new LightMapper().Map(blue,area,settings with {PlayerLanternStrength=0},1)),
                "Own-lantern weight altered the blue world spotlight");
            foreach(var invalid in new[]{-.01, double.NaN, double.PositiveInfinity, 2.01}) {
                try {LightMapper.Validate(settings with {PointLightStrength=invalid}); throw new Exception("Invalid point weight accepted");} catch(CrimsonHueException){}
            }
            foreach(var invalid in new[]{-.01, double.NaN, double.PositiveInfinity, 10.01}) {
                try {LightMapper.Validate(settings with {SpotLightStrength=invalid}); throw new Exception("Invalid spot weight accepted");} catch(CrimsonHueException){}
            }
            try {LightMapper.Validate(settings with {PlayerLanternStrength=1.01}); throw new Exception("Invalid lantern weight accepted");} catch(CrimsonHueException){}
        });
        add("Spotlights accept tenfold strength without dimming point-only scenes and persist through presets", () =>
        {
            var frame=Frame();
            var area=new EntertainmentArea(Fixtures.Id,"Point",false,[new(0,new(0,1,0),[],"Point")]);
            var settings=new MappingSettings(Brightness:1,SmoothingMs:0,Spread:0,SourceDiscRadiusDegrees:180);
            var spot=new LightContribution(new(0,1,4),new(0,0,.001),Metadata:new("standalone","spot",null,null));
            var point=spot with {Rgb=new(.001,0,0),Metadata=new("standalone","point",null,null)};
            Vec3 Map(LightContribution light,double weight)=>new LightMapper().Map(frame with {Lights=[light]},area,settings with {SpotLightStrength=weight},1)[0].Rgb;
            double Decode(double encoded)=>encoded<=.04045 ? encoded/12.92 : Math.Pow((encoded+.055)/1.055,2.4);
            Assert(Math.Abs(Decode(Map(spot,10).Z)/Decode(Map(spot,1).Z)-10)<1e-10,"Maximum spotlight boost is not tenfold");
            Assert(Map(point,10)==Map(point,1),"Spotlight range change dimmed point-only output");
            var prefs=new LightingPreferences(SpotLightStrength:10);
            Assert(LightingProfile.Resolve(prefs,settings).SpotLightStrength==10,"Recommended rejected maximum spot weight");
            var saved=new AppSettings(Mapping:settings with {SpotLightStrength=10},Lighting:prefs);
            var loaded=JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(saved))!;
            Assert(loaded.Mapping!.SpotLightStrength==10 && loaded.Lighting!.SpotLightStrength==10,"High weight was lost in local settings");
            var preset=MappingPreset.Import(System.Text.Encoding.UTF8.GetBytes(MappingPreset.Export("High spot weight",saved.Mapping!)));
            Assert(preset.Mapping.SpotLightStrength==10 && preset.ApplyTo(settings,PresetGroups.Output).SpotLightStrength==10,"High weight was lost in preset roundtrip");
            try{LightingProfile.Validate(prefs with {SpotLightStrength=10.01});throw new Exception("Out-of-range profile accepted");}catch(CrimsonHueException){}
        });
        add("Version one presets keep neutral weights and version two requires all three source controls", () =>
        {
            var current = JsonNode.Parse(MappingPreset.Export("New weights", new(PointLightStrength:.5,SpotLightStrength:1.5,PlayerLanternStrength:.1)))!;
            var legacy = current.DeepClone(); legacy["version"]=1;
            foreach(var key in new[]{"pointLightStrength","spotLightStrength","playerLanternStrength"}) legacy["mapping"]!.AsObject().Remove(key);
            var imported=MappingPreset.Import(JsonSerializer.SerializeToUtf8Bytes(legacy));
            Assert(imported.Mapping.PointLightStrength==1 && imported.Mapping.SpotLightStrength==1 && imported.Mapping.PlayerLanternStrength==1,
                "Legacy preset changed existing source balance");
            current["mapping"]!.AsObject().Remove("spotLightStrength");
            try {MappingPreset.Import(JsonSerializer.SerializeToUtf8Bytes(current)); throw new Exception("Incomplete v2 accepted");} catch(CrimsonHueException){}
            var prefs = JsonSerializer.Deserialize<LightingPreferences>("{}")!;
            Assert(prefs.PointLightStrength==1 && prefs.SpotLightStrength==1 && prefs.PlayerLanternStrength==.1,
                "Recommended omitted-field defaults changed");
            var custom = new MappingSettings(PointLightStrength:.3,SpotLightStrength:1.7,PlayerLanternStrength:.02);
            var tuned = new LightingPreferences(PointLightStrength:.6,SpotLightStrength:1.3,PlayerLanternStrength:.05);
            var effective = LightingProfile.Resolve(tuned,custom);
            Assert(effective.PointLightStrength==.6 && effective.SpotLightStrength==1.3 && effective.PlayerLanternStrength==.05 &&
                LightingProfile.Resolve(tuned with {Recommended=false},custom)==custom,"Profile weights leaked into the other mode");
        });
        add("Verified lantern signature excludes only the pair and keeps the raw frame intact", () =>
        {
            var frame = Frame(); var originals = frame.Lights.ToArray();
            Assert(PlayerLanternSignature.Find(frame).SetEquals(originals.Take(2)), "Lantern pair not recognized");
            var area = new EntertainmentArea(Fixtures.Id, "Single point", false, [new(0, new(0, 1, 0), [], "Point")]);
            var mapping = new MappingSettings(Brightness: 1, SmoothingMs: 0, SourceDiscRadiusDegrees: 180);
            var included = new LightMapper().Map(frame, area, mapping, 1);
            var ignored = new LightMapper().Map(frame, area, mapping with {PlayerLanternStrength=0}, 1);
            var expected = new LightMapper().Map(frame with { Lights = [originals[2]] }, area, mapping, 1);
            Assert(ignored.SequenceEqual(expected) && !ignored.SequenceEqual(included), "Filter/toggle did not affect only lantern output");
            Assert(frame.Lights.SequenceEqual(originals), "Raw frame was changed by output policy");
            var moved = frame with { Player = new(20, 3, -11), PlayerForward = new(1, 0, 0), Lights = frame.Lights.Select(l => l with {
                Position = new Vec3(20, 3, -11) + new Vec3(l.Position.Z, l.Position.Y, -l.Position.X),
                Metadata = l.Metadata! with { Direction = l.Metadata.Direction is { } d ? new Vec3(d.Z, d.Y, -d.X) : null } }).ToArray() };
            Assert(PlayerLanternSignature.Find(moved).Count == 2, "Recognition depended on a world coordinate or heading");
        });
        add("Lantern policy keeps incomplete ambiguous unsupported and non-player lights", () =>
        {
            var f = Frame(); var a = f.Lights[0]; var b = f.Lights[1];
            foreach (var invalid in new[] {
                f with { GameBuild = "future-build" }, f with { GameBuild = null }, f with { Feed = "rendered" },
                f with { PlayerForward = null }, f with { PlayerForward = new(0, 0, 0) },
                f with { Lights = [a] }, f with { Lights = [a, a, b] },
                f with { Lights = [a with { Metadata = null }, b] },
                f with { Lights = [a with { Metadata = a.Metadata! with { Type = "group" } }, b] },
                f with { Lights = [a with { Metadata = a.Metadata! with { Kind = "point" } }, b] },
                f with { Lights = [a with { Metadata = a.Metadata! with { Direction = new(0, -1, 0) } }, b] },
                f with { Lights = [a with { Metadata = a.Metadata! with { ConeHalfAngleDegrees = 40 } }, b] },
                f with { Lights = [a, b with { Rgb = b.Rgb * .5 }] },
                f with { Lights = [a with { Position = a.Position + new Vec3(.1, 0, 0) }, b] },
                f with { Lights = f.Lights.Select(l => l with { Position = l.Position + new Vec3(3, 0, 0) }).ToArray() },
                f with { Lights = f.Lights.Select(l => l with { Position = l.Position + new Vec3(-1, 0, 0) }).ToArray() }
            }) Assert(PlayerLanternSignature.Find(invalid).Count == 0, "A partial signature removed another source");
        });
        add("Optional lantern metadata survives parsing without filtering or invalidating raw input", () =>
        {
            var node = JsonNode.Parse(Fixtures.CurrentSnapshot())!;
            node["game"]!["build"] = "25477059";
            node["player"]!["orientation"] = JsonSerializer.SerializeToNode(new { source = "player-physics-root", forward = new Vec3(0, 0, 1) }, new JsonSerializerOptions {PropertyNamingPolicy=JsonNamingPolicy.CamelCase});
            var options = new JsonSerializerOptions {PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
            node["lights"]!["upstream"]!["sources"] = new JsonArray(Frame().Lights.Select(l => JsonSerializer.SerializeToNode(new {
                position=l.Position, colorLinear=l.Rgb, type=l.Metadata!.Type, kind=l.Metadata.Kind,
                direction=l.Metadata.Direction, coneHalfAngleDegrees=l.Metadata.ConeHalfAngleDegrees,
                sourceVisibility=new {status="clear",method="physics-ray-fan",volumeAgeMillisecondsAtCapture=0} }, options)).ToArray());
            var frame = TelemetryParser.Parse(JsonSerializer.SerializeToUtf8Bytes(node), out var status) ?? throw new Exception(status);
            Assert(frame.Lights.Count == 3 && PlayerLanternSignature.Find(frame).Count == 2, "Parser lost raw sources or signature annotations");
            node["lights"]!["upstream"]!["sources"]![0]!["direction"] = "invalid optional annotation";
            frame = TelemetryParser.Parse(JsonSerializer.SerializeToUtf8Bytes(node), out status) ?? throw new Exception(status);
            Assert(frame.Lights.Count == 3 && PlayerLanternSignature.Find(frame).Count == 0, "Bad optional metadata destroyed usable lighting");
            var saved = new AppSettings(Mapping: new(PlayerLanternStrength: .05), Lighting: new(PlayerLanternStrength: .2));
            var loaded = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(saved))!;
            Assert(loaded.Mapping!.PlayerLanternStrength == .05 && loaded.Lighting!.PlayerLanternStrength == .2,
                "Separate Custom/Recommended lantern weights did not persist");
        });
    }
}
