using Simulation.Core;

namespace Simulation.Application;

public sealed record RunMetrics(int Seed, double CompletedWorkItems, double Throughput,
    double AverageLeadTime, double AverageCycleTime, double AverageWip, double BlockedTimeFraction,
    double DeveloperUtilization, double TesterUtilization)
{
    public static RunMetrics From(SimulationResult result) => new(result.RandomSeed,
        result.CompletedWorkItems, result.Throughput, result.AverageLeadTime, result.AverageCycleTime,
        result.AverageWip, result.BlockedTimeFraction, result.DeveloperUtilization, result.TesterUtilization);
}

public sealed record BatchResult(IReadOnlyList<RunMetrics> Runs, RunMetrics Mean);

public sealed class SimulationRunner
{
    public SimulationResult Run(SimulationRequest request, CancellationToken cancellationToken = default)
        => new SimulationEngine().Run(request.ToScenario(), cancellationToken);

    public BatchResult RunBatch(SimulationRequest request, int runs = 100,
        IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        if (runs is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(runs), "Antal körningar måste vara 1–100.");
        var results = new List<RunMetrics>(runs);
        for (var index = 0; index < runs; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var runRequest = request with { RandomSeed = unchecked(request.RandomSeed + index) };
            results.Add(RunMetrics.From(Run(runRequest, cancellationToken)));
            progress?.Report(index + 1);
        }
        // Keep only metrics for a batch; full daily histories are not retained 100 times.
        return new BatchResult(results, new RunMetrics(request.RandomSeed,
            results.Average(r => r.CompletedWorkItems), results.Average(r => r.Throughput),
            results.Average(r => r.AverageLeadTime), results.Average(r => r.AverageCycleTime),
            results.Average(r => r.AverageWip), results.Average(r => r.BlockedTimeFraction),
            results.Average(r => r.DeveloperUtilization), results.Average(r => r.TesterUtilization)));
    }
}
