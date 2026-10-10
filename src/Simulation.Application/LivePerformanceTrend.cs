using Simulation.Core;

namespace Simulation.Application;

public enum LiveTrendMetric
{
    Throughput, CycleTime, AverageWip, ReviewQueue, TestingQueue, ReworkQueue,
    DeveloperUtilization, TesterUtilization, DevelopmentCapacity, DevelopmentWork, AvailableDevelopers, AvailableTesters, TechnicalDebtRatio, TechnicalDebt, DebtOverhead, DeliveryCost, SystemCost, SpecialistWorkWaiting, CompletionRate, DevelopmentCycleTime, ReleaseWaitTime, ReadyForRelease, WaitingForDependency
}
public sealed record LiveTrendMetricOption(LiveTrendMetric Metric, string Name, string Unit, bool Rolling);
public sealed record LiveTrendPoint(int Day, double? Value);
public sealed record LiveTrendSeries(IReadOnlyList<LiveTrendPoint> Points, IReadOnlyList<ConfigurationChange> Interventions);

/// <summary>Read-only O(days + items) projection. Display days and completion windows match LivePerformance.</summary>
public static class LivePerformanceTrend
{
    public static IReadOnlyList<LiveTrendMetricOption> Metrics { get; } = Array.AsReadOnly(new[] {
        new LiveTrendMetricOption(LiveTrendMetric.Throughput, "Throughput / 5 days", "items / 5 days", true),
        new(LiveTrendMetric.CycleTime, "Delivery Cycle Time", "simulated days", true),
        new(LiveTrendMetric.DeliveryCost, "Delivery Work Cost / Item", "capacity units / item", true),
        new(LiveTrendMetric.SystemCost, "System Cost / Released Item", "capacity units / released item", true),
        new(LiveTrendMetric.AverageWip, "Average WIP", "items", true),
        new(LiveTrendMetric.SpecialistWorkWaiting, "Specialist Work Waiting", "items", false),
        new(LiveTrendMetric.CompletionRate, "Completion Rate / 5 days", "items / 5 days", true),
        new(LiveTrendMetric.DevelopmentCycleTime, "Development Cycle Time", "simulated days", true),
        new(LiveTrendMetric.ReleaseWaitTime, "Release Wait Time", "simulated days", true),
        new(LiveTrendMetric.WaitingForDependency, "Waiting for Dependency", "items", false),
        new(LiveTrendMetric.ReadyForRelease, "Ready for Release", "items", false),
        new(LiveTrendMetric.ReviewQueue, "Waiting for Code Review", "items", false),
        new(LiveTrendMetric.TestingQueue, "Waiting for Testing", "items", false),
        new(LiveTrendMetric.ReworkQueue, "Waiting for Rework", "items", false),
        new(LiveTrendMetric.DeveloperUtilization, "Developer Utilization", "%", true),
        new(LiveTrendMetric.TesterUtilization, "Tester Utilization", "%", true),
        new(LiveTrendMetric.DevelopmentCapacity, "Development Capacity Used", "capacity units / day", false),
        new(LiveTrendMetric.DevelopmentWork, "Effective Development Work", "effort units / day", false),
        new(LiveTrendMetric.AvailableDevelopers, "Available Developer Capacity", "capacity units / day", false),
        new(LiveTrendMetric.AvailableTesters, "Available Tester Capacity", "capacity units / day", false),
        new(LiveTrendMetric.TechnicalDebtRatio, "Technical Debt Ratio", "% of developed scope", false),
        new(LiveTrendMetric.TechnicalDebt, "Technical Debt", "effort units", false),
        new(LiveTrendMetric.DebtOverhead, "Debt Overhead", "% added to new Development effort", false)
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
                LiveTrendMetric.SpecialistWorkWaiting => d.SpecialistWorkWaiting,
                LiveTrendMetric.WaitingForDependency => d.WaitingForDependencyCount,
                LiveTrendMetric.ReadyForRelease => d.ReadyForReleaseCount,
                LiveTrendMetric.ReviewQueue => d.WaitingForCodeReviewCount,
                LiveTrendMetric.TestingQueue => d.WaitingForTestingCount,
                LiveTrendMetric.ReworkQueue => d.WaitingForReworkCount,
                LiveTrendMetric.DevelopmentCapacity => d.UsedDevelopmentCapacity,
                LiveTrendMetric.AvailableDevelopers => d.AvailableDeveloperCapacity,
                LiveTrendMetric.AvailableTesters => d.AvailableTesterCapacity,
                LiveTrendMetric.TechnicalDebtRatio => 100 * (d.Debt?.State.Ratio ?? 0),
                LiveTrendMetric.TechnicalDebt => d.Debt?.State.Amount ?? 0,
                LiveTrendMetric.DebtOverhead => 100 * (d.Debt?.Overhead ?? 0),
                _ => d.DevelopmentWork
            })).ToArray();
            return new(daily, markers);
        }
        var completed = new int[count + 1]; var cycles = new double[count + 1];
        var costs = new double[count + 1]; var unknownCosts = new int[count + 1];
        var workCompleted = new int[count + 1]; var developmentCycles = new double[count + 1]; var releaseWait = new double[count + 1];
        foreach (var item in session.WorkItems)
        {
            if (item.DoneDay is { } done && done > 0 && done <= count)
            {
                completed[done]++; cycles[done] += done - item.DevelopmentStartedDay!.Value;
                releaseWait[done] += done - item.WorkCompletedDay!.Value;
            }
            if (item.WorkCompletedDay is { } ready && ready > 0 && ready <= count)
            {
                workCompleted[ready]++; developmentCycles[ready] += ready - item.DevelopmentStartedDay!.Value;
                if (item.DeliveryCost.IsComplete) costs[ready] += item.DeliveryCost.Total; else unknownCosts[ready]++;
            }
        }
        var points = new List<LiveTrendPoint>(count - first + 1);
        var usedDev = new double[count + 1]; var availableDev = new double[count + 1];
        var usedTest = new double[count + 1]; var availableTest = new double[count + 1]; var wip = new double[count + 1];
        var unknownSystemCosts = new int[count + 1];
        var changes = session.Changes.OrderBy(c => c.Day).ToArray();
        var changeIndex = 0; var configuration = session.InitialConfiguration;
        for (var i = 0; i < count; i++)
        {
            var d = days[i];
            while (changeIndex < changes.Length && changes[changeIndex].Day <= d.Day) configuration = changes[changeIndex++].After;
            unknownSystemCosts[i + 1] = unknownSystemCosts[i] + (SystemCost.HasExactHistory(d, configuration) ? 0 : 1);
            usedDev[i + 1] = usedDev[i] + d.UsedDeveloperCapacity;
            availableDev[i + 1] = availableDev[i] + d.AvailableDeveloperCapacity;
            usedTest[i + 1] = usedTest[i] + d.UsedTesterCapacity;
            availableTest[i + 1] = availableTest[i] + d.AvailableTesterCapacity;
            wip[i + 1] = wip[i] + d.TotalWip;
            completed[i + 1] += completed[i]; cycles[i + 1] += cycles[i];
            workCompleted[i + 1] += workCompleted[i]; developmentCycles[i + 1] += developmentCycles[i]; releaseWait[i + 1] += releaseWait[i];
            costs[i + 1] += costs[i]; unknownCosts[i + 1] += unknownCosts[i];
        }
        // Prefix differences retain the lookback before the visible range and exact zero-capacity windows.
        for (var day = first; day <= count; day++)
        {
            var start = Math.Max(0, day - window); var duration = day - start;
            var completions = completed[day] - completed[start];
            var readyCount = workCompleted[day] - workCompleted[start];
            double Sum(double[] values) => values[day] - values[start];
            double? value = metric switch {
                LiveTrendMetric.Throughput => 5.0 * completions / duration,
                LiveTrendMetric.CycleTime => completions == 0 ? null : Sum(cycles) / completions,
                LiveTrendMetric.DeliveryCost => readyCount == 0 || unknownCosts[day] != unknownCosts[start] ? null : Sum(costs) / readyCount,
                LiveTrendMetric.CompletionRate => 5.0 * readyCount / duration,
                LiveTrendMetric.DevelopmentCycleTime => readyCount == 0 ? null : Sum(developmentCycles) / readyCount,
                LiveTrendMetric.ReleaseWaitTime => completions == 0 ? null : Sum(releaseWait) / completions,
                LiveTrendMetric.SystemCost => completions == 0 || unknownSystemCosts[day] != unknownSystemCosts[start] ? null : (Sum(usedDev) + Sum(usedTest)) / completions,
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
