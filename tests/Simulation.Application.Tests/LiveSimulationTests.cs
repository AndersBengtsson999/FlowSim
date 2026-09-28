using System.Diagnostics;
using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Simulation.Application.Tests;

public sealed class LiveSimulationTests(ITestOutputHelper output)
{
    private static void Days(LiveSimulation live, int count) { for (var i = 0; i < count; i++) Assert.True(live.Step()); }
    private static string Result(LiveSimulation live) => JsonSerializer.Serialize(live.Session.GetResult());
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(19)] [InlineData(20)] [InlineData(21)] [InlineData(100)]
    public void RollingWindowUsesActualDurationAndCapacityTotals(int count)
    {
        var live = LiveSimulation.Start(LiveSimulation.Demo); Days(live, count);
        var days = live.Session.Days.TakeLast(20).ToArray(); var recent = live.Recent;
        Assert.Equal(Math.Min(20, count), recent.Days);
        var completed = live.Session.WorkItems.Count(w => w.DoneDay > Math.Max(0, count - 20) && w.DoneDay <= count);
        Assert.Equal(completed, recent.Completed);
        Assert.Equal(count == 0 ? 0 : completed * 5.0 / days.Length, recent.ThroughputPerFiveDays);
        Assert.Equal(count == 0 ? 0 : days.Average(d => d.TotalWip), recent.AverageWip);
        Assert.Equal(count == 0 ? 0 : days.Sum(d => d.UsedDeveloperCapacity) / days.Sum(d => d.AvailableDeveloperCapacity), recent.DeveloperUtilization);
        Assert.Equal(count == 0 ? 0 : days.Sum(d => d.UsedTesterCapacity) / days.Sum(d => d.AvailableTesterCapacity), recent.TesterUtilization);
    }
    [Fact]
    public void RollingUtilizationUsesChangingDenominatorsAndHandlesZeroCapacity()
    {
        var live = LiveSimulation.Start(LiveSimulation.Demo); Days(live, 10);
        live.Session.ApplyChanges(live.Session.Configuration with { Team = new(0, 0) }); Days(live, 10);
        Assert.Equal(live.Session.Days.Sum(d => d.UsedDeveloperCapacity) / 50, live.Recent.DeveloperUtilization);
        Assert.Equal(live.Session.Days.Sum(d => d.UsedTesterCapacity) / 20, live.Recent.TesterUtilization);
        Days(live, 20); Assert.Equal(0, live.Recent.DeveloperUtilization); Assert.Equal(0, live.Recent.TesterUtilization);
    }
    [Fact]
    public void SaveLoadCheckpointsAndContinueExactly()
    {
        var live = LiveSimulation.Start(LiveSimulation.Demo with { DevelopmentDistribution = new TriangularEffort(1, 3, 8),
            Quality = new() { Enabled = true, CodeReviewDefectProbability = .3, TestingDefectProbability = .4, TestingReworkEffortDistribution = new TriangularEffort(.1, 1, 2) } });
        Days(live, 101); var cp = live.CreateCheckpoint("Baseline");
        live.Session.ApplyChanges(live.Session.Configuration with { Team = new(5, 3), WorkItemsPerDay = 1.2m }, "Add tester");
        Days(live, 29);
        var loaded = LiveSessionJson.Load(LiveSessionJson.Save(live));
        Assert.Equal(Result(live), Result(loaded)); Assert.Single(loaded.Checkpoints); Assert.Single(loaded.Session.Changes);
        Assert.Equal("Add tester", loaded.Session.Changes[0].Label);
        Days(live, 70); Days(loaded, 70); Assert.Equal(Result(live), Result(loaded));
        live.RestoreCheckpoint(cp.Id); loaded.RestoreCheckpoint(cp.Id); Assert.Equal(101, live.Session.CurrentDay);
        Assert.Empty(live.Session.Changes);
        live.Session.ApplyChanges(live.Session.Configuration with { Team = new(5, 2, 1.4, 1) }, "AI-assisted development");
        loaded.Session.ApplyChanges(loaded.Session.Configuration with { Team = new(5, 2, 1.4, 1) }, "AI-assisted development");
        Days(live, 99); Days(loaded, 99); Assert.Equal(Result(live), Result(loaded));
        Assert.Equal(live.Session.Capture().NextWorkItemId, loaded.Session.Capture().NextWorkItemId);
        live.DeleteCheckpoint(cp.Id); Assert.Empty(live.Checkpoints); Assert.Single(loaded.Checkpoints);
    }
    [Fact]
    public void SafetyLimitStopsExactlyAndCanBeExtended()
    {
        var live = LiveSimulation.Start(LiveSimulation.Demo); live.SafetyLimit = 10; Days(live, 10);
        var before = Result(live); Assert.False(live.Step()); Assert.Equal(before, Result(live));
        live.SafetyLimit = 11; Assert.True(live.Step()); Assert.Equal(11, live.Session.CurrentDay);
    }
    [Fact]
    public void InvalidDocumentsRejected()
    {
        var live = LiveSimulation.Start(LiveSimulation.Demo); var doc = live.Capture();
        Assert.Throws<ScenarioValidationException>(() => LiveSimulation.Restore(doc with { SchemaVersion = 99 }));
        Assert.Throws<ScenarioValidationException>(() => LiveSimulation.Restore(doc with { SimulationModelVersion = "future" }));
        Assert.Throws<ScenarioValidationException>(() => LiveSimulation.Restore(doc with { State = doc.State with { ArrivalAccumulator = 1.5m } }));
    }
    [Fact]
    public void ThousandDayContinuousRunCanBeSavedAndContinued()
    {
        var timer = Stopwatch.StartNew(); var live = LiveSimulation.Start(LiveSimulation.Demo); Days(live, 1000);
        var elapsed = timer.Elapsed; var json = LiveSessionJson.Save(live); var loaded = LiveSessionJson.Load(json);
        Days(live, 2); Days(loaded, 2); Assert.Equal(Result(live), Result(loaded));
        Assert.Equal(801, live.Session.WorkItems.Count);
        output.WriteLine($"1000 days: {elapsed.TotalMilliseconds:0} ms; saved JSON: {json.Length:N0} chars; process working set: {Environment.WorkingSet:N0} bytes.");
    }
}
