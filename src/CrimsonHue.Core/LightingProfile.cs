using System.Globalization;

namespace CrimsonHue.Core;

public enum LightingResponse { Quick, Balanced, Calm }

public sealed record LightingPreferences(bool Recommended = true, double Brightness = .85,
    double LocalAmount = 1, double BackgroundAmount = .4,
    LightingResponse Response = LightingResponse.Balanced, int Revision = 1,
    double PointLightStrength = 1, double SpotLightStrength = 1, double PlayerLanternStrength = .1);

/// <summary>A deterministic starting profile, not scene or lamp auto-calibration.</summary>
public static class LightingProfile
{
    // Product/reset defaults are separate from MappingSettings' historical
    // constructor defaults, which also fill omitted fields in saved presets.
    public static MappingSettings Defaults { get; } = new(Spread: 4,
        SourceDiscRadiusDegrees: 30, SourceDiscSoftness: .2, PlayerLanternStrength: .1);

    public static void Validate(LightingPreferences preferences)
    {
        if (preferences is null || !InRange(preferences.Brightness, 0, 1) ||
            !InRange(preferences.LocalAmount, 0, 2) || !InRange(preferences.BackgroundAmount, 0, 1) ||
            !InRange(preferences.PointLightStrength, 0, 2) || !InRange(preferences.SpotLightStrength, 0, 10) ||
            !InRange(preferences.PlayerLanternStrength, 0, 1) ||
            !Enum.IsDefined(preferences.Response) || preferences.Revision != 1)
            throw new CrimsonHueException("Invalid lighting preferences.");
    }

    public static MappingSettings Resolve(LightingPreferences preferences, MappingSettings manual)
    {
        Validate(preferences);
        LightMapper.Validate(manual);
        if (!preferences.Recommended) return manual;
        var (localMs, ambientMs) = preferences.Response switch
        {
            LightingResponse.Quick => (100.0, 400.0),
            LightingResponse.Balanced => (250.0, 600.0),
            LightingResponse.Calm => (500.0, 1000.0),
            _ => throw new CrimsonHueException("Invalid lighting response.")
        };
        // The manual record is never rewritten: switching back restores it exactly.
        // Room alignment remains personal; threshold, fill and color changes do not.
        return Defaults with { Brightness = preferences.Brightness,
            LocalStrength = preferences.LocalAmount, AmbientOutput = preferences.BackgroundAmount,
            SmoothingMs = localMs, AmbientSmoothingMs = ambientMs, CameraYawOffset = manual.CameraYawOffset,
            PointLightStrength = preferences.PointLightStrength, SpotLightStrength = preferences.SpotLightStrength,
            PlayerLanternStrength = preferences.PlayerLanternStrength };
    }

    public static LightingPreferences FromLegacy(MappingSettings mapping)
    {
        LightMapper.Validate(mapping);
        // Keep the personal cap, but do not carry custom disabling controls into
        // the recommended profile. All manual values remain in the manual record.
        return new(Recommended: false, Brightness: Math.Clamp(mapping.Brightness, 0, 1));
    }

    private static bool InRange(double value, double min, double max) =>
        double.IsFinite(value) && value >= min && value <= max;
}

public sealed record LightingAssessment(string Code, string Summary, string Detail, bool Warning);

public static class LightingDiagnostics
{
    /// <summary>Pass freshness-filtered State.Read results. Colors are calculated
    /// commands, never confirmation of delivery or measured physical brightness.</summary>
    public static LightingAssessment Assess(TelemetryFrame? frame, AmbientFrame? ambient,
        MappingSettings mapping, IReadOnlyList<ChannelColor> colors, bool streaming, bool demo)
    {
        if (demo)
            return new("demo", "Demo preview only", "Synthetic lighting preview; no physical output is sent.", false);
        try { LightMapper.Validate(mapping); }
        catch (CrimsonHueException)
        {
            return Result("invalid-settings", "Lighting settings are invalid",
                "Restore a valid manual configuration or the recommended profile.", true);
        }
        if (mapping.Brightness == 0)
            return Result("master-zero", "Maximum brightness is zero",
                "The master control sets every calculated channel to black.", true);
        if (frame is null)
            return Result("local-unavailable", "Waiting for fresh game telemetry",
                "Missing or stale local telemetry clears both local light and background output until fresh data returns.", true);
        if (ambient is null)
            return Result("ambient-unavailable", "Background input unavailable",
                "CDT has no fresh usable Ambient sample. Available local sources can still contribute.", true);
        if (mapping.AmbientSensitivity == 0 || (mapping.AmbientOutput == 0 && mapping.AmbientFloor == 0))
            return Result("ambient-disabled", "Background disabled in settings",
                mapping.AmbientSensitivity == 0 ? "Ambient exposure is zero, which also disables extra background light." :
                    "Ambient amount and extra background light are both zero. Local lighting remains independent.", false);
        if (ambient.WorkingLevel > 0 && ambient.WorkingLevel <= mapping.AmbientCutoff && mapping.AmbientOutput > 0)
            return Result("ambient-cutoff", "Black threshold removes the Ambient sample",
                FormattableString.Invariant($"Received level {ambient.WorkingLevel:G6} is at or below threshold {mapping.AmbientCutoff:G6}. " ) +
                "Any extra background setting and local sources remain independent.", true);
        if (ambient.WorkingLevel <= 0)
        {
            if (ambient.SkyVisibility == 0 && Math.Max(ambient.SkyRgb.X, Math.Max(ambient.SkyRgb.Y, ambient.SkyRgb.Z)) > 0)
                return Result("sky-visibility-zero", "CDT reports zero camera sky visibility",
                    "The sky sample is nonzero, but its camera visibility makes the reported local Ambient level zero. " +
                    "This feed does not measure all scene lighting. Any extra background setting is independent.", true);
            return Result("ambient-zero", "CDT reports zero Ambient level",
                "There is no natural background contribution in this sample. Local sources and any extra background setting remain independent.", false);
        }
        if (colors.Count == 0)
            return Result("no-output", "No calculated channel output",
                "Select an Entertainment area and check the stream status.", true);
        if (colors.Any(c => !c.Rgb.IsFinite))
            return Result("invalid-output", "Calculated channel output is invalid",
                "A non-finite color cannot be sent to the bridge.", true);
        var peaks = colors.Select(c => Math.Max(c.Rgb.X, Math.Max(c.Rgb.Y, c.Rgb.Z))).ToArray();
        var range = string.Format(CultureInfo.InvariantCulture, "Calculated channel peaks: {0:0.0}% to {1:0.0}% of command range. ",
            peaks.Min() * 100, peaks.Max() * 100);
        if (peaks.Max() <= 0)
            return Result("output-zero", "Calculated output is black",
                "Check source range, color controls and the current transition. " + range, true);
        return Result(streaming ? "output-active" : "preview", streaming ? "Lighting output calculated" : "Lighting preview ready",
            range + "These values do not confirm lamp delivery or measure physical brightness.", false);

        LightingAssessment Result(string code, string summary, string detail, bool warning) =>
            new(code, summary, (streaming ? "Lighting sync is active. " : "Preview only; start lighting sync for lamp output. ") + detail, warning);
    }
}
