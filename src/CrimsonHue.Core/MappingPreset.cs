using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrimsonHue.Core;

[Flags]
public enum PresetGroups
{
    None = 0,
    Output = 1,
    Ambient = 2,
    Color = 4,
    Space = 8,
    All = Output | Ambient | Color | Space
}

public sealed record MappingPreset(string Format, int Version, string Name, MappingSettings Mapping)
{
    public const string FileFormat = "CrimsonHuePreset";
    public const int CurrentVersion = 1;
    public const int MaximumBytes = 64 * 1024;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
    private static readonly HashSet<string> RootKeys = ["format", "version", "name", "mapping"];
    private static readonly HashSet<string> MappingKeys = typeof(MappingSettings)
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is null)
        .Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name))
        .ToHashSet(StringComparer.Ordinal);

    public static string Export(string name, MappingSettings mapping)
    {
        LightMapper.Validate(mapping);
        name = ValidateName(name);
        var json = JsonSerializer.Serialize(new MappingPreset(FileFormat, CurrentVersion, name, mapping), Options) + "\n";
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaximumBytes)
            throw new CrimsonHueException("Preset is too large to export.");
        return json;
    }

    public static MappingPreset Import(byte[] bytes)
    {
        if (bytes.Length is 0 or > MaximumBytes) throw new CrimsonHueException("Preset file is empty or too large.");
        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });
            var root = document.RootElement;
            RequireKeys(root, RootKeys, "Preset file");
            var format = root.GetProperty("format");
            if (format.ValueKind != JsonValueKind.String || format.GetString() != FileFormat)
                throw new CrimsonHueException("This is not a CrimsonHue preset. Export only mixer values from the app.");
            var version = root.GetProperty("version");
            if (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number != CurrentVersion)
                throw new CrimsonHueException("Unsupported preset format version.");
            RequireKeys(root.GetProperty("mapping"), MappingKeys, "Preset mixer values");
            var preset = JsonSerializer.Deserialize<MappingPreset>(bytes, Options)
                ?? throw new CrimsonHueException("Preset file is empty.");
            ValidateName(preset.Name);
            if (preset.Mapping is null) throw new CrimsonHueException("Preset has no mixer values.");
            LightMapper.Validate(preset.Mapping);
            return preset;
        }
        catch (JsonException)
        {
            throw new CrimsonHueException("Preset JSON is invalid or contains a value of the wrong type.");
        }
    }

    public MappingSettings ApplyTo(MappingSettings current, PresetGroups groups)
    {
        if (groups == PresetGroups.None || (groups & ~PresetGroups.All) != 0)
            throw new CrimsonHueException("Choose at least one valid preset section.");
        LightMapper.Validate(current);
        LightMapper.Validate(Mapping);
        var next = current;
        if (groups.HasFlag(PresetGroups.Output)) next = next with
        {
            Brightness = Mapping.Brightness, Gain = Mapping.Gain, LocalStrength = Mapping.LocalStrength,
            OutputGamma = Mapping.OutputGamma, SmoothingMs = Mapping.SmoothingMs
        };
        if (groups.HasFlag(PresetGroups.Ambient)) next = next with
        {
            AmbientSensitivity = Mapping.AmbientSensitivity, AmbientReferenceLevel = Mapping.AmbientReferenceLevel,
            AmbientOutput = Mapping.AmbientOutput, AmbientFloor = Mapping.AmbientFloor,
            AmbientCutoff = Mapping.AmbientCutoff, DaylightLocalStrength = Mapping.DaylightLocalStrength,
            AmbientSmoothingMs = Mapping.AmbientSmoothingMs
        };
        if (groups.HasFlag(PresetGroups.Color)) next = next with
        {
            HueShiftDegrees = Mapping.HueShiftDegrees, Saturation = Mapping.Saturation,
            RedGain = Mapping.RedGain, GreenGain = Mapping.GreenGain, BlueGain = Mapping.BlueGain,
            AmbientTintHue = Mapping.AmbientTintHue, AmbientTintSaturation = Mapping.AmbientTintSaturation
        };
        if (groups.HasFlag(PresetGroups.Space)) next = next with
        {
            SeparateLeftRight = Mapping.SeparateLeftRight, SeparateFrontRear = Mapping.SeparateFrontRear,
            DirectionOriginBlend = Mapping.DirectionOriginBlend, SourceDiscRadiusDegrees = Mapping.SourceDiscRadiusDegrees,
            SourceDiscSoftness = Mapping.SourceDiscSoftness, FadeStart = Mapping.FadeStart, Radius = Mapping.Radius,
            FadeExponent = Mapping.FadeExponent, Spread = Mapping.Spread,
            DirectionNormalization = Mapping.DirectionNormalization, CameraYawOffset = Mapping.CameraYawOffset
        };
        LightMapper.Validate(next);
        return next;
    }

    private static string ValidateName(string? name)
    {
        name = name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80 || name.Any(char.IsControl))
            throw new CrimsonHueException("Preset name must contain 1–80 printable characters.");
        return name;
    }

    private static void RequireKeys(JsonElement element, HashSet<string> expected, string label)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            element.EnumerateObject().Count() != expected.Count ||
            !element.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal).SetEquals(expected))
            throw new CrimsonHueException($"{label} has missing or unexpected fields.");
    }
}
