using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.UI.ViewModels;
using Xunit;

namespace Simulation.UI.Tests;

public sealed class GameViewTests
{
    [Fact]
    public void SwitchingPreservesSessionHistoryCheckpointsAndPendingDraft()
    {
        using var vm = new LiveViewModel(); vm.Start(); for (var i = 0; i < 12; i++) vm.Tick();
        vm.Pause(); vm.Live!.CreateCheckpoint("Before edit"); vm.BeginChange();
        vm.Draft.NumberOfTesters = "7"; vm.ChangeLabel = "Keep this draft";
        var live = vm.Live; var before = JsonSerializer.Serialize(live.Capture());
        for (var i = 0; i < 20; i++) { vm.Visualization = "Game View"; _ = vm.GameScene; vm.Visualization = "Flow Board"; }
        Assert.Same(live, vm.Live); Assert.Equal(before, JsonSerializer.Serialize(live.Capture()));
        Assert.True(vm.IsEditing); Assert.Equal("7", vm.Draft.NumberOfTesters); Assert.Equal("Keep this draft", vm.ChangeLabel);
        vm.Tick(); vm.Step(); Assert.Equal(12, vm.Day); Assert.False(vm.IsRunning);
    }
    [Theory]
    [InlineData(.5)] [InlineData(10)]
    public void SelectionDoesNotChangePlaybackOrDuplicateExecution(double speed)
    {
        using var game = new LiveViewModel(); using var board = new LiveViewModel();
        game.Speed = speed; board.Speed = speed; game.Start(); board.Start();
        for (var day = 1; day <= 80; day++)
        {
            game.Visualization = day % 2 == 0 ? "Flow Board" : "Game View";
            game.GameRenderingMode = day % 3 == 0 ? "Legacy vectors" : "Sprites";
            Assert.True(game.IsRunning); game.Tick(); board.Tick();
            Assert.Equal(day, game.Day); Assert.Equal(day, game.GameScene.Day);
        }
        game.Pause(); board.Pause();
        Assert.Equal(JsonSerializer.Serialize(board.Live!.Capture()), JsonSerializer.Serialize(game.Live!.Capture()));
        game.Visualization = "Game View"; game.Tick(); Assert.Equal(80, game.Day);
    }
    [Fact]
    public void ScenePartitionsRealStatesWithoutDoubleCountingDependenciesOrRework()
    {
        var live = LiveSimulation.Start(BaselineScenario.DefectsAndReworkExample() with
        { ResidualDependencies = new(.5, 4), Release = new(ReleaseMode.Scheduled, 2, 5) }, WorkArrivalMode.AlwaysAvailable);
        for (var i = 0; i < 150; i++) live.Step();
        var before = JsonSerializer.Serialize(live.Capture()); var s = GameSceneState.From(live); var d = live.CurrentSnapshot;
        Assert.Equal(d.BacklogCount, s.Areas[0].Count);
        Assert.Equal(d.DevelopmentCount + d.ReworkCount + d.WaitingForReworkCount, s.Areas[1].Count);
        Assert.Equal(d.CodeReviewCount + d.WaitingForCodeReviewCount, s.Areas[2].Count);
        Assert.Equal(d.TestingCount + d.WaitingForTestingCount, s.Areas[3].Count);
        Assert.Equal(d.ReadyForReleaseCount, s.Areas[4].Count); Assert.Equal(d.DoneCount, s.Areas[5].Count);
        Assert.Equal(d.TotalWip, s.Areas.Skip(1).Take(4).Sum(a => a.Count));
        Assert.Equal(live.Session.DebtState.Amount, s.Debt); Assert.Equal(before, JsonSerializer.Serialize(live.Capture()));
    }
    [Fact]
    public void ReleaseCounterUsesActualDayEventsIncludingZeroBetweenOpportunities()
    {
        var live = LiveSimulation.Start(BaselineScenario.CreateRequest() with { Release = new(ReleaseMode.Scheduled, 2, 5) }, WorkArrivalMode.AlwaysAvailable);
        var previous = 0; var observed = 0;
        for (var day = 1; day <= 60; day++)
        {
            live.Step(); var scene = GameSceneState.From(live);
            Assert.Equal(live.CurrentSnapshot.DoneCount - previous, scene.ReleasedToday);
            if (day % 5 != 0) Assert.Equal(0, scene.ReleasedToday);
            previous = live.CurrentSnapshot.DoneCount; observed += scene.ReleasedToday;
        }
        Assert.True(observed > 0);
    }
    [Fact]
    public void DayZeroResetAndCheckpointRestoreNeverShowStaleScene()
    {
        using var vm = new LiveViewModel(); Assert.False(vm.GameScene.HasSession);
        vm.Preset = "Baseline"; vm.Start(); vm.Pause(); Assert.Equal(0, vm.GameScene.Day);
        Assert.Equal(vm.Live!.CurrentSnapshot.BacklogCount, vm.GameScene.Areas[0].Count);
        vm.Live.CreateCheckpoint("zero"); for (var i = 0; i < 8; i++) vm.Step(); Assert.Equal(8, vm.GameScene.Day);
        vm.Load(LiveSimulation.Restore(vm.Live.Capture()));
        vm.SelectedCheckpoint = vm.Checkpoints.Single(); vm.RestoreCommand.Execute(null); Assert.Equal(0, vm.GameScene.Day);
        vm.Reset(); Assert.False(vm.GameScene.HasSession); Assert.Empty(vm.GameScene.Areas);
    }
    [Fact]
    public void LargeCountsRemainExactWithBoundedVisualsAndCachedProjection()
    {
        using var vm = new LiveViewModel();
        vm.Load(LiveSimulation.Start(BaselineScenario.CreateRequest() with { DeveloperCount = 200, TesterCount = 100, Skills = new(40, .3), NumberOfWorkItems = 2000 }, WorkArrivalMode.FixedBacklog));
        var scene = vm.GameScene; Assert.Same(scene, vm.GameScene);
        Assert.Equal(2000, scene.Areas[0].Count); Assert.Equal(8, scene.Areas[0].VisiblePackages); Assert.Equal(1992, scene.Areas[0].GroupedPackages);
        Assert.Equal(6, scene.VisibleDevelopers); Assert.Equal(4, scene.VisibleTesters); Assert.Equal(6, scene.VisibleSpecialists);
        Assert.Equal(200, scene.Developers); Assert.Equal(40, scene.Specialists);
        vm.Visualization = "Game View"; Assert.Same(scene, vm.GameScene);
    }
    [Theory]
    [InlineData(0, 0)] [InlineData(0, 2000)] [InlineData(2000, 0)]
    [InlineData(1, 2000)] [InlineData(2000, 1)] [InlineData(100, 100)]
    public void PackageGroupsPreserveActiveAndWaitingWithoutInventingItems(int active, int waiting)
    {
        var area = new GameArea("Testing", active, waiting, "");
        Assert.InRange(area.VisibleActivePackages, 0, active);
        Assert.InRange(area.VisibleWaitingPackages, 0, waiting);
        Assert.Equal(Math.Min(8, active + waiting), area.VisibleActivePackages + area.VisibleWaitingPackages);
        if (active > 0) Assert.True(area.VisibleActivePackages > 0);
        if (waiting > 0) Assert.True(area.VisibleWaitingPackages > 0);
        Assert.Equal(active + waiting, area.VisiblePackages + area.GroupedPackages);
    }
    [Fact]
    public void AppliedTeamChangesRefreshSceneWithoutAdvancingDay()
    {
        using var vm = new LiveViewModel(); vm.Start(); vm.Pause();
        var before = vm.GameScene; var day = vm.Day;
        vm.BeginChange(); vm.Draft.NumberOfDevelopers = "9"; vm.Draft.NumberOfTesters = "6";
        vm.ApplyChanges();
        Assert.False(vm.IsEditing); Assert.Equal(day, vm.Day);
        Assert.NotSame(before, vm.GameScene);
        Assert.Equal(9, vm.GameScene.Developers); Assert.Equal(6, vm.GameScene.Testers);
        Assert.Equal(6, vm.GameScene.VisibleDevelopers); Assert.Equal(4, vm.GameScene.VisibleTesters);
    }
}
