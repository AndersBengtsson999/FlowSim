using Simulation.Application;
using Simulation.UI.ViewModels;
using Xunit;
namespace Simulation.UI.Tests;
public sealed class GuidedInterventionViewModelTests
{
    [Fact]
    public async Task GuidedEditorBaselinePlaybackDetailsAndRestoreUseExistingControls()
    {
        using var vm = new LiveViewModel(); vm.Start(); for (int i=0;i<100;i++) vm.Tick();
        vm.BeginGuidedCommand.Execute(null);
        Assert.True(vm.GuidedPending); Assert.True(vm.IsEditing); Assert.False(vm.IsRunning);
        vm.EvaluationDays = 10;
        var notified = false;
        vm.PropertyChanged += (_, e) => notified |= e.PropertyName == nameof(vm.GuidedDraftDescription);
        vm.ChangeFields.Single(f => f.Label == "Testers").Value = "3";
        Assert.True(notified); Assert.Contains("2 → 3", vm.GuidedDraftDescription);
        Assert.Contains("101–110", vm.GuidedDraftDescription);
        vm.ApplyChanges(); await vm.BaselineTask;
        Assert.Equal(100, vm.Day); Assert.False(vm.IsRunning); Assert.False(vm.GuidedPending);
        Assert.Empty(vm.GuidedRows); vm.Tick(); Assert.Equal(100, vm.Day);
        vm.Step(); Assert.Contains("1/10", vm.GuidedProgress); Assert.Equal(4, vm.GuidedRows.Count);
        vm.Resume(); vm.Tick(); vm.Pause(); var progress = vm.GuidedProgress;
        vm.Tick(); Assert.Equal(progress, vm.GuidedProgress);
        for(int i=0;i<8;i++) vm.Step(); Assert.StartsWith("Complete", vm.GuidedProgress);
        Assert.Equal(100, vm.GuidedQueueSeries[0].Points[0].Day);
        Assert.Equal(110, vm.GuidedQueueSeries[0].Points[^1].Day);
        vm.SelectedCheckpoint = vm.Checkpoints.Single(); vm.RestoreCommand.Execute(null);
        Assert.StartsWith("Invalidated", vm.GuidedProgress); Assert.Empty(vm.GuidedRows);
    }
    [Fact]
    public async Task NormalChangesRemainAllowedAndResetLoadCancelDiscardStaleData()
    {
        using var vm = new LiveViewModel(); vm.Start(); vm.BeginGuidedExperiment();
        vm.CancelChangeCommand.Execute(null); Assert.False(vm.GuidedPending); Assert.False(vm.HasGuidedExperiment);
        vm.BeginGuidedExperiment(); vm.Draft.NumberOfTesters = "3"; vm.ApplyChanges(); await vm.BaselineTask;
        vm.Step(); vm.BeginChange(); vm.Draft.NumberOfTesters = "4"; vm.ApplyChanges();
        Assert.Equal(4, vm.Live!.Session.Configuration.Team.TesterCount);
        Assert.StartsWith("Invalidated", vm.GuidedProgress); Assert.True(vm.CanResume);
        vm.BeginGuidedExperiment(); vm.ApplyChanges(); await vm.BaselineTask; vm.Step();
        Assert.NotEmpty(vm.GuidedRows); vm.Reset(); Assert.Empty(vm.GuidedRows); Assert.StartsWith("Invalidated", vm.GuidedProgress);
        vm.Load(LiveSimulation.Start(LiveSimulation.Demo)); Assert.Empty(vm.GuidedRows);
    }
    [Fact]
    public async Task NoOpIsValidButMultipleParametersDoNotStartGuidedComparison()
    {
        using var vm = new LiveViewModel(); vm.Start(); vm.BeginGuidedExperiment(); vm.ApplyChanges(); await vm.BaselineTask;
        vm.Step(); Assert.All(vm.GuidedRows, row => Assert.Equal(row.Baseline, row.Experiment));
        vm.BeginGuidedExperiment(); vm.Draft.NumberOfDevelopers = "9"; vm.Draft.NumberOfTesters = "4"; vm.ApplyChanges();
        Assert.Equal(9, vm.Live!.Session.Configuration.Team.DeveloperCount); Assert.Empty(vm.GuidedRows);
    }
}
