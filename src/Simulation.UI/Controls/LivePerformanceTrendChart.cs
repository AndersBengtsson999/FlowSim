using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Simulation.Application;
using Simulation.UI.ViewModels;

namespace Simulation.UI.Controls;

/// <summary>Single neutral series. Pixel envelopes bound drawing cost; tooltips use original points.</summary>
public sealed class LivePerformanceTrendChart : Control
{
    public static readonly StyledProperty<LiveTrendSeries?> SeriesProperty = AvaloniaProperty.Register<LivePerformanceTrendChart, LiveTrendSeries?>(nameof(Series));
    public static readonly StyledProperty<string> UnitProperty = AvaloniaProperty.Register<LivePerformanceTrendChart, string>(nameof(Unit), "");
    public static readonly StyledProperty<int> SelectedDayProperty = AvaloniaProperty.Register<LivePerformanceTrendChart, int>(nameof(SelectedDay), -1);
    public LiveTrendSeries? Series { get => GetValue(SeriesProperty); set => SetValue(SeriesProperty, value); }
    public string Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public int SelectedDay { get => GetValue(SelectedDayProperty); set => SetValue(SelectedDayProperty, value); }
    static LivePerformanceTrendChart() => AffectsRender<LivePerformanceTrendChart>(SeriesProperty, UnitProperty, SelectedDayProperty);
    private Rect Plot => new(55, 27, Math.Max(1, Bounds.Width - 70), Math.Max(1, Bounds.Height - 58));
    private int First => Series?.Points.FirstOrDefault()?.Day ?? 0;
    private int Last => Series?.Points.LastOrDefault()?.Day ?? 0;
    private int Start => Series?.Interventions.Any(c => c.Day == 0) == true ? 0 : First;
    private double X(int day) => Plot.Left + (day - Start) * Plot.Width / Math.Max(1, Last - Start);
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        Label(context, Unit, new(Plot.Left, 3));
        if (Series is not { Points.Count: > 0 } series) { Label(context, "No completed simulated days yet.", new(Plot.Left, Plot.Top)); return; }
        var max = Math.Max(1, series.Points.Max(p => p.Value ?? 0));
        double Y(double value) => Plot.Bottom - value / max * Plot.Height;
        var gridPen = new Pen(Brush.Parse("#E4EAEE"), 1);
        context.DrawLine(gridPen, Plot.BottomLeft, Plot.BottomRight);
        context.DrawLine(gridPen, Plot.TopLeft, Plot.TopRight);
        context.DrawLine(gridPen, new(Plot.Left, Plot.Center.Y), new(Plot.Right, Plot.Center.Y));
        Label(context, max.ToString("0.##"), new(0, Plot.Top)); Label(context, "0", new(20, Plot.Bottom - 12));
        Label(context, $"Day {Start}", new(Plot.Left, Plot.Bottom + 7));
        if (Last > Start) Label(context, $"Day {Last}", new(Plot.Right - 65, Plot.Bottom + 7));
        var pen = new Pen(Brush.Parse("#326D84"), 1.8);
        var bucket = Math.Max(1, (int)Math.Ceiling(series.Points.Count / Plot.Width));
        Point? previous = null;
        for (var i = 0; i < series.Points.Count; i += bucket)
        {
            var end = Math.Min(i + bucket, series.Points.Count);
            // Never join across missing observations, including gaps inside a rendering bucket.
            var j = i;
            while (j < end)
            {
                if (series.Points[j].Value is null) { previous = null; j++; continue; }
                var begin = j; var lo = double.MaxValue; var hi = double.MinValue;
                while (j < end && series.Points[j].Value is { } v) { lo = Math.Min(lo, v); hi = Math.Max(hi, v); j++; }
                var first = new Point(X(series.Points[begin].Day), Y(series.Points[begin].Value!.Value));
                var last = new Point(X(series.Points[j - 1].Day), Y(series.Points[j - 1].Value!.Value));
                if (previous is { } p) context.DrawLine(pen, p, first);
                context.DrawLine(pen, first, last);
                context.DrawLine(pen, new(last.X, Y(lo)), new(last.X, Y(hi)));
                if (begin == j - 1 && previous is null) context.DrawEllipse(pen.Brush, null, first, 2, 2);
                previous = last;
            }
        }
        foreach (var marker in series.Interventions)
        {
            var selected = marker.Day == SelectedDay;
            context.DrawLine(new Pen(Brushes.SlateGray, selected ? 2 : 1, DashStyle.Dash), new(X(marker.Day), Plot.Top), new(X(marker.Day), Plot.Bottom));
        }
        if (series.Points.All(p => p.Value is null)) Label(context, "No defined values in this range.", new(Plot.Left + 8, Plot.Top + 10));
    }
    public string DescribeAtDay(int day)
    {
        var point = Series?.Points.FirstOrDefault(p => p.Day == day);
        return point is null ? "" : $"Day {day}: {(point.Value is { } v ? v.ToString("G17", CultureInfo.CurrentCulture) + " " + Unit : "Unavailable")}";
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Series is not { Points.Count: > 0 } series) return;
        var x = e.GetPosition(this).X;
        var day = Math.Clamp((int)Math.Round(Start + (x - Plot.Left) * Math.Max(1, Last - Start) / Plot.Width), First, Last);
        var text = DescribeAtDay(day);
        var markers = series.Interventions.Where(c => Math.Abs(X(c.Day) - x) <= 7);
        foreach (var marker in markers) text += "\n" + InterventionPresentation.Marker(marker);
        ToolTip.SetTip(this, text);
    }
    private static void Label(DrawingContext context, string text, Point point) => context.DrawText(new FormattedText(text,
        CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Inter"), 12, Brush.Parse("#61717E")), point);
}
