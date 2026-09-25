using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Simulation.Application;

namespace Simulation.UI.Controls;

public sealed class SensitivityChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<AnalysisPoint>?> PointsProperty = AvaloniaProperty.Register<SensitivityChart, IReadOnlyList<AnalysisPoint>?>(nameof(Points));
    public static readonly StyledProperty<AnalysisMetric> MetricProperty = AvaloniaProperty.Register<SensitivityChart, AnalysisMetric>(nameof(Metric));
    public IReadOnlyList<AnalysisPoint>? Points { get => GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
    public AnalysisMetric Metric { get => GetValue(MetricProperty); set => SetValue(MetricProperty, value); }
    static SensitivityChart() => AffectsRender<SensitivityChart>(PointsProperty, MetricProperty);
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var points = Points?.OrderBy(p => p.ParameterValue).ToArray();
        if (points is not { Length: > 0 }) return;
        var observed = points.Select(p => p.MeasurementWindow.GetValueOrDefault(Metric)?.P50).Where(v => v.HasValue).Select(v => v!.Value).ToArray();
        if (observed.Length == 0) { Label(context, "No completed-item observations for this metric", new(15, 20)); return; }
        var plot = new Rect(72, 16, Math.Max(1, Bounds.Width - 95), Math.Max(1, Bounds.Height - 68));
        var max = Math.Max(1e-9, observed.Max()); var lo = points[0].ParameterValue; var hi = points[^1].ParameterValue;
        double X(double x) => hi == lo ? plot.Center.X : plot.Left + (x - lo) / (hi - lo) * plot.Width;
        for (int i = 0; i <= 4; i++)
        {
            var y = plot.Bottom - i / 4.0 * plot.Height;
            context.DrawLine(new Pen(Brushes.LightGray), new(plot.Left, y), new(plot.Right, y));
            Label(context, (max * i / 4).ToString("0.###", CultureInfo.InvariantCulture), new(0, y - 7));
        }
        Point? previous = null;
        foreach (var p in points)
        {
            var v = p.MeasurementWindow.GetValueOrDefault(Metric)?.P50;
            if (v is null) { previous = null; continue; } // Do not connect across missing measurements.
            var at = new Point(X(p.ParameterValue), plot.Bottom - v.Value / max * plot.Height);
            if (previous is { } last) context.DrawLine(new Pen(Brushes.Teal, 2), last, at);
            context.DrawEllipse(Brushes.Teal, null, at, 4, 4); previous = at;
            if (points.Length <= 15) Label(context, p.ParameterValue.ToString("0.###", CultureInfo.InvariantCulture), new(at.X - 10, plot.Bottom + 8));
        }
        if (points.Length > 15) { Label(context, lo.ToString(CultureInfo.InvariantCulture), new(plot.Left, plot.Bottom + 8)); Label(context, hi.ToString(CultureInfo.InvariantCulture), new(plot.Right - 30, plot.Bottom + 8)); }
        Label(context, "Parameter value (probabilities are ratios 0–1)", new(plot.Left, plot.Bottom + 30));
    }
    private static void Label(DrawingContext c, string s, Point p) => c.DrawText(new FormattedText(s, CultureInfo.InvariantCulture,
        FlowDirection.LeftToRight, new Typeface("Arial"), 12, Brushes.SlateGray), p);
}
