using Simulation.Core;

namespace Simulation.Application;

public sealed record RunMetrics(double CompletedWorkItems, double Throughput,
    double AverageLeadTime, double AverageCycleTime, double AverageWip, double BlockedTimeFraction,
    double DeveloperUtilization, double TesterUtilization, double ReviewUtilization,
    double DevelopmentUtilization, double AverageReviewWip, double AverageReviewTime, double ThroughputPerFiveDays)
{
    public static RunMetrics From(SimulationResult result) => new(
        result.CompletedWorkItems, result.Throughput, result.AverageLeadTime, result.AverageCycleTime,
        result.AverageWip, result.BlockedTimeFraction, result.DeveloperUtilization, result.TesterUtilization,
        result.ReviewUtilization, result.DevelopmentUtilization, result.AverageReviewWip, result.AverageReviewTime, result.ThroughputPerFiveDays);
}

public sealed class SimulationRunner
{
    public SimulationResult Run(SimulationRequest request, CancellationToken cancellationToken = default)
        => new SimulationEngine().Run(request.ToScenario(), cancellationToken);
}
