using System.Text.Json;
using Simulation.Core;
using Xunit;

namespace Simulation.Core.Tests;

public sealed class DefectTests
{
    private static DefectSettings Quality(double review = 1, double testing = 0, double effort = 1, int wip = 3) => new()
    {
        Enabled = true, CodeReviewDefectProbability = review, TestingDefectProbability = testing, ReworkWipLimit = wip,
        CodeReviewReworkEffortDistribution = new FixedEffort(effort), TestingReworkEffortDistribution = new FixedEffort(effort)
    };
    private static SimulationScenario Scenario(int days = 10, DefectSettings? quality = null, WorkItem[]? items = null, Team? team = null, int reviewWip = 3) =>
        new("Defects", days, team ?? new Team(2, 1), 5, reviewWip, 3, items ?? [new("A", "A", 1, 1, 1)]) { Quality = quality ?? Quality() };
    private static SimulationResult Run(SimulationScenario s) => new SimulationEngine().Run(s);

    [Fact]
    public void DisabledAndZeroProbabilityHaveExactlyTheSameExecution()
    {
        var s = Scenario(quality: Quality() with { Enabled = false });
        var disabled = Run(s);
        var zero = Run(s with { Quality = Quality(0, 0) });
        Assert.Equal(JsonSerializer.Serialize(disabled), JsonSerializer.Serialize(zero));
        Assert.Equal(1, disabled.CompletedWorkItems);
        Assert.Equal(0, disabled.TotalDefectsFound);
        Assert.Equal(0, disabled.TotalReworkEffort);
        Assert.Equal(0, disabled.ReworkDeveloperCapacityShare);
        Assert.Equal(1, disabled.WorkItems[0].CodeReviewAttempts);
    }

    [Fact]
    public void CertainCodeReviewDefectsLoopWithinTheFiniteHorizon()
    {
        var r = Run(Scenario(days: 5));
        var w = r.WorkItems[0];
        Assert.Equal(2, w.CodeReviewDefectsFound);
        Assert.Equal(0, w.TestingDefectsFound);
        Assert.Equal(2, w.ReworkCount);
        Assert.Equal(2, w.TotalReworkEffort);
        Assert.Equal(2, w.ReworkActiveTime);
        Assert.Equal(5, w.ActiveTime);
        Assert.Equal(WorkItemStatus.WaitingForCodeReview, w.FinalState);
        Assert.Equal(0, r.CompletedWorkItems);
        Assert.Null(w.DoneDay);
        Assert.Equal(WorkItemStatus.WaitingForRework, r.Days[1].Items[0].State);
        Assert.Equal(WorkItemStatus.WaitingForCodeReview, r.Days[2].Items[0].State);
        Assert.All(w.InspectionAttempts, a => Assert.True(a.DefectFound));
        Assert.All(w.Events.Where(e => e.EventType == WorkItemEventType.DefectFound), e => Assert.Equal(DefectSource.CodeReview, e.DefectSource));
    }

    [Fact]
    public void TestingDefectsAlwaysReturnThroughReview()
    {
        var r = Run(Scenario(days: 6, quality: Quality(0, 1)));
        var w = r.WorkItems[0];
        Assert.Equal(2, w.TestingDefectsFound);
        Assert.Equal(2, w.CodeReviewAttempts);
        Assert.Equal(2, w.TestingAttempts);
        Assert.Equal(1, w.ReworkCount);
        Assert.Equal(1, w.TotalReworkEffort);
        Assert.Equal(2, w.RequiredReworkEffort);
        Assert.Equal(1, w.RemainingReworkEffort);
        Assert.Equal(WorkItemStatus.WaitingForRework, w.FinalState);
        var loop = w.Transitions.Skip(5).Take(5).Select(t => t.To).ToArray();
        Assert.Equal(new[] {WorkItemStatus.WaitingForRework, WorkItemStatus.Rework, WorkItemStatus.WaitingForCodeReview,
            WorkItemStatus.CodeReview, WorkItemStatus.WaitingForTesting}, loop);
        Assert.Equal(1, w.FirstCodeReviewStartedDay);
        Assert.Equal(2, w.FirstTestingStartedDay);
        Assert.Equal(5, w.CodeReviewCompletedDay);
        Assert.Equal(6, w.TestingCompletedDay);
        Assert.Equal(new[] {1, 4}, w.InspectionAttempts.Where(a => a.Stage == DefectSource.CodeReview).Select(a => a.StartedDay));
    }

    [Fact]
    public void CapacityPriorityIsReviewThenReworkThenDevelopment()
    {
        var items = Enumerable.Range(1, 4).Select(i => new WorkItem($"A{i}", "A", .5, 1, 1)).Append(new WorkItem("E", "E", 10, 1, 1, createdDay: 2)).ToArray();
        var r = Run(Scenario(days: 4, quality: Quality(wip: 2), items: items, team: new Team(2, 1), reviewWip: 4));
        Assert.Equal(2, r.Days[2].ReviewWork);
        Assert.Equal(0, r.Days[2].UsedReworkDeveloperCapacity);
        Assert.Equal(0, r.Days[2].DevelopmentWork);
        Assert.Equal(2, r.Days[3].UsedReworkDeveloperCapacity);
        Assert.Equal(0, r.Days[3].DevelopmentWork);
        Assert.All(r.Days, d => Assert.InRange(d.UsedDeveloperCapacity, 0, 2));
    }

    [Fact]
    public void ReworkWipWaitingActiveAndTotalWipAreObserved()
    {
        var items = Enumerable.Range(1, 4).Select(i => new WorkItem($"A{i}", "A", 1, 1, 1)).ToArray();
        var r = Run(Scenario(days: 3, quality: Quality(effort: 3, wip: 1), items: items, team: new Team(4, 1), reviewWip: 4));
        var d = r.Days[2];
        Assert.Equal(3, d.WaitingForReworkCount);
        Assert.Equal(1, d.ReworkCount);
        Assert.Equal(4, d.TotalWip);
        Assert.Equal(1, d.ReworkWip);
        Assert.Equal(1, d.UsedReworkDeveloperCapacity);
        Assert.Equal(1, d.UsedDeveloperCapacity);
        Assert.Equal(3, r.WorkItems[0].ActiveTime);
        Assert.Equal(1, r.WorkItems[0].ReworkActiveTime);
        Assert.All(r.WorkItems.Skip(1), w => { Assert.Equal(1, w.WaitingForReworkTime); Assert.Equal(1, w.WaitingTime); });
        Assert.Equal(4, r.TotalDefectsFound);
        Assert.Equal(4, r.WorkItemsWithDefects);
        Assert.Equal(1, r.TotalReworkCount);
        Assert.Equal(1, r.TotalReworkEffort);
        Assert.Equal(1d / 9, r.ReworkDeveloperCapacityShare, 12);
        Assert.Equal(1, r.AverageCodeReviewAttempts);
        Assert.Equal(0, r.AverageTestingAttempts);
        Assert.Equal(0, r.AverageReworkEffortPerCompletedItem);
    }

    [Fact]
    public void DefectEventsAndResultsAreReproducible()
    {
        var s = Scenario(days: 100, quality: Quality(.4, .3), items: Enumerable.Range(1, 20).Select(i => new WorkItem($"A{i}", "A", 1, 1, 1)).ToArray());
        var a = Run(s);
        Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(Run(s)));
        Assert.NotEqual(JsonSerializer.Serialize(a.WorkItems.Select(w => w.Events)), JsonSerializer.Serialize(Run(s with { RandomSeed = 77 }).WorkItems.Select(w => w.Events)));
        Assert.True(a.CompletedWorkItems > 0);
        var completed = a.WorkItems.Where(w => w.DoneDay.HasValue).ToArray();
        Assert.Equal(completed.Average(w => w.TotalReworkEffort), a.AverageReworkEffortPerCompletedItem);
        Assert.Equal(a.WorkItems.Sum(w => w.DefectsFound), a.TotalDefectsFound);
        Assert.All(a.Days, d => Assert.Equal(d.DevelopmentWork + d.ReviewWork + d.UsedReworkDeveloperCapacity, d.UsedDeveloperCapacity));
        Assert.All(a.WorkItems, w => Assert.True(((ICollection<WorkItemEvent>)w.Events).IsReadOnly));
        Assert.All(s.WorkItems, w => Assert.Empty(w.Events));
    }

    [Fact]
    public void ZeroEffortReworkStillUsesSeparateDaysAndDoesNotCountAsActiveWork()
    {
        var r = Run(Scenario(days: 5, quality: Quality(effort: 0)));
        Assert.Equal(2, r.TotalReworkCount);
        Assert.Equal(0, r.TotalReworkEffort);
        Assert.Equal(0, r.WorkItems[0].ReworkActiveTime);
        Assert.Equal(3, r.WorkItems[0].ActiveTime);
        Assert.All(r.Days, d => Assert.All(d.Items, w => Assert.InRange(new[] {w.DevelopmentWork, w.CodeReviewWork, w.TestingWork, w.ReworkWork}.Count(x => x > 0), 0, 1)));
    }

    [Fact]
    public void DependentItemWaitsThroughEveryPredecessorReworkLoop()
    {
        var r = Run(Scenario(days: 20, items: [new("A", "A", 1, 1, 1), new("B", "B", 1, 1, 1, ["A"])]));
        Assert.Equal(WorkItemStatus.Backlog, r.WorkItems[1].FinalState);
        Assert.Equal(20, r.WorkItems[1].BlockedTime);
        Assert.Null(r.WorkItems[1].DevelopmentStartedDay);
    }

    [Fact]
    public void RepeatedInspectionUsesOriginalEffortAndRecordsCapacityEvents()
    {
        var r = Run(Scenario(days: 10, quality: Quality(0, 1), items: [new("A", "A", 1, 2, 2)]));
        var w = r.WorkItems[0];
        Assert.Equal(4, r.Days.Sum(d => d.ReviewWork));
        Assert.Equal(4, r.Days.Sum(d => d.TestingWork));
        Assert.Equal(1, w.TotalReworkEffort);
        Assert.Equal(1, w.Events.Count(e => e.EventType == WorkItemEventType.CapacityApplied && e.FromState == WorkItemStatus.Rework));
        Assert.Equal(2, w.CodeReviewAttempts);
        Assert.Equal(2, w.TestingAttempts);
    }

    [Fact]
    public void ReworkReturnsToTheTailOfTheReviewQueueUsingItsNewEntryDay()
    {
        var items = new WorkItem[] { new("A", "A", 1, 1, 1), new("B", "B", 1, 3, 1), new("C", "C", 2, 1, 1) };
        var r = Run(Scenario(days: 6, items: items, team: new Team(3, 1), reviewWip: 1));
        Assert.Equal(5, r.WorkItems[2].CodeReviewStartedDay);
        Assert.Equal(1, r.WorkItems[0].CodeReviewAttempts);
        Assert.Equal(3, r.WorkItems[0].WaitingForCodeReviewTime);
    }

    [Fact]
    public void IncompleteInspectionDoesNotDiscoverADefect()
    {
        var r = Run(Scenario(days: 2, items: [new("A", "A", 1, 5, 1)]));
        Assert.Equal(0, r.TotalDefectsFound);
        Assert.Equal(1, r.WorkItems[0].CodeReviewAttempts);
        Assert.Null(r.WorkItems[0].InspectionAttempts[0].CompletedDay);
        Assert.Null(r.WorkItems[0].InspectionAttempts[0].DefectFound);
    }

    [Fact]
    public void DiscoverySourceSelectsItsOwnReworkDistribution()
    {
        var quality = Quality() with { CodeReviewReworkEffortDistribution = new FixedEffort(.25), TestingReworkEffortDistribution = new FixedEffort(2.5) };
        var review = Run(Scenario(days: 3, quality: quality));
        Assert.Equal(.25, review.WorkItems[0].RequiredReworkEffort);
        Assert.Equal(.25, review.TotalReworkEffort);
        var testing = Run(Scenario(days: 4, quality: quality with { CodeReviewDefectProbability = 0, TestingDefectProbability = 1 }));
        Assert.Equal(2.5, testing.WorkItems[0].RequiredReworkEffort);
        Assert.Equal(1, testing.TotalReworkEffort);
        Assert.Equal(1.5, testing.WorkItems[0].RemainingReworkEffort);
    }

    [Fact]
    public void InvalidQualitySettingsAreRejectedEvenWhenDisabled()
    {
        foreach (var probability in new[] {-1d, 1.1, double.NaN, double.PositiveInfinity})
        {
            Assert.Throws<ScenarioValidationException>(() => Run(Scenario(quality: Quality(probability))));
            Assert.Throws<ScenarioValidationException>(() => Run(Scenario(quality: Quality(0, probability) with { Enabled = false })));
        }
        Assert.Throws<ScenarioValidationException>(() => Run(Scenario(quality: Quality(wip: 0))));
        Assert.Throws<ScenarioValidationException>(() => Run(Scenario(quality: Quality() with { TestingReworkEffortDistribution = null! })));
    }
}
