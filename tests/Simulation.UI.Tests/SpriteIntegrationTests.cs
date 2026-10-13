using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.Sprites;
using Simulation.UI.Controls.GameWorld;
using Simulation.UI.ViewModels;
using Xunit;

namespace Simulation.UI.Tests;

public sealed class SpriteIntegrationTests
{
    [Fact]
    public void RenderingModesPreserveCompleteCaptureDraftHistoryAndCheckpoint()
    {
        using var vm = new LiveViewModel(); vm.Start(); for (var i = 0; i < 30; i++) vm.Tick(); vm.Pause();
        vm.Live!.CreateCheckpoint("unchanged"); vm.BeginChange(); vm.Draft.NumberOfTesters = "6"; vm.ChangeLabel = "Pending";
        var before = JsonSerializer.Serialize(vm.Live.Capture()); var scene = vm.GameScene;
        Assert.True(vm.SpriteRendering);
        for (var i = 0; i < 20; i++)
        {
            vm.GameRenderingMode = "Legacy vectors"; Assert.False(vm.SpriteRendering);
            vm.GameRenderingMode = "Sprites"; Assert.True(vm.SpriteRendering);
            var sprite = new SpriteWorkshop(vm.GameScene); Assert.Same(scene, sprite.State);
            Assert.Equal(scene.Areas, sprite.State.Areas);
        }
        vm.GameRenderingMode = "unknown"; Assert.Equal("Sprites", vm.GameRenderingMode);
        Assert.Equal(before, JsonSerializer.Serialize(vm.Live.Capture()));
        Assert.True(vm.IsEditing); Assert.Equal("6", vm.Draft.NumberOfTesters); Assert.Equal("Pending", vm.ChangeLabel);
    }

    [Theory]
    [InlineData(1, 1, 0)] [InlineData(5, 2, 0)] [InlineData(5, 2, 3)] [InlineData(200, 100, 40)]
    public void RealCountsBoundedObjectsAndIndependentRoleCounts(int developers, int testers, int specialists)
    {
        var live = LiveSimulation.Start(BaselineScenario.DefectsAndReworkExample() with
        {
            DeveloperCount = developers, TesterCount = testers, Skills = new(specialists, .3),
            NumberOfWorkItems = 2000, Release = new(ReleaseMode.Scheduled, 2, 5), ResidualDependencies = new(.5, 5)
        }, WorkArrivalMode.FixedBacklog);
        for (var day = 0; day < 120; day++)
        {
            var state = GameSceneState.From(live); var scene = new SpriteWorkshop(state); var d = live.CurrentSnapshot;
            Assert.Equal(developers, state.Developers); Assert.Equal(testers, state.Testers); Assert.Equal(specialists, state.Specialists);
            Assert.Equal(developers, scene.Workers.Count(w => w.Role != "T") + scene.HiddenDevelopers);
            Assert.Equal(testers, scene.Workers.Count(w => w.Role == "T") + scene.HiddenTesters);
            Assert.Equal(Math.Min(3, specialists), scene.Workers.Count(w => w.Role == "S"));
            int Count(string id) => scene.Queues.Single(q => q.Id == id).Count;
            Assert.Equal(d.BacklogCount, Count("backlog")); Assert.Equal(d.DevelopmentCount, Count("development"));
            Assert.Equal(d.ReworkCount, Count("rework-active")); Assert.Equal(d.WaitingForReworkCount, Count("rework-wait"));
            Assert.Equal(d.CodeReviewCount, Count("review-active")); Assert.Equal(d.WaitingForCodeReviewCount, Count("review-wait"));
            Assert.Equal(d.TestingCount, Count("testing-active")); Assert.Equal(d.WaitingForTestingCount, Count("testing-wait"));
            Assert.Equal(d.ReadyForReleaseCount, Count("ready")); Assert.Equal(d.WaitingForDependencyCount, state.DependencyWaiting);
            Assert.Equal(state.ReleasedToday, Count("delivered")); Assert.Equal(d.DoneCount, state.Areas[5].Count);
            Assert.All(scene.Queues, q => { Assert.Equal(q.Count, q.Visible + q.Grouped); Assert.InRange(q.Visible, 0, q.Limit); });
            Assert.InRange(scene.Objects.Count, 0, 70); Assert.InRange(scene.Workers.Count, 0, 5);
            Assert.InRange(scene.Workers.Count(w => w.Role != "T" && w.Visual.State != SpritePose.Idle), 0, state.DeveloperPoseCeiling);
            Assert.InRange(scene.Workers.Count(w => w.Role == "T" && w.Visual.State != SpritePose.Idle), 0, state.TesterPoseCeiling);
            Assert.All(scene.Objects, o => Assert.True(SpriteCatalog.Assets.ContainsKey(o.AssetId)));
            live.Step();
        }
    }

    [Fact]
    public void WorkingPosesNeverExceedFractionalOrReducedAvailability()
    {
        var live = LiveSimulation.Start(BaselineScenario.CreateRequest() with
        { DeveloperAvailability = .1, TesterAvailability = .1 }, WorkArrivalMode.AlwaysAvailable);
        for (var i = 0; i < 40; i++) live.Step();
        var scene = new SpriteWorkshop(GameSceneState.From(live));
        Assert.All(scene.Workers, worker => Assert.Equal(SpritePose.Idle, worker.Visual.State));
    }

    [Fact]
    public void CollaborationIsOnlyIllustrativePairAndNotAUtilizationHeadcount()
    {
        var live = LiveSimulation.Start(BaselineScenario.CreateRequest(), WorkArrivalMode.AlwaysAvailable);
        var original = GameSceneState.From(live);
        var state = original with { DeveloperPoseCeiling = 5, UsedDeveloperCapacity = 5, CollaborationCapacity = .1 };
        var scene = new SpriteWorkshop(state);
        Assert.True(scene.IllustratesCollaboration);
        Assert.Equal(2, scene.Workers.Count(w => w.Visual.State == SpritePose.Collaborating));
        Assert.Equal(1, scene.Workers.Count(w => w.Role != "T" && w.Visual.State == SpritePose.Idle));
        var noPair = new SpriteWorkshop(state with { DeveloperPoseCeiling = 1 });
        Assert.False(noPair.IllustratesCollaboration);
        Assert.Single(noPair.Workers, w => w.Visual.State == SpritePose.Working);
    }

    [Fact]
    public void PoseLayersDepthAndGeometryRemainReusableAndWithinViewport()
    {
        var state = GameSceneState.From(LiveSimulation.Start(BaselineScenario.CreateRequest(), WorkArrivalMode.AlwaysAvailable)) with
        { DeveloperPoseCeiling = 5, TesterPoseCeiling = 2, UsedDeveloperCapacity = 5, UsedTesterCapacity = 2, CollaborationCapacity = 1 };
        var scene = new SpriteWorkshop(state);
        Assert.Equal(scene.Objects, SpritePlacement.InDrawOrder(scene.Objects.Reverse()));
        Assert.All(scene.Objects, item => Assert.True(new Avalonia.Rect(0, 0, 1080, 680).Contains(
            SpritePlacement.Destination(item, SpriteCatalog.Assets[item.AssetId], SpriteWorkshop.Projection)), item.Id));
        var ids = scene.Objects.Select(o => o.Id).ToList();
        Assert.True(ids.IndexOf("developer/0") < ids.IndexOf("development/desk"));
        Assert.True(ids.IndexOf("developer/0/upper-body") > ids.IndexOf("development/computer"));
        Assert.DoesNotContain(typeof(SpriteObject).Assembly.GetReferencedAssemblies(), a => a.Name is "Simulation.Core" or "Simulation.Application");
        Assert.All(SpriteCatalog.Assets.Values, a => Assert.Equal("avares", a.Resource.Scheme));
    }
}
