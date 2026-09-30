using System.Text.Json;
using Simulation.Application;
using Simulation.UI.Controls;
using Simulation.UI.ViewModels;
using Xunit;

namespace Simulation.UI.Tests;

public sealed class LiveTrendViewModelTests
{
    [Fact]
    public void LiveUpdatesSelectorsResetAndCheckpointRestoreRefreshTheSeries()
    {
        using var vm = new LiveViewModel(); var notifications = 0;
        vm.PropertyChanged += (_, _) => notifications++;
        vm.Start(); vm.Pause(); Assert.Empty(vm.Trend.Points);
        var options = vm.TrendMetrics;
        for (var i = 0; i < 25; i++) vm.Step();
        Assert.Same(options, vm.TrendMetrics);
        Assert.Equal(20, vm.Trend.Points.Count); Assert.Equal(25, vm.Trend.Points[^1].Day);
        vm.TrendRange = "Last 10 days"; Assert.Equal(10, vm.Trend.Points.Count);
        vm.TrendRange = "Full Session"; Assert.Equal(25, vm.Trend.Points.Count);
        var before = JsonSerializer.Serialize(vm.Live!.Session.Capture());
        foreach (var metric in vm.TrendMetrics)
        {
            vm.TrendMetric = metric;
            Assert.Equal(JsonSerializer.Serialize(LivePerformanceTrend.Project(vm.Live.Session, metric.Metric, vm.RollingWindow, null)), JsonSerializer.Serialize(vm.Trend));
        }
        Assert.Equal(before, JsonSerializer.Serialize(vm.Live.Session.Capture()));
        vm.RollingWindow = 10; Assert.Contains("Daily", vm.TrendDescription);
        vm.TrendMetric = vm.TrendMetrics[0]; Assert.Contains("Rolling 10", vm.TrendDescription);
        vm.CheckpointCommand.Execute(null); vm.SelectedCheckpoint = vm.Checkpoints.Single();
        var checkpoint = JsonSerializer.Serialize(vm.Trend);
        vm.BeginChange(); vm.Draft.DevelopmentWipLimit = "2"; vm.ChangeLabel = "WIP change"; vm.ApplyChanges();
        Assert.Equal(25, Assert.Single(vm.Trend.Interventions).Day); Assert.Equal(25, vm.SelectedTrendInterventionDay);
        vm.Resume(); vm.Tick(); vm.Pause(); Assert.Equal(26, vm.Trend.Points[^1].Day);
        vm.Tick(); Assert.Equal(26, vm.Trend.Points[^1].Day);
        vm.RestoreCommand.Execute(null); Assert.Equal(checkpoint, JsonSerializer.Serialize(vm.Trend));
        vm.Reset(); Assert.Empty(vm.Trend.Points); Assert.Empty(vm.Trend.Interventions);
        vm.Start(); vm.Pause(); vm.Step(); Assert.Single(vm.Trend.Points);
        Assert.True(notifications > 25);
    }
    [Fact]
    public void TooltipUsesUnderlyingValueAndMissingCycleTimesStayMissing()
    {
        var chart = new LivePerformanceTrendChart { Unit = "effort units / day", Series = new([new(1, 1.5), new(2, null)], []) };
        Assert.Contains(1.5.ToString("G17"), chart.DescribeAtDay(1));
        Assert.Contains("Day 1", chart.DescribeAtDay(1)); Assert.Contains("Unavailable", chart.DescribeAtDay(2));
    }
}
