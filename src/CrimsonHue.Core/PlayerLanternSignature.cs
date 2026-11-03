namespace CrimsonHue.Core;

/// <summary>Conservative recognition of the equipped lantern pair from CDT metadata.
/// This is not a durable engine identity or a general player-attachment classifier.</summary>
public static class PlayerLanternSignature
{
    private static readonly (double Wide, double Narrow)[] ConeProfiles =
        [(31.137793, 26.885372), (33.571743, 22.3392)];
    private static readonly Vec3 WideRgb = new(1.5816667, .63611203, .17278898);
    private static readonly Vec3 NarrowRgb = new(.6326667, .25444484, .069115594);

    public static IReadOnlySet<LightContribution> Find(TelemetryFrame frame)
    {
        var result = new HashSet<LightContribution>();
        // CDT validates the game build before publishing usable telemetry. Its
        // build label is diagnostic metadata, not a second allowlist for Hue.
        if (frame.Feed != "all-around" || !frame.Player.IsFinite) return result;
        bool Candidate(LightContribution light, double cone, Vec3 expectedRgb)
        {
            if (light.Metadata is not { Type: "standalone", Kind: "spot", Direction: { } direction,
                ConeHalfAngleDegrees: { } angle } || !direction.IsFinite || Math.Abs(direction.Length - 1) > .02 ||
                !double.IsFinite(angle) || Math.Abs(angle - cone) > .02 || !light.Position.IsFinite ||
                !MatchesEmission(light.Rgb, expectedRgb)) return false;
            var offset = light.Position - frame.Player;
            // The equipped lamp swings behind/alongside the player and tilts during
            // movement. Its measured emission pair identifies it inside this bounded
            // attachment volume; heading and a one-sided pose box do not.
            return offset.Length <= 2 && offset.Y is >= -.25 and <= 1.75;
        }
        var wide = new List<(LightContribution Light, int Profile)>();
        var narrow = new List<(LightContribution Light, int Profile)>();
        foreach (var light in frame.Lights)
        for (var profile = 0; profile < ConeProfiles.Length; profile++)
        {
            if (Candidate(light, ConeProfiles[profile].Wide, WideRgb)) wide.Add((light, profile));
            if (Candidate(light, ConeProfiles[profile].Narrow, NarrowRgb)) narrow.Add((light, profile));
        }
        // Missing/ambiguous pairs stay at the ordinary kind weight. Never identify by proximity,
        // color, source slot or a remembered world coordinate alone. Count across both
        // profiles so an extra candidate cannot silently select the wrong nearby pair.
        if (wide.Count != 1 || narrow.Count != 1 || wide[0].Profile != narrow[0].Profile) return result;
        var a = wide[0].Light; var b = narrow[0].Light;
        if ((a.Position - b.Position).Length > .03 || a.Rgb.Length <= 1e-9 ||
            (a.Rgb - b.Rgb * 2.5).Length > a.Rgb.Length * .01 ||
            a.Metadata!.Direction!.Value.Unit.Dot(b.Metadata!.Direction!.Value.Unit) < .999) return result;
        result.Add(a); result.Add(b);
        return result;
    }

    private static bool MatchesEmission(Vec3 actual, Vec3 expected) => actual.IsFinite &&
        Math.Abs(actual.X - expected.X) <= expected.X * .01 &&
        Math.Abs(actual.Y - expected.Y) <= expected.Y * .01 &&
        Math.Abs(actual.Z - expected.Z) <= expected.Z * .01;
}
