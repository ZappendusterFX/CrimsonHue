using CrimsonHue.Core;

namespace CrimsonHue.App;

public partial class MainWindow
{
    private OpenRgbStreamingSession? openRgbSession;
    private static bool OpenRgbPreviewBuild
    {
        get
        {
#if OPENRGB_PREVIEW
            return true;
#else
            return false;
#endif
        }
    }
    private bool OpenRgbPreviewEnabled => OpenRgbPreviewBuild && !smoke;

    private string WithOpenRgbStatus(string hueStatus) => !OpenRgbPreviewEnabled ? hueStatus :
        hueStatus + " | " + (openRgbSession?.State.Status ?? "OpenRGB preview · front · max 25% · starts with sync");

    private async Task RunLightingAsync(StreamingSession hue, EntertainmentArea area, CancellationToken token)
    {
        using var combined = CancellationTokenSource.CreateLinkedTokenSource(token);
        var rgb = openRgbSession;
        var rgbTask = rgb == null ? Task.CompletedTask :
            rgb.RunAsync(telemetry, ambient, () => Volatile.Read(ref mapping), combined.Token);
        try
        {
            await hue.RunAsync(area, telemetry, () => Volatile.Read(ref mapping), token,
                ambient, () => Volatile.Read(ref wallWash));
        }
        finally
        {
            await combined.CancelAsync();
            await rgbTask;
        }
    }
}
