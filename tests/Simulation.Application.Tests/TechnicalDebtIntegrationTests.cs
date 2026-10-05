using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.Infrastructure;
using Xunit;
namespace Simulation.Application.Tests;

public sealed class TechnicalDebtIntegrationTests
{
    private static string Json(object value)=>JsonSerializer.Serialize(value);
    private static string Fixture(string name)=>File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fixtures",name));
    private static void Until(LiveSimulation live,int day){while(live.Session.CurrentDay<day)Assert.True(live.Step());}
    private static SimulationRequest Request=>LiveSimulation.Demo with{DeveloperCount=5,TesterCount=5,DevelopmentEffort=4,CodeReviewEffort=1,TestingEffort=1,DevelopmentWipLimit=5};
    [Theory]
    [InlineData("ContinuousArrival")] [InlineData("AlwaysAvailable")]
    public void ZeroDebtMatchesGenuineModel04FullResultsAndRandomContinuation(string mode)
    {
        var old=LiveSessionJson.Load(Fixture($"model04-{mode}-live.json"));Assert.Equal("0.4",old.OriginalModelVersion);
        Assert.Equal(0,old.Session.DebtState.Amount);Assert.Equal(old.Session.WorkItems.Where(w=>w.DevelopmentCompletedDay.HasValue).Sum(w=>w.DevelopmentEffort),old.Session.DebtState.CumulativeDevelopmentScope);
        var history=Json(old.Session.Days);Until(old,60);Assert.Equal(history,Json(old.Session.Days.Take(30).ToArray()));
        var expected=JsonSerializer.Deserialize<SimulationResult>(Fixture($"model04-{mode}-result-day60.json"))! with{SimulationModelVersion=SimulationModel.Version};
        Assert.Equal(LegacyObservationJson.Serialize(expected),LegacyObservationJson.Serialize(old.Session.GetResult()));
        var request=ExperimentJson.LoadScenario(Fixture("model04-scenario.json")).Configuration;
        var fresh=LiveSimulation.Start(request,Enum.Parse<WorkArrivalMode>(mode));Until(fresh,60);
        Assert.Equal(LegacyObservationJson.Serialize(expected),LegacyObservationJson.Serialize(fresh.Session.GetResult()));
        Assert.Equal(fresh.Session.Capture().ArrivalRandomState,old.Session.Capture().ArrivalRandomState);
        Assert.Equal(fresh.Session.Capture().DiscoveryRandomState,old.Session.Capture().DiscoveryRandomState);
        Assert.All(fresh.Session.Days,d=>Assert.Equal(0,d.Debt!.State.Amount));
        Assert.Equal(fresh.Session.WorkItems.Where(w=>w.DevelopmentCompletedDay.HasValue).Sum(w=>w.DevelopmentEffort),fresh.Session.DebtState.CumulativeDevelopmentScope,10);
    }
    [Fact]
    public void FactorInterventionPreservesDebtActivePlansHistoryAndCheckpointContinuation()
    {
        var live = LiveSimulation.Start(Request with { Debt = new() { ShortcutRate = 1, Tolerance = 1, CreationFactor = 1 } }, WorkArrivalMode.AlwaysAvailable);
        Until(live, 30);
        var before = live.Session.Capture();
        Assert.True(before.DebtState!.Amount > 0);
        Assert.Contains(before.WorkItems, w => w.State == WorkItemStatus.Development);
        live.Session.ApplyChanges(live.Session.Configuration with { Debt = live.Session.Configuration.Debt with { CreationFactor = 2 } });
        Assert.Equal(before.DebtState, live.Session.DebtState);
        Assert.Equal(Json(before.WorkItems), Json(live.Session.Capture().WorkItems));
        Assert.Equal(Json(before.Days), Json(live.Session.Days));
        Assert.Equal(before.ArrivalRandomState, live.Session.Capture().ArrivalRandomState);
        var checkpoint = live.CreateCheckpoint("Factor 2");
        var restored = LiveSessionJson.Load(LiveSessionJson.Save(live));
        Until(live, 60); Until(restored, 60);
        Assert.Equal(Json(live.Session.Capture()), Json(restored.Session.Capture()));
        var expected = Json(live.Session.Capture());
        live.RestoreCheckpoint(checkpoint.Id);
        Assert.Equal(2, live.Session.Configuration.Debt.CreationFactor);
        Until(live, 60); Assert.Equal(expected, Json(live.Session.Capture()));
        foreach (var old in before.WorkItems.Where(w => w.DevelopmentPlan != null))
            Assert.Equal(old.DevelopmentPlan, live.Session.WorkItems.Single(w => w.Id == old.Id).DevelopmentPlan);
        var newStarts = live.Session.WorkItems.Where(w => w.DevelopmentStartedDay >= 30).ToArray();
        Assert.NotEmpty(newStarts);
        Assert.All(newStarts, w => Assert.Equal(w.DevelopmentPlan!.SavedEffort * 2, w.DevelopmentPlan.DebtToCreate));
        Assert.Equal(30, live.Session.Changes.Single().Day);
    }

    [Fact]
    public void LiveInterventionsUseNextDayAndDoNotRewriteWorkHistoryOrRandomState()
    {
        var live=LiveSimulation.Start(Request,WorkArrivalMode.AlwaysAvailable);Until(live,100);
        foreach(var (day,settings) in new[]{(100,new TechnicalDebtSettings{ShortcutRate=.4}),(120,new TechnicalDebtSettings{ShortcutRate=.4,Repayment=.3}),(140,new TechnicalDebtSettings{ShortcutRate=.4,Repayment=.3,Tolerance=.15})})
        {
            Until(live,day);var before=live.Session.Capture();live.Session.ApplyChanges(live.Session.Configuration with{Debt=settings});var after=live.Session.Capture();
            Assert.Equal(Json(before.WorkItems),Json(after.WorkItems));Assert.Equal(Json(before.Days),Json(after.Days));Assert.Equal(before.DebtState,after.DebtState);Assert.Equal(before.ArrivalRandomState,after.ArrivalRandomState);
            var c=LivePerformance.Compare(live.Session,live.Session.Changes.Last(),20);Assert.Equal(day-19,c.Before.FirstDay);Assert.Equal(day,c.Before.LastDay);Assert.Equal(day+1,c.After.FirstDay);Assert.Equal(0,c.After.AvailableDays);
            live.Step();Assert.Equal(settings.Tolerance,live.CurrentSnapshot.Debt!.Tolerance);
            foreach(var active in before.WorkItems.Where(w=>w.State==WorkItemStatus.Development))Assert.Equal(active.DevelopmentPlan,live.Session.WorkItems.Single(w=>w.Id==active.Id).DevelopmentPlan);
            Assert.Equal(1,LivePerformance.Compare(live.Session,live.Session.Changes.Last(),20).After.AvailableDays);
        }
        Until(live,160);Assert.Equal(new[]{100,120,140},live.Session.Changes.Select(c=>c.Day));
        Assert.All(live.Session.Days.Take(100),d=>Assert.Equal(0,d.Debt!.State.Amount));
        Assert.All(live.Session.Days.Skip(120),d=>Assert.True(d.UsedDebtRepaymentCapacity<=.3*(d.AvailableDeveloperCapacity-d.UsedReviewCapacity-d.UsedReworkDeveloperCapacity)+1e-10));
    }
    [Fact]
    public void DebtContainingCheckpointAndSaveReplayExactlyIncludingChoicesAndRandomState()
    {
        var request=Request with{Debt=new(){ShortcutRate=.4,ShortcutEffortReduction=.3,Tolerance=.05},DevelopmentDistribution=new TriangularEffort(1,4,7),Quality=new(){Enabled=true,CodeReviewDefectProbability=.1,TestingDefectProbability=.1}};
        var live=LiveSimulation.Start(request,WorkArrivalMode.AlwaysAvailable);Until(live,80);Assert.True(live.Session.DebtState.Amount>0);
        var checkpoint=live.CreateCheckpoint("Debt state");var saved=LiveSessionJson.Save(live);var loaded=LiveSessionJson.Load(saved);
        void Future(LiveSimulation x){x.Session.ApplyChanges(x.Session.Configuration with{Debt=x.Session.Configuration.Debt with{ShortcutRate=.2,Repayment=.3}});Until(x,100);x.Session.ApplyChanges(x.Session.Configuration with{Productivity=new(1.5,1.3,1.4)});Until(x,150);}
        Future(live);Future(loaded);Assert.Equal(Json(live.Capture()),Json(loaded.Capture()));
        var expected=Json(live.Session.Capture());live.RestoreCheckpoint(checkpoint.Id);Future(live);Assert.Equal(expected,Json(live.Session.Capture()));
        Assert.Equal(expected,Json(LiveSessionJson.Load(LiveSessionJson.Save(live)).Session.Capture()));
        Assert.All(live.Session.Days,d=>Assert.InRange(d.UsedDeveloperCapacity,0,d.AvailableDeveloperCapacity+1e-10));
    }
    [Fact]
    public void DailyDebtTrendUsesActualStateAndRestoresWithoutRollingAverages()
    {
        var live=LiveSimulation.Start(Request with{Debt=new(){ShortcutRate=.4,Tolerance=.05}},WorkArrivalMode.AlwaysAvailable);Until(live,80);var checkpoint=live.CreateCheckpoint("80");
        var series=LivePerformanceTrend.Project(live.Session,LiveTrendMetric.TechnicalDebtRatio,20,null);
        Assert.Equal(live.Session.Days.Select(d=>(double?)(100*d.Debt!.State.Ratio)),series.Points.Select(p=>p.Value));
        Assert.Equal(Json(series),Json(LivePerformanceTrend.Project(live.Session,LiveTrendMetric.TechnicalDebtRatio,100,null)));
        var comparisonChange=live.Session.Configuration with{Debt=live.Session.Configuration.Debt with{Repayment=.3}};live.Session.ApplyChanges(comparisonChange);Until(live,100);
        var comparison=LivePerformance.Compare(live.Session,live.Session.Changes.Last(),20);Assert.Equal(live.Session.Days[79].Debt!.State.Ratio,comparison.Before.EndDebtRatio);Assert.Equal(live.Session.Days[99].Debt!.Overhead,comparison.After.EndDebtOverhead);
        live.RestoreCheckpoint(checkpoint.Id);Assert.Equal(Json(series),Json(LivePerformanceTrend.Project(live.Session,LiveTrendMetric.TechnicalDebtRatio,20,null)));
    }
    [Fact]
    public void ConfigurationRoundTripsAllSixParametersAndComparisonRecognizesOnlyInputs()
    {
        var a=new ScenarioDefinition(Guid.NewGuid(),Request);var b=a with{Configuration=Request with{Debt=new(){ShortcutRate=.4,ShortcutEffortReduction=.2,Tolerance=.15,Repayment=.3,CreationFactor=1.2,ImpactFactor=.7}}};
        Assert.Equal(b,ExperimentJson.LoadScenario(ExperimentJson.SaveScenario(b)));
        var aa=ScenarioParameters.Describe(a.Configuration);var bb=ScenarioParameters.Describe(b.Configuration);
        Assert.Equal(6,aa.Keys.Count(k=>aa[k]!=bb[k]));Assert.DoesNotContain("Technical Debt Ratio",bb.Keys);
        var engine=new SimulationEngine().Run((b.Configuration with{SimulationDays=80,ArrivalMode=WorkArrivalMode.AlwaysAvailable}).ToScenario());
        var live=LiveSimulation.Start(b.Configuration,WorkArrivalMode.AlwaysAvailable);Until(live,80);Assert.Equal(Json(engine),Json(live.Session.GetResult()));
    }
}
