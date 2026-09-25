using System.Text.Json;
using Simulation.UI.ViewModels;
using Xunit;

namespace Simulation.UI.Tests;

public sealed class DefectViewModelTests
{
    [Fact]
    public async Task PresetSetsQualityAndResultsExposeReworkAndHistory()
    {
        var vm = new MainWindowViewModel();
        vm.DefectsExampleCommand.Execute(null);
        Assert.True(vm.DefectsEnabled);
        Assert.Equal("15", vm.CodeReviewDefectProbability);
        Assert.Equal("10", vm.TestingDefectProbability);
        Assert.Equal("3", vm.ReworkWipLimit);
        Assert.All(vm.ReworkEffortEditors, e => Assert.True(e.IsTriangular));
        await vm.RunAsync();
        Assert.False(vm.HasError);
        Assert.True(vm.Result!.TotalDefectsFound > 0);
        Assert.Equal(10, vm.QualityMetrics.Count);
        Assert.Equal(vm.Result.ReworkDeveloperCapacityShare.ToString("P1"), vm.QualityMetrics.Single(m => m.Label == "Rework Developer Capacity Share").Value);
        var day = vm.Days.First(d => d.WaitingForReworkCount > 0);
        vm.SelectedDay = day.Day + 1;
        Assert.Equal(day.WaitingForReworkCount, vm.WaitingForReworkCount);
        Assert.Equal(day.ReworkCount, vm.ReworkCount);
        vm.SelectedWorkItem = vm.WorkItems.First(w => w.DefectsFound > 0);
        Assert.Equal(vm.SelectedWorkItem.Events.Count, vm.SelectedHistory.Count);
        Assert.Contains(vm.SelectedHistory, e => e.Description.Contains("Defect found"));
        Assert.Contains(vm.SelectedHistory, e => e.Description.Contains("Rework"));
        vm.ResetToBaseline();
        Assert.False(vm.DefectsEnabled);
        Assert.Null(vm.SelectedWorkItem);
        Assert.Empty(vm.SelectedHistory);
    }

    [Fact]
    public async Task DisabledDefectsIgnoreHiddenInputsAndPreserveVariableRun()
    {
        var vm = new MainWindowViewModel();
        vm.UseVariableEffortExample();
        await vm.RunAsync();
        var original = JsonSerializer.Serialize(vm.Result);
        vm.CodeReviewDefectProbability = "invalid";
        vm.ReworkWipLimit = "-1";
        vm.TestingReworkDistribution.FixedValue = "invalid";
        await vm.RunAsync();
        Assert.False(vm.HasError);
        Assert.Equal(original, JsonSerializer.Serialize(vm.Result));
        vm.DefectsEnabled = true;
        await vm.RunAsync();
        Assert.True(vm.HasError);
    }

    [Fact]
    public async Task ProbabilitiesAreEnteredAsPercentagesAndValidated()
    {
        var vm = new MainWindowViewModel { DefectsEnabled = true, CodeReviewDefectProbability = "101" };
        await vm.RunAsync();
        Assert.True(vm.HasError);
        vm.CodeReviewDefectProbability = "100"; vm.DurationDays = "20";
        await vm.RunAsync();
        Assert.False(vm.HasError);
        Assert.True(vm.Result!.CodeReviewDefectsFound > 0);
        Assert.Equal(0, vm.Result.CompletedWorkItems);
    }

    [Fact]
    public async Task MonteCarloKeepsExistingMetricsAndAddsQualityDistributions()
    {
        var vm = new MainWindowViewModel();
        vm.UseDefectsExample(); vm.NumberOfRuns = "10";
        await vm.RunMonteCarloAsync();
        Assert.False(vm.HasError);
        Assert.Equal(9, vm.MonteCarloMetrics.Count);
        Assert.Equal(4, vm.MonteCarloQualityMetrics.Count);
        Assert.Equal(vm.MonteCarlo!.TotalReworkEffort.P50!.Value.ToString("0.00"), vm.MonteCarloQualityMetrics[2].P50);
        Assert.All(vm.MonteCarloQualityMetrics, m => Assert.Equal(10, m.SampleCount));
    }
}
