using System.Globalization;
using Simulation.Application;
using Simulation.Core;

namespace Simulation.UI.ViewModels;

/// <summary>Formatting only. Always start from numeric results, never previously formatted text.</summary>
public static class SimpleResultPresentation
{
    public static string Number(double? value) => value is null ? "n/a" : Rounded(value.Value).ToString("0.0", CultureInfo.InvariantCulture);
    public static string Percent(double? value) => value is null ? "n/a" : (value.Value * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
    public static string Change(double? value) => value is null ? "n/a" : Rounded(value.Value).ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture);
    private static double Rounded(double value) { var rounded = Math.Round(value, 1, MidpointRounding.AwayFromZero); return rounded == 0 ? 0 : rounded; }
    public static string QueueName(string name) => name switch
    {
        "WaitingForCodeReview" or "Waiting for Code Review" => "Code Review",
        "WaitingForTesting" or "Waiting for Testing" => "Testing",
        "WaitingForRework" or "Waiting for Rework" => "Rework",
        _ => name
    };
    public static string Queue(SimulationResult result)
    {
        var q = QueueObservation.From(result);
        return $"{QueueName(q.Name)}\n{q.Count} items";
    }
    public static IReadOnlyList<MetricRow> Run(SimulationResult? r) => r is null ? [] :
    [
        new("Throughput", Number(r.ThroughputPerFiveDays) + " items / 5 days", "How many Work Items are completed per five simulated working days."),
        new("Cycle Time", Number(r.AverageCycleTime) + " days", "Average time from Development start until Done. Completed items only."),
        new("Work in Progress", Number(r.AverageWip) + " items", "Average number of started Work Items that are not yet Done."),
        new("Largest Queue", Queue(r), "Largest observed end-of-day waiting queue during the simulation.")
    ];
    public static IReadOnlyList<SimpleMetric> Compare(ScenarioComparisonResult? comparison, Guid? alternative, string beforeQueue = "n/a", string afterQueue = "n/a")
    {
        if (comparison is null || alternative is null) return [];
        var rows = new List<SimpleMetric>();
        foreach (var metric in new[] { AnalysisMetric.ThroughputPerFiveDays, AnalysisMetric.AverageCycleTime, AnalysisMetric.AverageWip })
        {
            var row = comparison.Metrics.Single(r => r.Metric == metric);
            var before = row.Cells.Single(c => c.ScenarioId == comparison.ExperimentSnapshot.BaselineId);
            var after = row.Cells.Single(c => c.ScenarioId == alternative);
            var (name, unit) = metric switch {
                AnalysisMetric.ThroughputPerFiveDays => ("Throughput", " items / 5 days"),
                AnalysisMetric.AverageCycleTime => ("Cycle Time", " days"),
                _ => ("Work in Progress", " items")
            };
            rows.Add(new(name, Number(before.Distribution.P50) + unit, Number(after.Distribution.P50) + unit, Change(after.Delta.Absolute) + unit));
        }
        var beforeRun = comparison.Runs.Single(r => r.ScenarioSnapshot.Id == comparison.ExperimentSnapshot.BaselineId);
        var afterRun = comparison.Runs.Single(r => r.ScenarioSnapshot.Id == alternative);
        if (beforeRun.SingleRun is { } b && afterRun.SingleRun is { } a)
            rows.Add(new("Largest Queue", Queue(b), Queue(a), ""));
        else rows.Add(new("Largest Queue", beforeQueue, afterQueue, ""));
        return rows;
    }
    public static IReadOnlyList<CompareTextRow> More(ScenarioComparisonResult? c) => c is null ? [] : c.Metrics
        .Where(r => r.Metric is AnalysisMetric.AverageLeadTime or AnalysisMetric.AverageWaitingTime or AnalysisMetric.DeveloperUtilization or AnalysisMetric.TesterUtilization or AnalysisMetric.MaximumWaitingForCodeReviewQueue or AnalysisMetric.MaximumWaitingForTestingQueue or AnalysisMetric.MaximumWaitingForReworkQueue or AnalysisMetric.TotalDefectsFound or AnalysisMetric.TotalReworkEffort)
        .Select(r => new CompareTextRow(PresentationLabels.Label(r.Metric), r.Cells.Select(cell => new CompareTextCell(
            (ScenarioComparisonRunner.IsRatio(r.Metric) ? Percent(cell.Distribution.P50) : Number(cell.Distribution.P50)) + "\nChange: " +
            (cell.PercentagePoints is { } pp ? Change(pp) + " percentage points" : Change(cell.Delta.Absolute)))).ToArray())).ToArray();
}

public sealed record FlowObservationValues(QueueObservation Queue, double Throughput, double CycleTime, double Wip)
{
    public static FlowObservationValues From(SimulationResult r) => new(QueueObservation.From(r), r.ThroughputPerFiveDays, r.AverageCycleTime, r.AverageWip);
}

/// <summary>Deterministic presentation thresholds, never simulation rules or causal inference.</summary>
public static class SimpleFlowObservation
{
    public const int QueueChangeThreshold = 2;
    public const double MetricChangeThreshold = 0.1;
    private static bool Material(double a, double b) => Math.Round(Math.Abs(b - a), 10) >= MetricChangeThreshold;
    public static string Describe(FlowObservationValues before, FlowObservationValues after)
    {
        var b = before.Queue; var a = after.Queue;
        var bn = SimpleResultPresentation.QueueName(b.Name); var an = SimpleResultPresentation.QueueName(a.Name);
        if (b.Count > 0 && a.Count > 0 && bn != an)
            return $"The largest observed queue moved from {bn} to {an}.";
        if (Math.Abs(a.Count - b.Count) >= QueueChangeThreshold)
            return bn == an ? $"The largest observed {an} queue changed from {b.Count} to {a.Count} items."
                : $"The largest observed queue changed from {b.Count} to {a.Count} items.";
        if (Material(before.Throughput, after.Throughput))
            return $"Throughput changed from {SimpleResultPresentation.Number(before.Throughput)} to {SimpleResultPresentation.Number(after.Throughput)} items per 5 days.";
        // Stable queue is deliberately preferred to secondary metric changes, keeping the observation about flow.
        if (bn == an && b.Count == a.Count)
            return a.Count == 0 ? "No waiting queue was observed in either run." : $"The largest observed queue remained {an} at {a.Count} items.";
        if (SimpleResultPresentation.Number(before.Throughput) == SimpleResultPresentation.Number(after.Throughput))
        {
            if (Material(before.CycleTime, after.CycleTime))
                return $"Throughput remained {SimpleResultPresentation.Number(after.Throughput)} items per 5 days while average Cycle Time changed from {SimpleResultPresentation.Number(before.CycleTime)} to {SimpleResultPresentation.Number(after.CycleTime)} days.";
            if (Material(before.Wip, after.Wip))
                return $"Throughput remained {SimpleResultPresentation.Number(after.Throughput)} items per 5 days while average Work in Progress changed from {SimpleResultPresentation.Number(before.Wip)} to {SimpleResultPresentation.Number(after.Wip)} items.";
        }
        return "No notable change in the primary results.";
    }
}
