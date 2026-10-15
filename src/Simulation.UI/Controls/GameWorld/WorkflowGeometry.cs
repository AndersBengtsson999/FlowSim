using Avalonia;
using Simulation.Core;
namespace Simulation.UI.Controls.GameWorld;

/// <summary>Shared visual routes, sourced from existing statuses. Technical debt is deliberately absent.</summary>
public static class WorkflowGeometry
{
    public static readonly Point[] Aisle = [new(365, 283), new(402, 266), new(555, 225), new(690, 258), new(793, 310), new(715, 350), new(633, 391), new(610, 445), new(448, 510), new(395, 465), new(365, 410), new(305, 405)];
    public static int AisleIndex(WorkItemStatus s) => s switch
    {
        WorkItemStatus.Backlog => 0,
        WorkItemStatus.Development or WorkItemStatus.Rework or WorkItemStatus.WaitingForRework => 2,
        WorkItemStatus.WaitingForCodeReview or WorkItemStatus.CodeReview => 4,
        WorkItemStatus.WaitingForTesting or WorkItemStatus.Testing => 7,
        WorkItemStatus.ReadyForRelease => 8,
        _ => 11
    };

    public static readonly WorkItemStatus[] MainStages = [WorkItemStatus.Backlog, WorkItemStatus.Development,
        WorkItemStatus.CodeReview, WorkItemStatus.Testing, WorkItemStatus.ReadyForRelease, WorkItemStatus.Released];
    public static bool IsReturn(WorkItemStatus from, WorkItemStatus to) =>
        from is WorkItemStatus.CodeReview or WorkItemStatus.Testing && to == WorkItemStatus.WaitingForRework;
    public static Point[] Route(WorkItemStatus from, WorkItemStatus to)
    {
        var start = PackageMotion.Port(from); var end = PackageMotion.Port(to);
        if (IsReturn(from, to)) return [start, new(900, 365), new(720, 275), end];
        var a = AisleIndex(from); var b = AisleIndex(to);
        if (a == b) return [start, end];
        var points = new List<Point> { start, Aisle[a] };
        for (var k = a; k != b;) { k += Math.Sign(b - a); points.Add(Aisle[k]); }
        points.Add(end); return points.ToArray();
    }
    public static string Destination(string queue) => queue switch
    {
        "backlog" or "dependency" => "Backlog",
        "development" or "rework-active" or "rework-wait" => "Development",
        "review-active" or "review-wait" => "Code Review",
        "testing-active" or "testing-wait" => "Testing",
        "ready" => "Ready for Release", "delivered" => "Release", _ => throw new ArgumentException(queue)
    };
}
