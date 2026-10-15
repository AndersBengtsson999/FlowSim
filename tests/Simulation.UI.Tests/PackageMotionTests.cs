using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.Sprites;
using Simulation.UI.Controls.GameWorld;
using Simulation.UI.ViewModels;
using Xunit;

namespace Simulation.UI.Tests;

public sealed class PackageMotionTests
{
    private static GameSceneState State(int day, Guid session, params ItemJourney[] journeys) => new(day, [], 0, 0, 0, 0, 0, 0, "", "", true)
    { SessionIdentity = session, Journeys = journeys };
    private static readonly ItemJourney Journey = new("real-id", [WorkItemStatus.Development, WorkItemStatus.WaitingForCodeReview, WorkItemStatus.CodeReview]);
    [Fact] public void ExactIdentityAndWithinDayOrderComeFromHistory()
    {
        var live = LiveSimulation.Start(LiveSimulation.Demo, WorkArrivalMode.AlwaysAvailable);
        for (var day = 0; day < 30; day++)
        {
            live.Step(); var scene = GameSceneState.From(live);
            foreach (var journey in scene.Journeys)
            {
                var transitions = live.Session.WorkItems.Single(w => w.Id == journey.ItemId).Transitions.Where(t => t.Day == scene.Day).ToArray();
                Assert.Equal(new[] { transitions[0].From }.Concat(transitions.Select(t => t.To)), journey.Stages);
            }
            Assert.Equal(live.Session.WorkItems.Count(w => w.Transitions.LastOrDefault()?.Day == scene.Day), scene.ChangedItems);
        }
    }
    [Fact] public void MultipleTransitionsKeepOneIdentityAndReachExactEndpoint()
    {
        var id = Guid.NewGuid(); var motion = new PackageMotion();
        motion.Observe(State(1, id), 0, true, 1); motion.Observe(State(2, id, Journey), 0, true, 1);
        Assert.Equal("moving/real-id", Assert.Single(motion.Sample(.4)).Id);
        Assert.Equal(SpriteWorkshop.Ground(553, 211), PackageMotion.Position(Journey, 0));
        Assert.Equal(SpriteWorkshop.Ground(850, 292), PackageMotion.Position(Journey, 1));
        Assert.Empty(motion.Sample(1));
    }
    [Fact] public void PauseFreezesTimeAndDoesNotCreateTransitions()
    {
        var id = Guid.NewGuid(); var motion = new PackageMotion(); var clock = new PresentationClock();
        clock.SetRunning(true, TimeSpan.Zero);
        motion.Observe(State(1, id), 0, true, 1); motion.Observe(State(2, id, Journey), 0, true, 1);
        clock.SetRunning(false, TimeSpan.FromSeconds(.2)); var paused = motion.Sample(clock.Seconds);
        clock.Sample(TimeSpan.FromSeconds(10)); Assert.Equal(paused, motion.Sample(clock.Seconds));
        clock.SetRunning(true, TimeSpan.FromSeconds(10)); clock.Sample(TimeSpan.FromSeconds(10.2));
        Assert.NotEqual(paused[0].Position, motion.Sample(clock.Seconds)[0].Position);
        motion.Observe(State(3, id, Journey), clock.Seconds, false, 1); Assert.Equal(0, motion.Count);
    }
    [Theory] [InlineData(0)] [InlineData(2)] [InlineData(5)]
    public void DiscontinuousDaysCancel(int nextDay)
    {
        var id = Guid.NewGuid(); var motion = new PackageMotion(); motion.Observe(State(1, id), 0, true, 1);
        motion.Observe(State(2, id, Journey), 0, true, 1);
        motion.Observe(State(nextDay, Guid.NewGuid(), Journey), .1, true, 1); Assert.Equal(0, motion.Count);
    }
    [Fact] public void RestoreOfSameDayHasNewIdentity()
    {
        var live = LiveSimulation.Start(LiveSimulation.Demo, WorkArrivalMode.AlwaysAvailable); live.Step();
        var checkpoint = live.CreateCheckpoint("test"); var before = GameSceneState.From(live);
        live.RestoreCheckpoint(checkpoint.Id); var after = GameSceneState.From(live);
        Assert.NotEqual(before.SessionIdentity, after.SessionIdentity);
        Assert.Equal(before.Day, after.Day);
    }
    [Fact] public void HighSpeedSkipsAndEachDayReplacesBoundedBatch()
    {
        var id = Guid.NewGuid(); var motion = new PackageMotion(); motion.Observe(State(1, id), 0, true, 1);
        var many = Enumerable.Range(0, 1000).Select(i => Journey with { ItemId = i.ToString() }).ToArray();
        motion.Observe(State(2, id, many), 0, true, 1); Assert.Equal(12, motion.Count);
        motion.Observe(State(3, id, Journey), .1, true, 1); Assert.Equal(1, motion.Count);
        motion.Observe(State(4, id, many), .2, true, 10); Assert.Equal(0, motion.Count);
        motion.Observe(State(6, id, Journey), .3, true, 1); Assert.Equal(0, motion.Count);
        motion.Cancel(); Assert.Empty(motion.Sample(.4));
    }
    [Fact] public void ReworkAndReleaseJourneysUseActualHistory()
    {
        var live = LiveSimulation.Start(BaselineScenario.DefectsAndReworkExample() with { NumberOfWorkItems = 100, Release = new(ReleaseMode.Scheduled, 2, 5) }, WorkArrivalMode.FixedBacklog);
        var observed = new HashSet<WorkItemStatus>();
        for (var i = 0; i < 200; i++)
        {
            live.Step();
            foreach (var journey in GameSceneState.From(live).Journeys)
                foreach (var stage in journey.Stages) observed.Add(stage);
        }
        Assert.Contains(WorkItemStatus.WaitingForRework, observed);
        Assert.Contains(WorkItemStatus.Rework, observed);
        Assert.Contains(WorkItemStatus.ReadyForRelease, observed);
        Assert.Contains(WorkItemStatus.Released, observed);
    }
    [Fact] public void SameSnapshotDoesNotReplayAndBackwardDaySynchronizes()
    {
        var id = Guid.NewGuid(); var motion = new PackageMotion(); var next = State(2, id, Journey);
        motion.Observe(State(1, id), 0, true, 1); motion.Observe(next, 0, true, 1);
        motion.Sample(1); motion.Observe(next, 1, true, 1); Assert.Equal(0, motion.Count);
        motion.Observe(State(1, id, Journey), 2, true, 1); Assert.Equal(0, motion.Count);
    }
    [Fact] public void QueueCountsRemainAuthoritativeAndDependencySubsetIsNotDuplicated()
    {
        var live = LiveSimulation.Start(LiveSimulation.Demo, WorkArrivalMode.AlwaysAvailable); live.Step();
        var state = GameSceneState.From(live) with { DependencyWaiting = 500, Areas = [new("Backlog", 0, 2000, ""), new("Dev", 0, 0, ""), new("Review", 0, 900, ""), new("Test", 0, 1000, ""), new("Ready", 0, 800, ""), new("Release", 0, 0, "")] };
        var queues = new SpriteWorkshop(state).Queues;
        Assert.Equal(2000, queues.Where(q => q.Id is "backlog" or "dependency").Sum(q => q.Count));
        Assert.Equal(900, queues.Single(q => q.Id == "review-wait").Count);
        Assert.All(queues, q => Assert.InRange(q.Visible, 0, q.Active ? q.Limit : 12));
    }
    [Theory] [InlineData(7)] [InlineData(12345)] [InlineData(9876)]
    public void PresentationPreservesCompleteStateAndHistory(int seed)
    {
        var a = LiveSimulation.Start(LiveSimulation.Demo with { RandomSeed = seed }, WorkArrivalMode.AlwaysAvailable);
        a.CreateCheckpoint("Shared checkpoint");
        var b = LiveSimulation.Restore(a.Capture()); var motion = new PackageMotion();
        for (var i = 0; i < 100; i++)
        {
            a.Step(); b.Step();
            if (i == 40) { a.Session.ApplyChanges(a.Session.Configuration with { WorkItemsPerDay = 3 }, "Intervention"); b.Session.ApplyChanges(b.Session.Configuration with { WorkItemsPerDay = 3 }, "Intervention"); }
            motion.Observe(GameSceneState.From(a), i, true, i % 3 == 0 ? 10 : 1);
            for (var frame = 0; frame < 5; frame++) motion.Sample(i + frame * .1);
            Assert.Equal(JsonSerializer.Serialize(b.Capture()), JsonSerializer.Serialize(a.Capture()));
        }
    }
}
