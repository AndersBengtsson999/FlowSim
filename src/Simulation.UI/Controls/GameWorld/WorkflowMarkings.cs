using Avalonia;
using Avalonia.Media;
using Simulation.Core;
using Simulation.UI.ViewModels;
using static Simulation.UI.Controls.GameWorld.GameDrawing;
namespace Simulation.UI.Controls.GameWorld;

internal static class WorkflowMarkings
{
    internal static bool HasReturns(GameSceneState state) => state.ActiveRework + state.WaitingRework > 0 ||
        state.Journeys.Any(j => j.Stages.Zip(j.Stages.Skip(1)).Any(p => WorkflowGeometry.IsReturn(p.First, p.Second)));
    internal static void Draw(DrawingContext dc, GameSceneState state)
    {
        var aisle = WorkflowGeometry.Aisle;
        for (var i = 1; i < aisle.Length; i++)
            dc.DrawLine(new Pen(Ink("#859087"), 19, lineCap: PenLineCap.Round), aisle[i - 1], aisle[i]);
        // Only five main direction cues, matching the same route used by observed packages.
        foreach (var pair in new[] { (1, 2), (3, 4), (6, 7), (7, 8), (10, 11) })
        {
            var a = aisle[pair.Item1]; var b = aisle[pair.Item2];
            Arrow(dc, a + (b - a) * .35, a + (b - a) * .7, "#FFE366");
        }
        foreach (var status in new[] { WorkItemStatus.Backlog, WorkItemStatus.Development, WorkItemStatus.WaitingForCodeReview,
            WorkItemStatus.CodeReview, WorkItemStatus.WaitingForTesting, WorkItemStatus.Testing, WorkItemStatus.ReadyForRelease, WorkItemStatus.Released })
        {
            var a = aisle[WorkflowGeometry.AisleIndex(status)]; var b = PackageMotion.Port(status);
            dc.DrawLine(new Pen(Ink("#B7C4AF"), 2, new DashStyle([3, 4], 0)), a, b);
        }
        if (HasReturns(state))
        {
            foreach (var source in new[] { WorkItemStatus.CodeReview, WorkItemStatus.Testing })
            {
                var route = WorkflowGeometry.Route(source, WorkItemStatus.WaitingForRework);
                for (var i = 1; i < route.Length; i++)
                    dc.DrawLine(new Pen(Ink("#CEA0C5"), 2, new DashStyle([4, 5], 0)), route[i - 1], route[i]);
            }
            Arrow(dc, new(708, 268), new(666, 242), "#CEA0C5");
        }
        // Shipping staging and dock share a yellow boundary; maintenance has no through-route.
        dc.DrawLine(new Pen(Ink("#EBD17F"), 3), new(399, 478), new(448, 503));
        dc.DrawLine(new Pen(Ink("#EBD17F"), 3), new(448, 503), new(500, 477));
    }
    internal static void QueueIdentity(DrawingContext dc, WorkshopQueue queue)
    {
        var p = queue.Position;
        var color = queue.Id == "ready" ? "#F0D776" : queue.Active ? "#9BD9C2" : "#D5C7A0";
        // Open corners mark admission queues; active pads have a solid inner diamond.
        if (queue.Active)
        {
            dc.DrawLine(new Pen(Ink(color), 2), new(p.X - 12, p.Y + 22), new(p.X, p.Y + 28));
            dc.DrawLine(new Pen(Ink(color), 2), new(p.X, p.Y + 28), new(p.X + 12, p.Y + 22));
        }
        else
        {
            dc.DrawLine(new Pen(Ink(color), 2), new(p.X - 40, p.Y + 6), new(p.X - 40, p.Y + 14));
            dc.DrawLine(new Pen(Ink(color), 2), new(p.X - 40, p.Y + 14), new(p.X - 25, p.Y + 21));
            dc.DrawLine(new Pen(Ink(color), 2), new(p.X + 40, p.Y + 6), new(p.X + 40, p.Y + 14));
            dc.DrawLine(new Pen(Ink(color), 2), new(p.X + 40, p.Y + 14), new(p.X + 25, p.Y + 21));
        }
    }
    private static void Arrow(DrawingContext dc, Point a, Point b, string color)
    {
        var d = (Vector)(b - a); var unit = d / d.Length; var normal = new Vector(-unit.Y, unit.X);
        dc.DrawLine(new Pen(Ink(color), 2.5), a, b);
        Poly(dc, color, b, b - unit * 9 + normal * 4, b - unit * 9 - normal * 4);
    }
}
