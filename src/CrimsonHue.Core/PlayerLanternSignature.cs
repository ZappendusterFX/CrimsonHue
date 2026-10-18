namespace CrimsonHue.Core;

/// <summary>Conservative, exact-build recognition of the equipped lantern pair.
/// This is not a durable engine identity or a general player-attachment classifier.</summary>
public static class PlayerLanternSignature
{
    public static IReadOnlySet<LightContribution> Find(TelemetryFrame frame)
    {
        var result = new HashSet<LightContribution>();
        if (frame.GameBuild != "25477059" || frame.Feed != "all-around" ||
            frame.PlayerForward is not { } forward || !forward.IsFinite || Math.Abs(forward.Length - 1) > .02 ||
            Math.Abs(forward.Y) > .05) return result;
        forward = new Vec3(forward.X, 0, forward.Z).Unit;
        var right = new Vec3(forward.Z, 0, -forward.X);
        bool Candidate(LightContribution light, double cone)
        {
            if (light.Metadata is not { Type: "standalone", Kind: "spot", Direction: { } direction,
                ConeHalfAngleDegrees: { } angle } || !direction.IsFinite || Math.Abs(direction.Length - 1) > .02 ||
                Math.Abs(angle - cone) > .02 || Math.Abs(direction.Y) > .05 || direction.Unit.Dot(forward) < .996) return false;
            var offset = light.Position - frame.Player;
            var side = offset.Dot(right); var ahead = offset.Dot(forward);
            return side is >= .15 and <= .85 && ahead is >= 0 and <= .85 && offset.Y is >= .6 and <= 1.7;
        }
        var wide = frame.Lights.Where(l => Candidate(l, 31.137793)).ToArray();
        var narrow = frame.Lights.Where(l => Candidate(l, 26.885372)).ToArray();
        // Missing/ambiguous pairs stay at the ordinary kind weight. Never identify by proximity,
        // color, source slot or a remembered world coordinate alone.
        if (wide.Length != 1 || narrow.Length != 1) return result;
        var a = wide[0]; var b = narrow[0];
        if ((a.Position - b.Position).Length > .03 || a.Rgb.Length <= 1e-9 ||
            (a.Rgb - b.Rgb * 2.5).Length > a.Rgb.Length * .01 ||
            a.Metadata!.Direction!.Value.Unit.Dot(b.Metadata!.Direction!.Value.Unit) < .999) return result;
        result.Add(a); result.Add(b);
        return result;
    }
}
