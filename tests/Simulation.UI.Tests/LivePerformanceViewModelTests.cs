using Simulation.Application;
using Simulation.UI.ViewModels;
using Xunit;

namespace Simulation.UI.Tests;

public sealed class LivePerformanceViewModelTests
{
    [Theory]
    [InlineData(.049999, "Stable")]
    [InlineData(-.049999, "Stable")]
    [InlineData(.05, "Rising")]
    [InlineData(-.05, "Falling")]
    [InlineData(.051, "Rising")]
    [InlineData(-.051, "Falling")]
    public void StableThresholdIsStrict(double slope, string label) => Assert.StartsWith(label, LivePerformancePresentation.Trend(slope));

    [Fact]
    public void MissingTrendsAreNotStableAndZeroCapacityIsExplicit()
    {
        using var vm = new LiveViewModel(); vm.Start(); vm.Pause();
        Assert.All(vm.Metrics, row => Assert.Equal("Unavailable", row.Value));
        Assert.All(vm.CapacityMetrics, row => Assert.Equal("Unavailable", row.Value));
        vm.Step(); vm.Step();
        Assert.Contains("Unavailable", vm.FlowMetrics.Single(r => r.Label == "WIP trend").Value);
        vm.Step(); Assert.DoesNotContain("Unavailable", vm.FlowMetrics.Single(r => r.Label == "WIP trend").Value);
        Assert.False(vm.ShowPerformanceQuality);
    }

    [Fact]
    public void DemoWorkflowRefreshesWindowsComparisonSelectionAndCheckpointRestore()
    {
        using var vm = new LiveViewModel(); vm.Start(); vm.Pause();
        for (var i = 0; i < 100; i++) vm.Step();
        Assert.Contains("Days 81–100", vm.WindowLabel);
        vm.CheckpointLabel = "Baseline day 100"; vm.CheckpointCommand.Execute(null);
        vm.SelectedCheckpoint = vm.Checkpoints.Single();
        vm.BeginChange(); vm.Draft.NumberOfTesters = "3"; vm.ChangeLabel = "Add tester"; vm.ApplyChanges();
        Assert.Equal("Add tester", vm.SelectedIntervention!.Label);
        var interventions = vm.Interventions;
        vm.SelectedIntervention = null; // Transient ComboBox deselection during ItemsSource refresh.
        vm.Pause();
        Assert.Same(interventions, vm.Interventions);
        Assert.Equal("Add tester", vm.SelectedIntervention!.Label);
        Assert.Contains("Days 81–100", vm.BeforePeriod); Assert.Contains("Days 101–120", vm.AfterPeriod);
        Assert.Contains("0 of 20", vm.AfterPeriod); Assert.Equal("", vm.Observations);
        Assert.All(vm.ComparisonRows, r => Assert.Equal("Unavailable", r.After));
        for (var i = 0; i < 12; i++) vm.Step();
        Assert.Same(interventions, vm.Interventions);
        Assert.Contains("12 of 20", vm.AfterPeriod); Assert.Contains("Incomplete", vm.ComparisonStatus);
        for (var i = 0; i < 8; i++) vm.Step();
        Assert.Contains("20 of 20", vm.AfterPeriod); Assert.StartsWith("Complete", vm.ComparisonStatus);
        Assert.EndsWith(" pp", vm.ComparisonRows.Single(r => r.Metric == "Tester utilization").Difference);
        Assert.Equal(2, vm.Observations.Split('\n').Length);
        Assert.Equal(9, vm.ComparisonRows.Count);
        vm.RollingWindow = 10;
        Assert.Equal(10, vm.Performance!.AvailableDays); Assert.Contains("Days 91–100", vm.BeforePeriod);
        Assert.Contains("Days 101–110", vm.AfterPeriod); Assert.Contains("Days 111–120", vm.WindowLabel);
        vm.RollingWindow = 50; Assert.Contains("20 of 50", vm.AfterPeriod);
        vm.RestoreCommand.Execute(null);
        Assert.Equal(100, vm.Day); Assert.Null(vm.Comparison); Assert.Empty(vm.ComparisonRows); Assert.Null(vm.SelectedIntervention);
        vm.Reset(); Assert.Null(vm.Performance); Assert.Empty(vm.FlowMetrics); Assert.Empty(vm.CapacityMetrics);
    }

    [Fact]
    public void QualityAndLoadedInterventionsAreVisibleWhenRelevant()
    {
        var live = LiveSimulation.Start(LiveSimulation.Demo with { Quality = new() { Enabled = true, CodeReviewDefectProbability = .5 } });
        for (var i = 0; i < 30; i++) live.Step();
        live.Session.ApplyChanges(live.Session.Configuration with { Team = new(5, 3) }, "Add tester");
        for (var i = 0; i < 20; i++) live.Step();
        using var vm = new LiveViewModel(); vm.Load(live);
        Assert.True(vm.ShowPerformanceQuality); Assert.NotNull(vm.SelectedIntervention);
        Assert.Contains(vm.FlowMetrics, r => r.Label == "Waiting for Rework");
        Assert.Contains(vm.ComparisonRows, r => r.Metric == "Defects");
        Assert.Contains(vm.ComparisonRows, r => r.Metric == "Rework Capacity");
        var first = vm.SelectedIntervention;
        live.Session.ApplyChanges(live.Session.Configuration with { Team = new(5, 4) }, "Another tester");
        vm.SelectedIntervention = live.Session.Changes[^1];
        Assert.Equal(0, vm.Comparison!.After.AvailableDays);
        vm.SelectedIntervention = first; Assert.Equal(20, vm.Comparison!.After.AvailableDays);
    }
}
