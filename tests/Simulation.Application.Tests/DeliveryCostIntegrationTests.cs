using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Simulation.Application;
using Simulation.Core;
using Simulation.Infrastructure;
using Xunit;
namespace Simulation.Application.Tests;

public sealed class DeliveryCostIntegrationTests
{
    private static void Until(LiveSimulation live,int day) { while(live.Session.CurrentDay<day) Assert.True(live.Step()); }
    private static SimulationRequest Request => new() { DeveloperCount=1,TesterCount=1,DevelopmentWipLimit=1,NumberOfWorkItems=3,DevelopmentEffort=5,CodeReviewEffort=1,TestingEffort=2 };
    [Fact]
    public void CompletionCohortIncludesFullLifecycleAndBreakdownReconciles()
    {
        var live=LiveSimulation.Start(Request,WorkArrivalMode.FixedBacklog); Until(live,30);
        var first=live.Session.WorkItems.OrderBy(w=>w.DoneDay).First(); var done=first.DoneDay!.Value;
        var period=LivePerformance.Period(live.Session,done,done);
        Assert.Equal(1,period.Completed); Assert.Equal(8,period.DeliveryCostPerDoneItem);
        Assert.Equal(new DeliveryCost(5,1,0,2),period.AverageDeliveryCost);
        Assert.True(first.DevelopmentStartedDay<done-1);
        var all=LivePerformance.Period(live.Session,1,30);
        Assert.Equal(all.AverageDeliveryCost!.Development+all.AverageDeliveryCost.CodeReview+all.AverageDeliveryCost.Rework+all.AverageDeliveryCost.Testing,all.DeliveryCostPerDoneItem);
    }
    [Fact]
    public void EmptyAndPartlyUnknownCohortsAreUnavailableRatherThanBiasedMeans()
    {
        var live=LiveSimulation.Start(Request,WorkArrivalMode.FixedBacklog); Until(live,30);
        Assert.Null(LivePerformance.Period(live.Session,1,1).DeliveryCostPerDoneItem);
        var state=live.Session.Capture();
        var mixed=SimulationSession.Restore(state with { WorkItems=state.WorkItems.Select((w,i)=>i==0?w with{DeliveryCost=null}:w).ToArray() });
        Assert.Null(LivePerformance.Period(mixed,1,30).DeliveryCostPerDoneItem);
        var trend=LivePerformanceTrend.Project(mixed,LiveTrendMetric.DeliveryCost,30,null);
        Assert.Null(trend.Points.Last().Value);
    }
    [Fact]
    public void CheckpointAndSavedContinuationPreserveExactAccumulatedCosts()
    {
        var live=LiveSimulation.Start(Request with { DeveloperCount=3,DevelopmentWipLimit=2,Debt=new(){ShortcutRate=.4,Repayment=.2},Productivity=new(1.5,1.2,1.3),Quality=new(){Enabled=true,TestingDefectProbability=.2} },WorkArrivalMode.AlwaysAvailable);
        Until(live,15); var checkpoint=live.CreateCheckpoint("Cost state"); var loaded=LiveSessionJson.Load(LiveSessionJson.Save(live));
        Assert.Equal(JsonSerializer.Serialize(live.Session.Capture()),JsonSerializer.Serialize(loaded.Session.Capture()));
        Until(live,60); Until(loaded,60); var expected=JsonSerializer.Serialize(live.Session.Capture());
        Assert.Equal(expected,JsonSerializer.Serialize(loaded.Session.Capture()));
        live.RestoreCheckpoint(checkpoint.Id); Until(live,60); Assert.Equal(expected,JsonSerializer.Serialize(live.Session.Capture()));
    }
    [Fact]
    public void MissingJsonCostRemainsUnavailableForOldItemsWhileNewItemsAreMeasured()
    {
        var live=LiveSimulation.Start(Request,WorkArrivalMode.AlwaysAvailable); Until(live,10);
        var json=JsonNode.Parse(LiveSessionJson.Save(live))!; RemoveCost(json);
        var loaded=LiveSessionJson.Load(json.ToJsonString());
        var oldIds=loaded.Session.WorkItems.Select(w=>w.Id).ToHashSet();
        Assert.Null(LivePerformance.Rolling(loaded.Session).DeliveryCostPerDoneItem);
        Until(loaded,80);
        Assert.All(loaded.Session.WorkItems.Where(w=>!oldIds.Contains(w.Id)),w=>Assert.True(w.DeliveryCost.IsComplete));
        Assert.NotNull(LivePerformance.Rolling(loaded.Session,20).DeliveryCostPerDoneItem);
        Assert.All(loaded.Session.Days.Take(10).SelectMany(d=>d.Items),w=>Assert.Null(w.DeliveryCost));
    }
    [Fact]
    public void TrendAndBeforeAfterUseIdenticalFullLifecycleCompletionWindows()
    {
        var live=LiveSimulation.Start(Request,WorkArrivalMode.AlwaysAvailable); Until(live,20);
        live.Session.ApplyChanges(live.Session.Configuration with { Productivity=new(2,1,1) }); Until(live,60);
        foreach(var p in LivePerformanceTrend.Project(live.Session,LiveTrendMetric.DeliveryCost,10,null).Points)
        {
            var value=LivePerformance.Period(live.Session,Math.Max(1,p.Day-9),p.Day).DeliveryCostPerDoneItem;
            if(value is null) Assert.Null(p.Value); else Assert.InRange(Math.Abs(value.Value-p.Value!.Value),0,1e-9);
        }
        var comparison=LivePerformance.Compare(live.Session,live.Session.Changes.Single(),20);
        Assert.Equal(1,comparison.Before.FirstDay); Assert.Equal(20,comparison.Before.LastDay);
        Assert.Equal(21,comparison.After.FirstDay); Assert.Equal(40,comparison.After.LastDay);
        Assert.True(comparison.After.DeliveryCostPerDoneItem<comparison.Before.DeliveryCostPerDoneItem);
    }
    [Fact]
    public void EveryPreexistingObservationMatchesGenuinePreMeasurementExecutable()
    {
        var oldCulture=CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("sv-SE");
            var s=new SimulationSession(new SimulationScenario("Before delivery cost",80,new Team(5,3){DeveloperAvailability=.8,TesterAvailability=.9},3,2,2,[],12345) {
                ArrivalMode=WorkArrivalMode.AlwaysAvailable,Productivity=new(1.5,1.2,1.3),
                DevelopmentArrivalEffort=new TriangularEffort(1,4,7),CodeReviewArrivalEffort=new FixedEffort(1),TestingArrivalEffort=new FixedEffort(2),
                Debt=new(){ShortcutRate=.4,ShortcutEffortReduction=.3,Tolerance=.05,Repayment=.2},
                Quality=new(){Enabled=true,CodeReviewDefectProbability=.15,TestingDefectProbability=.2}
            });
            for(var i=0;i<80;i++)s.AdvanceOneDay();
            foreach(var pair in new[]{("pre-cost-state.json",JsonSerializer.Serialize(s.Capture())),("pre-cost-result.json",JsonSerializer.Serialize(s.GetResult()))})
            {
                var expected=JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures",pair.Item1)))!;
                var actual=JsonNode.Parse(pair.Item2)!;RemoveCost(actual); SkillsCompatibility.RemoveNewFields(actual);
                if (actual is JsonObject result && result.ContainsKey("SimulationModelVersion")) result["SimulationModelVersion"] = "0.5";
                Assert.True(JsonNode.DeepEquals(expected,actual),pair.Item1+": all pre-existing fields, including random streams and debt, must match exactly.");
            }
        }
        finally { CultureInfo.CurrentCulture=oldCulture; }
    }
    private static void RemoveCost(JsonNode node)
    {
        if(node is JsonObject obj)
        {
            foreach(var key in obj.Select(p=>p.Key).Where(k=>k.Equals("DeliveryCost",StringComparison.OrdinalIgnoreCase)).ToArray())obj.Remove(key);
            foreach(var child in obj.Select(p=>p.Value).OfType<JsonNode>())RemoveCost(child);
        }
        else if(node is JsonArray a)foreach(var child in a.OfType<JsonNode>())RemoveCost(child);
    }
}
