using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using CrimsonHue.Core;

namespace CrimsonHue.App;

public partial class MainWindow
{
    private readonly Dictionary<string, TuningControl> menuControls = [];

    private void InitializeMenuLighting()
    {
        var panel = new StackPanel { Margin = new Thickness(10) };
        MixerTabs.Items.Add(new TabItem { Header = "Menus", Tag = "Menus",
            Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
        panel.Children.Add(new TextBlock { Text = "Fixed lighting while the game is paused. Master brightness still applies.", TextWrapping = TextWrapping.Wrap });
        foreach (var (key, title, initial) in new[] { ("Brightness", "Menu brightness", 20d),
            ("Red", "Red", 100d), ("Green", "Green", 65d), ("Blue", "Blue", 35d) })
        {
            var control = new TuningControl(key, title, "", "%", 0, 100, 1, initial);
            control.ValueCommitted += (_, value) => ChangeMenuLighting(key, value / 100);
            menuControls.Add(key, control); panel.Children.Add(control);
        }
        var gold = new Button { Content = "Gold preset", Margin = new Thickness(0, 10, 0, 0) };
        gold.Click += (_, _) => SelectGoldMenuLighting(); panel.Children.Add(gold);
        var reset = new Button { Content = "Reset menu lighting", Margin = new Thickness(0, 10, 0, 0) };
        reset.Click += (_, _) => { Volatile.Write(ref menuLighting, new()); SyncMenuControls(); };
        panel.Children.Add(reset);
        SyncMenuControls();
    }

    private void SyncMenuControls()
    {
        var color = menuLighting.EffectiveColor;
        foreach (var (key, control) in menuControls)
            control.SetDisplayValue(100 * (key switch { "Brightness" => menuLighting.Brightness,
                "Red" => color.X, "Green" => color.Y, _ => color.Z }));
    }

    private void ChangeMenuLighting(string key, double value)
    {
        var color = menuLighting.EffectiveColor;
        var next = key switch
        {
            "Brightness" => menuLighting with { Brightness = value },
            "Red" => menuLighting with { Color = new(value, color.Y, color.Z) },
            "Green" => menuLighting with { Color = new(color.X, value, color.Z) },
            "Blue" => menuLighting with { Color = new(color.X, color.Y, value) },
            _ => throw new CrimsonHueException("Invalid menu lighting control.")
        };
        next.Validate(); Volatile.Write(ref menuLighting, next); SyncMenuControls();
        TuningStatus.Text = "Menu lighting changed · Save keeps it for the next launch";
    }

    private void SetMenuLighting(JsonElement command)
    {
        if (command.TryGetProperty("preset", out var preset))
        {
            if (preset.GetString() != "gold") throw new CrimsonHueException("Unknown menu lighting preset.");
            SelectGoldMenuLighting(); return;
        }
        if (command.TryGetProperty("reset", out var reset) && reset.GetBoolean())
        { Volatile.Write(ref menuLighting, new()); SyncMenuControls(); return; }
        if (command.TryGetProperty("color", out var color))
        {
            var next = menuLighting with { Color = new(color.GetProperty("r").GetDouble(),
                color.GetProperty("g").GetDouble(), color.GetProperty("b").GetDouble()) };
            next.Validate(); Volatile.Write(ref menuLighting, next); SyncMenuControls();
            TuningStatus.Text = "Menu color changed · Save keeps it for the next launch";
            return;
        }
        ChangeMenuLighting("Brightness", command.GetProperty("brightness").GetDouble());
    }

    private void SelectGoldMenuLighting()
    {
        Volatile.Write(ref menuLighting, menuLighting with { Enabled = true, Color = new(1, .65, .08), Brightness = .8 });
        SyncMenuControls();
        TuningStatus.Text = "Gold menu preset selected · Save keeps it for the next launch";
    }

    private void VerifyMenuControls()
    {
        var original = menuLighting;
        var originalMapping = mapping;
        var originalStatus = TuningStatus.Text;
        void Send(object command) => OverlayCommand(JsonSerializer.SerializeToUtf8Bytes(command));
        try
        {
            Send(new { action = "menuLighting", brightness = .35 });
            Send(new { action = "menuLighting", color = new { r = .4, g = .6, b = .8 } });
            if (menuLighting.Brightness != .35 || menuLighting.EffectiveColor != new Vec3(.4, .6, .8) || mapping != originalMapping)
                throw new InvalidOperationException("Menu controls did not update independently of gameplay tuning.");
            var valid = menuLighting;
            Send(new { action = "menuLighting", color = new { r = 2, g = .6, b = .8 } });
            if (menuLighting != valid) throw new InvalidOperationException("Invalid menu color was accepted.");
            if (CurrentSettings().MenuLighting != valid) throw new InvalidOperationException("Menu settings were not preserved.");
            Send(new { action = "menuLighting", preset = "gold" });
            if (menuLighting.Brightness != .8 || menuLighting.EffectiveColor != new Vec3(1, .65, .08) || mapping != originalMapping)
                throw new InvalidOperationException("Gold menu preset changed gameplay tuning or failed to apply.");
            var gold = menuLighting;
            Send(new { action = "menuLighting", preset = "invalid" });
            if (menuLighting != gold) throw new InvalidOperationException("Unknown menu preset changed settings.");
            Send(new { action = "menuLighting", reset = true });
            if (menuLighting != new MenuLightingSettings()) throw new InvalidOperationException("Menu reset failed.");
        }
        finally { menuLighting = original; SyncMenuControls(); TuningStatus.Text = originalStatus; }
    }
}
