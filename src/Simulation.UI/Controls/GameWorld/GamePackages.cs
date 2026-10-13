using Avalonia;
using Avalonia.Media;
using Simulation.UI.ViewModels;
using static Simulation.UI.Controls.GameWorld.GameDrawing;
namespace Simulation.UI.Controls.GameWorld;

/// <summary>Separate bounded active/waiting groups; never fill a queue with decorative work.</summary>
internal static class GamePackages
{
    internal static void Draw(DrawingContext dc, GameDrawing labels, GameArea area, Point origin, bool queueOnly)
    {
        if (queueOnly)
        {
            Group(dc, labels, origin.X, origin.Y, area.Waiting, area.VisibleWaitingPackages, true);
            return;
        }
        var active = area.VisibleActivePackages;
        var waiting = area.VisibleWaitingPackages;
        Group(dc, labels, origin.X - 52, origin.Y, area.Active, active, false);
        Group(dc, labels, origin.X + 57, origin.Y, area.Waiting, waiting, true);
    }
    private static void Group(DrawingContext dc, GameDrawing labels, double x, double y, int count, int visible, bool waiting)
    {
        // Recessed floor staging marks, not independent raised islands.
        Poly(dc, waiting ? (count > visible ? "#9B814B" : "#756C52") : "#4B7567", new(x - 49, y), new(x, y - 24), new(x + 49, y), new(x, y + 24));
        for (var i = 0; i < visible; i++)
        {
            var row = i / 4; var col = i % 4;
            Package(dc, x - 28 + col * 19 + row * 9, y - 13 + col * 5 - row * 12, waiting);
        }
        labels.Text(dc, $"{(waiting ? "Queue" : "Active")} {count:N0}", x, y + 21, 15, "#FFFFFF", true);
        if (count > visible) labels.Text(dc, $"+{count - visible:N0} grouped", x, y + 39, 12, "#FFE6AF", true);
    }
}
