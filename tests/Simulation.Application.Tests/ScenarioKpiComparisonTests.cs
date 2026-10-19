using Simulation.Application;
using Simulation.Core;
using System.Text.Json;
using Xunit;
namespace Simulation.Application.Tests;
public class ScenarioKpiComparisonTests
{
    [Theory]
    [InlineData(10)] [InlineData(20)] [InlineData(40)]
    public async Task SameCheckpointPrefixAggregationAndBaselineIsolation(int window)
    {
        var live=LiveSimulation.Start(LiveSimulation.Demo);for(int i=0;i<30;i++)live.Step();
        var cp=live.CreateCheckpoint("KPI experiment");var config=live.Session.Configuration;
        live.Session.ApplyChanges(config with{Team=config.Team with{DeveloperCount=config.Team.DeveloperCount+3}});
        var experiment=new GuidedInterventionExperiment(cp,live.Session,window);
        var state=JsonSerializer.Serialize(live.Capture());await experiment.PrepareBaselineAsync();
        Assert.Equal(state,JsonSerializer.Serialize(live.Capture()));
        Assert.NotNull(experiment.ObserveKpis(live.Session,TeamKpi.CycleTime).Unavailable);
        var baseline=SimulationSession.Restore(cp.State);
        for(int i=1;i<=window;i++)
        {
            live.Step();baseline.AdvanceOneDay();
            var result=experiment.ObserveKpis(live.Session,TeamKpi.CostPerDelivery);
            Assert.Equal(31,result.FirstDay);Assert.Equal(30+window,result.EndDay);Assert.Equal(i,result.ObservedDays);
            Assert.Equal(Enumerable.Range(31,i),result.Points.Select(p=>p.Day));
            var bp=LivePerformance.Period(baseline,31,30+i);var xp=LivePerformance.Period(live.Session,31,30+i);
            Assert.Equal((double?)bp.Economics.CostPerDelivery,result.Points[^1].Baseline);
            Assert.Equal((double?)xp.Economics.CostPerDelivery,result.Points[^1].Intervention);
            foreach(var row in result.Rows)
            {
                Assert.Equal(KpiDashboard.PeriodValue(row.Definition.Id,baseline,31,30+i),row.Baseline);
                Assert.Equal(KpiDashboard.PeriodValue(row.Definition.Id,live.Session,31,30+i),row.Intervention);
            }
            Assert.Equal((double?)xp.Economics.TotalTeamCost,result.Rows.Single(r=>r.Definition.Id==TeamKpi.TotalTeamCost).Intervention);
        }
        var completed=JsonSerializer.Serialize(experiment.ObserveKpis(live.Session,TeamKpi.ReleasedThroughput));
        live.Step();Assert.Equal(completed,JsonSerializer.Serialize(experiment.ObserveKpis(live.Session,TeamKpi.ReleasedThroughput)));
        var repeat=new GuidedInterventionExperiment(cp,SimulationSession.Restore(cp.State),window);
        await repeat.PrepareBaselineAsync(); // deterministic no-op comparison supported by B5.1
        Assert.True(repeat.BaselineReady);
    }
    [Fact]
    public async Task UndefinedValuesAndInvalidationNeverLeaveStaleSeries()
    {
        var live=LiveSimulation.Start(new(){NumberOfWorkItems=0,DeveloperCount=0,TesterCount=0});var cp=live.CreateCheckpoint("empty");
        var e=new GuidedInterventionExperiment(cp,live.Session,10);await e.PrepareBaselineAsync();live.Step();
        var result=e.ObserveKpis(live.Session,TeamKpi.CostPerDelivery);
        Assert.Null(result.Points.Single().Baseline);Assert.Null(result.Points.Single().Difference);
        Assert.Null(result.Rows.Single(r=>r.Definition.Id==TeamKpi.DeveloperUtilization).Baseline);
        live.Session.ApplyChanges(live.Session.Configuration with{Team=live.Session.Configuration.Team with{TesterCount=1}});
        result=e.ObserveKpis(live.Session,TeamKpi.CostPerDelivery);Assert.NotNull(result.Unavailable);Assert.Empty(result.Points);Assert.Empty(result.Rows);
    }
    [Fact]
    public void MultiVariableAndUnrelatedConfigurationAreRejected()
    {
        var live=LiveSimulation.Start(LiveSimulation.Demo);var cp=live.CreateCheckpoint("start");var c=live.Session.Configuration;
        Assert.False(GuidedInterventionExperiment.SingleStaffingChange(c,c with{Team=c.Team with{DeveloperCount=8,TesterCount=7}}));
        Assert.False(GuidedInterventionExperiment.SingleStaffingChange(c,c with{DevelopmentWipLimit=c.DevelopmentWipLimit+1}));
    }
}
