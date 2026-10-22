using Simulation.Application;
using Simulation.Core;
using System.Text.Json;
using Xunit;
namespace Simulation.Application.Tests;
public class KpiExplorerTests
{
    [Theory]
    [InlineData(10)] [InlineData(20)] [InlineData(40)]
    public void NewMetricsMatchRecordedCohortsAndPeriods(int window)
    {
        var live=LiveSimulation.Start(BaselineScenario.DefectsAndReworkExample() with { NumberOfWorkItems=60 },WorkArrivalMode.ContinuousArrival,2);
        for(var i=0;i<100;i++)live.Step();
        var session=live.Session; var state=JsonSerializer.Serialize(session.Capture());
        var days=session.Days.TakeLast(window).ToArray();var first=101-window;
        var releases=session.WorkItems.Where(w=>w.DoneDay>=first && w.DoneDay<=100).ToArray();
        var durations=releases.Select(w=>(double)(w.DoneDay!.Value-w.DevelopmentStartedDay!.Value)).Order().ToArray();
        var economics=TeamEconomics.From(days,releases.Length);
        var ids=days[^1].Items.Where(w=>w.State is not (WorkItemStatus.Backlog or WorkItemStatus.Done or WorkItemStatus.Released)).Select(w=>w.Id).ToHashSet();
        var wip=session.WorkItems.Where(w=>ids.Contains(w.Id)).ToArray();
        double? Expected(TeamKpi id)=>id switch {
            TeamKpi.CycleTimeP85=>durations.Length==0?null:durations[(int)Math.Ceiling(.85*durations.Length)-1],
            TeamKpi.DeliveryFrequency=>100d*releases.Select(w=>w.DoneDay).Distinct().Count()/window,
            TeamKpi.DeliveryVariability=>Math.Sqrt(days.Average(d=>Math.Pow(releases.Count(w=>w.DoneDay==d.Day+1)-(double)releases.Length/window,2))),
            TeamKpi.WipAge=>wip.Length==0?null:wip.Average(w=>(double)(100-w.DevelopmentStartedDay!.Value)),
            TeamKpi.ReviewQueueGrowth=>LivePerformance.Period(session,first,100).Review.Trend,
            TeamKpi.ReworkCost=>(double?)economics.ReworkCost,
            TeamKpi.ReworkProportion=>100*days.Sum(d=>d.UsedReworkDeveloperCapacity)/days.Sum(d=>d.UsedDeveloperCapacity),
            TeamKpi.DebtGrowth=>days.Sum(d=>d.Debt!.Created-d.Debt.Repaid),
            TeamKpi.DebtRepayment=>days.Sum(d=>d.Debt!.RepaymentCapacity),
            TeamKpi.CostPerWorkSize=>releases.Length==0?null:(double?)economics.TotalTeamCost/releases.Sum(w=>w.DevelopmentEffort),
            TeamKpi.UnlaunchedDevelopmentCost=>(double)LaunchEconomics.Calculate(session.Days,session.ProductDelivery).Current.Unlaunched,
            TeamKpi.WorkVolumePerSek=>releases.Sum(w=>w.DevelopmentEffort)/(double)economics.TotalTeamCost!,
            _=>throw new Exception()};
        foreach(var id in Enum.GetValues<TeamKpi>().Where(id=>id>TeamKpi.TechnicalDebt))
        {
            var observed=Assert.Single(KpiDashboard.Project(session,new(window,[id]),120));
            Assert.Equal(Expected(id),observed.Value);Assert.Equal(observed.Value,observed.Trend.Points[^1].Value);
            Assert.Equal(Enumerable.Range(1,100),observed.Trend.Points.Select(p=>p.Day));
        }
        Assert.Equal(state,JsonSerializer.Serialize(session.Capture()));
    }
    [Fact]
    public void MissingHistoryAndZeroDenominatorsRemainUnavailable()
    {
        var live=LiveSimulation.Start(new(){NumberOfWorkItems=0,DeveloperCount=0,TesterCount=0},WorkArrivalMode.FixedBacklog);live.Step();
        foreach(var id in new[]{TeamKpi.CycleTimeP85,TeamKpi.WipAge,TeamKpi.ReviewQueueGrowth,TeamKpi.ReworkProportion,TeamKpi.CostPerWorkSize,TeamKpi.WorkVolumePerSek})
            Assert.Null(KpiDashboard.Project(live.Session,new(20,[id])).Single().Value);
        var state=live.Session.Capture();var old=SimulationSession.Restore(state with{Days=state.Days.Select(d=>d with{Economics=null,Debt=null}).ToArray()});
        foreach(var id in new[]{TeamKpi.ReworkCost,TeamKpi.DebtGrowth,TeamKpi.DebtRepayment,TeamKpi.CostPerWorkSize,TeamKpi.WorkVolumePerSek})
            Assert.Null(KpiDashboard.Project(old,new(20,[id])).Single().Value);
        Assert.Equal(0,KpiDashboard.Project(live.Session,new(20,[TeamKpi.DeliveryFrequency])).Single().Value);
    }
    [Fact]
    public void HistoricalWipAgeDoesNotUseFutureCompletions()
    {
        var live=LiveSimulation.Start(new(){NumberOfWorkItems=20},WorkArrivalMode.FixedBacklog);
        for(var i=0;i<10;i++)live.Step();var early=KpiDashboard.Project(live.Session,new(10,[TeamKpi.WipAge]),120).Single().Value;
        for(var i=0;i<30;i++)live.Step();
        Assert.Equal(early,KpiDashboard.Project(live.Session,new(10,[TeamKpi.WipAge]),120).Single().Trend.Points.Single(p=>p.Day==10).Value);
    }
    [Fact]
    public void DebtRepaymentCanProduceNegativeGrowthAndMissingSampleIsNotBridged()
    {
        var live=LiveSimulation.Start(new(){NumberOfWorkItems=0},WorkArrivalMode.FixedBacklog);
        for(int i=0;i<3;i++)live.Step();
        var state=live.Session.Capture();
        var days=state.Days.Select((d,i)=>d with { Debt=i==1 ? null : new DebtObservation(new(10),.1,0,Created:1,Repaid:3,RepaymentCapacity:2) }).ToArray();
        var session=SimulationSession.Restore(state with {Days=days});
        var card=KpiDashboard.Project(session,new(10,[TeamKpi.DebtGrowth]),120).Single();
        Assert.Equal(-2,card.Trend.Points[0].Value);Assert.Null(card.Trend.Points[1].Value);Assert.Null(card.Value);
        days=state.Days.Select(d=>d with{Debt=new DebtObservation(new(10),.1,0,Created:1,Repaid:3,RepaymentCapacity:2)}).ToArray();
        card=KpiDashboard.Project(SimulationSession.Restore(state with{Days=days}),new(10,[TeamKpi.DebtGrowth])).Single();
        Assert.Equal(-6,card.Value);Assert.Equal(3,card.AvailableDays);Assert.Null(card.Previous);
    }
}
