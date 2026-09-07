namespace CrimsonHue.Core;

public sealed class LightMapper
{
    private readonly Dictionary<byte, Vec3> previous = [];
    public void Reset() => previous.Clear();

    public IReadOnlyList<ChannelColor> Map(TelemetryFrame frame, EntertainmentArea area, MappingSettings settings, double deltaSeconds)
    {
        Validate(settings);
        var result = new List<ChannelColor>(area.Channels.Count);
        foreach (var channel in area.Channels)
        {
            // Hue axes: x right, y towards screen, z up. Normalized room coordinates, not metres.
            var direction = channel.Position.Unit;
            Vec3 sum = default;
            foreach (var light in frame.Lights)
            {
                var playerDistance = (light.Position - frame.Player).Length;
                if (playerDistance >= settings.Radius) continue;
                var d = light.Position - frame.Camera.Position;
                var local = new Vec3(d.Dot(frame.Camera.Right), d.Dot(frame.Camera.Forward), d.Dot(frame.Camera.Up)).Unit;
                var angular = direction.Length < 0.01 ? 1 : Math.Exp(settings.Spread * (Math.Clamp(direction.Dot(local), -1, 1) - 1));
                // A consumer estimate: finite soft falloff, no invented source ranges/visibility.
                var falloff = Math.Pow(1 - playerDistance / settings.Radius, 2);
                sum += light.Rgb * (angular * falloff);
            }
            var peak = Math.Max(sum.X, Math.Max(sum.Y, sum.Z));
            // Compress HDR with a common scale, preserving RGB ratios before sRGB encoding.
            var linear = peak > 0 ? sum * ((1 - Math.Exp(-peak * settings.Gain)) / peak) : default;
            var rgb = new Vec3(Encode(linear.X), Encode(linear.Y), Encode(linear.Z)) * (settings.Brightness * channel.Brightness);
            var alpha = settings.SmoothingMs <= 0 ? 1 : 1 - Math.Exp(-Math.Clamp(deltaSeconds, 0, 1) * 1000 / settings.SmoothingMs);
            var old = previous.GetValueOrDefault(channel.Id);
            rgb = old + (rgb - old) * alpha;
            previous[channel.Id] = rgb;
            result.Add(new(channel.Id, rgb));
        }
        return result;
    }
    public static void Validate(MappingSettings s)
    {
        if (!double.IsFinite(s.Gain) || s.Gain is < 0.05 or > 10 || !double.IsFinite(s.Brightness) || s.Brightness is < 0 or > 1 ||
            !double.IsFinite(s.Radius) || s.Radius is < 1 or > 1000 || !double.IsFinite(s.Spread) || s.Spread is < 0.1 or > 8 ||
            !double.IsFinite(s.SmoothingMs) || s.SmoothingMs is < 0 or > 1000)
            throw new CrimsonHueException("Invalid lighting settings.");
    }
    private static double Encode(double linear) => linear <= 0.0031308 ? 12.92 * linear : 1.055 * Math.Pow(linear, 1 / 2.4) - 0.055;
}
