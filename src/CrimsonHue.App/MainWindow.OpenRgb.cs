using CrimsonHue.Core;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;

namespace CrimsonHue.App;

public partial class MainWindow
{
    private OpenRgbStreamingSession? openRgbSession;
    // New users opt in; existing private-test selections and enable state survive.
    private OpenRgbLayout openRgbLayout = new(Enabled: false, Devices: []);
    private IReadOnlyList<OpenRgbDevice> openRgbInventory = [];
    private bool openRgbInventoryKnown;
    private readonly LightMapper openRgbPreviewMapper = new();
    private readonly MenuLightingPolicy openRgbPreviewMenu = new();
    private readonly DispatcherTimer openRgbSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private Task? openRgbDiscoveryTask;
    private string openRgbDiscoveryStatus = "Read devices to connect to OpenRGB";

    private void LoadOpenRgbSettings(OpenRgbLayout? saved)
    {
        if (saved == null) return;
        try { saved.Validate(); openRgbLayout = saved; }
        catch (CrimsonHueException) { openRgbDiscoveryStatus = "Invalid OpenRGB settings ignored; read devices to set up again"; }
    }

    private void InitializeOpenRgb()
    {
        if (smoke)
        {
            openRgbLayout = new(Devices: [new(new string('A', 64), "Demo keyboard", new(-.25, .6, -.3)),
                new(new string('B', 64), "Demo mouse", new(.55, .6, -.3)),
                new(new string('C', 64), "Demo controller", new(0, .45, -.6)),
                new(new string('D', 64), "Demo PC lighting", new(.85, .5, -.5))]);
            openRgbInventory = openRgbLayout.Devices!.Select((p, i) => new OpenRgbDevice((uint)i, p.Name, 0, p.DeviceKey,
                [new("Direct", 1u << 5, 1, [])], 0, [new("RGB", 3, 0, null)], ["One", "Two", "Three"], new uint[3])).ToArray();
            openRgbInventoryKnown = true;
        }
        openRgbSaveTimer.Tick += (_, _) =>
        {
            openRgbSaveTimer.Stop();
            try { SaveSettings(); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { openRgbDiscoveryStatus = "Could not save OpenRGB placement"; }
        };
    }

    private void BeginOpenRgbDiscovery()
    {
        if (smoke || closing || openRgbDiscoveryTask?.IsCompleted == false) return;
        openRgbDiscoveryTask = DiscoverOpenRgbAsync();
    }

    private async Task DiscoverOpenRgbAsync()
    {
        openRgbDiscoveryStatus = "Reading OpenRGB devices...";
        openRgbInventoryKnown = false;
        try
        {
            var config = openRgbLayout;
            await using var client = new OpenRgbClient();
            await client.ConnectAsync(config.Port, lifetime.Token);
            var inventory = await client.InventoryAsync(lifetime.Token);
            if (closing || config.Port != openRgbLayout.Port) return;
            var next = openRgbLayout.Discover(inventory);
            openRgbInventory = inventory;
            openRgbInventoryKnown = true;
            CommitOpenRgb(next);
            openRgbDiscoveryStatus = $"Connected · {inventory.Count} devices · SDK {client.ProtocolVersion}";
        }
        catch (Exception ex) when (ex is IOException or System.Net.Sockets.SocketException or OperationCanceledException or CrimsonHueException)
        { openRgbDiscoveryStatus = "OpenRGB unavailable · check SDK server and port"; }
        finally { if (!closing) PublishOverlay(); }
    }

    private void CommitOpenRgb(OpenRgbLayout next)
    {
        next.Validate(); Volatile.Write(ref openRgbLayout, next);
        openRgbPreviewMapper.Reset();
        openRgbSaveTimer.Stop(); openRgbSaveTimer.Start();
    }

    private void SetOpenRgb(JsonElement command)
    {
        var config = openRgbLayout;
        var key = command.GetProperty("key").GetString();
        if (key == "enabled") CommitOpenRgb(config with { Enabled = command.GetProperty("value").GetBoolean() });
        else if (key == "port")
        {
            var next = config with { Port = command.GetProperty("value").GetInt32() };
            CommitOpenRgb(next);
            if (next.Port != config.Port) { openRgbInventoryKnown = false; openRgbInventory = []; }
        }
        else if (key == "brightness") CommitOpenRgb(config with { Brightness = command.GetProperty("value").GetDouble() });
        else if (key == "sourceRadius") CommitOpenRgb(config with { SourceRadiusDegrees = command.GetProperty("value").GetDouble() });
        else if (key == "background") CommitOpenRgb(config with { BackgroundAmount = command.GetProperty("value").GetDouble() });
        else if (key is "position" or "deviceEnabled")
        {
            var id = command.GetProperty("device").GetString();
            if (!(config.Devices ?? []).Any(p => p.DeviceKey == id)) throw new CrimsonHueException("Read OpenRGB devices first.");
            var devices = (config.Devices ?? []).Select(p => p.DeviceKey != id ? p : key == "position" ?
                p with { Position = Vec3.Read(command.GetProperty("position")) } :
                p with { Enabled = command.GetProperty("value").GetBoolean() }).ToArray();
            CommitOpenRgb(config with { Devices = devices });
        }
        else throw new CrimsonHueException("Unknown OpenRGB setting.");
    }

    private bool CanResetOpenRgb => !closing && !busy && streamTask == null && openRgbDiscoveryTask?.IsCompleted != false;
    private bool CanRemoveDisconnectedOpenRgb => CanResetOpenRgb && openRgbInventoryKnown &&
        (openRgbLayout.Devices ?? []).Any(p => !openRgbInventory.Any(d => d.Identity == p.DeviceKey));

    private void ResetOpenRgbSettings()
    {
        if (!CanResetOpenRgb) throw new CrimsonHueException("Stop lighting sync and wait for device reading before resetting OpenRGB.");
        CommitOpenRgb(openRgbLayout.Reset(openRgbInventoryKnown ? openRgbInventory : []));
        SaveSettings(); openRgbSaveTimer.Stop();
        openRgbDiscoveryStatus = "OpenRGB settings reset · output disabled" +
            (openRgbInventoryKnown ? "" : " · read devices to connect");
    }

    private void RemoveDisconnectedOpenRgbDevices()
    {
        if (!CanResetOpenRgb) throw new CrimsonHueException("Stop lighting sync and wait for device reading before removing saved devices.");
        if (!openRgbInventoryKnown) throw new CrimsonHueException("Read OpenRGB devices successfully before removing disconnected entries.");
        var next = openRgbLayout.RemoveDisconnected(openRgbInventory);
        var removed = (openRgbLayout.Devices?.Count ?? 0) - (next.Devices?.Count ?? 0);
        CommitOpenRgb(next);
        SaveSettings(); openRgbSaveTimer.Stop();
        openRgbDiscoveryStatus = $"Removed {removed} disconnected saved device(s)";
    }

    private object OpenRgbSnapshot()
    {
        var config = openRgbLayout;
        var colors = openRgbSession?.State.DeviceColors;
        if (colors == null)
        {
            var demo = smoke || DemoCheck.IsChecked == true;
            var frame = demo ? DemoData.Frame(uptime.Elapsed.TotalSeconds) : telemetry.Read(out _);
            var decision = openRgbPreviewMenu.Evaluate(demo ? null : activity.Read(out _), frame,
                demo ? null : ambient.Read(out _), demo ? menuLighting with { Enabled = false } : menuLighting);
            if (!demo) frame = decision.Frame;
            if (decision.ResetMapper) openRgbPreviewMapper.Reset();
            var entries = (config.Devices ?? []).Where(p => p.Enabled).ToArray();
            if (entries.Length > 0)
            {
                var area = new EntertainmentArea("openrgb-preview", "OpenRGB preview", false,
                    entries.Select((p, i) => new EntertainmentChannel((byte)i, p.Position, [], p.Name)).ToArray());
                IReadOnlyList<ChannelColor> mapped = frame == null ? [] : openRgbPreviewMapper.Map(frame, area, config.Mapping(mapping), .1,
                    demo ? null : decision.Ambient);
                if (frame == null) openRgbPreviewMapper.Reset();
                if (!demo) mapped = MenuLightingPolicy.Apply(decision, mapped, area, config.Mapping(mapping), menuLighting);
                colors = entries.Select((p, i) => (p.DeviceKey, Rgb: mapped.FirstOrDefault(c => c.Id == i)?.Rgb ?? default))
                    .ToDictionary(p => p.DeviceKey, p => p.Rgb);
            }
        }
        return new { available = true, config.Enabled, config.Port, config.Brightness,
            config.SourceRadiusDegrees, config.BackgroundAmount,
            canReset = CanResetOpenRgb, canRemoveDisconnected = CanRemoveDisconnectedOpenRgb,
            status = openRgbDiscoveryStatus, busy = openRgbDiscoveryTask?.IsCompleted == false,
            devices = (config.Devices ?? []).Select(p => {
                var device = openRgbInventory.FirstOrDefault(d => d.Identity == p.DeviceKey);
                return new { key = p.DeviceKey, name = device?.Name ?? p.Name, p.Enabled, position = p.Position,
                    present = smoke || device != null, eligible = smoke || device != null && OpenRgbLayout.Eligible(device),
                    leds = device?.PreviewLeds().Length ?? (smoke ? 3 : 0), rgb = colors?.GetValueOrDefault(p.DeviceKey) ?? default };
            }) };
    }
    private bool OpenRgbOutputAllowed => !smoke && DemoCheck.IsChecked != true;

    private string WithOpenRgbStatus(string hueStatus) => !OpenRgbOutputAllowed || !openRgbLayout.Enabled ? hueStatus :
        hueStatus + " | " + (openRgbSession?.State.Status ?? "OpenRGB · open the OpenRGB tab to place devices");

    private async Task RunLightingAsync(StreamingSession hue, EntertainmentArea area, CancellationToken token)
    {
        using var combined = CancellationTokenSource.CreateLinkedTokenSource(token);
        var rgb = openRgbSession;
        var rgbTask = rgb == null ? Task.CompletedTask :
            rgb.RunAsync(telemetry, ambient, () => Volatile.Read(ref mapping), combined.Token, () => Volatile.Read(ref openRgbLayout),
                activity, () => Volatile.Read(ref menuLighting));
        try
        {
            await hue.RunAsync(area, telemetry, () => Volatile.Read(ref mapping), token,
                ambient, () => Volatile.Read(ref wallWash), activity, () => Volatile.Read(ref menuLighting));
        }
        finally
        {
            await combined.CancelAsync();
            await rgbTask;
        }
    }
}
