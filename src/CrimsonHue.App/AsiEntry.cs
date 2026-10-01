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
