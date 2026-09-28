using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Simulation.UI.ViewModels;

namespace Simulation.UI.Controls;

/// <summary>One drawing surface, bounded min/max envelopes per pixel column; no per-day controls.</summary>
public sealed class LiveQueueChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<LiveQueuePoint>?> PointsProperty = AvaloniaProperty.Register<LiveQueueChart, IReadOnlyList<LiveQueuePoint>?>(nameof(Points));
    public static readonly StyledProperty<IReadOnlyList<LiveChangeRow>?> ChangesProperty = AvaloniaProperty.Register<LiveQueueChart, IReadOnlyList<LiveChangeRow>?>(nameof(Changes));
    public static readonly StyledProperty<int> CurrentDayProperty = AvaloniaProperty.Register<LiveQueueChart, int>(nameof(CurrentDay));
    public static readonly StyledProperty<bool> ShowReworkProperty = AvaloniaProperty.Register<LiveQueueChart, bool>(nameof(ShowRework));
    public IReadOnlyList<LiveQueuePoint>? Points { get => GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
    public IReadOnlyList<LiveChangeRow>? Changes { get => GetValue(ChangesProperty); set => SetValue(ChangesProperty, value); }
    public int CurrentDay { get => GetValue(CurrentDayProperty); set => SetValue(CurrentDayProperty, value); }
    public bool ShowRework { get => GetValue(ShowReworkProperty); set => SetValue(ShowReworkProperty, value); }
    static LiveQueueChart() => AffectsRender<LiveQueueChart>(PointsProperty, ChangesProperty, CurrentDayProperty, ShowReworkProperty);
    private Rect Plot => new(34, 22, Math.Max(1, Bounds.Width - 44), Math.Max(1, Bounds.Height - 52));
    private double X(int day) => Plot.Left + day * Plot.Width / Math.Max(1, CurrentDay);
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Points is not { Count: > 0 } points) return;
        var max = Math.Max(1, points.Max(p => Math.Max(Math.Max(p.Review, p.Testing), ShowRework ? p.Rework : 0)));
        double Y(int n) => Plot.Bottom - (double)n / max * Plot.Height;
        var grid = new Pen(Brushes.LightGray, 1);
        context.DrawLine(grid, Plot.BottomLeft, Plot.BottomRight);
        Label(context, max.ToString(), new(0, Plot.Top)); Label(context, "0", new(0, Plot.Bottom - 10));
        Label(context, "1", new(Plot.Left, Plot.Bottom + 5)); Label(context, $"Day {CurrentDay}", new(Plot.Right - 70, Plot.Bottom + 5));
        var bucket = Math.Max(1, (int)Math.Ceiling(points.Count / Math.Max(1, Plot.Width)));
        for (var s = 0; s < (ShowRework ? 3 : 2); s++)
        {
            int Value(LiveQueuePoint p) => s == 0 ? p.Review : s == 1 ? p.Testing : p.Rework;
            var pen = new Pen(Brush.Parse(s == 0 ? "#246B91" : s == 1 ? "#AE6B0D" : "#8753A6"), 1.7);
            Point? previous = null;
            for (var i = 0; i < points.Count; i += bucket)
            {
                var end = Math.Min(points.Count, i + bucket); var lo = int.MaxValue; var hi = 0;
                for (var j = i; j < end; j++) { var v = Value(points[j]); lo = Math.Min(lo, v); hi = Math.Max(hi, v); }
                var x = X(points[end - 1].Day); var current = new Point(x, Y(Value(points[end - 1])));
                if (previous is { } p) context.DrawLine(pen, p, current);
                context.DrawLine(pen, new(x, Y(lo)), new(x, Y(hi)));
                previous = current;
            }
        }
        foreach (var change in Changes ?? [])
            context.DrawLine(new Pen(Brushes.SlateGray, 1, DashStyle.Dash), new(X(change.Day), Plot.Top), new(X(change.Day), Plot.Bottom));
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var x = e.GetPosition(this).X;
        var changes = Changes?.Where(c => Math.Abs(X(c.Day) - x) < 8).ToArray() ?? [];
        ToolTip.SetTip(this, changes.Length == 0 ? null : string.Join("\n", changes.Select(c => $"Day {c.Day}: {c.Description}")));
    }
    private static void Label(DrawingContext c, string text, Point p) => c.DrawText(new FormattedText(text,
        CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Inter"), 11, Brushes.SlateGray), p);
}
