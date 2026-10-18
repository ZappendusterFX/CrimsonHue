using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrimsonHue.Core;

public readonly record struct Vec3(double X, double Y, double Z)
{
    public override string ToString() => FormattableString.Invariant($"({X:R}, {Y:R}, {Z:R})");
    [JsonIgnore] public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z);
    [JsonIgnore] public double Length => Math.Sqrt(Dot(this));
    public double Dot(Vec3 b) => X * b.X + Y * b.Y + Z * b.Z;
    [JsonIgnore] public Vec3 Unit => Length > 1e-9 ? this / Length : default;
    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator *(Vec3 a, double b) => new(a.X * b, a.Y * b, a.Z * b);
    public static Vec3 operator /(Vec3 a, double b) => a * (1 / b);
    public static Vec3 Read(JsonElement value)
    {
        var result = new Vec3(value.GetProperty("x").GetDouble(), value.GetProperty("y").GetDouble(), value.GetProperty("z").GetDouble());
        if (!result.IsFinite || Math.Max(Math.Abs(result.X), Math.Max(Math.Abs(result.Y), Math.Abs(result.Z))) > 1e12)
            throw new FormatException("Invalid vector.");
        return result;
    }
}

public sealed record CameraPose(Vec3 Position, Vec3 Right, Vec3 Up, Vec3 Forward);
public sealed record LightSourceMetadata(string? Type, string? Kind, Vec3? Direction, double? ConeHalfAngleDegrees);
public sealed record LightContribution(Vec3 Position, Vec3 Rgb, double? VisibilityAgeMs = null, LightSourceMetadata? Metadata = null);
public sealed record TelemetryFrame(long Sequence, long CaptureSequence, DateTimeOffset CapturedAt,
    DateTimeOffset LightCapturedAt, double ProducerAgeMs, Vec3 Player, CameraPose Camera, IReadOnlyList<LightContribution> Lights,
    int SourceCount = 0, string Feed = "rendered", bool ConfirmedVisibleOnly = false,
    string? GameBuild = null, Vec3? PlayerForward = null);
public sealed record AmbientFrame(long CaptureSequence, DateTimeOffset CapturedAt, double SkyAgeMs,
    double VisibilityAgeMs, double WorkingLevel, Vec3 WorkingRgb = default, Vec3 SkyRgb = default,
    double SkyVisibility = 0);
public sealed record ChannelMember(string ServiceId, int SegmentIndex);
public sealed record EntertainmentChannel(byte Id, Vec3 Position, IReadOnlyList<ChannelMember> Members, string Label, double Brightness = 1);
public sealed record EntertainmentServiceLocation(string ServiceId, IReadOnlyList<Vec3> Positions, double Brightness = 1);
public sealed record EntertainmentArea(string Id, string Name, bool Active, IReadOnlyList<EntertainmentChannel> Channels,
    IReadOnlyList<EntertainmentServiceLocation>? ServiceLocations = null)
{
    public int LampCount => Channels.SelectMany(c => c.Members).Select(m => m.ServiceId).Distinct().Count();
    public string Description => $"{Name}  ·  {LampCount} lights / {Channels.Count} channels";
    public override string ToString() => Description;

    // Service placement can change while the bridge's channel positions stay
    // fixed (observed with Play wall washers). Compare both, keeping their
    // distinct meanings; never infer segment coordinates from two endpoints.
    public bool HasSameLayout(EntertainmentArea other)
    {
        if (Id != other.Id || Channels.Count != other.Channels.Count) return false;
        var leftChannels = Channels.OrderBy(c => c.Id).ToArray();
        var rightChannels = other.Channels.OrderBy(c => c.Id).ToArray();
        for (var i = 0; i < leftChannels.Length; i++)
        {
            var left = leftChannels[i]; var right = rightChannels[i];
            if (left.Id != right.Id || left.Position != right.Position || left.Brightness != right.Brightness ||
                !left.Members.OrderBy(m => m.ServiceId, StringComparer.Ordinal).ThenBy(m => m.SegmentIndex)
                    .SequenceEqual(right.Members.OrderBy(m => m.ServiceId, StringComparer.Ordinal).ThenBy(m => m.SegmentIndex)))
                return false;
        }
        var leftLocations = (ServiceLocations ?? []).OrderBy(l => l.ServiceId, StringComparer.Ordinal).ToArray();
        var rightLocations = (other.ServiceLocations ?? []).OrderBy(l => l.ServiceId, StringComparer.Ordinal).ToArray();
        if (leftLocations.Length != rightLocations.Length) return false;
        for (var i = 0; i < leftLocations.Length; i++)
        {
            var left = leftLocations[i]; var right = rightLocations[i];
            if (left.ServiceId != right.ServiceId || left.Brightness != right.Brightness ||
                !left.Positions.SequenceEqual(right.Positions)) return false;
        }
        return true;
    }
}
public sealed record BridgeCredentials(string Address, string ApplicationKey, string ClientKey, string? CertificateSha256);
// Historical omitted-field defaults for saved settings/presets. The current
// product/profile/reset baseline is LightingProfile.Defaults.
public sealed record MappingSettings(double Gain = 1.5, double Brightness = 0.85, double Radius = 35, double Spread = 2,
    double SmoothingMs = 100, double FadeStart = 0, double AmbientSensitivity = 1,
    double AmbientOutput = 0.4, double AmbientFloor = 0, double AmbientCutoff = 0,
    double DaylightLocalStrength = 1, double LocalStrength = 1, double AmbientReferenceLevel = 4,
    double AmbientSmoothingMs = 400, double HueShiftDegrees = 0, double Saturation = 1,
    double RedGain = 1, double GreenGain = 1, double BlueGain = 1, double OutputGamma = 1,
    double DirectionNormalization = 1, double CameraYawOffset = 0, double FadeExponent = 2,
    double AmbientTintHue = 0, double AmbientTintSaturation = 0,
    bool SeparateLeftRight = true, bool SeparateFrontRear = true,
    double SourceDiscRadiusDegrees = 60, double SourceDiscSoftness = 0.25,
    double DirectionOriginBlend = 0.5, double PointLightStrength = 1,
    double SpotLightStrength = 1, double PlayerLanternStrength = 1)
{
    // Retain Radius in stored settings and the constructor for 0.1.0 compatibility.
    [JsonIgnore] public double FadeEnd => Radius;
    // SeparateLeftRight/SeparateFrontRear remain in saved settings and version-1
    // presets for compatibility only. Spherical mapping has no axis cut switches.
}
public sealed record ChannelColor(byte Id, Vec3 Rgb);

public sealed class CrimsonHueException(string message) : Exception(message);
