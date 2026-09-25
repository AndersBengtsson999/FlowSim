using System.Text.Json;
using Simulation.Application;
using Xunit;

namespace Simulation.Application.Tests;

public sealed class DefectMonteCarloTests
{
    [Fact]
    public void PresetChangesOnlyQualityAndNameAndKeepsGeneratedInitialEffort()
    {
        var previous = BaselineScenario.VariableEffortExample();
        var example = BaselineScenario.DefectsAndReworkExample();
        Assert.Equal(previous, example with { Name = previous.Name, Quality = previous.Quality });
        Assert.Equal(JsonSerializer.Serialize(previous.ToScenario().WorkItems), JsonSerializer.Serialize(example.ToScenario().WorkItems));
        Assert.True(example.Quality.Enabled);
        Assert.Equal(.15, example.Quality.CodeReviewDefectProbability);
        Assert.Equal(.10, example.Quality.TestingDefectProbability);
    }

    [Fact]
    public void DefectEnabledMonteCarloIsRepeatableAndAggregatesEveryRun()
    {
        var request = new MonteCarloRequest(BaselineScenario.DefectsAndReworkExample(), 20);
        var runner = new MonteCarloRunner();
        var result = runner.Run(request);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(runner.Run(request)));
        Assert.Equal(20, result.TotalDefectsFound.SampleCount);
        Assert.Equal(DistributionStatistics.Percentile(result.Runs.Select(r => (double)r.TotalDefectsFound), .95), result.TotalDefectsFound.P95);
        Assert.Equal(DistributionStatistics.Percentile(result.Runs.Select(r => r.TotalReworkEffort), .5), result.TotalReworkEffort.P50);
        Assert.True(result.TotalDefectsFound.P50 > 0);
        Assert.True(result.WorkItemsWithDefects.P50 > 0);
        Assert.All(result.Runs, r => Assert.InRange(r.ReworkDeveloperCapacityShare, 0, 1));
        Assert.Equal(20, result.AverageLeadTime.SampleCount);
    }

    [Fact]
    public void LegacyApplicationReportsStillCountEveryState()
    {
        var request = BaselineScenario.DefectsAndReworkExample() with { SimulationDays = 20 };
        var r = new ExperimentRunner().Run(request).A;
        Assert.All(r.History, d => Assert.Equal(30, d.Total));
        Assert.Equal(r.Unfinished.Count, r.Unfinished.Backlog + r.Unfinished.Development + r.Unfinished.CodeReview + r.Unfinished.Testing
            + r.Unfinished.WaitingForCodeReview + r.Unfinished.WaitingForTesting + r.Unfinished.WaitingForRework + r.Unfinished.Rework);
    }
}
