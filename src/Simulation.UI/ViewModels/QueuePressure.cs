using Simulation.Application;
using Simulation.Core;

namespace Simulation.UI.ViewModels;

public enum QueuePressureKind { Normal, Growing, Constrained, Blocked, Batching, Dependency }
public sealed record QueuePressure(QueuePressureKind Kind, string Label)
{
    public string Trend { get; init; } = "No recent comparison";
    public static readonly QueuePressure Normal = new(QueuePressureKind.Normal, "");
    public string Marker => Kind switch { QueuePressureKind.Growing => "↑", QueuePressureKind.Constrained => "Ⅱ", QueuePressureKind.Blocked => "!", QueuePressureKind.Batching => "B", QueuePressureKind.Dependency => "D", _ => "" };
    public string Color => Kind switch { QueuePressureKind.Blocked => "#FFC27A", QueuePressureKind.Constrained => "#EDAD49", QueuePressureKind.Growing => "#FFE36A", _ => "#B7CDCB" };

    // Capacity is observed raw shared capacity, never a new stage allocation calculation.
    public static QueuePressure Classify(int count, double? configuredCapacity, string zeroLabel,
        IReadOnlyList<int> counts, IReadOnlyList<bool> exhausted, string constraintLabel, bool scheduled = false)
    {
        var trend = counts.Count < 2 ? "No recent comparison" : counts[^1] > counts[^2] ? "Growing" : counts[^1] < counts[^2] ? "Shrinking" : "Unchanged";
        QueuePressure Result(QueuePressureKind kind, string label) => new(kind, label) { Trend = trend };
        if (count <= 0) return Result(QueuePressureKind.Normal, "");
        if (configuredCapacity == 0) return Result(QueuePressureKind.Blocked, zeroLabel);
        if (scheduled) return Result(QueuePressureKind.Batching, "Scheduled batch");
        var growing = counts.Count >= 2 && counts[^1] > counts[^2];
        var persistent = counts.Count == 4 && counts.Zip(counts.Skip(1), (a, b) => b >= a).All(x => x) && counts[^1] > counts[0];
        if (persistent && exhausted.Count == 3 && exhausted.All(x => x)) return Result(QueuePressureKind.Constrained, constraintLabel);
        return growing ? Result(QueuePressureKind.Growing, "Queue growing") : Result(QueuePressureKind.Normal, "");
    }
    public static IReadOnlyDictionary<string, QueuePressure> From(LiveSimulation live)
    {
        var s = live.Session; var c = s.Configuration; var d = live.CurrentSnapshot;
        // A change recorded on N takes effect on N+1, whose zero-based snapshot.Day is N.
        // Do not mix pre/post intervention evidence.
        var changed = s.Changes.Count == 0 ? -1 : s.Changes[^1].Day;
        var days = s.Days.TakeLast(4).Where(x => x.Day >= changed).ToArray();
        QueuePressure Stage(int count, Func<DailySnapshot, int> select, double available, string zero,
            Func<DailySnapshot, bool> saturated, string reason) => Classify(count, available, zero,
                days.Select(select).ToArray(), days.TakeLast(3).Select(saturated).ToArray(), reason);
        bool DevelopersBusy(DailySnapshot x) => x.AvailableDeveloperCapacity > 0 && x.UsedDeveloperCapacity >= x.AvailableDeveloperCapacity - 1e-8;
        bool TestersBusy(DailySnapshot x) => x.AvailableTesterCapacity > 0 && x.UsedTesterCapacity >= x.AvailableTesterCapacity - 1e-8;
        return new Dictionary<string, QueuePressure>
        {
            ["backlog"] = Stage(Math.Max(0, d.BacklogCount - d.WaitingForDependencyCount), x => Math.Max(0, x.BacklogCount - x.WaitingForDependencyCount), c.Team.AvailableDeveloperCapacity, "No developer capacity", _ => false, ""),
            ["dependency"] = d.WaitingForDependencyCount > 0 ? new(QueuePressureKind.Dependency, "Dependency wait") : Normal,
            ["review-wait"] = Stage(d.WaitingForCodeReviewCount, x => x.WaitingForCodeReviewCount, c.Team.AvailableDeveloperCapacity,
                c.Team.DeveloperCount == 0 ? "No developers" : "No developer capacity", x => DevelopersBusy(x) && x.UsedReviewCapacity > 0, "Shared Dev capacity busy"),
            ["testing-wait"] = Stage(d.WaitingForTestingCount, x => x.WaitingForTestingCount, c.Team.AvailableTesterCapacity,
                c.Team.TesterCount == 0 ? "No testers" : "No tester capacity", TestersBusy, "Tester capacity busy"),
            ["rework-wait"] = Stage(d.WaitingForReworkCount, x => x.WaitingForReworkCount, c.Team.AvailableDeveloperCapacity,
                "No developer capacity", x => DevelopersBusy(x) && x.UsedReworkDeveloperCapacity > 0, "Shared Dev capacity busy"),
            ["ready"] = Classify(d.ReadyForReleaseCount, c.Release.Capacity, "Release capacity zero",
                days.Select(x => x.ReadyForReleaseCount).ToArray(), [], "", c.Release.Mode == ReleaseMode.Scheduled)
        };
    }
}
