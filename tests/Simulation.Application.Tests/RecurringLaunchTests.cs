using System.Text.Json;
using System.Text.Json.Nodes;
using Simulation.Application;
using Simulation.Core;
using Simulation.Infrastructure;
using Xunit;
namespace Simulation.Application.Tests;
public class RecurringLaunchTests
{
    private static SimulationRequest Request(LaunchPlanningMode mode=LaunchPlanningMode.Recurring) => new()
    { SimulationDays=200,NumberOfWorkItems=20,DeveloperCount=4,TesterCount=2,DevelopmentEffort=1,CodeReviewEffort=1,TestingEffort=1,
      ArrivalMode=WorkArrivalMode.ContinuousArrival,WorkItemsPerDay=1,Release=new(ReleaseMode.Scheduled,8,10),
      ProductLaunch=new("Version 1.0",10,mode==LaunchPlanningMode.Manual?null:6,LaunchPlanUnit.Sprints){Mode=mode} };
    private static LiveSimulation Live() {var r=Request();return LiveSimulation.Start(r,r.ArrivalMode,r.WorkItemsPerDay);}
    private static void Until(LiveSimulation live,int day){while(live.Session.CurrentDay<day)Assert.True(live.Step());}
    [Theory]
    [InlineData(LaunchPlanningMode.Manual,0)]
    [InlineData(LaunchPlanningMode.OneTime,1)]
    [InlineData(LaunchPlanningMode.Recurring,3)]
    public void ModesRemainExplicitAndInternalDevelopmentIsUnchanged(LaunchPlanningMode mode,int count)
    {
        var result=new SimulationRunner().Run(Request(mode));var baseline=new SimulationRunner().Run(Request(LaunchPlanningMode.Manual));
        Assert.Equal(count,result.ProductDelivery!.CustomerLaunches.Count);
        Assert.Equal(JsonSerializer.Serialize(baseline.Days),JsonSerializer.Serialize(result.Days));
        Assert.Equal(baseline.CompletedWorkItems,result.CompletedWorkItems);
        Assert.Equal(baseline.AverageCycleTime,result.AverageCycleTime);
        if(mode==LaunchPlanningMode.Recurring)
        {
            Assert.Equal(new[]{60,120,180},result.ProductDelivery.CustomerLaunches.Select(l=>l.Day));
            Assert.Equal(240,result.ProductDelivery.Versions[^1].PlannedDay);
            Assert.Equal(4,result.ProductDelivery.Versions.Select(v=>v.Name).Distinct().Count());
            Assert.Equal(4,result.ProductDelivery.Versions.Select(v=>v.Id).Distinct().Count());
            var ids=result.ProductDelivery.CustomerLaunches.SelectMany(l=>l.ItemIds).ToArray();Assert.Equal(ids.Length,ids.Distinct().Count());
            Assert.Contains(result.ProductDelivery.InternalReleases,r=>r.VersionId=="V4");
        }
        else Assert.Null(result.ProductDelivery.Versions[^1].PlannedDay);
    }
    [Fact] public void LiveStopsAtEveryBoundaryAndMatchesRunWithSameDecisions()
    {
        var live=Live();
        foreach(var day in new[]{60,120,180})
        {
            Until(live,day);Assert.Equal(day,live.Session.PendingLaunchDecision!.Day);Assert.False(live.Step());
            var id=live.Session.PendingLaunchDecision.VersionId;
            var before=LaunchEconomics.Calculate(live.Session.Days,live.Session.ProductDelivery).Current;
            var launch=live.Session.LaunchProductVersion(id);Assert.Same(launch,live.Session.LaunchProductVersion(id));
            var after=LaunchEconomics.Calculate(live.Session.Days,live.Session.ProductDelivery).Current;
            Assert.Equal(before.Incurred,after.Incurred);Assert.Equal(before.WorkInProgress,after.WorkInProgress);
            Assert.Equal(before.CustomerLaunched+before.AwaitingLaunch,after.CustomerLaunched);Assert.Equal(0,after.AwaitingLaunch);
        }
        Until(live,200);
        Assert.Equal(Enumerable.Range(0,200),live.Session.Days.Select(d=>d.Day));
        Assert.Equal(JsonSerializer.Serialize(new SimulationRunner().Run(Request())),JsonSerializer.Serialize(live.Session.GetResult()));
        foreach(var p in LaunchEconomics.Calculate(live.Session.Days,live.Session.ProductDelivery).Points)
            Assert.InRange(Math.Abs(p.Incurred-(p.WorkInProgress+p.AwaitingLaunch+p.CustomerLaunched+p.Unallocated)),0,.000001m);
    }
    [Fact] public void PostponeAnchorsNextIntervalToActualDayAndKeepsOriginalRule()
    {
        var live=Live();Until(live,60);live.Session.PlanCustomerLaunch(2,LaunchPlanUnit.Sprints,10);Until(live,80);
        live.Session.LaunchProductVersion("V1");var first=live.Session.ProductDelivery.Versions[0];
        Assert.Equal(60,first.OriginalPlannedDay);Assert.Equal(80,first.PlannedDay);Assert.Equal(80,first.ActualLaunchDay);
        Assert.Equal(140,live.Session.ProductDelivery.Versions[^1].PlannedDay);Assert.Equal(6,live.Session.ProductDelivery.Versions[^1].EffectiveSchedule.Interval);
        Until(live,140);live.Session.LaunchProductVersion("V2");Assert.Equal(200,live.Session.ProductDelivery.Versions[^1].PlannedDay);
        Assert.Equal(first,live.Session.ProductDelivery.Versions[0]);
    }
    [Fact] public void SwitchingModesCancelsOrPlansExplicitlyAndPersists()
    {
        var live=Live();Until(live,60);live.Session.ConfigureCustomerLaunch(new(LaunchPlanningMode.Manual));
        Assert.Null(live.Session.PendingLaunchDecision);Assert.True(live.Step());
        live.Session.ConfigureCustomerLaunch(new(LaunchPlanningMode.Manual, null, LaunchPlanUnit.Sprints, 12));
        live=LiveSessionJson.Load(LiveSessionJson.Save(live));
        Assert.Null(live.Session.ProductDelivery.Versions[^1].PlannedDay);Assert.True(live.Session.ProductDelivery.Versions[^1].Plans.Last().Cancelled);
        live.Session.ConfigureCustomerLaunch(new(LaunchPlanningMode.OneTime,2));Until(live,63);live.Session.LaunchProductVersion("V1");
        Assert.Null(live.Session.ProductDelivery.Versions[^1].PlannedDay);
        live.Session.ConfigureCustomerLaunch(new(LaunchPlanningMode.Recurring,5));Until(live,68);live.Session.LaunchProductVersion("V2");
        Assert.Equal(73,live.Session.ProductDelivery.Versions[^1].PlannedDay);
        LiveSessionJson.Load(LiveSessionJson.Save(live));
    }
    [Fact] public void PendingSaveLoadCheckpointReplayPreservesSequenceAndEconomics()
    {
        var live=Live();Until(live,60);var saved=LiveSessionJson.Save(live);var checkpoint=live.CreateCheckpoint("Pending");
        live.Session.LaunchProductVersion("V1");Until(live,120);live.Session.LaunchProductVersion("V2");Until(live,125);
        var expected=JsonSerializer.Serialize(live.Session.GetResult());
        var loaded=LiveSessionJson.Load(saved);
        loaded.Session.LaunchProductVersion("V1");Until(loaded,120);loaded.Session.LaunchProductVersion("V2");Until(loaded,125);
        Assert.Equal(expected,JsonSerializer.Serialize(loaded.Session.GetResult()));
        live.RestoreCheckpoint(checkpoint.Id);Assert.NotNull(live.Session.PendingLaunchDecision);
        live.Session.LaunchProductVersion("V1");Until(live,120);live.Session.LaunchProductVersion("V2");Until(live,125);
        Assert.Equal(expected,JsonSerializer.Serialize(live.Session.GetResult()));
    }
    [Fact] public void LegacyPlanWithoutModesRemainsOneTime()
    {
        var r=Request(LaunchPlanningMode.OneTime);var live=LiveSimulation.Start(r,r.ArrivalMode,r.WorkItemsPerDay);Until(live,30);
        var json=JsonNode.Parse(LiveSessionJson.Save(live))!;
        void Strip(JsonNode? node)
        {
            if(node is JsonObject o){o.Remove("Mode");o.Remove("Schedule");o.Remove("Cancelled");foreach(var pair in o.ToArray())Strip(pair.Value);}
            else if(node is JsonArray a)foreach(var child in a)Strip(child);
        }
        Strip(json);var old=json.ToJsonString().Replace("\"0.10\"","\"0.9\"");
        var restored=LiveSessionJson.Load(old);Assert.Equal(LaunchPlanningMode.OneTime,restored.Session.ProductDelivery.Versions[^1].EffectiveSchedule.Mode);
        Until(restored,60);restored.Session.LaunchProductVersion("V1");Until(restored,200);Assert.Single(restored.Session.ProductDelivery.CustomerLaunches);
    }
    [Fact] public void InvalidRecurringRulesAndDuplicateNamesAreRejectedWithoutMutation()
    {
        var live=Live();var before=LiveSessionJson.Save(live);
        Assert.Throws<ScenarioValidationException>(()=>live.Session.ConfigureCustomerLaunch(new(LaunchPlanningMode.Recurring,0)));
        Assert.Equal(before,LiveSessionJson.Save(live));
        Until(live,60);live.Session.LaunchProductVersion("V1");
        Assert.Throws<ScenarioValidationException>(()=>live.Session.RenameActiveVersion("Version 1.0"));
        Assert.Equal("Version 1.0",live.Session.ProductDelivery.Versions[0].Name);
    }
    [Fact] public void EarlyManualLaunchUnderRecurringRuleAnchorsToItsActualDay()
    {
        var live=Live();Until(live,30);live.Session.LaunchProductVersion("V1");
        Assert.Equal(90,live.Session.ProductDelivery.Versions[^1].PlannedDay);
        Assert.Equal(60,live.Session.ProductDelivery.Versions[0].OriginalPlannedDay);
        Assert.Equal(30,live.Session.ProductDelivery.Versions[0].ActualLaunchDay);
        Until(live,90);Assert.NotNull(live.Session.PendingLaunchDecision);
    }
    [Fact] public void SuccessorOverflowDoesNotPartiallyLaunchOrLoseWork()
    {
        var live=Live();live.Session.ConfigureCustomerLaunch(new(LaunchPlanningMode.Recurring,int.MaxValue));
        Until(live,20);var before=LiveSessionJson.Save(live);
        Assert.Throws<ScenarioValidationException>(()=>live.Session.LaunchProductVersion("V1"));
        Assert.Equal(before,LiveSessionJson.Save(live));
    }

}
