using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Xunit;

namespace Simulation.Application.Tests;

public sealed class MonteCarloTests
{
    private sealed class CallbackProgress(Action<MonteCarloProgress> callback) : IProgress<MonteCarloProgress>
    { public void Report(MonteCarloProgress value) => callback(value); }

    [Fact]
    public void VariableSingleRunReproducesCompleteResultsAndExposesGeneratedEfforts()
    {
        var request = BaselineScenario.VariableEffortExample();
        var runner = new SimulationRunner();
        var result = runner.Run(request);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(runner.Run(request)));
        Assert.NotEqual(JsonSerializer.Serialize(result.WorkItems), JsonSerializer.Serialize(runner.Run(request with { RandomSeed = 54321 }).WorkItems));
        var generated = request.ToScenario();
        for (var i = 0; i < result.WorkItems.Count; i++)
        {
            Assert.Equal(generated.WorkItems[i].DevelopmentEffort, result.WorkItems[i].DevelopmentEffort);
            Assert.Equal(generated.WorkItems[i].CodeReviewEffort, result.WorkItems[i].CodeReviewEffort);
            Assert.Equal(generated.WorkItems[i].TestingEffort, result.WorkItems[i].TestingEffort);
        }
        Assert.Equal(request.RandomSeed, result.RandomSeed);
    }

    [Fact]
    public void FixedModePreservesThePreviousBaselineAcrossSeeds()
    {
        var runner = new SimulationRunner();
        var request = BaselineScenario.CreateRequest();
        var result = runner.Run(request);
        var other = runner.Run(request with { RandomSeed = -700 });
        Assert.Equal(JsonSerializer.Serialize(result.Days), JsonSerializer.Serialize(other.Days));
        Assert.Equal(JsonSerializer.Serialize(result.WorkItems), JsonSerializer.Serialize(other.WorkItems));
        Assert.Equal(30, result.CompletedWorkItems);
        Assert.Equal(1.5, result.ThroughputPerFiveDays);
        Assert.Equal(23.833333333333332, result.AverageLeadTime, 12);
        Assert.Equal(9.666666666666666, result.AverageCycleTime, 12);
        Assert.Equal(2.6, result.AverageWip, 12);
        Assert.Equal(.36, result.DeveloperUtilization, 12);
        Assert.Equal(.3, result.TesterUtilization, 12);
        Assert.All(result.WorkItems, w => { Assert.Equal(5, w.DevelopmentEffort); Assert.Equal(1, w.CodeReviewEffort); Assert.Equal(2, w.TestingEffort); });
    }

    [Fact]
    public void VariablePresetChangesOnlyEffortAndName()
    {
        var a = BaselineScenario.CreateRequest(); var b = BaselineScenario.VariableEffortExample();
        Assert.Equal(a, b with { Name = a.Name, DevelopmentDistribution = null, CodeReviewDistribution = null, TestingDistribution = null });
        Assert.IsType<TriangularEffort>(b.DevelopmentDistribution);
    }

    [Fact]
    public void MonteCarloIsDeterministicAndFirstRunMatchesSingleRun()
    {
        var request = new MonteCarloRequest(BaselineScenario.VariableEffortExample(), 20);
        var runner = new MonteCarloRunner();
        var result = runner.Run(request);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(runner.Run(request)));
        var single = new SimulationRunner().Run(request.Scenario);
        Assert.Equal(single.AverageLeadTime, result.Runs[0].AverageLeadTime);
        Assert.Equal(single.ThroughputPerFiveDays, result.Runs[0].ThroughputPerFiveDays);
        Assert.Equal(20, result.Runs.Select(r => r.RandomSeed).Distinct().Count());
        Assert.Equal(20, result.AverageWip.SampleCount);
        Assert.Equal(20, result.ThroughputPerFiveDays.Histogram.Sum(b => b.Count));
        // A later run reproduced independently does not depend on executing earlier runs.
        var later = result.Runs[10];
        var replay = new SimulationRunner().Run(request.Scenario with { RandomSeed = later.RandomSeed });
        Assert.Equal(replay.AverageLeadTime, later.AverageLeadTime);
        Assert.Equal(replay.MaximumWaitingForTestingQueue, later.MaximumWaitingForTestingQueue);
    }

    [Fact]
    public void SeedDerivationHasDefinedOverflowAndStableIndexing()
    {
        Assert.Equal(12345, MonteCarloRunner.DeriveSeed(12345, 0));
        Assert.Equal(12346, MonteCarloRunner.DeriveSeed(12345, 1));
        Assert.Equal(int.MinValue, MonteCarloRunner.DeriveSeed(int.MaxValue, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => MonteCarloRunner.DeriveSeed(0, -1));
    }

    [Fact]
    public void NoCompletionsAreNotPresentedAsZeroTimePercentiles()
    {
        var result = new MonteCarloRunner().Run(new(new SimulationRequest { TesterCount = 0 }, 3));
        Assert.Equal(0, result.RunsWithCompletions);
        Assert.Equal(0, result.AverageLeadTime.SampleCount);
        Assert.Null(result.AverageLeadTime.P50);
        Assert.Null(result.AverageCycleTime.P95);
        Assert.Empty(result.AverageLeadTime.Histogram);
        Assert.Equal(0d, result.ThroughputPerFiveDays.P50);
        Assert.Equal(3, result.AverageWip.SampleCount);
        Assert.All(result.Runs, r => Assert.Equal(0, r.AverageLeadTime));
    }

    [Fact]
    public void TimePercentilesUseOnlyRunsThatHaveCompletedItems()
    {
        var request = new SimulationRequest { NumberOfWorkItems = 1, SimulationDays = 4,
            DevelopmentDistribution = new TriangularEffort(.1, 1, 3), TestingEffort = 1 };
        var result = new MonteCarloRunner().Run(new(request, 50));
        var delivered = result.Runs.Where(r => r.CompletedWorkItems > 0).ToArray();
        Assert.InRange(delivered.Length, 1, 49);
        Assert.Equal(delivered.Length, result.AverageLeadTime.SampleCount);
        Assert.Equal(delivered.Length, result.RunsWithCompletions);
        Assert.Equal(DistributionStatistics.Percentile(delivered.Select(r => r.AverageLeadTime), .5), result.AverageLeadTime.P50);
        Assert.Equal(50, result.ThroughputPerFiveDays.SampleCount);
    }

    [Fact]
    public void FixedMonteCarloHasNoArtificialSpread()
    {
        var result = new MonteCarloRunner().Run(new(BaselineScenario.CreateRequest(), 5));
        Assert.Equal(result.AverageLeadTime.P50, result.AverageLeadTime.P95);
        Assert.Single(result.AverageLeadTime.Histogram);
        Assert.Equal(5, result.AverageLeadTime.Histogram[0].Count);
    }

    [Fact]
    public void ProgressAndCancellationStopBetweenRunsWithoutPartialAggregate()
    {
        using var source = new CancellationTokenSource();
        var observed = new List<int>();
        var progress = new CallbackProgress(p => { observed.Add(p.CompletedRuns); if (p.CompletedRuns == 3) source.Cancel(); });
        Assert.Throws<OperationCanceledException>(() => new MonteCarloRunner().Run(new(BaselineScenario.CreateRequest(), 20), progress, source.Token));
        Assert.Equal(new[] {1, 2, 3}, observed);
    }

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(10001)]
    public void InvalidRunCountsAreRejected(int count) =>
        Assert.Throws<ScenarioValidationException>(() => new MonteCarloRunner().Run(new(new(), count)));

    [Fact]
    public void ExcessiveTotalWorkIsRejected() =>
        Assert.Throws<ScenarioValidationException>(() => new MonteCarloRunner().Run(new(new() { NumberOfWorkItems = 1000, SimulationDays = 1000 }, 500)));
}
