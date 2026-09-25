using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Simulation.Application;

namespace Simulation.UI.Controls;

/// <summary>Draws precomputed bins; statistics belong to Application, not this control.</summary>
public sealed class HistogramChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<HistogramBin>?> BinsProperty =
        AvaloniaProperty.Register<HistogramChart, IReadOnlyList<HistogramBin>?>(nameof(Bins));
    public static readonly StyledProperty<string> AxisLabelProperty =
        AvaloniaProperty.Register<HistogramChart, string>(nameof(AxisLabel), "Value");
    public IReadOnlyList<HistogramBin>? Bins { get => GetValue(BinsProperty); set => SetValue(BinsProperty, value); }
    public string AxisLabel { get => GetValue(AxisLabelProperty); set => SetValue(AxisLabelProperty, value); }
    static HistogramChart() => AffectsRender<HistogramChart>(BinsProperty, AxisLabelProperty);

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bins is not { Count: > 0 } bins) { Label(context, "No observations with completed items.", new(15, 40)); return; }
        var plot = new Rect(48, 26, Math.Max(1, Bounds.Width - 65), Math.Max(1, Bounds.Height - 78));
        var maxCount = Math.Max(1, bins.Max(b => b.Count));
        var barWidth = plot.Width / bins.Count;
        var brush = Brush.Parse("#246B91");
        for (var i = 0; i < bins.Count; i++)
        {
            var height = plot.Height * bins[i].Count / maxCount;
            context.DrawRectangle(brush, null, new Rect(plot.Left + i * barWidth + 1, plot.Bottom - height, Math.Max(1, barWidth - 2), height));
        }
        context.DrawLine(new Pen(Brush.Parse("#64748B")), plot.BottomLeft, plot.BottomRight);
        Label(context, "Runs", new(0, 3));
        Label(context, maxCount.ToString(CultureInfo.CurrentCulture), new(0, plot.Top));
        Label(context, "0", new(20, plot.Bottom - 12));
        Label(context, bins[0].Minimum.ToString("0.###", CultureInfo.CurrentCulture), new(plot.Left, plot.Bottom + 5));
        if (bins.Count > 1) Label(context, bins[^1].Maximum.ToString("0.###", CultureInfo.CurrentCulture), new(plot.Right - 45, plot.Bottom + 5));
        Label(context, AxisLabel, new(plot.Left, plot.Bottom + 26));
    }
    private static void Label(DrawingContext c, string text, Point p) => c.DrawText(new FormattedText(text,
        CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Inter"), 11, Brush.Parse("#526279")), p);
}
