using System.Globalization;
using Avalonia;
using Avalonia.Media;
namespace Simulation.UI.Controls.GameWorld;

/// <summary>Shared vector primitives and bounded text cache for one snapshot.</summary>
internal sealed class GameDrawing
{
    private readonly Dictionary<(string, double, string), FormattedText> labels = [];
    internal static readonly Dictionary<string, IBrush> palette = new();
    internal static IBrush Ink(string color)
    {
        if (!palette.TryGetValue(color, out var brush)) palette[color] = brush = new SolidColorBrush(Color.Parse(color)).ToImmutable();
        return brush;
    }
    internal void Text(DrawingContext dc, string text, double x, double y, double size = 16, string color = "#DFE9EF", bool centered = false)
    {
        var key = (text, size, color);
        if (!labels.TryGetValue(key, out var label)) labels[key] = label = new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Inter"), size, Ink(color));
        dc.DrawText(label, new(centered ? x - label.Width / 2 : x, y));
    }
    internal static void Poly(DrawingContext dc, string fill, params Point[] points)
    {
        var g = new StreamGeometry();
        using (var p = g.Open()) { p.BeginFigure(points[0], true); foreach (var q in points.Skip(1)) p.LineTo(q); p.EndFigure(true); }
        dc.DrawGeometry(Ink(fill), null, g);
    }
    internal static void Box(DrawingContext dc, double x, double y, double w, double depth, double height, string top, string left, string right)
    {
        Point a = new(x - w / 2, y), b = new(x, y - depth / 2), c = new(x + w / 2, y), d = new(x, y + depth / 2);
        Poly(dc, left, a, d, d + new Vector(0, height), a + new Vector(0, height));
        Poly(dc, right, d, c, c + new Vector(0, height), d + new Vector(0, height));
        Poly(dc, top, a, b, c, d);
    }
    internal static void Package(DrawingContext dc, double x, double y, bool waiting)
    {
        Box(dc, x, y, 23, 13, 16, waiting ? "#F2CF87" : "#90DEC0", waiting ? "#B68844" : "#438775", waiting ? "#D6AA61" : "#66B39A");
        dc.DrawLine(new Pen(Ink("#FFF0C2"), 3), new(x - 6, y - 3), new(x + 6, y + 3));
    }
    internal static void Desk(DrawingContext dc, double x, double y, bool testing = false)
    {
        dc.DrawLine(new Pen(Ink("#455B6C"), 5), new(x - 28, y + 8), new(x - 28, y + 28));
        dc.DrawLine(new Pen(Ink("#455B6C"), 5), new(x + 28, y + 8), new(x + 28, y + 28));
        Box(dc, x, y, 88, 38, 7, "#DFCCAB", "#9F8B72", "#BDA98B");
        Poly(dc, "#112535", new(x - 18, y - 42), new(x + 17, y - 34), new(x + 17, y - 8), new(x - 18, y - 16));
        Poly(dc, testing ? "#70CDB6" : "#79BCDC", new(x - 14, y - 37), new(x + 13, y - 31), new(x + 13, y - 12), new(x - 14, y - 18));
        dc.DrawLine(new Pen(Ink("#D3F4E8"), 2), new(x - 10, y - 29), new(x + 5, y - 25));
        dc.DrawLine(new Pen(Ink("#45687D"), 3), new(x, y - 10), new(x, y + 1));
        Poly(dc, "#718592", new(x - 6, y + 7), new(x + 11, y + 11), new(x + 25, y + 4), new(x + 8, y));
    }
    internal static void Plant(DrawingContext dc, double x, double y)
    {
        Box(dc, x, y, 23, 14, 18, "#B7BDB6", "#717E83", "#91A09F");
        dc.DrawEllipse(Ink("#569F81"), null, new(x - 6, y - 10), 8, 15);
        dc.DrawEllipse(Ink("#82BC89"), null, new(x + 5, y - 17), 7, 18);
    }
    internal void ClearLabels() => labels.Clear();
}
