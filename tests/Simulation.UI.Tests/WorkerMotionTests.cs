using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.Sprites;
using Simulation.UI.Controls.GameWorld;
using Simulation.UI.ViewModels;
using Xunit;

namespace Simulation.UI.Tests;

public sealed class WorkerMotionTests
{
    private static GameSceneState State(Guid id, int day, bool active = true) => new(day,
        [new("Backlog", 0, 0, ""), new("Development", 1, 0, ""), new("Review", 1, 0, ""), new("Testing", 1, 0, ""), new("Ready", 0, 0, ""), new("Release", 0, 0, "")],
        8, 3, 2, 10, .2, 0, "", "", true)
    { SessionIdentity = id, ActiveDevelopment = 1, UsedDevelopmentCapacity = active ? 1 : 0, UsedReviewCapacity = active ? 1 : 0,
      UsedTesterCapacity = active ? 1 : 0, UsedDebtRepaymentCapacity = active ? .5 : 0, CollaborationCapacity = active ? .5 : 0,
      DeveloperPoseCeiling = 4, TesterPoseCeiling = 2 };

    [Fact] public void WaypointsInterpolateInWorldCoordinatesAndFaceTravel()
    {
        WorldPoint[] path = [SpriteWorkshop.Ground(400, 200), SpriteWorkshop.Ground(500, 250), SpriteWorkshop.Ground(600, 200)];
        var middle = WorkerMotion.Interpolate(path, .5, out var facing);
        Assert.Equal(path[1].X, middle.X, 6); Assert.Equal(path[1].Y, middle.Y, 6); Assert.Equal(0, facing);
        var end = WorkerMotion.Interpolate(path, 1, out facing);
        Assert.Equal(path[2], end); Assert.Equal(3, facing);
    }
    [Fact] public void ActivityCausesContinuousApproachAndLatestStateWinsWithoutBacklog()
    {
        var id = Guid.NewGuid(); var motion = new WorkerMotion(); motion.Observe(State(id, 1, false), 0, true);
        var home = motion.Sample(0); motion.Observe(State(id, 2), 0, true);
        var departure = motion.Sample(0); Assert.Equal(home[0].Position, departure[0].Position); Assert.True(departure[0].Walking);
        var walking = motion.Sample(.2); Assert.NotEqual(home[0].Position, walking[0].Position);
        motion.Observe(State(id, 3, false), .2, true);
        Assert.Equal(walking[0].Position, motion.Sample(.2)[0].Position);
        for (var t = .3; t < 12; t += .1) motion.Sample(t);
        Assert.Equal(WorkerMotion.Home(0), motion.Sample(12)[0].Position);
        Assert.All(motion.Sample(12), w => Assert.Equal(SpritePose.Idle, w.Pose));
    }
    [Fact] public void CollaborationAndMaintenanceApproachWithinOneSharedWalkingBudget()
    {
        var id = Guid.NewGuid(); var motion = new WorkerMotion(); motion.Observe(State(id, 1, false), 0, true);
        motion.Observe(State(id, 2), 0, true);
        var poses = new HashSet<SpritePose>(); var walkingIds = new HashSet<string>();
        for (var frame = 0; frame < 1800; frame++)
        {
            var people = motion.Sample(frame / 30.0);
            Assert.InRange(people.Count(w => w.Walking), 0, 1); Assert.Equal(6, people.Count);
            Assert.InRange(people.Count(w => w.Worker.Role != "T" && w.Pose is not (SpritePose.Idle or SpritePose.Walking)), 0, 4);
            Assert.Equal(people.Count, people.Select(w => w.Position).Distinct().Count());
            for (var i = 0; i < people.Count; i++)
                for (var j = i + 1; j < people.Count; j++)
                {
                    var d = SpriteWorkshop.Projection.Project(people[i].Position) - SpriteWorkshop.Projection.Project(people[j].Position);
                    Assert.True(d.X * d.X + d.Y * d.Y > 20 * 20, $"At {frame / 30.0}: {people[i].Worker.Visual.Id} {people[i].Position} vs {people[j].Worker.Visual.Id} {people[j].Position}.");
                }
            foreach (var person in people) { poses.Add(person.Pose); if (person.Walking) walkingIds.Add(person.Worker.Visual.Id); }
        }
        Assert.Contains(SpritePose.Collaborating, poses); Assert.Contains(SpritePose.Maintaining, poses); Assert.Contains(SpritePose.Testing, poses);
        Assert.Contains("developer/1", walkingIds); Assert.Contains("developer/3", walkingIds);
    }
    [Fact] public void SmallTeamReviewVisitsReviewStationAndNeverCreatesPeople()
    {
        var id = Guid.NewGuid(); var motion = new WorkerMotion();
        var scene = State(id, 1) with { Developers = 1, Testers = 0, UsedDevelopmentCapacity = 0, UsedDebtRepaymentCapacity = 0, DeveloperPoseCeiling = 1 };
        motion.Observe(scene, 0, true); var worker = Assert.Single(motion.Sample(0));
        Assert.Equal("review", worker.Station); Assert.Equal(SpritePose.Reviewing, worker.Pose);
    }
    [Theory] [InlineData(10, true)] [InlineData(1, false)]
    public void FastPlaybackAndReducedMotionSynchronizeWithoutStaleWalking(double speed, bool animate)
    {
        var id = Guid.NewGuid(); var motion = new WorkerMotion(); motion.Observe(State(id, 1, false), 0, true);
        motion.Observe(State(id, 2), 0, true); motion.Sample(.2);
        motion.Observe(State(id, 3), .3, animate, speed);
        Assert.Equal(0, motion.MovingCount); Assert.DoesNotContain(motion.Sample(.3, false), w => w.Walking);
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public void RestoreCancelsSameDayAndBackwardMovement(bool sameDay)
    {
        var id = Guid.NewGuid(); var motion = new WorkerMotion(); motion.Observe(State(id, 1, false), 0, true);
        motion.Observe(State(id, 2), 0, true); motion.Sample(.1);
        motion.Observe(State(Guid.NewGuid(), sameDay ? 2 : 1), .2, true);
        Assert.Equal(0, motion.MovingCount); Assert.Equal(SpritePose.Collaborating, motion.Sample(.2)[0].Pose);
    }
    [Fact] public void FreezeAndLifecycleSynchronizationAreDeterministic()
    {
        var scene = State(Guid.NewGuid(), 2); var a = new WorkerMotion(); var b = new WorkerMotion();
        a.Observe(scene, 0, true); b.Observe(scene, 0, true);
        for (var f = 0; f < 1800; f++) Assert.Equal(a.Sample(f / 30.0), b.Sample(f / 30.0));
        var frozen = a.Sample(60, false); Assert.Equal(frozen, a.Sample(60, false));
        a.Synchronize(60); Assert.Equal(0, a.MovingCount); a.Observe(null, 60, false); Assert.Equal(0, a.Count);
    }
    [Theory] [InlineData(1)] [InlineData(2)] [InlineData(200)]
    public void HeadcountAndCapacityStayBoundedDuringChanges(int count)
    {
        var id = Guid.NewGuid(); var motion = new WorkerMotion();
        for (var day = 1; day < 20; day++)
        {
            motion.Observe(State(id, day) with { Developers = count, Testers = count, DeveloperPoseCeiling = day % 3, TesterPoseCeiling = 1 }, day, true);
            var workers = motion.Sample(day);
            Assert.Equal(Math.Min(4, count) + Math.Min(2, count), workers.Count);
            Assert.InRange(workers.Count(w => w.Worker.Role != "T" && w.Pose is not (SpritePose.Idle or SpritePose.Walking)), 0, day % 3);
        }
    }
    [Theory] [InlineData(7)] [InlineData(12345)] [InlineData(9876)]
    public void CompleteStateHistoryCheckpointsAndResultsMatchWithLivingOfficeDisabled(int seed)
    {
        var a = LiveSimulation.Start(BaselineScenario.DefectsAndReworkExample() with { RandomSeed = seed, Skills = new(2, .3), Release = new(ReleaseMode.Scheduled, 2, 5) }, WorkArrivalMode.AlwaysAvailable);
        a.CreateCheckpoint("Shared"); var b = LiveSimulation.Restore(a.Capture()); var motion = new WorkerMotion();
        for (var day = 1; day <= 100; day++)
        {
            a.Step(); b.Step();
            if (day == 40) { a.Session.ApplyChanges(a.Session.Configuration with { Release = new(ReleaseMode.FlowBased, 3) }, "Release change"); b.Session.ApplyChanges(b.Session.Configuration with { Release = new(ReleaseMode.FlowBased, 3) }, "Release change"); }
            motion.Observe(GameSceneState.From(a), day * 2, true, day % 5 == 0 ? 10 : 1);
            for (var frame = 0; frame < 20; frame++)
                foreach (var worker in motion.Sample(day * 2 + frame * .1)) _ = WorkerMotion.Sprites(worker, day * 2 + frame * .1).ToArray();
            Assert.Equal(JsonSerializer.Serialize(b.Capture()), JsonSerializer.Serialize(a.Capture()));
        }
        Assert.Equal(JsonSerializer.Serialize(b.Session.GetResult()), JsonSerializer.Serialize(a.Session.GetResult()));
    }
}
