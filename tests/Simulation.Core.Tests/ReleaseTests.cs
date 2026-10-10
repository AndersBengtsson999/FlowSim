using System.Text.Json;
using Simulation.Core;
using Xunit;
namespace Simulation.Core.Tests;
public sealed class ReleaseTests
{
    static SimulationSession Batch(ReleaseSettings release,int count=12) => new(new SimulationScenario("Release",100,new(20,20),100,100,100,
        Enumerable.Range(0,count).Select(i=>new WorkItem($"W{count-i}","Work",0,0,0)).ToArray()){Release=release});
    static void Until(SimulationSession s,int day){while(s.CurrentDay<day)s.AdvanceOneDay();}
    [Theory]
    [InlineData(ReleaseMode.FlowBased,-1,5)] [InlineData(ReleaseMode.Scheduled,1,0)] [InlineData((ReleaseMode)99,1,5)]
    public void InvalidSettingsAreRejected(ReleaseMode mode,int capacity,int interval) => Assert.Throws<ScenarioValidationException>(()=>Batch(new(mode,capacity,interval)));
    [Fact]
    public void DefaultReleasesAtTestingCompletionBoundaryWithoutExtraTick()
    {
        var s=Batch(new());Until(s,3);Assert.Equal(12,s.Days[^1].DoneCount);Assert.Equal(0,s.Days[^1].ReadyForReleaseCount);
        Assert.All(s.WorkItems,w=>{Assert.Equal(WorkItemStatus.Released,w.State);Assert.Equal(3,w.ReadyForReleaseDay);Assert.Equal(3,w.ReleasedDay);
            Assert.Equal(new[]{WorkItemStatus.ReadyForRelease,WorkItemStatus.Released},w.Transitions.TakeLast(2).Select(t=>t.To));});
        Until(s,10);Assert.All(s.WorkItems,w=>Assert.Equal(3,w.ReleasedDay));
    }
    [Fact]
    public void FlowCapacityIsDiscreteFifoAndReadyQueueHasNoWipLimit()
    {
        var s=Batch(new(Capacity:2));Until(s,3);Assert.Equal(2,s.Days[^1].DoneCount);Assert.Equal(10,s.Days[^1].ReadyForReleaseCount);
        Assert.All(s.WorkItems,w=>Assert.Equal(3,w.ReadyForReleaseDay));
        Until(s,8);Assert.Equal(12,s.Days[^1].DoneCount);
        for(var i=0;i<12;i++)Assert.Equal(3+i/2,s.WorkItems[i].ReleasedDay);
        Assert.Equal(0,s.Days.Sum(d=>d.UsedDeveloperCapacity+d.UsedTesterCapacity));
    }
    [Fact]
    public void ScheduledUsesAbsoluteDaysAndCarriesExcessInFifoOrder()
    {
        var s=Batch(new(ReleaseMode.Scheduled,10,5));Until(s,4);Assert.Equal(12,s.Days[^1].ReadyForReleaseCount);Assert.Equal(0,s.Days[^1].DoneCount);
        s.AdvanceOneDay();Assert.Equal(10,s.Days[^1].DoneCount);Assert.Equal(2,s.Days[^1].ReadyForReleaseCount);
        Until(s,10);Assert.All(s.WorkItems.Take(10),w=>Assert.Equal(5,w.ReleasedDay));Assert.All(s.WorkItems.Skip(10),w=>Assert.Equal(10,w.ReleasedDay));
    }
    [Fact]
    public void IndividualCycleDurationsReconcileExactly()
    {
        var s=Batch(new(ReleaseMode.Scheduled,10,5),1);Until(s,5);var w=s.GetResult().WorkItems.Single();
        Assert.Equal(3,w.DevelopmentCycleTime);Assert.Equal(2,w.ReleaseWaitTime);Assert.Equal(5,w.CycleTime);
        Assert.Equal(w.CycleTime,w.DevelopmentCycleTime+w.ReleaseWaitTime);Assert.Equal(2,w.WaitingTime);
    }
    [Fact]
    public void InterventionsUseNextDayAndAbsoluteCalendarWithoutHistoryRewrite()
    {
        var s=Batch(new(ReleaseMode.FlowBased,2));Until(s,3);var before=JsonSerializer.Serialize(s.Days);
        s.ApplyChanges(s.Configuration with{Release=new(ReleaseMode.FlowBased,5)});Assert.Equal(before,JsonSerializer.Serialize(s.Days));
        s.AdvanceOneDay();Assert.Equal(7,s.Days[^1].DoneCount);
        s.ApplyChanges(s.Configuration with{Release=new(ReleaseMode.Scheduled,10,5)});s.AdvanceOneDay();Assert.Equal(12,s.Days[^1].DoneCount);
        var other=Batch(new(ReleaseMode.Scheduled,10,5));Until(other,4);other.ApplyChanges(other.Configuration with{Release=new(ReleaseMode.Scheduled,10,10)});
        Until(other,9);Assert.Equal(0,other.Days[^1].DoneCount);other.AdvanceOneDay();Assert.Equal(10,other.Days[^1].DoneCount);
        other.ApplyChanges(other.Configuration with{Release=new(ReleaseMode.FlowBased,1)});other.AdvanceOneDay();Assert.Equal(11,other.Days[^1].DoneCount);
    }
    [Fact]
    public void DependenciesStillUnlockAtWorkCompletionRatherThanRelease()
    {
        var s=new SimulationSession(new SimulationScenario("Dependencies",10,new(2,2),2,2,2,[new("A","A",0,0,0),new("B","B",0,0,0,["A"])]){Release=new(Capacity:0)});
        Until(s,6);Assert.All(s.WorkItems,w=>Assert.Equal(WorkItemStatus.ReadyForRelease,w.State));Assert.Equal(3,s.WorkItems[1].DevelopmentStartedDay);
    }
    [Fact]
    public void CheckpointStateWithWaitingItemsRepeatsFutureReleaseExactly()
    {
        var s=Batch(new(ReleaseMode.Scheduled,3,5));Until(s,4);var checkpoint=s.Capture();Until(s,25);
        var repeat=SimulationSession.Restore(checkpoint);Until(repeat,25);Assert.Equal(JsonSerializer.Serialize(s.Capture()),JsonSerializer.Serialize(repeat.Capture()));
    }
}
