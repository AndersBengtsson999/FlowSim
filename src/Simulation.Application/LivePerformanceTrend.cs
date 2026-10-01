using Simulation.Core;

namespace Simulation.Application;

public enum LiveTrendMetric
{
    Throughput, CycleTime, AverageWip, ReviewQueue, TestingQueue, ReworkQueue,
    DeveloperUtilization, TesterUtilization, DevelopmentCapacity, DevelopmentWork, AvailableDevelopers, AvailableTesters
}
public sealed record LiveTrendMetricOption(LiveTrendMetric Metric, string Name, string Unit, bool Rolling);
public sealed record LiveTrendPoint(int Day, double? Value);
public sealed record LiveTrendSeries(IReadOnlyList<LiveTrendPoint> Points, IReadOnlyList<ConfigurationChange> Interventions);

/// <summary>Read-only O(days + items) projection. Display days and completion windows match LivePerformance.</summary>
public static class LivePerformanceTrend
{
    public static IReadOnlyList<LiveTrendMetricOption> Metrics { get; } = Array.AsReadOnly(new[] {
        new LiveTrendMetricOption(LiveTrendMetric.Throughput, "Throughput / 5 days", "items / 5 days", true),
        new(LiveTrendMetric.CycleTime, "Cycle Time", "simulated days", true),
        new(LiveTrendMetric.AverageWip, "Average WIP", "items", true),
        new(LiveTrendMetric.ReviewQueue, "Waiting for Code Review", "items", false),
        new(LiveTrendMetric.TestingQueue, "Waiting for Testing", "items", false),
        new(LiveTrendMetric.ReworkQueue, "Waiting for Rework", "items", false),
        new(LiveTrendMetric.DeveloperUtilization, "Developer Utilization", "%", true),
        new(LiveTrendMetric.TesterUtilization, "Tester Utilization", "%", true),
        new(LiveTrendMetric.DevelopmentCapacity, "Development Capacity Used", "capacity units / day", false),
        new(LiveTrendMetric.DevelopmentWork, "Effective Development Work", "effort units / day", false),
        new(LiveTrendMetric.AvailableDevelopers, "Available Developer Capacity", "capacity units / day", false),
        new(LiveTrendMetric.AvailableTesters, "Available Tester Capacity", "capacity units / day", false)
    });

    public static LiveTrendSeries Project(SimulationSession session, LiveTrendMetric metric, int window = 20, int? range = 20)
    {
        if (window <= 0 || range is <= 0) throw new ArgumentOutOfRangeException(nameof(window));
        var days = session.Days; var count = days.Count;
        if (count == 0) return new([], session.Changes.Where(c => c.Day == 0).ToArray());
        var first = Math.Max(1, count - (range ?? count) + 1);
        var markers = session.Changes.Where(c => c.Day >= (first == 1 ? 0 : first) && c.Day <= count).ToArray();
        var option = Metrics.Single(m => m.Metric == metric);
        if (!option.Rolling)
        {
            var daily = days.Skip(first - 1).Select(d => new LiveTrendPoint(d.Day + 1, metric switch {
                LiveTrendMetric.ReviewQueue => d.WaitingForCodeReviewCount,
                LiveTrendMetric.TestingQueue => d.WaitingForTestingCount,
                LiveTrendMetric.ReworkQueue => d.WaitingForReworkCount,
                LiveTrendMetric.DevelopmentCapacity => d.UsedDevelopmentCapacity,
                LiveTrendMetric.AvailableDevelopers => d.AvailableDeveloperCapacity,
                LiveTrendMetric.AvailableTesters => d.AvailableTesterCapacity,
                _ => d.DevelopmentWork
            })).ToArray();
            return new(daily, markers);
        }
        var completed = new int[count + 1]; var cycles = new double[count + 1];
        if (metric is LiveTrendMetric.Throughput or LiveTrendMetric.CycleTime)
            foreach (var item in session.WorkItems)
                if (item.DoneDay is { } done && done > 0 && done <= count)
                { completed[done]++; cycles[done] += done - item.DevelopmentStartedDay!.Value; }
        var points = new List<LiveTrendPoint>(count - first + 1);
        var usedDev = new double[count + 1]; var availableDev = new double[count + 1];
        var usedTest = new double[count + 1]; var availableTest = new double[count + 1]; var wip = new double[count + 1];
        for (var i = 0; i < count; i++)
        {
            var d = days[i];
            usedDev[i + 1] = usedDev[i] + d.UsedDeveloperCapacity;
            availableDev[i + 1] = availableDev[i] + d.AvailableDeveloperCapacity;
            usedTest[i + 1] = usedTest[i] + d.UsedTesterCapacity;
            availableTest[i + 1] = availableTest[i] + d.AvailableTesterCapacity;
            wip[i + 1] = wip[i] + d.TotalWip;
            completed[i + 1] += completed[i]; cycles[i + 1] += cycles[i];
        }
        // Prefix differences retain the lookback before the visible range and exact zero-capacity windows.
        for (var day = first; day <= count; day++)
        {
            var start = Math.Max(0, day - window); var duration = day - start;
            var completions = completed[day] - completed[start];
            double Sum(double[] values) => values[day] - values[start];
            double? value = metric switch {
                LiveTrendMetric.Throughput => 5.0 * completions / duration,
                LiveTrendMetric.CycleTime => completions == 0 ? null : Sum(cycles) / completions,
                LiveTrendMetric.AverageWip => Sum(wip) / duration,
                LiveTrendMetric.DeveloperUtilization => Sum(availableDev) > 0 ? 100 * Sum(usedDev) / Sum(availableDev) : null,
                LiveTrendMetric.TesterUtilization => Sum(availableTest) > 0 ? 100 * Sum(usedTest) / Sum(availableTest) : null,
                _ => throw new ArgumentOutOfRangeException(nameof(metric))
            };
            points.Add(new(day, value));
        }
        return new(points.AsReadOnly(), markers);
    }
}
