using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Simulation.Core;

namespace Simulation.UI.Controls;

public enum DailyChartMode { Wip, Queues, Developers, Testers, ReworkStates, ReworkCapacity }

/// <summary>Presentation of immutable observations. No metric aggregation or simulation rules.</summary>
public sealed class DailyChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<DailySnapshot>?> DaysProperty =
        AvaloniaProperty.Register<DailyChart, IReadOnlyList<DailySnapshot>?>(nameof(Days));
    public static readonly StyledProperty<int> SelectedDayProperty =
        AvaloniaProperty.Register<DailyChart, int>(nameof(SelectedDay), 1);
    public static readonly StyledProperty<DailyChartMode> ModeProperty =
        AvaloniaProperty.Register<DailyChart, DailyChartMode>(nameof(Mode));
    public IReadOnlyList<DailySnapshot>? Days { get => GetValue(DaysProperty); set => SetValue(DaysProperty, value); }
    public int SelectedDay { get => GetValue(SelectedDayProperty); set => SetValue(SelectedDayProperty, value); }
    public DailyChartMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

    static DailyChart() => AffectsRender<DailyChart>(DaysProperty, SelectedDayProperty, ModeProperty);

    // Read existing snapshot values directly; scale and coordinates below are presentation only.
    private double Value(DailySnapshot d, int series) => Mode switch
    {
        DailyChartMode.Wip => d.TotalWip,
        DailyChartMode.Queues => series == 0 ? d.WaitingForCodeReviewCount : d.WaitingForTestingCount,
        DailyChartMode.Developers => series == 0 ? d.UsedDeveloperCapacity : d.AvailableDeveloperCapacity,
        DailyChartMode.ReworkStates => series == 0 ? d.ReworkCount : d.WaitingForReworkCount,
        DailyChartMode.ReworkCapacity => d.UsedReworkDeveloperCapacity,
        DailyChartMode.Testers => series == 0 ? d.UsedTesterCapacity : d.AvailableTesterCapacity,
        _ => 0
    };

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Days is not { Count: > 0 } days) return;
        var plot = new Rect(58, 25, Math.Max(1, Bounds.Width - 75), Math.Max(1, Bounds.Height - 67));
        var seriesCount = Mode is DailyChartMode.Wip or DailyChartMode.ReworkCapacity ? 1 : 2;
        var max = Math.Max(1, days.Max(d => Enumerable.Range(0, seriesCount).Max(s => Value(d, s))));
        double X(int i) => plot.Left + (days.Count == 1 ? plot.Width / 2 : i * plot.Width / (days.Count - 1));
        double Y(double value) => plot.Bottom - value / max * plot.Height;
        var grid = new Pen(Brush.Parse("#E2E8F0"), 1);
        for (var tick = 0; tick <= 2; tick++)
        {
            var value = max * tick / 2;
            var y = Y(value);
            context.DrawLine(grid, new Point(plot.Left, y), new Point(plot.Right, y));
            Label(context, value.ToString("0.##", CultureInfo.CurrentCulture), new Point(0, y - 7));
        }
        Label(context, Mode is DailyChartMode.Developers or DailyChartMode.Testers or DailyChartMode.ReworkCapacity ? "Capacity units" : Mode == DailyChartMode.Queues ? "Queue size (items)" : "Work Items", new Point(0, 3));
        // Draw available capacity first, so coinciding actual usage stays visible.
        for (var series = seriesCount - 1; series >= 0; series--)
        {
            var color = series == 0 ? "#246B91" : Mode is DailyChartMode.Queues or DailyChartMode.ReworkStates ? "#AE6B0D" : "#94A3B8";
            var brush = Brush.Parse(color);
            var pen = new Pen(brush, series == 0 ? 2.5 : 1.5);
            if (days.Count == 1) context.DrawEllipse(brush, null, new Point(X(0), Y(Value(days[0], series))), 3, 3);
            else
            {
                var geometry = new StreamGeometry();
                using (var path = geometry.Open())
                {
                    path.BeginFigure(new Point(X(0), Y(Value(days[0], series))), false);
                    for (var i = 1; i < days.Count; i++) path.LineTo(new Point(X(i), Y(Value(days[i], series))));
                    path.EndFigure(false);
                }
                context.DrawGeometry(null, pen, geometry);
            }
        }
        var selected = Math.Clamp(SelectedDay - 1, 0, days.Count - 1);
        context.DrawLine(new Pen(Brush.Parse("#475569"), 1), new Point(X(selected), plot.Top), new Point(X(selected), plot.Bottom));
        Label(context, "1", new Point(plot.Left, plot.Bottom + 5));
        if (days.Count > 1) Label(context, days.Count.ToString(CultureInfo.CurrentCulture), new Point(plot.Right - 20, plot.Bottom + 5));
        Label(context, "Simulation Day (end of day)", new Point(plot.Left + Math.Max(0, (plot.Width - 150) / 2), plot.Bottom + 23));
    }

    private static void Label(DrawingContext context, string text, Point location) =>
        context.DrawText(new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Inter"), 11, Brush.Parse("#526279")), location);
}
