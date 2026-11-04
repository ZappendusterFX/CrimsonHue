using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CrimsonHue.Core;

namespace CrimsonHue.App;

// Keys are entered in a current-user Windows dialog and written directly into
// DPAPI storage by the controller. They never enter CDT's HUD/shared memory.
internal sealed class TuyaSetupWindow : Window
{
    internal TuyaLamp? Lamp { get; private set; }
    internal string? LocalKey { get; private set; }
    private readonly Dictionary<string, TextBox> fields = [];
    private readonly PasswordBox password = new() { MaxLength = 32, Margin = new(0, 4, 0, 12), Padding = new(7),
        Background = new SolidColorBrush(Color.FromRgb(16, 21, 29)), Foreground = Brushes.WhiteSmoke,
        BorderBrush = new SolidColorBrush(Color.FromRgb(55, 65, 80)) };
    private readonly TextBlock error = new() { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.OrangeRed, Margin = new(0, 8, 0, 8) };

    internal TuyaSetupWindow(TuyaLamp? existing, string? savedKey)
    {
        Title = existing == null ? "CrimsonHue — add Tuya lamp" : "CrimsonHue — edit Tuya lamp";
        Width = 560; Height = 770; MinWidth = 470; MinHeight = 540;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = true; Background = new SolidColorBrush(Color.FromRgb(16, 21, 29));
        Foreground = Brushes.WhiteSmoke; FontFamily = new FontFamily("Segoe UI"); FontSize = 14;
        var panel = new StackPanel { Margin = new(22) };
        Content = new ScrollViewer { Content = panel, Background = Background, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        panel.Children.Add(new TextBlock { Text = "Tuya Wi-Fi · local protocol 3.5", FontSize = 22, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = "Add a compatible RGB lamp. Hue hardware is optional. Saving does not change the light. Local keys are protected for your Windows account.",
            TextWrapping = TextWrapping.Wrap, Margin = new(0, 10, 0, 16), Foreground = Brushes.LightGray });
        Add(panel, "name", "Lamp name", existing?.Name ?? "Tuya lamp", 100);
        Add(panel, "host", "Lamp IP address or local hostname", existing?.Host ?? "", 253);
        Add(panel, "device", "Tuya device ID", existing?.DeviceId ?? "", 100);
        panel.Children.Add(new TextBlock { Text = savedKey == null ? "Local key" : "Local key · leave blank to keep the saved key" });
        panel.Children.Add(password);
        var advanced = new StackPanel { Margin = new(0, 10, 0, 0) };
        panel.Children.Add(new Expander { Header = "Connection and device profile", Content = advanced, Margin = new(0, 0, 0, 12) });
        Add(advanced, "port", "TCP port", (existing?.Port ?? 6668).ToString(CultureInfo.InvariantCulture), 5);
        Add(advanced, "power", "Power data point", (existing?.PowerDps ?? 20).ToString(CultureInfo.InvariantCulture), 5);
        Add(advanced, "mode", "Mode data point", (existing?.ModeDps ?? 21).ToString(CultureInfo.InvariantCulture), 5);
        Add(advanced, "color", "HSV color data point", (existing?.ColorDps ?? 24).ToString(CultureInfo.InvariantCulture), 5);
        Add(advanced, "white", "White brightness data point", (existing?.WhiteBrightnessDps ?? 22).ToString(CultureInfo.InvariantCulture), 5);
        Add(advanced, "temperature", "White temperature data point", (existing?.ColorTemperatureDps ?? 23).ToString(CultureInfo.InvariantCulture), 5);
        Add(advanced, "scene", "Scene data point", (existing?.SceneDps ?? 25).ToString(CultureInfo.InvariantCulture), 5);
        panel.Children.Add(new TextBlock { Text = "Default profile: power 20, mode 21, HSV color 24 (LEDVANCE / compatible Tuya RGB lamps). Other protocol versions, Zigbee and Bluetooth are not supported. Obtain the device ID and local key from your device's setup; CrimsonHue does not need a cloud account.",
            TextWrapping = TextWrapping.Wrap, Foreground = Brushes.LightGray, Margin = new(0, 5, 0, 10) });
        panel.Children.Add(error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancel", Padding = new(18, 8, 18, 8), Margin = new(0, 0, 10, 0), IsCancel = true };
        var save = new Button { Content = "Save lamp", Padding = new(18, 8, 18, 8), IsDefault = true };
        buttons.Children.Add(cancel); buttons.Children.Add(save); panel.Children.Add(buttons);
        save.Click += (_, _) =>
        {
            try
            {
                var key = password.Password.Length == 0 && savedKey != null ? savedKey : password.Password;
                if (!SettingsStore.ValidTuyaKey(key)) throw new CrimsonHueException("Enter a valid 16, 24 or 32 character local key.");
                var lamp = new TuyaLamp(existing?.Id ?? Guid.NewGuid().ToString("N"), Text("name"), Text("host"), Text("device"),
                    existing?.Position ?? new(0, 1, 0), existing?.Enabled ?? true, Number("port"), "3.5",
                    Number("power"), Number("mode"), Number("color"), Number("white"), Number("temperature"), Number("scene"));
                new TuyaLayout(Devices: [lamp]).Validate();
                Lamp = lamp; LocalKey = key; password.Clear(); DialogResult = true;
            }
            catch (Exception ex) when (ex is CrimsonHueException or FormatException or OverflowException)
            { error.Text = ex is CrimsonHueException ? ex.Message : "Use whole numbers for the port and data points."; }
        };
        Closed += (_, _) => password.Clear();
        string Text(string name) => fields[name].Text.Trim();
        int Number(string name) => int.Parse(Text(name), NumberStyles.None, CultureInfo.InvariantCulture);
    }
    private void Add(Panel panel, string id, string label, string value, int maximum)
    {
        panel.Children.Add(new TextBlock { Text = label });
        var field = new TextBox { Text = value, MaxLength = maximum, Padding = new(7), Margin = new(0, 4, 0, 12) };
        fields.Add(id, field); panel.Children.Add(field);
    }
}
