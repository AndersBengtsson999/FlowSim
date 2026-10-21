using Simulation.Application;
using Simulation.Core;
using Simulation.Infrastructure;
using Simulation.UI.ViewModels;
using Xunit;
namespace Simulation.UI.Tests;
public class ProductLaunchViewModelTests
{
    private static LiveViewModel Create()
    {
        var vm=new LiveViewModel();vm.Setup.LaunchMode="One-time";vm.Setup.LaunchAfter="6";vm.Setup.LaunchUnit="Sprints";vm.Setup.LaunchSprintLength="10";vm.Start();vm.Pause();return vm;
    }
    [Fact] public async Task AcceleratedLiveStopsAt60AndCannotBypassTheDecision()
    {
        using var vm=Create();vm.TargetDay="200";await vm.RunToDayAsync();Assert.Equal(60,vm.Day);Assert.True(vm.LaunchDecisionPending);Assert.False(vm.CanResume);Assert.False(vm.IsRunning);
        vm.Resume();vm.Tick();vm.Step();await vm.RunToDayAsync();Assert.Equal(60,vm.Day);Assert.False(vm.ResumeCommand.CanExecute(null));Assert.False(vm.StepCommand.CanExecute(null));
        vm.LaunchDelay="2";vm.PlanningUnit="Sprints";vm.PlanLaunchCommand.Execute(null);Assert.False(vm.LaunchDecisionPending);Assert.True(vm.CanResume);
        await vm.RunToDayAsync();Assert.Equal(80,vm.Day);Assert.True(vm.LaunchDecisionPending);
        vm.LaunchProductCommand.Execute(null);Assert.Equal(80,Assert.Single(vm.Live!.Session.ProductDelivery.CustomerLaunches).Day);Assert.False(vm.LaunchDecisionPending);
        vm.LaunchProductCommand.Execute(null);Assert.Single(vm.Live.Session.ProductDelivery.CustomerLaunches);vm.Step();Assert.Equal(81,vm.Day);
    }
    [Fact] public void TicksAndRestoreRespectSamePendingBoundary()
    {
        using var vm=Create();vm.Speed=10;vm.Resume();for(var i=0;i<100;i++)vm.Tick();Assert.Equal(60,vm.Day);Assert.False(vm.IsRunning);
        vm.CheckpointCommand.Execute(null);vm.SelectedCheckpoint=vm.Checkpoints.Single();
        vm.LaunchProductCommand.Execute(null);vm.Step();Assert.Equal(61,vm.Day);vm.RestoreCommand.Execute(null);Assert.Equal(60,vm.Day);Assert.True(vm.LaunchDecisionPending);Assert.False(vm.CanResume);
        var json=LiveSessionJson.Save(vm.Live!);vm.Reset();Assert.False(vm.LaunchDecisionPending);vm.Load(LiveSessionJson.Load(json));Assert.True(vm.LaunchDecisionPending);Assert.False(vm.CanResume);
    }
    [Fact] public void InitialPlanRoundTripsAndNeverUsesCalendarDates()
    {
        var owner=new MainWindowViewModel();owner.LaunchMode="One-time";owner.ProductVersionName="Version X";owner.LaunchAfter="6";owner.LaunchUnit="Sprints";owner.LaunchSprintLength="10";
        var config=owner.CaptureSetup();owner.LoadConfiguration(config);Assert.Equal(config.ProductLaunch,owner.CaptureSetup().ProductLaunch);
        Assert.Equal(60,ProductLaunchSettings.TargetDay(0,6,config.ProductLaunch.Unit,config.ProductLaunch.SprintLength));
        owner.LaunchMode="Manual";owner.LaunchAfter="";Assert.Null(owner.CaptureSetup().ProductLaunch.After);
    }
    [Fact] public void InvalidPostponementDoesNotResolvePendingDecision()
    {
        using var vm=Create();vm.Resume();for(var i=0;i<60;i++)vm.Tick();vm.LaunchDelay="x";vm.PlanLaunchCommand.Execute(null);Assert.True(vm.LaunchDecisionPending);Assert.Equal(60,vm.Day);
    }
}
