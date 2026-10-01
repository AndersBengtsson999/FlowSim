using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.Infrastructure;
using Simulation.UI.ViewModels;
using Xunit;

namespace Simulation.UI.Tests;

public sealed class CompactLiveLayoutTests
{
    [Fact]
    public void SetupStartsExpandedCollapsesOnStartAndReturnsOnReset()
    {
        using var vm = new LiveViewModel();
        Assert.True(vm.SetupExpanded); Assert.Equal("Setup / Configuration", vm.ConfigurationSummary);
        vm.Start(); vm.Pause(); Assert.False(vm.SetupExpanded);
        vm.SetupExpanded = true; Assert.True(vm.SetupExpanded); Assert.True(vm.HasSession);
        vm.Reset(); Assert.True(vm.SetupExpanded); Assert.True(vm.SetupVisible);
    }

    [Fact]
    public async Task DisclosurePreservesSessionSelectionsCheckpointsAndStatus()
    {
        using var vm = new LiveViewModel(); vm.WorkSupply = "Always available"; vm.Start(); vm.Pause();
        vm.TargetDay = "100"; await vm.RunToDayAsync(); vm.CheckpointCommand.Execute(null);
        vm.BeginChange(); vm.Draft.DeveloperAvailability = "85"; vm.ApplyChanges();
        vm.TrendMetric = vm.TrendMetrics.Single(m => m.Metric == LiveTrendMetric.AvailableDevelopers);
        vm.TrendRange = "Full Session";
        var state = JsonSerializer.Serialize(vm.Live!.Capture());
        var delivery = vm.StatusDelivery; var capacity = vm.StatusCapacity; var queues = vm.StatusQueues; var change = vm.LatestIntervention;
        vm.SetupExpanded = true; vm.SetupExpanded = false;
        Assert.Equal(state, JsonSerializer.Serialize(vm.Live.Capture()));
        Assert.Equal(delivery, vm.StatusDelivery); Assert.Equal(capacity, vm.StatusCapacity); Assert.Equal(queues, vm.StatusQueues); Assert.Equal(change, vm.LatestIntervention);
        Assert.Equal(LiveTrendMetric.AvailableDevelopers, vm.TrendMetric.Metric); Assert.Equal("Full Session", vm.TrendRange);
        Assert.Single(vm.Checkpoints); Assert.True(vm.ChangeCommand.CanExecute(null)); Assert.True(vm.StepCommand.CanExecute(null));
    }

    [Fact]
    public void ConfigurationSummaryUsesCurrentSessionAndSurvivesLoading()
    {
        var live = LiveSimulation.Start(LiveSimulation.Demo with { DeveloperAvailability = .85, TesterAvailability = .75, DevelopmentWipLimit = 3 }, WorkArrivalMode.AlwaysAvailable);
        using var vm = new LiveViewModel(); vm.Load(LiveSessionJson.Load(LiveSessionJson.Save(live)));
        Assert.False(vm.SetupExpanded); Assert.Contains("5 Dev · 2 Test", vm.ConfigurationSummary);
        Assert.Contains("WIP 3/3/3", vm.ConfigurationSummary); Assert.Contains("Always available", vm.ConfigurationSummary);
        Assert.Contains("85", vm.ConfigurationSummary); Assert.Contains("75", vm.ConfigurationSummary);
        vm.BeginChange(); vm.Draft.NumberOfDevelopers = "4"; vm.ApplyChanges();
        Assert.Contains("4 Dev", vm.ConfigurationSummary); Assert.Contains("Day 0", vm.LatestIntervention);
        Assert.Contains("next day", vm.ConfigurationDetails);
    }
}
