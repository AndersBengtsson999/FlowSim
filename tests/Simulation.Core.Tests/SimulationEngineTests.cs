using System.Text.Json;
using Simulation.Core;
using Xunit;

namespace Simulation.Core.Tests;

public sealed class SimulationEngineTests
{
    internal static SimulationScenario Scenario(int days = 20, Team? team = null,
        WorkItem[]? items = null, int developmentWip = 5, int reviewWip = 3, int testingWip = 3) =>
        new("Test", days, team ?? new Team(), developmentWip, reviewWip, testingWip,
            items ?? [new("STORY-17", "Story", 5, 1, 2)]);

    [Fact]
    public void WorkItemInitializesIndependentEffortsAndAllTimingFields()
    {
        var dependencies = new List<string> { "A" };
        var item = new WorkItem("STORY-17", "Story", 5, 1, 2, dependencies);
        dependencies.Add("B");
        Assert.Equal(5, item.RemainingDevelopmentEffort);
        Assert.Equal(1, item.RemainingCodeReviewEffort);
        Assert.Equal(2, item.RemainingTestingEffort);
        Assert.Equal(WorkItemStatus.Backlog, item.State);
        Assert.Equal(0, item.CreatedDay);
        Assert.Single(item.Dependencies);
        Assert.Empty(item.Transitions);
        Assert.Null(item.DevelopmentStartedDay);
        Assert.Null(item.DevelopmentCompletedDay);
        Assert.Null(item.CodeReviewStartedDay);
        Assert.Null(item.CodeReviewCompletedDay);
        Assert.Null(item.TestingStartedDay);
        Assert.Null(item.TestingCompletedDay);
        Assert.Null(item.DoneDay);
        Assert.False(typeof(WorkItem).GetProperty(nameof(WorkItem.State))!.SetMethod!.IsPublic);
        Assert.False(typeof(WorkItem).GetProperty(nameof(WorkItem.RemainingDevelopmentEffort))!.SetMethod!.IsPublic);
    }

    [Fact]
    public void OneItemConsumesAtMostOneUnitDespiteFiveDevelopers()
    {
        var result = new SimulationEngine().Run(Scenario(days: 4));
        Assert.Equal(1, result.WorkItems[0].RemainingDevelopmentEffort);
        Assert.Null(result.WorkItems[0].DevelopmentCompletedDay);
        Assert.All(result.Days, d => Assert.Equal(1, d.DevelopmentWork));
    }

    [Fact]
    public void FiveDevelopersCanDevelopFiveItemsAtOnce()
    {
        var items = Enumerable.Range(1, 5).Select(i => new WorkItem($"S{i}", "Story", 5, 1, 2)).ToArray();
        var result = new SimulationEngine().Run(Scenario(days: 1, items: items));
        Assert.Equal(5, result.Days[0].DevelopmentWork);
        Assert.All(result.WorkItems, w => Assert.Equal(4, w.RemainingDevelopmentEffort));
        Assert.All(result.WorkItems, w => Assert.Equal(0, w.DevelopmentStartedDay));
    }

    [Theory]
    [InlineData(0.5, 0.5)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    public void ItemCapIsLimitedByBothOnePersonAndOneUnit(double perPerson, double expected)
    {
        var result = new SimulationEngine().Run(Scenario(1, new Team(5, 2, perPerson)));
        Assert.Equal(expected, result.Days[0].DevelopmentWork);
    }

    [Fact]
    public void EveryStateAndTimestampIsRecordedWithoutSkippingQueues()
    {
        var result = new SimulationEngine().Run(Scenario());
        var item = result.WorkItems[0];
        WorkItemStatus[] forwardFlow = [WorkItemStatus.Backlog, WorkItemStatus.Development,
            WorkItemStatus.WaitingForCodeReview, WorkItemStatus.CodeReview, WorkItemStatus.WaitingForTesting,
            WorkItemStatus.Testing, WorkItemStatus.Done];
        Assert.Equal(forwardFlow.Skip(1), item.Transitions.Select(t => t.To));
        Assert.Equal(forwardFlow.SkipLast(1), item.Transitions.Select(t => t.From));
        Assert.Equal(0, item.DevelopmentStartedDay);
        Assert.Equal(5, item.DevelopmentCompletedDay);
        Assert.Equal(5, item.CodeReviewStartedDay);
        Assert.Equal(6, item.CodeReviewCompletedDay);
        Assert.Equal(6, item.TestingStartedDay);
        Assert.Equal(8, item.TestingCompletedDay);
        Assert.Equal(8, item.DoneDay);
        Assert.Equal(WorkItemStatus.WaitingForCodeReview, result.Days[4].Items[0].State);
        Assert.Equal(WorkItemStatus.WaitingForTesting, result.Days[5].Items[0].State);
        Assert.Equal(WorkItemStatus.Testing, result.Days[6].Items[0].State);
        Assert.Equal(WorkItemStatus.Done, result.Days[7].Items[0].State);
        Assert.Equal(8, result.AverageLeadTime);
        Assert.Equal(8, result.AverageCycleTime);
        Assert.Equal(1.0 / 20, result.Throughput);
    }

    [Fact]
    public void AnItemNeverReceivesMultipleStagesOfWorkInOneDay()
    {
        var result = new SimulationEngine().Run(Scenario(items:
            Enumerable.Range(1, 8).Select(i => new WorkItem($"S{i}", "Story", 0.3, 0.2, 0.4)).ToArray()));
        Assert.All(result.Days, day => Assert.All(day.Items, item =>
        {
            Assert.InRange(new[] { item.DevelopmentWork, item.CodeReviewWork, item.TestingWork }.Count(w => w > 0), 0, 1);
            Assert.InRange(item.DevelopmentWork + item.CodeReviewWork + item.TestingWork, 0, 1);
        }));
    }

    [Fact]
    public void AllDependenciesMustCompleteBeforeAdmission()
    {
        var result = new SimulationEngine().Run(Scenario(items:
            [new("Dependent", "D", 1, 1, 1, ["A", "B"]), new("A", "A", 1, 1, 1), new("B", "B", 3, 1, 1)]));
        var byId = result.WorkItems.ToDictionary(w => w.Id);
        Assert.Equal(Math.Max(byId["A"].DoneDay!.Value, byId["B"].DoneDay!.Value), byId["Dependent"].DevelopmentStartedDay);
        Assert.All(result.Days.Take(byId["B"].DoneDay!.Value), d =>
            Assert.Equal(WorkItemStatus.Backlog, d.Items[0].State));
    }

    [Fact]
    public void BlockedOldItemDoesNotPreventEligibleItemsFromStarting()
    {
        var result = new SimulationEngine().Run(Scenario(1, items:
            [new("D", "Dependent", 1, 1, 1, ["A"]), new("A", "Parent", 1, 1, 1)], developmentWip: 1));
        Assert.Null(result.WorkItems[0].DevelopmentStartedDay);
        Assert.Equal(0, result.WorkItems[1].DevelopmentStartedDay);
        Assert.Equal(1, result.Days[0].BlockedItems);
    }

    [Fact]
    public void ActiveDevelopmentFifoUsesAdmissionTimeNotEarlierBlockedCreation()
    {
        var result = new SimulationEngine().Run(Scenario(4, new Team(1, 1),
            [new("D", "Dependent", 1, 1, 1, ["P"]), new("P", "Parent", 1, 1, 1),
             new("Y", "Younger", 10, 1, 1, createdDay: 1)], developmentWip: 2));
        Assert.Equal(3, result.WorkItems[0].DevelopmentStartedDay);
        Assert.Equal(1, result.WorkItems[2].DevelopmentStartedDay);
        Assert.Equal(1, result.Days[3].Items[2].DevelopmentWork);
        Assert.Equal(0, result.Days[3].Items[0].DevelopmentWork);
    }

    [Fact]
    public void AllThreeActiveWipLimitsHoldThroughoutTheRun()
    {
        var result = new SimulationEngine().Run(Scenario(40, new Team(8, 8),
            Enumerable.Range(1, 20).Select(i => new WorkItem($"S{i}", "Story", 1, 3, 4)).ToArray(), 4, 2, 1));
        Assert.All(result.Days, d =>
        {
            Assert.InRange(d.DevelopmentWip, 0, 4);
            Assert.InRange(d.ReviewWip, 0, 2);
            Assert.InRange(d.TestingWip, 0, 1);
            Assert.InRange(d.Items.Count(w => w.State == WorkItemStatus.Development), 0, 4);
            Assert.InRange(d.Items.Count(w => w.State == WorkItemStatus.CodeReview), 0, 2);
            Assert.InRange(d.Items.Count(w => w.State == WorkItemStatus.Testing), 0, 1);
        });
        Assert.Contains(result.Days, d => d.Items.Count(w => w.State == WorkItemStatus.WaitingForCodeReview) > 2);
        Assert.Contains(result.Days, d => d.Items.Count(w => w.State == WorkItemStatus.WaitingForTesting) > 1);
    }

    [Fact]
    public void WipPolicyExcludesWaitingStates()
    {
        Assert.False(WipPolicy.CountsToward(WorkItemStatus.WaitingForCodeReview, WorkItemStatus.CodeReview));
        Assert.False(WipPolicy.CountsToward(WorkItemStatus.WaitingForTesting, WorkItemStatus.Testing));
        Assert.False(WipPolicy.CountsToward(WorkItemStatus.WaitingForCodeReview, WorkItemStatus.Development));
        Assert.True(WipPolicy.CountsToward(WorkItemStatus.CodeReview, WorkItemStatus.CodeReview));
    }

    [Fact]
    public void FifoTiesFollowInputOrderRatherThanIdSorting()
    {
        var result = new SimulationEngine().Run(Scenario(1, new Team(1, 1),
            [new("Z", "First", 5, 1, 1), new("A", "Second", 5, 1, 1)], developmentWip: 2));
        Assert.Equal(1, result.Days[0].Items[0].DevelopmentWork);
        Assert.Equal(0, result.Days[0].Items[1].DevelopmentWork);
    }

    [Fact]
    public void EarlierCreationWinsBacklogAdmissionEvenWhenLaterInInput()
    {
        var result = new SimulationEngine().Run(Scenario(2, new Team(1, 1),
            [new("New", "New", 5, 1, 1, createdDay: 1), new("Old", "Old", 5, 1, 1)], developmentWip: 1));
        Assert.Null(result.WorkItems[0].DevelopmentStartedDay);
        Assert.Equal(0, result.WorkItems[1].DevelopmentStartedDay);
    }

    [Fact]
    public void IdenticalRunsHaveIdenticalFullHistoriesAndLeaveInputsUntouched()
    {
        var scenario = Scenario(items: [new("A", "A", 2.5, 1.2, 2), new("B", "B", 1, 1, 2, ["A"])]);
        var engine = new SimulationEngine();
        Assert.Equal(JsonSerializer.Serialize(engine.Run(scenario)), JsonSerializer.Serialize(engine.Run(scenario)));
        Assert.All(scenario.WorkItems, w =>
        {
            Assert.Equal(WorkItemStatus.Backlog, w.State);
            Assert.Equal(w.DevelopmentEffort, w.RemainingDevelopmentEffort);
            Assert.Empty(w.Transitions);
        });
    }

    [Fact]
    public void FutureArrivalsDoNotStartOrCountAsUnfinishedBeforeCreation()
    {
        var result = new SimulationEngine().Run(Scenario(10, items: [new("A", "A", 1, 1, 1, createdDay: 3)]));
        Assert.All(result.Days.Take(3), d => Assert.Equal(0, d.UnfinishedItems));
        Assert.Equal(3, result.WorkItems[0].DevelopmentStartedDay);
        Assert.Equal(6, result.WorkItems[0].DoneDay);
        Assert.Equal(3, result.AverageLeadTime);
    }

    [Fact]
    public void EmptyWorkloadHasFiniteZeroMetrics()
    {
        var result = new SimulationEngine().Run(Scenario(items: []));
        Assert.Equal(0, result.CompletedWorkItems);
        Assert.Equal(0, result.Throughput);
        Assert.Equal(0, result.AverageLeadTime);
        Assert.Equal(0, result.AverageCycleTime);
        Assert.Equal(0, result.AverageWip);
        Assert.Equal(0, result.BlockedTimeFraction);
        Assert.Equal(0, result.DeveloperUtilization);
        Assert.Equal(0, result.TesterUtilization);
    }

    [Fact]
    public void ZeroEffortStillVisitsEveryStateAndTakesOneDayPerStage()
    {
        var result = new SimulationEngine().Run(Scenario(3, new Team(0, 0), [new("A", "A", 0, 0, 0)]));
        Assert.Equal(3, result.WorkItems[0].DoneDay);
        Assert.Equal(6, result.WorkItems[0].Transitions.Count);
        Assert.Equal(0, result.DeveloperUtilization);
        Assert.Equal(0, result.TesterUtilization);
    }

    [Fact]
    public void FractionalCapacityDoesNotAddADayForFloatingPointResidue()
    {
        var result = new SimulationEngine().Run(Scenario(10, new Team(1, 1, 0.1), [new("A", "A", 1, 1, 1)]));
        Assert.Equal(10, result.WorkItems[0].DevelopmentCompletedDay);
        Assert.Equal(0, result.WorkItems[0].RemainingDevelopmentEffort);
    }

    [Fact]
    public void CancellationIsObservedBeforeWork()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        Assert.Throws<OperationCanceledException>(() => new SimulationEngine().Run(Scenario(), source.Token));
    }
}
