using System.Text.Json;
using System.Text.Json.Nodes;
using Simulation.Application;
using Simulation.Core;
using Simulation.Infrastructure;
using Xunit;
namespace Simulation.Application.Tests;
public sealed class TeamEconomicsTests
{
    private static LiveSimulation Start(SimulationRequest? request = null) => LiveSimulation.Start(request ?? new() { NumberOfWorkItems = 0 }, WorkArrivalMode.FixedBacklog);
    private static void To(LiveSimulation live, int day) { while(live.Session.CurrentDay < day) live.Step(); }
    private static string Json(object value) => JsonSerializer.Serialize(value);
    [Fact]
    public void IdleStaffCostAndSpecialistsAreChargedExactlyOnce()
    {
        var live = Start(new() { NumberOfWorkItems = 0, DeveloperCount = 5, TesterCount = 2, Skills = new(1, .5) });
        To(live,20); var d = live.Session.Days[0];
        Assert.Equal(0, d.UsedDeveloperCapacity); Assert.Equal(0, d.UsedTesterCapacity);
        Assert.Equal(17000m, d.Economics!.Developers); Assert.Equal(5000m, d.Economics.Specialists); Assert.Equal(7500m, d.Economics.Testers);
        var p = LivePerformance.Period(live.Session,1,20).Economics;
        Assert.Equal(590000m,p.TotalTeamCost); Assert.Null(p.CostPerDelivery); Assert.Equal(0,p.ReworkCost);
    }
    [Fact]
    public void AvailabilityAllocatesCostButProductivityAndNominalCapacityDoNotChangeStaffPrice()
    {
        var live=Start(new() { NumberOfWorkItems=0,DeveloperCount=4,TesterCount=2,DeveloperAvailability=.5,TesterAvailability=.25,DeveloperCapacityPerDay=0,TesterCapacityPerDay=10,Productivity=new(2,2,2),Skills=new(2,.5) });
        live.Step(); Assert.Equal(11125m,live.Session.Days[0].Economics!.Total);
    }
    [Fact]
    public void Day100InterventionChargesNewStaffAndRatesOnlyFrom101()
    {
        var live=Start();To(live,100);var old=live.Session.Days.ToArray();
        live.Session.ApplyChanges(live.Session.Configuration with {Team=live.Session.Configuration.Team with{TesterCount=3},RoleCosts=new(90000m,80000m,110000m,20)});
        Assert.Equal(28750m,live.Session.Days[^1].Economics!.Total);
        To(live,120);Assert.Equal(34500m,live.Session.Days[100].Economics!.Total);
        Assert.Equal(690000m,LivePerformance.Period(live.Session,101,120).Economics.TotalTeamCost);
        Assert.Equal(Json(old),Json(live.Session.Days.Take(100).ToArray()));
        Assert.Equal(28750m*10+34500m*10,LivePerformance.Period(live.Session,91,110).Economics.TotalTeamCost);
    }
    [Fact]
    public void ReleasedItemsOnlyAndTrendUseExactlyTheSameWindow()
    {
        var live=Start(new(){NumberOfWorkItems=50,DevelopmentEffort=1,CodeReviewEffort=1,TestingEffort=1,Release=new(ReleaseMode.Scheduled,2,10)});To(live,40);
        var p=LivePerformance.Period(live.Session,21,40);
        Assert.Equal(4,p.Completed);Assert.Equal(575000m/4,p.Economics.CostPerDelivery);
        var trend=LivePerformanceTrend.Project(live.Session,LiveTrendMetric.TeamCostPerDelivery,20,null);
        foreach(var point in trend.Points) Assert.Equal((double?)LivePerformance.Period(live.Session,Math.Max(1,point.Day-19),point.Day).Economics.CostPerDelivery,point.Value);
        Assert.Null(trend.Points[0].Value);
        var measured=AnalysisMetrics.Measure(new(),live.Session.GetResult(),20);
        Assert.Equal((double?)p.Economics.TotalTeamCost,measured.Values[AnalysisMetric.TotalTeamCostSek]);
    }
    [Fact]
    public void ReworkAllocationUsesObservedCapacityAndIsIncludedInTotal()
    {
        var live=Start(new(){NumberOfWorkItems=15,DevelopmentEffort=1,CodeReviewEffort=1,TestingEffort=1,Skills=new(1,.5),Quality=new(){Enabled=true,TestingDefectProbability=1}});To(live,30);
        Assert.Contains(live.Session.Days,d=>d.UsedReworkDeveloperCapacity>0);
        foreach(var d in live.Session.Days)
        {
            Assert.Equal(29500m,d.Economics!.Total);
            Assert.Equal(22000m*(decimal)(d.UsedReworkDeveloperCapacity/d.AvailableDeveloperCapacity),d.Economics.Rework);
        }
        Assert.Equal(live.Session.Days.Sum(d=>d.Economics!.Rework),LivePerformance.Period(live.Session,1,30).Economics.ReworkCost);
    }
    [Theory]
    [InlineData(120,12,100,10,10)] [InlineData(80,12,100,10,-10)] [InlineData(100,12,100,10,0)]
    public void IncrementalCostPreservesSign(int cost,int delivered,int baseCost,int baseDelivered,int expected)
    { Assert.Equal(expected,EconomicPeriod.IncrementalCostPerAdditionalDelivery(new(baseCost,baseDelivered,0),new(cost,delivered,0))); }
    [Theory]
    [InlineData(10)] [InlineData(9)]
    public void NoAdditionalDeliveriesIsUnavailable(int deliveries)
    { Assert.Null(EconomicPeriod.IncrementalCostPerAdditionalDelivery(new(100,10,0),new(120,deliveries,0))); }
    [Fact]
    public async Task GuidedBranchesCompareEconomicCostFromSameCheckpoint()
    {
        var live=LiveSimulation.Start(new(){DeveloperCount=10,TesterCount=2,TestingEffort=5,NumberOfWorkItems=100},WorkArrivalMode.ContinuousArrival,4);To(live,100);
        var cp=live.CreateCheckpoint("start");live.Session.ApplyChanges(live.Session.Configuration with{Team=live.Session.Configuration.Team with{TesterCount=3}});
        var e=new GuidedInterventionExperiment(cp,live.Session,20);await e.PrepareBaselineAsync();Assert.Equal(100,live.Session.CurrentDay);To(live,120);
        var r=e.Observe(live.Session);var b=r.Baseline!.Period.Economics;var x=r.Intervention!.Period.Economics;
        Assert.Equal(1000000m,b.TotalTeamCost);Assert.Equal(1075000m,x.TotalTeamCost);
        Assert.Equal(75000m,x.TotalTeamCost-b.TotalTeamCost);
        Assert.True(x.Released>b.Released);Assert.Equal(75000m/(x.Released-b.Released),EconomicPeriod.IncrementalCostPerAdditionalDelivery(b,x));
    }
    [Fact]
    public void SaveLoadCheckpointAndScenarioRoundTripPreserveRatesAndHistory()
    {
        var request=new SimulationRequest{RoleCosts=new(91000.25m,72000m,112000m,22)};
        Assert.Equal(request.RoleCosts,ExperimentJson.LoadScenario(ExperimentJson.SaveScenario(ScenarioDefinition.Create(request))).Configuration.RoleCosts);
        var live=Start(request);To(live,10);var cp=live.CreateCheckpoint("costs");var saved=LiveSessionJson.Save(live);
        live.Session.ApplyChanges(live.Session.Configuration with{RoleCosts=new(93000m,76000m,115000m,21)});To(live,30);
        var expected=Json(live.Session.Capture());var loaded=LiveSessionJson.Load(LiveSessionJson.Save(live));Assert.Equal(expected,Json(loaded.Session.Capture()));
        live.RestoreCheckpoint(cp.Id);var replay=LiveSessionJson.Load(saved);To(live,30);To(replay,30);Assert.Equal(Json(live.Session.Capture()),Json(replay.Session.Capture()));
        Assert.Equal(request.RoleCosts,live.Session.Configuration.RoleCosts);
    }
    [Fact]
    public void OldSaveDefaultsApplyOnlyToFutureDaysAndUnknownHistoryIsNotZero()
    {
        var live=Start();To(live,10);var json=JsonNode.Parse(LiveSessionJson.Save(live))!;
        void Strip(JsonNode node){if(node is JsonObject o){o.Remove("Economics");o.Remove("RoleCosts");foreach(var n in o.Select(x=>x.Value).OfType<JsonNode>())Strip(n);}else if(node is JsonArray a)foreach(var n in a.OfType<JsonNode>())Strip(n);}
        Strip(json);var old=LiveSessionJson.Load(json.ToJsonString());Assert.Equal(new RoleCostSettings(),old.Session.Configuration.RoleCosts);
        Assert.Null(LivePerformance.Period(old.Session,1,10).Economics.TotalTeamCost);To(old,20);
        Assert.Null(LivePerformance.Period(old.Session,1,20).Economics.TotalTeamCost);
        Assert.Equal(287500m,LivePerformance.Period(old.Session,11,20).Economics.TotalTeamCost);
        Assert.All(old.Session.Days.Take(10),d=>Assert.Null(d.Economics));
    }
    [Fact]
    public void InvalidRatesAreRejectedAndZeroRatesAreValid()
    {
        Assert.Throws<ScenarioValidationException>(()=>Start(new(){RoleCosts=new(-1,1,1,20)}));
        Assert.Throws<ScenarioValidationException>(()=>Start(new(){RoleCosts=new(1,1,1,0)}));
        var live=Start(new(){RoleCosts=new(0,0,0,20)});live.Step();Assert.Equal(0m,live.Session.Days[0].Economics!.Total);
    }
    [Fact]
    public void MoneySettingsDoNotChangeAnyOperationalObservationOrRandomState()
    {
        var a=Start(BaselineScenario.DefectsAndReworkExample());var b=Start(BaselineScenario.DefectsAndReworkExample() with{RoleCosts=new(1,2,3,5)});To(a,60);To(b,60);
        Assert.Equal(Json(a.Session.Days.Select(d=>d with{Economics=null})),Json(b.Session.Days.Select(d=>d with{Economics=null})));
        Assert.Equal(Json(a.Session.Capture().WorkItems),Json(b.Session.Capture().WorkItems));
        Assert.Equal(a.Session.Capture().ArrivalRandomState,b.Session.Capture().ArrivalRandomState);
        Assert.Equal(a.Session.Capture().DiscoveryRandomState,b.Session.Capture().DiscoveryRandomState);
        Assert.Equal(a.Session.Capture().ReworkRandomState,b.Session.Capture().ReworkRandomState);
    }
}
