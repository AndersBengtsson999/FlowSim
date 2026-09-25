using Simulation.Core;

namespace Simulation.Application;

public sealed record MonteCarloRequest(SimulationRequest Scenario, int NumberOfRuns = 500);
public sealed record MonteCarloProgress(int CompletedRuns, int TotalRuns);
public sealed record MonteCarloRunResult(int RunNumber, int RandomSeed, int CompletedWorkItems,
    double ThroughputPerFiveDays, double AverageLeadTime, double AverageCycleTime, double AverageWip,
    double DeveloperUtilization, double TesterUtilization,
    int MaximumWaitingForCodeReviewQueue, int MaximumWaitingForTestingQueue)
{
    public int TotalDefectsFound { get; init; }
    public int WorkItemsWithDefects { get; init; }
    public double TotalReworkEffort { get; init; }
    public double ReworkDeveloperCapacityShare { get; init; }
}
public sealed record MonteCarloResult(int NumberOfRuns, int RandomSeed, int RunsWithCompletions,
    IReadOnlyList<MonteCarloRunResult> Runs,
    MetricDistribution CompletedWorkItems, MetricDistribution ThroughputPerFiveDays,
    MetricDistribution AverageLeadTime, MetricDistribution AverageCycleTime, MetricDistribution AverageWip,
    MetricDistribution DeveloperUtilization, MetricDistribution TesterUtilization,
    MetricDistribution MaximumWaitingForCodeReviewQueue, MetricDistribution MaximumWaitingForTestingQueue)
{
    public MetricDistribution TotalDefectsFound { get; init; } = DistributionStatistics.Summarize([]);
    public MetricDistribution WorkItemsWithDefects { get; init; } = DistributionStatistics.Summarize([]);
    public MetricDistribution TotalReworkEffort { get; init; } = DistributionStatistics.Summarize([]);
    public MetricDistribution ReworkDeveloperCapacityShare { get; init; } = DistributionStatistics.Summarize([]);
}

public sealed class MonteCarloRunner
{
    // Run 1 reproduces the single run at the base seed. Arithmetic wraps explicitly at int boundaries.
    public static int DeriveSeed(int baseSeed, int zeroBasedRunIndex)
    {
        if (zeroBasedRunIndex < 0) throw new ArgumentOutOfRangeException(nameof(zeroBasedRunIndex));
        return unchecked(baseSeed + zeroBasedRunIndex);
    }

    public MonteCarloResult Run(MonteCarloRequest request, IProgress<MonteCarloProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Scenario);
        if (request.NumberOfRuns is < 1 or > 10000)
            throw new ScenarioValidationException("Number of Runs must be between 1 and 10,000.");
        var scenario = request.Scenario;
        // Validate even before the first run; retain the single-run workload safeguards.
        scenario.ToScenario();
        if ((long)scenario.NumberOfWorkItems * scenario.SimulationDays * request.NumberOfRuns > 100_000_000)
            throw new ScenarioValidationException("Monte Carlo is limited to 100,000,000 total item-days. Reduce runs, items or days.");
        var runs = new MonteCarloRunResult[request.NumberOfRuns];
        var runner = new SimulationRunner();
        for (var i = 0; i < runs.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var seed = DeriveSeed(scenario.RandomSeed, i);
            var r = runner.Run(scenario with { RandomSeed = seed }, cancellationToken);
            runs[i] = new(i + 1, seed, r.CompletedWorkItems, r.ThroughputPerFiveDays, r.AverageLeadTime,
                r.AverageCycleTime, r.AverageWip, r.DeveloperUtilization, r.TesterUtilization,
                r.MaximumWaitingForCodeReviewQueue, r.MaximumWaitingForTestingQueue)
            {
                TotalDefectsFound = r.TotalDefectsFound, WorkItemsWithDefects = r.WorkItemsWithDefects,
                TotalReworkEffort = r.TotalReworkEffort, ReworkDeveloperCapacityShare = r.ReworkDeveloperCapacityShare
            };
            // Only compact run metrics survive the iteration, not all item-day histories.
            progress?.Report(new(i + 1, runs.Length));
        }
        cancellationToken.ThrowIfCancellationRequested();
        var completed = runs.Where(r => r.CompletedWorkItems > 0).ToArray();
        MetricDistribution All(Func<MonteCarloRunResult, double> value) => DistributionStatistics.Summarize(runs.Select(value));
        return new(runs.Length, scenario.RandomSeed, completed.Length, Array.AsReadOnly(runs),
            All(r => r.CompletedWorkItems), All(r => r.ThroughputPerFiveDays),
            DistributionStatistics.Summarize(completed.Select(r => r.AverageLeadTime)),
            DistributionStatistics.Summarize(completed.Select(r => r.AverageCycleTime)), All(r => r.AverageWip),
            All(r => r.DeveloperUtilization), All(r => r.TesterUtilization),
            All(r => r.MaximumWaitingForCodeReviewQueue), All(r => r.MaximumWaitingForTestingQueue))
        {
            TotalDefectsFound = All(r => r.TotalDefectsFound), WorkItemsWithDefects = All(r => r.WorkItemsWithDefects),
            TotalReworkEffort = All(r => r.TotalReworkEffort), ReworkDeveloperCapacityShare = All(r => r.ReworkDeveloperCapacityShare)
        };
    }
}
