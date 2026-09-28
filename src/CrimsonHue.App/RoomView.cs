using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CrimsonHue.Core;

namespace CrimsonHue.App;

public sealed class RoomView : FrameworkElement
{
    public EntertainmentArea? Area { get; set; }
    public IReadOnlyList<ChannelColor> Colors { get; set; } = [];
    public TelemetryFrame? Frame { get; set; }
    public bool Demo { get; set; }
    public MappingSettings Settings { get; set; } = new();
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var w = ActualWidth; var h = ActualHeight;
        var center = new Point(w / 2, h / 2 + 8);
        var sx = Math.Min(w * 0.40, h * 0.53); var sy = h * 0.34;
        var line = new Pen(Brush("#293040"), 1);
        dc.DrawRoundedRectangle(Brush("#171D28"), line, new Rect(center.X - sx, center.Y - sy, sx * 2, sy * 2), 18, 18);
        for (var i = -2; i <= 2; i++)
        {
            var x = center.X + i * sx / 3; var y = center.Y + i * sy / 3;
            dc.DrawLine(new Pen(Brush("#202735"), 1), new Point(x, center.Y - sy + 12), new Point(x, center.Y + sy - 12));
            dc.DrawLine(new Pen(Brush("#202735"), 1), new Point(center.X - sx + 12, y), new Point(center.X + sx - 12, y));
        }
        dc.DrawRoundedRectangle(Brush("#4B566C"), null, new Rect(center.X - 60, center.Y - sy - 8, 120, 5), 2, 2);
        Label(dc, "SCREEN", new Point(center.X, center.Y - sy - 27), 10, "#9AA7BE");
        dc.DrawEllipse(Brush("#C1C8D8"), null, center, 5, 5);
        dc.DrawLine(new Pen(Brush("#79869E"), 2), new Point(center.X, center.Y - 8), new Point(center.X, center.Y - 22));
        Label(dc, "YOU", new Point(center.X, center.Y + 14), 9, "#79869E");
        if (Frame != null && Area != null)
            foreach (var light in Frame.Lights.Take(512))
            {
                var fade = LightMapper.DistanceWeight((light.Position - Frame.Player).Length, Settings);
                if (fade <= 0) continue;
                var d = light.Position - Frame.Camera.Position;
                var length = Math.Max(1, d.Length);
                var localRight = d.Dot(Frame.Camera.Right) / length;
                var localFront = d.Dot(Frame.Camera.Forward) / length;
                var yaw = Settings.CameraYawOffset * Math.PI / 180;
                var roomRight = localRight * Math.Cos(yaw) - localFront * Math.Sin(yaw);
                var roomFront = localRight * Math.Sin(yaw) + localFront * Math.Cos(yaw);
                var point = new Point(center.X + roomRight * sx * 0.7, center.Y - roomFront * sy * 0.7);
                var sourceBrush = Brush("#9A6D59"); sourceBrush.Opacity = fade;
                dc.DrawEllipse(sourceBrush, null, point, 1.8, 1.8);
            }
        if (Area == null)
        {
            dc.DrawRoundedRectangle(Brush("#E6141720"), null, new Rect(0, center.Y - 42, w, 90), 6, 6);
            Label(dc, "Your lights will appear here", new Point(center.X, center.Y - 17), 17, "#D0D6E3");
            Label(dc, "Pair your bridge and select an Entertainment area", new Point(center.X, center.Y + 11), 12, "#8B97AD");
        }
        else foreach (var channel in Area.Channels)
        {
            var rgb = Colors.FirstOrDefault(c => c.Id == channel.Id)?.Rgb ?? default;
            var color = ColorFrom(rgb);
            var pos = new Point(center.X + channel.Position.X * sx * 0.88, center.Y - channel.Position.Y * sy * 0.88);
            var glow = new RadialGradientBrush(Color.FromArgb(85, color.R, color.G, color.B), ColorsTransparent);
            dc.DrawEllipse(glow, null, pos, 35, 35);
            dc.DrawEllipse(new SolidColorBrush(color), new Pen(Brush("#A6ADC0"), 1), pos, 8, 8);
            Label(dc, channel.Id.ToString(CultureInfo.InvariantCulture), new Point(pos.X, pos.Y + 13), 10, "#BAC4D8");
        }
        if (Demo) Label(dc, "SIMULATED DATA · NO LAMP OUTPUT", new Point(center.X, h - 17), 10, "#D993A5");
    }
    private static Color ColorsTransparent => Color.FromArgb(0, 0, 0, 0);
    internal static Color ColorFrom(Vec3 rgb) => Color.FromRgb((byte)Math.Round(Math.Clamp(rgb.X, 0, 1) * 255), (byte)Math.Round(Math.Clamp(rgb.Y, 0, 1) * 255), (byte)Math.Round(Math.Clamp(rgb.Z, 0, 1) * 255));
    private static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    private void Label(DrawingContext dc, string text, Point center, double size, string hex)
    {
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, Brush(hex), VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(formatted, new Point(center.X - formatted.Width / 2, center.Y));
    }
}

public static class DemoData
{
    public static EntertainmentArea Area { get; } = new("12345678-1234-1234-1234-123456789abc", "Demo room", false,
        new[] { (-0.8, 0.8, 0.2, "Desk left"), (0.8, 0.8, 0.2, "Desk right"), (-0.9, 0.1, 0.7, "Floor lamp"), (0.9, 0.1, 0.7, "Bookshelf"), (-0.7, -0.8, 0.2, "Rear left"), (0.7, -0.8, 0.2, "Rear right") }
            .Select((p, i) => new EntertainmentChannel((byte)i, new(p.Item1, p.Item2, p.Item3), [new("demo-" + i, 0)], p.Item4)).ToArray());
    public static TelemetryFrame Frame(double seconds)
    {
        var now = DateTimeOffset.UtcNow;
        return new(1, 1, now, now, 0, default, new(default, new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)),
            [new(new(-7, 1, 8), new Vec3(2.8, 0.4, 0.08) * (0.8 + 0.2 * Math.Sin(seconds * 5))),
             new(new(8, 2, 5), new(0.15, 0.8, 2.4)), new(new(Math.Sin(seconds * 0.5) * 8, 3, -7), new(0.7, 0.18, 1.0))]);
    }
}
