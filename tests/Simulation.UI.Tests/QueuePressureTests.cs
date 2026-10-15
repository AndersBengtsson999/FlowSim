using System.Text.Json;
using Avalonia;
using Simulation.Application;
using Simulation.Core;
using Simulation.Sprites;
using Simulation.UI.Controls.GameWorld;
using Simulation.UI.ViewModels;
using Xunit;

namespace Simulation.UI.Tests;

public sealed class QueuePressureTests
{
    [Theory] [InlineData(0, 0)] [InlineData(2, 2)] [InlineData(4, 4)] [InlineData(35, 11)] [InlineData(10000, 12)] [InlineData(int.MaxValue, 12)]
    public void NonlinearStacksStayBounded(int count, int expected) => Assert.Equal(expected, WorkshopQueue.StackCount(count));

    [Fact] public void StableSlotsGrowAndShrinkWithoutRepackingExistingBoxes()
    {
        var a = new WorkshopQueue("test", "Test", 2, new(670, 471), false);
        var b = a with { Count = 35 };
        Assert.Equal(a.Sprites(), b.Sprites().Take(2)); Assert.True(b.Aggregated);
        Assert.Equal(b.Sprites().Take(2), (b with { Count = 2 }).Sprites());
    }
    [Fact] public void ClassificationRequiresEvidenceAndProtectsScheduledBatching()
    {
        QueuePressure P(int count, double? capacity, int[] counts, bool[] busy, bool scheduled = false) => QueuePressure.Classify(count, capacity, "No testers", counts, busy, "Capacity busy", scheduled);
        Assert.Equal(QueuePressureKind.Normal, P(10000, null, [], []).Kind);
        Assert.Equal(QueuePressureKind.Normal, P(0, 0, [], []).Kind);
        Assert.Equal(QueuePressureKind.Blocked, P(35, 0, [], []).Kind);
        Assert.Equal(QueuePressureKind.Growing, P(35, 2, [32, 33, 34, 35], [false, true, true]).Kind);
        Assert.Equal(QueuePressureKind.Constrained, P(35, 2, [32, 33, 34, 35], [true, true, true]).Kind);
        Assert.Equal(QueuePressureKind.Normal, P(30, 2, [32, 33, 34, 30], [true, true, true]).Kind);
        Assert.Equal(QueuePressureKind.Batching, P(35, 2, [32, 33, 34, 35], [true, true, true], true).Kind);
        Assert.Equal(QueuePressureKind.Blocked, P(35, 0, [], [], true).Kind);
    }
    private static LiveSimulation TestingQueue(int count = 38) => LiveSimulation.Start(BaselineScenario.CreateRequest() with
    {
        NumberOfWorkItems = count, DeveloperCount = 5, TesterCount = 0, DevelopmentEffort = 1, CodeReviewEffort = 1,
        TestingEffort = 2, DevelopmentWipLimit = 20, CodeReviewWipLimit = 20, TestingWipLimit = 3
    }, WorkArrivalMode.FixedBacklog);
    [Fact] public void RealZeroTesterQueueHas35WaitingAndRecoveryClearsWarning()
    {
        var live = TestingQueue(); for (var i = 0; i < 100; i++) live.Step();
        var state = GameSceneState.From(live);
        Assert.Equal(35, state.Areas[3].Waiting); Assert.Equal(QueuePressureKind.Blocked, state.QueuePressures["testing-wait"].Kind);
        Assert.Equal("No testers", state.QueuePressures["testing-wait"].Label);
        Assert.DoesNotContain(new SpriteWorkshop(state).Workers, w => w.Role == "T");
        live.Session.ApplyChanges(live.Session.Configuration with { Team = live.Session.Configuration.Team with { TesterCount = 5 } }, "Recovery");
        Assert.NotEqual(QueuePressureKind.Blocked, GameSceneState.From(live).QueuePressures["testing-wait"].Kind);
        for (var i = 0; i < 100; i++) live.Step();
        Assert.Equal(0, GameSceneState.From(live).Areas[3].Waiting);
        Assert.Equal(QueuePressureKind.Normal, GameSceneState.From(live).QueuePressures["testing-wait"].Kind);
    }
    [Fact] public void AllStackGeometryIsBoundedAndInsideFloorAtHugeCounts()
    {
        var state = GameSceneState.From(TestingQueue()) with { Areas = Enumerable.Range(0, 6).Select(i => new GameArea("Stage", 10000, 10000, "")).ToArray(), WaitingRework = 10000, DependencyWaiting = 10000 };
        var scene = new SpriteWorkshop(state); Assert.InRange(scene.Objects.Count, 0, SpriteWorkshop.MaximumObjects);
        foreach (var queue in scene.Queues)
        foreach (var sprite in queue.Sprites())
        {
            var p = SpriteWorkshop.Projection.Project(sprite.Position with { Elevation = 0 });
            Assert.True(Math.Abs(p.X - 540) / 525 + Math.Abs(p.Y - 302) / 285 < 1, sprite.Id);
            Assert.True(new Rect(0, 0, 1080, 680).Contains(SpritePlacement.Destination(sprite, SpriteCatalog.Assets["package"], SpriteWorkshop.Projection)));
        }
    }
    [Theory] [InlineData(false, 1)] [InlineData(true, 10)]
    public void DisabledAndHighSpeedShowCurrentStackImmediately(bool enabled, double speed)
    {
        var live = TestingQueue(); var first = GameSceneState.From(live); var m = new QueueStackMotion(); m.Observe(first, 0, true, 1);
        var next = first with { Day = 1, WaitingRework = 35 }; m.Observe(next, 0, enabled, speed);
        Assert.Empty(m.Sample(.1)); Assert.Empty(m.Hidden);
        Assert.Equal(11, new SpriteWorkshop(next).Queues.Single(q => q.Id == "rework-wait").Visible);
    }
    [Fact] public void FadeUsesSharedTimeAndRestoreCancelsIt()
    {
        var live = TestingQueue(); var first = GameSceneState.From(live); var m = new QueueStackMotion(); m.Observe(first, 0, true, 1);
        var next = first with { Day = 1, WaitingRework = 35 }; m.Observe(next, 0, true, 1);
        Assert.Equal(11, m.Sample(.1).Count); Assert.Equal(m.Sample(.1), m.Sample(.1));
        Assert.All(m.Sample(.1), x => Assert.InRange(x.Opacity, .01, .99));
        m.Observe(next with { SessionIdentity = Guid.NewGuid() }, .2, true, 1); Assert.Empty(m.Sample(.2));
        m.Observe(next with { Day = 2, WaitingRework = 2 }, .3, true, 1); Assert.Empty(m.Sample(1));
    }
    [Fact] public void ShrinkingStacksFadeOnlyRemovedSlotsAndThenReleaseOverrides()
    {
        var state = GameSceneState.From(TestingQueue()) with { WaitingRework = 35 };
        var motion = new QueueStackMotion(); motion.Observe(state, 0, true, 1);
        motion.Observe(state with { Day = 1, WaitingRework = 2 }, 0, true, 1);
        Assert.Equal(9, motion.Sample(.1).Count);
        Assert.DoesNotContain(motion.Sample(.1), x => x.Id is "queue/rework-wait/0" or "queue/rework-wait/1");
        Assert.True(motion.Sample(.2)[0].Opacity < motion.Sample(.1)[0].Opacity);
        Assert.Empty(motion.Sample(.35)); Assert.Empty(motion.Hidden);
    }
    [Theory] [InlineData(7)] [InlineData(12345)] [InlineData(9876)]
    public void VisualPressureAndTransitionsPreserveAllHistoryAndResults(int seed)
    {
        var a = LiveSimulation.Start(BaselineScenario.DefectsAndReworkExample() with { RandomSeed = seed }, WorkArrivalMode.AlwaysAvailable);
        a.CreateCheckpoint("Shared"); var b = LiveSimulation.Restore(a.Capture()); var motion = new QueueStackMotion();
        for (var day = 1; day <= 100; day++)
        {
            a.Step(); b.Step();
            if (day == 40)
            {
                var change = a.Session.Configuration with { Team = a.Session.Configuration.Team with { TesterCount = 0 } };
                a.Session.ApplyChanges(change, "Pause testing"); b.Session.ApplyChanges(change, "Pause testing");
            }
            motion.Observe(GameSceneState.From(a), day, true, day % 2 == 0 ? 1 : 10); motion.Sample(day + .1); motion.Sample(day + .4);
            Assert.Equal(JsonSerializer.Serialize(b.Capture()), JsonSerializer.Serialize(a.Capture()));
        }
        Assert.Equal(JsonSerializer.Serialize(b.Session.GetResult()), JsonSerializer.Serialize(a.Session.GetResult()));
    }
}
