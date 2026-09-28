using System.Text.Json;
using Simulation.UI.ViewModels;
using Xunit;

namespace Simulation.UI.Tests;

public sealed class LiveViewModelTests
{
    private static string Result(LiveViewModel vm) => JsonSerializer.Serialize(vm.Live!.Session.GetResult());
    [Fact]
    public void PauseAndStepAreDistinctAndEditingPreventsAdvancement()
    {
        using var vm = new LiveViewModel(); vm.Start(); vm.Tick(); Assert.Equal(1, vm.Day);
        vm.Pause(); var result = Result(vm); vm.Tick(); Assert.Equal(result, Result(vm));
        vm.Step(); Assert.Equal(2, vm.Day);
        vm.Resume(); vm.BeginChange(); Assert.False(vm.IsRunning);
        vm.Tick(); vm.Step(); Assert.Equal(2, vm.Day);
        vm.Draft.NumberOfTesters = "3"; vm.ChangeLabel = "Add tester"; vm.ApplyChanges();
        Assert.False(vm.IsRunning); Assert.Equal(2, vm.Live!.Session.Changes.Single().Day);
        vm.Step(); Assert.Equal(3, vm.Day); Assert.Equal(3, vm.Live.Session.Days[^1].AvailableTesterCapacity);
    }
    [Theory]
    [InlineData(.5)] [InlineData(1)] [InlineData(2)] [InlineData(5)] [InlineData(10)]
    public void SpeedChangesWallClockOnly(double speed)
    {
        using var a = new LiveViewModel(); using var b = new LiveViewModel(); a.Speed = speed; a.Start(); b.Start();
        for (var i = 0; i < 100; i++) { a.Tick(); b.Tick(); }
        a.Pause(); b.Pause(); Assert.Equal(Result(a), Result(b));
    }
    [Fact]
    public void LimitPausesAndStopRequiresReset()
    {
        using var vm = new LiveViewModel { SafetyLimit = "2" }; vm.Start(); vm.Tick(); vm.Tick();
        Assert.False(vm.IsRunning); Assert.Equal("Live simulation safety limit reached.", vm.Status);
        vm.Step(); Assert.Equal(2, vm.Day);
        vm.SafetyLimit = "3"; vm.LimitCommand.Execute(null); vm.Step(); Assert.Equal(3, vm.Day);
        vm.StopCommand.Execute(null); Assert.False(vm.CanResume);
        vm.Reset(); Assert.True(vm.SetupVisible); Assert.Equal(0, vm.Day);
    }
    [Fact]
    public void InvalidEditDoesNotChangeSessionAndReworkLimitCanChangeWithDefectsOff()
    {
        using var vm = new LiveViewModel(); vm.Start(); vm.BeginChange();
        vm.Draft.NumberOfTesters = "-1"; vm.ApplyCommand.Execute(null);
        Assert.True(vm.IsEditing); Assert.Empty(vm.Live!.Session.Changes);
        vm.Draft.NumberOfTesters = "2"; vm.Draft.ReworkWipLimit = "2"; vm.ApplyCommand.Execute(null);
        Assert.False(vm.IsEditing); Assert.Equal(2, vm.Live.Session.Configuration.Quality.ReworkWipLimit);
    }
    [Fact]
    public void NavigatingAwayPausesLiveAndKeepsItsState()
    {
        var main = new MainWindowViewModel(); var simple = main.Simple;
        simple.OpenLiveCommand.Execute(null); Assert.True(simple.LiveVisible);
        simple.Live.Start(); simple.Live.Tick(); simple.OpenRunCommand.Execute(null);
        Assert.False(simple.Live.IsRunning); Assert.True(simple.RunVisible);
        simple.OpenLiveCommand.Execute(null); Assert.Equal(1, simple.Live.Day); simple.Live.Dispose();
    }
    [Fact]
    public void NoOpDoesNotCreateMarkerAndDisabledQualitySettingsAreRetained()
    {
        using var vm = new LiveViewModel(); vm.Start(); vm.BeginChange(); vm.ApplyChanges();
        Assert.Empty(vm.Changes); Assert.StartsWith("No parameters changed", vm.Status);
        vm.BeginChange(); vm.Draft.CodeReviewDefectProbability = "25"; vm.ApplyChanges();
        Assert.False(vm.Live!.Session.Configuration.Quality.Enabled);
        Assert.Equal(.25, vm.Live.Session.Configuration.Quality.CodeReviewDefectProbability);
        Assert.Single(vm.Changes);
        vm.Pause(); Assert.Equal("Paused.", vm.Status);
    }

    [Fact]
    public void FlowUsesSharedPresentationAndShowsLoweredLimitWithoutEviction()
    {
        using var vm = new LiveViewModel(); vm.Preset = "Baseline"; vm.Start(); vm.Tick(); vm.BeginChange();
        vm.Draft.DevelopmentWipLimit = "3"; vm.ApplyChanges();
        var row = vm.Flow.Single(r => r.Name == "Development"); Assert.Equal(5, row.Count); Assert.StartsWith("5 / 3", row.Kind);
        vm.SelectedFlow = vm.Flow.Single(r => r.Name == "Backlog"); Assert.Contains("items", vm.QueueDetail);
    }
}
