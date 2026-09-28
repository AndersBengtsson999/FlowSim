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
            quality || defects > 0 || days.Any(d => d.UsedReworkDeveloperCapacity > 0 || d.WaitingForReworkCount > 0 || d.ReworkCount > 0));
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
