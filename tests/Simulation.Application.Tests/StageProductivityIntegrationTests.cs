using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.Infrastructure;
using Xunit;
namespace Simulation.Application.Tests;

public sealed class StageProductivityIntegrationTests
{
    private static string Json(object value) => JsonSerializer.Serialize(value);
    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures",name));
    private static void Until(LiveSimulation l,int day) { while(l.Session.CurrentDay<day) Assert.True(l.Step()); }
    private static SimulationRequest Request => LiveSimulation.Demo with { DeveloperCount=5, TesterCount=5, DevelopmentWipLimit=5,
        CodeReviewWipLimit=3,TestingWipLimit=5,DevelopmentEffort=4,CodeReviewEffort=2,TestingEffort=3 };
    [Theory]
    [InlineData("ContinuousArrival")] [InlineData("AlwaysAvailable")]
    public void DefaultProductivityReproducesGenuineModel03HistoryAndContinuation(string mode)
    {
        var live=LiveSessionJson.Load(Fixture($"model03-{mode}-live.json"));
        Assert.Equal("0.3",live.OriginalModelVersion);Assert.Equal(StageProductivity.Default,live.Session.Configuration.Productivity);
        var history=Json(live.Session.Days);Until(live,60);
        var expected=JsonSerializer.Deserialize<SimulationResult>(Fixture($"model03-{mode}-result-day60.json"))! with {SimulationModelVersion=SimulationModel.Version};
        Assert.Equal(LegacyObservationJson.Serialize(expected),LegacyObservationJson.Serialize(live.Session.GetResult()));Assert.Equal(history,Json(live.Session.Days.Take(30).ToArray()));
        var config=ExperimentJson.LoadScenario(Fixture("model03-scenario.json")).Configuration;
        Assert.Equal(StageProductivity.Default,config.Productivity);
        var fresh=LiveSimulation.Start(config with { Productivity=new(1,1,1) },Enum.Parse<WorkArrivalMode>(mode));Until(fresh,60);
        Assert.Equal(LegacyObservationJson.Serialize(expected),LegacyObservationJson.Serialize(fresh.Session.GetResult()));
    }
    [Fact]
    public void IndependentDay100120140InterventionsKeepHistoryEffortAndRandomStateAndReplayExactly()
    {
        var l=LiveSimulation.Start(Request,WorkArrivalMode.AlwaysAvailable);Until(l,100);
        var checkpoint=l.CreateCheckpoint("Before productivity");
        void Sequence()
        {
            foreach(var (day,p) in new[]{(100,new StageProductivity(1.5,1,1)),(120,new StageProductivity(1.5,1.3,1)),(140,new StageProductivity(1.5,1.3,1.4))})
            {
                Until(l,day);var before=l.Session.Capture();var old=before.Configuration.Productivity;
                l.Session.ApplyChanges(l.Session.Configuration with{Productivity=p},"User assumption");
                var after=l.Session.Capture();Assert.Equal(Json(before.Days),Json(after.Days));Assert.Equal(Json(before.WorkItems),Json(after.WorkItems));
                Assert.Equal(before.ArrivalRandomState,after.ArrivalRandomState);Assert.Equal(before.DiscoveryRandomState,after.DiscoveryRandomState);Assert.Equal(before.ReworkRandomState,after.ReworkRandomState);
                var change=l.Session.Changes.Last();Assert.Equal(day,change.Day);Assert.Equal(old,change.Before.Productivity);Assert.Equal(p,change.After.Productivity);
                var comparison=LivePerformance.Compare(l.Session,change,20);Assert.Equal(day-19,comparison.Before.FirstDay);Assert.Equal(day,comparison.Before.LastDay);
                Assert.Equal(day+1,comparison.After.FirstDay);Assert.Equal(day+20,comparison.After.LastDay);Assert.Equal(0,comparison.After.AvailableDays);
                Assert.True(l.Step());var d=l.CurrentSnapshot;
                Assert.Equal((d.PrimaryDevelopmentCapacity+.5*d.CollaborationDevelopmentCapacity)*p.Development,d.DevelopmentWork,10);
                Assert.Equal(d.UsedReviewCapacity*p.CodeReview,d.ReviewWork,10);Assert.Equal(d.UsedTesterCapacity*p.Testing,d.TestingWork,10);
                Assert.Equal(5,d.AvailableDeveloperCapacity);Assert.Equal(5,d.AvailableTesterCapacity);
                Assert.Equal(1,LivePerformance.Compare(l.Session,change,20).After.AvailableDays);
            }
            Until(l,160);
        }
        Sequence();var result=Json(l.Session.Capture());
        var loaded=LiveSessionJson.Load(LiveSessionJson.Save(l));Assert.Equal(result,Json(loaded.Session.Capture()));
        l.RestoreCheckpoint(checkpoint.Id);Sequence();Assert.Equal(result,Json(l.Session.Capture()));
        var c=LivePerformance.Compare(l.Session,l.Session.Changes.First(),20);Assert.True(c.After.IsComplete);
        var days=l.Session.Days.Skip(100).Take(20).ToArray();
        Assert.Equal(days.Sum(d=>d.UsedDeveloperCapacity)/days.Sum(d=>d.AvailableDeveloperCapacity),c.After.DeveloperUtilization!.Value,12);
        foreach(var m in new[]{LiveTrendMetric.DevelopmentCapacity,LiveTrendMetric.DevelopmentWork,LiveTrendMetric.DeveloperUtilization,LiveTrendMetric.TesterUtilization})
        {
            var series=LivePerformanceTrend.Project(l.Session,m,20,null);Assert.Equal(new[]{100,120,140},series.Interventions.Select(i=>i.Day));
            if(m==LiveTrendMetric.DevelopmentCapacity)Assert.Equal(l.Session.Days.Select(d=>(double?)d.UsedDevelopmentCapacity),series.Points.Select(p=>p.Value));
            if(m==LiveTrendMetric.DevelopmentWork)Assert.Equal(l.Session.Days.Select(d=>(double?)d.DevelopmentWork),series.Points.Select(p=>p.Value));
            if(m is LiveTrendMetric.DeveloperUtilization or LiveTrendMetric.TesterUtilization)Assert.All(series.Points,p=>Assert.InRange(p.Value!.Value,0,100+1e-9));
        }
    }
    [Theory]
    [InlineData(WorkItemStatus.Development)] [InlineData(WorkItemStatus.CodeReview)] [InlineData(WorkItemStatus.Testing)]
    public void AlreadyActiveItemKeepsRemainingEffortAndUsesNewFactorOnlyOnNextDay(WorkItemStatus stage)
    {
        var r=Request with {DeveloperCount=1,TesterCount=1,NumberOfWorkItems=1,DevelopmentEffort=10,CodeReviewEffort=10,TestingEffort=10};
        var l=LiveSimulation.Start(r,WorkArrivalMode.FixedBacklog);while(!l.Session.Days.Any(d=>d.Items.Any(i=>i.StateDuringDay==stage)))l.Step();
        var before=l.Session.WorkItems.Single();double Remaining()=>stage switch{WorkItemStatus.Development=>before.RemainingDevelopmentEffort,WorkItemStatus.CodeReview=>before.RemainingCodeReviewEffort,_=>before.RemainingTestingEffort};
        var remaining=Remaining();var original=Json(l.Session.Capture().WorkItems);
        l.Session.ApplyChanges(l.Session.Configuration with { Productivity=stage switch {WorkItemStatus.Development=>new(1.5,1,1),WorkItemStatus.CodeReview=>new(1,1.3,1),_=>new(1,1,1.4)}});
        Assert.Equal(original,Json(l.Session.Capture().WorkItems));l.Step();Assert.Equal(remaining-(stage==WorkItemStatus.Development?1.5:stage==WorkItemStatus.CodeReview?1.3:1.4),Remaining(),12);
    }
    [Fact]
    public void ScenarioExperimentLiveAndComparisonPersistAllIndependentFactors()
    {
        var baseline=new ScenarioDefinition(Guid.NewGuid(),Request);var changed=new ScenarioDefinition(Guid.NewGuid(),Request with{Productivity=new(1.5,1.2,1.4)});
        Assert.Equal(changed,ExperimentJson.LoadScenario(ExperimentJson.SaveScenario(changed)));
        var e=new Experiment(Guid.NewGuid(),"Productivity","User assumptions",new[]{baseline,changed},baseline.Id,new());
        var loaded=ExperimentJson.LoadExperiment(ExperimentJson.SaveExperiment(e));Assert.Equal(changed,loaded.Scenarios[1]);Assert.Equal("0.5",loaded.SimulationModelVersion);
        var a=ScenarioParameters.Describe(baseline.Configuration);var b=ScenarioParameters.Describe(changed.Configuration);
        Assert.Equal(new[]{"Code Review Productivity","Development Productivity","Testing Productivity"},a.Keys.Where(k=>a[k]!=b[k]).Order().ToArray());
        var live=LiveSimulation.Start(changed.Configuration,WorkArrivalMode.AlwaysAvailable);Until(live,25);live.CreateCheckpoint("Non-default");
        var restored=LiveSessionJson.Load(LiveSessionJson.Save(live));Until(live,70);Until(restored,70);Assert.Equal(Json(live.Capture()),Json(restored.Capture()));
        Assert.Equal(changed.Configuration.Productivity,restored.Checkpoints.Single().State.Configuration.Productivity);
    }
}
