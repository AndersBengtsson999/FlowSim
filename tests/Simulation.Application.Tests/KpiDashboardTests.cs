using System.Text.Json;
using System.Text.Json.Nodes;
using Simulation.Application;
using Simulation.Core;
using Simulation.Infrastructure;
using Xunit;
namespace Simulation.Application.Tests;
public sealed class KpiDashboardTests
{
    private static LiveSimulation At(int day)
    {
        var live=LiveSimulation.Start(BaselineScenario.DefectsAndReworkExample() with{NumberOfWorkItems=30},WorkArrivalMode.ContinuousArrival,1);
        for(int i=0;i<day;i++)live.Step();return live;
    }
    [Fact]
    public void DefaultSelectionAndNormalizationAreBounded()
    {
        var settings=new KpiDashboardSettings().Normalize();
        Assert.Equal(20,settings.Window);Assert.True(settings.Visible);
        Assert.Equal(new[]{TeamKpi.ReleasedThroughput,TeamKpi.CycleTime,TeamKpi.CostPerDelivery,TeamKpi.ReworkEffort},settings.Selected);
        var invalid=new KpiDashboardSettings(25,[TeamKpi.CycleTime,TeamKpi.CycleTime,(TeamKpi)999,TeamKpi.TotalWip,TeamKpi.TestingQueue,TeamKpi.TotalTeamCost,TeamKpi.TechnicalDebt]).Normalize();
        Assert.Equal(20,invalid.Window);Assert.Equal(4,invalid.Selected!.Count);Assert.Equal(4,invalid.Selected.Distinct().Count());
        Assert.Empty(new KpiDashboardSettings(10,[]).Normalize().Selected!);
    }
    [Theory]
    [InlineData(10)] [InlineData(20)] [InlineData(40)]
    public void MatchedWindowsAndEveryMetricUseAuthoritativeObservations(int window)
    {
        var live=At(2*window);var before=JsonSerializer.Serialize(live.Session.Capture());
        var current=LivePerformance.Period(live.Session,window+1,2*window);
        var previous=LivePerformance.Period(live.Session,1,window);
        foreach(var id in Enum.GetValues<TeamKpi>())
        {
            var card=Assert.Single(KpiDashboard.Project(live.Session,new(window,[id])));
            Assert.Equal(window+1,card.FirstDay);Assert.Equal(2*window,card.LastDay);Assert.Equal(window,card.AvailableDays);
            if(card.Value is { } value) Assert.Equal(value,card.Trend.Points[^1].Value!.Value,9);
            else Assert.Null(card.Trend.Points[^1].Value);
            double? Expected(PerformancePeriod p)=> id switch {
                TeamKpi.ReleasedThroughput=>p.Throughput,TeamKpi.CycleTime=>p.CycleTime,
                TeamKpi.CostPerDelivery=>(double?)p.Economics.CostPerDelivery,TeamKpi.TotalTeamCost=>(double?)p.Economics.TotalTeamCost,
                TeamKpi.ReworkEffort=>live.Session.Days.Skip(p.FirstDay-1).Take(window).Sum(d=>d.UsedReworkDeveloperCapacity),
                TeamKpi.DeveloperUtilization=>p.DeveloperUtilization*100,TeamKpi.TesterUtilization=>p.TesterUtilization*100,
                TeamKpi.TotalWip=>live.Session.Days[p.LastDay-1].TotalWip,TeamKpi.DevelopmentQueue=>live.Session.Days[p.LastDay-1].BacklogCount,
                TeamKpi.ReviewQueue=>p.Review.Current,TeamKpi.TestingQueue=>p.Testing.Current,
                TeamKpi.TechnicalDebt=>live.Session.Days[p.LastDay-1].Debt?.State.Amount,_=>throw new Exception()};
            Assert.Equal(Expected(current),card.Value);Assert.Equal(Expected(previous),card.Previous);Assert.Equal(card.Value-card.Previous,card.Difference);
            Assert.Equal(2*window,card.Trend.Points[^1].Day);
        }
        Assert.Equal(before,JsonSerializer.Serialize(live.Session.Capture()));
    }
    [Fact]
    public void PartialHistoryIsLabelledAndNeverComparedWithShorterPrecedingPeriod()
    {
        var live=At(9);var cards=KpiDashboard.Project(live.Session,new(20));
        Assert.All(cards,c=>{Assert.Equal(9,c.AvailableDays);Assert.Equal(1,c.FirstDay);Assert.Null(c.Previous);Assert.Null(c.Difference);});
        while(live.Session.CurrentDay<39)live.Step();Assert.All(KpiDashboard.Project(live.Session,new()),c=>Assert.Null(c.Previous));
        Assert.All(KpiDashboard.Project(At(0).Session,new()),c=>Assert.Null(c.Value));
    }
    [Fact]
    public void MissingMoneyAndDebtHistoryAndUndefinedRatiosAreNotZero()
    {
        var live=LiveSimulation.Start(new(){DeveloperCount=0,TesterCount=0,NumberOfWorkItems=0},WorkArrivalMode.FixedBacklog);live.Step();
        foreach(var id in new[]{TeamKpi.CycleTime,TeamKpi.CostPerDelivery,TeamKpi.DeveloperUtilization,TeamKpi.TesterUtilization})
            Assert.Null(Assert.Single(KpiDashboard.Project(live.Session,new(20,[id]))).Value);
        var state=live.Session.Capture();var legacy=SimulationSession.Restore(state with{Days=state.Days.Select(d=>d with{Economics=null,Debt=null}).ToArray()});
        foreach(var id in new[]{TeamKpi.TotalTeamCost,TeamKpi.TechnicalDebt})
        {
            var card=Assert.Single(KpiDashboard.Project(legacy,new(20,[id])));Assert.Null(card.Value);Assert.Null(card.Trend.Points.Single().Value);
        }
        Assert.Equal(0,Assert.Single(KpiDashboard.Project(live.Session,new(20,[TeamKpi.ReworkEffort]))).Value);
    }
    [Fact]
    public void MonetaryRatioIsPeriodTotalOverReleasedItemsNotAverageDailyRatios()
    {
        var live=LiveSimulation.Start(new(){NumberOfWorkItems=50,DevelopmentEffort=1,TestingEffort=1,Release=new(ReleaseMode.Scheduled,2,10)},WorkArrivalMode.FixedBacklog);
        for(int i=0;i<40;i++)live.Step();
        var card=Assert.Single(KpiDashboard.Project(live.Session,new(20,[TeamKpi.CostPerDelivery])));
        Assert.Equal(575000d/4,card.Value);Assert.Equal(21,card.FirstDay);Assert.Equal(40,card.LastDay);
    }
    [Fact]
    public void SaveLoadPreservesPreferencesOutsideSimulationStateAndOldFilesDefaultSafely()
    {
        var live=At(12);var state=JsonSerializer.Serialize(live.Session.Capture());
        live.Dashboard=new(40,[TeamKpi.TotalWip,TeamKpi.TechnicalDebt],false);
        var saved=LiveSessionJson.Save(live);var loaded=LiveSessionJson.Load(saved);
        Assert.Equal(40,loaded.Dashboard.Window);Assert.False(loaded.Dashboard.Visible);Assert.Equal(live.Dashboard.Selected,loaded.Dashboard.Selected);
        Assert.Equal(state,JsonSerializer.Serialize(loaded.Session.Capture()));
        var old=JsonNode.Parse(saved)!;old.AsObject().Remove("Dashboard");var legacy=LiveSessionJson.Load(old.ToJsonString());
        Assert.Equal(KpiDashboardSettings.Defaults,legacy.Dashboard.Selected);Assert.Equal(20,legacy.Dashboard.Window);
    }
}
