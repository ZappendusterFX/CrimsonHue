using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using CrimsonHue.Core;

namespace CrimsonHue.App;

// Reuse the existing UI controller on its STA without ever showing its WPF
// window. The native renderer receives a bounded, credential-free UI snapshot.
public partial class MainWindow
{
    private MappingPreset? overlayPreset;
    private bool overlayController;
    private static readonly JsonSerializerOptions OverlayJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    internal void StartOverlayController()
    {
        overlayController = true;
        if (smoke) { EnableSmokePreview(); VerifyDistanceControls(); }
        else _ = GuardAsync(async () => { await ConnectTelemetryAsync(); if (bridge != null) await LoadAreasAsync(); });
        PublishOverlay();
    }

    internal void PublishOverlay()
    {
        if (!overlayController || closing) return;
        var area = Room.Area;
        var snapshot = new
        {
            ready = true, demo = DemoCheck.IsChecked == true, frozen = FreezeRawCheck.IsChecked == true,
            smoke, windowVisible = IsVisible,
            telemetry = TelemetryStatus.Text, capture = CaptureInfo.Text, ambient = AmbientStatus.Text,
            stream = StreamStatus.Text, tuning = TuningStatus.Text, bridgeStatus = BridgeStatus.Text,
            certificate = CertificateLabel.Text, pairing = PairPanel.Visibility == Visibility.Visible,
            layout = LayoutInfo.Text, boundary = SpatialBoundaryStatus.Text,
            leftRight = mapping.SeparateLeftRight, frontRear = mapping.SeparateFrontRear,
            curveEnd = mapping.FadeEnd,
            curve = Enumerable.Range(0, 101).Select(i => LightMapper.DistanceWeight(mapping.FadeEnd * i / 100, mapping)),
            bridgeAddress = BridgeAddress.Text, telemetryAddress = TelemetryAddress.Text,
            canStart = StartButton.IsEnabled, canStop = StopButton.IsEnabled,
            canSetup = !busy && streamTask == null, canRefresh = RefreshButton.IsEnabled,
            selectedArea = SelectedArea?.Id ?? "",
            areas = (AreaBox.ItemsSource as IReadOnlyList<EntertainmentArea> ?? []).Select(a => new { a.Id, a.Name, a.Active }),
            controls = parameters.Select(p => new { p.Key, p.Group, p.Title, p.Unit, p.Description,
                p.Min, p.Max, p.Step, value = p.Get(mapping) * p.Scale, initial = p.Get(defaults) * p.Scale }),
            switches = switches.Select(p => new { p.Key, p.Group, p.Title, p.Description, value = p.Get(mapping) }),
            channels = (area?.Channels ?? []).Select(c => new { c.Id, c.Label, x = c.Position.X, y = c.Position.Y, z = c.Position.Z,
                rgb = Room.Colors.FirstOrDefault(v => v.Id == c.Id)?.Rgb ?? default }),
            sources = (Room.Frame?.Lights ?? []).Take(512).Select(l => new { direction = LightMapper.SourceDirection(l.Position, Room.Frame!, mapping),
                fade = LightMapper.DistanceWeight((l.Position - Room.Frame!.Player).Length, mapping) }),
            rawAmbient = RawAmbientValues.Text, rawSummary = RawSourceSummary.Text,
            rawLights = (RawLightsGrid.ItemsSource as System.Collections.IEnumerable)?.Cast<object>().Take(512),
            presetName = overlayPreset?.Name ?? "", presetChanges = overlayPreset == null ? [] :
                DescribePresetChanges(mapping, overlayPreset.Mapping, PresetGroups.All)
        };
        AsiEntry.PublishState(JsonSerializer.SerializeToUtf8Bytes(snapshot, OverlayJson));
    }

    internal void OverlayCommand(byte[] bytes)
    {
        if (closing) return;
        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });
            var command = document.RootElement;
            var action = command.GetProperty("action").GetString();
            string Text(string key) => command.GetProperty(key).GetString() ?? "";
            switch (action)
            {
                case "set":
                    var parameter = parameters.First(p => p.Key == Text("key"));
                    tuningControls[parameter.Key].SetUserValue(command.GetProperty("value").GetDouble()); break;
                case "switch":
                    var flag = switches.First(p => p.Key == Text("key"));
                    ApplySwitch(flag, command.GetProperty("value").GetBoolean()); break;
                case "reset":
                    MixerTabs.SelectedItem = MixerTabs.Items.Cast<TabItem>().First(t => (string)t.Tag == Text("group"));
                    ResetTabClick(this, new()); break;
                case "save": SaveTuningClick(this, new()); break;
                case "demo":
                    if (streamTask == null) DemoCheck.IsChecked = smoke || command.GetProperty("value").GetBoolean(); break;
                case "freeze": FreezeRawCheck.IsChecked = command.GetProperty("value").GetBoolean(); break;
                case "setup":
                    if (busy || streamTask != null) break;
                    var address = Text("bridge"); var endpoint = Text("telemetry");
                    _ = BridgeClient.NormalizeAddress(address); _ = TelemetryClient.ValidateEndpoint(endpoint);
                    BridgeAddress.Text = address; TelemetryAddress.Text = endpoint; break;
                case "find": if (!smoke && FindButton.IsEnabled) ProbeClick(this, new()); break;
                case "pair": if (!smoke && !busy && streamTask == null) PairClick(this, new()); break;
                case "refresh": if (!smoke && RefreshButton.IsEnabled) RefreshClick(this, new()); break;
                case "reconnect": if (!smoke && !busy) ReconnectClick(this, new()); break;
                case "area":
                    if (!busy && streamTask == null && AreaBox.ItemsSource is IReadOnlyList<EntertainmentArea> areas)
                        AreaBox.SelectedItem = areas.First(a => a.Id == Text("id")); break;
                case "start": if (!smoke && StartButton.IsEnabled) StartClick(this, new()); break;
                case "stop": StopClick(this, new()); break;
                case "close":
                    StopClick(this, new()); SaveSettings(); break;
                case "export":
                    if (smoke) { TuningStatus.Text = "Synthetic mode does not write presets"; break; }
                    var path = PresetPath(Text("path"));
                    if (File.Exists(path) && !command.GetProperty("overwrite").GetBoolean())
                        throw new CrimsonHueException("Preset already exists; confirm overwrite first.");
                    File.WriteAllText(path, MappingPreset.Export(Path.GetFileNameWithoutExtension(path), mapping), new UTF8Encoding(false));
                    TuningStatus.Text = "Preset exported · mixer controls only"; break;
                case "import":
                    if (smoke) { TuningStatus.Text = "Synthetic mode does not read presets"; break; }
                    var input = new FileInfo(PresetPath(Text("path")));
                    if (input.Length > MappingPreset.MaximumBytes) throw new CrimsonHueException("Preset file is too large.");
                    overlayPreset = MappingPreset.Import(File.ReadAllBytes(input.FullName)); break;
                case "cancelImport": overlayPreset = null; break;
                case "applyImport":
                    if (overlayPreset == null) break;
                    var groups = (PresetGroups)command.GetProperty("groups").GetInt32();
                    var next = overlayPreset.ApplyTo(mapping, groups);
                    if (!smoke) store.SaveSettings(new(BridgeAddress.Text.Trim(), TelemetryAddress.Text.Trim(), SelectedArea?.Id ?? selectedAreaId, next));
                    Volatile.Write(ref mapping, next); SyncTuningValues();
                    TuningStatus.Text = $"Imported {overlayPreset.Name} ({SectionNames(groups)}) · saved";
                    overlayPreset = null; break;
                default: throw new CrimsonHueException("Unknown overlay action.");
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Do not forward exception messages that might expose file contents,
            // endpoint credentials, device responses or protected settings.
            TuningStatus.Text = ex is CrimsonHueException ? ex.Message : "Action failed. Check the selected value, setup or preset path.";
        }
        RefreshPreview(); PublishOverlay();
    }

    private static string PresetPath(string input)
    {
        if (input.Length > 2048 || !Path.IsPathFullyQualified(input) || !input.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            throw new CrimsonHueException("Enter an absolute path to a .json preset.");
        var path = Path.GetFullPath(input);
        var storeRoot = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrimsonHue"));
        var relative = Path.GetRelativePath(storeRoot, path);
        if (!relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative))
            throw new CrimsonHueException("Choose a preset outside CrimsonHue's protected settings/cache folder.");
        return path;
    }
}
