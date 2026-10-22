using Avalonia;
using Avalonia.Media;
using static Simulation.UI.Controls.GameWorld.GameDrawing;

namespace Simulation.UI.Controls.GameWorld;

public sealed record StationRegion(string Name, Point[] Points)
{
    public bool Contains(Point point)
    {
        var inside = false;
        for (int i = 0, j = Points.Length - 1; i < Points.Length; j = i++)
        {
            var a = Points[i]; var b = Points[j];
            if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }
        return inside;
    }
}

/// <summary>Designated, non-overlapping world-space floor regions. Viewbox transforms input and art together.</summary>
public static class StationMap
{
    public const double SceneHeight = 620;
    public static IReadOnlyList<StationRegion> Regions { get; } =
    [
        new("Backlog", [new(240,190), new(302,90), new(400,130), new(400,200), new(395,230), new(330,280), new(250,255)]),
        new("Development", [new(455,100), new(555,80), new(650,135), new(670,235), new(600,270), new(495,225), new(455,170)]),
        new("Code Review", [new(760,180), new(825,175), new(875,155), new(955,195), new(955,225), new(1005,280), new(1005,320), new(885,335), new(785,290), new(760,250)]),
        new("Testing", [new(650,335), new(730,310), new(825,365), new(825,450), new(750,500), new(640,500), new(625,445), new(625,390)]),
        new("Ready for Release", [new(380,390), new(450,365), new(520,415), new(525,490), new(480,550), new(385,505)]),
        new("Release", [new(130,270), new(215,235), new(300,285), new(335,350), new(300,395), new(210,425), new(125,380)]),
        new("Technical Debt", [new(365,270), new(410,235), new(535,285), new(535,330), new(440,360), new(365,330)])
    ];
    public static IReadOnlyList<StationRegion> QueueRegions { get; } =
    [
        Queue("Dependency queue", 365, 225), Queue("Review queue", 937, 282),
        Queue("Testing queue", 670, 471), Queue("Rework queue", 639, 225)
    ];
    private static StationRegion Queue(string name, double x, double y) => new(name,
        [new(x-23,y-10), new(x,y-24), new(x+23,y-10), new(x+23,y+8), new(x,y+20), new(x-23,y+8)]);
    public static string? InspectAt(Point point) => QueueRegions.FirstOrDefault(r => r.Contains(point))?.Name ?? HitTest(point);
    public static IReadOnlyList<string> InspectionOptions { get; } = Regions.Concat(QueueRegions).Select(r => r.Name).ToArray();
    public static IReadOnlyList<string> Names { get; } = Regions.Select(r => r.Name).ToArray();
    public static string? HitTest(Point point) => Regions.FirstOrDefault(r => r.Contains(point))?.Name;
    public static string Move(string? current, int offset)
    {
        var index = Array.IndexOf(Names.ToArray(), current);
        return Names[index < 0 ? offset < 0 ? Names.Count - 1 : 0 : (index + offset + Names.Count) % Names.Count];
    }
    public static bool SidePanel(double width) => width >= 1120;
    public static double FitHeight(double remainingHeight, double headerHeight, bool sidePanel, bool selected) =>
        Math.Max(240, remainingHeight - headerHeight - 32 - (!sidePanel && selected ? 210 : 0));
    public static double SelectionStroke(double scale) => 2 / Math.Max(.01, scale);
    internal static void SelectionOutline(DrawingContext dc, string? selected, double scale)
    {
        var region = Regions.Concat(QueueRegions).FirstOrDefault(r => r.Name == selected);
        if (region is null) return;
        var geometry = new StreamGeometry();
        using (var path = geometry.Open()) { path.BeginFigure(region.Points[0], false); foreach (var p in region.Points.Skip(1)) path.LineTo(p); path.EndFigure(true); }
        // Constant screen-width outline, no fill above workers or packages.
        dc.DrawGeometry(null, new Pen(Ink(Simulation.UI.Theming.FlowPalette.Accent), SelectionStroke(scale)), geometry);
    }
    internal static void Highlight(DrawingContext dc, string? selected, string? hover, bool focused)
    {
        foreach (var region in Regions.Concat(QueueRegions).Where(r => r.Name == selected || r.Name == hover || focused && selected is null && r.Name == Names[0]))
        {
            var geometry = new StreamGeometry();
            using (var path = geometry.Open()) { path.BeginFigure(region.Points[0], true); foreach (var p in region.Points.Skip(1)) path.LineTo(p); path.EndFigure(true); }
            using (dc.PushOpacity(region.Name == selected ? .12 : .06)) dc.DrawGeometry(Ink(Simulation.UI.Theming.FlowPalette.Accent), null, geometry);
            dc.DrawGeometry(null, new Pen(Ink(region.Name == selected ? Simulation.UI.Theming.FlowPalette.Accent : "#D5E1DA"), region.Name == selected ? 2.5 : 1.5,
                focused ? new DashStyle([3, 2], 0) : null), geometry);
        }
    }
}
