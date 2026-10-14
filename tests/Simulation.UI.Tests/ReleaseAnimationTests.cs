using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.Sprites;
using Simulation.UI.Controls.GameWorld;
using Simulation.UI.ViewModels;
using Xunit;

namespace Simulation.UI.Tests;

public sealed class ReleaseAnimationTests
{
    private static GameSceneState State(Guid id, int day, int total, int today) => new(day,
        [new("Backlog", 0, 0, ""), new("Development", 1, 0, ""), new("Review", 1, 0, ""), new("Testing", 1, 0, ""), new("Ready", 0, 0, ""), new("Release", total, 0, "")],
        8, 3, 2, 10, .2, today, "", "", true)
    { SessionIdentity = id, ActiveDevelopment = 1, UsedDevelopmentCapacity = 1, UsedReviewCapacity = 1, UsedTesterCapacity = 1, UsedDebtRepaymentCapacity = .5, CollaborationCapacity = .5, DeveloperPoseCeiling = 4, TesterPoseCeiling = 2 };

    [Fact] public void LoadingDepartureAndReturnFollowActualCount()
    {
        var id = Guid.NewGuid(); var m = new PackageMotion();
        m.Observe(State(id, 4, 10, 0), 0, true, 1); m.Observe(State(id, 5, 17, 7), 0, true, 1);
        Assert.Equal(7, m.Release(0)!.Count); Assert.Equal(ReleasePhase.Loading, m.Release(.9)!.Phase);
        Assert.Equal(ReleasePhase.Departing, m.Release(1.3)!.Phase); Assert.InRange(m.Release(1.4)!.Offset, 0.01, .99);
        Assert.Equal(ReleasePhase.Away, m.Release(1.9)!.Phase);
        Assert.Equal(ReleasePhase.Returning, m.Release(2.2)!.Phase);
        Assert.Null(m.Release(2.8));
    }
    [Fact] public void FlowEventsCoalesceWithoutRestartingVehicleOrDuplicatingDay()
    {
        var id = Guid.NewGuid(); var m = new PackageMotion(); m.Observe(State(id, 0, 0, 0), 0, true, 1);
        m.Observe(State(id, 1, 2, 2), 0, true, 1); var next = State(id, 2, 5, 3);
        m.Observe(next, 1, true, 1); m.Observe(next with { }, 1.1, true, 1);
        Assert.Equal(5, m.Release(1.3)!.Count); Assert.Equal(ReleasePhase.Departing, m.Release(1.3)!.Phase);
        Assert.Contains("Days 1–2", m.Release(1.3)!.Caption); Assert.Null(m.Release(3));
    }
    [Fact] public void ConfiguredReleaseDayWithoutReleasedItemsDoesNotDispatch()
    {
        var id = Guid.NewGuid(); var m = new PackageMotion(); m.Observe(State(id, 4, 0, 0), 0, true, 1);
        m.Observe(State(id, 5, 0, 0), 1, true, 1); Assert.Null(m.Release(1));
        m.Observe(State(id, 6, 0, 5), 2, true, 1); Assert.Null(m.Release(2));
    }
    [Theory] [InlineData(ReleaseMode.Scheduled)] [InlineData(ReleaseMode.FlowBased)]
    public void RealReleaseEventsRespectSchedulingAndCapacity(ReleaseMode mode)
    {
        var live = LiveSimulation.Start(LiveSimulation.Demo with { Release = new(mode, 2, 5) }, WorkArrivalMode.AlwaysAvailable);
        var m = new PackageMotion(); m.Observe(GameSceneState.From(live), 0, true, 1); var count = 0;
        for (var day = 1; day <= 100; day++)
        {
            live.Step(); var state = GameSceneState.From(live); m.Observe(state, day * 4, true, 1);
            if (state.ReleasedToday == 0) Assert.Null(m.Release(day * 4));
            else { Assert.Equal(state.ReleasedToday, m.Release(day * 4)!.Count); Assert.InRange(state.ReleasedToday, 1, 2); count++; if (mode == ReleaseMode.Scheduled) Assert.Equal(0, day % 5); }
        }
        Assert.True(count > 0);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void RestoreAndDiscontinuitiesCancelEvenOnSameDay(bool sameDay)
    {
        var id = Guid.NewGuid(); var m = new PackageMotion(); m.Observe(State(id, 0, 0, 0), 0, true, 1);
        m.Observe(State(id, 1, 2, 2), 0, true, 1);
        m.Observe(State(Guid.NewGuid(), sameDay ? 1 : 0, 2, 2), .3, true, 1); Assert.Null(m.Release(.3));
    }
    [Fact] public void PausedClockFreezesWithoutDuplicateReleaseOnResume()
    {
        var id = Guid.NewGuid(); var m = new PackageMotion(); var clock = new PresentationClock(); clock.SetRunning(true, TimeSpan.Zero);
        m.Observe(State(id, 0, 0, 0), 0, true, 1); var scene = State(id, 1, 2, 2); m.Observe(scene, 0, true, 1);
        clock.SetRunning(false, TimeSpan.FromSeconds(1.3)); var paused = m.Release(clock.Seconds);
        clock.Sample(TimeSpan.FromSeconds(10)); Assert.Equal(paused, m.Release(clock.Seconds));
        clock.SetRunning(true, TimeSpan.FromSeconds(10)); m.Observe(scene, clock.Seconds, true, 1);
        Assert.Equal(2, m.Release(clock.Seconds)!.Count);
        m.Observe(State(id, 2, 4, 2), clock.Seconds, false, 1); Assert.Null(m.Release(clock.Seconds));
    }
    [Fact] public void HighSpeedAggregatesOnlyFeedbackAndCancellationReturnsParked()
    {
        var id = Guid.NewGuid(); var m = new PackageMotion(); m.Observe(State(id, 0, 0, 0), 0, true, 10);
        for (var i = 1; i <= 7; i++) m.Observe(State(id, i, i * 2, 2), i * .1, true, 10);
        var feedback = m.Release(.7)!; Assert.Equal(14, feedback.Count); Assert.Equal(ReleasePhase.FeedbackOnly, feedback.Phase); Assert.Equal(0, feedback.Offset);
        m.Cancel(); Assert.Null(m.Release(.7));
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void MaintenanceSharesDeveloperBudgetWithReviewWorkAndCollaboration(int budget)
    {
        var state = State(Guid.NewGuid(), 2, 0, 0) with { DeveloperPoseCeiling = budget };
        var a = WorkshopActivity.From(state); Assert.InRange(a.BusyDevelopers, 0, budget);
        if (budget == 4) Assert.Equal(new[] { SpritePose.Collaborating, SpritePose.Collaborating, SpritePose.Reviewing, SpritePose.Maintaining }, a.Developers);
        Assert.DoesNotContain(SpritePose.Maintaining, WorkshopActivity.From(state with { UsedDebtRepaymentCapacity = 0 }).Developers);
        Assert.DoesNotContain(SpritePose.Collaborating, WorkshopActivity.From(state with { CollaborationCapacity = 0 }).Developers);
    }
    [Theory] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void SmallTeamsReserveMaintenanceWithoutAddingPeople(int developers)
    {
        var state = State(Guid.NewGuid(), 2, 0, 0) with { Developers = developers, DeveloperPoseCeiling = developers };
        var scene = new SpriteWorkshop(state);
        Assert.Equal(developers, scene.Workers.Count(w => w.Role != "T"));
        Assert.True(scene.Activity.UnpicturedMaintenance);
        Assert.InRange(scene.Activity.BusyDevelopers, 1, developers);
        Assert.False(scene.IllustratesCollaboration);
    }
    [Fact] public void GesturesHaveMatchingFootAnchorsAndNeutralIdle()
    {
        var definition = SpriteCatalog.Assets["developer-gestures"];
        Assert.Single(definition.Clips[SpritePose.Idle].Frames);
        Assert.Equal(2, definition.Clips[SpritePose.Collaborating].Frames.Distinct().Count());
        Assert.All(definition.Clips[SpritePose.Collaborating].Frames, f => Assert.Equal(820, f.Anchor.Y));
    }
    [Theory] [InlineData(7, ReleaseMode.FlowBased)] [InlineData(12345, ReleaseMode.Scheduled)] [InlineData(9876, ReleaseMode.FlowBased)]
    public void CompleteHistoriesAndMetricsRemainIdentical(int seed, ReleaseMode mode)
    {
        var a = LiveSimulation.Start(BaselineScenario.DefectsAndReworkExample() with { RandomSeed = seed, Release = new(mode, 2, 5) }, WorkArrivalMode.AlwaysAvailable);
        a.CreateCheckpoint("Shared"); var b = LiveSimulation.Restore(a.Capture()); var m = new PackageMotion();
        m.Observe(GameSceneState.From(a), 0, true, 1);
        for (var day = 1; day <= 100; day++)
        {
            a.Step(); b.Step();
            if (day == 40) { a.Session.ApplyChanges(a.Session.Configuration with { Release = new(ReleaseMode.Scheduled, 3, 4) }, "Release intervention"); b.Session.ApplyChanges(b.Session.Configuration with { Release = new(ReleaseMode.Scheduled, 3, 4) }, "Release intervention"); }
            m.Observe(GameSceneState.From(a), day, true, day % 3 == 0 ? 10 : 1);
            for (var f = 0; f < 5; f++) { m.Sample(day + f * .1); m.Release(day + f * .1)?.Sprites(); }
            Assert.Equal(JsonSerializer.Serialize(b.Capture()), JsonSerializer.Serialize(a.Capture()));
        }
        Assert.Equal(JsonSerializer.Serialize(b.Session.GetResult()), JsonSerializer.Serialize(a.Session.GetResult()));
    }
}
