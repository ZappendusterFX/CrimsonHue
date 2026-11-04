using System.Collections.Concurrent;
using System.Text.Json;
using CrimsonHue.Core;

namespace CrimsonHue.App;

public partial class MainWindow
{
    private bool hueEnabled;
    private readonly ConcurrentDictionary<string, string> outputFailures = new();
    private bool CanConfigureOutputs => !closing && !busy && streamTask == null && !tuyaConfiguring && !tuyaProbing;
    private OutputSelection SelectedOutputs => new(hueEnabled, openRgbLayout.Enabled, tuyaLayout.Enabled);
    private OutputSelection ReadyOutputs => SelectedOutputs.Ready(
        bridgeConnected && bridge != null && SelectedArea?.Channels.Count > 0,
        (openRgbLayout.Devices ?? []).Any(d => d.Enabled),
        (tuyaLayout.Devices ?? []).Any(d => d.Enabled && TuyaKey(d.Id) != null));
    private bool CanStartOutputs => CanConfigureOutputs && !smoke && DemoCheck.IsChecked != true && ReadyOutputs.Any;
    private bool AnyOutputSending => session?.Sending == true || openRgbSession?.State.Sending == true || tuyaSession?.State.Sending == true;

    private IReadOnlyList<ChannelColor> SelectedOutputColors()
    {
        var colors = new List<ChannelColor>();
        if (hueEnabled) colors.AddRange(Room.Colors);
        if (openRgbLayout.Enabled && openRgbPreviewColors != null)
            colors.AddRange(openRgbPreviewColors.Values.Select(rgb => new ChannelColor(0, rgb)));
        if (tuyaLayout.Enabled && tuyaPreviewColors != null)
            colors.AddRange(tuyaPreviewColors.Values.Select(rgb => new ChannelColor(0, rgb)));
        return colors;
    }

    private async Task ConnectSelectedOutputsAsync()
    {
        await GuardAsync(ConnectTelemetryAsync);
        if (hueEnabled && bridge != null) await GuardAsync(LoadAreasAsync, bridgeSetup: true);
    }

    private string OutputStatus()
    {
        var statuses = new List<string>();
        if (hueEnabled) statuses.Add(outputFailures.TryGetValue("Hue", out var hueFailure) ? "Hue · " + hueFailure :
            session?.Status ?? (bridgeConnected && SelectedArea != null ? "Hue · ready" : "Hue · configure bridge and area"));
        if (openRgbLayout.Enabled) statuses.Add(outputFailures.TryGetValue("OpenRGB", out var rgbFailure) ? "OpenRGB · " + rgbFailure :
            openRgbSession?.State.Status ?? "OpenRGB · select devices in its tab");
        if (tuyaLayout.Enabled) statuses.Add(outputFailures.TryGetValue("Tuya", out var tuyaFailure) ? "Tuya · " + tuyaFailure :
            tuyaSession?.State.Status ?? tuyaStatus);
        return statuses.Count == 0 ? "Choose Hue, OpenRGB or Tuya in Setup. Each output works independently." : string.Join(" | ", statuses);
    }

    private void SetOutput(JsonElement command)
    {
        if (!CanConfigureOutputs) throw new CrimsonHueException("Stop lighting sync before changing selected outputs.");
        var enabled = command.GetProperty("value").GetBoolean();
        var backend = command.GetProperty("backend").GetString();
        // Save before publishing a changed selection. Each output owns its own
        // configuration, so failed persistence cannot half-change the selection.
        switch (backend)
        {
            case "hue":
                if (!smoke) store.SaveSettings(CurrentSettings() with { HueEnabled = enabled });
                hueEnabled = enabled; break;
            case "openRgb":
                var rgb = openRgbLayout with { Enabled = enabled };
                if (!smoke) store.SaveSettings(CurrentSettings() with { OpenRgb = rgb });
                CommitOpenRgb(rgb); openRgbSaveTimer.Stop(); break;
            case "tuya":
                var tuya = tuyaLayout with { Enabled = enabled };
                if (!smoke) store.SaveTuyaConfiguration(tuya, tuyaKeys);
                CommitTuya(tuya); tuyaDirty = false; tuyaSaveTimer.Stop(); break;
            default: throw new CrimsonHueException("Unknown lighting output.");
        }
        UpdateButtons(); StreamStatus.Text = OutputStatus();
        // A saved pairing can be re-enabled without reconnecting unrelated outputs.
        if (!smoke && backend == "hue" && hueEnabled && bridge != null && !bridgeConnected) _ = GuardAsync(LoadAreasAsync, bridgeSetup: true);
    }

    private Task RunLightingAsync(EntertainmentArea? area, CancellationToken token)
    {
        var outputs = new List<LightingOutput>();
        if (session is { } hue && area != null)
            outputs.Add(new("Hue", ct => hue.RunAsync(area, telemetry, () => Volatile.Read(ref mapping), ct,
                ambient, () => Volatile.Read(ref wallWash), activity, () => Volatile.Read(ref menuLighting))));
        if (openRgbSession is { } rgb)
            outputs.Add(new("OpenRGB", ct => rgb.RunAsync(telemetry, ambient, () => Volatile.Read(ref mapping), ct,
                () => Volatile.Read(ref openRgbLayout), activity, () => Volatile.Read(ref menuLighting))));
        if (tuyaSession is { } tuya)
            outputs.Add(new("Tuya", ct => tuya.RunAsync(telemetry, ambient, () => Volatile.Read(ref mapping), ct,
                () => Volatile.Read(ref tuyaLayout), TuyaKey, activity, () => Volatile.Read(ref menuLighting))));
        return IndependentOutputs.RunAsync(outputs, token, (name, failure) => outputFailures[name] = failure);
    }

    private void VerifyIndependentOutputControls()
    {
        if (!smoke) throw new InvalidOperationException("Output regression requires synthetic mode.");
        var oldHue = hueEnabled; var oldConnected = bridgeConnected; var oldArea = AreaBox.SelectedItem;
        var oldRgb = openRgbLayout; var oldTuya = tuyaLayout; var oldKeys = tuyaKeys;
        try
        {
            hueEnabled = false; bridgeConnected = false; AreaBox.SelectedItem = null;
            var bridgeAddress = BridgeAddress.Text;
            ApplyOverlayAddresses("", TelemetryAddress.Text);
            if (BridgeAddress.Text != bridgeAddress) throw new InvalidOperationException("Hue-free setup changed a saved bridge address.");
            openRgbLayout = oldRgb with { Enabled = true }; tuyaLayout = oldTuya with { Enabled = false };
            UpdateButtons();
            if (ReadyOutputs != new OutputSelection(OpenRgb: true))
                throw new InvalidOperationException("OpenRGB-only readiness depends on Hue.");
            openRgbLayout = oldRgb with { Enabled = false }; tuyaLayout = oldTuya with { Enabled = true };
            tuyaKeys = oldTuya.Devices!.ToDictionary(d => d.Id, _ => "0123456789abcdef");
            UpdateButtons();
            if (ReadyOutputs != new OutputSelection(Tuya: true))
                throw new InvalidOperationException("Tuya-only readiness depends on Hue or OpenRGB.");
            // Ready device configuration must still never bypass smoke isolation.
            if (StartButton.IsEnabled || CanStartOutputs || AnyOutputSending)
                throw new InvalidOperationException("Synthetic output readiness enabled physical output.");
            OverlayCommand(JsonSerializer.SerializeToUtf8Bytes(new { action = "start" }));
            if (streamTask != null || session != null || openRgbSession != null || tuyaSession != null)
                throw new InvalidOperationException("Synthetic Start created an output session.");
        }
        finally
        {
            hueEnabled = oldHue; bridgeConnected = oldConnected; AreaBox.SelectedItem = oldArea;
            openRgbLayout = oldRgb; tuyaLayout = oldTuya; tuyaKeys = oldKeys; UpdateButtons();
        }
    }
}
