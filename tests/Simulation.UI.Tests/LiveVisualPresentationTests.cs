using System.Text.Json;
using Simulation.Core;
using Simulation.UI.ViewModels;
using Xunit;

namespace Simulation.UI.Tests;

public sealed class LiveVisualPresentationTests
{
    [Fact]
    public void WipIndicatorsUseExistingCurrentCountsAndLimitsWithoutClampingTheLabel()
    {
        using var vm = new LiveViewModel(); vm.Preset = "Baseline"; vm.Start(); vm.Pause(); vm.Step();
        vm.BeginChange(); vm.Draft.DevelopmentWipLimit = "3"; vm.ApplyChanges();
        var development = vm.Flow.Single(r => r.State == WorkItemStatus.Development);
        Assert.Equal(5, development.Count); Assert.Equal("5 / 3", development.WipText); Assert.Equal(3, development.WipMaximum);
        foreach (var row in vm.Flow)
            Assert.Equal(row.State is WorkItemStatus.Development or WorkItemStatus.CodeReview or WorkItemStatus.Testing or WorkItemStatus.Rework, row.HasWipLimit);
        Assert.Equal(vm.Live!.CurrentSnapshot.UsedDevelopmentCapacity, development.DevelopmentCapacityUsed);
        Assert.Equal(vm.Live.CurrentSnapshot.DevelopmentWork, development.EffectiveDevelopmentWork);
    }

    [Fact]
    public async Task GroupedStatusAndConfigurationAreReadOnlyViewsOfExistingObservations()
    {
        using var vm = new LiveViewModel(); vm.WorkSupply = "Always available"; vm.Setup.DeveloperAvailability = "85"; vm.Setup.TesterAvailability = "0";
        vm.Start(); vm.Pause(); vm.TargetDay = "100"; await vm.RunToDayAsync();
        var before = JsonSerializer.Serialize(vm.Live!.Capture());
        var a = vm.StatusPrimaryGroups; var b = vm.StatusSecondaryGroups;
        Assert.Equal("100", a.Single(g => g.Label == "Day").Value);
        Assert.Equal(vm.LiveStatus!.Done.ToString(), a.Single(g => g.Label == "Released").Value);
        Assert.Contains(LivePerformancePresentation.Percent(vm.LiveStatus.DeveloperUtilization), b.Single(g => g.Label == "Dev").Value);
        Assert.Contains("Unavailable", b.Single(g => g.Label == "Test").Value);
        Assert.Equal("Always available", b.Single(g => g.Label == "Work").Value);
        Assert.Equal(vm.ConfigurationSummary, vm.ConfigurationHeading + vm.ConfigurationValues);
        Assert.Equal(before, JsonSerializer.Serialize(vm.Live.Capture()));
    }

    [Fact]
    public void ToolbarPlaybackLabelTracksExistingLifecycleAndKeepsUsefulMessages()
    {
        using var vm = new LiveViewModel(); Assert.Equal("Ready", vm.PlaybackState);
        vm.Start(); Assert.Equal("Running", vm.PlaybackState); Assert.False(vm.HasStatusMessage);
        vm.Pause(); Assert.Equal("Paused", vm.PlaybackState); Assert.False(vm.HasStatusMessage);
        vm.NotifyStatus("A file could not be opened."); Assert.True(vm.HasStatusMessage);
        vm.StopCommand.Execute(null); Assert.Equal("Stopped", vm.PlaybackState); Assert.True(vm.HasStatusMessage);
        vm.Reset(); Assert.Equal("Ready", vm.PlaybackState);
    }
}
