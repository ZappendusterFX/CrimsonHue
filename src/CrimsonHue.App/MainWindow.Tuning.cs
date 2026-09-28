using System.IO;
using System.Windows;
using System.Windows.Controls;
using CrimsonHue.Core;

namespace CrimsonHue.App;

public partial class MainWindow
{
    private sealed record Parameter(string Key, string Group, string Title, string Unit, string Description,
        double Min, double Max, double Step, double Scale,
        Func<MappingSettings, double> Get, Func<MappingSettings, double, MappingSettings> Set);
    private readonly Dictionary<string, TuningControl> tuningControls = [];
    private readonly DistanceCurveView distanceCurve = new() { Height = 115, Margin = new Thickness(0, 8, 0, 4) };
    private static readonly MappingSettings defaults = new();
    private static readonly Parameter[] parameters =
    [
        new("Brightness", "Output", "Maximum brightness", "%", "Final limit for every lamp; imported channel balance still applies.", 0, 100, 1, 100, m => m.Brightness, (m,v) => m with { Brightness=v }),
        new("Gain", "Output", "Light sensitivity", "×", "Exposure of each local source before distance fade. Zero removes local sources.", 0, 10, .05, 1, m => m.Gain, (m,v) => m with { Gain=v }),
        new("LocalStrength", "Output", "Local light mix", "%", "Amount of local light added to the Ambient layer.", 0, 400, 5, 100, m => m.LocalStrength, (m,v) => m with { LocalStrength=v }),
        new("OutputGamma", "Output", "Output gamma", "", "1 is neutral. Higher values lift dim output; black stays black.", .1, 4, .05, 1, m => m.OutputGamma, (m,v) => m with { OutputGamma=v }),
        new("SmoothingMs", "Output", "Legacy smoothing", "ms", "Only for the old rendered feed. Confirmed-visible lights clear immediately.", 0, 1000, 10, 1, m => m.SmoothingMs, (m,v) => m with { SmoothingMs=v }),
        new("AmbientSensitivity", "Ambient", "Ambient exposure", "×", "Scales CDT's local Ambient level. Zero disables the Ambient layer.", 0, 10, .05, 1, m => m.AmbientSensitivity, (m,v) => m with { AmbientSensitivity=v }),
        new("AmbientReferenceLevel", "Ambient", "Ambient reference level", "raw units", "At 1× exposure: level after the black threshold that produces 63% response.", .01, 100, .1, 1, m => m.AmbientReferenceLevel, (m,v) => m with { AmbientReferenceLevel=v }),
        new("AmbientOutput", "Ambient", "Ambient mix", "%", "Maximum linear contribution of the Ambient layer before the master limit.", 0, 100, 1, 100, m => m.AmbientOutput, (m,v) => m with { AmbientOutput=v }),
        new("AmbientFloor", "Ambient", "Extra background light", "%", "For fresh, enabled Ambient. 0 adds nothing; raising it also lights black samples.", 0, 100, 1, 100, m => m.AmbientFloor, (m,v) => m with { AmbientFloor=v }),
        new("AmbientCutoff", "Ambient", "Ambient black threshold", "raw units", "Subtract this from the raw level. 0 keeps every reported value.", 0, 100, .001, 1, m => m.AmbientCutoff, (m,v) => m with { AmbientCutoff=v }),
        new("DaylightLocalStrength", "Ambient", "Local mix in bright Ambient", "%", "100 keeps local contrast. Lower values suppress it as Ambient rises.", 0, 200, 1, 100, m => m.DaylightLocalStrength, (m,v) => m with { DaylightLocalStrength=v }),
        new("AmbientSmoothingMs", "Ambient", "Ambient transition", "ms", "Response time for changing Ambient brightness. 0 responds immediately.", 0, 5000, 50, 1, m => m.AmbientSmoothingMs, (m,v) => m with { AmbientSmoothingMs=v }),
        new("HueShiftDegrees", "Color", "Local hue shift", "°", "Rotate all local-light colors. 0 keeps their hue; no fire-specific rule.", -180, 180, 1, 1, m => m.HueShiftDegrees, (m,v) => m with { HueShiftDegrees=v }),
        new("Saturation", "Color", "Local saturation", "%", "100 keeps source saturation. 0 removes color; values above 100 intensify it.", 0, 200, 1, 100, m => m.Saturation, (m,v) => m with { Saturation=v }),
        new("RedGain", "Color", "Red balance", "×", "Local-source red multiplier before exposure. 1 is unchanged.", 0, 4, .01, 1, m => m.RedGain, (m,v) => m with { RedGain=v }),
        new("GreenGain", "Color", "Green balance", "×", "Local-source green multiplier before exposure. 1 is unchanged.", 0, 4, .01, 1, m => m.GreenGain, (m,v) => m with { GreenGain=v }),
        new("BlueGain", "Color", "Blue balance", "×", "Local-source blue multiplier before exposure. 1 is unchanged.", 0, 4, .01, 1, m => m.BlueGain, (m,v) => m with { BlueGain=v }),
        new("AmbientTintHue", "Color", "Ambient tint hue", "°", "Your chosen tint; this is not a conversion of CDT sky RGB.", 0, 360, 1, 1, m => m.AmbientTintHue, (m,v) => m with { AmbientTintHue=v }),
        new("AmbientTintSaturation", "Color", "Ambient tint saturation", "%", "0 keeps Ambient neutral white. Increase to apply the chosen tint.", 0, 100, 1, 100, m => m.AmbientTintSaturation, (m,v) => m with { AmbientTintSaturation=v }),
        new("FadeStart", "Space", "Fade starts", "game units", "Sources keep full distance strength inside this player-relative distance.", 0, Math.BitDecrement(1000), .5, 1, m => m.FadeStart, (m,v) => m with { FadeStart=v }),
        new("Radius", "Space", "Off beyond", "game units", "Sources at or beyond this distance contribute nothing.", 1, 1000, .5, 1, m => m.Radius, (m,v) => m with { Radius=v }),
        new("FadeExponent", "Space", "Distance falloff", "", "Higher values remove distant influence faster; see the curve above.", .1, 8, .1, 1, m => m.FadeExponent, (m,v) => m with { FadeExponent=v }),
        new("Spread", "Space", "Directional focus", "", "0 reaches every channel equally; higher values concentrate light direction.", 0, 16, .1, 1, m => m.Spread, (m,v) => m with { Spread=v }),
        new("DirectionNormalization", "Space", "Strongest-channel compensation", "%", "100 gives each source full angular strength at its best matching channel.", 0, 100, 1, 100, m => m.DirectionNormalization, (m,v) => m with { DirectionNormalization=v }),
        new("CameraYawOffset", "Space", "Room direction offset", "°", "Rotate the camera-to-room alignment without moving imported Hue positions.", -180, 180, 1, 1, m => m.CameraYawOffset, (m,v) => m with { CameraYawOffset=v })
    ];

    private void InitializeTuning()
    {
        var panels = new Dictionary<string, StackPanel>
        {
            ["Output"] = OutputControls, ["Ambient"] = AmbientControls,
            ["Color"] = ColorControls, ["Space"] = SpatialControls
        };
        SpatialControls.Children.Add(distanceCurve);
        foreach (var p in parameters)
        {
            var control = new TuningControl(p.Key, p.Title, p.Description, p.Unit,
                p.Min, p.Max, p.Step, p.Get(defaults) * p.Scale);
            control.ValueCommitted += (_, value) => ApplyParameter(p, value / p.Scale);
            tuningControls.Add(p.Key, control);
            panels[p.Group].Children.Add(control);
        }
    }

    private void ApplyParameter(Parameter parameter, double value)
    {
        if (!initialized || updatingSettings) return;
        var next = parameter.Set(mapping, value);
        if (next.FadeStart >= next.FadeEnd)
            next = parameter.Key == "FadeStart" ? next with { Radius = Math.Min(1000, next.FadeStart + .5) } :
                next with { FadeStart = Math.Max(0, next.FadeEnd - .5) };
        LightMapper.Validate(next);
        Volatile.Write(ref mapping, next);
        SyncTuningValues();
        TuningStatus.Text = "Live changes · saved on Save, Start or close";
    }

    private void SyncTuningValues()
    {
        updatingSettings = true;
        try
        {
            foreach (var p in parameters) tuningControls[p.Key].SetDisplayValue(p.Get(mapping) * p.Scale);
            distanceCurve.Settings = mapping;
            distanceCurve.InvalidateVisual();
        }
        finally { updatingSettings = false; }
    }

    private void ResetTabClick(object sender, RoutedEventArgs e)
    {
        var group = (MixerTabs.SelectedItem as TabItem)?.Tag as string;
        var next = mapping;
        foreach (var p in parameters.Where(p => p.Group == group)) next = p.Set(next, p.Get(defaults));
        LightMapper.Validate(next);
        Volatile.Write(ref mapping, next);
        SyncTuningValues();
        TuningStatus.Text = $"{group ?? "Setup"} defaults · click Save to keep";
    }

    private void SaveTuningClick(object sender, RoutedEventArgs e)
    {
        try { SaveSettings(); TuningStatus.Text = "Settings saved for the next launch"; }
        catch (IOException) { TuningStatus.Text = "Could not save settings in your user profile"; }
    }

    internal void VerifyDistanceControls()
    {
        if (!smoke) throw new InvalidOperationException("Control self-test requires isolated smoke mode.");
        var original = mapping;
        try
        {
            var fields = typeof(MappingSettings).GetProperties().Where(p => p.PropertyType == typeof(double) && p.Name != "FadeEnd").Select(p => p.Name).Order().ToArray();
            if (!fields.SequenceEqual(parameters.Select(p => p.Key).Order())) throw new InvalidOperationException("A mapping setting has no visible control.");
            foreach (var p in parameters)
            {
                var value = (p.Min + p.Max) / 2;
                tuningControls[p.Key].SetUserValue(value);
                if (Math.Abs(p.Get(mapping) * p.Scale - value) > 1e-8) throw new InvalidOperationException(p.Key + " did not update live mapping.");
            }
            tuningControls["FadeStart"].SetUserValue(999.5);
            if (mapping.FadeEnd != 1000) throw new InvalidOperationException("Distance end did not follow start.");
            tuningControls["FadeStart"].SetUserValue(999.8);
            if (mapping.FadeStart != 999.8 || tuningControls["FadeStart"].Value != 999.8) throw new InvalidOperationException("Precise fade start was clamped.");
            tuningControls["Radius"].SetUserValue(1);
            if (mapping.FadeStart != .5) throw new InvalidOperationException("Distance start did not follow end.");
            if (!tuningControls["HueShiftDegrees"].VerifyNumericInput("23.5") || mapping.HueShiftDegrees != 23.5)
                throw new InvalidOperationException("Numeric tuning entry failed.");
            if (tuningControls["HueShiftDegrees"].VerifyNumericInput("NaN") || tuningControls["HueShiftDegrees"].VerifyNumericInput("181") || mapping.HueShiftDegrees != 23.5)
                throw new InvalidOperationException("Invalid numeric entry changed mapping.");
            LightMapper.Validate(mapping);
        }
        finally { Volatile.Write(ref mapping, original); SyncTuningValues(); }
    }
}
