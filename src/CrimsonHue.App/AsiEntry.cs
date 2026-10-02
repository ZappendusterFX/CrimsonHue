using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace CrimsonHue.App;

/// <summary>Native ASI entry points. All WPF work runs on one dedicated STA.</summary>
public static class AsiEntry
{
    private static App? current;
    private static int started;
    private static int stopping;
    private static byte[] state = "{}"u8.ToArray();
    private static int queuedCommands;
    internal static void PublishState(byte[] bytes) { if (bytes.Length <= 256 * 1024) Volatile.Write(ref state, bytes); }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    public static int ReadState(nint destination, int capacity)
    {
        try { var bytes = Volatile.Read(ref state); if (destination == 0 || capacity < bytes.Length) return -bytes.Length;
            Marshal.Copy(bytes, 0, destination, bytes.Length); return bytes.Length; }
        catch { return 0; }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    public static int SendCommand(nint source, int length)
    {
        if (source == 0 || length is <= 0 or > 16384 || Volatile.Read(ref stopping) != 0) return 0;
        var app = Volatile.Read(ref current);
        if (app == null || app.Dispatcher.HasShutdownStarted) return 0;
        if (Interlocked.Increment(ref queuedCommands) > 64) { Interlocked.Decrement(ref queuedCommands); return 0; }
        try
        {
            var bytes = new byte[length]; Marshal.Copy(source, bytes, 0, length);
            app.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            { try { if (app.MainWindow is MainWindow window) window.OverlayCommand(bytes); }
              finally { Interlocked.Decrement(ref queuedCommands); } }));
            return 1;
        }
        catch { Interlocked.Decrement(ref queuedCommands); return 0; }
    }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void ReadyCallback();

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    public static int Run(nint smokePath, nint readyCallback)
    {
        if (Interlocked.Exchange(ref started, 1) != 0) return -1;
        var result = 0;
        string? path = null;
        try
        {
            path = smokePath == 0 ? null : Marshal.PtrToStringUni(smokePath);
            var ready = Marshal.GetDelegateForFunctionPointer<ReadyCallback>(readyCallback);
            var ui = new Thread(() =>
            {
                try
                {
                    var app = new App(path);
                    Volatile.Write(ref current, app);
                    app.UiReady += () =>
                    {
                        ready();
                        if (Volatile.Read(ref stopping) != 0) app.RequestStop();
                    };
                    app.InitializeComponent();
                    app.Run();
                    if (app.Failed) result = -2;
                }
                catch (Exception ex)
                {
                    result = -3;
                    // Only the synthetic harness receives exception types, never
                    // messages, credentials, bridge responses or live captures.
                    if (path != null)
                        try { File.WriteAllText(path + ".error", ex.GetType().FullName); } catch { }
                }
                finally { Volatile.Write(ref current, null); }
            }) { IsBackground = true, Name = "CrimsonHue ASI UI" };
            ui.SetApartmentState(ApartmentState.STA);
            ui.Start();
            ui.Join();
        }
        catch { return -4; }
        return result;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    public static int RequestStop()
    {
        Interlocked.Exchange(ref stopping, 1);
        return Dispatch(app => app.RequestStop());
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    public static int ToggleWindow() => Dispatch(app => app.ToggleWindow());

    private static int Dispatch(Action<App> action)
    {
        try
        {
            var app = Volatile.Read(ref current);
            if (app == null || app.Dispatcher.HasShutdownStarted) return 0;
            app.Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => action(app)));
            return 1;
        }
        catch { return 0; }
    }
}
