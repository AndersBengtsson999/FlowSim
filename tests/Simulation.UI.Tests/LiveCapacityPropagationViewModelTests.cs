using Simulation.Application;
using Simulation.UI.ViewModels;
using Xunit;

namespace Simulation.UI.Tests;

public sealed class LiveCapacityPropagationViewModelTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DeveloperEditRefreshesRollingCapacityRowsAndRaisesNotifications(bool supplied)
    {
        using var vm = new LiveViewModel();
        if (supplied)
        {
            vm.Setup.NumberOfDevelopers = "1";
            vm.Setup.DevelopmentWipLimit = "40";
            vm.Setup.CodeReviewWipLimit = "40";
            vm.Setup.TestingWipLimit = "10";
            vm.ArrivalRate = "4";
        }
        vm.StartCommand.Execute(null); vm.PauseCommand.Execute(null);
        for (var i = 0; i < 100; i++) vm.StepCommand.Execute(null);
        string TesterValue() => vm.CapacityMetrics.Single(r => r.Label == "Tester utilization").Value;
        var before = TesterValue();
        vm.ChangeCommand.Execute(null);
        vm.ChangeFields.Single(f => f.Label == "Developers").Value = "5000";
        vm.ApplyCommand.Execute(null);
        Assert.False(vm.IsEditing); Assert.Equal(100, vm.Day);
        Assert.Equal(5000, vm.Live!.Session.Configuration.Team.DeveloperCount);
        var notifications = 0;
        vm.PropertyChanged += (_, e) => { if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(vm.CapacityMetrics)) notifications++; };
        var values = new HashSet<string>();
        for (var day = 101; day <= 140; day++)
        {
            var old = notifications;
            vm.StepCommand.Execute(null);
            Assert.True(notifications > old);
            Assert.Equal(day, vm.Day);
            Assert.Equal(5000, vm.Live.Session.Days[^1].AvailableDeveloperCapacity);
            var p = LivePerformance.Rolling(vm.Live.Session);
            Assert.Equal(LivePerformancePresentation.Percent(p.TesterUtilization), TesterValue());
            values.Add(TesterValue());
        }
        if (supplied) { Assert.NotEqual(before, TesterValue()); Assert.True(values.Count > 2); }
        else Assert.Equal(before, TesterValue());
    }
}
