using Simulation.Application;
using Simulation.Core;
using Simulation.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Simulation.Application.Tests;

public sealed class LivePerformanceTests(ITestOutputHelper output)
{
    private static void ToDay(LiveSimulation live, int day) { while (live.Session.CurrentDay < day) Assert.True(live.Step()); }

    [Fact]
    public void WindowSelectsCompletionDaysAndRetainsFullCycleTimes()
    {
        var items = new[] { 78, 79, 98, 99 }.Select(n => new WorkItem($"W{n}", "Boundary", n, 1, 1)).ToArray();
        var session = new SimulationSession(new("Boundaries", 120, new(10, 10), 10, 10, 10, items));
        var live = new LiveSimulation(session); ToDay(live, 101);
        Assert.Equal(new int?[] { 80, 81, 100, 101 }, session.WorkItems.Select(w => w.DoneDay));
        var p = LivePerformance.Period(session, 81, 100);
        Assert.Equal(81, p.FirstDay); Assert.Equal(100, p.LastDay); Assert.Equal(20, p.AvailableDays);
        Assert.Equal(2, p.Completed); Assert.Equal(.5, p.Throughput); Assert.Equal(90.5, p.CycleTime);
        var day100 = new LiveSimulation(new SimulationSession(new("Boundaries", 120, new(10, 10), 10, 10, 10, items)));
        ToDay(day100, 100);
        Assert.Equal(p, LivePerformance.Rolling(day100.Session, 20));
    }

    [Fact]
    public void CapacityUsesTotalsWithDifferentDenominatorsAndZeroCapacityIsUnavailable()
    {
        var live = LiveSimulation.Start(LiveSimulation.Demo); var state = live.Session.Capture();
        DailySnapshot Day(int day, double used, double available) => new(day, 0, 0, 0, 0, 0, used, 0, used, [], available, available);
        var session = SimulationSession.Restore(state with { CurrentDay = 3, Days = [Day(0, 1, 1), Day(1, 1, 3), Day(2, 0, 0)] });
        var p = LivePerformance.Rolling(session);
        Assert.Equal(.5, p.DeveloperUtilization); Assert.Equal(.5, p.TesterUtilization);
        Assert.NotEqual((1 + 1.0 / 3) / 2, p.DeveloperUtilization);
        p = LivePerformance.Period(session, 3, 3);
        Assert.Null(p.DeveloperUtilization); Assert.Null(p.TesterUtilization); Assert.Null(p.ReworkCapacity);
    }

    [Theory]
    [InlineData(1, 2, 3, 1)]
    [InlineData(3, 2, 1, -1)]
    [InlineData(2, 2, 2, 0)]
    [InlineData(1, 10, 2, .5)]
    [InlineData(1, 1.001, 1.002, .001)]
    public void OlsUsesAllDailyObservations(double a, double b, double c, double expected)
    {
        Assert.Equal(expected, LivePerformance.Slope([(81, a), (82, b), (83, c)])!.Value, 10);
    }

    [Fact]
    public void OlsUsesDayIndexesAndDiffersFromEndpointEstimate()
    {
        Assert.Equal(.4, LivePerformance.Slope([(81, 0), (82, 4), (83, 0), (84, 0), (85, 4)])!.Value, 10);
        Assert.Equal(2, LivePerformance.Slope([(1, 2), (3, 6), (8, 16)]));
        Assert.Null(LivePerformance.Slope([]));
        Assert.Null(LivePerformance.Slope([(1, 5)]));
        Assert.Null(LivePerformance.Slope([(1, 5), (2, 5)]));
    }

    [Fact]
    public void BeforeAfterHasExactBoundariesPartialProgressAndPercentagePointDeltas()
    {
        var live = LiveSimulation.Start(LiveSimulation.Demo); ToDay(live, 100);
        var checkpoint = live.CreateCheckpoint("Day 100 baseline");
        live.Session.ApplyChanges(live.Session.Configuration with { Team = new(5, 3) }, "Add tester");
        var change = live.Session.Changes.Single();
        var c = LivePerformance.Compare(live.Session, change, 20);
        Assert.Equal((81, 100, 101, 120), (c.Before.FirstDay, c.Before.LastDay, c.After.FirstDay, c.After.LastDay));
        Assert.Equal(0, c.After.AvailableDays); Assert.Null(c.After.CycleTime); Assert.Null(c.After.TesterUtilization);
        ToDay(live, 112); c = LivePerformance.Compare(live.Session, change, 20);
        Assert.Equal(12, c.After.AvailableDays); Assert.False(c.After.IsComplete); Assert.Equal(20, c.After.ExpectedDays);
        ToDay(live, 120); c = LivePerformance.Compare(live.Session, change, 20);
        Assert.True(c.Before.IsComplete); Assert.True(c.After.IsComplete);
        Assert.Equal((c.After.TesterUtilization - c.Before.TesterUtilization) * 100, c.TesterPercentagePoints);
        Assert.Equal(40, live.Session.Days.Skip(80).Take(20).Sum(d => d.AvailableTesterCapacity));
        Assert.Equal(60, live.Session.Days.Skip(100).Take(20).Sum(d => d.AvailableTesterCapacity));
        output.WriteLine($"Before: {c.Before}"); output.WriteLine($"After: {c.After}");
        var loaded = LiveSessionJson.Load(LiveSessionJson.Save(live));
        Assert.Equal(c, LivePerformance.Compare(loaded.Session, loaded.Session.Changes.Single(), 20));
        live.RestoreCheckpoint(checkpoint.Id); Assert.Empty(live.Session.Changes);
        Assert.Equal(c.Before, LivePerformance.Rolling(live.Session));
    }

    [Fact]
    public void ComparisonUsesAverageQueuesRatherThanFinalQueueSizes()
    {
        var live = LiveSimulation.Start(LiveSimulation.Demo with { TesterCount = 1, TestingWipLimit = 1 }, rate: 1.8m);
        ToDay(live, 30); live.Session.ApplyChanges(live.Session.Configuration with { Team = new(5, 2) }, "Testers"); ToDay(live, 50);
        var c = LivePerformance.Compare(live.Session, live.Session.Changes.Single(), 20);
        Assert.Equal(live.Session.Days.Skip(10).Take(20).Average(d => d.WaitingForTestingCount), c.Before.Testing.Average);
        Assert.Equal(live.Session.Days.Skip(30).Take(20).Average(d => d.WaitingForTestingCount), c.After.Testing.Average);
        Assert.NotEqual(c.Before.Testing.Current, c.Before.Testing.Average);
        Assert.NotEqual(c.After.Testing.Current, c.After.Testing.Average);
    }

    [Fact]
    public void QualityUsesPeriodEventsAndActualReworkCapacityAndSurvivesDisable()
    {
        var live = LiveSimulation.Start(LiveSimulation.Demo with { Quality = new() { Enabled = true, CodeReviewDefectProbability = 1 } });
        ToDay(live, 40); var p = LivePerformance.Rolling(live.Session);
        var expected = live.Session.WorkItems.SelectMany(w => w.Events).Count(e => e.EventType == WorkItemEventType.DefectFound && e.Day >= 21 && e.Day <= 40);
        Assert.True(expected > 0); Assert.Equal(expected, p.Defects); Assert.True(p.QualityRelevant);
        var days = live.Session.Days.Skip(20).ToArray();
        Assert.Equal(days.Sum(d => d.UsedReworkDeveloperCapacity) / days.Sum(d => d.UsedDeveloperCapacity), p.ReworkCapacity);
        live.Session.ApplyChanges(live.Session.Configuration with { Quality = new() }, "Defects off");
        Assert.Equal(p, LivePerformance.Rolling(live.Session));
        ToDay(live, 45); Assert.True(LivePerformance.Rolling(live.Session).QualityRelevant);
    }

    [Fact]
    public void QualityEventBoundariesExcludeEarlierAndLaterDiscoveries()
    {
        var live = LiveSimulation.Start(LiveSimulation.Demo with
        {
            NumberOfWorkItems = 1, DevelopmentEffort = 1, CodeReviewEffort = 1,
            Quality = new() { Enabled = true, CodeReviewDefectProbability = 1, CodeReviewReworkEffortDistribution = new FixedEffort(1) }
        }, WorkArrivalMode.FixedBacklog);
        ToDay(live, 6);
        Assert.Equal(new[] { 2, 4, 6 }, live.Session.WorkItems.Single().Events.Where(e => e.EventType == WorkItemEventType.DefectFound).Select(e => e.Day));
        var period = LivePerformance.Period(live.Session, 3, 4);
        Assert.Equal(1, period.Defects); Assert.Equal(.5, period.ReworkCapacity);
        Assert.Equal(1, LivePerformance.Period(live.Session, 4, 5).Defects);
    }

    [Fact]
    public void EarlyInterventionAndAdditionalChangesAreExplicit()
    {
        var live = LiveSimulation.Start(LiveSimulation.Demo);
        live.Session.ApplyChanges(live.Session.Configuration with { Team = new(5, 3) }, "Day zero");
        var first = live.Session.Changes[0]; ToDay(live, 5);
        live.Session.ApplyChanges(live.Session.Configuration with { Team = new(5, 4) }, "Another change");
        var c = LivePerformance.Compare(live.Session, first, 20);
        Assert.Equal(0, c.Before.AvailableDays); Assert.False(c.Before.IsComplete); Assert.Equal(1, c.OtherInterventions);
        Assert.Equal(c, LivePerformance.Compare(live.Session, first with { }, 20));
        Assert.Throws<ArgumentOutOfRangeException>(() => LivePerformance.Rolling(live.Session, 0));
        Assert.Throws<ArgumentException>(() => LivePerformance.Compare(live.Session, first with { Day = 99 }, 20));
    }
}
