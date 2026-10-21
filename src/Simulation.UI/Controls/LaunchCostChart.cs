using Simulation.UI.Theming;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Simulation.Core;
using System.Globalization;
namespace Simulation.UI.Controls;

/// <summary>Draws existing economic observations with Avalonia, like DailyChart. No financial calculations.</summary>
public sealed class LaunchCostChart : Control
{
    public static readonly StyledProperty<LaunchCostHistory?> HistoryProperty = AvaloniaProperty.Register<LaunchCostChart, LaunchCostHistory?>(nameof(History));
    public LaunchCostHistory? History { get => GetValue(HistoryProperty); set => SetValue(HistoryProperty, value); }
    static LaunchCostChart() => AffectsRender<LaunchCostChart>(HistoryProperty);
    public override void Render(DrawingContext dc)
    {
        base.Render(dc);
        void Text(string value, double x, double y, string color = FlowPalette.Text) => dc.DrawText(new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 11, Brush.Parse(color)), new(x, y));
        if (History?.Points is not { Count: > 0 } points) { Text("No recorded cost history yet.", 4, 8); return; }
        var plot = new Rect(68, 24, Math.Max(1, Bounds.Width - 80), Math.Max(1, Bounds.Height - 50));
        var max = Math.Max(1m, points.Max(p => Math.Max(p.Unlaunched, p.CustomerLaunched)));
        double X(LaunchCostPoint p) => plot.Left + (points.Count == 1 ? plot.Width / 2 : (p.Day - points[0].Day) * plot.Width / Math.Max(1, points[^1].Day - points[0].Day));
        double Y(decimal value) => plot.Bottom - (double)(value / max) * plot.Height;
        Text("Estimated allocation · SEK", 4, 2);
        for (var t = 0; t <= 2; t++) { var value = max * t / 2; var y = Y(value); dc.DrawLine(new Pen(Brush.Parse(FlowPalette.Border)), new(plot.Left,y), new(plot.Right,y)); Text(value.ToString("N0"), 0, y - 7); }
        // Pixel buckets preserve extrema and launch boundaries; never alter authoritative data.
        var stride = Math.Max(1, points.Count / Math.Max(1, (int)plot.Width));
        var indexes = new SortedSet<int> { 0, points.Count - 1 };
        for (var i = 0; i < points.Count; i += stride)
        {
            var end = Math.Min(points.Count, i + stride); indexes.Add(i); indexes.Add(end - 1);
            for (var s = 0; s < 4; s++)
            {
                var series = s; var range = Enumerable.Range(i, end - i);
                indexes.Add(range.MinBy(j => Value(points[j], series))); indexes.Add(range.MaxBy(j => Value(points[j], series)));
            }
        }
        for (var i = 0; i < points.Count; i++) if (points[i].CustomerLaunch) { indexes.Add(i); if (i > 0) indexes.Add(i - 1); }
        foreach (var p in points.Where(p => p.CustomerLaunch)) dc.DrawLine(new Pen(Brush.Parse(FlowPalette.Warning), 1, new DashStyle([3,3],0)), new(X(p),plot.Top),new(X(p),plot.Bottom));
        string[] colors = [FlowPalette.Accent, FlowPalette.Warning, FlowPalette.Purple, FlowPalette.Success];
        for (var series = 0; series < 4; series++)
        {
            var pen = new Pen(Brush.Parse(colors[series]), series == 2 ? 2.5 : 1.5); Point? previous = null;
            foreach (var i in indexes) { var point = new Point(X(points[i]), Y(Value(points[i], series))); if (previous is { } prior) dc.DrawLine(pen, prior, point); else dc.DrawEllipse(pen.Brush, null, point, 2, 2); previous = point; }
        }
        Text($"Days {points[0].Day}–{points[^1].Day} · dashed lines: customer launches", plot.Left, plot.Bottom + 5);
    }
    private static decimal Value(LaunchCostPoint p, int series) => series switch { 0 => p.WorkInProgress, 1 => p.AwaitingLaunch, 2 => p.Unlaunched, _ => p.CustomerLaunched };
}
