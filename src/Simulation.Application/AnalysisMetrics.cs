using System.Collections.ObjectModel;
using Simulation.Core;

namespace Simulation.Application;

public enum AnalysisMetric
{
    CompletedWorkItems, ThroughputPerFiveDays, AverageLeadTime, AverageCycleTime, AverageActiveTime,
    AverageWaitingTime, AverageWip, DeveloperUtilization, TesterUtilization,
    MaximumWaitingForCodeReviewQueue, MaximumWaitingForTestingQueue, MaximumWaitingForReworkQueue,
    TotalDefectsFound, TotalReworkEffort, ReworkDeveloperCapacityShare,
    AverageAvailableDeveloperCapacity, AverageUsedDeveloperCapacity, AverageAvailableTesterCapacity, AverageUsedTesterCapacity,
    DevelopmentWipSaturation, CodeReviewWipSaturation, TestingWipSaturation, ReworkWipSaturation,
    AverageWaitingForCodeReviewQueue, AverageWaitingForTestingQueue, AverageWaitingForReworkQueue, AverageBacklog
}
public sealed record AnalysisMeasurements(int StartDay, int EndDay, IReadOnlyDictionary<AnalysisMetric, double?> Values);

/// <summary>Measurement only: never changes execution. Window [warmUp, horizon), completion boundaries (warmUp, horizon].</summary>
public static class AnalysisMetrics
{
    public static AnalysisMeasurements Measure(SimulationRequest scenario, SimulationResult result, int warmUpDays)
    {
        if (warmUpDays < 0 || warmUpDays >= result.SimulationDays)
            throw new ScenarioValidationException("Warm-Up Days must be at least zero and less than Simulation Days.");
        var days = result.Days.Where(d => d.Day >= warmUpDays).ToArray();
        var completed = result.WorkItems.Where(w => w.DoneDay > warmUpDays && w.DoneDay <= result.SimulationDays).ToArray();
        double? ItemMean(Func<WorkItemResult, double> f) => completed.Length == 0 ? null : completed.Average(f);
        double Ratio(double used, double available) => available == 0 ? 0 : used / available;
        var dev = days.Sum(d => d.UsedDeveloperCapacity);
        var rework = days.Sum(d => d.UsedReworkDeveloperCapacity);
        var v = new Dictionary<AnalysisMetric, double?>
        {
            [AnalysisMetric.CompletedWorkItems] = completed.Length,
            [AnalysisMetric.ThroughputPerFiveDays] = completed.Length * 5.0 / days.Length,
            [AnalysisMetric.AverageLeadTime] = ItemMean(w => w.LeadTime!.Value),
            [AnalysisMetric.AverageCycleTime] = ItemMean(w => w.CycleTime!.Value),
            [AnalysisMetric.AverageActiveTime] = ItemMean(w => w.ActiveTime),
            [AnalysisMetric.AverageWaitingTime] = ItemMean(w => w.WaitingTime),
            [AnalysisMetric.AverageWip] = days.Average(d => d.TotalWip),
            [AnalysisMetric.DeveloperUtilization] = Ratio(dev, days.Sum(d => d.AvailableDeveloperCapacity)),
            [AnalysisMetric.TesterUtilization] = Ratio(days.Sum(d => d.UsedTesterCapacity), days.Sum(d => d.AvailableTesterCapacity)),
            [AnalysisMetric.MaximumWaitingForCodeReviewQueue] = days.Max(d => d.WaitingForCodeReviewCount),
            [AnalysisMetric.MaximumWaitingForTestingQueue] = days.Max(d => d.WaitingForTestingCount),
            [AnalysisMetric.AverageAvailableDeveloperCapacity] = days.Average(d => d.AvailableDeveloperCapacity),
            [AnalysisMetric.AverageUsedDeveloperCapacity] = days.Average(d => d.UsedDeveloperCapacity),
            [AnalysisMetric.AverageAvailableTesterCapacity] = days.Average(d => d.AvailableTesterCapacity),
            [AnalysisMetric.AverageUsedTesterCapacity] = days.Average(d => d.UsedTesterCapacity),
            [AnalysisMetric.DevelopmentWipSaturation] = days.Count(d => d.DevelopmentWip >= scenario.DevelopmentWipLimit) / (double)days.Length,
            [AnalysisMetric.CodeReviewWipSaturation] = days.Count(d => d.ReviewWip >= scenario.CodeReviewWipLimit) / (double)days.Length,
            [AnalysisMetric.TestingWipSaturation] = days.Count(d => d.TestingWip >= scenario.TestingWipLimit) / (double)days.Length,
            [AnalysisMetric.AverageWaitingForCodeReviewQueue] = days.Average(d => d.WaitingForCodeReviewCount),
            [AnalysisMetric.AverageWaitingForTestingQueue] = days.Average(d => d.WaitingForTestingCount),
            [AnalysisMetric.AverageBacklog] = days.Average(d => d.BacklogCount)
        };
        if (scenario.Quality.Enabled)
        {
            v[AnalysisMetric.MaximumWaitingForReworkQueue] = days.Max(d => d.WaitingForReworkCount);
            v[AnalysisMetric.AverageWaitingForReworkQueue] = days.Average(d => d.WaitingForReworkCount);
            v[AnalysisMetric.TotalDefectsFound] = result.WorkItems.Sum(w => w.Events.Count(e => e.EventType == WorkItemEventType.DefectFound && e.Day > warmUpDays && e.Day <= result.SimulationDays));
            v[AnalysisMetric.TotalReworkEffort] = rework;
            v[AnalysisMetric.ReworkDeveloperCapacityShare] = Ratio(rework, dev);
            v[AnalysisMetric.ReworkWipSaturation] = days.Count(d => d.ReworkWip >= scenario.Quality.ReworkWipLimit) / (double)days.Length;
        }
        return new(warmUpDays, result.SimulationDays, new ReadOnlyDictionary<AnalysisMetric, double?>(v));
    }
}
