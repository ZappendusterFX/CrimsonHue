using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CrimsonHue.App;

/// <summary>A slider and precise numeric entry for the same visible setting.</summary>
public sealed class TuningControl : Border
{
    private readonly Slider slider;
    private readonly TextBox number;
    private readonly double initial;
    private bool syncing;
    public string Key { get; }
    public double Value => slider.Value;
    public event Action<TuningControl, double>? ValueCommitted;

    public TuningControl(string key, string title, string description, string unit,
        double minimum, double maximum, double step, double initial)
    {
        Key = key;
        this.initial = initial;
        Padding = new Thickness(0, 10, 0, 10);
        BorderBrush = new SolidColorBrush(Color.FromRgb(44, 49, 63));
        BorderThickness = new Thickness(0, 0, 0, 1);
        var panel = new StackPanel();
        var row = new Grid();
        row.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new() { Width = new GridLength(78) });
        row.ColumnDefinitions.Add(new() { Width = new GridLength(31) });
        var label = new TextBlock
        {
            Text = title + (unit.Length == 0 ? "" : " · " + unit), TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0)
        };
        number = new TextBox
        {
            Padding = new Thickness(5, 3, 5, 3), FontFamily = new FontFamily("Consolas"),
            HorizontalContentAlignment = HorizontalAlignment.Right,
            ToolTip = $"{minimum} to {maximum} {unit}. Enter applies; Escape restores."
        };
        System.Windows.Automation.AutomationProperties.SetName(number, title + " numeric value");
        Grid.SetColumn(number, 1);
        var reset = new Button { Content = "↺", Padding = new Thickness(0), Margin = new Thickness(5, 0, 0, 0), ToolTip = $"Reset to {initial} {unit}" };
        Grid.SetColumn(reset, 2);
        row.Children.Add(label); row.Children.Add(number); row.Children.Add(reset);
        panel.Children.Add(row);
        slider = new Slider
        {
            Minimum = minimum, Maximum = maximum, SmallChange = step, LargeChange = step * 10,
            TickFrequency = step, IsSnapToTickEnabled = true, IsMoveToPointEnabled = true, Margin = new Thickness(0, 5, 0, 2)
        };
        System.Windows.Automation.AutomationProperties.SetName(slider, title);
        panel.Children.Add(slider);
        panel.Children.Add(new TextBlock
        {
            Text = description, FontSize = 11, TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(153, 164, 185))
        });
        Child = panel;
        slider.ValueChanged += (_, _) =>
        {
            if (syncing) return;
            SetDisplayValue(slider.Value);
            ValueCommitted?.Invoke(this, slider.Value);
        };
        reset.Click += (_, _) => SetUserValue(this.initial);
        number.LostKeyboardFocus += (_, _) => CommitNumber();
        number.KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter) { CommitNumber(); e.Handled = true; }
            if (e.Key == System.Windows.Input.Key.Escape) { SetDisplayValue(Value); e.Handled = true; }
        };
        SetDisplayValue(initial);
    }

    public void SetDisplayValue(double value)
    {
        syncing = true;
        try
        {
            slider.Value = value;
            number.Text = value.ToString("G", CultureInfo.CurrentCulture);
            number.ClearValue(TextBox.BorderBrushProperty);
        }
        finally { syncing = false; }
    }

    public void SetUserValue(double value)
    {
        if (!double.IsFinite(value) || value < slider.Minimum || value > slider.Maximum)
            throw new ArgumentOutOfRangeException(nameof(value));
        SetDisplayValue(value);
        ValueCommitted?.Invoke(this, value);
    }

    private bool CommitNumber()
    {
        var valid = double.TryParse(number.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) ||
            double.TryParse(number.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        if (!valid || !double.IsFinite(value) || value < slider.Minimum || value > slider.Maximum)
        {
            number.BorderBrush = new SolidColorBrush(Color.FromRgb(238, 87, 117));
            return false;
        }
        SetUserValue(value);
        return true;
    }

    internal bool VerifyNumericInput(string text)
    {
        number.Text = text;
        return CommitNumber();
    }
}
