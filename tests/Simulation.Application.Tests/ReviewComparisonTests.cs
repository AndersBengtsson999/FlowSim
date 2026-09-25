using Simulation.Application;
using Simulation.Core;
using Xunit;

namespace Simulation.Application.Tests;

public sealed class ReviewComparisonTests
{
    [Fact]
    public void UnfinishedReportsSeparateActiveReviewFromItsQueue()
    {
        var report = new ExperimentRunner().Run(new SimulationRequest
        {
            NumberOfWorkItems = 5, DeveloperCount = 5, DevelopmentEffort = 1, CodeReviewEffort = 5,
            CodeReviewWipLimit = 1, SimulationDays = 2
        }).A;
        Assert.Equal(1, report.Unfinished.CodeReview);
        Assert.Equal(4, report.Unfinished.WaitingForCodeReview);
        Assert.Equal(5, report.Unfinished.Count);
        Assert.Equal(2, report.Unfinished.AverageAge);
        Assert.Equal(0, report.Metrics.AverageReviewTime); // no completed reviews
    }

    [Fact]
    public void HorizonDependencyCountsUseFinalStateInsteadOfDayStart()
    {
        var scenario = new SimulationScenario("Dependency", 3, new Team(1, 1), 2, 2, 2,
            [new("A", "A", 1, 1, 1), new("B", "B", 1, 1, 1, ["A"])]);
        var result = new SimulationEngine().Run(scenario);
        var report = ResultAnalysis.Analyze(result, scenario);
        Assert.Equal(1, result.Days[^1].BlockedItems);
        Assert.Equal(0, report.Unfinished.DependencyBlocked);
        Assert.Equal(1, report.Unfinished.ReadyBacklog);
        Assert.Null(Assert.Single(report.OldestItems).CycleAge);
    }

    [Fact]
    public void FutureItemsAreExcludedFromReportCountsAndAgesUntilCreation()
    {
        var scenario = new SimulationScenario("Arrivals", 5, new Team(1, 0), 1, 1, 1,
            [new("A", "A", 1, 1, 2, createdDay: 2), new("B", "Future", 1, 1, 1, createdDay: 10)]);
        var report = ResultAnalysis.Analyze(new SimulationEngine().Run(scenario), scenario);
        Assert.Equal(0, report.History[0].Total);
        Assert.Equal(1, report.History[^1].Total);
        var item = Assert.Single(report.OldestItems);
        Assert.Equal(3, item.Age);
        Assert.Equal(3, item.CycleAge);
        Assert.Equal(1, report.Unfinished.Testing);
    }

    [Fact]
    public void ReviewMetricsMapToTheResultWithoutChangingTheirMeaning()
    {
        var result = new SimulationRunner().Run(new());
        var metrics = RunMetrics.From(result);
        Assert.Equal(result.ReviewUtilization, metrics.ReviewUtilization);
        Assert.Equal(result.AverageReviewTime, metrics.AverageReviewTime);
        Assert.Equal(result.DevelopmentUtilization, metrics.DevelopmentUtilization);
        Assert.Equal(result.AverageReviewWip, metrics.AverageReviewWip);
    }
}
