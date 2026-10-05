using Simulation.Core;
using Xunit;
namespace Simulation.Core.Tests;

public sealed class DeliveryCostTests
{
    private static SimulationSession Create(StageProductivity? p = null, int developers = 1, double dev = 5,
        TechnicalDebtSettings? debt = null, DefectSettings? quality = null) => new(new SimulationScenario("Cost", 100,
        new(developers, 1), 1, 1, 1, [new("A", "A", dev, 2, 3)], 12345)
        { Productivity = p ?? new(), Debt = debt ?? new(), Quality = quality ?? new() });
    private static void Run(SimulationSession s, int days = 100) { for (var i = 0; i < days; i++) s.AdvanceOneDay(); }

    [Theory]
    [InlineData(1,1,1,5,2,3)]
    [InlineData(2,1,1,2.5,2,3)]
    [InlineData(1,2,1,5,1,3)]
    [InlineData(1,1,2,5,2,1.5)]
    public void EachStageAccumulatesConsumedCapacityAndProductivityReducesOnlyThatCost(double d, double r, double t, double cd, double cr, double ct)
    {
        var s = Create(new(d,r,t)); Run(s); var cost = s.WorkItems.Single().DeliveryCost;
        Assert.Equal(cd, cost.Development, 12); Assert.Equal(cr, cost.CodeReview, 12); Assert.Equal(ct, cost.Testing, 12);
        Assert.Equal(0, cost.Rework); Assert.Equal(cd + cr + ct, cost.Total, 12); Assert.True(cost.IsComplete);
        Assert.Equal(cost, s.GetResult().WorkItems.Single().DeliveryCost);
        Assert.Equal(cost, s.Days.Last().Items.Single().DeliveryCost);
    }
    [Fact]
    public void CollaborationCountsBothContributionsAsRawCapacity()
    {
        var s = Create(developers:2, dev:3); var day = s.AdvanceOneDay();
        Assert.Equal(1.5, day.DevelopmentWork); Assert.Equal(2, s.WorkItems.Single().DeliveryCost.Development);
        var frozen = day.Items.Single().DeliveryCost;
        s.AdvanceOneDay(); Assert.Equal(4, s.WorkItems.Single().DeliveryCost.Development);
        Assert.Equal(2, frozen!.Development); // old observations remain immutable
    }
    [Fact]
    public void RepeatedReworkAndReviewAttemptsKeepAccumulating()
    {
        var s = Create(quality:new() { Enabled=true, CodeReviewDefectProbability=1, CodeReviewReworkEffortDistribution=new FixedEffort(2) });
        Run(s,30); var item=s.WorkItems.Single();
        Assert.True(item.Transitions.Count(t=>t.To==WorkItemStatus.Rework)>2);
        double Consumed(WorkItemStatus stage) => item.Events.Where(e=>e.EventType==WorkItemEventType.CapacityApplied && e.FromState==stage).Sum(e=>e.CapacityConsumed);
        Assert.Equal(Consumed(WorkItemStatus.Rework), item.DeliveryCost.Rework);
        Assert.Equal(Consumed(WorkItemStatus.CodeReview), item.DeliveryCost.CodeReview);
        Assert.True(item.DeliveryCost.Rework>2); Assert.True(item.DeliveryCost.CodeReview>2);
    }
    [Theory]
    [InlineData(0,5.5)] [InlineData(1,3.85)]
    public void DebtAndShortcutAffectCostOnlyThroughActualDevelopmentEffort(double shortcut, double expected)
    {
        var s=Create(debt:new(){ShortcutRate=shortcut,ShortcutEffortReduction=.3,Tolerance=.1});
        s=SimulationSession.Restore(s.Capture() with { DebtState=new(20,100) });
        Run(s); Assert.Equal(expected,s.WorkItems.Single().DeliveryCost.Development,12);
        Assert.Equal(expected+5,s.WorkItems.Single().DeliveryCost.Total,12);
    }
    [Fact]
    public void RepaymentIsExcludedFromItemsButStillIncludedInUtilization()
    {
        var s=Create(new(1.5,1,1), debt:new(){Repayment=.5,Tolerance=1});
        s=SimulationSession.Restore(s.Capture() with { DebtState=new(10,100) });
        var day=s.AdvanceOneDay(); var c=s.WorkItems.Single().DeliveryCost;
        Assert.Equal(.5,day.UsedDebtRepaymentCapacity); Assert.Equal(.75,day.Debt!.Repaid);
        Assert.Equal(.5,c.Development); Assert.Equal(1,day.UsedDeveloperCapacity);
        Assert.Equal(1,s.GetResult().DeveloperUtilization);
        Run(s); Assert.Equal(5/1.5,s.WorkItems.Single().DeliveryCost.Development,12);
        Assert.Equal(s.Days.Sum(d=>d.UsedDeveloperCapacity-d.UsedDebtRepaymentCapacity+d.UsedTesterCapacity),s.WorkItems.Sum(w=>w.DeliveryCost.Total),10);
    }
    [Fact]
    public void MissingCostTracksFutureWorkWithoutInventingPastConsumption()
    {
        var s=Create(); s.AdvanceOneDay(); var state=s.Capture();
        s=SimulationSession.Restore(state with { WorkItems=state.WorkItems.Select(w=>w with { DeliveryCost=null }).ToArray() });
        Assert.False(s.WorkItems.Single().DeliveryCost.IsComplete); Assert.Equal(0,s.WorkItems.Single().DeliveryCost.Total);
        s.AdvanceOneDay(); Assert.Equal(1,s.WorkItems.Single().DeliveryCost.Development); Assert.False(s.WorkItems.Single().DeliveryCost.IsComplete);
        var fresh=Create().Capture(); var backlog=SimulationSession.Restore(fresh with { WorkItems=fresh.WorkItems.Select(w=>w with{DeliveryCost=null}).ToArray() });
        Assert.True(backlog.WorkItems.Single().DeliveryCost.IsComplete);
    }
    [Fact]
    public void InvalidPersistedCostIsRejected()
    {
        var state=Create().Capture();
        Assert.Throws<ScenarioValidationException>(()=>SimulationSession.Restore(state with { WorkItems=state.WorkItems.Select(w=>w with{DeliveryCost=new(-1)}).ToArray() }));
    }
}
