using CrimsonHue.Core;
using System.Text.Json;
using System.Text.Json.Nodes;

static class PlayerLanternTests
{
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    static TelemetryFrame Frame(bool currentPair = false)
    {
        var at = DateTimeOffset.UtcNow;
        var position = new Vec3(.45, 1.05, .45);
        return new(1, 1, at, at, 0, default, new(default, new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)),
            [new(position, new(1.5816667, .63611203, .17278898), Metadata: new("standalone", "spot", new(0, 0, 1), currentPair ? 33.571743 : 31.137793)),
             new(position, new(.6326667, .25444484, .069115594), Metadata: new("standalone", "spot", new(0, 0, 1), currentPair ? 22.3392 : 26.885372)),
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
            var settings = new MappingSettings(Brightness:1,Gain:.01,SmoothingMs:0,Spread:0,SourceDiscRadiusDegrees:180,SpotLightStrength:1.5);
            var pair = f with {Lights=f.Lights.Take(2).ToArray()};
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
        add("CDT build labels do not change complete-pair recognition or own-lantern weighting", () =>
        {
            var area = new EntertainmentArea(Fixtures.Id, "Point", false, [new(0, new(0, 1, 0), [], "Point")]);
            var settings = new MappingSettings(Brightness: 1, SmoothingMs: 0, SourceDiscRadiusDegrees: 180);
            foreach (var currentPair in new[] { false, true })
            {
            var original = Frame(currentPair);
            var expected = new LightMapper().Map(original with { Lights = [original.Lights[2]] }, area, settings, 1);
            foreach (var build in new string?[] { "25477059", "26361232", "future-build", null })
            {
                var frame = original with { GameBuild = build };
                Assert(PlayerLanternSignature.Find(frame).SetEquals(original.Lights.Take(2)),
                    "A CDT build label blocked an otherwise complete lantern pair");
                Assert(new LightMapper().Map(frame, area, settings with { PlayerLanternStrength = 0 }, 1).SequenceEqual(expected),
                    "Own-lantern weighting depended on the CDT game-build label");
                Assert(frame.Lights.SequenceEqual(original.Lights), "Recognition changed raw CDT light data");
            }
            }
        });
        add("Lantern pair recognition survives recorded swing envelopes without a player-heading dependency", () =>
        {
            // Relative positions/directions from the preserved corridor and lightning
            // recordings, including the 1.812 GU separation, near-ground pose and pitch.
            var poses = new (Vec3 Position, Vec3 Direction)[] {
                (new(-.598, .8452, -1.4873), new(-.060634192, .1536758, -.9862592)),
                (new(.231, -.0278, -.0434), new(-.06794744, .12771432, -.98948073)),
                (new(-.056, .83355, .5122), new(-.9345904, -.34351325, -.092409216)),
                (new(-.331, .9265, .5077), new(-.9516835, .2771064, .13232747)),
                (new(-.351, .7914, .3547), new(-.8135468, -.3506162, -.4639072)),
                (new(.45, .9046, -.1393), new(.4003362, .21005446, .8919686)) };
            foreach (var currentPair in new[] { false, true })
            foreach (var (position, direction) in poses)
            foreach (var heading in new Vec3?[] { null, default(Vec3), new(0, 0, 1), new(0, 1, 0), new(-1, 0, 0) })
            {
                var original = Frame(currentPair);
                var player = new Vec3(20, 3, -11);
                var pair = original.Lights.Take(2).Select(light => light with {
                    Position = player + position,
                    Metadata = light.Metadata! with { Direction = direction } }).ToArray();
                var frame = original with { Player = player, PlayerForward = heading, Lights = pair };
                Assert(PlayerLanternSignature.Find(frame).SetEquals(pair),
                    "A complete measured lantern pair fell back to ordinary spot strength during a swing");
                var varied = frame with { Lights = pair.Select(light => light with { Rgb = light.Rgb * 1.005 }).ToArray() };
                Assert(PlayerLanternSignature.Find(varied).Count == 2,
                    "Harmless sub-percent RGB variation rejected the complete pair");
            }
        });
        add("Own lantern zero ten and full strength preserve unrelated points spots and spell-colored cone pairs", () =>
        {
            double Decode(double encoded) => encoded <= .04045 ? encoded / 12.92 : Math.Pow((encoded + .055) / 1.055, 2.4);
            Vec3 Linear(ChannelColor color) => new(Decode(color.Rgb.X), Decode(color.Rgb.Y), Decode(color.Rgb.Z));
            var area = new EntertainmentArea(Fixtures.Id, "Centre", false, [new(0, default, [], "Centre")]);
            var settings = new MappingSettings(Brightness: 1, Gain: .001, SmoothingMs: 0, Spread: 0,
                SourceDiscRadiusDegrees: 180, PointLightStrength: .7, SpotLightStrength: 1.3);
            foreach (var currentPair in new[] { false, true })
            {
                var original = Frame(currentPair);
                var spellWide = original.Lights[0] with { Position = new(.3, .9, -.2), Rgb = new(20, 30, 80) };
                var spellNarrow = original.Lights[1] with { Position = spellWide.Position, Rgb = spellWide.Rgb / 2.5 };
                var others = new[] { original.Lights[2], spellWide, spellNarrow,
                    new LightContribution(new(.2, 1, 2.5), new(.2, .6, .1), Metadata: new("standalone", "point", null, null)) };
                var frame = original with { Lights = original.Lights.Take(2).Concat(others).ToArray() };
                Assert(PlayerLanternSignature.Find(frame).SetEquals(original.Lights.Take(2)),
                    "A different-colored pair using the same cones interfered with lantern recognition");
                ChannelColor Map(TelemetryFrame input, double strength) =>
                    new LightMapper().Map(input, area, settings with { PlayerLanternStrength = strength }, 1)[0];
                var off = Map(frame, 0);
                var ten = Map(frame, .1);
                var full = Map(frame, 1);
                Assert(off == Map(frame with { Lights = others }, 1),
                    "Zero own-lantern strength altered unrelated world or spell light");
                Assert((Linear(ten) - Linear(off) - (Linear(full) - Linear(off)) * .1).Length < 1e-10,
                    "Ten-percent own-lantern strength did not affect just the known pair");
                Assert((full.Rgb - off.Rgb).Length > .001 && frame.Lights.Count == 6,
                    "The fixture had no meaningful lantern output or raw sources were changed");
            }
        });
        add("Lantern policy keeps incomplete ambiguous and non-player lights on every CDT build", () =>
        {
            foreach (var currentPair in new[] { false, true })
            {
            var f = Frame(currentPair); var a = f.Lights[0]; var b = f.Lights[1];
            var otherProfile = Frame(!currentPair).Lights.Take(2).ToArray();
            TelemetryFrame At(Vec3 position) => f with { Lights = new[] { a, b }.Select(l => l with { Position = position }).ToArray() };
            foreach (var invalid in new[] {
                f with { Feed = "rendered" },
                f with { Player = new(double.NaN, 0, 0) }, f with { Player = new(0, double.PositiveInfinity, 0) },
                f with { Lights = [a] }, f with { Lights = [a, a, b] }, f with { Lights = [a, b, b] },
                f with { Lights = [a, b, otherProfile[0]] },
                f with { Lights = [a, b, otherProfile[0], otherProfile[1]] },
                f with { Lights = [otherProfile[0], b] },
                f with { Lights = [a with { Metadata = null }, b] },
                f with { Lights = [a with { Metadata = a.Metadata! with { Type = "group" } }, b] },
                f with { Lights = [a with { Metadata = a.Metadata! with { Kind = "point" } }, b] },
                f with { Lights = [a with { Metadata = a.Metadata! with { Direction = null } }, b] },
                f with { Lights = [a with { Metadata = a.Metadata! with { Direction = default(Vec3) } }, b] },
                f with { Lights = [a with { Metadata = a.Metadata! with { Direction = new(0, 0, 1.03) } }, b] },
                f with { Lights = [a with { Metadata = a.Metadata! with { Direction = new(0, -1, 0) } }, b] },
                f with { Lights = [a with { Metadata = a.Metadata! with { Direction = new(.1, 0, Math.Sqrt(.99)) } }, b] },
                f with { Lights = [a with { Metadata = a.Metadata! with { ConeHalfAngleDegrees = 40 } }, b] },
                f with { Lights = [a, b with { Rgb = b.Rgb * .5 }] },
                f with { Lights = [a with { Rgb = a.Rgb * 2 }, b with { Rgb = b.Rgb * 2 }] },
                f with { Lights = [a with { Rgb = a.Rgb * 1.02 }, b with { Rgb = b.Rgb * 1.02 }] },
                f with { Lights = [a with { Rgb = new(a.Rgb.X, a.Rgb.Y * 1.02, a.Rgb.Z) },
                                  b with { Rgb = new(b.Rgb.X, b.Rgb.Y * 1.02, b.Rgb.Z) }] },
                f with { Lights = [a with { Rgb = a.Rgb * 1.009 }, b with { Rgb = b.Rgb * .991 }] },
                f with { Lights = [a with { Rgb = new(double.NaN, a.Rgb.Y, a.Rgb.Z) }, b] },
                f with { Lights = [a with { Position = new(double.NaN, 1, 0) }, b] },
                f with { Lights = [a with { Position = a.Position + new Vec3(.031, 0, 0) }, b] },
                At(new(2.001, 0, 0)), At(new(0, 1.751, 0)), At(new(0, -.251, 0))
            })
                foreach (var build in new string?[] { "25477059", "26361232", "future-build", null })
                    Assert(PlayerLanternSignature.Find(invalid with { GameBuild = build }).Count == 0,
                        "A partial signature removed another source");
            }
        });
        add("Optional lantern metadata survives parsing without filtering or invalidating raw input", () =>
        {
            var node = JsonNode.Parse(Fixtures.CurrentSnapshot())!;
            node["game"]!["build"] = "26361232";
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
