using System.Text.Json;
using Avalonia;
using Simulation.Application;
using Simulation.Core;
using Simulation.UI.Controls.GameWorld;
using Simulation.UI.ViewModels;
using Xunit;
namespace Simulation.UI.Tests;
public class WorkflowInspectionTests
{
    private static LiveSimulation Start() => LiveSimulation.Start(BaselineScenario.CreateRequest() with
    { NumberOfWorkItems = 38, DeveloperCount = 5, TesterCount = 0, DevelopmentEffort = 1, CodeReviewEffort = 1,
      DevelopmentWipLimit = 20, CodeReviewWipLimit = 20, TestingWipLimit = 3 }, WorkArrivalMode.FixedBacklog);
    [Fact] public void QueueTargetsSurviveCameraTransformsAndNeverOverlap()
    {
        var camera = new WorkshopViewport(); camera.Resize(new(850, 430), new(1080,620)); camera.ZoomAt(1.8,new(400,200)); camera.Pan(new(70,-30));
        foreach (var r in StationMap.QueueRegions)
        {
            var center = new Point(r.Points.Average(p=>p.X),r.Points.Average(p=>p.Y));
            Assert.Equal(r.Name,StationMap.InspectAt(camera.ToWorld(camera.ToScreen(center))));
            Assert.Single(StationMap.QueueRegions,q=>q.Contains(center));
        }
        Assert.Equal("Testing",StationMap.InspectAt(new(710,404)));
    }
    [Fact] public void ExactMembershipDurationsAndObservationsDoNotMutateState()
    {
        var live=Start(); for(int i=0;i<30;i++)live.Step();
        var before=JsonSerializer.Serialize(live.Capture()); var scene=GameSceneState.From(live);
        var queue=StationDetails.For("Testing queue",scene);
        Assert.Equal(live.CurrentSnapshot.WaitingForTestingCount,queue.Items.Count);
        Assert.All(queue.Items,i=>Assert.Equal(WorkItemStatus.WaitingForTesting,i.State));
        Assert.Contains("No testers",queue.Explanation);
        foreach(var item in live.Session.WorkItems.Where(w=>w.State==WorkItemStatus.WaitingForTesting))
        {
            var age=30-item.Transitions.Last().Day;
            Assert.Contains($"Current state: {age} days",queue.Items.Single(i=>i.Id==item.Id).Description);
        }
        Assert.Equal(live.CurrentSnapshot.TestingCount+queue.Items.Count,StationDetails.For("Testing",scene).Items.Count);
        Assert.Equal(before,JsonSerializer.Serialize(live.Capture()));
    }
    [Fact] public void RestoreLoadAndResetCannotRetainCurrentItems()
    {
        using var vm=new LiveViewModel();vm.Load(Start());for(int i=0;i<30;i++)vm.Step();
        var saved=Simulation.Infrastructure.LiveSessionJson.Save(vm.Live!);var expected=JsonSerializer.Serialize(GameSceneState.From(vm.Live).InspectionItems);
        vm.Live!.Session.ApplyChanges(vm.Live.Session.Configuration with {Team=vm.Live.Session.Configuration.Team with{TesterCount=5}});
        for(int i=0;i<10;i++)vm.Step();
        Assert.NotEqual(expected,JsonSerializer.Serialize(GameSceneState.From(vm.Live).InspectionItems));
        vm.Load(Simulation.Infrastructure.LiveSessionJson.Load(saved));
        Assert.Equal(expected,JsonSerializer.Serialize(GameSceneState.From(vm.Live).InspectionItems));
        vm.Reset();Assert.Empty(GameSceneState.From(vm.Live).InspectionItems);
    }
    [Fact] public void IncompleteHistoryIsExplicitlyUnavailable()
    {
        var live=Start();for(int i=0;i<30;i++)live.Step();var saved=live.Capture();
        var broken=saved.State.WorkItems.Select(w=>w.State==WorkItemStatus.Backlog?w:w with{Transitions=[]}).ToArray();
        var restored=LiveSimulation.Restore(saved with{State=saved.State with{WorkItems=broken}});
        var item=GameSceneState.From(restored).InspectionItems.First(i=>i.State!=WorkItemStatus.Backlog);
        Assert.Contains("Current state: N/A",item.Description);Assert.Contains("Queue waiting: N/A",item.Description);
    }
    [Fact] public void ResidualDependencySubsetAndMissingCapacityAreExplicit()
    {
        var live=LiveSimulation.Start(BaselineScenario.CreateRequest() with
        { NumberOfWorkItems=12, ResidualDependencies=new(1,100) },WorkArrivalMode.FixedBacklog);
        live.Step();var scene=GameSceneState.From(live);
        var queue=StationDetails.For("Dependency queue",scene);
        Assert.Equal(live.CurrentSnapshot.WaitingForDependencyCount,queue.Items.Count);
        Assert.All(queue.Items,i=>Assert.Contains("Residual dependency unresolved",i.Description));
        Assert.Contains("Dependency wait",queue.Explanation);
        var zero=StationDetails.For("Testing",GameSceneState.From(Start()));
        Assert.Contains(zero.Rows,r=>r.Label=="Tester utilization · last day" && r.Value=="N/A");
    }

}
