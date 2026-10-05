using System.Text.Json;
using Simulation.Core;
using Xunit;
namespace Simulation.Core.Tests;

public sealed class TechnicalDebtTests
{
    private static SimulationSession Session(TechnicalDebtSettings? settings = null, TechnicalDebtState? debt = null,
        int developers = 5, double effort = 100, int count = 5, StageProductivity? productivity = null, DefectSettings? quality = null)
    {
        var s = new SimulationSession(new SimulationScenario("Debt", 200, new(developers, 3), count, 5, 5,
            Enumerable.Range(0,count).Select(i=>new WorkItem($"W{i}","Work",effort,1,1)).ToArray(), 12345)
            { Debt=settings??new(),Productivity=productivity??new(),Quality=quality??new() });
        return debt is null ? s : SimulationSession.Restore(s.Capture() with { DebtState=debt });
    }
    [Theory]
    [InlineData(1, 1.5)] [InlineData(1.5, 2.25)] [InlineData(2, 3)]
    public void CreationFactorScalesOnlySavedEffortAtCompletion(double factor, double expected)
    {
        var s = Session(new() { ShortcutRate = 1, ShortcutEffortReduction = .3, CreationFactor = factor }, developers: 1, effort: 5, count: 1);
        s.AdvanceOneDay();
        Assert.Equal(0, s.DebtState.Amount);
        Assert.Equal(1.5, s.WorkItems.Single().DevelopmentPlan!.SavedEffort, 12);
        while (s.CurrentDay < 4) s.AdvanceOneDay();
        Assert.Equal(expected, s.DebtState.Amount, 12);
        Assert.Equal(5, s.DebtState.CumulativeDevelopmentScope);
    }

    [Fact]
    public void FactorChangeUsesOldFactorForActiveCompletionAndNewFactorForNextStart()
    {
        var s = new SimulationSession(new SimulationScenario("Factor timing", 20, new(1, 1), 1, 1, 1,
            [new WorkItem("A", "A", 5, 0, 0), new WorkItem("B", "B", 5, 0, 0)], 12345)
            { Debt = new() { ShortcutRate = 1, ShortcutEffortReduction = .3, Tolerance = 1 } });
        s.AdvanceOneDay();
        var plan = s.WorkItems[0].DevelopmentPlan;
        s.ApplyChanges(s.Configuration with { Debt = s.Configuration.Debt with { CreationFactor = 2 } });
        while (s.CurrentDay < 4) s.AdvanceOneDay();
        Assert.Equal(plan, s.WorkItems[0].DevelopmentPlan);
        Assert.Equal(1.5, s.DebtState.Amount, 12);
        while (s.CurrentDay < 8) s.AdvanceOneDay();
        Assert.Equal(3, s.WorkItems[1].DevelopmentPlan!.DebtToCreate, 12);
        Assert.Equal(4.5, s.DebtState.Amount, 12);
    }

    [Fact]
    public void ZeroShortcutsCreateNoDebtEvenWithLargeCreationFactor()
    {
        var s = Session(new() { CreationFactor = 1000000 }, effort: 2);
        for (var i = 0; i < 30; i++) s.AdvanceOneDay();
        Assert.Equal(0, s.DebtState.Amount);
    }

    [Fact]
    public void ZeroRateHasNoChoicesDebtOrExtraRandomDrawsAndScopeUsesBaseCompletions()
    {
        var s=Session(effort:2,count:10);var random=s.Capture().ArrivalRandomState;
        for(int i=0;i<50;i++)s.AdvanceOneDay();
        Assert.Equal(random,s.Capture().ArrivalRandomState);Assert.Equal(0,s.DebtState.Amount);
        Assert.Equal(20,s.DebtState.CumulativeDevelopmentScope);Assert.All(s.WorkItems,w=>Assert.Null(w.DevelopmentPlan));
    }
    [Theory]
    [InlineData(0,0)] [InlineData(1,20)]
    public void EndpointShortcutRatesChooseAtStart(double rate,int expected)
    {
        var s=Session(new(){ShortcutRate=rate},developers:0,count:20);s.AdvanceOneDay();
        Assert.Equal(expected,s.WorkItems.Count(w=>w.DevelopmentPlan?.IsShortcut==true));Assert.Equal(0,s.DebtState.Amount);
    }
    [Fact]
    public void IntermediateChoiceIsSeededOnceAndNotRerolledDaily()
    {
        var a=Session(new(){ShortcutRate=.4},developers:0,count:100);var b=Session(new(){ShortcutRate=.4},developers:0,count:100);
        a.AdvanceOneDay();b.AdvanceOneDay();Assert.InRange(a.WorkItems.Count(w=>w.DevelopmentPlan!.IsShortcut),1,99);
        Assert.Equal(JsonSerializer.Serialize(a.Capture()),JsonSerializer.Serialize(b.Capture()));
        var plans=a.WorkItems.Select(w=>w.DevelopmentPlan).ToArray();var random=a.Capture().ArrivalRandomState;
        for(int i=0;i<20;i++)a.AdvanceOneDay();Assert.Equal(plans,a.WorkItems.Select(w=>w.DevelopmentPlan));Assert.Equal(random,a.Capture().ArrivalRandomState);
    }
    [Fact]
    public void EffortLocksOverheadBeforeShortcutAndNoProductivityIsEmbedded()
    {
        var s=Session(new(){ShortcutRate=1,ShortcutEffortReduction=.3,Tolerance=.1},new(20,100),0,5,1,new(1.5,1.3,1.4));
        s.AdvanceOneDay();var p=s.WorkItems.Single().DevelopmentPlan!;
        Assert.Equal(5,p.BaseEffort);Assert.Equal(.1,p.Overhead,12);Assert.Equal(5.5,p.EffortWithDebt,12);
        Assert.Equal(3.85,p.FinalEffort,12);Assert.Equal(1.65,p.SavedEffort,12);Assert.Equal(1.65,p.DebtToCreate,12);
        Assert.Equal(20,s.DebtState.Amount);Assert.Equal(100,s.DebtState.CumulativeDevelopmentScope);
        s.ApplyChanges(s.Configuration with{Team=new(1,1),Debt=new(){ShortcutRate=0,Tolerance=1,Repayment=1,ShortcutEffortReduction=1}});
        Assert.Equal(3.85,s.WorkItems.Single().RemainingDevelopmentEffort,12);s.AdvanceOneDay();
        Assert.Equal(p,s.WorkItems.Single().DevelopmentPlan);Assert.Equal(3.85,s.WorkItems.Single().RemainingDevelopmentEffort,12); // all capacity repays debt
    }
    [Fact]
    public void DebtAndOriginalScopeAreAddedOnceAtDevelopmentCompletionNotStartOrRework()
    {
        var s=Session(new(){ShortcutRate=1,ShortcutEffortReduction=.3},developers:1,effort:5,count:1,
            quality:new(){Enabled=true,CodeReviewDefectProbability=1});
        for(int i=0;i<3;i++){s.AdvanceOneDay();Assert.Equal(0,s.DebtState.Amount);Assert.Equal(0,s.DebtState.CumulativeDevelopmentScope);}
        s.AdvanceOneDay();Assert.Equal(1.5,s.DebtState.Amount,12);Assert.Equal(5,s.DebtState.CumulativeDevelopmentScope);
        for(int i=0;i<30;i++)s.AdvanceOneDay();Assert.True(s.GetResult().TotalReworkCount>1);
        Assert.Equal(1.5,s.DebtState.Amount,12);Assert.Equal(5,s.DebtState.CumulativeDevelopmentScope);
    }
    [Theory]
    [InlineData(20,200,.1)] [InlineData(20,500,.04)] [InlineData(0,0,0)] [InlineData(20,0,0)]
    public void RatioIsScopeNormalizedWithExplicitZeroConvention(double debt,double scope,double ratio) => Assert.Equal(ratio,new TechnicalDebtState(debt,scope).Ratio,12);
    [Theory]
    [InlineData(5,0)] [InlineData(10,0)] [InlineData(15,.05)] [InlineData(25,.15)]
    public void OnlyExcessAboveToleranceAddsEffort(double debt,double overhead)
    {
        var s=Session(new(){Tolerance=.1},new(debt,100),0,5,1);s.AdvanceOneDay();
        Assert.Equal(overhead,s.DebtState.Overhead(s.Configuration.Debt),12);
        Assert.Equal(5*(1+overhead),s.WorkItems.Single().RemainingDevelopmentEffort,12);
    }
    [Theory]
    [InlineData(1,100,1,1)] [InlineData(1.5,100,1,1.5)] [InlineData(1.5,.3,.2,.3)] [InlineData(1.5,0,0,0)]
    public void RepaymentUsesFractionOfRemainingPoolProductivityAndOnlyNecessaryCapacity(double productivity,double amount,double capacity,double work)
    {
        var s=Session(new(){Repayment=.2},new(amount,1000),productivity:new(productivity,2,3));var d=s.AdvanceOneDay();
        Assert.Equal(capacity,d.UsedDebtRepaymentCapacity,12);Assert.Equal(work,d.Debt!.Repaid,12);
        Assert.Equal(amount-work,s.DebtState.Amount,12);Assert.Equal(5-capacity,d.UsedDevelopmentCapacity,12);
        Assert.Equal((5-capacity)*productivity,d.DevelopmentWork,12);Assert.Equal(5,d.UsedDeveloperCapacity,12);
        Assert.Equal(1,s.GetResult().DeveloperUtilization,12);Assert.Equal(0,d.CollaborationDevelopmentCapacity);
    }
    [Fact]
    public void ReviewAndReworkConsumeFirstThenRepaymentUsesOnlyRemainingPool()
    {
        var s=Session(new(){Repayment=.2},new(100,1000),count:5);var state=s.Capture();
        s=SimulationSession.Restore(state with {WorkItems=state.WorkItems.Select((w,i)=>w with {State=i==0?WorkItemStatus.WaitingForCodeReview:i==1?WorkItemStatus.Rework:WorkItemStatus.Backlog,RemainingReworkEffort=100,CurrentReworkEffort=100}).ToArray()});
        var d=s.AdvanceOneDay();Assert.Equal(1,d.UsedReviewCapacity);Assert.Equal(1,d.UsedReworkDeveloperCapacity);
        Assert.Equal(.6,d.UsedDebtRepaymentCapacity,12);Assert.Equal(2.4,d.UsedDevelopmentCapacity,12);Assert.Equal(5,d.UsedDeveloperCapacity,12);
    }
    [Fact]
    public void RepaymentHasNoCollaborationOrHeadcountContributionLimit()
    {
        var s=Session(new(){Repayment=1},new(100,1000),developers:1,count:1,productivity:new(1.5,3,4));
        var state=s.Capture();s=SimulationSession.Restore(state with{Configuration=state.Configuration with{Team=new(1,1,5,1)}});
        var d=s.AdvanceOneDay();Assert.Equal(5,d.UsedDebtRepaymentCapacity);Assert.Equal(7.5,d.Debt!.Repaid);Assert.Equal(0,d.CollaborationDevelopmentCapacity);Assert.Equal(0,d.DevelopmentWork);
    }
    [Fact]
    public void SameDayStartsDoNotSeeDebtsCreatedLaterAndNextDayStartsDo()
    {
        var s=Session(new(){ShortcutRate=1,ShortcutEffortReduction=.5,Tolerance=0},developers:5,effort:1,count:3);
        var state=s.Capture();s=SimulationSession.Restore(state with{WorkItems=state.WorkItems.Select((w,i)=>w with{CreatedDay=i==2?1:0}).ToArray()});
        s.AdvanceOneDay();Assert.All(s.WorkItems.Take(2),w=>Assert.Equal(0,w.DevelopmentPlan!.Overhead));
        Assert.Equal(1,s.DebtState.Amount);Assert.Equal(2,s.DebtState.CumulativeDevelopmentScope);
        s.AdvanceOneDay();Assert.Equal(.5,s.WorkItems[2].DevelopmentPlan!.Overhead);Assert.Equal(.75,s.WorkItems[2].DevelopmentPlan!.FinalEffort);
    }
    [Theory]
    [InlineData(-.01)] [InlineData(1.01)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void InvalidDebtPercentagesAreRejected(double value)
    {
        foreach(var settings in new[]{new TechnicalDebtSettings{ShortcutRate=value},new(){ShortcutEffortReduction=value},new(){Tolerance=value},new(){Repayment=value}})
            Assert.Throws<ScenarioValidationException>(()=>Session(settings));
    }
    [Fact]
    public void AvailabilityRemainsUpstreamOfDebtAllocation()
    {
        var s=Session(new(){Repayment=.25},new(10,1000));var state=s.Capture();
        s=SimulationSession.Restore(state with{Configuration=state.Configuration with{Team=state.Configuration.Team with{DeveloperAvailability=.8}}});
        var d=s.AdvanceOneDay();Assert.Equal(4,d.AvailableDeveloperCapacity);Assert.Equal(1,d.UsedDebtRepaymentCapacity);Assert.Equal(3,d.UsedDevelopmentCapacity);
    }
    [Fact]
    public void OverheadAndShortcutCompletionStillAddsOnlyOriginalBaseScope()
    {
        var s=Session(new(){ShortcutRate=1,ShortcutEffortReduction=.3,Tolerance=.1},new(20,100),5,5,1);
        while(s.WorkItems.Single().DevelopmentCompletedDay is null)s.AdvanceOneDay();
        Assert.Equal(105,s.DebtState.CumulativeDevelopmentScope);Assert.Equal(21.65,s.DebtState.Amount,12);
        Assert.Equal(3.85,s.WorkItems.Single().DevelopmentPlan!.FinalEffort,12);
    }
    [Fact]
    public void FiftyPercentRepaymentWithNoDebtIsIdenticalToNoRepayment()
    {
        var a=Session(new(){Repayment=.5},effort:3,count:10);var b=Session(effort:3,count:10);
        for(int i=0;i<30;i++){a.AdvanceOneDay();b.AdvanceOneDay();}
        Assert.Equal(JsonSerializer.Serialize(a.GetResult()),JsonSerializer.Serialize(b.GetResult()));
    }
    [Fact]
    public void InvalidPersistedDebtAndNonFiniteOverheadAreRejected()
    {
        var s=Session();Assert.Throws<ScenarioValidationException>(()=>SimulationSession.Restore(s.Capture() with{DebtState=new(-1,1)}));
        Assert.Throws<ScenarioValidationException>(()=>new TechnicalDebtState(2,1).Overhead(new(){Tolerance=0,ImpactFactor=double.MaxValue}));
        Assert.Throws<ScenarioValidationException>(()=>Session(new(){CreationFactor=-1}));
        Assert.Throws<ScenarioValidationException>(()=>Session(new(){ImpactFactor=double.PositiveInfinity}));
    }
}
