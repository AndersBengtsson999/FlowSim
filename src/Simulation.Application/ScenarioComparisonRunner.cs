using System.Collections.ObjectModel;
using Simulation.Core;

namespace Simulation.Application;

public sealed record ScenarioRun(Guid RunId, ScenarioDefinition ScenarioSnapshot, SimulationRequest EffectiveConfiguration,
    ComparisonOptions Options, string SimulationModelVersion, DateTimeOffset ExecutedAtUtc,
    SimulationResult? SingleRun, MonteCarloResult? MonteCarlo)
{
    public bool IsOutOfDate(ScenarioDefinition current, ComparisonOptions options) =>
        ScenarioSnapshot != current || Options != options || SimulationModelVersion != SimulationModel.Version;
}
public sealed record ExperimentProgress(int ScenarioNumber, int ScenarioCount, string ScenarioName, int CompletedRuns, int RunsPerScenario);
public sealed record ComparisonCell(Guid ScenarioId, MetricDistribution Distribution, MetricDelta Delta,
    double? PercentagePoints, MetricDistribution? PairedDelta);
public sealed record ComparisonMetricRow(AnalysisMetric Metric, IReadOnlyList<ComparisonCell> Cells);
public sealed record ScenarioComparisonResult(Experiment ExperimentSnapshot, IReadOnlyList<ScenarioRun> Runs,
    IReadOnlyList<ComparisonMetricRow> Metrics, string SimulationModelVersion = SimulationModel.Version);

public sealed class ScenarioComparisonRunner
{
    public ScenarioRun Run(ScenarioDefinition scenario, ComparisonOptions options,
        IProgress<MonteCarloProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var experiment = new Experiment(Guid.NewGuid(), "Validation", "", Array.AsReadOnly(new[] { scenario }), scenario.Id, options);
        experiment.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        var effective = options.CommonRandomNumbers ? scenario.Configuration with { RandomSeed = options.BaseSeed } : scenario.Configuration;
        var executed = DateTimeOffset.UtcNow;
        SimulationResult? single = null; MonteCarloResult? monteCarlo = null;
        if (options.Mode == ExperimentRunMode.SingleRun) single = new SimulationRunner().Run(effective, cancellationToken);
        else monteCarlo = new MonteCarloRunner().Run(new(effective, options.MonteCarloRuns), progress, cancellationToken);
        return new(Guid.NewGuid(), scenario, effective, options, SimulationModel.Version, executed, single, monteCarlo);
    }
    public IReadOnlyList<ScenarioRun> RunAll(Experiment experiment, IProgress<ExperimentProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        experiment.Validate();
        int runs = experiment.Options.Mode == ExperimentRunMode.SingleRun ? 1 : experiment.Options.MonteCarloRuns;
        var work = experiment.Scenarios.Sum(s => (long)s.Configuration.SimulationDays * s.Configuration.NumberOfWorkItems);
        if (experiment.Options.Mode == ExperimentRunMode.SingleRun && work > 5_000_000)
            throw new ScenarioValidationException("Single-run comparisons retain full daily histories and are limited to 5,000,000 total item-days.");
        if (work * runs > 500_000_000 || (runs > 1 && experiment.Scenarios.Any(s => (long)s.Configuration.SimulationDays * s.Configuration.NumberOfWorkItems * runs > 100_000_000)))
            throw new ScenarioValidationException("Comparison exceeds 500,000,000 total item-days or 100,000,000 per Monte Carlo scenario. Reduce days, items or runs.");
        var output = new List<ScenarioRun>();
        for (var i = 0; i < experiment.Scenarios.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested(); var scenario = experiment.Scenarios[i];
            progress?.Report(new(i + 1, experiment.Scenarios.Count, scenario.Name, 0, runs));
            var reporter = new InlineProgress<MonteCarloProgress>(p => progress?.Report(new(i + 1, experiment.Scenarios.Count, scenario.Name, p.CompletedRuns, runs)));
            output.Add(Run(scenario, experiment.Options, reporter, cancellationToken));
            progress?.Report(new(i + 1, experiment.Scenarios.Count, scenario.Name, runs, runs));
        }
        return output.AsReadOnly(); // Publish only a complete batch.
    }
    public ScenarioComparisonResult Compare(Experiment experiment, IReadOnlyList<ScenarioRun> runs)
    {
        experiment.Validate();
        if (runs.Count != experiment.Scenarios.Count || runs.Select(r => r.ScenarioSnapshot.Id).Distinct().Count() != runs.Count
            || experiment.Scenarios.Any(s => !runs.Any(r => r.ScenarioSnapshot.Id == s.Id && !r.IsOutOfDate(s, experiment.Options))))
            throw new ScenarioValidationException("Comparison requires current results for every included scenario and its baseline. Out of Date results are excluded; run the scenarios again.");
        var ordered = experiment.Scenarios.Select(s => runs.Single(r => r.ScenarioSnapshot.Id == s.Id)).ToArray();
        var baseline = ordered.Single(r => r.ScenarioSnapshot.Id == experiment.BaselineId);
        var values = ordered.ToDictionary(r => r.ScenarioSnapshot.Id, Distributions);
        var rows = values.Values.SelectMany(d => d.Keys).Distinct().OrderBy(m => (int)m).Select(metric =>
        {
            var baselineValue = values[baseline.ScenarioSnapshot.Id].GetValueOrDefault(metric)?.P50;
            var cells = ordered.Select(r =>
            {
                var distribution = values[r.ScenarioSnapshot.Id].GetValueOrDefault(metric) ?? DistributionStatistics.Summarize([]);
                var delta = MetricDelta.Between(baselineValue, distribution.P50);
                MetricDistribution? paired = null;
                if (experiment.Options.CommonRandomNumbers && experiment.Options.Mode == ExperimentRunMode.MonteCarlo)
                {
                    var differences = r.MonteCarlo!.Runs.Zip(baseline.MonteCarlo!.Runs, (point, reference) =>
                    {
                        if (point.RunNumber != reference.RunNumber || point.RandomSeed != reference.RandomSeed)
                            throw new ScenarioValidationException("Paired Monte Carlo requires equal run numbers and seeds.");
                        var x = MonteCarloValue(r, point, metric); var b = MonteCarloValue(baseline, reference, metric);
                        return x.HasValue && b.HasValue ? x - b : null;
                    });
                    paired = DistributionStatistics.Summarize(differences.Where(d => d.HasValue).Select(d => d!.Value));
                }
                return new ComparisonCell(r.ScenarioSnapshot.Id, distribution, delta,
                    IsRatio(metric) ? delta.Absolute * 100 : null, paired);
            }).ToArray();
            return new ComparisonMetricRow(metric, Array.AsReadOnly(cells));
        }).ToArray();
        return new(experiment with { Scenarios = Array.AsReadOnly(experiment.Scenarios.ToArray()) }, Array.AsReadOnly(ordered), Array.AsReadOnly(rows));
    }
    public static bool IsRatio(AnalysisMetric metric) => metric is AnalysisMetric.DeveloperUtilization or AnalysisMetric.TesterUtilization or AnalysisMetric.ReworkDeveloperCapacityShare;
    private static IReadOnlyDictionary<AnalysisMetric, MetricDistribution> Distributions(ScenarioRun run)
    {
        if (run.SingleRun is { } single)
        {
            var metrics = AnalysisMetrics.Measure(run.EffectiveConfiguration, single, 0).Values;
            return new ReadOnlyDictionary<AnalysisMetric, MetricDistribution>(metrics.Where(p => DisplayMetrics.Contains(p.Key)).ToDictionary(p => p.Key,
                p => DistributionStatistics.Summarize(p.Value.HasValue ? new[] { p.Value.Value } : [])));
        }
        return new ReadOnlyDictionary<AnalysisMetric, MetricDistribution>(DisplayMetrics.Where(m => MonteCarloMetrics.Contains(m)
            && (!QualityMetrics.Contains(m) || run.EffectiveConfiguration.Quality.Enabled)).ToDictionary(m => m,
            m => DistributionStatistics.Summarize(run.MonteCarlo!.Runs.Select(r => MonteCarloValue(run, r, m)).Where(v => v.HasValue).Select(v => v!.Value))));
    }
    private static double? MonteCarloValue(ScenarioRun run, MonteCarloRunResult value, AnalysisMetric metric)
    {
        if (QualityMetrics.Contains(metric) && !run.EffectiveConfiguration.Quality.Enabled) return null;
        return metric switch
        {
            AnalysisMetric.CompletedWorkItems => value.CompletedWorkItems,
            AnalysisMetric.ThroughputPerFiveDays => value.ThroughputPerFiveDays,
            AnalysisMetric.AverageLeadTime => value.CompletedWorkItems > 0 ? value.AverageLeadTime : null,
            AnalysisMetric.AverageCycleTime => value.CompletedWorkItems > 0 ? value.AverageCycleTime : null,
            AnalysisMetric.AverageWip => value.AverageWip,
            AnalysisMetric.DeveloperUtilization => value.DeveloperUtilization,
            AnalysisMetric.TesterUtilization => value.TesterUtilization,
            AnalysisMetric.MaximumWaitingForCodeReviewQueue => value.MaximumWaitingForCodeReviewQueue,
            AnalysisMetric.MaximumWaitingForTestingQueue => value.MaximumWaitingForTestingQueue,
            AnalysisMetric.TotalDefectsFound => value.TotalDefectsFound,
            AnalysisMetric.TotalReworkEffort => value.TotalReworkEffort,
            AnalysisMetric.ReworkDeveloperCapacityShare => value.ReworkDeveloperCapacityShare,
            _ => null
        };
    }
    private static readonly AnalysisMetric[] QualityMetrics = [AnalysisMetric.TotalDefectsFound, AnalysisMetric.TotalReworkEffort,
        AnalysisMetric.ReworkDeveloperCapacityShare, AnalysisMetric.MaximumWaitingForReworkQueue];
    private static readonly AnalysisMetric[] MonteCarloMetrics = [AnalysisMetric.CompletedWorkItems, AnalysisMetric.ThroughputPerFiveDays,
        AnalysisMetric.AverageLeadTime, AnalysisMetric.AverageCycleTime, AnalysisMetric.AverageWip, AnalysisMetric.DeveloperUtilization,
        AnalysisMetric.TesterUtilization, AnalysisMetric.MaximumWaitingForCodeReviewQueue, AnalysisMetric.MaximumWaitingForTestingQueue,
        AnalysisMetric.TotalDefectsFound, AnalysisMetric.TotalReworkEffort, AnalysisMetric.ReworkDeveloperCapacityShare];
    public static IReadOnlyList<AnalysisMetric> DisplayMetrics { get; } = Array.AsReadOnly(new[] { AnalysisMetric.CompletedWorkItems, AnalysisMetric.ThroughputPerFiveDays,
        AnalysisMetric.AverageLeadTime, AnalysisMetric.AverageCycleTime, AnalysisMetric.AverageActiveTime, AnalysisMetric.AverageWaitingTime,
        AnalysisMetric.AverageBlockedTime, AnalysisMetric.AverageWip, AnalysisMetric.DeveloperUtilization, AnalysisMetric.TesterUtilization,
        AnalysisMetric.MaximumWaitingForCodeReviewQueue, AnalysisMetric.MaximumWaitingForTestingQueue, AnalysisMetric.MaximumWaitingForReworkQueue,
        AnalysisMetric.TotalDefectsFound, AnalysisMetric.TotalReworkEffort, AnalysisMetric.ReworkDeveloperCapacityShare });
    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T> { public void Report(T value) => report(value); }
}
