using Simulation.Core;
using Simulation.UI.ViewModels;
using Xunit;
namespace Simulation.UI.Tests;
public class RecurringLaunchViewModelTests
{
    [Fact] public async Task AcceleratedLiveStopsThreeTimesPreservesDetailsAndShowsEveryLaunch()
    {
        using var vm=new LiveViewModel();vm.Setup.LaunchMode="Recurring";vm.Setup.LaunchAfter="6";vm.Setup.LaunchUnit="Sprints";vm.Setup.LaunchSprintLength="10";
        vm.Start();vm.Pause();vm.TargetDay="200";vm.ProductDetailsExpanded=true;
        foreach(var day in new[]{60,120,180})
        {
            await vm.RunToDayAsync();Assert.Equal(day,vm.Day);Assert.True(vm.LaunchDecisionPending);Assert.True(vm.ProductDetailsExpanded);
            Assert.Contains("Recurring: every 6 sprints",vm.ProductCompactSummary);
            Assert.False(vm.ResumeCommand.CanExecute(null));Assert.False(vm.StepCommand.CanExecute(null));
            vm.LaunchProductCommand.Execute(null);Assert.Equal(day,vm.LaunchNotices.Current!.Day);
            Assert.Equal(day/60,vm.Live!.Session.ProductDelivery.CustomerLaunches.Count);
            Assert.Contains($"actual Day {day}",vm.LiveCustomerHistory);
            vm.LaunchNotices.DismissCommand.Execute(null);Assert.False(vm.LaunchNotices.HasNotification);
        }
        await vm.RunToDayAsync();Assert.Equal(200,vm.Day);Assert.Contains("Day 240",vm.ProductCompactSummary);
        Assert.Equal(3,vm.Live!.Session.ProductDelivery.CustomerLaunches.Count);
    }
    [Fact] public void ModeSelectionIsExplicitAndManualDoesNotUseStaleInterval()
    {
        var setup=new MainWindowViewModel();setup.LaunchAfter="6";setup.LaunchMode="Manual";
        Assert.Equal(LaunchPlanningMode.Manual,setup.CaptureSetup().ProductLaunch.Schedule.Mode);
        setup.LaunchMode="Recurring";setup.LaunchUnit="Sprints";var request=setup.CaptureSetup();setup.LoadConfiguration(request);
        Assert.Equal("Recurring",setup.LaunchMode);Assert.Equal(request.ProductLaunch,setup.CaptureSetup().ProductLaunch);
    }
    [Fact] public async Task ExpandedStrategyAndPostponementHaveSeparateEffects()
    {
        using var vm=new LiveViewModel();vm.Start();vm.Pause();
        vm.LaunchModeDraft="Recurring";vm.LaunchInterval="6";vm.PlanningUnit="Sprints";vm.PlanningSprintLength="10";vm.ConfigureLaunchCommand.Execute(null);
        vm.TargetDay="200";await vm.RunToDayAsync();Assert.Equal(60,vm.Day);
        vm.LaunchDelay="2";vm.PlanLaunchCommand.Execute(null);await vm.RunToDayAsync();Assert.Equal(80,vm.Day);
        vm.LaunchProductCommand.Execute(null);Assert.Equal(140,vm.Live!.Session.ProductDelivery.Versions[^1].PlannedDay);
        vm.LaunchModeDraft="Manual";vm.ConfigureLaunchCommand.Execute(null);Assert.Contains("Not scheduled",vm.ProductCompactSummary);
        Assert.Null(vm.Live.Session.ProductDelivery.Versions[^1].PlannedDay);
    }
}
