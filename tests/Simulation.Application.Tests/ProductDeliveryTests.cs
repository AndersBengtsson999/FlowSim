using System.Text.Json;
using System.Text.Json.Nodes;
using Simulation.Application;
using Simulation.Core;
using Simulation.Infrastructure;
using Xunit;
namespace Simulation.Application.Tests;
public class ProductDeliveryTests
{
    private static SimulationRequest Request(int? after = 6, LaunchPlanUnit unit = LaunchPlanUnit.Sprints) => new()
    { NumberOfWorkItems=50, SimulationDays=100, DeveloperCount=5,TesterCount=2, DevelopmentEffort=1,CodeReviewEffort=1,TestingEffort=1,
      ProductLaunch=new("Version 2.0",10,after,unit), Release=new(ReleaseMode.Scheduled,3,5) };
    private static LiveSimulation Live(int? after=6) => LiveSimulation.Start(Request(after),WorkArrivalMode.ContinuousArrival,2);
    private static void Advance(LiveSimulation live,int day) { while(live.Session.CurrentDay<day) Assert.True(live.Step()); }
    [Fact] public void InitialVersionAndSprintPlanResolveToDay60()
    {
        var live=Live();var state=live.Session.ProductDelivery;var version=Assert.Single(state.Versions);
        Assert.Equal("V1",version.Id);Assert.Equal("Version 2.0",version.Name);Assert.Equal(0,version.StartDay);Assert.Equal(60,version.PlannedDay);Assert.Equal(60,version.OriginalPlannedDay);
        Assert.Empty(state.CustomerLaunches);Assert.Null(state.Pending);
    }
    [Theory] [InlineData(0,6,LaunchPlanUnit.Sprints,10,60)] [InlineData(60,2,LaunchPlanUnit.Sprints,10,80)] [InlineData(60,7,LaunchPlanUnit.WorkingDays,10,67)]
    public void PlanConversionUsesCompletedDayBoundary(int now,int amount,LaunchPlanUnit unit,int sprint,int expected) => Assert.Equal(expected,ProductLaunchSettings.TargetDay(now,amount,unit,sprint));
    [Fact] public void PendingDecisionIsAnAuthoritativeBarrierAndCaptureIsUnchangedByRejectedAdvance()
    {
        var live=Live();Advance(live,60);Assert.NotNull(live.Session.PendingLaunchDecision);Assert.Equal(0,live.Session.GetResult().CustomerDelivery.LaunchedItems);
        var captured=LiveSessionJson.Save(live);
        for(var i=0;i<5;i++)Assert.False(live.Step());
        Assert.Throws<ScenarioValidationException>(()=>live.Session.AdvanceOneDay());
        Assert.Equal(captured,LiveSessionJson.Save(live));Assert.Equal(60,live.Session.Days.Count);
    }
    [Fact] public void SameDayInternalReleasesAreIncludedButUnfinishedWorkIsNot()
    {
        var live=Live();Advance(live,60);var before=live.Session.Capture();var ledger=live.Session.ProductDelivery;
        Assert.True(ledger.InternalReleases.Count>1);Assert.Contains(ledger.InternalReleases,r=>r.Day==60);
        var expected=live.Session.WorkItems.Where(w=>w.State.IsDelivered()).Select(w=>w.Id).ToHashSet();
        var unfinished=live.Session.WorkItems.Where(w=>!w.State.IsDelivered()).Select(w=>w.Id).ToHashSet();Assert.NotEmpty(unfinished);
        var launch=live.Session.LaunchProductVersion("V1");Assert.Equal(60,launch.Day);Assert.True(expected.SetEquals(launch.ItemIds));Assert.DoesNotContain(launch.ItemIds,id=>unfinished.Contains(id));
        Assert.Equal(JsonSerializer.Serialize(before.WorkItems),JsonSerializer.Serialize(live.Session.Capture().WorkItems));
        Assert.Equal(JsonSerializer.Serialize(before.Days),JsonSerializer.Serialize(live.Session.Days));
        Assert.Same(launch,live.Session.LaunchProductVersion("V1"));Assert.Single(live.Session.ProductDelivery.CustomerLaunches);
        Assert.Equal("V2",live.Session.ProductDelivery.Versions[^1].Id);Assert.Equal(60,live.Session.ProductDelivery.Versions[^1].StartDay);
        Assert.Equal("Version 3.0",live.Session.ProductDelivery.Versions[^1].Name);
        Advance(live,65);Assert.Contains(live.Session.ProductDelivery.InternalReleases,r=>r.VersionId=="V2");Assert.True(expected.SetEquals(live.Session.ProductDelivery.CustomerLaunches[0].ItemIds));
    }
    [Fact] public void MultiplePostponementsRecordOriginalPlanAndResumeWithoutDuplicateDays()
    {
        var live=Live();Advance(live,60);live.Session.PlanCustomerLaunch(2,LaunchPlanUnit.Sprints,10);Assert.Null(live.Session.PendingLaunchDecision);Advance(live,80);
        live.Session.PlanCustomerLaunch(3,LaunchPlanUnit.WorkingDays,10);Advance(live,83);var v=live.Session.ProductDelivery.Versions[0];
        Assert.Equal(60,v.OriginalPlannedDay);Assert.Equal(83,v.PlannedDay);Assert.Equal(new[]{60,80,83},v.Plans.Select(p=>p.TargetDay));
        live.Session.LaunchProductVersion(v.Id);Assert.True(live.Step());Assert.Equal(84,live.Session.CurrentDay);Assert.Equal(Enumerable.Range(0,84),live.Session.Days.Select(d=>d.Day));
    }
    [Fact] public void ManualLaunchUsesCurrentDayAndDoesNotRewriteInternalReleaseDates()
    {
        var live=Live(null);Advance(live,30);var releases=JsonSerializer.Serialize(live.Session.ProductDelivery.InternalReleases);
        live.Session.LaunchProductVersion("V1");Assert.Equal(30,live.Session.ProductDelivery.CustomerLaunches[0].Day);
        Assert.Equal(releases,JsonSerializer.Serialize(live.Session.ProductDelivery.InternalReleases));
        Assert.Throws<ScenarioValidationException>(()=>live.Session.LaunchProductVersion("V2"));
        live.Session.RenameActiveVersion("Next Product");Assert.Equal("Next Product",live.Session.ProductDelivery.Versions[^1].Name);
    }
    [Fact] public void AutomaticRunAndMatchingLiveDecisionsAreIdentical()
    {
        var request=Request();var result=new SimulationRunner().Run(request);
        var live=LiveSimulation.Start(request,WorkArrivalMode.FixedBacklog);
        for(var i=0;i<100;i++) {Assert.True(live.Step());if(live.Session.PendingLaunchDecision is { } decision)live.Session.LaunchProductVersion(decision.VersionId);}
        Assert.Equal(JsonSerializer.Serialize(result),JsonSerializer.Serialize(live.Session.GetResult()));
        Assert.Equal(60,Assert.Single(result.ProductDelivery!.CustomerLaunches).Day);
    }
    [Fact] public void ManualOnlyUnattendedExecutionNeverBlocksOrInventsCustomerLaunches()
    {
        var result=new SimulationRunner().Run(Request(null));Assert.Equal(100,result.SimulationDays);Assert.Empty(result.ProductDelivery!.CustomerLaunches);Assert.True(result.CustomerDelivery.InternallyReleasedItemsAwaitingLaunch>0);
    }
    [Fact] public void PlanningNeverChangesDevelopmentRandomStreamsCostsOrInternalMetrics()
    {
        var request=Request();var withLaunch=new SimulationRunner().Run(request);var withoutLaunch=new SimulationRunner().Run(request with{ProductLaunch=new()});
        Assert.Equal(JsonSerializer.Serialize(withLaunch.Days),JsonSerializer.Serialize(withoutLaunch.Days));
        Assert.Equal(withoutLaunch.CompletedWorkItems,withLaunch.CompletedWorkItems);Assert.Equal(withoutLaunch.AverageLeadTime,withLaunch.AverageLeadTime);Assert.Equal(withoutLaunch.AverageCycleTime,withLaunch.AverageCycleTime);
        Assert.Equal(JsonSerializer.Serialize(withLaunch.WorkItems.Select(w=>w with{ProductVersionId=null,CustomerLaunchDay=null})),JsonSerializer.Serialize(withoutLaunch.WorkItems.Select(w=>w with{ProductVersionId=null,CustomerLaunchDay=null})));
        var a=Live();var b=Live(null);Advance(a,60);Advance(b,60);var sa=a.Session.Capture();var sb=b.Session.Capture();Assert.Equal(sa.ArrivalRandomState,sb.ArrivalRandomState);Assert.Equal(sa.DiscoveryRandomState,sb.DiscoveryRandomState);Assert.Equal(sa.DependencyRandomState,sb.DependencyRandomState);Assert.Equal(sa.SkillRandomState,sb.SkillRandomState);
    }
    [Theory] [InlineData(20,false)] [InlineData(60,false)] [InlineData(60,true)]
    public void SaveLoadAndCheckpointPreserveActivePendingAndLaunchedState(int day,bool launch)
    {
        var live=Live();Advance(live,day);if(launch)live.Session.LaunchProductVersion("V1");var checkpoint=live.CreateCheckpoint("delivery");var json=LiveSessionJson.Save(live);var loaded=LiveSessionJson.Load(json);
        Assert.Equal(json,LiveSessionJson.Save(loaded));
        if(loaded.Session.PendingLaunchDecision is not null)Assert.False(loaded.Step());
        else Assert.True(loaded.Step());
        loaded.RestoreCheckpoint(checkpoint.Id);Assert.Equal(json,LiveSessionJson.Save(loaded));
    }
    [Fact] public void LegacySaveReconstructsOnlyKnownInternalReleasesAndNoCustomerHistory()
    {
        var live=Live(null);Advance(live,30);var root=JsonNode.Parse(LiveSessionJson.Save(live))!;root["SimulationModelVersion"]="0.8";
        root["State"]!["Header"]!.AsObject().Remove("ProductDelivery");
        var loaded=LiveSessionJson.Load(root.ToJsonString());Assert.True(loaded.Session.ProductDelivery.ImportedLegacy);Assert.Empty(loaded.Session.ProductDelivery.CustomerLaunches);
        Assert.Equal(live.Session.GetResult().CompletedWorkItems,loaded.Session.GetResult().CustomerDelivery.InternallyReleasedItemsAwaitingLaunch);
        Assert.Null(loaded.Session.PendingLaunchDecision);Assert.Equal(30,loaded.Session.CurrentDay);
    }
    [Fact] public void CustomerMetricsUseOnlyLaunchedCohortAndOriginalTimestamps()
    {
        var live=Live();Advance(live,60);live.Session.LaunchProductVersion("V1");Advance(live,65);var result=live.Session.GetResult();var launched=result.WorkItems.Where(w=>w.CustomerLaunchDay.HasValue).ToArray();var m=result.CustomerDelivery;
        Assert.Equal(launched.Length,m.LaunchedItems);Assert.Equal(launched.Average(w=>60-w.DoneDay!.Value),m.AverageInternalReleaseToCustomerDays);
        Assert.Equal(launched.Average(w=>60-w.DevelopmentStartedDay!.Value),m.AverageWorkStartToCustomerDays);
        Assert.Equal(result.WorkItems.Count(w=>w.State.IsDelivered() && w.CustomerLaunchDay is null),m.InternallyReleasedItemsAwaitingLaunch);
        Assert.Equal(result.ProductDelivery!.InternalReleases.Count(r=>r.VersionId=="V2"),m.InternalReleasesAwaitingLaunch);
    }
    [Fact] public void EmptyScheduledLaunchIsValidButDoesNotInventItems()
    {
        var request=Request(1,LaunchPlanUnit.WorkingDays) with{DeveloperCount=0};var result=new SimulationRunner().Run(request);
        Assert.Empty(Assert.Single(result.ProductDelivery!.CustomerLaunches).ItemIds);Assert.Equal(0,result.CustomerDelivery.LaunchedItems);Assert.Null(result.CustomerDelivery.AverageInternalReleaseToCustomerDays);
    }
    [Fact] public void InvalidOrTamperedLaunchHistoryIsRejected()
    {
        var live=Live();Advance(live,60);var saved=live.Capture();var ledger=saved.State.ProductDelivery!;
        Assert.Throws<ScenarioValidationException>(()=>LiveSimulation.Restore(saved with{State=saved.State with{ProductDelivery=ledger with{Pending=null}}}));
        Assert.Throws<ScenarioValidationException>(()=>live.Session.PlanCustomerLaunch(0,LaunchPlanUnit.Sprints,10));
        Assert.Throws<ScenarioValidationException>(()=>ProductLaunchSettings.TargetDay(int.MaxValue,1,LaunchPlanUnit.WorkingDays,10));
        Assert.Throws<ScenarioValidationException>(()=>live.Session.LaunchProductVersion("V99"));
        var release=ledger.InternalReleases[0];Assert.Throws<ScenarioValidationException>(()=>LiveSimulation.Restore(saved with{State=saved.State with{ProductDelivery=ledger with{InternalReleases=ledger.InternalReleases.Append(release).ToArray()}}}));
    }
}
