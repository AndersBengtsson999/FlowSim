using Simulation.UI.ViewModels;
using Xunit;

namespace Simulation.UI.Tests;

public sealed class InterventionBoundaryViewModelTests
{
    [Fact]
    public void VisiblePeriodStringsAndPartialStatusRemainAnchoredDuringLiveProgress()
    {
        using var vm = new LiveViewModel(); vm.Start(); vm.Pause();
        for (var i = 0; i < 100; i++) vm.Step();
        vm.BeginChange(); vm.ChangeFields.Single(f => f.Label == "Testers").Value = "3"; vm.ApplyChanges();
        var change = vm.SelectedIntervention;
        foreach (var day in new[] { 100, 101, 110, 119, 120, 140 })
        {
            while (vm.Day < day) vm.Step();
            Assert.Equal("Before: Days 81–100 · 20 of 20 days available", vm.BeforePeriod);
            Assert.Equal($"After: Days 101–120 · {Math.Min(day - 100, 20)} of 20 days available", vm.AfterPeriod);
            Assert.StartsWith(day < 120 ? "Incomplete periods" : "Complete periods", vm.ComparisonStatus);
            Assert.Same(change, vm.SelectedIntervention);
            if (day < 120) Assert.Equal("", vm.Observations);
        }
        Assert.Contains("Days 121–140", vm.WindowLabel);
        var period = vm.Comparison;
        foreach (var (window, first, last) in new[] { (10, 91, 110), (50, 51, 150), (100, 1, 200) })
        {
            vm.RollingWindow = window;
            Assert.Contains($"Days {first}–100", vm.BeforePeriod);
            Assert.Contains($"Days 101–{last}", vm.AfterPeriod);
        }
        vm.RollingWindow = 20; Assert.Equal(period, vm.Comparison);
    }
}
