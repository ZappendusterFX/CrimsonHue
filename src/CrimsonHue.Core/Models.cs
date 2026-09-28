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
public sealed record LightContribution(Vec3 Position, Vec3 Rgb, double? VisibilityAgeMs = null);
public sealed record TelemetryFrame(long Sequence, long CaptureSequence, DateTimeOffset CapturedAt,
    DateTimeOffset LightCapturedAt, double ProducerAgeMs, Vec3 Player, CameraPose Camera, IReadOnlyList<LightContribution> Lights,
    int SourceCount = 0, string Feed = "rendered", bool ConfirmedVisibleOnly = false);
public sealed record AmbientFrame(long CaptureSequence, DateTimeOffset CapturedAt, double SkyAgeMs,
    double VisibilityAgeMs, double WorkingLevel);
public sealed record ChannelMember(string ServiceId, int SegmentIndex);
public sealed record EntertainmentChannel(byte Id, Vec3 Position, IReadOnlyList<ChannelMember> Members, string Label, double Brightness = 1);
public sealed record EntertainmentArea(string Id, string Name, bool Active, IReadOnlyList<EntertainmentChannel> Channels)
{
    public int LampCount => Channels.SelectMany(c => c.Members).Select(m => m.ServiceId).Distinct().Count();
    public string Description => $"{Name}  ·  {LampCount} lights / {Channels.Count} channels";
    public override string ToString() => Description;
}
public sealed record BridgeCredentials(string Address, string ApplicationKey, string ClientKey, string? CertificateSha256);
public sealed record MappingSettings(double Gain = 1.5, double Brightness = 0.85, double Radius = 35, double Spread = 2,
    double SmoothingMs = 100, double FadeStart = 0, double AmbientSensitivity = 1)
{
    // Retain Radius in stored settings and the constructor for 0.1.0 compatibility.
    [JsonIgnore] public double FadeEnd => Radius;
}
public sealed record ChannelColor(byte Id, Vec3 Rgb);

public sealed class CrimsonHueException(string message) : Exception(message);
