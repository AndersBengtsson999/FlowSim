using System.Globalization;
using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.UI.ViewModels;
using Xunit;

namespace Simulation.UI.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public async Task ResetUsesAuthoritativeBaselineAndClearsPriorResults()
    {
        var vm = new MainWindowViewModel { NumberOfDevelopers = "1", NumberOfTesters = "0", DurationDays = "5" };
        await vm.RunAsync();
        Assert.True(vm.HasResults);
        var notifications = new List<string?>();
        vm.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
        vm.ResetCommand.Execute(null);
        var baseline = BaselineScenario.Create();
        Assert.Equal(baseline.Team.DeveloperCount.ToString(), vm.NumberOfDevelopers);
        Assert.Equal(baseline.Team.TesterCount.ToString(), vm.NumberOfTesters);
        Assert.Equal(baseline.Team.DeveloperCapacityPerDay.ToString(CultureInfo.InvariantCulture), vm.DeveloperCapacity);
        Assert.Equal(baseline.Team.TesterCapacityPerDay.ToString(CultureInfo.InvariantCulture), vm.TesterCapacity);
        Assert.Equal(baseline.DevelopmentWipLimit.ToString(), vm.DevelopmentWipLimit);
        Assert.Equal(baseline.CodeReviewWipLimit.ToString(), vm.CodeReviewWipLimit);
        Assert.Equal(baseline.TestingWipLimit.ToString(), vm.TestingWipLimit);
        Assert.Equal(baseline.WorkItems.Count.ToString(), vm.NumberOfWorkItems);
        Assert.Equal(baseline.SimulationDays.ToString(), vm.DurationDays);
        Assert.Equal(baseline.WorkItems[0].DevelopmentEffort.ToString(CultureInfo.InvariantCulture), vm.DevelopmentEffort);
        Assert.Equal(baseline.WorkItems[0].CodeReviewEffort.ToString(CultureInfo.InvariantCulture), vm.CodeReviewEffort);
        Assert.Equal(baseline.WorkItems[0].TestingEffort.ToString(CultureInfo.InvariantCulture), vm.TestingEffort);
        Assert.False(vm.HasResults);
        Assert.Null(vm.SelectedSnapshot);
        Assert.Empty(vm.Metrics);
        Assert.Equal(0, vm.SelectedView);
        Assert.Contains(string.Empty, notifications);
    }

    [Fact]
    public async Task RunUsesApplicationResultsWithoutRecalculatingMetrics()
    {
        var vm = new MainWindowViewModel();
        await vm.RunAsync();
        var expected = new SimulationEngine().Run(BaselineScenario.Create());
        Assert.False(vm.HasError);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(vm.Result));
        Assert.Same(vm.Result!.Days, vm.Days);
        Assert.Same(vm.Result.WorkItems, vm.WorkItems);
        Assert.Equal(12, vm.Metrics.Count);
        Assert.All(vm.Metrics, m => Assert.False(string.IsNullOrWhiteSpace(m.Explanation)));
        Assert.Equal(2, vm.SelectedView);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task DaySelectionUsesExistingSnapshotAndNeverReruns()
    {
        var vm = new MainWindowViewModel();
        await vm.RunAsync();
        var result = vm.Result;
        vm.NumberOfDevelopers = "invalid"; // Reading configuration again would fail.
        vm.SelectedDay = 5;
        Assert.Same(result!.Days[4], vm.SelectedSnapshot);
        Assert.Equal(result.Days[4].WaitingForCodeReviewCount, vm.FlowStates[2].Count);
        Assert.Equal(result.Days[4].TotalWip + result.Days[4].BacklogCount + result.Days[4].DoneCount, vm.FlowStates.Sum(s => s.Count));
        vm.SelectedDay = 0;
        Assert.Same(result.Days[0], vm.SelectedSnapshot);
        vm.SelectedDay = 1000;
        Assert.Same(result.Days[^1], vm.SelectedSnapshot);
        Assert.Equal(100, vm.SelectedDay);
        Assert.Same(result, vm.Result);
        Assert.False(vm.HasError);
    }

    [Fact]
    public void NoResultsDaySelectionIsSafe()
    {
        var vm = new MainWindowViewModel { SelectedDay = 99 };
        Assert.Equal(1, vm.SelectedDay);
        Assert.Null(vm.SelectedSnapshot);
        Assert.Empty(vm.FlowStates);
        Assert.Empty(vm.WorkItems);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("sv-SE")]
    public async Task ResultsUseConsistentCultureAwareUnitsAndPercentages(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var vm = new MainWindowViewModel();
            await vm.RunAsync();
            var r = vm.Result!;
            Assert.Equal(r.DeveloperUtilization.ToString("P1"), vm.Metrics.Single(m => m.Label == "Developer Utilization").Value);
            Assert.Equal(r.TesterUtilization.ToString("P1"), vm.Metrics.Single(m => m.Label == "Tester Utilization").Value);
            Assert.Equal(r.ThroughputPerFiveDays.ToString("0.000") + " items / 5 days", vm.Metrics[1].Value);
            Assert.All(vm.Metrics.Where(m => m.Label.EndsWith("Time", StringComparison.Ordinal)), m => Assert.EndsWith(" working days", m.Value));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public async Task InvalidInputClearsOldResultsAndReportsActionableError()
    {
        var vm = new MainWindowViewModel();
        await vm.RunAsync();
        vm.DevelopmentWipLimit = "0";
        await vm.RunAsync();
        Assert.True(vm.HasError);
        Assert.Contains("WIP", vm.ErrorMessage);
        Assert.False(vm.HasResults);
        Assert.Empty(vm.Days);
        Assert.True(vm.RunCommand.CanExecute(null));
        Assert.True(vm.ResetCommand.CanExecute(null));
    }

    [Fact]
    public async Task FractionalCommaInputAndSingleDayIncompleteOutputAreSupported()
    {
        var vm = new MainWindowViewModel { DeveloperCapacity = "0,5", DurationDays = "1" };
        await vm.RunAsync();
        Assert.False(vm.HasError);
        Assert.Single(vm.Days);
        Assert.Equal(2.5, vm.SelectedSnapshot!.AvailableDeveloperCapacity);
        Assert.All(vm.WorkItems, w => { Assert.Null(w.DoneDay); Assert.Null(w.LeadTime); Assert.Null(w.CycleTime); });
        Assert.Equal(1, vm.LastDay);
    }

    [Fact]
    public async Task EmptyWorkloadHasAFullZeroHistory()
    {
        var vm = new MainWindowViewModel { NumberOfWorkItems = "0" };
        await vm.RunAsync();
        Assert.False(vm.HasError);
        Assert.Empty(vm.WorkItems);
        Assert.Equal(100, vm.Days.Count);
        Assert.All(vm.FlowStates, state => Assert.Equal(0, state.Count));
        Assert.Equal(0, vm.Result!.AverageLeadTime);
    }
}
