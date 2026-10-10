using System.Text.Json;
using Simulation.Core;
using Xunit;
namespace Simulation.Core.Tests;

public sealed class SkillsTests
{
    static SimulationSession Create(int specialists=1, double rate=1, int count=5, int wip=5, double availability=1, double productivity=1, double effort=10) =>
        new(new SimulationScenario("Skills",200,new Team(5,2){DeveloperAvailability=availability},wip,3,3,
            Enumerable.Range(0,count).Select(i=>new WorkItem($"W{i}","Work",effort,1,2)).ToArray(),12345) {
            Skills=new(specialists,rate),Productivity=new(productivity,1,1) });
    static void Near(double expected,double actual) => Assert.InRange(Math.Abs(expected-actual),0,1e-10);

    [Theory]
    [InlineData(-1,0)] [InlineData(6,0)] [InlineData(1,-.1)] [InlineData(1,1.1)] [InlineData(1,double.NaN)]
    public void InvalidSkillsAreRejected(int specialists,double rate) => Assert.Throws<ScenarioValidationException>(()=>Create(specialists,rate));

    [Fact]
    public void AllSpecialistWithoutSpecialistsStallsWithoutConsumingCapacity()
    {
        var s=Create(0); for(var i=0;i<10;i++) {
            var d=s.AdvanceOneDay();Assert.Equal(0,d.UsedDeveloperCapacity);Assert.Equal(5,d.SpecialistWorkWaiting);
            Assert.Equal(5,d.DevelopmentWip);Assert.Equal(0,d.DoneCount);
        }
        Assert.All(s.WorkItems,w=>{Assert.True(w.RequiresSpecialist);Assert.Equal(0,w.DeliveryCost.Total);});
    }

    [Theory]
    [InlineData(1,1,1,1,1,0)] [InlineData(2,1,1,2,1.5,1)]
    [InlineData(2,.75,1,1.5,1.25,.5)] [InlineData(2,1,2,2,3,1)]
    public void SpecialistCollaborationUsesOnlyEligibleRawCapacity(int specialists,double availability,double productivity,double capacity,double work,double collaboration)
    {
        var s=Create(specialists,count:1,wip:1,availability:availability,productivity:productivity);var d=s.AdvanceOneDay();
        Near(capacity,d.UsedDevelopmentCapacity);Near(work,d.DevelopmentWork);Near(collaboration,d.CollaborationDevelopmentCapacity);
        Near(capacity,s.WorkItems.Single().DeliveryCost.Development);Near(5*availability,d.AvailableDeveloperCapacity);
        Assert.InRange(s.WorkItems.Single().Events.Count(e=>e.EventType==WorkItemEventType.CapacityApplied),1,2);
    }

    [Fact]
    public void GeneralOnlyWorkUsesSpecialistCapacityWithoutReservationOrPremium()
    {
        var a=Create(0,0,count:1,wip:1);var b=Create(5,0,count:1,wip:1);
        for(var i=0;i<20;i++) Assert.Equal(JsonSerializer.Serialize(a.AdvanceOneDay()),JsonSerializer.Serialize(b.AdvanceOneDay()));
        Assert.Equal(a.WorkItems.Single().DeliveryCost,b.WorkItems.Single().DeliveryCost);
    }

    [Fact]
    public void MixedWorkPrefersSpecialistsAndFallsBackAfterEligibleWorkCannotUseMore()
    {
        var original=Create(2,0,count:2,wip:2);var state=original.Capture();
        var s=SimulationSession.Restore(state with {WorkItems=state.WorkItems.Select((w,i)=>w with{RequiresSpecialist=i==1}).ToArray()});
        var d=s.AdvanceOneDay();
        var general=d.Items[0];var specialist=d.Items[1];
        Near(2,specialist.UsedDevelopmentCapacity);Near(1.5,specialist.DevelopmentWork);
        Near(2,general.UsedDevelopmentCapacity);Near(1.5,general.DevelopmentWork);Near(4,d.UsedDeveloperCapacity);
        Assert.All(s.WorkItems,w=>Assert.Equal(2,w.Events.Count(e=>e.EventType==WorkItemEventType.CapacityApplied)));
        // One specialist can supply a primary only; its spare capacity is not reserved.
        var finish=Create(2,count:2,wip:2,effort:.25);var fs=finish.Capture();
        finish=SimulationSession.Restore(fs with{WorkItems=fs.WorkItems.Select((w,i)=>w with{RequiresSpecialist=i==1}).ToArray()});
        var fd=finish.AdvanceOneDay();Near(.5,fd.UsedDevelopmentCapacity);Assert.Equal(0,fd.SpecialistWorkWaiting);
    }

    [Fact]
    public void SharedWipHasNoReservedSlotsAndReductionsDoNotEvictItems()
    {
        var initial=Create(1,0,count:3,wip:2,effort:2).Capture();
        var s=SimulationSession.Restore(initial with{WorkItems=initial.WorkItems.Select((w,i)=>w with{RequiresSpecialist=i==2}).ToArray()});
        var first=s.AdvanceOneDay();Assert.Equal(WorkItemStatus.Backlog,s.WorkItems[2].State);Assert.Equal(0,first.SpecialistWorkWaiting);
        s.ApplyChanges(s.Configuration with{DevelopmentWipLimit=1});
        Assert.Equal(2,s.WorkItems.Count(w=>w.State==WorkItemStatus.Development));
        for(var i=0;i<10;i++)s.AdvanceOneDay();
        Assert.Equal(WorkItemStatus.Released,s.WorkItems[2].State);
        Assert.All(s.Days.Skip(2),d=>Assert.InRange(d.DevelopmentWip,0,1));
    }

    [Fact]
    public void ReviewReworkAndRepaymentKeepPriorityAndUseProportionalRemainingComposition()
    {
        var s=Create(2,count:5);var state=s.Capture();
        s=SimulationSession.Restore(state with {DebtState=new(100,100),Configuration=state.Configuration with{Debt=new(){Repayment=.25}}});
        var d=s.AdvanceOneDay();Near(1.25,d.UsedDebtRepaymentCapacity);Near(1.5,d.UsedDevelopmentCapacity);
        Near(2.75,d.UsedDeveloperCapacity);Assert.True(d.SpecialistWorkWaiting>0);
        Assert.All(s.WorkItems,w=>{Assert.True(w.DevelopmentPlan!.Overhead>0);Near(w.Events.Sum(e=>e.CapacityConsumed),w.DeliveryCost.Total);});
        s.ApplyChanges(s.Configuration with{Skills=new(0,1)});var after=s.AdvanceOneDay();
        Assert.Equal(0,after.UsedDevelopmentCapacity);Assert.True(after.UsedDebtRepaymentCapacity>0);
    }

    [Fact]
    public void SpecialistCountInterventionAppliesNextDayWithoutChangingItemsOrHistory()
    {
        var s=Create(1);s.AdvanceOneDay();var before=s.Capture();
        s.ApplyChanges(s.Configuration with{Skills=new(2,1)});Assert.Equal(1,s.Changes.Single().Day);
        Assert.Equal(JsonSerializer.Serialize(before.WorkItems),JsonSerializer.Serialize(s.Capture().WorkItems));
        Assert.Equal(JsonSerializer.Serialize(before.Days),JsonSerializer.Serialize(s.Days));
        Near(1,s.Days[0].UsedDevelopmentCapacity);Near(2,s.AdvanceOneDay().UsedDevelopmentCapacity);
        Assert.Throws<ScenarioValidationException>(()=>s.ApplyChanges(s.Configuration with{Team=new(1,2)}));
        s.ApplyChanges(s.Configuration with{Team=new(3,2),Skills=new(1,1)});Assert.Equal(3,s.Configuration.Team.DeveloperCount);
    }

    [Fact]
    public void SpecialistCapacityFallsBackToGeneralAfterSpecialistFinishes()
    {
        var s=new SimulationSession(new SimulationScenario("Fallback",10,new(2,1),2,1,1,
            [new("G","General",10,1,1),new("S","Specialist",.25,1,1)]){Skills=new(2,0)});
        var state=s.Capture();s=SimulationSession.Restore(state with{WorkItems=state.WorkItems.Select(w=>w with{RequiresSpecialist=w.Id=="S"}).ToArray()});
        var d=s.AdvanceOneDay();Near(.25,d.Items[1].UsedDevelopmentCapacity);Near(1.75,d.Items[0].UsedDevelopmentCapacity);
        Near(2,d.UsedDeveloperCapacity);Near(1.375,d.Items[0].DevelopmentWork);
    }

    [Fact]
    public void SpecialistReviewReworkAndTestingContinueAfterSpecialistsBecomeZero()
    {
        var baseState=Create(1,count:1,wip:1,effort:1).Capture();
        var s=SimulationSession.Restore(baseState with{Configuration=baseState.Configuration with{Quality=new(){Enabled=true,CodeReviewDefectProbability=1}}});
        s.AdvanceOneDay();Assert.Equal(WorkItemStatus.WaitingForCodeReview,s.WorkItems[0].State);
        s.ApplyChanges(s.Configuration with{Skills=new(0,1)});
        s.AdvanceOneDay();Assert.Equal(WorkItemStatus.WaitingForRework,s.WorkItems[0].State);
        s.ApplyChanges(s.Configuration with{Quality=new()});
        for(var i=0;i<150;i++)s.AdvanceOneDay();
        Assert.True(s.WorkItems[0].RequiresSpecialist);Assert.Equal(WorkItemStatus.Released,s.WorkItems[0].State);
        Assert.Contains(s.Days,d=>d.UsedReworkDeveloperCapacity>0);
        Assert.All(s.Days,d=>Assert.Equal(0,d.SpecialistWorkWaiting));
    }

    [Fact]
    public void EndpointRatesDoNotConsumeAnyRandomNumbersAndIntermediateRateUsesIndependentStream()
    {
        var zero=Create(0,0);var full=Create(0,1);var mixed=Create(0,.4);
        Assert.Equal(zero.Capture().SkillRandomState,full.Capture().SkillRandomState);
        Assert.NotEqual(zero.Capture().SkillRandomState,mixed.Capture().SkillRandomState);
        Assert.Equal(zero.Capture().ArrivalRandomState,mixed.Capture().ArrivalRandomState);
        Assert.Equal(zero.Capture().DiscoveryRandomState,mixed.Capture().DiscoveryRandomState);
        Assert.Equal(zero.Capture().ReworkRandomState,mixed.Capture().ReworkRandomState);
    }
}
