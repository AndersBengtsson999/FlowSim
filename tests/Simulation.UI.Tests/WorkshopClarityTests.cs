using Avalonia;
using Simulation.Application;
using Simulation.Core;
using Simulation.Infrastructure;
using Simulation.UI.Controls.GameWorld;
using Simulation.UI.ViewModels;
using Xunit;
namespace Simulation.UI.Tests;

public class WorkshopClarityTests
{
    [Theory]
    [InlineData(700,420)]
    [InlineData(1200,700)]
    public void FitIncludesMarginsAndManualCameraSurvivesResize(int width,int height)
    {
        var camera=new WorkshopViewport();
        camera.Resize(new(width,height),new(1080,620),12);
        var corner=camera.ToScreen(default);
        var end=camera.ToScreen(new(1080,620));
        Assert.True(corner.X>=11.99 && corner.Y>=11.99);
        Assert.True(end.X<=width-11.99 && end.Y<=height-11.99);
        camera.ZoomAt(1.5,new(width/2d,height/2d));
        camera.Pan(new(20,10));
        var center=camera.ToWorld(new(width/2d,height/2d));
        camera.Resize(new(width+100,height+100),new(1080,620),12);
        Assert.Equal(1.5,camera.Zoom);
        Assert.Equal(center.X,camera.ToWorld(new((width+100)/2d,(height+100)/2d)).X,8);
        Assert.True(camera.IsManual);
    }
    [Fact]
    public void VehicleBoundsFollowRenderedMovement()
    {
        var parked=VehicleInspection.Bounds(null);
        var moving=VehicleInspection.Bounds(new(3,40,40,ReleasePhase.Away,1,1));
        Assert.Equal(parked.Width,moving.Width);
        Assert.Equal(parked.X-100,moving.X,8);
        Assert.Equal(parked.Y+50,moving.Y,8);
        Assert.Equal(parked,VehicleInspection.Bounds(new(3,40,40,ReleasePhase.FeedbackOnly,1,1)));
    }
    [Fact]
    public void SwitchingViewsNotifiesProductHeaderVisibility()
    {
        using var vm=new LiveViewModel();
        vm.Load(LiveSimulation.Start(new()));
        var changed=new List<string?>();
        vm.PropertyChanged+=(_,e)=>changed.Add(e.PropertyName);
        vm.Visualization="Game View";
        Assert.False(vm.ShowFlowProduct);
        Assert.Contains(nameof(vm.ShowFlowProduct),changed);
        vm.Visualization="Flow Board";
        Assert.True(vm.ShowFlowProduct);
    }
    [Fact]
    public void ReleaseInspectionUsesAwaitingAllocationAndRefreshesAfterLaunchAndRestore()
    {
        var live=LiveSimulation.Start(new(){NumberOfWorkItems=40,DeveloperCount=5,TesterCount=1,
            DevelopmentEffort=1,CodeReviewEffort=1,TestingEffort=2,Release=new(ReleaseMode.Scheduled,8,10)},WorkArrivalMode.ContinuousArrival,2);
        using var vm=new LiveViewModel();vm.Load(live);
        Assert.Equal(0,vm.ReleaseEconomicContext!.AwaitingItems);
        for(int i=0;i<42;i++)live.Step();
        vm.Load(live);
        var saved=LiveSessionJson.Save(live);
        var stock=vm.LaunchCostHistory.Current;
        var info=vm.ReleaseEconomicContext!;
        Assert.True(stock.WorkInProgress>0 && info.AwaitingCost>0);
        Assert.Equal(stock.AwaitingLaunch,info.AwaitingCost);
        Assert.Equal(GameSceneState.From(live).AwaitingCustomerLaunch,info.AwaitingItems);
        Assert.Equal(saved,LiveSessionJson.Save(live));
        vm.LaunchProductCommand.Execute(null);
        Assert.Equal(0,vm.ReleaseEconomicContext!.AwaitingCost);
        Assert.Equal(0,GameSceneState.From(live).AwaitingCustomerLaunch);
        Assert.Equal(1,vm.ReleaseEconomicContext.Launches);
        Assert.Equal(stock.WorkInProgress,vm.LaunchCostHistory.Current.WorkInProgress);
        vm.Load(LiveSessionJson.Load(saved));
        Assert.Equal(info,vm.ReleaseEconomicContext);
    }
}
