using Avalonia;
using Simulation.Application;
using Simulation.Core;
using Simulation.Sprites;
using Simulation.UI.Controls.GameWorld;
using Simulation.UI.ViewModels;
using Xunit;
namespace Simulation.UI.Tests;

public sealed class WorkflowGeometryTests
{
    [Fact] public void MainFlowUsesExistingDeliveryStagesAndExcludesMaintenance()
    {
        Assert.Equal(new[] { WorkItemStatus.Backlog, WorkItemStatus.Development, WorkItemStatus.CodeReview,
            WorkItemStatus.Testing, WorkItemStatus.ReadyForRelease, WorkItemStatus.Released }, WorkflowGeometry.MainStages);
    }
    [Fact] public void AllQueuesBelongToTheirDownstreamStationAndShareAnimationPorts()
    {
        var live = LiveSimulation.Start(BaselineScenario.CreateRequest(), WorkArrivalMode.AlwaysAvailable); live.Step();
        var scene = new SpriteWorkshop(GameSceneState.From(live));
        foreach (var queue in scene.Queues) Assert.Equal(WorkflowGeometry.Destination(queue.Id), StationMap.HitTest(queue.Position));
        foreach (var status in Enum.GetValues<WorkItemStatus>().Where(s => s != WorkItemStatus.Done))
        {
            var p = PackageMotion.Port(status);
            Assert.Contains(scene.Queues, q => q.Position == p);
        }
    }
    [Theory] [InlineData(WorkItemStatus.CodeReview)] [InlineData(WorkItemStatus.Testing)]
    public void ObservedReturnUsesSeparateRouteAndCorrectEndpoints(WorkItemStatus from)
    {
        var route = WorkflowGeometry.Route(from, WorkItemStatus.WaitingForRework);
        Assert.True(WorkflowGeometry.IsReturn(from, WorkItemStatus.WaitingForRework));
        Assert.Equal(PackageMotion.Port(from), route[0]); Assert.Equal(PackageMotion.Port(WorkItemStatus.WaitingForRework), route[^1]);
        Assert.Contains(new Point(900, 365), route);
        var journey = new ItemJourney("observed", [from, WorkItemStatus.WaitingForRework]);
        Assert.Equal(SpriteWorkshop.Ground(route[0].X, route[0].Y), PackageMotion.Position(journey, 0));
        Assert.Equal(SpriteWorkshop.Ground(route[^1].X, route[^1].Y), PackageMotion.Position(journey, 1));
        Assert.False(WorkflowGeometry.IsReturn(from, WorkItemStatus.ReadyForRelease));
    }
    [Fact] public void ForwardPathsUseTheSameAisleAsFloorMarkings()
    {
        var stages = WorkflowGeometry.MainStages;
        for (var i = 1; i < stages.Length; i++)
        {
            var route = WorkflowGeometry.Route(stages[i - 1], stages[i]);
            Assert.Equal(PackageMotion.Port(stages[i - 1]), route[0]); Assert.Equal(PackageMotion.Port(stages[i]), route[^1]);
            Assert.All(route.Skip(1).SkipLast(1), p => Assert.Contains(p, WorkflowGeometry.Aisle));
        }
    }
    [Theory] [InlineData(350, 130, "Backlog")] [InlineData(910, 205, "Code Review")]
    public void NewBoardFacesAreSelectableAfterCameraTransform(double x, double y, string station)
    {
        var camera = new WorkshopViewport(); camera.Resize(new(1000, 600), new(1080, 620));
        camera.ZoomAt(2, new(500, 300)); camera.Pan(new(-70, 30));
        Assert.Equal(station, StationMap.HitTest(camera.ToWorld(camera.ToScreen(new(x, y)))));
    }
    [Fact] public void StationAccessoriesAreIndependentBoundedSpritesWithAlignedFloorAnchors()
    {
        var live = LiveSimulation.Start(BaselineScenario.CreateRequest(), WorkArrivalMode.AlwaysAvailable);
        var state = GameSceneState.From(live); var scene = new SpriteWorkshop(state);
        foreach (var (id, station) in new[] { ("backlog/planning", "Backlog"), ("review/checklist", "Code Review"), ("testing/diagnostics", "Testing"), ("development/second-monitor", "Development") })
        {
            var item = Assert.Single(scene.Objects, o => o.Id == id);
            var anchor = SpriteWorkshop.Projection.Project(item.Position with { Elevation = 0 });
            Assert.Equal(station, StationMap.HitTest(anchor));
            var bounds = SpritePlacement.Destination(item, SpriteCatalog.Assets[item.AssetId], SpriteWorkshop.Projection);
            Assert.InRange(bounds.Width, 30, 110); Assert.InRange(bounds.Top, 0, 620);
        }
        Assert.InRange(scene.Objects.Count, 0, SpriteWorkshop.MaximumObjects);
        var saved = live.Capture(); _ = new SpriteWorkshop(GameSceneState.From(LiveSimulation.Restore(saved)));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(saved), System.Text.Json.JsonSerializer.Serialize(live.Capture()));
    }
}
