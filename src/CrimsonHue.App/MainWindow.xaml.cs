using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CrimsonHue.Core;

namespace CrimsonHue.App;

public partial class MainWindow : Window
{
    private readonly bool smoke;
    private readonly SettingsStore store = new();
    private readonly TelemetryState telemetry = new();
    private readonly LightMapper previewMapper = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly Stopwatch uptime = Stopwatch.StartNew();
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? telemetryCancel;
    private Task? telemetryTask;
    private CancellationTokenSource? streamCancel;
    private Task? streamTask;
    private StreamingSession? session;
    private BridgeClient? bridge;
    private BridgeProbe? probe;
    private MappingSettings mapping = new();
    private string? selectedAreaId;
    private bool initialized;
    private bool updatingSettings;
    private bool busy;
    private bool closing;
    private bool allowClose;
    private EntertainmentArea? SelectedArea => AreaBox.SelectedItem as EntertainmentArea;

    public MainWindow(bool smoke = false)
    {
        this.smoke = smoke;
        InitializeComponent();
        if (!smoke)
        {
            try
            {
                var saved = store.LoadSettings();
                BridgeAddress.Text = saved.BridgeAddress;
                TelemetryAddress.Text = saved.TelemetryAddress;
                selectedAreaId = saved.AreaId;
                var m = saved.Mapping ?? new(); LightMapper.Validate(m);
                BrightnessSlider.Value = m.Brightness; GainSlider.Value = m.Gain;
                FadeEndSlider.Maximum = Math.Max(100, m.FadeEnd);
                FadeStartSlider.Maximum = FadeEndSlider.Maximum - 0.5;
                FadeEndSlider.Value = m.FadeEnd; FadeStartSlider.Value = m.FadeStart;
                SpreadSlider.Value = m.Spread; SmoothingSlider.Value = m.SmoothingMs;
                var credentials = store.LoadCredentials();
                if (credentials != null)
                {
                    bridge = new(credentials); BridgeAddress.Text = new Uri(credentials.Address).Host;
                    BridgeStatus.Text = "Paired · credentials protected by Windows";
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or CryptographicException or CrimsonHueException or ArgumentException)
            { BridgeStatus.Text = "Saved setup could not be loaded. Find and pair your bridge again."; }
        }
        initialized = true;
        SettingsChanged(this, null!);
        timer.Tick += (_, _) => RefreshPreview();
        timer.Start();
        Loaded += async (_, _) =>
        {
            if (smoke) return;
            await GuardAsync(async () => { await ConnectTelemetryAsync(); if (bridge != null) await LoadAreasAsync(); });
        };
    }
    private async Task GuardAsync(Func<Task> action)
    {
        if (busy || closing) return;
        busy = true; UpdateButtons();
        try { await action(); }
        catch (OperationCanceledException) when (closing) { }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or CrimsonHueException or CryptographicException or InvalidOperationException or FormatException or KeyNotFoundException or OperationCanceledException)
        { StreamStatus.Text = FriendlyError(ex); }
        finally { busy = false; UpdateButtons(); }
    }
    private static string FriendlyError(Exception ex) => ex switch
    {
        CrimsonHueException => ex.Message,
        HttpRequestException => "Cannot reach the bridge securely. Check its IP and HTTPS service; use Find bridge again if its certificate changed.",
        OperationCanceledException => "The bridge did not respond in time. Check its address and try again.",
        CryptographicException => "Windows could not save or unlock the bridge credentials for this account.",
        _ => "The operation failed. Check the bridge connection and configuration, then try again."
    };
    private async void ProbeClick(object sender, RoutedEventArgs e) => await GuardAsync(async () =>
    {
        BridgeStatus.Text = "Looking for bridge…";
        probe = null; PairPanel.Visibility = Visibility.Collapsed;
        probe = await BridgeClient.ProbeAsync(BridgeAddress.Text.Trim(), lifetime.Token);
        BridgeStatus.Text = $"Found {probe.Name}\nBridge ID: {probe.BridgeId}";
        CertificateLabel.Text = "Certificate SHA-256: " + probe.CertificateSha256;
        PairPanel.Visibility = Visibility.Visible;
        StreamStatus.Text = "Bridge found. Press its link button, then pair CrimsonHue.";
    });
    private async void PairClick(object sender, RoutedEventArgs e) => await GuardAsync(async () =>
    {
        if (probe == null || BridgeClient.NormalizeAddress(BridgeAddress.Text.Trim()) != probe.Address)
            throw new CrimsonHueException("Find this bridge before pairing it.");
        var credentials = await BridgeClient.PairAsync(probe, lifetime.Token);
        store.SaveCredentials(credentials);
        bridge?.Dispose(); bridge = new(credentials);
        BridgeStatus.Text = "Paired · credentials protected by Windows";
        PairPanel.Visibility = Visibility.Collapsed;
        SaveSettings();
        await LoadAreasAsync();
        StreamStatus.Text = "Bridge paired. Select your area, then start lighting sync.";
    });
    private async void RefreshClick(object sender, RoutedEventArgs e) => await GuardAsync(LoadAreasAsync);
    private async Task LoadAreasAsync()
    {
        if (bridge == null) return;
        var previous = SelectedArea?.Id ?? selectedAreaId;
        var areas = await bridge.GetAreasAsync(lifetime.Token);
        AreaBox.ItemsSource = areas;
        AreaBox.SelectedItem = areas.FirstOrDefault(a => a.Id == previous) ?? areas.FirstOrDefault();
        if (areas.Count == 0) LayoutInfo.Text = "No areas found. Create one in the Hue app, then refresh.";
        RefreshButton.IsEnabled = true;
    }
    private void AreaChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!initialized) return;
        selectedAreaId = SelectedArea?.Id;
        previewMapper.Reset();
        if (SelectedArea is { } area)
        {
            var unique = area.Channels.Select(c => c.Position).Distinct().Count();
            LayoutInfo.Text = $"{area.LampCount} lights · {area.Channels.Count} channels · {unique} positions" +
                (unique < area.Channels.Count ? "\nSome channels share a position." : "") + (area.Active ? "\nCurrently used by a sync app." : "");
        }
        UpdateButtons();
    }
    private async void ReconnectClick(object sender, RoutedEventArgs e) => await GuardAsync(ConnectTelemetryAsync);
    private async Task ConnectTelemetryAsync()
    {
        var uri = TelemetryClient.ValidateEndpoint(TelemetryAddress.Text.Trim());
        if (telemetryCancel != null) { await telemetryCancel.CancelAsync(); if (telemetryTask != null) await telemetryTask; telemetryCancel.Dispose(); }
        telemetryCancel = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        telemetryTask = new TelemetryClient(telemetry).RunAsync(uri, telemetryCancel.Token);
    }
    private async void StartClick(object sender, RoutedEventArgs e)
    {
        if (streamTask != null || bridge == null || SelectedArea == null || DemoCheck.IsChecked == true) return;
        try { SaveSettings(); }
        catch (IOException) { StreamStatus.Text = "Could not save settings. Check your profile's write permissions."; return; }
        streamCancel = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        session = new StreamingSession(bridge);
        var activeSession = session;
        var area = SelectedArea;
        streamTask = Task.Run(() => activeSession.RunAsync(area, telemetry, () => Volatile.Read(ref mapping), streamCancel.Token));
        UpdateButtons();
        try { await streamTask; StreamStatus.Text = "Stopped · previous light states restored when still owned"; }
        catch (Exception ex) { StreamStatus.Text = FriendlyError(ex); }
        finally
        {
            streamTask = null; session = null; streamCancel.Dispose(); streamCancel = null;
            previewMapper.Reset(); UpdateButtons();
        }
    }
    private async void StopClick(object sender, RoutedEventArgs e)
    {
        if (streamCancel == null) return;
        StreamStatus.Text = "Stopping and restoring light states…";
        await streamCancel.CancelAsync();
    }
    private void SettingsChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!initialized || updatingSettings) return;
        updatingSettings = true;
        try
        {
            if (FadeStartSlider.Value >= FadeEndSlider.Value)
            {
                if (ReferenceEquals(sender, FadeStartSlider)) FadeEndSlider.Value = FadeStartSlider.Value + 0.5;
                else FadeStartSlider.Value = Math.Max(0, FadeEndSlider.Value - 0.5);
            }
            var next = new MappingSettings(GainSlider.Value, BrightnessSlider.Value, FadeEndSlider.Value, SpreadSlider.Value, SmoothingSlider.Value, FadeStartSlider.Value);
            LightMapper.Validate(next);
            Volatile.Write(ref mapping, next);
        }
        finally { updatingSettings = false; }
        BrightnessValue.Text = $"{mapping.Brightness:P0}";
        GainValue.Text = $"{mapping.Gain:F2}×";
        FadeStartValue.Text = $"{mapping.FadeStart:F1}";
        FadeEndValue.Text = $"{mapping.FadeEnd:F1}";
    }
    private void DemoChanged(object sender, RoutedEventArgs e) { if (initialized) { previewMapper.Reset(); UpdateButtons(); } }
    private void RefreshPreview()
    {
        var frame = telemetry.Read(out var message);
        TelemetryStatus.Text = message;
        TelemetryStatus.Foreground = frame == null ? new SolidColorBrush(Color.FromRgb(175, 182, 198)) : new SolidColorBrush(Color.FromRgb(145, 215, 185));
        CaptureInfo.Text = frame == null ? "Requires fresh CDT light capture. For all-around lighting, enable upstream lights and source visibility." :
            $"{frame.Feed} · {frame.Lights.Count}/{frame.SourceCount} usable lights · {frame.Lights.Count(l => (l.Position - frame.Player).Length < mapping.FadeEnd)} in range · capture {frame.CaptureSequence} · age {Math.Max(0, (DateTimeOffset.UtcNow - frame.LightCapturedAt).TotalMilliseconds):F0} ms";
        var demo = DemoCheck.IsChecked == true;
        var area = demo ? DemoData.Area : SelectedArea;
        if (demo) frame = DemoData.Frame(uptime.Elapsed.TotalSeconds);
        IReadOnlyList<ChannelColor> colors = [];
        if (area != null)
        {
            if (session != null) colors = session.Colors;
            else if (frame != null) colors = previewMapper.Map(frame, area, mapping, 0.1);
            else previewMapper.Reset();
        }
        Room.Area = area; Room.Colors = colors; Room.Frame = frame; Room.Demo = demo; Room.Settings = mapping; Room.InvalidateVisual();
        ChannelCards.ItemsSource = area?.Channels.Select(c => new
        {
            Name = c.Label,
            Subtitle = $"CH {c.Id:D2}  ·  height {c.Position.Z:+0.00;-0.00;0.00}",
            Details = $"{c.Label}\nHue position: {c.Position}\n{c.Members.Count} member(s)",
            Color = new SolidColorBrush(RoomView.ColorFrom(colors.FirstOrDefault(v => v.Id == c.Id)?.Rgb ?? default))
        }).ToArray();
        if (session != null && streamCancel?.IsCancellationRequested != true) StreamStatus.Text = session.Status;
    }
    private void UpdateButtons()
    {
        if (!initialized) return;
        var running = streamTask != null;
        StartButton.IsEnabled = !busy && !running && bridge != null && SelectedArea?.Channels.Count > 0 && DemoCheck.IsChecked != true;
        StopButton.IsEnabled = running;
        FindButton.IsEnabled = PairButton.IsEnabled = !busy && !running;
        RefreshButton.IsEnabled = !busy && !running && bridge != null;
        AreaBox.IsEnabled = BridgeAddress.IsEnabled = DemoCheck.IsEnabled = !running && !busy;
    }
    private void OpenBridgeClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var uri = new Uri(BridgeClient.NormalizeAddress(BridgeAddress.Text.Trim()));
            // Public setup page, no credentials in the URL.
            Process.Start(new ProcessStartInfo("http://" + uri.Host) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is CrimsonHueException or Win32Exception) { StreamStatus.Text = ex.Message; }
    }
    private void SaveSettings()
    {
        if (!smoke) store.SaveSettings(new(BridgeAddress.Text.Trim(), TelemetryAddress.Text.Trim(), SelectedArea?.Id, mapping));
    }
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (allowClose) return;
        e.Cancel = true;
        if (closing) return;
        closing = true; timer.Stop();
        try
        {
            await lifetime.CancelAsync();
            if (streamTask != null) await streamTask;
            if (telemetryTask != null) await telemetryTask;
            SaveSettings();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (!smoke) MessageBox.Show(this, FriendlyError(ex), "CrimsonHue", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        finally { bridge?.Dispose(); lifetime.Dispose(); allowClose = true; _ = Dispatcher.BeginInvoke(new Action(Close)); }
    }
    public void EnableSmokePreview()
    {
        DemoCheck.IsChecked = true;
        BridgeStatus.Text = "Demo preview · no bridge connected";
        StreamStatus.Text = "Demo preview · physical lamp output disabled";
        RefreshPreview();
    }
    internal void VerifyDistanceControls()
    {
        if (!smoke) throw new InvalidOperationException("Control self-test requires isolated smoke mode.");
        var start = FadeStartSlider.Value; var end = FadeEndSlider.Value;
        try
        {
            FadeStartSlider.Value = 45;
            if (mapping.FadeStart != 45 || mapping.FadeEnd != 45.5) throw new InvalidOperationException("Fade-end control did not follow start.");
            FadeEndSlider.Value = 2;
            if (mapping.FadeStart != 1.5 || mapping.FadeEnd != 2) throw new InvalidOperationException("Fade-start control did not follow end.");
            FadeStartSlider.Value = 99.5;
            if (mapping.FadeEnd != 100) throw new InvalidOperationException("Upper fade limit is inconsistent.");
            FadeEndSlider.Value = 1;
            if (mapping.FadeStart != 0.5) throw new InvalidOperationException("Lower fade limit is inconsistent.");
            LightMapper.Validate(mapping);
        }
        finally { FadeEndSlider.Value = end; FadeStartSlider.Value = start; }
    }
}
