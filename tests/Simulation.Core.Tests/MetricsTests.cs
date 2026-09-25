using System.Text.Json;
using Simulation.Core;
using Xunit;

namespace Simulation.Core.Tests;

public sealed class MetricsTests
{
    private static SimulationScenario Scenario(int days = 10, WorkItem[]? items = null, Team? team = null,
        int development = 5, int review = 3, int testing = 3) =>
        new("Metrics", days, team ?? new Team(5, 2), development, review, testing,
            items ?? [new("A", "A", 1, 1, 1)]);
    private static SimulationResult Run(SimulationScenario scenario) => new SimulationEngine().Run(scenario);

    [Fact]
    public void KnownItemHasCorrectElapsedAndActiveTimes()
    {
        var r = Run(Scenario(items: [new("A", "A", 2, 1, 2, createdDay: 2)]));
        var w = Assert.Single(r.WorkItems);
        Assert.Equal(7, w.DoneDay);
        Assert.Equal(5, w.LeadTime);
        Assert.Equal(5, w.CycleTime);
        Assert.Equal(5, w.ActiveTime);
        Assert.Equal(0, w.WaitingTime);
        Assert.Equal(0, w.BlockedTime);
        Assert.Equal(5, r.AverageActiveTime);
        Assert.Equal(5, r.AverageLeadTime);
        Assert.Equal(5, r.AverageCycleTime);
    }

    [Fact]
    public void ActiveTimeCountsPositiveWorkDaysNotEffortOrActiveStateOccupancy()
    {
        var fractional = Run(Scenario(items: [new("A", "A", .5, .5, .5)], team: new Team(1, 1, .25, .25)));
        Assert.Equal(6, fractional.WorkItems[0].ActiveTime);
        var stalled = Run(Scenario(team: new Team(0, 0)));
        Assert.Equal(WorkItemStatus.Development, stalled.WorkItems[0].FinalState);
        Assert.Equal(0, stalled.WorkItems[0].ActiveTime);
        var zero = Run(Scenario(items: [new("A", "A", 0, 0, 0)], team: new Team(0, 0)));
        Assert.Equal(3, zero.WorkItems[0].CycleTime);
        Assert.Equal(0, zero.WorkItems[0].ActiveTime);
    }

    [Fact]
    public void WaitingCountsWholeIntervalsInEachQueue()
    {
        var r = Run(Scenario(items: [new("A", "A", 1, 2, 3), new("B", "B", 1, 1, 1)], review: 1, testing: 1));
        var b = r.WorkItems[1];
        Assert.Equal(2, b.WaitingForCodeReviewTime);
        Assert.Equal(2, b.WaitingForTestingTime);
        Assert.Equal(4, b.WaitingTime);
        Assert.Equal(2, r.AverageWaitingTime);
        Assert.Equal(b.CodeReviewStartedDay - b.DevelopmentCompletedDay, b.WaitingForCodeReviewTime);
        Assert.Equal(b.TestingStartedDay - b.CodeReviewCompletedDay, b.WaitingForTestingTime);
    }

    [Fact]
    public void DependenciesAreDistinctFromOrdinaryBacklogWaiting()
    {
        var r = Run(Scenario(items: [new("A", "A", 1, 1, 1), new("B", "B", 1, 1, 1, ["A"]), new("C", "C", 1, 1, 1)], development: 1));
        Assert.Equal(3, r.WorkItems[1].BlockedTime);
        Assert.Equal(0, r.WorkItems[2].BlockedTime);
        Assert.Equal(1, r.WorkItems[2].DevelopmentStartedDay);
        Assert.Equal(1, r.AverageBlockedTime);
        Assert.Equal(6, r.WorkItems[1].LeadTime);
        Assert.Equal(3, r.WorkItems[1].CycleTime);
    }

    [Fact]
    public void ThroughputCountsCompletedItemsOverWholeHorizon()
    {
        var r = Run(Scenario(days: 10, items: [new("A", "A", 1, 1, 1), new("B", "B", 100, 1, 1)]));
        Assert.Equal(10, r.SimulationDays);
        Assert.Equal(2, r.TotalWorkItems);
        Assert.Equal(1, r.CompletedWorkItems);
        Assert.Equal(1, r.IncompleteWorkItems);
        Assert.Equal(.1, r.ThroughputPerDay);
        Assert.Equal(.5, r.ThroughputPerFiveDays);
        Assert.Null(r.WorkItems[1].LeadTime);
        Assert.Null(r.WorkItems[1].CycleTime);
        Assert.Equal(3, r.AverageLeadTime);
        Assert.Equal(3, r.AverageActiveTime);
    }

    [Fact]
    public void SnapshotsCountEveryStateAtDayEndAndWipExcludesBacklogAndDone()
    {
        var r = Run(Scenario(days: 5, items: [new("A", "A", 1, 1, 1), new("B", "B", 1, 1, 1, ["A"])]));
        Assert.Equal(Enumerable.Range(0, 5), r.Days.Select(d => d.Day));
        Assert.All(r.Days, d =>
        {
            Assert.Equal(2, d.BacklogCount + d.TotalWip + d.DoneCount);
            Assert.Equal(d.DevelopmentCount + d.WaitingForCodeReviewCount + d.CodeReviewCount + d.WaitingForTestingCount + d.TestingCount, d.TotalWip);
        });
        Assert.Equal(new[] {1, 1, 0, 1, 1}, r.Days.Select(d => d.TotalWip));
        Assert.Equal(.8, r.AverageWip);
        Assert.Equal(1, r.Days[0].WaitingForCodeReviewCount);
        Assert.Equal(1, r.Days[1].WaitingForTestingCount);
        Assert.Equal(1, r.MaximumWaitingForCodeReviewQueue);
        Assert.Equal(1, r.MaximumWaitingForTestingQueue);
    }

    [Fact]
    public void UtilizationUsesSharedDeveloperAndIndependentTesterPools()
    {
        var r = Run(Scenario(days: 4, team: new Team(2, 1), items: [new("A", "A", 1, 1, 1), new("B", "B", 2, 1, 1)]));
        Assert.All(r.Days, d =>
        {
            Assert.Equal(2, d.AvailableDeveloperCapacity);
            Assert.Equal(1, d.AvailableTesterCapacity);
            Assert.Equal(d.DevelopmentWork + d.ReviewWork, d.UsedDeveloperCapacity);
            Assert.Equal(d.TestingWork, d.UsedTesterCapacity);
            Assert.InRange(d.UsedDeveloperCapacity, 0, 2);
            Assert.InRange(d.UsedTesterCapacity, 0, 1);
        });
        Assert.Equal(1, r.Days[1].ReviewWork);
        Assert.Equal(1, r.Days[1].DevelopmentWork);
        Assert.Equal(5.0 / 8, r.DeveloperUtilization);
        Assert.Equal(2.0 / 4, r.TesterUtilization);
        Assert.Equal(r.ReviewUtilization + r.DevelopmentUtilization, r.DeveloperUtilization);
    }

    [Fact]
    public void IncompleteQueuesAccumulateObservedWaitingButDoNotEnterCompletedAverages()
    {
        var r = Run(Scenario(days: 5, items: [new("A", "A", 1, 100, 1), new("B", "B", 1, 1, 1)], review: 1));
        Assert.Equal(4, r.WorkItems[1].WaitingForCodeReviewTime);
        Assert.Equal(0, r.AverageWaitingTime);
        Assert.Equal(0, r.AverageCycleTime);
        Assert.Equal(0, r.AverageLeadTime);
        Assert.All(r.WorkItems, w => Assert.Null(w.LeadTime));
    }

    [Fact]
    public void FutureItemsDoNotAccumulateTimeOrAppearInDailyCounts()
    {
        var r = Run(Scenario(days: 3, items: [new("A", "A", 1, 1, 1, createdDay: 10)]));
        Assert.Equal(1, r.IncompleteWorkItems);
        Assert.All(r.Days, d => Assert.Equal(0, d.BacklogCount + d.TotalWip + d.DoneCount));
        Assert.Equal(0, r.WorkItems[0].ActiveTime + r.WorkItems[0].WaitingTime + r.WorkItems[0].BlockedTime);
    }

    [Fact]
    public void ResultsAndSnapshotsAreDeterministicAndCollectionsReadOnly()
    {
        var s = Scenario();
        var first = Run(s);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(Run(s)));
        Assert.True(((ICollection<WorkItemResult>)first.WorkItems).IsReadOnly);
        Assert.True(((ICollection<DailySnapshot>)first.Days).IsReadOnly);
        Assert.True(((ICollection<WorkItemDaySnapshot>)first.Days[0].Items).IsReadOnly);
        Assert.True(((ICollection<StateTransition>)first.WorkItems[0].Transitions).IsReadOnly);
    }
}
