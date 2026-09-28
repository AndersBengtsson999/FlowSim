using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Simulation.UI.ViewModels;

namespace Simulation.UI.Controls;

public sealed class ComparisonBarChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<CompareChartValue>?> ValuesProperty = AvaloniaProperty.Register<ComparisonBarChart, IReadOnlyList<CompareChartValue>?>(nameof(Values));
    public IReadOnlyList<CompareChartValue>? Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    static ComparisonBarChart() => AffectsRender<ComparisonBarChart>(ValuesProperty);
    public override void Render(DrawingContext context)
    {
        base.Render(context); if (Values is not { Count: > 0 } values) return;
        double max = Math.Max(1e-9, values.Max(v => v.Value ?? 0));
        var row = Math.Min(40, Bounds.Height / values.Count);
        for (int i = 0; i < values.Count; i++)
        {
            var v = values[i]; var y = row * i;
            ChartText.Draw(context, v.Name.Length > 27 ? v.Name[..24] + "…" : v.Name, new(0, y + 6));
            if (v.Value is { } value) context.DrawRectangle(Brushes.Teal, null, new Rect(205, y + 4, Math.Max(0, (Bounds.Width - 310) * value / max), Math.Max(1, row - 10)));
            ChartText.Draw(context, Simulation.Application.AnalysisReport.Number(v.Value), new(Math.Max(210, Bounds.Width - 95), y + 6));
        }
    }
}
public sealed class ComparisonFlowChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<ComparisonFlowSeries>?> SeriesProperty = AvaloniaProperty.Register<ComparisonFlowChart, IReadOnlyList<ComparisonFlowSeries>?>(nameof(Series));
    public IReadOnlyList<ComparisonFlowSeries>? Series { get => GetValue(SeriesProperty); set => SetValue(SeriesProperty, value); }
    static ComparisonFlowChart() => AffectsRender<ComparisonFlowChart>(SeriesProperty);
    public override void Render(DrawingContext context)
    {
        base.Render(context); if (Series is not { Count: > 0 } series) return;
        var all = series.SelectMany(s => s.Points).ToArray(); if (all.Length == 0) return;
        var plot = new Rect(50, 70, Math.Max(1, Bounds.Width - 70), Math.Max(1, Bounds.Height - 105));
        double max = Math.Max(1, all.Max(p => p.Value)), end = Math.Max(1, all.Max(p => p.Day));
        IBrush[] colors = [Brushes.Teal, Brushes.DarkOrchid, Brushes.DarkOrange];
        for (int i = 0; i <= 2; i++)
        {
            double y = plot.Bottom - i / 2.0 * plot.Height;
            context.DrawLine(new Pen(Brushes.LightGray), new(plot.Left, y), new(plot.Right, y));
            ChartText.Draw(context, (max * i / 2).ToString("0.##", CultureInfo.InvariantCulture), new(0, y - 7));
        }
        for (int i = 0; i < Math.Min(3, series.Count); i++)
        {
            context.DrawRectangle(colors[i], null, new Rect(0, i * 21, 10, 10)); ChartText.Draw(context, series[i].Name, new(17, i * 21 - 3));
            foreach (var p in series[i].Points)
                context.DrawEllipse(colors[i], null, new(plot.Left + (end == 1 ? 0 : (p.Day - 1) / (end - 1) * plot.Width), plot.Bottom - p.Value / max * plot.Height), 2, 2);
        }
        ChartText.Draw(context, $"Observed end-of-day samples · day 1 to {end:0} · no interpolation", new(plot.Left, plot.Bottom + 10));
    }
}
internal static class ChartText
{
    public static void Draw(DrawingContext c, string text, Point at) => c.DrawText(new FormattedText(text, CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight, new Typeface("Arial"), 12, Brushes.SlateGray), at);
}
