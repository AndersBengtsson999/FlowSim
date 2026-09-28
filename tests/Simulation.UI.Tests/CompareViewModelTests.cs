using Simulation.Application;
using Simulation.UI.ViewModels;
using Xunit;

namespace Simulation.UI.Tests;

public sealed class CompareViewModelTests
{
    [Fact]
    public async Task DuplicateEditApplyRunAndSwitchBaselineUseExistingEditor()
    {
        var main = new MainWindowViewModel(); var vm = main.Compare;
        var baseline = vm.Selected!.Scenario;
        vm.DuplicateCommand.Execute(null); vm.ScenarioName = "More Testers"; vm.RenameCommand.Execute(null);
        vm.EditCommand.Execute(null); Assert.True(vm.IsEditing); Assert.Equal(0, main.SelectedView);
        main.NumberOfTesters = "4"; vm.ApplyCommand.Execute(null); Assert.False(vm.IsEditing); Assert.Equal(5, main.SelectedView);
        Assert.Equal(2, baseline.Configuration.TesterCount); Assert.Equal(4, vm.Selected!.Scenario.Configuration.TesterCount);
        await vm.RunAsync(true); Assert.NotNull(vm.Comparison); Assert.Equal(2, vm.Headers.Count);
        Assert.Contains(vm.ParameterRows.Single(r => r.Label == "Testers").Cells, c => c.Different);
        Assert.Equal(2, vm.FlowSeries.Count);
        Assert.All(vm.FlowSeries, s => Assert.Equal(100, s.Points.Count));
        vm.Baseline = vm.Selected; Assert.Equal(vm.Selected.Id, vm.Comparison!.ExperimentSnapshot.BaselineId);
        Assert.Contains("Executed:", vm.Traceability);
    }
    [Fact]
    public async Task EditingAndInvalidOptionsNeverSilentlyShowOldComparison()
    {
        var main = new MainWindowViewModel(); var vm = main.Compare; await vm.RunAsync(true);
        Assert.NotNull(vm.Comparison); vm.EditCommand.Execute(null); Assert.Null(vm.Comparison);
        Assert.Equal("Out of Date", vm.Selected!.Status);
        vm.DiscardCommand.Execute(null); Assert.NotNull(vm.Comparison);
        vm.BaseSeed = "bad"; Assert.Null(vm.Comparison);
        vm.Selected!.Included = false; Assert.Null(vm.Comparison);
        vm.BaseSeed = "12345"; Assert.NotNull(vm.Comparison);
    }
    [Fact]
    public async Task MonteCarloShowsSignedPairedRowsAndNoInventedFlow()
    {
        var main = new MainWindowViewModel(); var vm = main.Compare;
        vm.ImportScenario(ScenarioDefinition.Create(BaselineScenario.VariableEffortExample()));
        vm.Mode = ExperimentRunMode.MonteCarlo; vm.MonteCarloRuns = "3";
        await vm.RunAsync(true); Assert.NotNull(vm.Comparison);
        Assert.NotEmpty(vm.PairedRows); Assert.Empty(vm.FlowSeries);
        Assert.Contains("P95", vm.PairedRows[0].Cells[1].Text);
        vm.CommonRandomNumbers = false; Assert.Null(vm.Comparison);
        await vm.RunAsync(true); Assert.Empty(vm.PairedRows);
    }
    [Fact]
    public void LoadingExperimentAndScenarioPreservesIdentitySemantics()
    {
        var vm = new MainWindowViewModel().Compare;
        var e = DemonstrationExperiments.Create("Testing Capacity"); vm.LoadExperiment(e);
        Assert.Equal(e.Id, vm.Session.Experiment.Id); Assert.Equal(e.BaselineId, vm.Baseline!.Id);
        vm.ImportScenario(e.Scenarios[0]); Assert.NotEqual(e.Scenarios[0].Id, vm.Selected!.Id);
        Assert.Equal(e.Scenarios[0].Configuration, vm.Selected.Scenario.Configuration);
        Assert.All(vm.Scenarios, s => Assert.Equal("Not Run", s.Status));
    }
}
