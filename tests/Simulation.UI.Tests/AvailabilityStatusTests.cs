using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.UI.ViewModels;
using Xunit;

namespace Simulation.UI.Tests;

public sealed class AvailabilityStatusTests
{
    [Fact]
    public async Task StatusTracksHistoryInterventionAndNavigationWithoutReset()
    {
        var main = new MainWindowViewModel(); using var vm = main.Simple.Live;
        vm.WorkSupply = "Always available"; Assert.False(vm.ShowArrivalRate);
        vm.Setup.DeveloperAvailability = "85"; vm.Setup.TesterAvailability = "75";
        vm.Start(); vm.Pause(); vm.TargetDay = "100"; await vm.RunToDayAsync();
        Assert.Equal(100, vm.Day); var d = vm.Live!.CurrentSnapshot; var s = vm.LiveStatus!;
        Assert.Equal(d.DoneCount, s.Done); Assert.Equal(d.TotalWip, s.Wip);
        Assert.Equal(d.WaitingForCodeReviewCount, s.ReviewQueue); Assert.Equal(d.WaitingForTestingCount, s.TestingQueue);
        Assert.Equal(d.UsedDeveloperCapacity, s.DeveloperUsed); Assert.Equal(4.25, s.DeveloperAvailable); Assert.Equal(1.5, s.TesterAvailable);
        Assert.Contains("Always available", vm.StatusQueues); Assert.Contains("Day 100", vm.StatusDelivery); Assert.Empty(vm.LatestIntervention);
        var trend = LivePerformancePresentation.Trend(vm.Performance!.WipTrend);
        Assert.Contains(trend.StartsWith("Rising") ? "↑" : trend.StartsWith("Falling") ? "↓" : "→", vm.StatusDelivery);
        vm.BeginChange(); vm.Draft.DeveloperAvailability = "80"; vm.DraftSupply = "Continuous"; vm.DraftArrivalRate = "0.8"; vm.ApplyChanges();
        Assert.Contains("Day 100", vm.LatestIntervention); Assert.Contains("effective Day 101", vm.LatestIntervention);
        Assert.Equal(4.25, vm.LiveStatus!.DeveloperAvailable);
        vm.TargetDay = "120"; await vm.RunToDayAsync(); Assert.Equal(4, vm.LiveStatus!.DeveloperAvailable);
        vm.TrendRange = "Full Session"; vm.TrendMetric = vm.TrendMetrics.Single(m => m.Metric == LiveTrendMetric.AvailableDevelopers);
        Assert.Equal(4.25, vm.Trend.Points[99].Value); Assert.Equal(4, vm.Trend.Points[100].Value); Assert.Equal(100, vm.Trend.Interventions.Single().Day);
        Assert.Contains("81–100", vm.BeforePeriod); Assert.Contains("101–120", vm.AfterPeriod);
        var state = JsonSerializer.Serialize(vm.Live.Capture());
        main.Simple.OpenAnalyzeCommand.Execute(null); main.Simple.AdvancedCommand.Execute(null); main.Simple.OpenLiveCommand.Execute(null);
        Assert.Equal(state, JsonSerializer.Serialize(vm.Live.Capture())); Assert.Equal(120, vm.LiveStatus.Day);
    }

    [Fact]
    public void SupplyAndAvailabilityEditorsRoundTripAndDefaultToFullAvailability()
    {
        using var vm = new LiveViewModel(); Assert.Equal("100", vm.Setup.DeveloperAvailability); Assert.Equal("100", vm.Setup.TesterAvailability);
        Assert.True(vm.ShowArrivalRate); vm.WorkSupply = "Always available"; Assert.False(vm.ShowArrivalRate);
        vm.FixedBacklog = true; Assert.False(vm.SupplySelectorEnabled); Assert.False(vm.ShowArrivalRate);
        var editor = new MainWindowViewModel(); var request = LiveSimulation.Demo with { DeveloperAvailability = .85, TesterAvailability = .75, ArrivalMode = WorkArrivalMode.AlwaysAvailable };
        editor.LoadConfiguration(request); var restored = editor.CaptureSetup();
        Assert.Equal(request.DeveloperAvailability, restored.DeveloperAvailability); Assert.Equal(request.TesterAvailability, restored.TesterAvailability);
        Assert.Equal(request.ArrivalMode, restored.ArrivalMode);
    }
}
