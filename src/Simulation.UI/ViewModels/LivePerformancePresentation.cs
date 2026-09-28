using Simulation.Application;

namespace Simulation.UI.ViewModels;

public sealed record PerformanceComparisonRow(string Metric, string Before, string After, string Difference);

public static class LivePerformancePresentation
{
    public static string Number(double? value) => value.HasValue ? $"{value:0.0}" : "Unavailable";
    public static string Percent(double? value) => value.HasValue ? $"{value:P0}" : "Unavailable";
    public static string Trend(double? slope) => slope is not double value ? "Unavailable · needs 3 days"
        : Math.Abs(value) < .05 ? $"Stable ({value:+0.000;-0.000;0.000} items/day)"
        : $"{(value > 0 ? "Rising" : "Falling")} ({value:+0.000;-0.000;0.000} items/day)";
    public static string Period(PerformancePeriod p) =>
        (p.LastDay <= 0 ? "No simulated days before intervention" : $"Days {Math.Max(1, p.FirstDay)}–{p.LastDay}")
        + $" · {p.AvailableDays} of {p.ExpectedDays} days available";

    public static IReadOnlyList<MetricRow> Delivery(PerformancePeriod p) =>
    [new("Recent Throughput", p.AvailableDays == 0 ? "Unavailable" : $"{p.Throughput:0.0} items / 5 days", "Completions in the period / observed days × 5."),
     new("Recent Cycle Time", Number(p.CycleTime) + (p.CycleTime.HasValue ? " days" : ""), "Items completed in the period; full DevelopmentStartedDay to DoneDay, including time before the period.")];

    public static IReadOnlyList<MetricRow> Flow(PerformancePeriod p, bool rework)
    {
        var rows = new List<MetricRow>
        {
            new("Average WIP", p.AvailableDays == 0 ? "Unavailable" : Number(p.AverageWip) + " items", "Average end-of-day started, unfinished items, including queues."),
            new("WIP trend", Trend(p.WipTrend), "OLS slope over daily WIP. Stable means absolute slope < 0.05 items/day.")
        };
        void Queue(string label, QueuePerformance q) => rows.Add(new(label,
            p.AvailableDays == 0 ? "Unavailable" : $"Now {q.Current} · Avg {q.Average:0.0}\n{Trend(q.Trend)}",
            "Now: latest day. Avg: period average. Trend: OLS slope in items per simulated day."));
        Queue("Waiting for Code Review", p.Review); Queue("Waiting for Testing", p.Testing);
        if (rework) Queue("Waiting for Rework", p.Rework);
        return rows;
    }

    public static IReadOnlyList<MetricRow> Capacity(PerformancePeriod p) =>
    [new("Developer utilization", Percent(p.DeveloperUtilization), "Sum used / sum available capacity. Unavailable if available capacity is zero."),
     new("Tester utilization", Percent(p.TesterUtilization), "Sum used / sum available capacity. Unavailable if available capacity is zero.")];

    public static IReadOnlyList<MetricRow> Quality(PerformancePeriod p) =>
    [new("Defects", p.AvailableDays == 0 ? "Unavailable" : p.Defects.ToString(), "Defect discovery events during the selected period, including repeat discoveries."),
     new("Rework Capacity", Percent(p.ReworkCapacity), "Developer capacity used for Rework / total developer capacity used. Unavailable when no developer capacity was used.")];

    public static IReadOnlyList<PerformanceComparisonRow> Comparison(PerformanceComparison c)
    {
        var a = c.Before; var b = c.After;
        var rows = new List<PerformanceComparisonRow>();
        void Add(string name, double? before, double? after, bool percent = false)
        {
            if (a.AvailableDays == 0) before = null;
            if (b.AvailableDays == 0) after = null;
            var delta = after - before;
            rows.Add(new(name, percent ? Percent(before) : Number(before), percent ? Percent(after) : Number(after),
                delta is null ? "Unavailable" : percent ? $"{delta * 100:+0.0;-0.0;0.0} pp" : $"{delta:+0.0;-0.0;0.0}"));
        }
        Add("Throughput · items / 5 days", a.Throughput, b.Throughput);
        Add("Cycle Time · days", a.CycleTime, b.CycleTime);
        Add("Average WIP · items", a.AverageWip, b.AverageWip);
        Add("Average Code Review queue", a.Review.Average, b.Review.Average);
        Add("Average Testing queue", a.Testing.Average, b.Testing.Average);
        if (a.QualityRelevant || b.QualityRelevant) Add("Average Rework queue", a.Rework.Average, b.Rework.Average);
        Add("Developer utilization", a.DeveloperUtilization, b.DeveloperUtilization, true);
        Add("Tester utilization", a.TesterUtilization, b.TesterUtilization, true);
        if (a.QualityRelevant || b.QualityRelevant)
        {
            Add("Defects", a.Defects, b.Defects);
            Add("Rework Capacity", a.ReworkCapacity, b.ReworkCapacity, true);
        }
        return rows;
    }

    public static string Observations(PerformanceComparison c) => !c.Before.IsComplete || !c.After.IsComplete ? ""
        : $"Throughput was {c.Before.Throughput:0.0} before and {c.After.Throughput:0.0} after (items / 5 days).\nAverage Testing queue was {c.Before.Testing.Average:0.0} items before and {c.After.Testing.Average:0.0} after.";
}
