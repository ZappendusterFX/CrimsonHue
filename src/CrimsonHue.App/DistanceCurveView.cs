using System.Globalization;
using System.Windows;
using System.Windows.Media;
using CrimsonHue.Core;

namespace CrimsonHue.App;

public sealed class DistanceCurveView : FrameworkElement
{
    public MappingSettings Settings { get; set; } = new();
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var width = Math.Max(1, ActualWidth - 48);
        var chart = new Rect(32, 15, width, 67);
        var grid = new Pen(new SolidColorBrush(Color.FromRgb(53, 62, 79)), 1);
        for (var i = 0; i < 3; i++)
            dc.DrawLine(grid, new(chart.Left, chart.Top + i * chart.Height / 2), new(chart.Right, chart.Top + i * chart.Height / 2));
        var points = Enumerable.Range(0, 101).Select(i => new Point(chart.Left + width * i / 100,
            chart.Bottom - chart.Height * LightMapper.DistanceWeight(Settings.FadeEnd * i / 100, Settings))).ToArray();
        var curve = new StreamGeometry();
        using (var context = curve.Open()) { context.BeginFigure(points[0], false, false); context.PolyLineTo(points.Skip(1).ToArray(), true, false); }
        dc.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromRgb(238, 137, 161)), 2.5), curve);
        Label("100%", new(0, 8)); Label("0%", new(7, 74));
        Label("0", new(chart.Left, 86));
        Label($"{Settings.FadeEnd:0.##} gu", new(chart.Right - 52, 86));
        var middle = (Settings.FadeStart + Settings.FadeEnd) / 2;
        Label($"At {middle:0.##} gu: {LightMapper.DistanceWeight(middle, Settings):P0} contribution", new(chart.Left + 25, 86));
        void Label(string text, Point at) => dc.DrawText(new FormattedText(text, CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10,
            new SolidColorBrush(Color.FromRgb(160, 174, 194)), VisualTreeHelper.GetDpi(this).PixelsPerDip), at);
    }
}
