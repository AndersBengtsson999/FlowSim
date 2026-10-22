using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.Infrastructure;
using Xunit;
namespace Simulation.Application.Tests;
public class UnlaunchedKpiTests
{
    private static TeamKpiObservation Observe(SimulationSession session,int window=20) => KpiDashboard.Project(session,new(window,[TeamKpi.UnlaunchedDevelopmentCost]),400).Single();
    [Fact] public void BalanceAndHistoryReuseAllocationAcrossVersionsAndReplay()
    {
        var request=new SimulationRequest {NumberOfWorkItems=40,DeveloperCount=5,TesterCount=2,DevelopmentEffort=1,CodeReviewEffort=1,TestingEffort=1,Release=new(ReleaseMode.Scheduled,3,10),ProductLaunch=new("Version 1",10,2,LaunchPlanUnit.Sprints){Mode=LaunchPlanningMode.Recurring}};
        var live=LiveSimulation.Start(request,WorkArrivalMode.ContinuousArrival,2);
        for(int version=0;version<3;version++)
        {
            while(live.Session.PendingLaunchDecision is null)Assert.True(live.Step());
            var before=Observe(live.Session);var old=before.BalanceHistory!.Current;
            Assert.True(old.WorkInProgress>0);Assert.True(old.AwaitingLaunch>0);
            live.Session.LaunchProductVersion(live.Session.ProductDelivery.Versions[^1].Id);
            var after=Observe(live.Session);var next=after.BalanceHistory!.Current;
            Assert.Equal(old.WorkInProgress,next.Unlaunched);Assert.Equal(old.Incurred,next.Incurred);
            Assert.Equal(old.CustomerLaunched+old.AwaitingLaunch,next.CustomerLaunched);
            Assert.Equal(before.BalanceHistory.Points.SkipLast(1),after.BalanceHistory.Points.SkipLast(1));
            Assert.Equal(after.Value,Observe(live.Session,40).Value);Assert.Null(after.Previous);Assert.Equal(0,after.Window);
            Assert.Equal((double)next.Unlaunched,after.Value);
            foreach(var p in after.BalanceHistory.Points)
            {
                Assert.Equal((double)p.Unlaunched,after.Trend.Points.Single(x=>x.Day==p.Day).Value);
                Assert.Equal(p.Incurred,p.Unlaunched+p.CustomerLaunched+p.Unallocated);
            }
        }
        var loaded=LiveSessionJson.Load(LiveSessionJson.Save(live));
        Assert.Equal(JsonSerializer.Serialize(Observe(live.Session)),JsonSerializer.Serialize(Observe(loaded.Session)));
        var restored=SimulationSession.Restore(live.Session.Capture());
        Assert.Equal(JsonSerializer.Serialize(Observe(live.Session)),JsonSerializer.Serialize(Observe(restored)));
        live.Step();loaded.Step();
        Assert.Equal(JsonSerializer.Serialize(Observe(live.Session)),JsonSerializer.Serialize(Observe(loaded.Session)));
    }
    [Fact] public void MissingLegacyAllocationsStayExplicitAndAreNotInvented()
    {
        var live=LiveSimulation.Start(new(){NumberOfWorkItems=20},WorkArrivalMode.FixedBacklog);for(int i=0;i<10;i++)live.Step();
        var saved=live.Session.Capture();var old=SimulationSession.Restore(saved with{Days=saved.Days.Select(d=>d with{Economics=null}).ToArray()});
        var observation=Observe(old);Assert.Equal(10,observation.BalanceHistory!.MissingCostDays);Assert.Equal(0,observation.Value);
        Assert.All(observation.BalanceHistory.Points,p=>Assert.Equal(0,p.Unlaunched));
    }
    [Fact] public void RawWorkPerSekIsUnchangedAndUsesInternalReleaseBoundary()
    {
        var live=LiveSimulation.Start(new(){NumberOfWorkItems=40,DevelopmentEffort=1,CodeReviewEffort=1,TestingEffort=1},WorkArrivalMode.ContinuousArrival,2);for(int i=0;i<40;i++)live.Step();
        var expected=live.Session.WorkItems.Where(w=>w.DoneDay>=21 && w.DoneDay<=40).Sum(w=>w.DevelopmentEffort)/(double)TeamEconomics.From(live.Session.Days.Skip(20).ToArray(),0).TotalTeamCost!;
        var before=KpiDashboard.PeriodValue(TeamKpi.WorkVolumePerSek,live.Session,21,40);Assert.Equal(expected,before);
        live.Session.LaunchProductVersion("V1");
        Assert.Equal(before,KpiDashboard.PeriodValue(TeamKpi.WorkVolumePerSek,live.Session,21,40));
    }
}
