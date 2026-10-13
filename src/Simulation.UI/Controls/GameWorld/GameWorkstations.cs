using Avalonia;
using Avalonia.Media;
using Simulation.UI.ViewModels;
using static Simulation.UI.Controls.GameWorld.GameDrawing;
namespace Simulation.UI.Controls.GameWorld;

internal static class GameWorkstations
{
    internal static void Draw(DrawingContext dc, Workstation station, GameSceneState scene)
    {
        var (x, y) = station.Position;
        switch (station.AreaIndex)
        {
            case 0: Rack(dc, x, y - 15); break;
            case 1:
                Desk(dc, x - 49, y - 9); Desk(dc, x + 42, y + 17);
                Workers(dc, station, scene.VisibleDevelopers, scene.VisibleSpecialists, false);
                break;
            case 2:
                Desk(dc, x - 36, y - 8); Desk(dc, x + 43, y + 13);
                // Review uses the shared developer pool. No extra employee figures are invented.
                dc.DrawLine(new Pen(Ink("#F3E5A0"), 3), new(x - 47, y - 32), new(x - 31, y - 28));
                break;
            case 3:
                Desk(dc, x - 43, y - 5, true); Desk(dc, x + 43, y + 15, true);
                Box(dc, x + 87, y - 12, 26, 13, 32, "#B7DBD1", "#3B6660", "#5C9B8B");
                Workers(dc, station, scene.VisibleTesters, 0, true);
                break;
            case 4:
                Rack(dc, x - 24, y - 8);
                Box(dc, x + 56, y + 1, 64, 32, 5, "#C7AA79", "#8A714D", "#A78A5E");
                break;
            case 5: Truck(dc, x, y); break;
        }
    }
    private static void Workers(DrawingContext dc, Workstation station, int count, int specialists, bool tester)
    {
        Point[] positions = [new(-59, -12), new(23, 6), new(-16, 0), new(63, 18), new(-78, 15), new(4, 28)];
        for (var i = 0; i < count; i++)
            GameCharacters.Draw(dc, new($"{station.Id}/capacity-{i}", station.Position + (Vector)positions[i],
                tester ? WorkerAppearance.Tester : i < specialists ? WorkerAppearance.Specialist : WorkerAppearance.Developer));
    }
    private static void Rack(DrawingContext dc, double x, double y)
    {
        // Open shelves stay empty until authoritative packages are drawn in the staging area.
        for (var i = 0; i < 3; i++) Box(dc, x, y - i * 19, 127, 42, 4, "#B7B29D", "#706F61", "#91917F");
        foreach (var dx in new[] { -60, 0, 60 })
            dc.DrawLine(new Pen(Ink("#394C4E"), 4), new(x + dx, y - 50 + (dx == 0 ? 20 : 0)), new(x + dx, y + 22));
    }
    private static void Truck(DrawingContext dc, double x, double y)
    {
        // Parked vehicle and painted loading bay do not assert that a release occurred.
        Poly(dc, "#434D4A", new(x - 97, y + 2), new(x - 8, y - 42), new(x + 99, y + 11), new(x + 11, y + 55));
        for (var i = 0; i < 5; i++) dc.DrawLine(new Pen(Ink("#E4CB50"), 3), new(x - 88 + i * 17, y + 8 + i * 8), new(x - 76 + i * 17, y + 2 + i * 8));
        Box(dc, x - 16, y - 36, 116, 58, 45, "#F6E65C", "#BBA623", "#E0C830");
        Box(dc, x + 57, y - 8, 53, 27, 30, "#FFEF86", "#BEA735", "#E6CF49");
        Poly(dc, "#83C3CF", new(x + 57, y - 18), new(x + 77, y - 8), new(x + 77, y + 7), new(x + 57, y - 3));
        foreach (var wheel in new[] { new Point(x - 44, y + 10), new Point(x + 13, y + 35), new Point(x + 73, y + 22) })
        { dc.DrawEllipse(Ink("#23363D"), null, wheel, 9, 12); dc.DrawEllipse(Ink("#A9B6B2"), null, wheel, 4, 6); }
    }
    internal static void Maintenance(DrawingContext dc)
    {
        var (x, y) = GameWorldLayout.Maintenance;
        Box(dc, x, y, 125, 62, 6, "#AE8568", "#765D4E", "#936F58");
        Box(dc, x - 31, y - 30, 34, 17, 32, "#A4B6B0", "#485D5C", "#6B8581");
        for (var i = 0; i < 3; i++) dc.DrawLine(new Pen(Ink("#9EC4AC"), 2), new(x - 43, y - 21 + i * 7), new(x - 32, y - 16 + i * 7));
        dc.DrawLine(new Pen(Ink("#D6DDCE"), 5), new(x + 6, y - 5), new(x + 28, y + 6));
        dc.DrawEllipse(null, new Pen(Ink("#D6DDCE"), 3), new(x + 5, y - 6), 6, 4);
        Box(dc, x + 37, y - 16, 26, 13, 13, "#D38F66", "#82533E", "#B77552");
    }
}
