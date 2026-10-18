using CrimsonHue.Core;

namespace CrimsonHue.App;

public partial class MainWindow
{
    // Effective output and preserved custom tuning are separate. Switching to
    // Recommended cannot silently retain a cutoff/gamma/color correction.
    private AppSettings CurrentSettings(MappingSettings? custom = null, LightingPreferences? preferences = null,
        OverlayShortcut? binding = null) => new(BridgeAddress.Text.Trim(), TelemetryAddress.Text.Trim(),
            SelectedArea?.Id ?? selectedAreaId, custom ?? manualMapping, Shortcut: binding ?? shortcut,
            WallWash: wallWash, Lighting: preferences ?? lighting);

    private void CommitCustomMapping(MappingSettings next)
    {
        LightMapper.Validate(next);
        manualMapping = next;
        lighting = lighting with { Recommended = false };
        Volatile.Write(ref mapping, next);
    }

    private void SelectLightingProfile(bool recommended)
    {
        // Keep the current overall limit when adopting the recommended profile.
        var next = lighting with { Recommended = recommended,
            Brightness = recommended ? mapping.Brightness : lighting.Brightness };
        var effective = LightingProfile.Resolve(next, manualMapping);
        lighting = next;
        Volatile.Write(ref mapping, effective);
        SyncTuningValues();
        TuningStatus.Text = recommended
            ? "Recommended lighting active. Your custom tuning is kept in Advanced. Save to keep this choice."
            : "Custom tuning active. Advanced settings affect output. Save to keep this choice.";
    }

    private void SetLightingPreference(string key, double value)
    {
        if (!double.IsFinite(value)) throw new CrimsonHueException("Invalid lighting value.");
        if (!lighting.Recommended && key is "PointLightStrength" or "SpotLightStrength" or "PlayerLanternStrength")
        {
            CommitCustomMapping(key switch {
                "PointLightStrength" => manualMapping with { PointLightStrength = value },
                "SpotLightStrength" => manualMapping with { SpotLightStrength = value },
                _ => manualMapping with { PlayerLanternStrength = value } });
            SyncTuningValues();
            TuningStatus.Text = "Light balance changed · Save keeps it for the next launch";
            return;
        }
        if (!lighting.Recommended) throw new CrimsonHueException("Select Recommended lighting before changing these controls.");
        var next = key switch
        {
            "Brightness" => lighting with { Brightness = value },
            "LocalAmount" => lighting with { LocalAmount = value },
            "BackgroundAmount" => lighting with { BackgroundAmount = value },
            "PointLightStrength" => lighting with { PointLightStrength = value },
            "SpotLightStrength" => lighting with { SpotLightStrength = value },
            "PlayerLanternStrength" => lighting with { PlayerLanternStrength = value },
            "Response" when value == Math.Truncate(value) && value is >= 0 and <= 2 =>
                lighting with { Response = (LightingResponse)(int)value },
            _ => throw new CrimsonHueException("Invalid lighting control.")
        };
        var effective = LightingProfile.Resolve(next, manualMapping);
        lighting = next;
        Volatile.Write(ref mapping, effective);
        SyncTuningValues();
        TuningStatus.Text = "Recommended lighting · live changes · Save keeps them for the next launch";
    }
}
