using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.Infrastructure;
using Xunit;

namespace Simulation.Application.Tests;

public sealed class SystemCostTests
{
    static LiveSimulation Create(SimulationRequest? request = null) => LiveSimulation.Start(request ?? new() {
        DeveloperCount=1, TesterCount=1, DevelopmentWipLimit=1, DevelopmentEffort=5, CodeReviewEffort=1, TestingEffort=2
    }, WorkArrivalMode.AlwaysAvailable);
    static void Until(LiveSimulation live, int day) { while(live.Session.CurrentDay<day) Assert.True(live.Step()); }
    static void Near(double expected, double? actual) { Assert.NotNull(actual); Assert.InRange(Math.Abs(expected-actual.Value),0,1e-8); }

    [Theory]
    [InlineData(0,8)] [InlineData(20,9)]
    public void ExactPeriodNumeratorIncludesRepaymentWithoutChangingDelivery(double repayment,double expected)
    {
        var live=Create(); Until(live,150); var state=live.Session.Capture();
        var done=state.WorkItems.Where(w=>w.DoneDay is not null).Take(20).ToArray(); Assert.Equal(20,done.Length);
        var day=state.Days[0] with { ConsumedCapacity=new(100,20,40), UsedReworkDeveloperCapacity=0,
            Debt=new(new(),.1,0,0,0,repayment) };
        var session=SimulationSession.Restore(state with { Days=new[]{day}.Concat(state.Days.Skip(1).Select(d=>d with {
            ConsumedCapacity=new(0,0,0), UsedReworkDeveloperCapacity=0, Debt=new(new(),.1,0) })).ToArray() });
        var p=LivePerformance.Period(session,1,done[^1].DoneDay!.Value);
        Assert.Equal(20,p.Completed); Near(expected,p.SystemCostPerDoneItem); Near(8,p.DeliveryCostPerDoneItem);
        Near(160+repayment,p.ConsumedSystemCapacity!.Total);
    }

    [Fact]
    public void LifecycleAndUnfinishedWorkUseDifferentBoundaries()
    {
        var live=Create(); Until(live,30); var done=live.Session.WorkItems.First(w=>w.DoneDay.HasValue).DoneDay!.Value;
        var p=LivePerformance.Period(live.Session,done,done);
        Near(8,p.DeliveryCostPerDoneItem); Assert.True(p.SystemCostPerDoneItem<8);
        var d=live.Session.Days[done-1]; Near(d.UsedDeveloperCapacity+d.UsedTesterCapacity,p.ConsumedSystemCapacity!.Total);
        Assert.Contains(d.Items,w=>w.State!=WorkItemStatus.Done && w.DevelopmentWork>0);
        Assert.Equal(1,p.Completed);
        var empty=LivePerformance.Period(live.Session,1,1);
        Assert.True(empty.ConsumedSystemCapacity!.Total>0); Assert.Null(empty.SystemCostPerDoneItem);
    }

    [Fact]
    public void RawCapacityIncludesCollaborationReworkAndRepayment()
    {
        var live=Create(new() { DeveloperCount=5, TesterCount=2, DevelopmentWipLimit=2,
            Productivity=new(2,1.3,1.5), Debt=new(){ShortcutRate=.5,Repayment=.25},
            Quality=new(){Enabled=true,TestingDefectProbability=.4} }); Until(live,150);
        var p=LivePerformance.Rolling(live.Session,50); var ds=live.Session.Days.TakeLast(50).ToArray(); var c=p.ConsumedSystemCapacity!;
        Assert.Contains(ds,d=>d.CollaborationDevelopmentCapacity>0); Assert.True(c.Rework>0); Assert.True(c.DebtRepayment>0);
        Near(ds.Sum(d=>d.UsedDeveloperCapacity+d.UsedTesterCapacity),c.Total);
        Near(c.Total/p.Completed,p.SystemCostPerDoneItem);
        Assert.True(Math.Abs(ds.Sum(d=>d.DevelopmentWork)-c.Development)>1);
        Near(live.Session.Days.Sum(d=>d.UsedDeveloperCapacity+d.UsedTesterCapacity),
            live.Session.WorkItems.Sum(w=>w.DeliveryCost.Total)+live.Session.Days.Sum(d=>d.UsedDebtRepaymentCapacity));
    }

    [Fact]
    public void GrowingQueueIncludesCapacitySpentOnFutureCompletions()
    {
        var runs=ModelValidation.Scenarios.Run(); var p=LivePerformance.Rolling(runs.Productivity.Session,50);
        Assert.True(p.Testing.Current>50); Assert.True(p.SystemCostPerDoneItem>p.DeliveryCostPerDoneItem);
        Near(runs.Productivity.Session.Days.TakeLast(50).Sum(d=>d.UsedDeveloperCapacity+d.UsedTesterCapacity)/p.Completed,p.SystemCostPerDoneItem);
    }

    [Fact]
    public void CheckpointSaveLoadAndMeasurementLeaveContinuationIdentical()
    {
        var live=Create(); Until(live,30); var checkpoint=live.CreateCheckpoint("System cost");
        var loaded=LiveSessionJson.Load(LiveSessionJson.Save(live)); var before=JsonSerializer.Serialize(live.Session.Capture());
        foreach(var window in new[]{10,20,50,100}) LivePerformanceTrend.Project(live.Session,LiveTrendMetric.SystemCost,window,null);
        Assert.Equal(before,JsonSerializer.Serialize(live.Session.Capture()));
        Until(live,90); Until(loaded,90);
        var expected=JsonSerializer.Serialize(live.Session.Capture());
        var trend=JsonSerializer.Serialize(LivePerformanceTrend.Project(live.Session,LiveTrendMetric.SystemCost,20,null));
        Assert.Equal(expected,JsonSerializer.Serialize(loaded.Session.Capture()));
        live.RestoreCheckpoint(checkpoint.Id); Until(live,90);
        Assert.Equal(expected,JsonSerializer.Serialize(live.Session.Capture()));
        Assert.Equal(trend,JsonSerializer.Serialize(LivePerformanceTrend.Project(live.Session,LiveTrendMetric.SystemCost,20,null)));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void MissingAuthoritativeHistoryIsUnavailableAndNewDaysRecover(bool missingDebt)
    {
        var live=Create(new(){Productivity=new(2,1,1)}); Until(live,30); var state=live.Session.Capture();
        var session=SimulationSession.Restore(state with { Days=state.Days.Select(d=>missingDebt?d with{Debt=null}:d with{ConsumedCapacity=null}).ToArray() });
        Assert.Null(LivePerformance.Rolling(session).SystemCostPerDoneItem);
        Assert.Null(LivePerformanceTrend.Project(session,LiveTrendMetric.SystemCost).Points[^1].Value);
        for(var i=0;i<40;i++)session.AdvanceOneDay();
        Assert.NotNull(LivePerformance.Rolling(session).SystemCostPerDoneItem);
        Assert.NotNull(LivePerformanceTrend.Project(session,LiveTrendMetric.SystemCost).Points[^1].Value);
    }

    [Fact]
    public void BeforeAfterUsesRecordedDayAndPartialObservedAfter()
    {
        var live=Create(); Until(live,100); live.Session.ApplyChanges(live.Session.Configuration with{Productivity=new(2,1,1)}); Until(live,110);
        var c=LivePerformance.Compare(live.Session,live.Session.Changes.Single(),20);
        Assert.Equal(81,c.Before.FirstDay); Assert.Equal(100,c.Before.LastDay);
        Assert.Equal(101,c.After.FirstDay); Assert.Equal(120,c.After.LastDay); Assert.Equal(10,c.After.AvailableDays);
        Near(LivePerformance.Period(live.Session,81,100).SystemCostPerDoneItem!.Value,c.Before.SystemCostPerDoneItem);
        Near(LivePerformance.Period(live.Session,101,110).SystemCostPerDoneItem!.Value,c.After.SystemCostPerDoneItem);
    }
}
