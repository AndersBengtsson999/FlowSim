using Simulation.Application;
using Simulation.UI.ViewModels;
using Xunit;

namespace Simulation.UI.Tests;

public sealed class SensitivityViewModelTests
{
    [Fact]
    public async Task CurrentFormIsCapturedAndResultsRemainAttachedToCompletedConfiguration()
    {
        var main = new MainWindowViewModel { NumberOfDevelopers = "3", NumberOfWorkItems = "10", DurationDays = "20" };
        var vm = main.Sensitivity;
        vm.BaseScenario = "Current Scenario form"; vm.WarmUpDays = "0"; vm.Values = "1, 2";
        await vm.RunAsync();
        Assert.NotNull(vm.Result);
        Assert.Equal(3, vm.Result!.BaseScenario.DeveloperCount);
        Assert.Equal(2, vm.Rows.Count);
        Assert.Contains("AverageAvailableDeveloperCapacity", vm.Diagnostics);
        Assert.DoesNotContain(AnalysisMetric.TotalDefectsFound, vm.Metrics);
        vm.Metric = AnalysisMetric.TesterUtilization;
        Assert.Equal(AnalysisReport.Number(vm.Points[0].MeasurementWindow[vm.Metric].P50), vm.Rows[0].Selected);
        main.NumberOfDevelopers = "9";
        Assert.Equal(3, vm.Result.BaseScenario.DeveloperCount);
        Assert.Contains("developers/testers=3/2", vm.ResultConfiguration);
    }
    [Fact]
    public async Task InvalidDistributionRangeReportsErrorWithoutReplacingPriorResults()
    {
        var vm = new SensitivityViewModel(BaselineScenario.CreateRequest) { BaseScenario = "Baseline", WarmUpDays = "0", Values = "1" };
        await vm.RunAsync(); var result = vm.Result;
        vm.BaseScenario = "Variable Effort Example";
        vm.Parameter = SensitivityParameter.TestingEffort; vm.Values = "13";
        await vm.RunAsync();
        Assert.Contains("Triangular", vm.Status);
        Assert.Same(result, vm.Result);
        Assert.False(vm.IsBusy);
        Assert.True(vm.RunCommand.CanExecute(null));
    }
    [Fact]
    public async Task MonteCarloHasPercentilesAndQualityMetricsWhenApplicable()
    {
        var vm = new SensitivityViewModel(BaselineScenario.CreateRequest)
        {
            BaseScenario = "Defects & Rework Example", Mode = SensitivityMode.MonteCarlo,
            Runs = "3", WarmUpDays = "0", Values = "2, 3"
        };
        await vm.RunAsync();
        Assert.Equal(3, vm.Result!.RunsPerPoint);
        Assert.All(vm.Rows, r => Assert.NotEqual("n/a", r.P85));
        Assert.Contains(AnalysisMetric.TotalReworkEffort, vm.Metrics);
        vm.Parameter = SensitivityParameter.TestingDefectProbability;
        Assert.Equal("0, 0.05, 0.1, 0.2, 0.3, 0.5, 0.7", vm.Values);
    }
}
