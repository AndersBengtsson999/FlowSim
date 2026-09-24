using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Simulation.Application;

namespace Simulation.UI.Controls;

/// <summary>End-of-day stacked status counts. Presentation only; no simulation rules.</summary>
public sealed class StatusChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<StatusPoint>?> HistoryProperty =
        AvaloniaProperty.Register<StatusChart, IReadOnlyList<StatusPoint>?>(nameof(History));
    public static readonly StyledProperty<int> SelectedDayProperty =
        AvaloniaProperty.Register<StatusChart, int>(nameof(SelectedDay));
    public IReadOnlyList<StatusPoint>? History { get => GetValue(HistoryProperty); set => SetValue(HistoryProperty, value); }
    public int SelectedDay { get => GetValue(SelectedDayProperty); set => SetValue(SelectedDayProperty, value); }
    private static readonly IBrush[] Colors = [Brush.Parse("#CBD5E1"), Brush.Parse("#3B82F6"),
        Brush.Parse("#A78BFA"), Brush.Parse("#F59E0B"), Brush.Parse("#149185")];

    static StatusChart() => AffectsRender<StatusChart>(HistoryProperty, SelectedDayProperty);

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var history = History;
        var plot = new Rect(44, 18, Math.Max(1, Bounds.Width - 58), Math.Max(1, Bounds.Height - 48));
        context.DrawRectangle(Brushes.White, null, plot);
        if (history is not { Count: > 0 })
        {
            Label(context, "Run a scenario to see status over time.", new Point(48, 60));
            return;
        }
        var max = Math.Max(1, history.Max(p => p.Total));
        var width = plot.Width / history.Count;
        for (var i = 0; i < history.Count; i++)
        {
            var point = history[i];
            double[] values = [point.Backlog, point.Development, point.CodeReview, point.Testing, point.Done];
            var y = plot.Bottom;
            for (var state = 0; state < values.Length; state++)
            {
                var height = plot.Height * values[state] / max;
                y -= height;
                if (height > 0) context.DrawRectangle(Colors[state], null, new Rect(plot.X + i * width, y, width, height));
            }
        }
        var pen = new Pen(Brush.Parse("#64748B"), 1);
        context.DrawLine(pen, plot.BottomLeft, plot.BottomRight);
        context.DrawLine(pen, plot.TopLeft, plot.BottomLeft);
        Label(context, max.ToString("0.#", CultureInfo.CurrentCulture), new Point(0, plot.Top - 7));
        Label(context, "0", new Point(25, plot.Bottom - 10));
        Label(context, "Day 1", new Point(plot.Left, plot.Bottom + 7));
        Label(context, $"Day {history.Count}", new Point(Math.Max(plot.Left + 55, plot.Right - 55), plot.Bottom + 7));
        var selected = Math.Clamp(SelectedDay, 0, history.Count - 1);
        var x = plot.Left + (selected + 0.5) * width;
        context.DrawLine(new Pen(Brush.Parse("#0F172A"), 2), new Point(x, plot.Top), new Point(x, plot.Bottom));
    }

    private static void Label(DrawingContext context, string text, Point location) =>
        context.DrawText(new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Inter"), 11, Brush.Parse("#526279")), location);
}
