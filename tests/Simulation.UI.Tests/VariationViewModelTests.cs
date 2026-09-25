using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.UI.ViewModels;
using Xunit;

namespace Simulation.UI.Tests;

public sealed class VariationViewModelTests
{
    [Fact]
    public void EditorSwitchesModesWithoutUsingHiddenInvalidInputs()
    {
        var editor = new EffortEditorViewModel("Testing") { FixedValue = "2", Minimum = "1", MostLikely = "2", Maximum = "5" };
        Assert.True(editor.IsFixed);
        Assert.IsType<FixedEffort>(editor.Read());
        editor.Kind = EffortKind.Triangular; editor.FixedValue = "invalid";
        Assert.True(editor.IsTriangular);
        Assert.IsType<TriangularEffort>(editor.Read());
        editor.Minimum = "0";
        Assert.Contains("Testing", Assert.Throws<ArgumentException>(() => editor.Read()).Message);
        editor.Kind = EffortKind.Fixed; editor.FixedValue = "2";
        Assert.IsType<FixedEffort>(editor.Read());
    }

    [Fact]
    public async Task VariablePresetKeepsBaselineTeamAndCanResetBackToFixed()
    {
        var vm = new MainWindowViewModel();
        vm.VariableEffortCommand.Execute(null);
        Assert.All(vm.EffortEditors, e => Assert.True(e.IsTriangular));
        Assert.Equal("2", vm.DevelopmentDistribution.Minimum);
        Assert.Equal("12", vm.DevelopmentDistribution.Maximum);
        Assert.Equal("0.5", vm.CodeReviewDistribution.Minimum);
        Assert.Equal("5", vm.TestingDistribution.Maximum);
        Assert.Equal("5", vm.NumberOfDevelopers); Assert.Equal("2", vm.NumberOfTesters);
        Assert.Equal("5", vm.DevelopmentWipLimit); Assert.Equal("3", vm.CodeReviewWipLimit);
        Assert.Equal("500", vm.NumberOfRuns);
        await vm.RunAsync();
        Assert.False(vm.HasError);
        var initial = JsonSerializer.Serialize(vm.Result);
        await vm.RunAsync();
        Assert.Equal(initial, JsonSerializer.Serialize(vm.Result));
        vm.RandomSeed = "54321";
        await vm.RunAsync();
        Assert.NotEqual(initial, JsonSerializer.Serialize(vm.Result));
        vm.ResetToBaseline();
        Assert.All(vm.EffortEditors, e => Assert.True(e.IsFixed));
        Assert.Equal("12345", vm.RandomSeed);
        Assert.Null(vm.MonteCarlo);
        await vm.RunAsync();
        Assert.Equal(30, vm.Result!.CompletedWorkItems);
        Assert.All(vm.WorkItems, w => Assert.Equal(5, w.DevelopmentEffort));
    }

    [Fact]
    public async Task MixedStagesUseTheirOwnDistribution()
    {
        var vm = new MainWindowViewModel();
        vm.TestingDistribution.Kind = EffortKind.Triangular;
        await vm.RunAsync();
        Assert.False(vm.HasError);
        Assert.All(vm.WorkItems, w => { Assert.Equal(5, w.DevelopmentEffort); Assert.Equal(1, w.CodeReviewEffort); Assert.InRange(w.TestingEffort, 1, 5); });
    }

    [Fact]
    public async Task MonteCarloRetainsSingleRunAndFormatsAuthoritativePercentiles()
    {
        var vm = new MainWindowViewModel();
        vm.UseVariableEffortExample();
        await vm.RunAsync();
        var single = vm.Result;
        vm.NumberOfRuns = "5";
        await vm.RunMonteCarloAsync();
        Assert.False(vm.HasError);
        Assert.Same(single, vm.Result);
        Assert.Equal(3, vm.SelectedView);
        Assert.Equal(9, vm.MonteCarloMetrics.Count);
        Assert.Equal(vm.MonteCarlo!.AverageLeadTime.P50!.Value.ToString("0.00"), vm.MonteCarloMetrics[2].P50);
        Assert.Same(vm.MonteCarlo.ThroughputPerFiveDays.Histogram, vm.ThroughputHistogram);
        Assert.All(vm.MonteCarloMetrics, m => Assert.Equal(5, m.SampleCount));
        Assert.True(vm.RunCommand.CanExecute(null));
        Assert.True(vm.MonteCarloCommand.CanExecute(null));
    }

    [Fact]
    public async Task InvalidSeedAndRunCountAreReported()
    {
        var vm = new MainWindowViewModel { RandomSeed = "not-an-integer" };
        await vm.RunAsync();
        Assert.True(vm.HasError); Assert.Contains("Random Seed", vm.ErrorMessage);
        vm.RandomSeed = "12345"; vm.NumberOfRuns = "0";
        await vm.RunMonteCarloAsync();
        Assert.True(vm.HasError); Assert.Contains("Number of Runs", vm.ErrorMessage);
        Assert.Null(vm.MonteCarlo);
    }

    [Fact]
    public async Task MonteCarloCanBeCancelledAndDoesNotPublishPartialResults()
    {
        var vm = new MainWindowViewModel { NumberOfRuns = "10000" };
        var task = vm.RunMonteCarloAsync();
        Assert.True(vm.IsBusy);
        Assert.False(vm.RunCommand.CanExecute(null));
        Assert.False(vm.ResetCommand.CanExecute(null));
        vm.Cancel();
        await task;
        Assert.Null(vm.MonteCarlo);
        Assert.False(vm.IsBusy);
        Assert.StartsWith("Cancelled", vm.StatusMessage);
    }
}
