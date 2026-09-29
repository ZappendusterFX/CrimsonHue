using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CrimsonHue.Core;
using Microsoft.Win32;

namespace CrimsonHue.App;

public partial class MainWindow
{
    private static readonly (string Name, PresetGroups Flag)[] presetSections =
    [
        ("Output", PresetGroups.Output), ("Ambient", PresetGroups.Ambient),
        ("Color", PresetGroups.Color), ("Space", PresetGroups.Space)
    ];

    private void ExportPresetClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export CrimsonHue mixer preset",
            FileName = "CrimsonHue-preset.json",
            DefaultExt = ".json",
            Filter = "CrimsonHue preset (*.json)|*.json",
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var name = Path.GetFileNameWithoutExtension(dialog.FileName);
            var json = MappingPreset.Export(name, mapping);
            File.WriteAllText(dialog.FileName, json, new UTF8Encoding(false));
            TuningStatus.Text = $"Preset exported: {parameters.Length + switches.Length} mixer controls only · no bridge or account data";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CrimsonHueException or ArgumentException)
        {
            ShowPresetError(ex.Message);
        }
    }

    private void ImportPresetClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import CrimsonHue mixer preset",
            DefaultExt = ".json",
            Filter = "CrimsonHue preset (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var file = new FileInfo(dialog.FileName);
            if (file.Length > MappingPreset.MaximumBytes)
                throw new CrimsonHueException("Preset file is too large.");
            var preset = MappingPreset.Import(File.ReadAllBytes(dialog.FileName));
            var sections = PreviewPresetImport(preset);
            if (sections is null) return;
            var next = preset.ApplyTo(mapping, sections.Value);
            // Persist before changing live output, so a failed save leaves the current mix intact.
            store.SaveSettings(new(BridgeAddress.Text.Trim(), TelemetryAddress.Text.Trim(),
                SelectedArea?.Id ?? selectedAreaId, next));
            Volatile.Write(ref mapping, next);
            SyncTuningValues();
            TuningStatus.Text = $"Imported “{preset.Name}” ({SectionNames(sections.Value)}) · saved";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CrimsonHueException or ArgumentException)
        {
            ShowPresetError(ex.Message);
        }
    }

    private PresetGroups? PreviewPresetImport(MappingPreset preset)
    {
        var window = new Window
        {
            Title = "Import CrimsonHue preset",
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Width = 660,
            Height = 600,
            MinWidth = 540,
            MinHeight = 470,
            Background = new SolidColorBrush(Color.FromRgb(23, 26, 34)),
            Foreground = Brushes.White,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13
        };
        var root = new Grid { Margin = new Thickness(20) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var title = new TextBlock
        {
            Text = preset.Name, FontSize = 23, FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        root.Children.Add(title);
        var explanation = new TextBlock
        {
            Text = "Choose the sections to apply. Space depends on your room and is off by default. " +
                "Changes affect active lighting immediately and are saved; bridge setup and credentials stay yours.",
            Foreground = new SolidColorBrush(Color.FromRgb(167, 173, 188)),
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 16)
        };
        Grid.SetRow(explanation, 1); root.Children.Add(explanation);

        var sectionPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };
        var choices = new Dictionary<PresetGroups, CheckBox>();
        foreach (var section in presetSections)
        {
            var box = new CheckBox
            {
                Content = section.Name, IsChecked = section.Flag != PresetGroups.Space,
                FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 22, 8)
            };
            choices.Add(section.Flag, box);
            sectionPanel.Children.Add(box);
        }
        Grid.SetRow(sectionPanel, 2); root.Children.Add(sectionPanel);

        var preview = new TextBox
        {
            IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"), FontSize = 12,
            Padding = new Thickness(12), VerticalContentAlignment = VerticalAlignment.Top
        };
        Grid.SetRow(preview, 3); root.Children.Add(preview);

        var footer = new DockPanel { Margin = new Thickness(0, 15, 0, 0) };
        var summary = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromRgb(117, 208, 193)),
            VerticalAlignment = VerticalAlignment.Center
        };
        footer.Children.Add(summary);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        DockPanel.SetDock(buttons, Dock.Right);
        var cancel = new Button { Content = "Cancel", MinWidth = 95, Margin = new Thickness(0, 0, 8, 0) };
        var apply = new Button
        {
            Content = "Apply selected", MinWidth = 130,
            Background = new SolidColorBrush(Color.FromRgb(188, 53, 86))
        };
        buttons.Children.Add(cancel); buttons.Children.Add(apply);
        footer.Children.Add(buttons);
        Grid.SetRow(footer, 4); root.Children.Add(footer);
        window.Content = root;

        PresetGroups Selected() => choices.Where(c => c.Value.IsChecked == true)
            .Aggregate(PresetGroups.None, (current, choice) => current | choice.Key);
        void Refresh()
        {
            var groups = Selected();
            var changed = DescribePresetChanges(mapping, preset.Mapping, groups);
            preview.Text = changed.Count == 0 ? "No mixer values differ in the selected sections." :
                string.Join(Environment.NewLine, changed);
            summary.Text = $"{changed.Count} control(s) will change";
            apply.IsEnabled = groups != PresetGroups.None && changed.Count > 0;
        }
        foreach (var box in choices.Values)
        {
            box.Checked += (_, _) => Refresh();
            box.Unchecked += (_, _) => Refresh();
        }
        PresetGroups selected = PresetGroups.None;
        cancel.Click += (_, _) => window.DialogResult = false;
        apply.Click += (_, _) => { selected = Selected(); window.DialogResult = true; };
        Refresh();
        return window.ShowDialog() == true ? selected : null;
    }

    private static List<string> DescribePresetChanges(MappingSettings current, MappingSettings incoming, PresetGroups groups)
    {
        var changes = new List<string>();
        foreach (var section in presetSections)
        {
            if (!groups.HasFlag(section.Flag)) continue;
            foreach (var parameter in parameters.Where(p => p.Group == section.Name))
            {
                if (parameter.Get(current) == parameter.Get(incoming)) continue;
                var unit = parameter.Unit is "%" or "°" ? parameter.Unit :
                    parameter.Unit.Length == 0 ? "" : " " + parameter.Unit;
                string Display(double value) =>
                    (value * parameter.Scale).ToString("0.######", CultureInfo.CurrentCulture) + unit;
                changes.Add($"{section.Name} · {parameter.Title}: {Display(parameter.Get(current))} → {Display(parameter.Get(incoming))}");
            }
            foreach (var flag in switches.Where(p => p.Group == section.Name))
            {
                if (flag.Get(current) != flag.Get(incoming))
                    changes.Add($"{section.Name} · {flag.Title}: {(flag.Get(current) ? "On" : "Off")} → {(flag.Get(incoming) ? "On" : "Off")}");
            }
        }
        return changes;
    }

    private static string SectionNames(PresetGroups groups) => string.Join(", ",
        presetSections.Where(s => groups.HasFlag(s.Flag)).Select(s => s.Name));

    private void ShowPresetError(string message)
    {
        TuningStatus.Text = "Preset was not imported or exported";
        MessageBox.Show(this, message, "CrimsonHue preset", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
