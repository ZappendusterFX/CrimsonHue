namespace CrimsonHue.Core;

public sealed record OpenRgbPlacement(string DeviceKey, string Name, Vec3 Position, bool Enabled = true);
public sealed record OpenRgbTarget(OpenRgbDevice Device, Vec3 Position);

// Only hashed hardware identity is persisted. SDK IDs/indexes are transient.
public sealed record OpenRgbLayout(bool Enabled = true, int Port = 6742, double Brightness = .25,
    IReadOnlyList<OpenRgbPlacement>? Devices = null, double SourceRadiusDegrees = 90, double BackgroundAmount = .25)
{
    public void Validate()
    {
        if (Port is < 1 or > 65535 || !double.IsFinite(Brightness) || Brightness is < 0 or > 1 ||
            !double.IsFinite(SourceRadiusDegrees) || SourceRadiusDegrees is < 1 or > 180 ||
            !double.IsFinite(BackgroundAmount) || BackgroundAmount is < 0 or > 1 ||
            Devices?.Count > OpenRgbProtocol.MaximumDevices)
            throw new CrimsonHueException("Invalid OpenRGB connection or light response settings.");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var d in Devices ?? [])
            if (d is null || d.DeviceKey is null || d.DeviceKey.Length != 64 || !d.DeviceKey.All(Uri.IsHexDigit) ||
                !keys.Add(d.DeviceKey) || d.Name is null || d.Name.Length > 200 || !d.Position.IsFinite ||
                Math.Max(Math.Abs(d.Position.X), Math.Max(Math.Abs(d.Position.Y), Math.Abs(d.Position.Z))) > 1)
                throw new CrimsonHueException("Invalid OpenRGB device placement.");
    }

    // Desk devices need a wider angular footprint than the Hue point/washer
    // profile. Neutral background fill is independently balanced for RGB LEDs.
    // Keep source RGB and shared smoothing/lantern handling unchanged.
    public MappingSettings Mapping(MappingSettings shared)
    {
        Validate();
        return shared with { Brightness = Math.Min(Brightness, shared.Brightness),
            SourceDiscRadiusDegrees = SourceRadiusDegrees,
            AmbientOutput = shared.AmbientOutput * BackgroundAmount,
            AmbientFloor = shared.AmbientFloor * BackgroundAmount };
    }

    public static bool Eligible(OpenRgbDevice d) => !d.HasZoneModes && d.DirectMode >= 0 &&
        !d.Modes[d.ActiveMode].AutomaticSave && d.PreviewLeds().Length > 0;

    public OpenRgbLayout Discover(IReadOnlyList<OpenRgbDevice> inventory)
    {
        Validate();
        var entries = (Devices ?? []).ToList();
        foreach (var d in inventory)
            if (!entries.Any(e => e.DeviceKey == d.Identity))
                entries.Add(new(d.Identity, d.Name, new(0, 1, 0), Eligible(d)));
        var next = this with { Devices = entries.ToArray() }; next.Validate(); return next;
    }

    public IReadOnlyList<OpenRgbTarget> Targets(IReadOnlyList<OpenRgbDevice> inventory)
    {
        Validate();
        return inventory.Where(Eligible).Where(d => Devices == null || Devices.Any(p => p.DeviceKey == d.Identity && p.Enabled))
            .OrderBy(d => d.Identity, StringComparer.Ordinal)
            .Select(d => new OpenRgbTarget(d, Devices?.First(p => p.DeviceKey == d.Identity).Position ?? new(0, 1, 0))).ToArray();
    }

    public static EntertainmentArea Area(IReadOnlyList<OpenRgbTarget> targets) => new("openrgb-room", "OpenRGB room", false,
        targets.Select((t, index) => new EntertainmentChannel((byte)index, t.Position, [], t.Device.Name)).ToArray());
}
