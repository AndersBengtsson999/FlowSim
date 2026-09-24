using System.Text.Json;
using Simulation.Core;
using Xunit;

namespace Simulation.Core.Tests;

public sealed class SimulationEngineTests
{
    private static SimulationScenario Scenario(int days = 6, int wip = 2,
        double dev = 1, double test = 1, WorkItem[]? items = null,
        Dependency[]? dependencies = null, int developers = 1, int testers = 1) => new(
            new Organization("Test", [new Team("Team", developers, testers, wip)]),
            items ?? [new WorkItem(1, "A", 1, 1)], dependencies ?? [], days, dev, test, 5, 3, 42);
    private static SimulationResult Run(SimulationScenario scenario) => new SimulationEngine().Run(scenario);

    [Fact]
    public void DependenciesBlockStartUntilAllPredecessorsAreDone()
    {
        var result = Run(Scenario(days: 12, wip: 3,
            items: [new(1, "A", 1, 1), new(2, "B", 2, 1), new(3, "C", 1, 1)],
            dependencies: [new(3, 1), new(3, 2)]));
        var dependent = result.WorkItems.Single(w => w.Id == 3);
        var readyAt = result.WorkItems.Where(w => w.Id != 3).Max(w => w.CompletedAt!.Value);
        Assert.Equal(readyAt, dependent.StartedAt);
        Assert.All(result.Days.Take(readyAt), d => Assert.Equal(WorkItemStatus.Backlog, d.Statuses[3]));
    }

    [Fact]
    public void DevelopmentTransitionsToCodeReviewWhenSizeTimesComplexityIsConsumed()
    {
        var result = Run(Scenario(days: 2, items: [new(1, "A", 1, 2)]));
        Assert.Equal(WorkItemStatus.Development, result.Days[0].Statuses[1]);
        Assert.Equal(WorkItemStatus.CodeReview, result.Days[1].Statuses[1]);
    }

    [Fact]
    public void CodeReviewTransitionsToTestingOnTheFollowingDay()
    {
        var result = Run(Scenario(days: 2));
        Assert.Equal(WorkItemStatus.CodeReview, result.Days[0].Statuses[1]);
        Assert.Equal(WorkItemStatus.Testing, result.Days[1].Statuses[1]);
        Assert.Equal(0, result.Days[1].TestingWork);
    }

    [Fact]
    public void TestingTransitionsToDoneAfterConsumingTestCapacity()
    {
        var result = Run(Scenario(days: 4, test: 0.5));
        Assert.Equal(WorkItemStatus.Testing, result.Days[2].Statuses[1]);
        Assert.Equal(WorkItemStatus.Done, result.Days[3].Statuses[1]);
        Assert.Equal(4, result.WorkItems[0].CompletedAt);
    }

    [Fact]
    public void WipLimitIncludesDevelopmentReviewAndTesting()
    {
        var result = Run(Scenario(days: 15, wip: 1, items: [new(1, "A", 1, 1), new(2, "B", 1, 1)]));
        Assert.All(result.Days, d => Assert.InRange(d.Wip, 0, 1));
        var ordered = result.WorkItems.OrderBy(w => w.StartedAt).ToArray();
        Assert.Equal(ordered[0].CompletedAt, ordered[1].StartedAt);
        Assert.Equal(2, result.CompletedWorkItems);
    }

    [Fact]
    public void DeveloperCapacityIsSharedAndNeverExceeded()
    {
        var result = Run(Scenario(days: 2, dev: 1.5, developers: 2,
            items: [new(1, "A", 100, 1), new(2, "B", 100, 1)]));
        Assert.All(result.Days, d => Assert.Equal(3, d.DevelopmentWork));
        Assert.All(result.WorkItems, w => Assert.Equal(WorkItemStatus.Development, w.Status));
        Assert.Equal(1, result.DeveloperUtilization);
    }

    [Fact]
    public void TesterCapacityIsSharedAndNeverExceeded()
    {
        var result = Run(Scenario(days: 4, dev: 100, test: 0.5, testers: 2,
            items: [new(1, "A", 10, 1), new(2, "B", 10, 1)]));
        Assert.All(result.Days, d => Assert.InRange(d.TestingWork, 0, 1));
        Assert.Equal(2, result.Days.Sum(d => d.TestingWork));
        Assert.Equal(0, result.CompletedWorkItems);
        Assert.Equal(0.5, result.TesterUtilization);
    }

    [Fact]
    public void FixedSeedReproducesEntireResultWithoutMutatingInput()
    {
        var scenario = Scenario(items: [new(1, "A", 1, 2), new(2, "B", 2, 1)]);
        Assert.Equal(JsonSerializer.Serialize(Run(scenario)), JsonSerializer.Serialize(Run(scenario)));
        Assert.All(scenario.WorkItems, w => Assert.Equal(WorkItemStatus.Backlog, w.Status));
        Assert.All(scenario.WorkItems, w => Assert.Null(w.StartedAt));
    }

    [Fact]
    public void LeadTimeIncludesWaitingAndCycleTimeStartsAtAdmission()
    {
        var result = Run(Scenario(items: [new(1, "A", 1, 1), new(2, "B", 1, 1)],
            dependencies: [new(2, 1)]));
        Assert.Equal(4.5, result.AverageLeadTime); // (3 + 6) / 2
        Assert.Equal(3, result.AverageCycleTime);
        Assert.Equal(1, result.AverageWip);
        Assert.Equal(1.0 / 3, result.BlockedTimeFraction, 10); // 3 / 9 unfinished item-days
    }

    [Fact]
    public void ThroughputIsCompletedItemsDividedByEntireSimulationDuration()
    {
        var result = Run(Scenario(days: 10, items: [new(1, "A", 1, 1), new(2, "B", 1, 1)]));
        Assert.Equal(2, result.CompletedWorkItems);
        Assert.Equal(0.2, result.Throughput, 10);
    }

    [Fact]
    public void FutureArrivalIsNotCountedBeforeCreation()
    {
        var result = Run(Scenario(days: 6, items: [new(1, "A", 1, 1, CreatedAt: 3)]));
        Assert.Equal(3, result.WorkItems[0].StartedAt);
        Assert.Equal(3, result.AverageLeadTime);
        Assert.All(result.Days.Take(3), day => Assert.Equal(0, day.UnfinishedItems));
    }

    [Fact]
    public void EmptyScenarioProducesFiniteZeroMetrics()
    {
        var result = Run(Scenario(items: []));
        Assert.Equal(0, result.CompletedWorkItems);
        Assert.Equal(0, result.AverageLeadTime);
        Assert.Equal(0, result.AverageCycleTime);
        Assert.Equal(0, result.AverageWip);
        Assert.Equal(0, result.BlockedTimeFraction);
        Assert.Equal(0, result.DeveloperUtilization);
        Assert.Equal(0, result.TesterUtilization);
    }

    [Fact]
    public void ZeroTestersLeaveItemsInTestingAndOccupyWip()
    {
        var result = Run(Scenario(wip: 1, testers: 0, items: [new(1, "A", 1, 1), new(2, "B", 1, 1)]));
        Assert.Equal(0, result.CompletedWorkItems);
        Assert.Equal(0, result.TesterUtilization);
        Assert.Single(result.WorkItems, w => w.Status == WorkItemStatus.Testing);
        Assert.Single(result.WorkItems, w => w.Status == WorkItemStatus.Backlog);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void ZeroDevelopmentCapacityCannotCompleteDevelopment(int developers, double capacity)
    {
        var result = Run(Scenario(developers: developers, dev: capacity));
        Assert.Equal(WorkItemStatus.Development, result.WorkItems[0].Status);
        Assert.Equal(0, result.DeveloperUtilization);
    }

    [Fact]
    public void ReleasesIncludeCompletedItemsOnlyOnceAndSprintsEndAtHorizon()
    {
        var result = Run(Scenario(days: 7));
        Assert.Equal(new Sprint(0, 5), result.Sprints[0]);
        Assert.Equal(new Sprint(5, 7), result.Sprints[1]);
        Assert.Equal(3, result.Releases[0].Date);
        Assert.Single(result.Releases[0].IncludedWorkItems);
        Assert.Empty(result.Releases[1].IncludedWorkItems);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InvalidDependencyGraphsAreRejected(bool cycle)
    {
        var scenario = Scenario(items: [new(1, "A", 1, 1), new(2, "B", 1, 1)],
            dependencies: cycle ? [new(1, 2), new(2, 1)] : [new(1, 99)]);
        Assert.Throws<ArgumentException>(() => Run(scenario));
    }

    [Fact]
    public void InvalidDurationWipAndNonFiniteCapacityAreRejected()
    {
        Assert.Throws<ArgumentException>(() => Run(Scenario(days: 0)));
        Assert.Throws<ArgumentException>(() => Run(Scenario(wip: 0)));
        Assert.Throws<ArgumentException>(() => Run(Scenario(dev: double.NaN)));
        Assert.Throws<ArgumentException>(() => Run(Scenario(test: double.PositiveInfinity)));
    }

    [Fact]
    public void CancellationIsObserved()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        Assert.Throws<OperationCanceledException>(() => new SimulationEngine().Run(Scenario(), source.Token));
    }
}
