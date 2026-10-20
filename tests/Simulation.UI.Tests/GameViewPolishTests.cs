using System.Text.Json;
using Simulation.Application;
using Simulation.UI.ViewModels;
using Xunit;
namespace Simulation.UI.Tests;
public class GameViewPolishTests
{
    [Fact]
    public void ExpandedPresentationPreservesComparisonModeKpisAndSimulation()
    {
        using var vm=new LiveViewModel();vm.Start();vm.Pause();for(int i=0;i<25;i++)vm.Step();
        vm.OpenExplorer(TeamKpi.CycleTime);vm.ComparisonKpi=vm.ExplorerOptions.Single(d=>d.Id==TeamKpi.TotalTeamCost);vm.KpiWindow=10;
        var state=JsonSerializer.Serialize(vm.Live!.Session.Capture());var values=JsonSerializer.Serialize(vm.KpiCards);
        vm.ExplorerMode="Compare Scenarios";vm.ExplorerExpanded=true;vm.ExplorerExpanded=false;
        Assert.Equal("Compare Scenarios",vm.ExplorerMode);Assert.Equal(10,vm.KpiWindow);Assert.Equal(TeamKpi.TotalTeamCost,vm.ComparisonKpi!.Id);
        Assert.Equal(values,JsonSerializer.Serialize(vm.KpiCards));Assert.Equal(state,JsonSerializer.Serialize(vm.Live.Session.Capture()));
    }
    [Fact]
    public void FeedbackFollowsObservedDaysAndCheckpointRestoration()
    {
        using var vm=new LiveViewModel();vm.Start();vm.Pause();for(int i=0;i<30;i++)vm.Step();
        var releases=vm.Live!.Session.WorkItems.Count(w=>w.DoneDay==vm.Day);
        Assert.Contains($"Day 30 · Released {releases}",vm.WorkshopFeedback);
        vm.CheckpointCommand.Execute(null);vm.SelectedCheckpoint=vm.Checkpoints.Single();var expected=vm.WorkshopFeedback;
        vm.BeginChange();vm.Draft.NumberOfTesters="3";vm.ApplyChanges();Assert.Contains("takes effect Day 31",vm.WorkshopFeedback);
        vm.Step();Assert.Contains("effective Day 31",vm.WorkshopFeedback);
        vm.RestoreCommand.Execute(null);Assert.Equal(expected,vm.WorkshopFeedback);
        vm.Reset();Assert.Contains("Start or step",vm.WorkshopFeedback);
    }
    [Fact]
    public async Task ExperimentCompletionFeedbackUsesExistingValidatedExperiment()
    {
        using var vm=new LiveViewModel();vm.Start();vm.Pause();vm.EvaluationDays=10;
        vm.BeginGuidedExperiment();vm.Draft.NumberOfTesters="3";vm.ApplyChanges();await vm.BaselineTask;
        for(int i=0;i<9;i++)vm.Step();Assert.DoesNotContain("Experiment complete",vm.WorkshopFeedback);
        vm.Step();Assert.Contains("Experiment complete",vm.WorkshopFeedback);
        vm.SelectedCheckpoint=vm.Checkpoints.Single();vm.RestoreCommand.Execute(null);Assert.DoesNotContain("Experiment complete",vm.WorkshopFeedback);
    }
}
