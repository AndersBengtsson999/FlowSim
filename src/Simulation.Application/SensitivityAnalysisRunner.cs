using System.Collections.ObjectModel;
using Simulation.Core;

namespace Simulation.Application;

public sealed record SensitivityAnalysisRequest(SimulationRequest BaseScenario, SensitivityParameter Parameter,
    IReadOnlyList<double> Values, SensitivityMode Mode = SensitivityMode.SingleRun, int MonteCarloRuns = 100, int WarmUpDays = 0);
public sealed record MetricDelta(double? Absolute, double? Percentage)
{
    public static MetricDelta Between(double? baseline, double? value) => baseline is null || value is null
        ? new(null, null) : new(value - baseline, baseline == 0 ? null : (value - baseline) / Math.Abs(baseline.Value) * 100);
}
public sealed record AnalysisPoint(double ParameterValue,
    IReadOnlyDictionary<AnalysisMetric, MetricDistribution> FullSimulation,
    IReadOnlyDictionary<AnalysisMetric, MetricDistribution> MeasurementWindow,
    IReadOnlyDictionary<AnalysisMetric, MetricDelta> Deltas);
public sealed record SensitivityAnalysisResult(SimulationRequest BaseScenario, SensitivityParameter Parameter,
    SensitivityMode Mode, int RunsPerPoint, int WarmUpDays, AnalysisPoint BasePoint, IReadOnlyList<AnalysisPoint> Points);
public sealed record AnalysisProgress(int CompletedRuns, int TotalRuns);

public sealed class SensitivityAnalysisRunner
{
    public SensitivityAnalysisResult Run(SensitivityAnalysisRequest request, IProgress<AnalysisProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.BaseScenario);
        if (!Enum.IsDefined(request.Mode) || !Enum.IsDefined(request.Parameter)) throw new ScenarioValidationException("Unknown analysis mode or parameter.");
        if (request.Values is null || request.Values.Count is < 1 or > 100)
            throw new ScenarioValidationException("Provide between 1 and 100 parameter values.");
        if (request.WarmUpDays < 0 || request.WarmUpDays >= request.BaseScenario.SimulationDays)
            throw new ScenarioValidationException("Warm-Up Days must be at least zero and less than Simulation Days.");
        int runs = request.Mode == SensitivityMode.SingleRun ? 1 : request.MonteCarloRuns;
        if (runs is < 1 or > 10000) throw new ScenarioValidationException("Monte Carlo runs must be between 1 and 10,000.");
        var values = request.Values.ToArray(); // Defensive snapshot before asynchronous caller edits.
        request.BaseScenario.ToScenario();
        var scenarios = values.Select(v => SensitivityParameters.Apply(request.BaseScenario, request.Parameter, v)).ToArray();
        foreach (var s in scenarios) s.ToScenario(); // Validate every point before executing any run.
        if ((long)request.BaseScenario.NumberOfWorkItems * request.BaseScenario.SimulationDays * runs * (values.Length + 1) > 500_000_000)
            throw new ScenarioValidationException("Sensitivity analysis is limited to 500,000,000 total item-days including the base point. Reduce values, runs, items or days.");
        int done = 0, total = runs * (values.Length + 1);
        AnalysisPoint Evaluate(SimulationRequest s, double value)
        {
            var full = new List<AnalysisMeasurements>();
            var window = new List<AnalysisMeasurements>();
            for (int i = 0; i < runs; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var input = s with { RandomSeed = MonteCarloRunner.DeriveSeed(s.RandomSeed, i) };
                var result = new SimulationRunner().Run(input, cancellationToken);
                full.Add(AnalysisMetrics.Measure(input, result, 0));
                window.Add(request.WarmUpDays == 0 ? full[^1] : AnalysisMetrics.Measure(input, result, request.WarmUpDays));
                progress?.Report(new(++done, total));
            }
            return new(value, Summarize(full), Summarize(window), new ReadOnlyDictionary<AnalysisMetric, MetricDelta>(new Dictionary<AnalysisMetric, MetricDelta>()));
        }
        var baseline = Evaluate(request.BaseScenario, SensitivityParameters.Value(request.BaseScenario, request.Parameter));
        var points = scenarios.Select((s, i) =>
        {
            var p = Evaluate(s, values[i]);
            return p with { Deltas = new ReadOnlyDictionary<AnalysisMetric, MetricDelta>(p.MeasurementWindow.ToDictionary(k => k.Key,
                k => MetricDelta.Between(baseline.MeasurementWindow[k.Key].P50, k.Value.P50))) };
        }).ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        return new(request.BaseScenario, request.Parameter, request.Mode, runs, request.WarmUpDays, baseline, Array.AsReadOnly(points));
    }
    private static IReadOnlyDictionary<AnalysisMetric, MetricDistribution> Summarize(List<AnalysisMeasurements> runs) =>
        new ReadOnlyDictionary<AnalysisMetric, MetricDistribution>(runs[0].Values.Keys.ToDictionary(k => k,
            k => DistributionStatistics.Summarize(runs.Select(r => r.Values[k]).Where(v => v.HasValue).Select(v => v!.Value))));
}
