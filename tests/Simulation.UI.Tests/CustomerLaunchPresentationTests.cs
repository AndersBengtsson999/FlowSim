using Simulation.Application;
using Simulation.Core;
using Simulation.UI.ViewModels;
using Simulation.UI.Controls;
using Xunit;
namespace Simulation.UI.Tests;
public class CustomerLaunchPresentationTests
{
    private static ProductDeliveryState State(int count) => new(Enumerable.Range(1,count).Select(i=>new ProductVersion($"V{i}",$"Version {i}",0,null,null,i*10,[])).ToArray(),
        Enumerable.Range(1,count).Select(i=>new InternalRelease($"IR-{i}",$"V{i}",i*10,[$"I{i}"])).ToArray(),
        Enumerable.Range(1,count).Select(i=>new CustomerLaunch($"CL-{i}",$"V{i}",i*10,[$"I{i}"])).ToArray(),null);
    [Fact] public void HighSpeedBatchRemainsIndividuallyAccessibleAndDoesNotDuplicate()
    {
        var presenter=new CustomerLaunchPresentation();presenter.Observe(State(3));
        for(var i=1;i<=3;i++) {Assert.Equal($"CL-{i}",presenter.Current!.Id);Assert.Contains($"Day {i*10}",presenter.Description);Assert.Contains("1 internal releases",presenter.Description);presenter.Observe(State(3));presenter.DismissCommand.Execute(null);}
        Assert.False(presenter.HasNotification);presenter.Observe(State(3));Assert.False(presenter.HasNotification);
    }
    [Fact] public void RestoreAndLoadDoNotReplayHistoryButNewLaunchIsShown()
    {
        var presenter=new CustomerLaunchPresentation();presenter.Observe(State(2));presenter.Observe(State(1),historical:true);Assert.False(presenter.HasNotification);
        presenter.Observe(State(2));Assert.False(presenter.HasNotification);presenter.Observe(State(3));Assert.Equal("CL-3",presenter.Current!.Id);
        presenter.Reset();presenter.Observe(State(3),historical:true);Assert.False(presenter.HasNotification);
    }
    [Fact] public async Task LiveCompactDetailsPersistAndManualLaunchProducesNoticeWithAnimationOff()
    {
        using var vm=new LiveViewModel();vm.Setup.LaunchAfter="10";vm.Start();vm.Pause();vm.ProductDetailsExpanded=true;vm.AnimateGameActivity=false;
        vm.TargetDay="100";await vm.RunToDayAsync();Assert.Equal(10,vm.Day);Assert.True(vm.ProductDetailsExpanded);Assert.Contains("DECISION REQUIRED",vm.ProductCompactSummary);
        vm.LaunchProductCommand.Execute(null);Assert.True(vm.LaunchNotices.HasNotification);Assert.Contains("Day 10",vm.LaunchNotices.Description);
        vm.Step();Assert.True(vm.ProductDetailsExpanded);Assert.True(vm.LaunchNotices.HasNotification);
        vm.LaunchNotices.DismissCommand.Execute(null);vm.Step();Assert.False(vm.LaunchNotices.HasNotification);
    }
    [Fact] public void UnattendedLaunchUsesSamePresentationAndInternalLabelsAreExplicit()
    {
        var result=new SimulationRunner().Run(new(){SimulationDays=12,ProductLaunch=new("Version 2.0",10,10)});
        var presenter=new CustomerLaunchPresentation();presenter.Observe(result.ProductDelivery);Assert.True(presenter.HasNotification);Assert.Equal(10,presenter.Current!.Day);
        Assert.Equal("Internally released",WorkflowStateNames.Display(WorkItemStatus.Released));
    }
}
