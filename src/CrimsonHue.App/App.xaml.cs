using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace CrimsonHue.App;

public partial class App : Application
{
    private readonly string? smokePath;
    private HwndSource? hotkeyWindow;
    private bool hotkeyRegistered;
    private const int HotkeyId = 0x4348;
    internal event Action? UiReady;
    internal bool Failed { get; private set; }

    public App(string? smokePath = null)
    {
        this.smokePath = smokePath;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += (_, e) =>
        {
            e.Handled = true;
            Failed = true;
            if (MainWindow is MainWindow window) window.ReportHostFailure();
            RequestStop();
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var smoke = smokePath != null;
        var window = new MainWindow(smoke, asi: true);
        MainWindow = window;
        window.Closed += (_, _) => Shutdown(Failed ? 1 : 0);
        // Production has no visible WPF window. Controls remain an internal STA
        // controller while the native ASI draws the UI into the game's backbuffer.
        var overlay = !smoke || Environment.GetEnvironmentVariable("CRIMSONHUE_ASI_OVERLAY_SMOKE") == "1";
        if (overlay)
        {
            window.StartOverlayController();
            UiReady?.Invoke();
            return;
        }
        window.SourceInitialized += (_, _) =>
        {
            hotkeyWindow = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
            hotkeyWindow?.AddHook(HotkeyHook);
            // Ctrl + Shift + F10; MOD_NOREPEAT avoids a held key flickering the UI.
            hotkeyRegistered = !smoke && RegisterHotKey(new WindowInteropHelper(window).Handle, HotkeyId, 0x4006, 0x79);
            window.SetHostHint(smoke || hotkeyRegistered);
        };
        window.Loaded += (_, _) => UiReady?.Invoke();
        if (smoke)
        {
            window.Loaded += async (_, _) =>
            {
                window.EnableSmokePreview();
                window.VerifyDistanceControls();
                await Task.Delay(350);
                window.UpdateLayout();
                var path = Path.GetFullPath(smokePath!); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                Capture(path);
                foreach (var group in new[] { "Ambient", "Color", "Space", "Setup" })
                {
                    window.SelectSmokePanel(group);
                    await Dispatcher.InvokeAsync(window.UpdateLayout, System.Windows.Threading.DispatcherPriority.ContextIdle);
                    Capture(Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "-" + group.ToLowerInvariant() + ".png"));
                }
                window.SelectSmokePanel("Ambient", raw: true);
                await Dispatcher.InvokeAsync(window.UpdateLayout, System.Windows.Threading.DispatcherPriority.ContextIdle);
                Capture(Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "-raw.png"));
                // Verify ASI-specific hide/reopen and close behaviour without
                // credentials, network access or a physical stream.
                ToggleWindow();
                if (window.IsVisible) throw new InvalidOperationException("ASI hide failed.");
                ToggleWindow();
                if (!window.IsVisible) throw new InvalidOperationException("ASI reopen failed.");
                window.Close();
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
                if (window.IsVisible) throw new InvalidOperationException("ASI close did not hide the window.");
                ToggleWindow();
                RequestStop();
                void Capture(string destination)
                {
                    var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(window);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var file = File.Create(destination); encoder.Save(file);
                }
            };
        }
        window.Show();
    }

    internal void ToggleWindow()
    {
        if (MainWindow is not MainWindow window) return;
        if (window.IsVisible && window.WindowState != WindowState.Minimized) window.HideSettings();
        else { window.Show(); window.WindowState = WindowState.Normal; window.Activate(); }
    }

    internal void RequestStop()
    {
        if (MainWindow is MainWindow window) window.RequestHostShutdown();
        else Shutdown();
    }

    private nint HotkeyHook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x0312 && wParam == HotkeyId)
        {
            GetWindowThreadProcessId(GetForegroundWindow(), out var process);
            if (process == Environment.ProcessId) ToggleWindow();
            handled = true;
        }
        return 0;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (hotkeyWindow != null)
        {
            if (hotkeyRegistered) UnregisterHotKey(hotkeyWindow.Handle, HotkeyId);
            hotkeyWindow.RemoveHook(HotkeyHook);
        }
        base.OnExit(e);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint window, int id);
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint process);
}
