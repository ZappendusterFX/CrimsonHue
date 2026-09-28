namespace CrimsonHue.Core;

public sealed class LightMapper
{
    private readonly Dictionary<byte, Vec3> previous = [];
    private double ambientLevel;
    public void Reset() { previous.Clear(); ambientLevel = 0; }

    public IReadOnlyList<ChannelColor> Map(TelemetryFrame frame, EntertainmentArea area, MappingSettings settings, double deltaSeconds,
        AmbientFrame? ambient = null)
    {
        Validate(settings);
        // CDT's working RGB is not calibrated to local-light/display RGB. Use its
        // camera-local estimate only as a relative brightness control. Its tint
        // is explicitly user-selected, not a conversion of AP1-like sky RGB.
        var workingLevel = Math.Max(0, (ambient?.WorkingLevel ?? 0) - settings.AmbientCutoff);
        var targetAmbient = 1 - Math.Exp(-workingLevel * settings.AmbientSensitivity / settings.AmbientReferenceLevel);
        var ambientAlpha = settings.AmbientSmoothingMs == 0 ? 1 :
            1 - Math.Exp(-Math.Clamp(deltaSeconds, 0, 1) * 1000 / settings.AmbientSmoothingMs);
        // Unavailable data cannot sustain output. Valid transitions may smooth
        // for the displayed time constant; a zero time constant is immediate.
        ambientLevel = ambient is null || settings.AmbientSensitivity == 0 ? 0 :
            ambientLevel + (targetAmbient - ambientLevel) * ambientAlpha;
        var ambientFill = ambient is null || settings.AmbientSensitivity == 0 ? 0 :
            settings.AmbientFloor + settings.AmbientOutput * ambientLevel;
        var ambientColor = FromHue(settings.AmbientTintHue, settings.AmbientTintSaturation, 1) * ambientFill;
        var localContrast = 1 - (1 - settings.DaylightLocalStrength) * ambientLevel;
        // Bound each source's HDR intensity before spatial attenuation. Applying
        // tone mapping afterwards can turn a tiny distant HDR contribution back
        // into a saturated output, effectively undoing the fade.
        var contributions = new List<(Vec3 Direction, Vec3 Rgb)>();
        foreach (var light in frame.Lights)
        {
            var fade = DistanceWeight((light.Position - frame.Player).Length, settings);
            if (fade <= 0) continue;
            var adjusted = AdjustColor(light.Rgb, settings);
            var peak = Math.Max(adjusted.X, Math.Max(adjusted.Y, adjusted.Z));
            if (peak <= 0) continue;
            var local = SourceDirection(light.Position, frame, settings);
            var bounded = adjusted * ((1 - Math.Exp(-peak * settings.Gain)) / peak);
            contributions.Add((local, bounded * fade));
        }
        var directions = area.Channels.Select(channel => channel.Position.Unit).ToArray();
        var sums = new Vec3[area.Channels.Count];
        foreach (var contribution in contributions)
        {
            var angular = new double[directions.Length];
            var coverage = new double[directions.Length];
            var strongest = 0.0;
            for (var i = 0; i < directions.Length; i++)
            {
                // Hue axes: x right, y towards screen, z up. Normalized room
                // coordinates, not metres. Reject the opposite side before
                // normalization: no available channel may redirect a source
                // across an enabled boundary. A position exactly on an axis
                // belongs to both adjoining halves; height is still weighted.
                if ((settings.SeparateLeftRight && OppositeSides(directions[i].X, contribution.Direction.X)) ||
                    (settings.SeparateFrontRear && OppositeSides(directions[i].Y, contribution.Direction.Y))) continue;
                var dot = Math.Clamp(directions[i].Dot(contribution.Direction), -1, 1);
                var centered = directions[i].Length < 0.01;
                coverage[i] = centered ? 1 : SourceDiscWeight(dot, settings);
                if (coverage[i] == 0) continue;
                angular[i] = centered ? 1 : Math.Exp(settings.Spread * (dot - 1));
                strongest = Math.Max(strongest, angular[i]);
            }
            if (strongest == 0) continue;
            for (var i = 0; i < sums.Length; i++)
                sums[i] += contribution.Rgb * coverage[i] * (angular[i] * (1 - settings.DirectionNormalization) +
                    angular[i] / strongest * settings.DirectionNormalization);
        }
        var result = new List<ChannelColor>(area.Channels.Count);
        for (var i = 0; i < area.Channels.Count; i++)
        {
            var channel = area.Channels[i];
            var sum = sums[i] * settings.LocalStrength;
            var peak = Math.Max(sum.X, Math.Max(sum.Y, sum.Z));
            // Normalize only overflow from overlapping sources; never boost the
            // attenuated sum. Mixing and ratio preservation stay in linear RGB.
            var local = sum / Math.Max(1, peak);
            var combined = ambientColor + local * localContrast;
            var combinedPeak = Math.Max(combined.X, Math.Max(combined.Y, combined.Z));
            var linear = combined / Math.Max(1, combinedPeak);
            var rgb = new Vec3(Math.Pow(Encode(linear.X), 1 / settings.OutputGamma),
                Math.Pow(Encode(linear.Y), 1 / settings.OutputGamma), Math.Pow(Encode(linear.Z), 1 / settings.OutputGamma)) *
                (settings.Brightness * channel.Brightness);
            // A channel-level EMA can retain RGB after a source becomes hidden
            // or crosses a strict boundary. Replace colors immediately in those
            // modes; Ambient still has its own exposed transition control.
            var alpha = frame.ConfirmedVisibleOnly || settings.SeparateLeftRight || settings.SeparateFrontRear || settings.SmoothingMs <= 0 ? 1 :
                1 - Math.Exp(-Math.Clamp(deltaSeconds, 0, 1) * 1000 / settings.SmoothingMs);
            if (alpha < 1)
            {
                var old = previous.GetValueOrDefault(channel.Id);
                rgb = old + (rgb - old) * alpha;
            }
            previous[channel.Id] = rgb;
            result.Add(new(channel.Id, rgb));
        }
        return result;
    }
    /// <summary>0 is the raw player position; 1 is the paired camera position.
    /// Intermediate values interpolate all three coordinates without offsets.</summary>
    public static Vec3 DirectionOrigin(TelemetryFrame frame, MappingSettings settings) =>
        frame.Player + (frame.Camera.Position - frame.Player) * settings.DirectionOriginBlend;
    /// <summary>Room direction shared by mapping and preview. Position follows
    /// the selected blend; viewing axes always come from the paired camera.</summary>
    public static Vec3 SourceDirection(Vec3 position, TelemetryFrame frame, MappingSettings settings)
    {
        var d = position - DirectionOrigin(frame, settings);
        var local = new Vec3(d.Dot(frame.Camera.Right), d.Dot(frame.Camera.Forward), d.Dot(frame.Camera.Up)).Unit;
        var yaw = settings.CameraYawOffset * Math.PI / 180;
        return new(local.X * Math.Cos(yaw) - local.Y * Math.Sin(yaw),
            local.X * Math.Sin(yaw) + local.Y * Math.Cos(yaw), local.Z);
    }
    private static bool OppositeSides(double a, double b) => (a < 0 && b > 0) || (a > 0 && b < 0);
    private static double SourceDiscWeight(double dot, MappingSettings settings)
    {
        // Intersect a channel ray with the 2D plane normal to the source's
        // camera-relative bearing at unit depth. Its radial coordinate is
        // tan(angle). This bounded circle is an angular footprint, not a
        // sphere in the game world, and also works for off-screen sources.
        if (dot <= 0) return 0;
        var radius = Math.Tan(settings.SourceDiscRadiusDegrees * Math.PI / 180);
        var radial = Math.Sqrt(Math.Max(0, 1 - dot * dot)) / dot / radius;
        if (radial >= 1) return 0;
        if (settings.SourceDiscSoftness == 0 || radial <= 1 - settings.SourceDiscSoftness) return 1;
        var t = (radial - (1 - settings.SourceDiscSoftness)) / settings.SourceDiscSoftness;
        return 1 - t * t * (3 - 2 * t);
    }
    /// <summary>Player-relative fade, identical when approaching or leaving.
    /// Call with validated settings. Distances are game units, not room metres.</summary>
    public static double DistanceWeight(double distance, MappingSettings settings)
    {
        if (!double.IsFinite(distance) || distance < 0 || distance >= settings.FadeEnd) return 0;
        if (distance <= settings.FadeStart) return 1;
        var t = (distance - settings.FadeStart) / (settings.FadeEnd - settings.FadeStart);
        // Smooth at both boundaries, with a subdued tail after sRGB encoding.
        var smooth = Math.Clamp(1 - t * t * (3 - 2 * t), 0, 1);
        return Math.Pow(smooth, settings.FadeExponent);
    }
    public static void Validate(MappingSettings s)
    {
        if (!InRange(s.Gain, 0, 10) || !InRange(s.Brightness, 0, 1) ||
            !InRange(s.Radius, 1, 1000) || !InRange(s.Spread, 0, 16) || !InRange(s.SmoothingMs, 0, 1000) ||
            !double.IsFinite(s.FadeStart) || s.FadeStart < 0 || s.FadeStart >= s.FadeEnd ||
            !InRange(s.AmbientSensitivity, 0, 10) || !InRange(s.AmbientOutput, 0, 1) ||
            !InRange(s.AmbientFloor, 0, 1) || !InRange(s.AmbientCutoff, 0, 100) ||
            !InRange(s.DaylightLocalStrength, 0, 2) || !InRange(s.LocalStrength, 0, 4) ||
            !InRange(s.AmbientReferenceLevel, 0.01, 100) || !InRange(s.AmbientSmoothingMs, 0, 5000) ||
            !InRange(s.HueShiftDegrees, -180, 180) || !InRange(s.Saturation, 0, 2) ||
            !InRange(s.RedGain, 0, 4) || !InRange(s.GreenGain, 0, 4) || !InRange(s.BlueGain, 0, 4) ||
            !InRange(s.OutputGamma, 0.1, 4) || !InRange(s.DirectionNormalization, 0, 1) ||
            !InRange(s.CameraYawOffset, -180, 180) || !InRange(s.FadeExponent, 0.1, 8) ||
            !InRange(s.AmbientTintHue, 0, 360) || !InRange(s.AmbientTintSaturation, 0, 1) ||
            !InRange(s.SourceDiscRadiusDegrees, 1, 89) || !InRange(s.SourceDiscSoftness, 0, 1) ||
            !InRange(s.DirectionOriginBlend, 0, 1))
            throw new CrimsonHueException("Invalid lighting settings.");
    }
    private static bool InRange(double value, double minimum, double maximum) =>
        double.IsFinite(value) && value >= minimum && value <= maximum;

    // General HSV adjustment of linear RGB, preserving peak and black. All
    // colors follow this same rule. Neutral controls preserve the source ratios.
    private static Vec3 AdjustColor(Vec3 rgb, MappingSettings settings)
    {
        rgb = new(rgb.X * settings.RedGain, rgb.Y * settings.GreenGain, rgb.Z * settings.BlueGain);
        if (settings.HueShiftDegrees == 0 && settings.Saturation == 1) return rgb;
        var peak = Math.Max(rgb.X, Math.Max(rgb.Y, rgb.Z));
        var chroma = peak - Math.Min(rgb.X, Math.Min(rgb.Y, rgb.Z));
        if (peak <= 0 || chroma <= 0) return rgb;
        var hue = 60 * (peak == rgb.X ? (rgb.Y - rgb.Z) / chroma :
            peak == rgb.Y ? (rgb.Z - rgb.X) / chroma + 2 : (rgb.X - rgb.Y) / chroma + 4);
        return FromHue(hue + settings.HueShiftDegrees, Math.Clamp(chroma / peak * settings.Saturation, 0, 1), peak);
    }
    private static Vec3 FromHue(double degrees, double saturation, double peak)
    {
        var sector = ((degrees % 360 + 360) % 360) / 60;
        var chroma = peak * saturation;
        var secondary = chroma * (1 - Math.Abs(sector % 2 - 1));
        var color = sector < 1 ? new Vec3(chroma, secondary, 0) : sector < 2 ? new Vec3(secondary, chroma, 0) :
            sector < 3 ? new Vec3(0, chroma, secondary) : sector < 4 ? new Vec3(0, secondary, chroma) :
            sector < 5 ? new Vec3(secondary, 0, chroma) : new Vec3(chroma, 0, secondary);
        var minimum = peak - chroma;
        return color + new Vec3(minimum, minimum, minimum);
    }
    private static double Encode(double linear) => linear <= 0.0031308 ? 12.92 * linear : 1.055 * Math.Pow(linear, 1 / 2.4) - 0.055;
}
