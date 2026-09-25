using Simulation.Core;

namespace Simulation.Application;

public sealed record ValidationExperiment(string Name, SimulationRequest ScenarioA, SimulationRequest ScenarioB);
public sealed record ValidationComparison(ValidationExperiment Experiment, AnalysisMeasurements A, AnalysisMeasurements B,
    IReadOnlyDictionary<AnalysisMetric, MetricDelta> Deltas, bool WeakResponse, string Observation);
public sealed record ModelValidationResult(int WarmUpDays, IReadOnlyList<ValidationComparison> Comparisons);

public static class ValidationScenarios
{
    public const int DefaultWarmUpDays = 50;
    public static SimulationRequest SteadyFlow() => new()
    {
        Name = "Steady Flow Validation", SimulationDays = 250, NumberOfWorkItems = 1000,
        DevelopmentWipLimit = 10, CodeReviewWipLimit = 5, TestingWipLimit = 10
    };
    public static SimulationRequest DefectStressBase() => SteadyFlow() with
    {
        Name = "Defect stress validation", Quality = new DefectSettings
        {
            Enabled = true, CodeReviewReworkEffortDistribution = new TriangularEffort(1, 3, 6),
            TestingReworkEffortDistribution = new TriangularEffort(2, 5, 10)
        }
    };
    public static IReadOnlyList<ValidationExperiment> Experiments(int seed = 12345)
    {
        var steady = SteadyFlow() with { RandomSeed = seed };
        var testing = steady with { TesterCount = 1, TestingEffort = 10 };
        var development = steady with { DeveloperCount = 1, TesterCount = 20, TestingWipLimit = 20, TestingEffort = 1 };
        var defects = DefectStressBase() with { RandomSeed = seed };
        return Array.AsReadOnly(new[]
        {
            new ValidationExperiment("Testing Constraint", testing, testing with { TesterCount = 10, TestingEffort = 1 }),
            new ValidationExperiment("Development Capacity", development, development with { DeveloperCount = 10 }),
            new ValidationExperiment("Defect/Rework Stress", defects, defects with { Quality = defects.Quality with { CodeReviewDefectProbability = .7, TestingDefectProbability = .7 } })
        });
    }
}
public sealed class ModelValidationRunner
{
    // Investigation signal only, exclusive to predefined extremes. Zero-to-positive is always distinguishable.
    public static bool IsWeakResponse(AnalysisMeasurements a, AnalysisMeasurements b)
    {
        var keys = new[] { AnalysisMetric.ThroughputPerFiveDays, AnalysisMetric.AverageCycleTime,
            AnalysisMetric.AverageWaitingTime, AnalysisMetric.AverageWip, AnalysisMetric.MaximumWaitingForTestingQueue,
            AnalysisMetric.TotalDefectsFound, AnalysisMetric.TotalReworkEffort };
        bool Similar(double x, double y) => x == 0 ? y == 0 : Math.Abs(y - x) / Math.Abs(x) < .05;
        return keys.Where(k => a.Values.ContainsKey(k) && b.Values.ContainsKey(k))
            .All(k => a.Values[k] is { } x && b.Values[k] is { } y ? Similar(x, y) : a.Values[k] == b.Values[k]);
    }
    public ModelValidationResult Run(int seed = 12345, int warmUpDays = 50,
        IProgress<AnalysisProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (warmUpDays < 0 || warmUpDays >= 250) throw new ScenarioValidationException("Warm-Up Days must be between 0 and 249 for the predefined validation experiments.");
        var comparisons = new List<ValidationComparison>(); int done = 0;
        foreach (var experiment in ValidationScenarios.Experiments(seed))
        {
            AnalysisMeasurements Run(SimulationRequest request)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = new SimulationRunner().Run(request, cancellationToken);
                var metrics = AnalysisMetrics.Measure(request, result, warmUpDays);
                progress?.Report(new(++done, 6));
                return metrics;
            }
            var a = Run(experiment.ScenarioA); var b = Run(experiment.ScenarioB);
            var delta = new System.Collections.ObjectModel.ReadOnlyDictionary<AnalysisMetric, MetricDelta>(a.Values.Keys.ToDictionary(k => k,
                k => MetricDelta.Between(a.Values[k], b.Values[k])));
            var weak = IsWeakResponse(a, b);
            string observation = weak ? "Unexpectedly weak model response. Potential model/implementation issue requiring investigation. Review capacity allocation, WIP rules, state transitions and metric calculations."
                : "The extreme configurations produced distinguishable measurements. This is not a correctness certification or an optimal-staffing recommendation.";
            comparisons.Add(new(experiment, a, b, delta, weak, observation));
        }
        return new(warmUpDays, comparisons.AsReadOnly());
    }
}
