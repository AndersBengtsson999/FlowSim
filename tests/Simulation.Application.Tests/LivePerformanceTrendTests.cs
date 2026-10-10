using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Xunit;

namespace Simulation.Application.Tests;

public sealed class LivePerformanceTrendTests
{
    private static LiveSimulation Create(int days = 140)
    {
        var live = LiveSimulation.Start(new() { NumberOfWorkItems = 100, DevelopmentEffort = 10,
            CodeReviewEffort = .2, TestingEffort = .2, DevelopmentWipLimit = 3 }, WorkArrivalMode.FixedBacklog);
        for (var i = 0; i < days; i++)
        {
            if (i == 100) live.Session.ApplyChanges(live.Session.Configuration with { Team = new(3, 4) }, "Capacity change");
            live.Step();
        }
        return live;
    }
    [Theory]
    [InlineData(10)] [InlineData(20)] [InlineData(50)] [InlineData(100)] [InlineData(null)]
    public void RangesUseInclusiveDisplayDaysAndRetainLookback(int? range)
    {
        var live = Create(); var points = LivePerformanceTrend.Project(live.Session, LiveTrendMetric.Throughput, 20, range).Points;
        Assert.Equal(range ?? 140, points.Count); Assert.Equal(141 - (range ?? 140), points[0].Day); Assert.Equal(140, points[^1].Day);
        Assert.Equal(LivePerformance.Period(live.Session, Math.Max(1, points[0].Day - 19), points[0].Day).Throughput, points[0].Value);
    }
    [Theory]
    [InlineData(10)] [InlineData(20)] [InlineData(50)] [InlineData(100)]
    public void EveryRollingPointMatchesAuthoritativeStep13Period(int window)
    {
        var live = Create();
        foreach (var metric in LivePerformanceTrend.Metrics.Where(m => m.Rolling))
        foreach (var point in LivePerformanceTrend.Project(live.Session, metric.Metric, window, null).Points)
        {
            var p = LivePerformance.Period(live.Session, Math.Max(1, point.Day - window + 1), point.Day);
            var expected = metric.Metric switch {
                LiveTrendMetric.Throughput => p.Throughput, LiveTrendMetric.CycleTime => p.CycleTime,
                LiveTrendMetric.DeliveryCost => p.DeliveryCostPerDoneItem,
                LiveTrendMetric.CompletionRate => p.CompletionRate, LiveTrendMetric.DevelopmentCycleTime => p.DevelopmentCycleTime, LiveTrendMetric.ReleaseWaitTime => p.ReleaseWaitTime,
                LiveTrendMetric.SystemCost => p.SystemCostPerDoneItem,
                LiveTrendMetric.AverageWip => p.AverageWip, LiveTrendMetric.DeveloperUtilization => 100 * p.DeveloperUtilization,
                _ => 100 * p.TesterUtilization };
            if (expected is null) Assert.Null(point.Value); else Assert.InRange(Math.Abs(expected.Value - point.Value!.Value), 0, 1e-9);
        }
    }
    [Fact]
    public void DailySeriesAndCollaborationMatchHistoryWithoutMutatingIt()
    {
        var live = Create(); var before = JsonSerializer.Serialize(live.Session.Capture());
        foreach (var metric in LivePerformanceTrend.Metrics.Where(m => !m.Rolling))
        foreach (var p in LivePerformanceTrend.Project(live.Session, metric.Metric, range: null).Points)
        {
            var d = live.Session.Days[p.Day - 1];
            var expected = metric.Metric switch { LiveTrendMetric.WaitingForDependency => d.WaitingForDependencyCount, LiveTrendMetric.ReadyForRelease => d.ReadyForReleaseCount, LiveTrendMetric.SpecialistWorkWaiting => d.SpecialistWorkWaiting, LiveTrendMetric.ReviewQueue => d.WaitingForCodeReviewCount,
                LiveTrendMetric.TestingQueue => d.WaitingForTestingCount, LiveTrendMetric.ReworkQueue => d.WaitingForReworkCount,
                LiveTrendMetric.DevelopmentCapacity => d.UsedDevelopmentCapacity, LiveTrendMetric.AvailableDevelopers => d.AvailableDeveloperCapacity, LiveTrendMetric.AvailableTesters => d.AvailableTesterCapacity,
                LiveTrendMetric.TechnicalDebtRatio => 100 * (d.Debt?.State.Ratio ?? 0),
                LiveTrendMetric.TechnicalDebt => d.Debt?.State.Amount ?? 0,
                LiveTrendMetric.DebtOverhead => 100 * (d.Debt?.Overhead ?? 0), _ => d.DevelopmentWork };
            Assert.Equal(expected, p.Value);
        }
        Assert.Equal(5, LivePerformanceTrend.Project(live.Session, LiveTrendMetric.DevelopmentCapacity, range: null).Points[0].Value);
        Assert.Equal(4, LivePerformanceTrend.Project(live.Session, LiveTrendMetric.DevelopmentWork, range: null).Points[0].Value);
        Assert.Equal(before, JsonSerializer.Serialize(live.Session.Capture()));
    }
    [Fact]
    public void PartialEmptyAndUndefinedHistoryAreNotFabricated()
    {
        var live = Create(0); Assert.Empty(LivePerformanceTrend.Project(live.Session, LiveTrendMetric.CycleTime).Points);
        for (var i = 0; i < 7; i++) live.Step();
        var points = LivePerformanceTrend.Project(live.Session, LiveTrendMetric.CycleTime).Points;
        Assert.Equal(7, points.Count); Assert.All(points, p => Assert.Null(p.Value));
        live.Session.ApplyChanges(live.Session.Configuration with { Team = new(0, 0) });
        for (var i = 0; i < 20; i++) live.Step();
        Assert.Null(LivePerformanceTrend.Project(live.Session, LiveTrendMetric.DeveloperUtilization).Points[^1].Value);
    }
    [Fact]
    public void MarkersRetainRecordedDayValuesLabelAndRestoreHistory()
    {
        var live = Create(100); var checkpoint = live.CreateCheckpoint("Before");
        live.Session.ApplyChanges(live.Session.Configuration with { DevelopmentWipLimit = 2 }, "WIP intervention");
        for (var i = 0; i < 40; i++) live.Step();
        var trend = LivePerformanceTrend.Project(live.Session, LiveTrendMetric.DevelopmentCapacity, range: 50);
        var marker = Assert.Single(trend.Interventions);
        Assert.Equal(100, marker.Day); Assert.Equal(3, marker.Before.DevelopmentWipLimit); Assert.Equal(2, marker.After.DevelopmentWipLimit);
        Assert.Equal("WIP intervention", marker.Label);
        Assert.Empty(LivePerformanceTrend.Project(live.Session, LiveTrendMetric.DevelopmentCapacity, range: 20).Interventions);
        live.RestoreCheckpoint(checkpoint.Id);
        var restored = LivePerformanceTrend.Project(live.Session, LiveTrendMetric.DevelopmentCapacity, range: null);
        Assert.Equal(100, restored.Points.Count); Assert.Empty(restored.Interventions);
        Assert.Equal(JsonSerializer.Serialize(LivePerformanceTrend.Project(Create(100).Session, LiveTrendMetric.DevelopmentCapacity, range: null)), JsonSerializer.Serialize(restored));
    }
}
