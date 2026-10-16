using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Xunit;

namespace Simulation.Application.Tests;
public sealed class GuidedInterventionTests
{
    private static LiveSimulation At(int day = 100, ReleaseSettings? release = null)
    {
        var live = LiveSimulation.Start(LiveSimulation.Demo with { TesterCount = 2, Release = release ?? new(), RandomSeed = 73 });
        while (live.Session.CurrentDay < day) live.Step();
        return live;
    }
    private static void Testers(LiveSimulation live, int count) => live.Session.ApplyChanges(live.Session.Configuration with { Team = live.Session.Configuration.Team with { TesterCount = count } });
    private static string Json(object? value) => JsonSerializer.Serialize(value);
    [Theory]
    [InlineData(10)] [InlineData(20)] [InlineData(40)]
    public async Task BranchesPreserveHistoryRandomnessAndCompareOnlyMatchedDays(int window)
    {
        var live = At(); var checkpoint = live.CreateCheckpoint("start"); Testers(live, 3);
        var experiment = new GuidedInterventionExperiment(checkpoint, live.Session, window);
        var unchanged = Json(live.Capture());
        await experiment.PrepareBaselineAsync();
        Assert.Equal(unchanged, Json(live.Capture()));
        Assert.Equal(100, live.Session.Changes.Last().Day);
        Assert.Equal(100 + window, experiment.EndDay);
        Assert.Null(experiment.Observe(live.Session).Baseline);
        var replay = SimulationSession.Restore(checkpoint.State);
        for (int i = 1; i <= window; i++)
        {
            replay.AdvanceOneDay(); live.Step();
            var result = experiment.Observe(live.Session);
            Assert.Equal(i, result.ObservedDays); Assert.Equal(i == window, result.Complete);
            Assert.Equal(101, result.Baseline!.Period.FirstDay); Assert.Equal(100+i, result.Baseline.Period.LastDay);
            Assert.Equal(i, result.Intervention!.Period.AvailableDays);
            Assert.Equal(LivePerformance.Period(replay, 101, 100+i), result.Baseline.Period);
            Assert.Equal(LivePerformance.Period(live.Session, 101, 100+i), result.Intervention.Period);
            Assert.Equal(100, result.Baseline.TestingQueue[0].Day);
            Assert.Equal(100+i, result.Baseline.TestingQueue[^1].Day);
            Assert.Equal(3, live.Session.Days[^1].AvailableTesterCapacity);
        }
        Assert.Equal(Json(checkpoint.State.Days), Json(live.Session.Days.Take(100).ToArray()));
        Assert.Equal(Json(checkpoint.State.Days), Json(replay.Days.Take(100).ToArray()));
        var expectedCost = live.Session.Days.Skip(100).Sum(d => d.UsedDeveloperCapacity + d.UsedTesterCapacity);
        Assert.Equal(expectedCost, experiment.Observe(live.Session).Intervention!.Period.ConsumedSystemCapacity!.Total, 8);
    }
    [Fact]
    public async Task NoOpAndRepeatedBranchesAreIdentical()
    {
        var live = At(37); var checkpoint = live.CreateCheckpoint("no-op"); Testers(live, 2);
        var a = new GuidedInterventionExperiment(checkpoint, live.Session, 20);
        var b = new GuidedInterventionExperiment(checkpoint, live.Session, 20);
        await Task.WhenAll(a.PrepareBaselineAsync(), b.PrepareBaselineAsync());
        for (int i = 0; i < 20; i++) live.Step();
        var result = a.Observe(live.Session);
        Assert.Equal(Json(result.Baseline), Json(result.Intervention));
        Assert.Equal(Json(result), Json(b.Observe(live.Session)));
        var replay = SimulationSession.Restore(checkpoint.State);
        for (int i = 0; i < 20; i++) replay.AdvanceOneDay();
        Assert.Equal(Json(live.Session.Capture()), Json(replay.Capture()));
    }
    [Fact]
    public async Task ReleaseBottleneckDoesNotImplyThroughputGain()
    {
        var live = At(100, new(ReleaseMode.Scheduled, 1, 1000));
        var checkpoint = live.CreateCheckpoint("release constrained"); Testers(live, 3);
        var experiment = new GuidedInterventionExperiment(checkpoint, live.Session, 20);
        await experiment.PrepareBaselineAsync(); for (int i = 0; i < 20; i++) live.Step();
        var result = experiment.Observe(live.Session);
        Assert.True(live.CurrentSnapshot.ReadyForReleaseCount > 0);
        Assert.Equal(0, result.Baseline!.Period.Completed); Assert.Equal(0, result.Intervention!.Period.Completed);
        Assert.Null(result.Baseline.Period.CycleTime); Assert.Null(result.Intervention.Period.CycleTime);
    }
    [Fact]
    public async Task FurtherInterventionsAndRestorationInvalidateButLaterChangesDoNot()
    {
        var live = At(); var checkpoint = live.CreateCheckpoint("start"); Testers(live, 3);
        var experiment = new GuidedInterventionExperiment(checkpoint, live.Session, 10);
        await experiment.PrepareBaselineAsync(); live.Step(); Testers(live, 4);
        var result = experiment.Observe(live.Session);
        Assert.NotNull(result.InvalidReason); Assert.Null(result.Baseline); Assert.False(result.Complete);
        live.RestoreCheckpoint(checkpoint.Id);
        var restored = new GuidedInterventionExperiment(checkpoint, live.Session, 10);
        await restored.PrepareBaselineAsync(); for(int i=0;i<10;i++) live.Step();
        Testers(live, 5); Assert.True(restored.Observe(live.Session).Complete);
        live.RestoreCheckpoint(checkpoint.Id);
        Assert.NotNull(restored.Observe(live.Session).InvalidReason);
        Assert.NotNull(restored.Observe(null).InvalidReason);
    }
    [Fact]
    public async Task AllExistingRandomStreamsAndCostMechanismsReplayFromCheckpoint()
    {
        var live = LiveSimulation.Start(LiveSimulation.Demo with {
            Skills = new(2, .5), ResidualDependencies = new(.4, 5),
            DevelopmentDistribution = new TriangularEffort(2, 5, 9), TestingDistribution = new TriangularEffort(1, 2, 4),
            Productivity = new(1.4, .8, 1.2), Release = new(ReleaseMode.Scheduled, 2, 5),
            Debt = new() { ShortcutRate = .4, Repayment = .2, CreationFactor = 2, Tolerance = .1 },
            Quality = new() { Enabled = true, CodeReviewDefectProbability = .2, TestingDefectProbability = .2 }
        }, WorkArrivalMode.AlwaysAvailable);
        for(int i=0;i<100;i++) live.Step();
        var checkpoint = live.CreateCheckpoint("all streams");
        var experiment = new GuidedInterventionExperiment(checkpoint, live.Session, 40);
        var before = Json(live.Capture()); await experiment.PrepareBaselineAsync(); Assert.Equal(before, Json(live.Capture()));
        for(int i=0;i<40;i++) live.Step();
        var result = experiment.Observe(live.Session);
        Assert.Equal(Json(result.Baseline), Json(result.Intervention));
        Assert.NotNull(result.Baseline!.Period.ConsumedSystemCapacity);
        var repeat = SimulationSession.Restore(checkpoint.State);
        for(int i=0;i<40;i++) repeat.AdvanceOneDay();
        Assert.Equal(Json(live.Session.Capture()), Json(repeat.Capture()));
    }
    [Fact]
    public void OtherParameterChangesAreRejected()
    {
        var live = At(); var checkpoint = live.CreateCheckpoint("start");
        live.Session.ApplyChanges(live.Session.Configuration with { TestingWipLimit = 123 });
        Assert.Throws<ArgumentException>(() => new GuidedInterventionExperiment(checkpoint, live.Session, 20));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GuidedInterventionExperiment(checkpoint, live.Session, 12));
    }
}
