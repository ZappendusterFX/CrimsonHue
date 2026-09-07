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
                await Task.Delay(350);
                window.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(window);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                var path = Path.GetFullPath(e.Args[1]); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using (var file = File.Create(path)) encoder.Save(file);
                window.Close();
            };
        }
        window.Show();
    }
}
