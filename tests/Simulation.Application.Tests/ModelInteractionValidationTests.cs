using System.Text.Json;
using ModelValidation;
using Simulation.Application;
using Simulation.Core;
using Xunit;
namespace Simulation.Application.Tests;

public sealed class ModelInteractionValidationTests
{
    private static readonly Lazy<Runs> Runs = new(Scenarios.Run);
    [Fact]
    public void FourScenariosMaintainDailyCapacityCostDebtAndWipConservation()
    {
        var r=Runs.Value;
        foreach(var session in new[]{r.Baseline.Session,r.Productivity.Session,r.DebtTimeline.Session})
        {
            var audit=Audit.Check(session);
            Assert.Equal(session.CurrentDay,audit.DaysChecked);
            Assert.True(audit.ItemObservationsChecked>session.CurrentDay);
        }
    }
    [Fact]
    public void DevelopmentProductivityLowersCostWhileTestingConstrainsDelivery()
    {
        var rows=Runs.Value.Observations;var baseline=rows[0];var faster=rows[1];
        Assert.Equal(Runs.Value.Baseline.Session.InitialConfiguration with{Productivity=new(2,1,1)},Runs.Value.Productivity.Session.InitialConfiguration);
        Assert.True(faster.DevelopmentWork/faster.DevelopmentCapacity>baseline.DevelopmentWork/baseline.DevelopmentCapacity);
        Assert.True(faster.CostBreakdown!.Development<baseline.CostBreakdown!.Development);
        Assert.Equal(baseline.AvailableDeveloperCapacity,faster.AvailableDeveloperCapacity);
        Assert.Equal(baseline.AvailableTesterCapacity,faster.AvailableTesterCapacity);
        Assert.Equal(1,faster.TesterUtilization);Assert.True(faster.AverageTestingQueue>baseline.AverageTestingQueue);
        Assert.True(faster.CycleTime>baseline.CycleTime);
        Assert.InRange(faster.ThroughputPerFiveDays,baseline.ThroughputPerFiveDays,baseline.ThroughputPerFiveDays*2-1e-9);
    }
    [Fact]
    public void StoppingShortcutsKeepsHistoricalStateAndLockedPlansWhileFutureStartsUseOverhead()
    {
        var r=Runs.Value;var before=r.AtStop;
        Assert.True(before.DebtState!.Ratio>before.Configuration.Debt.Tolerance);
        var clone=SimulationSession.Restore(before);
        clone.ApplyChanges(clone.Configuration with{Debt=clone.Configuration.Debt with{ShortcutRate=0}},"Stop shortcuts");
        var after=clone.Capture();
        Assert.Equal(JsonSerializer.Serialize(before.WorkItems),JsonSerializer.Serialize(after.WorkItems));
        Assert.Equal(JsonSerializer.Serialize(before.Days),JsonSerializer.Serialize(after.Days));
        Assert.Equal(before.DebtState,after.DebtState);Assert.Equal(before.ArrivalRandomState,after.ArrivalRandomState);
        Assert.Equal(JsonSerializer.Serialize(before.Days),JsonSerializer.Serialize(r.DebtTimeline.Session.Days.Take(Scenarios.StopDay).ToArray()));
        foreach(var active in before.WorkItems.Where(w=>w.State==WorkItemStatus.Development))
            Assert.Equal(active.DevelopmentPlan,r.DebtTimeline.Session.WorkItems.Single(w=>w.Id==active.Id).DevelopmentPlan);
        var newItems=r.DebtTimeline.Session.WorkItems.Where(w=>w.DevelopmentStartedDay>=Scenarios.StopDay&&w.DevelopmentStartedDay<Scenarios.RepaymentDay).ToArray();
        Assert.NotEmpty(newItems);Assert.All(newItems,w=>{
            Assert.NotNull(w.DevelopmentPlan);Assert.False(w.DevelopmentPlan!.IsShortcut);
            Assert.Equal(w.DevelopmentEffort*(1+w.DevelopmentPlan.Overhead),w.DevelopmentPlan.FinalEffort);
            Assert.True(w.DevelopmentPlan.Overhead>0);
        });
        var firstAfter=r.Observations[3];Assert.True(firstAfter.Debt>=r.Observations[2].Debt); // locked active shortcuts can still complete
        Assert.True(firstAfter.CostBreakdown!.Development>r.Observations[2].CostBreakdown!.Development);
        Assert.Equal(firstAfter.Debt,r.Observations[4].Debt); // no repayment; scope alone lowers ratio
    }
    [Fact]
    public void RepaymentCompetesForCapacityAndFractionalPayoffReturnsUnusedAllocation()
    {
        var r=Runs.Value;var before=r.Observations[4];var after=r.Observations[5];
        Assert.True(after.Debt<before.Debt);Assert.True(after.RepaymentCapacity>0);
        Assert.True(after.DevelopmentCapacity<before.DevelopmentCapacity);Assert.Equal(1,after.DeveloperUtilization);
        Assert.True(after.CostPerItem<before.CostPerItem);Assert.True(after.ThroughputPerFiveDays<before.ThroughputPerFiveDays);
        var payoff=r.DebtTimeline.Session.Days[r.DebtZeroDay-1];
        var available=payoff.AvailableDeveloperCapacity-payoff.UsedReviewCapacity-payoff.UsedReworkDeveloperCapacity;
        Assert.InRange(payoff.UsedDebtRepaymentCapacity,double.Epsilon,available*.25-1e-9);
        Assert.Equal(0,payoff.Debt!.State.Amount);
        Assert.True(payoff.UsedDevelopmentCapacity>available*.75); // unused reservation really used that day
        Assert.All(r.DebtTimeline.Session.Days.Skip(r.DebtZeroDay),d=>Assert.Equal(0,d.UsedDebtRepaymentCapacity));
        Assert.Equal(0,r.Observations[6].Debt);Assert.Equal(0,r.Observations[6].RepaymentCapacity);
    }
    [Fact]
    public void CompleteRepeatedDebtAndRepaymentTimelineIsExactlyDeterministic()
    {
        var repeat=Scenarios.Run();
        Assert.Equal(Audit.Fingerprint(Runs.Value.Baseline.Session),Audit.Fingerprint(repeat.Baseline.Session));
        Assert.Equal(Audit.Fingerprint(Runs.Value.Productivity.Session),Audit.Fingerprint(repeat.Productivity.Session));
        Assert.Equal(Audit.Fingerprint(Runs.Value.DebtTimeline.Session),Audit.Fingerprint(repeat.DebtTimeline.Session));
        Assert.Equal(Runs.Value.Observations,repeat.Observations);
    }
    [Fact]
    public void EveryMetricProjectionIsReadOnlyAndCostTrendMatchesTheCompletionCohort()
    {
        var session=Runs.Value.DebtTimeline.Session;var before=Audit.Fingerprint(session);
        foreach(var metric in LivePerformanceTrend.Metrics)
        {
            var series=LivePerformanceTrend.Project(session,metric.Metric,Scenarios.Window,null);
            Assert.All(series.Points,p=>Assert.True(p.Value is null||double.IsFinite(p.Value.Value)));
        }
        var cost=LivePerformanceTrend.Project(session,LiveTrendMetric.DeliveryCost,Scenarios.Window,null);
        foreach(var point in cost.Points.Where(p=>p.Day%25==0||p.Day==session.CurrentDay))
        {
            var period=LivePerformance.Period(session,Math.Max(1,point.Day-Scenarios.Window+1),point.Day);
            if(period.DeliveryCostPerDoneItem is null)Assert.Null(point.Value);
            else Assert.InRange(Math.Abs(period.DeliveryCostPerDoneItem.Value-point.Value!.Value),0,1e-8);
        }
        Assert.Equal(before,Audit.Fingerprint(session));
    }
}
