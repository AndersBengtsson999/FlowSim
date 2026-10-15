using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.Sprites;
using Simulation.UI.Controls.GameWorld;
using Simulation.UI.ViewModels;
using Xunit;

namespace Simulation.UI.Tests;

public sealed class WorkerAnimationTests
{
    private static GameSceneState Busy() => new(30,
        [new("Backlog", 0, 2000, ""), new("Development", 100, 0, ""), new("Review", 20, 30, ""),
         new("Testing", 40, 2000, ""), new("Ready", 0, 300, ""), new("Release", 100, 0, "")],
        200, 100, 40, 2, .1, 5, "Scheduled", "Observed capacity", true)
    { ActiveDevelopment = 100, UsedDeveloperCapacity = 8, UsedDevelopmentCapacity = 4, UsedReviewCapacity = 2,
      UsedTesterCapacity = 2, UsedDebtRepaymentCapacity = .4, CollaborationCapacity = .5, DeveloperPoseCeiling = 5, TesterPoseCeiling = 2 };

    [Fact]
    public void WorkingReviewTestingAndCollaborationUseBoundedAggregateSignals()
    {
        var a = WorkshopActivity.From(Busy());
        Assert.Equal(new[] { SpritePose.Collaborating, SpritePose.Collaborating, SpritePose.Reviewing, SpritePose.Maintaining }, a.Developers);
        Assert.Equal(new[] { SpritePose.Testing, SpritePose.Idle }, a.Testers);
        Assert.True(a.RepaymentObserved);
        var scene = new SpriteWorkshop(Busy());
        Assert.Equal(196, scene.HiddenDevelopers); Assert.Equal(98, scene.HiddenTesters);
        Assert.InRange(scene.Objects.Count, 0, SpriteWorkshop.MaximumObjects);
    }

    [Fact]
    public void QueueSizeAndUtilizationNeverBecomeBusyWorkerCounts()
    {
        var state = Busy(); var initial = WorkshopActivity.From(state);
        var changed = WorkshopActivity.From(state with { ActiveDevelopment = 10000, UsedDeveloperCapacity = 199, UsedDevelopmentCapacity = 198 });
        Assert.Equal(initial.Developers, changed.Developers);
        var idle = WorkshopActivity.From(state with { UsedDevelopmentCapacity = 0, UsedReviewCapacity = 0, UsedTesterCapacity = 0, UsedDebtRepaymentCapacity = 0 });
        Assert.All(idle.Developers.Concat(idle.Testers), p => Assert.Equal(SpritePose.Idle, p));
        Assert.False(idle.RepaymentObserved);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void CombinedDeveloperAndTestPosesRespectCapacityCeilings(int ceiling)
    {
        var a = WorkshopActivity.From(Busy() with { DeveloperPoseCeiling = ceiling, TesterPoseCeiling = ceiling });
        Assert.InRange(a.BusyDevelopers, 0, ceiling); Assert.InRange(a.BusyTesters, 0, ceiling);
        if (ceiling < 3) Assert.DoesNotContain(SpritePose.Collaborating, a.Developers);
    }

    [Fact]
    public void CompletedWorkDoesNotAnimateAnEmptyStageAndDayZeroIsIdle()
    {
        var state = Busy(); var areas = state.Areas.Select(a => a with { Active = 0 }).ToArray();
        var empty = WorkshopActivity.From(state with { Areas = areas, ActiveDevelopment = 0, UsedDebtRepaymentCapacity = 0 });
        Assert.All(empty.Developers.Concat(empty.Testers), p => Assert.Equal(SpritePose.Idle, p));
        var zero = WorkshopActivity.From(state with { Day = 0 });
        Assert.All(zero.Developers.Concat(zero.Testers), p => Assert.Equal(SpritePose.Idle, p));
        Assert.False(zero.RepaymentObserved);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public void SmallTeamCanIllustrateReviewWithoutAddingOrMovingAWorker(int developers)
    {
        var state = Busy() with { Developers = developers, Specialists = 0, ActiveDevelopment = 0, UsedDevelopmentCapacity = 0, UsedDebtRepaymentCapacity = 0 };
        var review = new SpriteWorkshop(state);
        Assert.Equal(SpritePose.Reviewing, review.Activity.Developers[0]);
        Assert.Equal(developers, review.Workers.Count(w => w.Role != "T"));
        Assert.Equal("development", review.Workers[0].Visual.StationId); // Capacity vignette, no claimed assignment or travel.
        var working = new SpriteWorkshop(state with { ActiveDevelopment = 1, UsedDevelopmentCapacity = 1 });
        Assert.Equal(review.Workers[0].Visual.Position, working.Workers[0].Visual.Position);
    }

    [Fact]
    public void ReworkAndDebtAreIndependentObservedSignals()
    {
        var activity = WorkshopActivity.From(Busy() with { ActiveDevelopment = 0, ActiveRework = 1, UsedDevelopmentCapacity = 0, UsedReworkCapacity = .5, CollaborationCapacity = 0 });
        Assert.Equal(SpritePose.Working, activity.Developers[0]); Assert.True(activity.RepaymentObserved);
        Assert.Equal(3, activity.BusyDevelopers); // Work + review + maintenance all share the same ceiling.
    }

    [Fact]
    public void PoseChangesKeepSeatAnchorsAndObjectDepthStable()
    {
        var busy = new SpriteWorkshop(Busy()); var idle = new SpriteWorkshop(Busy() with { DeveloperPoseCeiling = 0, TesterPoseCeiling = 0 });
        Assert.Equal(busy.Workers.Select(w => (w.Visual.Id, w.Visual.Position)), idle.Workers.Select(w => (w.Visual.Id, w.Visual.Position)));
        Assert.Equal(busy.Workers.Select(w => w.Visual.Presentation.AssetId), idle.Workers.Select(w => w.Visual.Presentation.AssetId));
        foreach (var worker in busy.Workers.Where(w => w.Visual.Presentation.Seated))
        {
            var item = worker.Visual.ToSprite(); var definition = SpriteCatalog.Assets[item.AssetId];
            foreach (var pose in new[] { SpritePose.Idle, SpritePose.Working, SpritePose.Reviewing, SpritePose.Testing, SpritePose.Collaborating })
                Assert.Equal(SpritePlacement.Destination(item, definition, SpriteWorkshop.Projection),
                    SpritePlacement.Destination(item with { Pose = pose }, definition, SpriteWorkshop.Projection));
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void AnyHiddenDisabledPausedOrDetachedConditionStopsClock(int disabledCondition)
    {
        var flags = Enumerable.Repeat(true, 7).ToArray();
        Assert.True(PresentationClock.ShouldRun(flags[0], flags[1], flags[2], flags[3], flags[4], flags[5], flags[6]));
        flags[disabledCondition] = false;
        Assert.False(PresentationClock.ShouldRun(flags[0], flags[1], flags[2], flags[3], flags[4], flags[5], flags[6]));
    }

    [Fact]
    public void ClockFreezesResumesAndSkipsElapsedFramesWithoutCatchupTasks()
    {
        var clock = new PresentationClock(); clock.SetRunning(true, TimeSpan.Zero); clock.Sample(TimeSpan.FromSeconds(2));
        Assert.Equal(2, clock.Seconds); clock.SetRunning(false, TimeSpan.FromSeconds(2)); clock.Sample(TimeSpan.FromHours(1));
        Assert.Equal(2, clock.Seconds); clock.SetRunning(true, TimeSpan.FromHours(1)); clock.Sample(TimeSpan.FromSeconds(3601));
        Assert.Equal(3, clock.Seconds); clock.Sample(TimeSpan.FromSeconds(3600)); Assert.Equal(3, clock.Seconds);
        Assert.Equal(1, SpriteFrameSampling.Index(4, 4, 1000000.25));
        clock.Reset(TimeSpan.FromSeconds(3601)); Assert.Equal(0, clock.Seconds);
    }

    [Theory]
    [InlineData(7, .5)] [InlineData(12345, 2)] [InlineData(9876, 10)]
    public void FullSeededRunsAndInterventionsMatchWithAnimationEnabledDisabledAndHidden(int seed, double speed)
    {
        var original = LiveSimulation.Start(BaselineScenario.DefectsAndReworkExample() with
        { RandomSeed = seed, Skills = new(2, .3), Release = new(ReleaseMode.Scheduled, 2, 5), ResidualDependencies = new(.5, 4) }, WorkArrivalMode.AlwaysAvailable);
        original.CreateCheckpoint("Shared initial checkpoint");
        using var animated = new LiveViewModel(); using var disabled = new LiveViewModel();
        animated.Load(LiveSimulation.Restore(original.Capture())); disabled.Load(LiveSimulation.Restore(original.Capture()));
        animated.Speed = disabled.Speed = speed; animated.Resume(); disabled.Resume(); disabled.AnimateGameActivity = false;
        var clock = new PresentationClock(); var seconds = 0.0;
        for (var day = 1; day <= 100; day++)
        {
            animated.Visualization = day % 4 == 0 ? "Flow Board" : "Game View";
            animated.GameRenderingMode = day % 5 == 0 ? "Legacy vectors" : "Sprites";
            animated.AnimateGameActivity = day % 7 != 0;
            var before = JsonSerializer.Serialize(animated.Live!.Capture());
            clock.SetRunning(PresentationClock.ShouldRun(true, animated.GameViewSelected, true, animated.SpriteRendering,
                animated.AnimateGameActivity, animated.IsRunning, true), TimeSpan.FromSeconds(seconds));
            for (var frame = 0; frame < 13; frame++)
            {
                seconds += .037; clock.Sample(TimeSpan.FromSeconds(seconds));
                _ = new SpriteWorkshop(animated.GameScene);
            }
            Assert.Equal(before, JsonSerializer.Serialize(animated.Live.Capture()));
            animated.Tick(); disabled.Tick();
            if (day == 40)
                foreach (var vm in new[] { animated, disabled })
                {
                    vm.Pause(); vm.BeginChange(); vm.Draft.NumberOfTesters = "3"; vm.Draft.DeveloperAvailability = "60";
                    vm.ChangeLabel = "Same intervention"; vm.ApplyChanges(); Assert.False(vm.IsEditing); vm.Resume();
                }
            Assert.Equal(JsonSerializer.Serialize(disabled.Live!.Capture()), JsonSerializer.Serialize(animated.Live.Capture()));
        }
        animated.Pause(); disabled.Pause();
        Assert.Equal(JsonSerializer.Serialize(disabled.Live!.Session.GetResult()), JsonSerializer.Serialize(animated.Live!.Session.GetResult()));
        animated.BeginChange(); animated.Draft.NumberOfTesters = "6";
        var paused = JsonSerializer.Serialize(animated.Live.Capture());
        animated.AnimateGameActivity = false; animated.GameRenderingMode = "Legacy vectors"; animated.AnimateGameActivity = true;
        Assert.Equal(paused, JsonSerializer.Serialize(animated.Live.Capture())); Assert.Equal("6", animated.Draft.NumberOfTesters);
        Assert.Contains("paused", animated.GameAnimationStatus);
    }
}
