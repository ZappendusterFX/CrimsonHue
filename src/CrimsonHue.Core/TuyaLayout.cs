using System.Net;
using System.Net.Sockets;

namespace CrimsonHue.Core;

// Credentials belong to the protected user store, never to the serializable layout.
public sealed record TuyaLamp(string Id, string Name, string Host, string DeviceId, Vec3 Position,
    bool Enabled = true, int Port = 6668, string Protocol = "3.5", int PowerDps = 20,
    int ModeDps = 21, int ColorDps = 24, int WhiteBrightnessDps = 22,
    int ColorTemperatureDps = 23, int SceneDps = 25)
{
    public void Validate()
    {
        if (!Identifier(Id, 64) || !Identifier(DeviceId, 128) || string.IsNullOrWhiteSpace(Name) ||
            Name.Length > 200 || Name.Any(char.IsControl) || string.IsNullOrWhiteSpace(Host) ||
            Host.Length > 253 || Host != Host.Trim() || Port is < 1 or > 65535 || Protocol != "3.5" ||
            !Position.IsFinite || Math.Max(Math.Abs(Position.X), Math.Max(Math.Abs(Position.Y), Math.Abs(Position.Z))) > 1)
            throw new CrimsonHueException("Invalid Tuya lamp identity, connection or placement.");
        var hostKind = Uri.CheckHostName(Host);
        if (hostKind is not (UriHostNameType.Dns or UriHostNameType.IPv4) ||
            (IPAddress.TryParse(Host, out var address) && address.AddressFamily != AddressFamily.InterNetwork))
            throw new CrimsonHueException("Tuya lamps require an IPv4 address or hostname.");
        var dps = new[] { PowerDps, ModeDps, ColorDps, WhiteBrightnessDps, ColorTemperatureDps, SceneDps };
        if (dps.Any(d => d is < 1 or > 255) || dps.Distinct().Count() != dps.Length)
            throw new CrimsonHueException("Tuya DPS values must be distinct numbers from 1 to 255.");
    }

    private static bool Identifier(string? value, int maximum) => !string.IsNullOrEmpty(value) &&
        value.Length <= maximum && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.');

    internal bool SameConnection(TuyaLamp other) => Id == other.Id &&
        string.Equals(Host, other.Host, StringComparison.OrdinalIgnoreCase) && DeviceId == other.DeviceId &&
        Port == other.Port && Protocol == other.Protocol && PowerDps == other.PowerDps && ModeDps == other.ModeDps &&
        ColorDps == other.ColorDps && WhiteBrightnessDps == other.WhiteBrightnessDps &&
        ColorTemperatureDps == other.ColorTemperatureDps && SceneDps == other.SceneDps;
}

public sealed record TuyaLayout(bool Enabled = false, double Brightness = .25,
    IReadOnlyList<TuyaLamp>? Devices = null, double SourceRadiusDegrees = 90, double BackgroundAmount = .25)
{
    public const int MaximumDevices = 32;

    public void Validate()
    {
        if (!double.IsFinite(Brightness) || Brightness is < 0 or > 1 ||
            !double.IsFinite(SourceRadiusDegrees) || SourceRadiusDegrees is < 1 or > 180 ||
            !double.IsFinite(BackgroundAmount) || BackgroundAmount is < 0 or > 1 || Devices?.Count > MaximumDevices)
            throw new CrimsonHueException("Invalid Tuya output or light response settings.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var physicalIds = new HashSet<string>(StringComparer.Ordinal);
        var endpoints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var lamp in Devices ?? [])
        {
            if (lamp == null) throw new CrimsonHueException("Invalid Tuya lamp.");
            lamp.Validate();
            if (!ids.Add(lamp.Id) || !physicalIds.Add(lamp.DeviceId) || !endpoints.Add($"{lamp.Host}:{lamp.Port}"))
                throw new CrimsonHueException("Duplicate Tuya lamp identity or endpoint.");
        }
    }

    public MappingSettings Mapping(MappingSettings shared)
    {
        Validate();
        return shared with { Brightness = Math.Min(Brightness, shared.Brightness),
            SourceDiscRadiusDegrees = SourceRadiusDegrees,
            AmbientOutput = shared.AmbientOutput * BackgroundAmount,
            AmbientFloor = shared.AmbientFloor * BackgroundAmount };
    }

    public IReadOnlyList<TuyaLamp> Targets()
    {
        Validate();
        return (Devices ?? []).Where(d => d.Enabled).OrderBy(d => d.Id, StringComparer.Ordinal).ToArray();
    }

    public static EntertainmentArea Area(IReadOnlyList<TuyaLamp> targets) => new("tuya-room", "Tuya room", false,
        targets.Select((lamp, index) => new EntertainmentChannel((byte)index, lamp.Position, [], lamp.Name)).ToArray());
}
