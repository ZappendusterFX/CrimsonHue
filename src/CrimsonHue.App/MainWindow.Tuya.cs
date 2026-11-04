using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Threading;
using CrimsonHue.Core;

namespace CrimsonHue.App;

public partial class MainWindow
{
    private TuyaLayout tuyaLayout = new();
    private TuyaStreamingSession? tuyaSession;
    private TuyaStreamState? lastTuyaDiagnostics;
    private IReadOnlyDictionary<string, string> tuyaKeys = new Dictionary<string, string>();
    private readonly LightMapper tuyaPreviewMapper = new();
    private readonly MenuLightingPolicy tuyaPreviewMenu = new();
    private IReadOnlyDictionary<string, Vec3>? tuyaPreviewColors;
    private readonly DispatcherTimer tuyaSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private bool tuyaDirty;
    private string tuyaStatus = "Tuya · add a Wi-Fi 3.5 lamp";
    private bool tuyaConfiguring;
    private bool tuyaProbing;
    private Task? tuyaProbeTask;
    private string? TuyaKey(string id) => Volatile.Read(ref tuyaKeys).GetValueOrDefault(id);

    private void RememberTuyaDiagnosticState()
    {
        // Called after session cleanup, before the active session is released.
        // Keep its result for the HUD, never a color source for stopped preview.
        if (tuyaSession is { } active) lastTuyaDiagnostics = active.State with { DeviceColors = null };
    }

    private void InitializeTuya()
    {
        tuyaSaveTimer.Tick += (_, _) =>
        {
            tuyaSaveTimer.Stop();
            try { FlushTuyaSettings(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or CrimsonHueException)
            { tuyaStatus = "Tuya · could not save configuration securely"; }
        };
        if (smoke)
        {
            tuyaLayout = new(Enabled: true, Devices: [
                new("synthetic-left", "Tuya lamp · left", "127.0.0.1", "synthetic-left", new(-.8, .7, .1)),
                new("synthetic-right", "Tuya lamp · right", "127.0.0.2", "synthetic-right", new(.8, .7, .1))]);
            tuyaStatus = "Synthetic Tuya preview · no network or lamp output";
            return;
        }
        try
        {
            if (store.LoadTuyaConfiguration() is { } saved) { tuyaLayout = saved.Layout; tuyaKeys = saved.Keys; }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or CryptographicException or CrimsonHueException)
        { tuyaStatus = "Tuya · Windows could not unlock the saved keys; edit lamp connections"; }
    }
    private void CommitTuya(TuyaLayout next)
    {
        next.Validate(); Volatile.Write(ref tuyaLayout, next);
        tuyaPreviewMapper.Reset(); UpdateButtons();
        tuyaDirty = true; tuyaSaveTimer.Stop(); tuyaSaveTimer.Start();
    }
    private void FlushTuyaSettings()
    {
        if (!tuyaDirty) return;
        if (!smoke) store.SaveTuyaConfiguration(tuyaLayout, tuyaKeys);
        tuyaDirty = false; tuyaSaveTimer.Stop();
    }
    private void SetTuya(JsonElement command)
    {
        var key = command.GetProperty("key").GetString();
        var config = tuyaLayout;
        if (key is "enabled" or "deviceEnabled" && !CanConfigureOutputs)
            throw new CrimsonHueException("Stop lighting sync before changing selected lamps.");
        var next = key switch
        {
            "enabled" => config with { Enabled = command.GetProperty("value").GetBoolean() },
            "brightness" => config with { Brightness = command.GetProperty("value").GetDouble() },
            "sourceRadius" => config with { SourceRadiusDegrees = command.GetProperty("value").GetDouble() },
            "background" => config with { BackgroundAmount = command.GetProperty("value").GetDouble() },
            "position" or "deviceEnabled" => config with { Devices = ChangeDevice() },
            _ => throw new CrimsonHueException("Unknown Tuya setting.")
        };
        if (key is "enabled" or "deviceEnabled")
        {
            if (!smoke) store.SaveTuyaConfiguration(next, tuyaKeys);
            CommitTuya(next); tuyaDirty = false; tuyaSaveTimer.Stop();
        }
        else CommitTuya(next);
        StreamStatus.Text = OutputStatus();
        IReadOnlyList<TuyaLamp> ChangeDevice()
        {
            var id = command.GetProperty("device").GetString();
            if (!(config.Devices ?? []).Any(d => d.Id == id)) throw new CrimsonHueException("Select a configured Tuya lamp.");
            return (config.Devices ?? []).Select(d => d.Id != id ? d : key == "position" ?
                d with { Position = Vec3.Read(command.GetProperty("position")) } :
                d with { Enabled = command.GetProperty("value").GetBoolean() }).ToArray();
        }
    }
    private void ConfigureTuya(string id)
    {
        if (smoke || !CanConfigureOutputs) return;
        tuyaConfiguring = true; UpdateButtons();
        try
        {
            var existing = (tuyaLayout.Devices ?? []).FirstOrDefault(d => d.Id == id);
            var dialog = new TuyaSetupWindow(existing, existing == null ? null : TuyaKey(existing.Id));
            if (dialog.ShowDialog() != true || dialog.Lamp is not { } lamp || dialog.LocalKey is not { } localKey) return;
            var next = tuyaLayout with { Devices = (tuyaLayout.Devices ?? []).Where(d => d.Id != id).Append(lamp).ToArray() };
            next.Validate();
            var keys = new Dictionary<string, string>(tuyaKeys);
            if (existing != null) keys.Remove(existing.Id);
            keys[lamp.Id] = localKey;
            // One atomic protected file keeps the endpoint/profile and its key together.
            store.SaveTuyaConfiguration(next, keys);
            Volatile.Write(ref tuyaLayout, next);
            Volatile.Write(ref tuyaKeys, keys);
            lastTuyaDiagnostics = null;
            tuyaDirty = false; tuyaSaveTimer.Stop();
            tuyaPreviewMapper.Reset(); tuyaStatus = "Tuya · lamp saved; enable output, then Start lighting sync";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or CrimsonHueException or JsonException)
        { tuyaStatus = "Tuya · could not save lamp configuration securely"; }
        finally { tuyaConfiguring = false; UpdateButtons(); }
    }
    private void RemoveTuya(string id)
    {
        if (smoke || !CanConfigureOutputs) return;
        if (!(tuyaLayout.Devices ?? []).Any(d => d.Id == id)) return;
        var next = tuyaLayout with { Devices = (tuyaLayout.Devices ?? []).Where(d => d.Id != id).ToArray() };
        var keys = new Dictionary<string, string>(tuyaKeys); keys.Remove(id);
        store.SaveTuyaConfiguration(next, keys);
        CommitTuya(next); tuyaDirty = false; tuyaSaveTimer.Stop(); Volatile.Write(ref tuyaKeys, keys);
        lastTuyaDiagnostics = null;
        tuyaStatus = "Tuya · lamp removed";
    }
    private void ProbeTuya(string id)
    {
        if (smoke || !CanConfigureOutputs || tuyaProbeTask?.IsCompleted == false) return;
        var lamp = (tuyaLayout.Devices ?? []).FirstOrDefault(d => d.Id == id);
        if (lamp == null) return;
        tuyaProbing = true; UpdateButtons();
        tuyaProbeTask = Probe();
        async Task Probe()
        {
            tuyaStatus = "Tuya · checking TCP reachability (no light command)";
            try { tuyaStatus = await new TuyaClient().ProbeAsync(lamp, lifetime.Token) ?
                "Tuya · TCP reachable; key and lamp output not tested" : "Tuya · TCP not reachable"; }
            catch (OperationCanceledException) { tuyaStatus = "Tuya · reachability check canceled"; }
            catch (Exception) { tuyaStatus = "Tuya · TCP not reachable"; }
            finally { tuyaProbing = false; UpdateButtons(); }
        }
    }
    private object TuyaSnapshot()
    {
        var config = tuyaLayout;
        var activeState = tuyaSession?.State;
        var diagnostics = activeState ?? lastTuyaDiagnostics;
        var colors = activeState?.DeviceColors;
        if (colors == null)
        {
            var demo = smoke || DemoCheck.IsChecked == true;
            var frame = demo ? DemoData.Frame(uptime.Elapsed.TotalSeconds) : telemetry.Read(out _);
            var decision = tuyaPreviewMenu.Evaluate(demo ? null : activity.Read(out _), frame,
                demo ? null : ambient.Read(out _), demo ? menuLighting with { Enabled = false } : menuLighting);
            if (!demo) frame = decision.Frame;
            if (decision.ResetMapper) tuyaPreviewMapper.Reset();
            var devices = (config.Devices ?? []).Where(d => d.Enabled).ToArray();
            if (devices.Length > 0)
            {
                var area = new EntertainmentArea("tuya-preview", "Tuya room", false,
                    devices.Select((d, i) => new EntertainmentChannel((byte)i, d.Position, [], d.Name)).ToArray());
                IReadOnlyList<ChannelColor> mapped = frame == null ? [] : tuyaPreviewMapper.Map(frame, area, config.Mapping(mapping), .1,
                    demo ? null : decision.Ambient);
                if (frame == null) tuyaPreviewMapper.Reset();
                if (!demo) mapped = MenuLightingPolicy.Apply(decision, mapped, area, config.Mapping(mapping), menuLighting);
                colors = devices.Select((d, i) => (d.Id, Rgb: mapped.FirstOrDefault(c => c.Id == i)?.Rgb ?? default))
                    .ToDictionary(d => d.Id, d => d.Rgb);
            }
        }
        tuyaPreviewColors = colors;
        return new { available = true, config.Enabled, config.Brightness, config.SourceRadiusDegrees, config.BackgroundAmount,
            status = activeState?.Status ?? tuyaStatus, busy = tuyaConfiguring || tuyaProbing,
            canConfigure = CanConfigureOutputs, devices = (config.Devices ?? []).Select(d => {
                var configured = smoke || TuyaKey(d.Id) != null;
                var deviceStatus = diagnostics?.DeviceStatuses?.GetValueOrDefault(d.Id);
                return new {
                    key = d.Id, d.Name, d.Enabled, position = d.Position, configured,
                    status = deviceStatus ?? (smoke ? "Synthetic preview - no device connection" :
                        !configured ? "Connection configuration required" : !d.Enabled ? "Not selected for lighting sync" :
                        activeState?.Status ?? "Ready for lighting sync"),
                    statusFromLastSync = activeState == null && deviceStatus != null,
                    rgb = colors?.GetValueOrDefault(d.Id) ?? default };
            }) };
    }
}
