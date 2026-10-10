using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.Infrastructure;
using Xunit;
namespace Simulation.Application.Tests;
public sealed class ReleaseIntegrationTests
{
    static SimulationRequest Request => new(){DeveloperCount=5,TesterCount=2,DevelopmentWipLimit=5,NumberOfWorkItems=0,DevelopmentEffort=5,CodeReviewEffort=1,TestingEffort=2};
    static LiveSimulation Start(ReleaseSettings settings) => LiveSimulation.Start(Request with{Release=settings},WorkArrivalMode.AlwaysAvailable);
    static void Until(LiveSimulation live,int day){while(live.Session.CurrentDay<day)Assert.True(live.Step());}
    static void Near(double expected,double? actual){Assert.NotNull(actual);Assert.InRange(Math.Abs(expected-actual.Value),0,1e-8);}
    [Fact]
    public void WindowPopulationsDistinguishReadyFromReleasedIncludingEarlierStarts()
    {
        var live=LiveSimulation.Start(Request with{NumberOfWorkItems=1,Release=new(ReleaseMode.Scheduled,10,10)},WorkArrivalMode.FixedBacklog);Until(live,20);
        var w=live.Session.WorkItems.Single();var ready=w.ReadyForReleaseDay!.Value;var released=w.ReleasedDay!.Value;Assert.True(ready<released);
        var a=LivePerformance.Period(live.Session,ready,ready);Assert.Equal(1,a.WorkCompleted);Assert.Equal(0,a.Completed);Assert.NotNull(a.DeliveryCostPerDoneItem);Assert.Null(a.SystemCostPerDoneItem);Assert.Null(a.ReleaseWaitTime);
        var b=LivePerformance.Period(live.Session,released,released);Assert.Equal(0,b.WorkCompleted);Assert.Equal(1,b.Completed);Assert.Null(b.DeliveryCostPerDoneItem);Assert.Null(b.DevelopmentCycleTime);
        Near(released-ready,b.ReleaseWaitTime);Near(released-w.DevelopmentStartedDay!.Value,b.CycleTime);Near(0,b.SystemCostPerDoneItem);
    }
    [Fact]
    public void ReleaseChangesOnlyDeliveryStateNotRawWorkRandomnessSkillsDebtOrItemCosts()
    {
        var request=Request with{Skills=new(2,.4),Productivity=new(1.5,1.2,1.3),Debt=new(){ShortcutRate=.3,Repayment=.25},Quality=new(){Enabled=true,TestingDefectProbability=.2}};
        var fast=LiveSimulation.Start(request,WorkArrivalMode.AlwaysAvailable);var stopped=LiveSimulation.Start(request with{Release=new(Capacity:0)},WorkArrivalMode.AlwaysAvailable);
        Until(fast,160);Until(stopped,160);var a=fast.Session.Capture();var b=stopped.Session.Capture();
        Assert.Equal(a.ArrivalRandomState,b.ArrivalRandomState);Assert.Equal(a.SkillRandomState,b.SkillRandomState);Assert.Equal(a.DiscoveryRandomState,b.DiscoveryRandomState);Assert.Equal(a.ReworkRandomState,b.ReworkRandomState);Assert.Equal(a.DebtState,b.DebtState);
        Assert.Equal(fast.Session.WorkItems.Select(w=>(w.Id,w.RequiresSpecialist,w.DeliveryCost,w.ReadyForReleaseDay)),stopped.Session.WorkItems.Select(w=>(w.Id,w.RequiresSpecialist,w.DeliveryCost,w.ReadyForReleaseDay)));
        for(var i=0;i<160;i++){var x=a.Days[i];var y=b.Days[i];Assert.Equal(x.UsedDeveloperCapacity,y.UsedDeveloperCapacity);Assert.Equal(x.UsedTesterCapacity,y.UsedTesterCapacity);Assert.Equal(x.SpecialistWorkWaiting,y.SpecialistWorkWaiting);}
        var p=LivePerformance.Rolling(stopped.Session,50);Assert.True(p.CompletionRate>0);Assert.Equal(0,p.Throughput);Assert.Null(p.SystemCostPerDoneItem);Assert.True(p.ConsumedSystemCapacity!.Total>0);
        Near(LivePerformance.Rolling(fast.Session,50).DeliveryCostPerDoneItem!.Value,p.DeliveryCostPerDoneItem);
    }
    [Fact]
    public void EveryReleaseTrendPointMatchesItsOwnPeriodPopulationAndBeforeAfter()
    {
        var live=Start(new(ReleaseMode.Scheduled,5,5));Until(live,100);live.Session.ApplyChanges(live.Session.Configuration with{Release=new(ReleaseMode.FlowBased,1)},"Release change");Until(live,120);
        foreach(var window in new[]{10,20,50,100})foreach(var metric in new[]{LiveTrendMetric.Throughput,LiveTrendMetric.CompletionRate,LiveTrendMetric.CycleTime,LiveTrendMetric.DevelopmentCycleTime,LiveTrendMetric.ReleaseWaitTime,LiveTrendMetric.DeliveryCost,LiveTrendMetric.SystemCost})
        foreach(var point in LivePerformanceTrend.Project(live.Session,metric,window,null).Points){var p=LivePerformance.Period(live.Session,Math.Max(1,point.Day-window+1),point.Day);double? expected=metric switch{LiveTrendMetric.Throughput=>p.Throughput,LiveTrendMetric.CompletionRate=>p.CompletionRate,LiveTrendMetric.CycleTime=>p.CycleTime,LiveTrendMetric.DevelopmentCycleTime=>p.DevelopmentCycleTime,LiveTrendMetric.ReleaseWaitTime=>p.ReleaseWaitTime,LiveTrendMetric.DeliveryCost=>p.DeliveryCostPerDoneItem,_=>p.SystemCostPerDoneItem};if(expected is null)Assert.Null(point.Value);else Near(expected.Value,point.Value);}
        var c=LivePerformance.Compare(live.Session,live.Session.Changes.Single(),20);Assert.Equal(81,c.Before.FirstDay);Assert.Equal(100,c.Before.LastDay);Assert.Equal(101,c.After.FirstDay);Assert.Equal(120,c.After.LastDay);
    }
    [Fact]
    public void EquivalentNominalCapacityStillProducesBatchWaiting()
    {
        var flow=Start(new(Capacity:2));var scheduled=Start(new(ReleaseMode.Scheduled,10,5));Until(flow,200);Until(scheduled,200);
        var a=LivePerformance.Rolling(flow.Session,50);var b=LivePerformance.Rolling(scheduled.Session,50);
        Assert.Equal(a.Completed,b.Completed);Assert.True(b.ReleaseWaitTime>a.ReleaseWaitTime);Assert.True(b.CycleTime>a.CycleTime);
        Assert.Contains(scheduled.Session.Days,d=>d.ReadyForReleaseCount>0);Assert.Equal(0,scheduled.CurrentSnapshot.ReadyForReleaseCount);
    }
    [Fact]
    public void TwoArrivalsPerDayExerciseEquivalentNominalCapacityWithDifferentBatchWaiting()
    {
        var request=Request with{DevelopmentEffort=0,CodeReviewEffort=0,TestingEffort=0};
        var flow=LiveSimulation.Start(request with{Release=new(Capacity:2)},WorkArrivalMode.ContinuousArrival,2);
        var batch=LiveSimulation.Start(request with{Release=new(ReleaseMode.Scheduled,10,5)},WorkArrivalMode.ContinuousArrival,2);
        Until(flow,200);Until(batch,200);var a=LivePerformance.Rolling(flow.Session,50);var b=LivePerformance.Rolling(batch.Session,50);
        Assert.Equal(100,a.Completed);Assert.Equal(100,b.Completed);Near(10,a.Throughput);Near(10,b.Throughput);
        Near(0,a.ReleaseWaitTime);Near(2,b.ReleaseWaitTime);Assert.True(b.ReadyForRelease.Average>a.ReadyForRelease.Average);
    }
    [Fact]
    public void SaveLoadAndLiveCheckpointPreserveQueueInterventionAndMetrics()
    {
        var live=Start(new(ReleaseMode.Scheduled,2,5));Until(live,49);Assert.True(live.CurrentSnapshot.ReadyForReleaseCount>0);
        live.Session.ApplyChanges(live.Session.Configuration with{Release=new(ReleaseMode.Scheduled,5,10)},"Calendar");var cp=live.CreateCheckpoint("Waiting");var loaded=LiveSessionJson.Load(LiveSessionJson.Save(live));
        Until(live,100);Until(loaded,100);var expected=JsonSerializer.Serialize(live.Session.Capture());Assert.Equal(expected,JsonSerializer.Serialize(loaded.Session.Capture()));
        live.RestoreCheckpoint(cp.Id);Until(live,100);Assert.Equal(expected,JsonSerializer.Serialize(live.Session.Capture()));
    }
    [Fact]
    public void SupportedLegacyDoneHistoryIsRetainedWithoutRewriting()
    {
        var old=LiveSessionJson.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures","model04-AlwaysAvailable-live.json")));
        var before=JsonSerializer.Serialize(old.Session.Days);var done=old.Session.WorkItems.Where(w=>w.State==WorkItemStatus.Done).ToArray();Assert.NotEmpty(done);Assert.Equal(new ReleaseSettings(),old.Session.Configuration.Release);
        var length=old.Session.Days.Count;Until(old,length+50);Assert.Equal(before,JsonSerializer.Serialize(old.Session.Days.Take(length)));
        Assert.All(done,w=>{Assert.Equal(WorkItemStatus.Done,w.State);Assert.Null(w.ReleasedDay);Assert.Equal(w.DoneDay,w.WorkCompletedDay);});
        Assert.Contains(old.Session.WorkItems,w=>w.State==WorkItemStatus.Released);
        Assert.Equal(old.CurrentSnapshot.DoneCount,old.Inspect(WorkItemStatus.Released).Count);
    }
}
