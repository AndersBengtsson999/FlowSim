using System.Text.Json;
using Simulation.Application;
using Xunit;

namespace Simulation.Application.Tests;

public sealed class SimulationRunnerTests
{
    [Fact]
    public void RequestGeneratesReproducibleAcyclicDependencies()
    {
        var request = new SimulationRequest { NumberOfWorkItems = 20, DependencyProbability = 1 };
        var scenario = request.ToScenario();
        Assert.Equal(19, scenario.Dependencies.Count);
        Assert.All(scenario.Dependencies, d => Assert.True(d.DependsOnWorkItemId < d.WorkItemId));
        Assert.Equal(JsonSerializer.Serialize(scenario), JsonSerializer.Serialize(request.ToScenario()));
    }

    [Fact]
    public void DifferentSeedsGenerateDifferentScenarios()
    {
        var request = new SimulationRequest { DependencyProbability = 1 };
        Assert.NotEqual(JsonSerializer.Serialize(request.ToScenario().Dependencies),
            JsonSerializer.Serialize((request with { RandomSeed = 43 }).ToScenario().Dependencies));
    }

    [Fact]
    public void BatchUses100DistinctSeedsAndCorrectMeans()
    {
        var request = new SimulationRequest { NumberOfWorkItems = 5, DurationDays = 10 };
        var runner = new SimulationRunner();
        var result = runner.RunBatch(request);
        Assert.Equal(100, result.Runs.Count);
        Assert.Equal(Enumerable.Range(42, 100), result.Runs.Select(r => r.Seed));
        Assert.Equal(result.Runs.Average(r => r.CompletedWorkItems), result.Mean.CompletedWorkItems);
        Assert.Equal(result.Runs.Average(r => r.Throughput), result.Mean.Throughput);
        Assert.Equal(RunMetrics.From(runner.Run(request)), result.Runs[0]);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(runner.RunBatch(request)));
    }

    [Fact]
    public void BatchSeedOverflowIsDeterministic()
    {
        var result = new SimulationRunner().RunBatch(new SimulationRequest
            { RandomSeed = int.MaxValue, NumberOfWorkItems = 0, DurationDays = 1 }, 2);
        Assert.Equal(int.MinValue, result.Runs[1].Seed);
    }

    [Fact]
    public void InvalidRequestsAreRejected()
    {
        var runner = new SimulationRunner();
        Assert.Throws<ArgumentException>(() => runner.Run(new() { NumberOfWorkItems = -1 }));
        Assert.Throws<ArgumentException>(() => runner.Run(new() { DependencyProbability = 2 }));
        Assert.Throws<ArgumentException>(() => runner.Run(new() { Size = 0 }));
        Assert.Throws<ArgumentException>(() => runner.Run(new() { NumberOfDevelopers = -1 }));
        Assert.Throws<ArgumentException>(() => runner.Run(new() { NumberOfWorkItems = 2000, DurationDays = 3650 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => runner.RunBatch(new(), 0));
    }

    [Fact]
    public void BatchCanBeCancelledBetweenRuns()
    {
        using var source = new CancellationTokenSource();
        var progress = new CallbackProgress(_ => source.Cancel());
        Assert.Throws<OperationCanceledException>(() => new SimulationRunner().RunBatch(
            new SimulationRequest { NumberOfWorkItems = 1, DurationDays = 1 }, 100, progress, source.Token));
    }

    private sealed class CallbackProgress(Action<int> callback) : IProgress<int>
    {
        public void Report(int value) => callback(value);
    }
}
