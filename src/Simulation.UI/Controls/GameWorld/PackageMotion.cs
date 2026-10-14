using Avalonia;
using Simulation.Core;
using Simulation.Sprites;
using Simulation.UI.ViewModels;

namespace Simulation.UI.Controls.GameWorld;

public sealed record ItemJourney(string ItemId, IReadOnlyList<WorkItemStatus> Stages);

/// <summary>Bounded, replace-on-snapshot motion. Consumes immutable observations, never a session.</summary>
public sealed partial class PackageMotion
{
    public const int MaximumPackages = 12;
    public const int MaximumStages = 32;
    private GameSceneState? previous;
    private IReadOnlyList<ItemJourney> journeys = [];
    private double started, duration;
    public int Count => journeys.Count;
    public void Cancel() { journeys = []; releaseCount = 0; }
    public void Observe(GameSceneState? next, double seconds, bool running, double speed)
    {
        if (ReferenceEquals(previous, next)) return;
        var old = previous; previous = next;
        if (next is null || old is null || next.SessionIdentity != old.SessionIdentity || (next.Day != old.Day && next.Day != old.Day + 1))
        { Cancel(); return; }
        if (next.Day == old.Day) return;
        if (!running) { Cancel(); return; }
        var coalescedDelivery = Release(seconds) is not null;
        ObserveRelease(old, next, seconds, speed);
        journeys = speed > 2 ? [] : next.Journeys.Where(j => j.Stages.Count is >= 2 and <= MaximumStages && (!coalescedDelivery || !j.Stages[^1].IsDelivered())).Take(MaximumPackages).ToArray();
        started = seconds; duration = .85 / speed;
    }
    public IReadOnlyList<SpriteObject> Sample(double seconds)
    {
        if (journeys.Count == 0) return [];
        var progress = Math.Clamp((seconds - started) / duration, 0, 1);
        if (progress >= 1) { journeys = []; return []; }
        return journeys.Where(j => !j.Stages[^1].IsDelivered() || Release(seconds) is not { Phase: ReleasePhase.Departing or ReleasePhase.Away or ReleasePhase.Returning }).Select((j, i) => new SpriteObject($"moving/{j.ItemId}", "package",
            Position(j, Math.Clamp((progress - i * .012) / (1 - i * .012), 0, 1)), .26)).ToArray();
    }
    public static WorldPoint Position(ItemJourney journey, double progress)
    {
        var path = new List<Point>();
        for (var i = 1; i < journey.Stages.Count; i++)
        {
            var from = Port(journey.Stages[i - 1]); var to = Port(journey.Stages[i]);
            // The outside aisle connects station ports without crossing furniture. Rework uses it in reverse.
            path.Add(from);
            var a = AisleIndex(journey.Stages[i - 1]); var b = AisleIndex(journey.Stages[i]);
            if (a == b) { path.Add(to); continue; }
            path.Add(Aisle[a]);
            for (var k = a; k != b;) { k += Math.Sign(b - a); path.Add(Aisle[k]); }
            path.Add(to);
        }
        if (path.Count == 0) return SpriteWorkshop.Ground(306, 253);
        var lengths = path.Zip(path.Skip(1), (a, b) => ((Vector)(b - a)).Length).ToArray();
        var t = Math.Clamp(progress, 0, 1); var distance = lengths.Sum() * (t * t * (3 - 2 * t));
        for (var i = 0; i < lengths.Length; i++)
        {
            if (distance <= lengths[i] && lengths[i] > 0)
            {
                var p = path[i] + (path[i + 1] - path[i]) * (distance / lengths[i]);
                return SpriteWorkshop.Ground(p.X, p.Y);
            }
            distance -= lengths[i];
        }
        return SpriteWorkshop.Ground(path[^1].X, path[^1].Y);
    }
    private static readonly Point[] Aisle = [new(365, 283), new(402, 266), new(555, 225), new(690, 258), new(793, 310), new(715, 350), new(633, 391), new(610, 445), new(448, 510), new(395, 465), new(365, 410), new(305, 405)];
    private static int AisleIndex(WorkItemStatus s) => s switch
    {
        WorkItemStatus.Backlog => 0,
        WorkItemStatus.Development or WorkItemStatus.Rework or WorkItemStatus.WaitingForRework => 2,
        WorkItemStatus.WaitingForCodeReview or WorkItemStatus.CodeReview => 4,
        WorkItemStatus.WaitingForTesting or WorkItemStatus.Testing => 7,
        WorkItemStatus.ReadyForRelease => 8,
        _ => 11
    };
    public static Point Port(WorkItemStatus s) => s switch
    {
        WorkItemStatus.Backlog => new(306, 253), WorkItemStatus.Development => new(553, 211),
        WorkItemStatus.WaitingForCodeReview => new(937, 282), WorkItemStatus.CodeReview => new(850, 292),
        WorkItemStatus.WaitingForTesting => new(670, 471), WorkItemStatus.Testing => new(741, 444),
        WorkItemStatus.WaitingForRework => new(639, 225), WorkItemStatus.Rework => new(612, 211),
        WorkItemStatus.ReadyForRelease => new(448, 492), _ => new(284, 354)
    };
}
