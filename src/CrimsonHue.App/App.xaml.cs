using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CrimsonHue.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var smoke = e.Args.Length == 2 && e.Args[0] == "--ui-smoke";
        var window = new MainWindow(smoke);
        MainWindow = window;
        if (smoke)
        {
            window.Loaded += async (_, _) =>
            {
                window.EnableSmokePreview();
                window.VerifyDistanceControls();
                await Task.Delay(350);
                window.UpdateLayout();
                var path = Path.GetFullPath(e.Args[1]); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
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
                window.Close();
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
}
