using System.Text.Json;
using Simulation.Core;
using Xunit;

namespace Simulation.Core.Tests;

public sealed class SimulationSessionTests
{
    private static SimulationScenario Scenario(int days = 100, double effort = 5, int count = 30) =>
        new("Session", days, new(), 5, 3, 3, EffortGenerator.Generate(count, new FixedEffort(effort), new FixedEffort(1), new FixedEffort(2), 12345));
    private static void Days(SimulationSession s, int n) { for (var i = 0; i < n; i++) s.AdvanceOneDay(); }
    private static string Result(SimulationSession s) => JsonSerializer.Serialize(s.GetResult());
    private static SimulationSession Arrivals(decimal rate, int count = 0) => new(Scenario(count: count),
        new(new()) { ArrivalMode = WorkArrivalMode.ContinuousArrival, WorkItemsPerDay = rate });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IncrementalAndFixedRunAreExactlyEquivalentIncludingHistory(bool defects)
    {
        var scenario = Scenario() with { Quality = new() { Enabled = defects, CodeReviewDefectProbability = .2, TestingDefectProbability = .3,
            TestingReworkEffortDistribution = new TriangularEffort(.3, 1, 3) } };
        var s = new SimulationSession(scenario); Days(s, 100);
        Assert.Equal(JsonSerializer.Serialize(new SimulationEngine().Run(scenario)), Result(s));
        Assert.All(scenario.WorkItems, w => Assert.Equal(WorkItemStatus.Backlog, w.State));
    }

    [Theory]
    [InlineData("1.0", 100)]
    [InlineData("0.5", 50)]
    [InlineData("0.8", 80)]
    [InlineData("1.5", 150)]
    public void FractionalArrivalsAccumulateExactly(string rate, int count)
    {
        var s = Arrivals(decimal.Parse(rate, System.Globalization.CultureInfo.InvariantCulture)); Days(s, 100);
        Assert.Equal(count, s.WorkItems.Count);
        Assert.Equal(count, s.WorkItems.Select(w => w.Id).Distinct().Count());
    }
    [Fact]
    public void HalfRateArrivesAtBeginningOfEverySecondDay()
    {
        var s = Arrivals(.5m);
        s.AdvanceOneDay(); Assert.Empty(s.WorkItems);
        s.AdvanceOneDay(); Assert.Single(s.WorkItems); Assert.Equal(1, s.WorkItems[0].CreatedDay);
        s.AdvanceOneDay(); Assert.Single(s.WorkItems);
        s.AdvanceOneDay(); Assert.Equal(2, s.WorkItems.Count);
    }
    [Fact]
    public void RateChangeRetainsFractionAndBeginsNextDay()
    {
        var s = Arrivals(.8m); s.AdvanceOneDay();
        s.ApplyChanges(s.Configuration with { WorkItemsPerDay = 1.5m }, "Demand");
        Assert.Empty(s.WorkItems); Assert.Equal(1, s.Changes[0].Day);
        s.AdvanceOneDay(); Assert.Equal(2, s.WorkItems.Count); Assert.Equal(.3m, s.Capture().ArrivalAccumulator);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CountChangeAt100OnlyChangesFutureCapacity(bool developers)
    {
        var s = Arrivals(1); Days(s, 100); var before = JsonSerializer.Serialize(s.Days);
        s.ApplyChanges(s.Configuration with { Team = developers ? s.Configuration.Team with { DeveloperCount = 2 } : s.Configuration.Team with { TesterCount = 3 } });
        Assert.Equal(100, s.CurrentDay); Assert.Equal(before, JsonSerializer.Serialize(s.Days));
        var next = s.AdvanceOneDay(); Assert.Equal(100, next.Day);
        Assert.Equal(developers ? 2 : 5, next.AvailableDeveloperCapacity);
        Assert.Equal(developers ? 2 : 3, next.AvailableTesterCapacity);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CapacityChangePreservesWorkAndAppliesNextInterval(bool developers)
    {
        var s = Arrivals(2); Days(s, 20);
        var before = JsonSerializer.Serialize(s.Capture().WorkItems);
        s.ApplyChanges(s.Configuration with { Team = developers ? s.Configuration.Team with { DeveloperCapacityPerDay = .4 } : s.Configuration.Team with { TesterCapacityPerDay = .3 } });
        Assert.Equal(before, JsonSerializer.Serialize(s.Capture().WorkItems));
        var day = s.AdvanceOneDay();
        Assert.Equal(developers ? 2 : 5, day.AvailableDeveloperCapacity);
        Assert.Equal(developers ? 2 : .6, day.AvailableTesterCapacity, 10);
        Assert.All(day.Items, w => Assert.True(developers ? w.DevelopmentWork + w.CodeReviewWork + w.ReworkWork <= .4 : w.TestingWork <= .3));
    }
    [Fact]
    public void ReducedWipDoesNotEvictAndPreventsAdmission()
    {
        var s = new SimulationSession(Scenario(effort: 5)); s.AdvanceOneDay();
        var active = s.WorkItems.Where(w => w.State == WorkItemStatus.Development).Select(w => w.Id).ToArray();
        Assert.Equal(5, active.Length);
        s.ApplyChanges(s.Configuration with { DevelopmentWipLimit = 3, Team = new(2, 2) });
        Assert.Equal(active, s.WorkItems.Where(w => w.State == WorkItemStatus.Development).Select(w => w.Id));
        s.AdvanceOneDay(); Assert.Equal(5, s.Days[^1].DevelopmentWip);
        Assert.All(s.WorkItems.Skip(5), w => Assert.Equal(WorkItemStatus.Backlog, w.State));
        Days(s, 30);
        Assert.All(s.Days.Skip(20), d => Assert.True(d.DevelopmentWip <= 3));
    }
    [Fact]
    public void QualityChangeOnlyAffectsFutureInspections()
    {
        var s = new SimulationSession(Scenario(effort: 1, count: 1)); s.AdvanceOneDay();
        var history = JsonSerializer.Serialize(s.WorkItems[0].Events);
        s.ApplyChanges(s.Configuration with { Quality = new() { Enabled = true, CodeReviewDefectProbability = 1 } });
        Assert.Equal(history, JsonSerializer.Serialize(s.WorkItems[0].Events));
        s.AdvanceOneDay(); Assert.Equal(WorkItemStatus.WaitingForRework, s.WorkItems[0].State);
        s.ApplyChanges(s.Configuration with { Quality = s.Configuration.Quality with { CodeReviewDefectProbability = 0 } });
        Days(s, 10); Assert.Equal(WorkItemStatus.Done, s.WorkItems[0].State);
        Assert.Single(s.WorkItems[0].Events, e => e.EventType == WorkItemEventType.DefectFound);
    }
    [Fact]
    public void RestorePreservesRandomArrivalQueueReworkAndEvents()
    {
        var s = new SimulationSession(Scenario(count: 0), new(new()) {
            ArrivalMode = WorkArrivalMode.ContinuousArrival, WorkItemsPerDay = .8m,
            DevelopmentEffort = new TriangularEffort(.5, 2, 4), CodeReviewEffort = new TriangularEffort(.2, .4, 1.2),
            Quality = new() { Enabled = true, CodeReviewDefectProbability = .35, TestingDefectProbability = .4,
                TestingReworkEffortDistribution = new TriangularEffort(.2, 1, 2) } });
        Days(s, 101); var checkpoint = s.Capture();
        var restored = SimulationSession.Restore(checkpoint);
        Assert.Equal(Result(s), Result(restored));
        s.ApplyChanges(s.Configuration with { Team = new(7, 3), WorkItemsPerDay = 1.5m }, "Same change");
        restored.ApplyChanges(restored.Configuration with { Team = new(7, 3), WorkItemsPerDay = 1.5m }, "Same change");
        Days(s, 100); Days(restored, 100);
        Assert.Equal(Result(s), Result(restored));
        Assert.Equal(s.Capture().ArrivalRandomState, restored.Capture().ArrivalRandomState);
        Assert.Equal(101, checkpoint.CurrentDay); Assert.Equal(80, checkpoint.WorkItems.Count);
        Assert.Equal(s.WorkItems.Count, s.WorkItems.Select(w => w.Id).Distinct().Count());
    }
    [Fact]
    public void ArrivalsUseConfiguredDistributionsAndUniqueIdsIncludingExistingIds()
    {
        var scenario = Scenario(count: 0) with { WorkItems = new[] { new WorkItem("LIVE-1", "Existing", 5, 1, 2) } };
        var s = new SimulationSession(scenario, new(new()) { ArrivalMode = WorkArrivalMode.ContinuousArrival, WorkItemsPerDay = 1,
            DevelopmentEffort = new TriangularEffort(1, 3, 8), CodeReviewEffort = new FixedEffort(.3), TestingEffort = new FixedEffort(.7) });
        Days(s, 1000);
        Assert.Equal(1001, s.WorkItems.Select(w => w.Id).Distinct().Count());
        Assert.All(s.WorkItems.Skip(1), w => { Assert.InRange(w.DevelopmentEffort, 1, 8); Assert.Equal(.3, w.CodeReviewEffort); Assert.Equal(.7, w.TestingEffort); });
        Assert.True(s.WorkItems.Skip(1).Select(w => w.DevelopmentEffort).Distinct().Count() > 1);
    }
    [Fact]
    public void FixedBacklogNeverGeneratesArrivalsAndDependenciesSurviveRestore()
    {
        var s = new SimulationSession(Scenario(count: 0) with { WorkItems = new[] { new WorkItem("A", "A", 1, 1, 1), new WorkItem("B", "B", 1, 1, 1, ["A"]) } });
        Days(s, 2); var r = SimulationSession.Restore(s.Capture()); Days(r, 10);
        Assert.Equal(2, r.WorkItems.Count); Assert.True(r.WorkItems[1].DevelopmentStartedDay >= r.WorkItems[0].DoneDay);
    }
    [Fact]
    public void InvalidInterventionIsAtomicAndZeroDayResultsAreDefined()
    {
        var s = Arrivals(.8m); Assert.Equal(0, s.GetResult().Throughput); Assert.Empty(s.GetResult().Days);
        Assert.Throws<ScenarioValidationException>(() => s.ApplyChanges(s.Configuration with { DevelopmentWipLimit = 0 }));
        Assert.Throws<ScenarioValidationException>(() => s.ApplyChanges(s.Configuration with { WorkItemsPerDay = -1 }));
        Assert.Empty(s.Changes); Assert.Equal(5, s.Configuration.DevelopmentWipLimit);
    }
}
