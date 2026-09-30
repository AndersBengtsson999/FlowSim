using Simulation.Application;
using Simulation.Core;
using Xunit;

namespace Simulation.Application.Tests;

public sealed class InterventionBoundaryTests
{
    private static void ToDay(LiveSimulation live, int day) { while (live.Session.CurrentDay < day) live.Step(); }

    [Theory]
    [InlineData(10, 91, 110)]
    [InlineData(20, 81, 120)]
    [InlineData(50, 51, 150)]
    [InlineData(100, 1, 200)]
    public void CompletePeriodsAreInclusiveContiguousAndAnchored(int window, int first, int last)
    {
        var live = LiveSimulation.Start(LiveSimulation.Demo); ToDay(live, 100);
        live.Session.ApplyChanges(live.Session.Configuration with { Team = new(5, 3) }, "Day 100");
        var change = Assert.Single(live.Session.Changes);
        ToDay(live, 200);
        var c = LivePerformance.Compare(live.Session, change, window);
        Assert.Equal(100, change.Day);
        Assert.Equal((first, 100, 101, last), (c.Before.FirstDay, c.Before.LastDay, c.After.FirstDay, c.After.LastDay));
        Assert.Equal(c.Before.LastDay + 1, c.After.FirstDay);
        Assert.Equal(window, c.Before.LastDay - c.Before.FirstDay + 1);
        Assert.Equal(window, c.After.LastDay - c.After.FirstDay + 1);
        Assert.Equal(window, c.Before.AvailableDays); Assert.Equal(window, c.After.AvailableDays);
        Assert.True(c.Before.IsComplete); Assert.True(c.After.IsComplete);
        Assert.Equal(2, live.Session.Days[99].AvailableTesterCapacity);
        Assert.Equal(3, live.Session.Days[100].AvailableTesterCapacity);
    }

    [Theory]
    [InlineData(100, 0)] [InlineData(101, 1)] [InlineData(110, 10)]
    [InlineData(119, 19)] [InlineData(120, 20)] [InlineData(140, 20)]
    public void AfterAvailabilityStopsAtItsAnchoredEnd(int day, int available)
    {
        var live = LiveSimulation.Start(LiveSimulation.Demo); ToDay(live, 100);
        live.Session.ApplyChanges(live.Session.Configuration with { Team = new(5, 3) });
        ToDay(live, day);
        var c = LivePerformance.Compare(live.Session, live.Session.Changes.Single(), 20);
        Assert.Equal((81, 100, 101, 120), (c.Before.FirstDay, c.Before.LastDay, c.After.FirstDay, c.After.LastDay));
        Assert.Equal(available, c.After.AvailableDays); Assert.Equal(20, c.After.ExpectedDays);
        Assert.Equal(available == 20, c.After.IsComplete);
        Assert.Equal(20, c.Before.AvailableDays);
        if (available == 0) Assert.Null(c.After.TesterUtilization);
    }

    [Fact]
    public void EveryComparisonMetricExcludesSentinelsAndIncludesBothBoundaryDays()
    {
        // Deliberately constructed analysis fixture; no engine advancement is performed.
        // Days 80/121 have large outliers. Days 100/101 differ from their period interiors.
        var template = LiveSimulation.Start(LiveSimulation.Demo with { NumberOfWorkItems = 6, Quality = new() { Enabled = true } }).Session.Capture();
        int[] completions = [80, 81, 100, 101, 120, 121];
        int[] cycles = [1, 11, 13, 17, 19, 99];
        int[] defects = [40, 2, 3, 5, 7, 40];
        var items = template.WorkItems.Select((w, i) => w with
        {
            State = WorkItemStatus.Done, DoneDay = completions[i], DevelopmentStartedDay = completions[i] - cycles[i],
            Events = Enumerable.Range(0, defects[i]).Select(_ => new WorkItemEvent(completions[i], w.Id, WorkItemEventType.DefectFound,
                WorkItemStatus.CodeReview, WorkItemStatus.WaitingForRework)).ToArray()
        }).ToArray();
        var days = Enumerable.Range(1, 140).Select(displayDay =>
        {
            var (dev, review, testing, rework, devUsed, reviewUsed, reworkUsed, devAvailable, testUsed, testAvailable) = displayDay switch
            {
                80 or 121 => (25, 25, 25, 25, 20d, 20d, 20d, 100d, 50d, 100d),
                100 => (9, 10, 11, 12, 1d, 1d, 2d, 5d, 1d, 4d),
                101 => (13, 14, 15, 16, 1d, 2d, 3d, 12d, 2d, 8d),
                >= 101 => (8, 5, 6, 7, 3d, 2d, 3d, 20d, 3d, 4d),
                _ => (5, 2, 3, 4, 2d, 1d, 1d, 10d, 1d, 2d)
            };
            var observations = new List<WorkItemDaySnapshot>();
            void Add(int count, WorkItemStatus state)
            {
                for (var i = 0; i < count; i++) observations.Add(new($"{state}-{i}", state, 0, 0, 0, 0, 0, 0, 0, state, false));
            }
            Add(dev, WorkItemStatus.Development); Add(review, WorkItemStatus.WaitingForCodeReview);
            Add(testing, WorkItemStatus.WaitingForTesting); Add(rework, WorkItemStatus.WaitingForRework);
            return new DailySnapshot(displayDay - 1, dev, 0, 0, 0, observations.Count, devUsed, reviewUsed, testUsed,
                observations, devAvailable, testAvailable, reworkUsed);
        }).ToArray();
        var changed = template.Configuration with { Team = new(5, 3) };
        var change = new ConfigurationChange(100, "Boundary", template.Configuration, changed);
        var session = SimulationSession.Restore(template with { CurrentDay = 140, Days = days, WorkItems = items, Configuration = changed, Changes = [change] });
        var c = LivePerformance.Compare(session, session.Changes.Single(), 20);
        Assert.Equal(2, c.Before.Completed); Assert.Equal(2, c.After.Completed);
        Assert.Equal(.5, c.Before.Throughput); Assert.Equal(.5, c.After.Throughput);
        Assert.Equal(12, c.Before.CycleTime); Assert.Equal(18, c.After.CycleTime);
        Assert.Equal(15.4, c.Before.AverageWip); Assert.Equal(27.6, c.After.AverageWip);
        Assert.Equal(2.4, c.Before.Review.Average); Assert.Equal(5.45, c.After.Review.Average);
        Assert.Equal(3.4, c.Before.Testing.Average); Assert.Equal(6.45, c.After.Testing.Average);
        Assert.Equal(4.4, c.Before.Rework.Average); Assert.Equal(7.45, c.After.Rework.Average);
        Assert.Equal(80d / 195, c.Before.DeveloperUtilization); Assert.Equal(158d / 392, c.After.DeveloperUtilization);
        Assert.Equal(20d / 42, c.Before.TesterUtilization); Assert.Equal(59d / 84, c.After.TesterUtilization);
        Assert.Equal(5, c.Before.Defects); Assert.Equal(12, c.After.Defects);
        Assert.Equal(21d / 80, c.Before.ReworkCapacity); Assert.Equal(60d / 158, c.After.ReworkCapacity);
        Assert.True(c.Before.QualityRelevant); Assert.True(c.After.QualityRelevant);
        var rolling = LivePerformance.Rolling(session, 20);
        Assert.Equal((121, 140), (rolling.FirstDay, rolling.LastDay));
        Assert.NotEqual(c.After, rolling);
        Assert.NotEqual(c.After.TesterUtilization, rolling.TesterUtilization);
    }
}
