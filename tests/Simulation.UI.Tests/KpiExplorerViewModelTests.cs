using Simulation.Application;
using Simulation.Infrastructure;
using Simulation.UI.ViewModels;
using System.Text.Json;
using Xunit;
namespace Simulation.UI.Tests;
public class KpiExplorerViewModelTests
{
    [Fact]
    public void SelectionComparisonAndWindowsAreReadOnlyAndAligned()
    {
        using var vm=new LiveViewModel();vm.Start();vm.Pause();for(int i=0;i<45;i++)vm.Step();
        var state=JsonSerializer.Serialize(vm.Live!.Session.Capture());
        vm.OpenExplorer(TeamKpi.CycleTimeP85);vm.ComparisonKpi=vm.ExplorerOptions.Single(d=>d.Id==TeamKpi.TotalTeamCost);
        foreach(var window in new[]{10,20,40})
        {
            vm.KpiWindow=window;Assert.True(vm.ExplorerOpen);Assert.True(vm.HasComparison);
            Assert.Equal(window,vm.ExplorerPrimary!.Observation.Window);
            Assert.Equal(vm.ExplorerPrimary.Trend.Points.Select(p=>p.Day),vm.ExplorerSecondary!.Trend.Points.Select(p=>p.Day));
        }
        Assert.Equal(4,vm.KpiCards.Count);Assert.Equal(state,JsonSerializer.Serialize(vm.Live.Session.Capture()));
        vm.ClearComparison();Assert.False(vm.HasComparison);vm.CloseExplorer();Assert.False(vm.ExplorerOpen);Assert.Null(vm.ExplorerPrimary);
    }
    [Fact]
    public void LiveRestoreResetAndSaveLoadRefreshExplorer()
    {
        using var vm=new LiveViewModel();vm.Start();vm.Pause();vm.OpenExplorer(TeamKpi.ReworkCost);
        vm.ComparisonKpi=vm.ExplorerOptions.Single(d=>d.Id==TeamKpi.DebtGrowth);
        for(int i=0;i<20;i++)vm.Step();vm.CheckpointCommand.Execute(null);vm.SelectedCheckpoint=vm.Checkpoints.Single();
        var expected=JsonSerializer.Serialize(vm.ExplorerPrimary);vm.Speed=5;vm.Tick();Assert.Equal(expected,JsonSerializer.Serialize(vm.ExplorerPrimary));
        vm.BeginChange();vm.Draft.NumberOfTesters="3";vm.Draft.DevelopmentWipLimit="2";vm.ApplyChanges();vm.Resume();vm.Tick();vm.Pause();
        Assert.Equal(21,vm.ExplorerPrimary!.Observation.LastDay);
        vm.RestoreCommand.Execute(null);Assert.Equal(expected,JsonSerializer.Serialize(vm.ExplorerPrimary));
        using var loaded=new LiveViewModel();loaded.Load(LiveSessionJson.Load(LiveSessionJson.Save(vm.Live!)));
        Assert.True(loaded.ExplorerOpen);Assert.True(loaded.HasComparison);Assert.Equal(expected,JsonSerializer.Serialize(loaded.ExplorerPrimary));
        vm.Reset();Assert.Null(vm.ExplorerPrimary);vm.Start();vm.Pause();vm.Step();Assert.Equal(1,vm.ExplorerPrimary!.Observation.AvailableDays);
    }
}
