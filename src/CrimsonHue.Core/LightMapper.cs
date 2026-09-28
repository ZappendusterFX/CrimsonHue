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
        // camera-local estimate only as a relative brightness control, with a
        // neutral room baseline; never add its channels to renderer RGB.
        var targetAmbient = ambient is null ? 0 : 1 - Math.Exp(-Math.Max(0, ambient.WorkingLevel) * settings.AmbientSensitivity / 4);
        var ambientAlpha = 1 - Math.Exp(-Math.Clamp(deltaSeconds, 0, 1) / 0.4);
        ambientLevel = settings.AmbientSensitivity == 0 ? 0 : ambientLevel + (targetAmbient - ambientLevel) * ambientAlpha;
        var localContrast = Math.Max(0.05, Math.Pow(1 - ambientLevel, 1.5));
        // Bound each source's HDR intensity before spatial attenuation. Applying
        // tone mapping afterwards can turn a tiny distant HDR contribution back
        // into a saturated output, effectively undoing the fade.
        var contributions = new List<(Vec3 Direction, Vec3 Rgb)>();
        foreach (var light in frame.Lights)
        {
            var fade = DistanceWeight((light.Position - frame.Player).Length, settings);
            if (fade <= 0) continue;
            var peak = Math.Max(light.Rgb.X, Math.Max(light.Rgb.Y, light.Rgb.Z));
            if (peak <= 0) continue;
            var d = light.Position - frame.Camera.Position;
            var local = new Vec3(d.Dot(frame.Camera.Right), d.Dot(frame.Camera.Forward), d.Dot(frame.Camera.Up)).Unit;
            var bounded = light.Rgb * ((1 - Math.Exp(-peak * settings.Gain)) / peak);
            contributions.Add((local, bounded * fade));
        }
        var result = new List<ChannelColor>(area.Channels.Count);
        foreach (var channel in area.Channels)
        {
            // Hue axes: x right, y towards screen, z up. Normalized room coordinates, not metres.
            var direction = channel.Position.Unit;
            Vec3 sum = default;
            foreach (var contribution in contributions)
            {
                var angular = direction.Length < 0.01 ? 1 : Math.Exp(settings.Spread * (Math.Clamp(direction.Dot(contribution.Direction), -1, 1) - 1));
                sum += contribution.Rgb * angular;
            }
            var peak = Math.Max(sum.X, Math.Max(sum.Y, sum.Z));
            // Normalize only overflow from overlapping sources; never boost the
            // attenuated sum. Mixing and ratio preservation stay in linear RGB.
            var local = sum / Math.Max(1, peak);
            var combined = new Vec3(ambientLevel * 0.8, ambientLevel * 0.8, ambientLevel * 0.8) + local * localContrast;
            var combinedPeak = Math.Max(combined.X, Math.Max(combined.Y, combined.Z));
            var linear = combined / Math.Max(1, combinedPeak);
            var rgb = new Vec3(Encode(linear.X), Encode(linear.Y), Encode(linear.Z)) * (settings.Brightness * channel.Brightness);
            // A channel-level EMA can retain RGB from a source that just became
            // blocked/unknown. Without stable source identities, the clear-only
            // feed must replace channel colors immediately.
            var alpha = frame.ConfirmedVisibleOnly || settings.SmoothingMs <= 0 ? 1 :
                1 - Math.Exp(-Math.Clamp(deltaSeconds, 0, 1) * 1000 / settings.SmoothingMs);
            var old = previous.GetValueOrDefault(channel.Id);
            rgb = old + (rgb - old) * alpha;
            previous[channel.Id] = rgb;
            result.Add(new(channel.Id, rgb));
        }
        return result;
    }
    /// <summary>Player-relative fade, identical when approaching or leaving.
    /// Call with validated settings. Distances are game units, not room metres.</summary>
    public static double DistanceWeight(double distance, MappingSettings settings)
    {
        if (!double.IsFinite(distance) || distance < 0 || distance >= settings.FadeEnd) return 0;
        if (distance <= settings.FadeStart) return 1;
        var t = (distance - settings.FadeStart) / (settings.FadeEnd - settings.FadeStart);
        // Smooth at both boundaries, with a subdued tail after sRGB encoding.
        var smooth = 1 - t * t * (3 - 2 * t);
        return smooth * smooth;
    }
    public static void Validate(MappingSettings s)
    {
        if (!double.IsFinite(s.Gain) || s.Gain is < 0.05 or > 10 || !double.IsFinite(s.Brightness) || s.Brightness is < 0 or > 1 ||
            !double.IsFinite(s.Radius) || s.Radius is < 1 or > 1000 || !double.IsFinite(s.Spread) || s.Spread is < 0.1 or > 8 ||
            !double.IsFinite(s.SmoothingMs) || s.SmoothingMs is < 0 or > 1000 ||
            !double.IsFinite(s.FadeStart) || s.FadeStart < 0 || s.FadeStart >= s.FadeEnd ||
            !double.IsFinite(s.AmbientSensitivity) || s.AmbientSensitivity is < 0 or > 3)
            throw new CrimsonHueException("Invalid lighting settings.");
    }
    private static double Encode(double linear) => linear <= 0.0031308 ? 12.92 * linear : 1.055 * Math.Pow(linear, 1 / 2.4) - 0.055;
}
