using Simulation.Core;

namespace Simulation.Application;

public sealed record QueuePerformance(int Current, double Average, double? Trend);

/// <summary>Inclusive, one-based display days; snapshots retain their zero-based Core day index.</summary>
public sealed record PerformancePeriod(int FirstDay, int LastDay, int AvailableDays, int ExpectedDays,
    int Completed, double Throughput, double? CycleTime, double AverageWip, double? WipTrend,
    QueuePerformance Review, QueuePerformance Testing, QueuePerformance Rework,
    double? DeveloperUtilization, double? TesterUtilization, int Defects, double? ReworkCapacity,
    bool QualityRelevant)
{
    public int WorkCompleted { get; init; }
    public double CompletionRate { get; init; }
    public double? DevelopmentCycleTime { get; init; }
    public double? ReleaseWaitTime { get; init; }
    public QueuePerformance WaitingForDependency { get; init; } = new(0,0,null);
    public bool DependenciesRelevant { get; init; }
    public QueuePerformance ReadyForRelease { get; init; } = new(0,0,null);
    public SystemCost? ConsumedSystemCapacity { get; init; }
    public double? SystemCostPerReleasedItem => Completed > 0 ? ConsumedSystemCapacity?.Total / Completed : null;
    // Compatibility property names: terminal output is now Released (plus historical Done).
    public double? SystemCostPerDoneItem => SystemCostPerReleasedItem;
    public DeliveryCost? AverageDeliveryCost { get; init; }
    public double? DeliveryWorkCostPerItem => AverageDeliveryCost?.Total;
    // Work completion population, now Ready for Release; never the release cohort.
    public double? DeliveryCostPerDoneItem => DeliveryWorkCostPerItem;
    public double EndDebtRatio { get; init; }
    public double EndDebtOverhead { get; init; }
    public bool DebtRelevant { get; init; }
    public bool IsComplete => AvailableDays == ExpectedDays;
}

public sealed record PerformanceComparison(PerformancePeriod Before, PerformancePeriod After,
    int OtherInterventions)
{
    public double? DeveloperPercentagePoints => 100 * (After.DeveloperUtilization - Before.DeveloperUtilization);
    public double? TesterPercentagePoints => 100 * (After.TesterUtilization - Before.TesterUtilization);
}

public static class LivePerformance
{
    public static PerformancePeriod Rolling(SimulationSession session, int window = 20)
    {
        Validate(window);
        return Period(session, Math.Max(1, session.CurrentDay - window + 1), Math.Max(1, session.CurrentDay));
    }

    public static PerformanceComparison Compare(SimulationSession session, ConfigurationChange change, int window)
    {
        Validate(window);
        var selected = session.Changes.FirstOrDefault(c => ReferenceEquals(c, change))
            ?? session.Changes.FirstOrDefault(c => c == change)
            ?? throw new ArgumentException("Intervention is not in this timeline.", nameof(change));
        return new(Period(session, change.Day - window + 1, change.Day),
            Period(session, change.Day + 1, change.Day + window),
            session.Changes.Count(c => !ReferenceEquals(c, selected) && c.Day >= change.Day - window && c.Day < change.Day + window));
    }

    public static PerformancePeriod Period(SimulationSession session, int firstDay, int lastDay)
    {
        if (lastDay < firstDay) throw new ArgumentOutOfRangeException(nameof(lastDay));
        var days = session.Days.Where(d => d.Day + 1 >= firstDay && d.Day + 1 <= lastDay).ToArray();
        var completed = session.WorkItems.Where(w => w.DoneDay >= firstDay && w.DoneDay <= lastDay).ToArray();
        var workCompleted = session.WorkItems.Where(w => w.WorkCompletedDay >= firstDay && w.WorkCompletedDay <= lastDay).ToArray();
        double Average(Func<DailySnapshot, double> value) => days.Length == 0 ? 0 : days.Average(value);
        double? Trend(Func<DailySnapshot, double> value) => Slope(days.Select(d => ((double)d.Day, value(d))));
        QueuePerformance Queue(Func<DailySnapshot, int> value) => new(days.Length == 0 ? 0 : value(days[^1]), Average(d => value(d)), Trend(d => value(d)));
        var used = days.Sum(d => d.UsedDeveloperCapacity);
        var defects = session.WorkItems.SelectMany(w => w.Events).Count(e => e.EventType == WorkItemEventType.DefectFound && e.Day >= firstDay && e.Day <= lastDay);
        // Include quality that was enabled during the period, even if subsequently disabled.
        var quality = days.Any(d => (session.Changes.LastOrDefault(c => c.Day <= d.Day)?.After ?? session.InitialConfiguration).Quality.Enabled);
        return new(firstDay, lastDay, days.Length, lastDay - firstDay + 1, completed.Length,
            days.Length == 0 ? 0 : 5.0 * completed.Length / days.Length,
            completed.Length == 0 ? null : completed.Average(w => (double)(w.DoneDay!.Value - w.DevelopmentStartedDay!.Value)),
            Average(d => d.TotalWip), Trend(d => d.TotalWip),
            Queue(d => d.WaitingForCodeReviewCount), Queue(d => d.WaitingForTestingCount), Queue(d => d.WaitingForReworkCount),
            Ratio(used, days.Sum(d => d.AvailableDeveloperCapacity)),
            Ratio(days.Sum(d => d.UsedTesterCapacity), days.Sum(d => d.AvailableTesterCapacity)),
            defects, Ratio(days.Sum(d => d.UsedReworkDeveloperCapacity), used),
            quality || defects > 0 || days.Any(d => d.UsedReworkDeveloperCapacity > 0 || d.WaitingForReworkCount > 0 || d.ReworkCount > 0))
        { WorkCompleted = workCompleted.Length, CompletionRate = days.Length == 0 ? 0 : 5.0 * workCompleted.Length / days.Length,
          DevelopmentCycleTime = workCompleted.Length == 0 ? null : workCompleted.Average(w => (double)(w.WorkCompletedDay!.Value - w.DevelopmentStartedDay!.Value)),
          ReleaseWaitTime = completed.Length == 0 ? null : completed.Average(w => (double)(w.DoneDay!.Value - w.WorkCompletedDay!.Value)),
          WaitingForDependency = Queue(d => d.WaitingForDependencyCount),
          DependenciesRelevant = days.Any(d => d.WaitingForDependencyCount > 0 || (session.Changes.LastOrDefault(c => c.Day <= d.Day)?.After ?? session.InitialConfiguration).ResidualDependencies.Rate > 0),
          ReadyForRelease = Queue(d => d.ReadyForReleaseCount),
          ConsumedSystemCapacity = SystemCost.Sum(days, session), AverageDeliveryCost = AverageCost(workCompleted), EndDebtRatio = days.LastOrDefault()?.Debt?.State.Ratio ?? 0, EndDebtOverhead = days.LastOrDefault()?.Debt?.Overhead ?? 0,
          DebtRelevant = days.Any(d => d.Debt is { } debt && (debt.State.Amount > 0 || debt.Created > 0 || debt.RepaymentCapacity > 0)) };
    }

    private static DeliveryCost? AverageCost(IReadOnlyList<WorkItem> completed)
    {
        // Do not silently average only the known subset of a completion cohort.
        if (completed.Count == 0 || completed.Any(w => !w.DeliveryCost.IsComplete)) return null;
        return new(completed.Average(w => w.DeliveryCost.Development), completed.Average(w => w.DeliveryCost.CodeReview),
            completed.Average(w => w.DeliveryCost.Rework), completed.Average(w => w.DeliveryCost.Testing));
    }

    public static double? Slope(IEnumerable<(double Day, double Value)> observations)
    {
        var points = observations.ToArray();
        if (points.Length < 3) return null;
        var x = points.Average(p => p.Day); var y = points.Average(p => p.Value);
        var denominator = points.Sum(p => (p.Day - x) * (p.Day - x));
        return denominator == 0 ? null : points.Sum(p => (p.Day - x) * (p.Value - y)) / denominator;
    }

    private static double? Ratio(double used, double available) => available > 0 ? used / available : null;
    private static void Validate(int window) { if (window <= 0) throw new ArgumentOutOfRangeException(nameof(window)); }
}
