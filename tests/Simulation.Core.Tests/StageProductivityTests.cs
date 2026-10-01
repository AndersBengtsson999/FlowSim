using Simulation.Core;
using Xunit;
namespace Simulation.Core.Tests;

public sealed class StageProductivityTests
{
    // Detached active-stage fixtures isolate allocation from earlier stages and admission timing.
    private static SimulationSession Active(WorkItemStatus stage, StageProductivity p, double[] efforts, Team? team = null)
    {
        var s = new SimulationSession(new SimulationScenario("Stage allocation", 10, team ?? new(5, 5), 10, 10, 10,
            efforts.Select((e, i) => new WorkItem($"W{i}", "Work", e, e, e)).ToArray()) { Productivity = p });
        return SimulationSession.Restore(s.Capture() with { WorkItems = s.Capture().WorkItems.Select(w => w with {
            State = stage == WorkItemStatus.CodeReview ? WorkItemStatus.WaitingForCodeReview : stage == WorkItemStatus.Testing ? WorkItemStatus.WaitingForTesting : stage, RemainingReworkEffort = w.DevelopmentEffort, CurrentReworkEffort = w.DevelopmentEffort }).ToArray() });
    }
    [Theory]
    [InlineData(1, 1, 0, 1.4)] [InlineData(2, 2, 1, 2.1)]
    public void PrimaryAndCollaborationMultiplyWorkNotCapacity(int people, double capacity, double collaboration, double effective)
    {
        var s = Active(WorkItemStatus.Development, new(1.4, 1, 1), [100], new(people, 1)); var d = s.AdvanceOneDay();
        Assert.Equal(capacity, d.UsedDevelopmentCapacity, 12); Assert.Equal(effective, d.DevelopmentWork, 12);
        Assert.Equal(collaboration, d.CollaborationDevelopmentCapacity, 12);
        Assert.Equal(people, d.AvailableDeveloperCapacity); Assert.Equal(1, s.GetResult().DeveloperUtilization, 12);
        Assert.Equal(capacity, s.WorkItems.Single().Events.Sum(e => e.CapacityConsumed), 12);
    }
    [Theory]
    [InlineData(WorkItemStatus.CodeReview, 1.3)] [InlineData(WorkItemStatus.Testing, 1.4)] [InlineData(WorkItemStatus.Rework, 1)]
    public void StagesUseOnlyTheirOwnFactorWithoutCollaboration(WorkItemStatus stage, double effective)
    {
        var s = Active(stage, new(1.5, 1.3, 1.4), [100]); var d = s.AdvanceOneDay();
        Assert.Equal(effective, d.Items.Single().DevelopmentWork + d.ReviewWork + d.TestingWork + d.UsedReworkDeveloperCapacity, 12);
        Assert.Equal(1, stage == WorkItemStatus.Testing ? d.UsedTesterCapacity : d.UsedDeveloperCapacity, 12);
        Assert.Equal(5, d.AvailableDeveloperCapacity); Assert.Equal(5, d.AvailableTesterCapacity);
        Assert.Equal(0, d.CollaborationDevelopmentCapacity);
    }
    [Theory]
    [InlineData(WorkItemStatus.Development, .7)] [InlineData(WorkItemStatus.CodeReview, .65)] [InlineData(WorkItemStatus.Testing, .7)]
    public void FractionalFinishingCapacityIsReusedByNextEligibleItem(WorkItemStatus stage, double effort)
    {
        var s = Active(stage, new(1.4, 1.3, 1.4), [effort, 100], new(1, 1)); var d = s.AdvanceOneDay();
        var events = s.WorkItems.Select(w => w.Events.Single(e => e.EventType == WorkItemEventType.CapacityApplied)).ToArray();
        Assert.Equal(.5, events[0].CapacityConsumed, 12); Assert.Equal(.5, events[1].CapacityConsumed, 12);
        Assert.Equal(effort, events[0].EffortApplied, 12); Assert.Equal(effort, events[1].EffortApplied, 12);
        Assert.Equal(1, stage == WorkItemStatus.Testing ? d.UsedTesterCapacity : d.UsedDeveloperCapacity, 12);
    }
    [Fact]
    public void FractionalCollaborationFinishesClosestItemThenReusesRemainder()
    {
        var s = Active(WorkItemStatus.Development, new(1.4, 1, 1), [1.75, 100], new(3, 1)); var d = s.AdvanceOneDay();
        Assert.Equal(.5, d.Items[0].CollaborationDevelopmentCapacity, 12);
        Assert.Equal(.5, d.Items[1].CollaborationDevelopmentCapacity, 12);
        Assert.Equal(1.75, d.Items[0].DevelopmentWork, 12); Assert.Equal(1.75, d.Items[1].DevelopmentWork, 12);
        Assert.Equal(3, d.UsedDevelopmentCapacity, 12);
        Assert.Equal(WorkItemStatus.WaitingForCodeReview, d.Items[0].State);
    }
    [Fact]
    public void ReviewThenReworkRetainSharedPoolPriorityBeforeDevelopment()
    {
        var s = Active(WorkItemStatus.Development, new(1.5, 1.3, 1.4), [100, 100, 100], new(5, 2) { DeveloperAvailability = .3 });
        var state=s.Capture(); s=SimulationSession.Restore(state with { WorkItems = state.WorkItems.Select((w,i)=>w with {
            State = i==0 ? WorkItemStatus.CodeReview : i==1 ? WorkItemStatus.Rework : WorkItemStatus.Development }).ToArray() });
        var d=s.AdvanceOneDay(); Assert.Equal(1.3,d.ReviewWork,12); Assert.Equal(1,d.UsedReviewCapacity,12);
        Assert.Equal(.5,d.UsedReworkDeveloperCapacity,12); Assert.Equal(0,d.DevelopmentWork);
        Assert.Equal(1.5,d.AvailableDeveloperCapacity); Assert.Equal(1,s.GetResult().DeveloperUtilization,12);
        Assert.Equal(1/1.5,s.GetResult().ReviewUtilization,12);
    }
    [Theory]
    [InlineData(WorkItemStatus.Development, 1)] [InlineData(WorkItemStatus.Testing, 1)]
    [InlineData(WorkItemStatus.Development, .8)] [InlineData(WorkItemStatus.Testing, .8)]
    [InlineData(WorkItemStatus.Development, 0)] [InlineData(WorkItemStatus.Testing, 0)]
    public void AvailabilityPrecedesAllocationAndProductivityDoesNotInflateUtilization(WorkItemStatus stage, double availability)
    {
        var s=Active(stage,new(1.4,1.3,1.4),Enumerable.Repeat(100d,5).ToArray(),new(5,5){DeveloperAvailability=availability,TesterAvailability=availability});
        var d=s.AdvanceOneDay(); Assert.Equal(5*availability,d.AvailableDeveloperCapacity); Assert.Equal(5*availability,d.AvailableTesterCapacity);
        Assert.Equal(5*availability,stage==WorkItemStatus.Testing?d.UsedTesterCapacity:d.UsedDeveloperCapacity,12);
        Assert.Equal(7*availability,stage==WorkItemStatus.Testing?d.TestingWork:d.DevelopmentWork,12);
        Assert.Equal(availability==0?0:1,stage==WorkItemStatus.Testing?s.GetResult().TesterUtilization:s.GetResult().DeveloperUtilization,12);
    }
    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void InvalidFactorsAreRejectedIndependently(double value)
    {
        foreach(var p in new[]{new StageProductivity(value,1,1),new(1,value,1),new(1,1,value)})
            Assert.Throws<ScenarioValidationException>(()=>Active(WorkItemStatus.Development,p,[1]));
    }
    [Fact]
    public void SubUnitProductivityKeepsContributionCapacityLimit()
    {
        var s=Active(WorkItemStatus.CodeReview,new(1,.5,1),[2]);var d=s.AdvanceOneDay();
        Assert.Equal(.5,d.ReviewWork);Assert.Equal(1,d.UsedReviewCapacity);
    }
}
