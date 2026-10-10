using System.Text.Json;
using Simulation.Core;
using Xunit;
namespace Simulation.Core.Tests;
public sealed class DependencyTests
{
    static SimulationSession Start(DependencySettings settings, int count = 20, int wip = 5, int seed = 12345) => new(new SimulationScenario("Dependencies", 200, new(5,2),wip,3,3,
        Enumerable.Range(0,count).Select(i=>new WorkItem($"W{i}","Work",5,1,2)).ToArray(),seed) {ResidualDependencies=settings});
    static void Until(SimulationSession s,int day){while(s.CurrentDay<day)s.AdvanceOneDay();}
    [Theory]
    [InlineData(-.1, 0)] [InlineData(1.1, 0)] [InlineData(0,-1)] [InlineData(double.NaN,0)] [InlineData(0,double.PositiveInfinity)]
    public void InvalidSettingsRejected(double rate,double mean) => Assert.Throws<ScenarioValidationException>(()=>Start(new(rate,mean)));
    [Fact]
    public void LiveValidationRejectsInvalidSettingsAtomically()
    {
        var s=Start(new());var before=JsonSerializer.Serialize(s.Capture());
        Assert.Throws<ScenarioValidationException>(()=>s.ApplyChanges(s.Configuration with{ResidualDependencies=new(2,3)}));
        Assert.Equal(before,JsonSerializer.Serialize(s.Capture()));
    }
    [Fact]
    public void GeometricSamplesHaveConfiguredMeanAndCertainAssignmentSkipsProbabilityDraws()
    {
        var s=Start(new(1,3),10000);s.AdvanceOneDay();
        Assert.InRange(s.WorkItems.Average(w=>w.ResidualDependency!.WaitingDays),2.8,3.2);
        var random=new SeededRandom(unchecked(12345 ^ (int)0xD3EED123));
        for(var i=0;i<10000;i++)random.NextUnitDouble();
        Assert.Equal(random.State,s.Capture().DependencyRandomState);
    }
    [Fact]
    public void ZeroRateConsumesNoDependencyRandomnessAndIsEquivalentWithAnyMean()
    {
        var a=Start(new());var b=Start(new(0,999));var state=a.Capture().DependencyRandomState;Until(a,60);Until(b,60);
        Assert.Equal(state,a.Capture().DependencyRandomState);
        Assert.Equal(JsonSerializer.Serialize(a.Days),JsonSerializer.Serialize(b.Days));
        Assert.All(a.WorkItems,w=>Assert.Null(w.ResidualDependency));
    }
    [Fact]
    public void AllAssignedOnceAtArrivalAndBlockedItemsUseNoCapacityOrWip()
    {
        var s=Start(new(1,50));var d=s.AdvanceOneDay();
        Assert.All(s.WorkItems,w=>Assert.NotNull(w.ResidualDependency));
        var blocked=s.WorkItems.Where(w=>w.DependencyUnresolved(0)).ToArray();Assert.NotEmpty(blocked);
        foreach(var w in blocked){Assert.Null(w.DevelopmentStartedDay);Assert.Equal(0,w.DeliveryCost.Total);Assert.DoesNotContain(w.Transitions,t=>t.To==WorkItemStatus.Development);}
        Assert.Equal(s.WorkItems.Count(w=>w.DevelopmentStartedDay==0),d.DevelopmentWip);
        var assigned=s.WorkItems.Select(w=>w.ResidualDependency).ToArray();Until(s,5);Assert.Equal(assigned,s.WorkItems.Select(w=>w.ResidualDependency));
        Assert.All(s.WorkItems,w=>Assert.Equal(w.CreatedDay+(long)w.ResidualDependency!.WaitingDays,w.ResidualDependency.ResolutionDay));
    }
    [Fact]
    public void ResolutionBoundaryAllowsStartAndLaterEligibleItemsBypassBlockedItems()
    {
        var s=Start(new(1,5),50,50);s.AdvanceOneDay();
        var blocked=s.WorkItems.First(w=>w.ResidualDependency!.WaitingDays>1);
        Assert.Contains(s.WorkItems.SkipWhile(w=>w!=blocked).Skip(1),w=>w.DevelopmentStartedDay==0);
        var target=(int)blocked.ResidualDependency!.ResolutionDay;Until(s,target);Assert.Null(blocked.DevelopmentStartedDay);
        s.AdvanceOneDay();Assert.Equal(target,blocked.DevelopmentStartedDay);
    }
    [Fact]
    public void DependenciesResolveDuringOtherBacklogWaitingWithoutRestartingClock()
    {
        var s=Start(new(1,3),30,1);Until(s,150);
        var absorbed=s.WorkItems.Where(w=>w.ResidualDependency!.WaitingDays>0 && w.DevelopmentStartedDay>w.ResidualDependency.ResolutionDay).ToArray();Assert.NotEmpty(absorbed);
        Assert.All(absorbed,w=>Assert.Equal(w.CreatedDay+(long)w.ResidualDependency!.WaitingDays,w.ResidualDependency.ResolutionDay));
    }
    [Fact]
    public void ZeroMeanAssignsImmediateDependenciesWithoutAnyRandomDraw()
    {
        var a=Start(new());var b=Start(new(1,0));var state=b.Capture().DependencyRandomState;Until(a,30);Until(b,30);
        Assert.Equal(state,b.Capture().DependencyRandomState);
        Assert.Equal(a.WorkItems.Select(w=>w.DevelopmentStartedDay),b.WorkItems.Select(w=>w.DevelopmentStartedDay));
        Assert.Equal(a.WorkItems.Select(w=>w.DeliveryCost),b.WorkItems.Select(w=>w.DeliveryCost));
        Assert.All(b.WorkItems,w=>Assert.Equal(0,w.ResidualDependency!.WaitingDays));
    }
    [Fact]
    public void FutureFixedArrivalsUseIntervenedSettingsAndExistingAssignmentsRemain()
    {
        var s=new SimulationSession(new("Future",100,new(),5,3,3,[new("A","A",5,1,2),new("B","B",5,1,2,createdDay:10)]) {ResidualDependencies=new(1,5)});
        s.AdvanceOneDay();var old=s.WorkItems[0].ResidualDependency;
        s.ApplyChanges(s.Configuration with{ResidualDependencies=new(1,100)},"Longer dependencies");Until(s,11);
        Assert.Equal(old,s.WorkItems[0].ResidualDependency);Assert.NotNull(s.WorkItems[1].ResidualDependency);Assert.Equal(1,s.Changes.Single().Day);
    }
    [Fact]
    public void CheckpointPreservesAssignmentsAndIndependentRandomStreamExactly()
    {
        var s=Start(new(.5,5));Until(s,15);var restored=SimulationSession.Restore(s.Capture());Until(s,70);Until(restored,70);
        Assert.Equal(JsonSerializer.Serialize(s.Capture()),JsonSerializer.Serialize(restored.Capture()));
    }
}
