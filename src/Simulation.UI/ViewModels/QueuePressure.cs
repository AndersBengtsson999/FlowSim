using Simulation.Application;
using Simulation.Core;

namespace Simulation.UI.ViewModels;

public enum QueuePressureKind { Normal, Growing, Constrained, Blocked, Batching, Dependency, Shrinking }
public sealed record QueuePressure(QueuePressureKind Kind, string Label)
{
    public int ObservedDays { get; init; }
    public string Period { get; init; } = "No completed-day history";
    public string Evidence { get; init; } = "Insufficient evidence to classify capacity pressure.";
    public string Condition => Kind switch { QueuePressureKind.Constrained => "Sustained pressure · potential bottleneck", QueuePressureKind.Growing => "Recent queue growth", QueuePressureKind.Shrinking => "Queue shrinking", QueuePressureKind.Blocked => "No configured capacity", QueuePressureKind.Batching => "Scheduled accumulation", QueuePressureKind.Dependency => "Dependency waiting", _ => "No sustained pressure established" };
    public const string Thresholds = "Growing: last count exceeds the previous count. Shrinking: last count is lower. Sustained pressure: four nondecreasing daily counts with net growth, plus full observed pool usage on all last three days; review/rework also require positive work in that stage. Zero configured capacity is separate. Scheduled release accumulation is batching. Evidence restarts after an intervention; queue size alone is not a diagnosis.";
    public string Trend { get; init; } = "No recent comparison";
    public static readonly QueuePressure Normal = new(QueuePressureKind.Normal, "");
    public string Marker => Kind switch { QueuePressureKind.Growing => "↑", QueuePressureKind.Shrinking => "↓", QueuePressureKind.Constrained => "Ⅱ", QueuePressureKind.Blocked => "!", QueuePressureKind.Batching => "B", QueuePressureKind.Dependency => "D", _ => "" };
    public string Color => Kind switch { QueuePressureKind.Shrinking => "#55C995", QueuePressureKind.Blocked => "#FFC27A", QueuePressureKind.Constrained => "#EDAD49", QueuePressureKind.Growing => "#FFE36A", _ => "#B7CDCB" };

    // Capacity is observed raw shared capacity, never a new stage allocation calculation.
    public static QueuePressure Classify(int count, double? configuredCapacity, string zeroLabel,
        IReadOnlyList<int> counts, IReadOnlyList<bool> exhausted, string constraintLabel, bool scheduled = false)
    {
        var trend = counts.Count < 2 ? "No recent comparison" : counts[^1] > counts[^2] ? "Growing" : counts[^1] < counts[^2] ? "Shrinking" : "Unchanged";
        QueuePressure Result(QueuePressureKind kind, string label) => new(kind, label) { Trend = trend, ObservedDays = counts.Count,
            Evidence = kind switch {
                QueuePressureKind.Blocked => "Waiting work and zero configured capacity. A new configuration takes effect on the next simulated day.",
                QueuePressureKind.Batching => "Waiting work under a scheduled release policy; accumulation alone is not evidence of a capacity bottleneck.",
                QueuePressureKind.Constrained => "Queue did not shrink across four daily snapshots and grew overall; the relevant capacity pool was fully used on each of the last three days. Potential constraint, not a proven root cause.",
                QueuePressureKind.Growing => "Queue increased since the preceding completed day. Sustained capacity pressure is not yet established.",
                QueuePressureKind.Shrinking => "Fewer items than on the preceding completed day. This is an improving queue trend, not proof of higher throughput.",
                _ => counts.Count < 4 ? "Fewer than four post-change observations; no sustained pressure established." : "The sustained-pressure threshold is not met. A large queue alone does not identify a bottleneck."
            } };
        if (count <= 0) return Result(QueuePressureKind.Normal, "");
        if (configuredCapacity == 0) return Result(QueuePressureKind.Blocked, zeroLabel);
        if (scheduled) return Result(QueuePressureKind.Batching, "Scheduled batch");
        var growing = counts.Count >= 2 && counts[^1] > counts[^2];
        var persistent = counts.Count == 4 && counts.Zip(counts.Skip(1), (a, b) => b >= a).All(x => x) && counts[^1] > counts[0];
        if (persistent && exhausted.Count == 3 && exhausted.All(x => x)) return Result(QueuePressureKind.Constrained, constraintLabel);
        return growing ? Result(QueuePressureKind.Growing, "Queue growing") : trend == "Shrinking" ? Result(QueuePressureKind.Shrinking, "Queue shrinking") : Result(QueuePressureKind.Normal, "");
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
                days.Select(select).ToArray(), days.TakeLast(3).Select(saturated).ToArray(), reason) with { Period = Period(days) };
        static string Period(DailySnapshot[] days) => days.Length == 0 ? "No completed-day history since change" : $"Queue evidence: Days {days[0].Day + 1}–{days[^1].Day + 1} ({days.Length} observations)";
        bool DevelopersBusy(DailySnapshot x) => x.AvailableDeveloperCapacity > 0 && x.UsedDeveloperCapacity >= x.AvailableDeveloperCapacity - 1e-8;
        bool TestersBusy(DailySnapshot x) => x.AvailableTesterCapacity > 0 && x.UsedTesterCapacity >= x.AvailableTesterCapacity - 1e-8;
        return new Dictionary<string, QueuePressure>
        {
            ["backlog"] = Stage(Math.Max(0, d.BacklogCount - d.WaitingForDependencyCount), x => Math.Max(0, x.BacklogCount - x.WaitingForDependencyCount), c.Team.AvailableDeveloperCapacity, "No developer capacity", _ => false, ""),
            ["dependency"] = d.WaitingForDependencyCount > 0 ? new(QueuePressureKind.Dependency, "Dependency wait") { Period = $"Current Day {s.CurrentDay}", Evidence = "Backlog items with unresolved dependencies; not a capacity diagnosis." } : Normal,
            ["review-wait"] = Stage(d.WaitingForCodeReviewCount, x => x.WaitingForCodeReviewCount, c.Team.AvailableDeveloperCapacity,
                c.Team.DeveloperCount == 0 ? "No developers" : "No developer capacity", x => DevelopersBusy(x) && x.UsedReviewCapacity > 0, "Shared Dev capacity busy"),
            ["testing-wait"] = Stage(d.WaitingForTestingCount, x => x.WaitingForTestingCount, c.Team.AvailableTesterCapacity,
                c.Team.TesterCount == 0 ? "No testers" : "No tester capacity", TestersBusy, "Tester capacity busy"),
            ["rework-wait"] = Stage(d.WaitingForReworkCount, x => x.WaitingForReworkCount, c.Team.AvailableDeveloperCapacity,
                "No developer capacity", x => DevelopersBusy(x) && x.UsedReworkDeveloperCapacity > 0, "Shared Dev capacity busy"),
            ["ready"] = Classify(d.ReadyForReleaseCount, c.Release.Capacity, "Release capacity zero",
                days.Select(x => x.ReadyForReleaseCount).ToArray(), [], "", c.Release.Mode == ReleaseMode.Scheduled) with { Period = Period(days) }
        };
    }
}
