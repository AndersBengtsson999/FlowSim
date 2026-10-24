using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.UI.Help;
using Simulation.UI.Insights;
using Simulation.UI.ViewModels;
using Xunit;

namespace Simulation.UI.Tests;

public class SimulationInsightsTests
{
    private static LiveSimulation Scenario(int testers=1)=>LiveSimulation.Start(new SimulationRequest {
        NumberOfWorkItems=80,DeveloperCount=6,TesterCount=testers,DevelopmentEffort=1,CodeReviewEffort=1,TestingEffort=5,
        DevelopmentWipLimit=20,CodeReviewWipLimit=20,TestingWipLimit=6 },WorkArrivalMode.ContinuousArrival,3);
    private static string Json(object value)=>JsonSerializer.Serialize(value);
    [Theory][InlineData(7)][InlineData(12345)][InlineData(9876)]
    public void AnalysisIsDeterministicReadOnlyAndDoesNotConsumeRandomness(int seed)
    {
        var a=LiveSimulation.Start(BaselineScenario.DefectsAndReworkExample() with { RandomSeed=seed },WorkArrivalMode.AlwaysAvailable);
        var b=LiveSimulation.Restore(a.Capture());
        for(var n=0;n<60;n++)
        {
            a.Step();b.Step();var before=Json(a.Capture());
            var report=SimulationInsights.Analyze(a);
            Assert.Equal(Json(report),Json(SimulationInsights.Analyze(a)));
            Assert.Equal(before,Json(a.Capture()));
            Assert.Equal(Json(b.Session.Capture()),Json(a.Session.Capture()));
        }
        Assert.Equal(Json(KpiDashboard.Project(b.Session,new())),Json(KpiDashboard.Project(a.Session,new())));
    }
    [Fact] public void EmptyAndPartialHistoryIsNotZeroEvidence()
    {
        var live=Scenario();var empty=SimulationInsights.Analyze(live);
        Assert.Equal(0,empty.CapacityPeriod.Observations);Assert.Null(empty.CapacityPeriod.FirstDay);
        Assert.All(empty.Insights,i=>{Assert.False(i.Relevant);Assert.Equal(EvidenceStrength.InsufficientEvidence,i.Strength);Assert.Null(i.Metrics[0].Value);});
        for(var n=0;n<3;n++)live.Step();
        var shortReport=SimulationInsights.Analyze(live);
        Assert.DoesNotContain(shortReport.Insights,i=>i.Category==InsightCategory.CapacityImbalance);
        Assert.Equal(3,shortReport.CapacityPeriod.Observations);
    }
    [Fact] public void StationClassificationMatchesEveryDayIncludingInterventionBoundary()
    {
        var live=Scenario(0);
        for(var n=0;n<50;n++)
        {
            live.Step();
            if(n==25)live.Session.ApplyChanges(live.Session.Configuration with {Team=live.Session.Configuration.Team with {TesterCount=2}},"Test");
            var report=SimulationInsights.Analyze(live);var station=QueuePressure.From(live);
            foreach(var key in new[]{"backlog","review-wait","testing-wait"})
                Assert.Equal(station[key].Kind.ToString(),report.Insights.Single(i=>i.Id=="queue."+key).MaterialState);
            if(n==25)Assert.Equal(0,report.CapacityPeriod.Observations);
            if(n==26)Assert.Equal(27,report.CapacityPeriod.FirstDay);
        }
    }
    [Fact] public void CapacityEvidenceUsesTransitionsAndSharedRawDeveloperCapacity()
    {
        var live=Scenario();for(var n=0;n<35;n++)live.Step();
        var report=SimulationInsights.Analyze(live);
        var insight=Assert.Single(report.Insights,i=>i.Id=="imbalance.testing-wait");
        var days=live.Session.Days.TakeLast(20).ToArray();
        Assert.Equal(days.Sum(d=>d.UsedDeveloperCapacity),insight.Metrics.Single(m=>m.Name=="Developer capacity used (all shared work)").Value);
        Assert.Equal(days.Sum(d=>d.UsedReviewCapacity),insight.Metrics.Single(m=>m.Name=="Review capacity used (shared developers)").Value);
        Assert.Equal(live.Session.WorkItems.Count(w=>w.DoneDay>=16 && w.DoneDay<=35),insight.Metrics.Single(m=>m.Name=="Internal releases").Value);
        Assert.Equal(live.Session.WorkItems.SelectMany(w=>w.Transitions).Count(t=>t.From==WorkItemStatus.Development && t.To==WorkItemStatus.WaitingForCodeReview && t.Day>=16 && t.Day<=35),insight.Metrics.Single(m=>m.Name=="Development exits to review").Value);
        Assert.Equal(16,insight.Period.FirstDay);Assert.Equal(35,insight.Period.LastDay);
        Assert.Contains("Correlation does not establish cause",insight.Explanation);
        Assert.All(report.Insights,i=>Assert.DoesNotContain("definitely",i.Observation,StringComparison.OrdinalIgnoreCase));
        Assert.Contains("no independent reviewer staffing pool",report.Insights.Single(i=>i.Id=="queue.review-wait").Explanation);
    }
    [Fact] public void StableIdentityIgnoresDayAndCountsWhileMaterialChangeIsUpdated()
    {
        var live=Scenario(0);for(var n=0;n<20;n++)live.Step();
        var first=SimulationInsights.Analyze(live);var tracker=new InsightTracker();tracker.Observe(first);
        var item=first.Insights.Single(i=>i.Id=="queue.testing-wait");Assert.True(item.Relevant);
        Assert.Equal(InsightStatus.New,tracker.Items.Single(i=>i.Insight.Id==item.Id).Status);
        tracker.Review(item.Id);live.Step();tracker.Observe(SimulationInsights.Analyze(live));
        Assert.Equal(InsightStatus.Reviewed,tracker.Items.Single(i=>i.Insight.Id==item.Id).Status);
        var changed=item with { MaterialState="Constrained",Strength=EvidenceStrength.TrendWithCapacityEvidence };
        tracker.Observe(first with { Insights=[changed] });Assert.Equal(1,tracker.UpdatedCount);Assert.Equal(0,tracker.NewCount);
        tracker.Observe(first with { Insights=[changed] });Assert.Equal(1,tracker.UpdatedCount);
        tracker.Review(item.Id);Assert.Equal(0,tracker.UpdatedCount);
        tracker.Observe(first with { Insights=[] });Assert.Equal(InsightStatus.NoLongerRelevant,tracker.Items.Single(i=>i.Insight.Id==item.Id).Status);
        tracker.Observe(first with { Insights=[changed] });Assert.Equal(1,tracker.UpdatedCount);
        tracker.Reset();Assert.Empty(tracker.Items);Assert.Equal("Insights",tracker.Indicator);
    }
    [Fact] public void LiveIndicatorReviewLoadAndCheckpointRestoreAreTimelineScoped()
    {
        using var vm=new LiveViewModel();var live=Scenario(0);for(var n=0;n<20;n++)live.Step();vm.Load(live);
        Assert.Contains("new",vm.InsightsIndicator);
        foreach(var item in vm.Insights.Items)vm.ReviewInsight(item.Insight.Id);
        Assert.Equal("Insights",vm.InsightsIndicator);
        vm.Load(LiveSimulation.Restore(live.Capture()));Assert.True(vm.Insights.NewCount>0);
        vm.Reset();Assert.Empty(vm.Insights.Items);Assert.Null(vm.InsightsReport);
    }
    [Fact] public void IdleTestingIsAnObservationNotAnInferredStaffingExcess()
    {
        var live=LiveSimulation.Start(new(){NumberOfWorkItems=0,TesterCount=2},WorkArrivalMode.FixedBacklog);
        for(var n=0;n<6;n++)live.Step();
        var item=Assert.Single(SimulationInsights.Analyze(live).Insights,i=>i.Id=="imbalance.testing-supply");
        Assert.Contains("no active or waiting Testing items at day end",item.Observation);
        Assert.Contains("not proof of a staffing excess",item.Evidence);
        Assert.Equal(EvidenceStrength.ObservedCondition,item.Strength);
        Assert.DoesNotContain(SimulationInsights.Analyze(live).Insights,i=>i.Id=="imbalance.testing-wait");
    }
    [Fact] public void Day100ChangeStartsNewEvidenceOnDay101WithoutMixingOldCapacity()
    {
        var live=Scenario();for(var n=0;n<100;n++)live.Step();
        live.Session.ApplyChanges(live.Session.Configuration with {Team=live.Session.Configuration.Team with {TesterCount=3}},"Change");
        Assert.Equal(0,SimulationInsights.Analyze(live).CapacityPeriod.Observations);
        live.Step();var first=SimulationInsights.Analyze(live);
        Assert.Equal(101,first.CapacityPeriod.FirstDay);Assert.Equal(101,first.CapacityPeriod.LastDay);
        Assert.DoesNotContain(first.Insights,i=>i.Category==InsightCategory.CapacityImbalance);
        for(var n=0;n<19;n++)live.Step();
        Assert.Equal(new InsightPeriod(101,120,20,20),SimulationInsights.Analyze(live).CapacityPeriod);
    }
    [Fact] public void RestoringCheckpointClearsAcknowledgementsEvenOnSameLiveWrapper()
    {
        using var vm=new LiveViewModel();var live=Scenario(0);for(var n=0;n<20;n++)live.Step();
        var saved=live.CreateCheckpoint("Observation");vm.Load(live);
        foreach(var i in vm.Insights.Items)vm.ReviewInsight(i.Insight.Id);
        Assert.Equal(0,vm.Insights.NewCount);
        vm.SelectedCheckpoint=new CheckpointRow(saved.Id,"Observation");vm.RestoreCommand.Execute(null);
        Assert.True(vm.Insights.NewCount>0);Assert.Equal(20,vm.Day);
    }
    [Fact] public void DocumentationAndContextArePackagedAndSearchable()
    {
        var guide=new GuideCatalog();guide.Validate(requireImages:true);
        Assert.Equal(SimulationInsights.Topic,guide.Resolve(SimulationInsights.Topic).Id);
        Assert.Contains(guide.Search("Simulation Insights"),r=>r.Id==SimulationInsights.Topic);
        Assert.Equal("reference.insight-terms",guide.Resolve("reference.insight-terms").Id);
        var live=Scenario();for(var n=0;n<20;n++)live.Step();
        Assert.All(SimulationInsights.Analyze(live).Insights,i=>{
            Assert.Equal(i.Topic,guide.Resolve(i.Topic).Id);
            Assert.All(i.Kpis,k=>Assert.Contains(KpiDashboard.Definitions,d=>d.Id==k));
            Assert.All(i.Stations,s=>Assert.NotEqual("live-game-view.station-inspection",guide.Station(s)));
        });
    }
}
