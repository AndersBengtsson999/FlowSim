using Avalonia;
using Avalonia.Media;
using static Simulation.UI.Controls.GameWorld.GameDrawing;

namespace Simulation.UI.Controls.GameWorld;

// Stable presentation anchors, not item identities or employee assignments.
internal sealed record Workstation(string Id, int AreaIndex, Point Position, string Accent);
internal static class GameWorldLayout
{
    internal static readonly Workstation[] Stations =
    [
        new("backlog", 0, new(320, 210), "#DAC697"),
        new("development", 1, new(555, 142), "#FFEB00"),
        new("review", 2, new(805, 248), "#97C9F0"),
        new("testing", 3, new(680, 390), "#80D8B6"),
        new("ready", 4, new(457, 447), "#E6C694"),
        new("release", 5, new(215, 340), "#FFEB00")
    ];
    internal static readonly Point Maintenance = new(505, 302);
    internal static Point Floor(double u, double v) => new(540 + (u - v) * .875, 17 + (u + v) * .475);

    internal static void Draw(DrawingContext dc, bool spriteDecoration = false)
    {
        // One continuous 2:1 isometric slab. All furniture shares its perspective.
        Box(dc, 540, 302, 1050, 570, 18, "#626D69", "#303E40", "#43504F");
        for (var i = 0; i <= 12; i++)
        {
            var p = i * 50;
            dc.DrawLine(new Pen(Ink("#758079"), .7), Floor(p, 0), Floor(p, 600));
            dc.DrawLine(new Pen(Ink("#758079"), .7), Floor(0, p), Floor(600, p));
        }
        // Low perimeter walls, open at the front so queues remain visible.
        dc.DrawLine(new Pen(Ink("#AFA993"), 7), new(15, 302), new(540, 17));
        dc.DrawLine(new Pen(Ink("#CCC4AD"), 7), new(540, 17), new(1065, 302));
        dc.DrawLine(new Pen(Ink("#FFE6A1"), 2), new(17, 297), new(540, 13));
        dc.DrawLine(new Pen(Ink("#FFE6A1"), 2), new(540, 13), new(1063, 297));
        if (!spriteDecoration)
        {
        Path(dc, [new(325, 228), new(402, 266), new(555, 190), new(690, 258), new(793, 310), new(715, 350), new(633, 391), new(455, 480), new(305, 405), new(218, 362)]);
        Arrow(dc, new(426, 253), new(463, 235));
        Arrow(dc, new(654, 240), new(690, 258));
        Arrow(dc, new(766, 324), new(733, 341));
        Arrow(dc, new(569, 423), new(532, 441));
        Arrow(dc, new(353, 429), new(317, 411));
        // Maintenance is a side connection, deliberately without a process arrow.
        dc.DrawLine(new Pen(Ink("#D49D7C"), 3, new DashStyle([3, 3], 0)), new(548, 216), new(505, 269));
        }
        if (!spriteDecoration) { Plant(dc, 413, 118); Plant(dc, 913, 345); Plant(dc, 354, 474); }
    }
    private static void Path(DrawingContext dc, Point[] points)
    {
        for (var i = 1; i < points.Length; i++)
        {
            dc.DrawLine(new Pen(Ink("#859087"), 26, lineCap: PenLineCap.Round), points[i - 1], points[i]);
            dc.DrawLine(new Pen(Ink("#BEBB99"), 1, new DashStyle([5, 6], 0)), points[i - 1], points[i]);
        }
    }
    private static void Arrow(DrawingContext dc, Point a, Point b)
    {
        var d = (Vector)(b - a); var unit = d / d.Length; var normal = new Vector(-unit.Y, unit.X);
        dc.DrawLine(new Pen(Ink("#FFE366"), 3), a, b);
        Poly(dc, "#FFE366", b, b - unit * 11 + normal * 5, b - unit * 11 - normal * 5);
    }
}
