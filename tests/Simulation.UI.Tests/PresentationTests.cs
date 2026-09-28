using System.Text.Json;
using Simulation.Application;
using Simulation.UI.ViewModels;
using Xunit;

namespace Simulation.UI.Tests;

public sealed class PresentationTests
{
    [Fact]
    public async Task SummaryContainsSixPrimaryMetricsAndRetainsDetails()
    {
        var vm = new MainWindowViewModel(); await vm.RunAsync();
        Assert.Equal(PresentationLabels.Primary.Select(p => PresentationLabels.Label(p)).Order(), vm.PrimaryMetrics.Select(m => m.Label).Order());
        Assert.All(vm.PrimaryMetrics, m => Assert.NotEmpty(m.Explanation));
        Assert.Equal(12, vm.Metrics.Count); Assert.Equal(6, vm.AdvancedMetrics.Count); Assert.Equal(10, vm.QualityMetrics.Count);
        Assert.Contains("items", vm.LargestQueue);
    }
    [Fact]
    public void ChangedParametersExcludeIdenticalValuesAndNames()
    {
        var baseline = BaselineScenario.CreateRequest();
        Assert.Empty(ChangedParameter.Between(baseline, baseline with { Name = "Copy" }));
        var changes = ChangedParameter.Between(baseline, baseline with { TesterCount = 3, DeveloperCount = 8, TestingWipLimit = 5 });
        Assert.Equal(3, changes.Count);
        Assert.Equal(new ChangedParameter("Testers", "2", "3"), changes.Single(c => c.Name == "Testers"));
    }
    [Fact]
    public async Task DuplicateWorkflowPreservesExecutedBaselineAndOtherScenarios()
    {
        var vm = new MainWindowViewModel(); await vm.RunAsync(); var result = vm.Result;
        vm.NumberOfDevelopers = "99"; vm.DuplicateAndCompare();
        Assert.Equal(3, vm.Compare.Scenarios.Count); Assert.True(vm.Compare.IsEditing);
        var baseline = vm.Compare.Baseline!.Scenario; var alternative = vm.Compare.Alternative!.Id;
        Assert.NotEqual(baseline.Id, alternative); Assert.Equal(5, baseline.Configuration.DeveloperCount);
        Assert.Same(result, vm.Compare.Session.Results.Single().SingleRun);
        vm.NumberOfTesters = "3"; await vm.RunOrApplyAsync();
        Assert.Equal(5, vm.SelectedView); Assert.Equal(1, vm.MainArea);
        Assert.Equal(2, vm.Compare.Baseline!.Scenario.Configuration.TesterCount);
        Assert.Equal(3, vm.Compare.Alternative!.Scenario.Configuration.TesterCount);
        Assert.Single(vm.Compare.ChangedParameters); Assert.Equal(6, vm.Compare.SimpleMetrics.Count);
        Assert.Null(vm.Compare.Comparison); // unrelated initial scenario has never run; pair still works
        Assert.True(vm.Compare.HasPairFlow); Assert.Equal(5, vm.Compare.SimpleFlow.Count);
        var comparison = vm.Compare.Session.Compare([alternative]);
        foreach (var row in vm.Compare.SimpleMetrics)
        {
            var metric = comparison.Metrics.Single(m => PresentationLabels.Label(m.Metric) == row.Name);
            var a = metric.Cells.Single(c => c.ScenarioId == alternative);
            var b = metric.Cells.Single(c => c.ScenarioId == baseline.Id);
            var format = ScenarioComparisonRunner.IsRatio(metric.Metric) ? "P1" : "0.###";
            Assert.Equal(b.Distribution.P50!.Value.ToString(format, System.Globalization.CultureInfo.InvariantCulture), row.Before);
            Assert.Equal(a.Distribution.P50!.Value.ToString(format, System.Globalization.CultureInfo.InvariantCulture), row.After);
            if (ScenarioComparisonRunner.IsRatio(metric.Metric)) Assert.Equal(a.PercentagePoints!.Value.ToString("+0.###;-0.###;0", System.Globalization.CultureInfo.InvariantCulture) + " percentage points", row.Difference);
            else Assert.Equal(a.Delta.Absolute!.Value.ToString("+0.###;-0.###;0", System.Globalization.CultureInfo.InvariantCulture), row.Difference);
        }
        vm.Compare.Baseline = vm.Compare.Alternative;
        Assert.NotEqual(vm.Compare.Baseline!.Id, vm.Compare.Alternative!.Id);
    }
    [Fact]
    public async Task PresentationAndNavigationDoNotChangeSimulation()
    {
        var vm = new MainWindowViewModel(); await vm.RunAsync(); var before = JsonSerializer.Serialize(vm.Result);
        var expected = JsonSerializer.Serialize(new SimulationRunner().Run(BaselineScenario.CreateRequest()));
        Assert.Equal(expected, before);
        vm.MainArea = 2; vm.AnalyzePage = 1; Assert.Equal(6, vm.SelectedView);
        vm.MainArea = 0; vm.SimulatePage = 1; vm.SelectedDay = 50;
        _ = vm.PrimaryMetrics; _ = vm.AdvancedMetrics; _ = vm.FlowStates;
        Assert.Equal(before, JsonSerializer.Serialize(vm.Result));
        await vm.RunAsync(); Assert.Equal(before, JsonSerializer.Serialize(vm.Result));
    }
    [Fact]
    public async Task MonteCarloKeepsAdvancedDetailsWithoutSynthesizedFlow()
    {
        var vm = new MainWindowViewModel().Compare; vm.DuplicateCommand.Execute(null);
        vm.Mode = ExperimentRunMode.MonteCarlo; vm.MonteCarloRuns = "3"; await vm.RunAsync(true);
        Assert.Equal(6, vm.SimpleMetrics.Count); Assert.NotEmpty(vm.Rows); Assert.NotEmpty(vm.PairedRows);
        Assert.False(vm.HasPairFlow); Assert.Empty(vm.SimpleFlow); Assert.Contains("median", vm.PairStatus);
        vm.BaseSeed = "invalid"; Assert.Empty(vm.SimpleMetrics);
    }

    [Fact]
    public async Task PairSelectionIgnoresTransientBindingNullAndUsesSelectedSnapshots()
    {
        var main = new MainWindowViewModel(); await main.RunAsync(); main.DuplicateAndCompare();
        main.NumberOfTesters = "3"; await main.RunOrApplyAsync(); var vm = main.Compare;
        var selected = vm.Alternative!.Id; vm.Alternative = null;
        Assert.Equal(selected, vm.Alternative!.Id);
        vm.ImportScenario(ScenarioDefinition.Create(BaselineScenario.CreateRequest() with { Name = "Shorter run", SimulationDays = 20, TesterCount = 1 }));
        vm.Alternative = vm.Selected; await vm.RunAsync(false);
        Assert.Equal(vm.Selected!.Id, vm.Alternative!.Id); Assert.Equal(20, vm.FlowLastDay);
        vm.FlowDay = 100; Assert.Equal(20, vm.FlowDay);
        var a = vm.Session.Results.Single(r => r.ScenarioSnapshot.Id == vm.Baseline!.Id).SingleRun!.Days[19];
        var b = vm.Session.Results.Single(r => r.ScenarioSnapshot.Id == vm.Alternative.Id).SingleRun!.Days[19];
        var queue = vm.SimpleFlow.Single(r => r.Name == "Waiting for Testing");
        Assert.Equal(a.WaitingForTestingCount, queue.Before); Assert.Equal(b.WaitingForTestingCount, queue.After);
        Assert.Equal(2, vm.ChangedParameters.Count);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VariableEffortAndReworkRemainIdenticalAfterPresentation(bool defects)
    {
        var request = defects ? BaselineScenario.DefectsAndReworkExample() : BaselineScenario.VariableEffortExample();
        var main = new MainWindowViewModel(); main.LoadConfiguration(request); await main.RunAsync();
        var expected = new SimulationRunner().Run(request);
        Assert.Equal(JsonSerializer.Serialize(expected.Days), JsonSerializer.Serialize(main.Result!.Days));
        Assert.Equal(JsonSerializer.Serialize(expected.WorkItems), JsonSerializer.Serialize(main.Result.WorkItems));
        var before = JsonSerializer.Serialize(main.Result); _ = main.CapacityMetrics; _ = main.LargestQueue;
        main.DuplicateAndCompare(); main.Compare.DiscardDraft();
        Assert.Equal(before, JsonSerializer.Serialize(main.Result));
    }
}
