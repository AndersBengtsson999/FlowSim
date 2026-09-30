using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.Infrastructure;
using Simulation.UI.ViewModels;
using Xunit;

namespace Simulation.UI.Tests;

public sealed class LiveFirstTests
{
    [Fact]
    public void StartsInLiveAndAnalysisSubviewsPreserveSessionAndDrafts()
    {
        var main = new MainWindowViewModel(); var s = main.Simple;
        Assert.True(s.LiveVisible); Assert.False(s.HomeVisible); Assert.False(s.RunVisible);
        s.Live.Start(); s.Live.Tick();
        var state = JsonSerializer.Serialize(s.Live.Live!.Capture());
        s.OpenAnalyzeCommand.Execute(null); Assert.True(s.AnalyzeVisible); Assert.True(s.ChangesVisible);
        Assert.False(s.Live.IsRunning);
        s.Try.NumberOfTesters = "7";
        s.OpenExploreCommand.Execute(null); Assert.True(s.ExploreVisible); Assert.True(s.AnalyzeVisible);
        s.OpenLiveCommand.Execute(null); Assert.True(s.LiveVisible);
        s.OpenAnalyzeCommand.Execute(null); Assert.True(s.ExploreVisible);
        s.OpenChangesCommand.Execute(null); Assert.Equal("7", s.Try.NumberOfTesters);
        s.OpenExperimentsCommand.Execute(null); Assert.True(s.ExperimentsVisible);
        s.AdvancedCommand.Execute(null); Assert.True(s.AdvancedVisible);
        s.OpenLiveCommand.Execute(null);
        Assert.Equal(state, JsonSerializer.Serialize(s.Live.Live.Capture()));
        s.Live.Dispose();
    }
    [Fact]
    public void ExperimentEditorOpensAdvancedAndReturnsToAnalyze()
    {
        var main = new MainWindowViewModel(); var s = main.Simple;
        s.OpenExperimentsCommand.Execute(null); main.Compare.EditCommand.Execute(null);
        Assert.True(main.Compare.IsEditing); Assert.True(s.AdvancedVisible); Assert.Equal(0, main.SelectedView);
        main.Compare.DiscardDraft(); Assert.True(s.ExperimentsVisible); Assert.False(main.Compare.IsEditing);
    }
    [Fact]
    public async Task FastAdvanceMatchesNormalContinuationWithInterventionsRandomStateAndCheckpoints()
    {
        var source = LiveSimulation.Start(LiveSimulation.Demo with { DevelopmentWipLimit = 2,
            DevelopmentDistribution = new TriangularEffort(1, 3, 7), Quality = new() { Enabled = true, CodeReviewDefectProbability = .3, TestingDefectProbability = .2 } });
        for (var i = 0; i < 37; i++) source.Step();
        source.CreateCheckpoint("Before intervention");
        source.Session.ApplyChanges(source.Session.Configuration with { Team = new(4, 3), WorkItemsPerDay = 1.3m }, "Change at 37");
        using var vm = new LiveViewModel(); vm.Load(LiveSessionJson.Load(LiveSessionJson.Save(source)));
        var before = JsonSerializer.Serialize(source.Session.Days);
        vm.TargetDay = "140"; await vm.RunToDayAsync();
        while (source.Session.CurrentDay < 140) source.Step();
        Assert.Equal(JsonSerializer.Serialize(source.Capture()), JsonSerializer.Serialize(vm.Live!.Capture()));
        Assert.Equal(before, JsonSerializer.Serialize(vm.Live.Session.Days.Take(37).ToArray()));
        Assert.False(vm.IsRunning); Assert.True(vm.CanResume); Assert.Equal(140, vm.Trend.Points[^1].Day);
        vm.SelectedCheckpoint = vm.Checkpoints.Single(); vm.RestoreCommand.Execute(null);
        Assert.Equal(37, vm.Day); Assert.Empty(vm.Live.Session.Changes);
    }
    [Theory]
    [InlineData("0")] [InlineData("-1")] [InlineData("abc")] [InlineData("10001")]
    public async Task InvalidTargetsDoNotChangeState(string target)
    {
        using var vm = new LiveViewModel(); vm.Start(); vm.Pause();
        var before = JsonSerializer.Serialize(vm.Live!.Capture()); vm.TargetDay = target; await vm.RunToDayAsync();
        Assert.Equal(before, JsonSerializer.Serialize(vm.Live.Capture())); Assert.Contains("Run to Day:", vm.Status);
    }
    [Fact]
    public async Task PauseAndNavigationInterruptFastAdvanceAtACompletedDay()
    {
        var s = new MainWindowViewModel().Simple; var vm = s.Live; vm.Start(); vm.Pause();
        vm.TargetDay = "1000"; var advance = vm.RunToDayAsync();
        Assert.True(vm.IsFastAdvancing); Assert.False(vm.CanChange); Assert.False(vm.CanResume);
        s.OpenAnalyzeCommand.Execute(null); var day = vm.Day; await advance;
        Assert.Equal(day, vm.Day); Assert.InRange(day, 1, 999); Assert.True(vm.CanResume);
        s.OpenLiveCommand.Execute(null); vm.TargetDay = "1000"; advance = vm.RunToDayAsync();
        vm.Pause(); day = vm.Day; await advance; Assert.Equal(day, vm.Day);
        vm.Dispose();
    }
    [Fact]
    public async Task ResetDuringFastAdvanceLeavesNoOldHistoryAndSafetyLimitIsHonored()
    {
        using var vm = new LiveViewModel(); vm.Start(); vm.Pause(); vm.TargetDay = "1000";
        var advance = vm.RunToDayAsync(); vm.Reset(); await advance;
        Assert.False(vm.HasSession); Assert.Empty(vm.Trend.Points); Assert.False(vm.IsFastAdvancing);
        vm.SafetyLimit = "50"; vm.Start(); vm.Pause(); vm.TargetDay = "50"; await vm.RunToDayAsync();
        Assert.Equal(50, vm.Day); Assert.False(vm.CanResume); Assert.Contains("safety limit", vm.Status);
    }
}
