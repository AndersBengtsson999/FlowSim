using Simulation.Core;
using Xunit;
using static Simulation.Core.Tests.SimulationEngineTests;

namespace Simulation.Core.Tests;

public sealed class CodeReviewTests
{
    [Fact]
    public void ReviewAndDevelopmentShareExactlyOneDeveloperPoolAndReviewWins()
    {
        var result = new SimulationEngine().Run(Scenario(2, new Team(1, 1),
            [new("ReviewFirst", "A", 1, 1, 1), new("DevelopLater", "B", 5, 1, 1)]));
        Assert.Equal(1, result.Days[1].ReviewWork);
        Assert.Equal(0, result.Days[1].DevelopmentWork);
        Assert.Equal(5, result.WorkItems[1].RemainingDevelopmentEffort);
        Assert.All(result.Days, d => Assert.InRange(d.DevelopmentWork + d.ReviewWork, 0, 1));
        Assert.Equal(1, result.DeveloperUtilization);
        Assert.Equal(0.5, result.ReviewUtilization);
        Assert.Equal(0.5, result.DevelopmentUtilization);
    }

    [Fact]
    public void ReviewRemainderCanBeUsedForDevelopmentTheSameDay()
    {
        var result = new SimulationEngine().Run(Scenario(2, new Team(1, 1),
            [new("A", "A", 1, 0.25, 1), new("B", "B", 5, 1, 1)]));
        Assert.Equal(0.25, result.Days[1].ReviewWork);
        Assert.Equal(0.75, result.Days[1].DevelopmentWork);
    }

    [Fact]
    public void ReviewCannotUseFiveDevelopersToCompleteOneLargeReviewInADay()
    {
        var result = new SimulationEngine().Run(Scenario(3, items: [new("A", "A", 1, 5, 1)]));
        Assert.Equal(3, result.WorkItems[0].RemainingCodeReviewEffort);
        Assert.Equal(WorkItemStatus.CodeReview, result.WorkItems[0].State);
        Assert.Null(result.WorkItems[0].CodeReviewCompletedDay);
        Assert.Equal(1, result.Days[1].ReviewWork);
        Assert.Equal(1, result.Days[2].ReviewWork);
    }

    [Fact]
    public void ReviewQueueIsFifoAndWaitingDoesNotCountAsActiveWip()
    {
        var result = new SimulationEngine().Run(Scenario(6, new Team(2, 2),
            [new("Z", "First", 1, 3, 1), new("A", "Second", 1, 3, 1)], reviewWip: 1));
        Assert.Equal(1, result.WorkItems[0].CodeReviewStartedDay);
        Assert.Equal(4, result.WorkItems[1].CodeReviewStartedDay);
        Assert.Equal(WorkItemStatus.WaitingForCodeReview, result.Days[2].Items[1].State);
        Assert.Equal(1, result.Days[2].ReviewWip);
    }

    [Fact]
    public void TestingUsesOnlyTesterCapacityAndHasOneUnitItemCap()
    {
        var result = new SimulationEngine().Run(Scenario(4, new Team(1, 5),
            [new("A", "A", 1, 1, 5), new("B", "B", 10, 1, 1)]));
        Assert.Equal(1, result.Days[2].TestingWork);
        Assert.Equal(1, result.Days[2].DevelopmentWork);
        Assert.Equal(0, result.Days[2].ReviewWork);
        Assert.Equal(3, result.WorkItems[0].RemainingTestingEffort);
        Assert.All(result.Days, d => Assert.All(d.Items, w => Assert.InRange(w.TestingWork, 0, 1)));
    }

    [Fact]
    public void TestingQueueIsFifoAndAdmitsOnlyAtNextDayBoundary()
    {
        var result = new SimulationEngine().Run(Scenario(7, new Team(2, 2),
            [new("Z", "First", 1, 1, 3), new("A", "Second", 1, 1, 3)], testingWip: 1));
        Assert.Equal(2, result.WorkItems[0].TestingStartedDay);
        Assert.Equal(5, result.WorkItems[1].TestingStartedDay);
        Assert.Equal(WorkItemStatus.WaitingForTesting, result.Days[3].Items[1].State);
    }

    [Fact]
    public void PositiveEffortWithNoDevelopersCannotAdvance()
    {
        var result = new SimulationEngine().Run(Scenario(5, new Team(0, 2)));
        Assert.Equal(5, result.WorkItems[0].RemainingDevelopmentEffort);
        Assert.Equal(0, result.CompletedWorkItems);
        Assert.Equal(0, result.DeveloperUtilization);
    }

    [Fact]
    public void NoTestersLeavesPositiveTestingEffortUnchanged()
    {
        var result = new SimulationEngine().Run(Scenario(10, new Team(5, 0)));
        Assert.Equal(WorkItemStatus.Testing, result.WorkItems[0].State);
        Assert.Equal(2, result.WorkItems[0].RemainingTestingEffort);
        Assert.Equal(0, result.TesterUtilization);
        Assert.Equal(0, result.CompletedWorkItems);
    }

    [Fact]
    public void FinishedWorkEffortIsConservedAndTotalsStayWithinCapacity()
    {
        var result = new SimulationEngine().Run(Scenario(50, new Team(3, 2),
            [new("A", "A", 2.5, 1.5, 3), new("B", "B", 1, 0.25, 2)]));
        Assert.Equal(3.5, result.Days.Sum(d => d.DevelopmentWork));
        Assert.Equal(1.75, result.Days.Sum(d => d.ReviewWork));
        Assert.Equal(5, result.Days.Sum(d => d.TestingWork));
        Assert.Equal(2, result.CompletedWorkItems);
        Assert.Equal(result.DeveloperUtilization, result.DevelopmentUtilization + result.ReviewUtilization, 12);
        Assert.All(result.Days, d =>
        {
            Assert.InRange(d.DevelopmentWork + d.ReviewWork, 0, 3);
            Assert.InRange(d.TestingWork, 0, 2);
        });
    }
}
