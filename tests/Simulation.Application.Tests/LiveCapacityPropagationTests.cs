using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Xunit;
using Xunit.Abstractions;

namespace Simulation.Application.Tests;

public sealed class LiveCapacityPropagationTests(ITestOutputHelper output)
{
    private static void ToDay(LiveSimulation live, int day) { while (live.Session.CurrentDay < day) Assert.True(live.Step()); }
    private static string Json(object value) => JsonSerializer.Serialize(value);
    private static void Increase(LiveSimulation live) => live.Session.ApplyChanges(
        live.Session.Configuration with { Team = live.Session.Configuration.Team with { DeveloperCount = 5000 } }, "Developers to 5000");
    private static SimulationRequest Supplied => LiveSimulation.Demo with
    { DeveloperCount = 1, DevelopmentWipLimit = 40, CodeReviewWipLimit = 40, TestingWipLimit = 10 };

    private void Report(string label, LiveSimulation live, int first, int last)
    {
        var days = live.Session.Days.Where(d => d.Day + 1 >= first && d.Day + 1 <= last).ToArray();
        var p = LivePerformance.Period(live.Session, first, last);
        output.WriteLine($"{label} {first}-{last}: dev available/day={days.Average(d => d.AvailableDeveloperCapacity):F4}, used/day={days.Average(d => d.UsedDeveloperCapacity):F4}, dev active={days.Average(d => d.DevelopmentWip):F4}, review waiting={p.Review.Average:F4}, review active={days.Average(d => d.ReviewWip):F4}, test waiting={p.Testing.Average:F4}, test active={days.Average(d => d.TestingWip):F4}, tester available/day={days.Average(d => d.AvailableTesterCapacity):F4}, used/day={days.Average(d => d.UsedTesterCapacity):F4}, tester utilization={p.TesterUtilization:P4}, throughput/5={p.Throughput:F4}, dev utilization={p.DeveloperUtilization:P4}");
    }

    [Fact]
    public void DemoMoreDevelopersCanLeaveTestingUnchanged()
    {
        var live = LiveSimulation.Start(LiveSimulation.Demo); ToDay(live, 100);
        var control = new LiveSimulation(SimulationSession.Restore(live.Session.Capture()));
        Increase(live); ToDay(live, 140); ToDay(control, 140);
        Report("Demo before", live, 81, 100); Report("Demo after", live, 101, 120); Report("Demo settled", live, 121, 140);
        var before = LivePerformance.Period(live.Session, 81, 100);
        var after = LivePerformance.Period(live.Session, 121, 140);
        Assert.Equal(before.TesterUtilization, after.TesterUtilization);
        Assert.Equal(before.Throughput, after.Throughput);
        Assert.Equal(LivePerformance.Rolling(control.Session).TesterUtilization, after.TesterUtilization);
        Assert.Equal(32, live.Session.Days.Skip(120).Sum(d => d.UsedTesterCapacity));
        Assert.All(live.Session.Days.Skip(100), d =>
        {
            Assert.Equal(5000, d.AvailableDeveloperCapacity);
            Assert.InRange(d.DevelopmentWork, 0, 7.5);
            Assert.InRange(d.ReviewWork, 0, 3);
            Assert.Equal(2, d.AvailableTesterCapacity);
        });
        Assert.Equal(112, live.Session.WorkItems.Count); // 140 × 0.8, unchanged supply.
    }

    [Fact]
    public void DeveloperInterventionPreservesHistoryWorkQueuesArrivalsAndRandomStates()
    {
        var live = LiveSimulation.Start(LiveSimulation.Demo with { DevelopmentDistribution = new TriangularEffort(2, 5, 8) });
        ToDay(live, 101); var before = live.Session.Capture();
        Increase(live); var after = live.Session.Capture();
        Assert.Equal(Json(before.WorkItems), Json(after.WorkItems));
        Assert.Equal(Json(before.Days), Json(after.Days));
        Assert.Equal(before.CurrentDay, after.CurrentDay);
        Assert.Equal(before.ArrivalAccumulator, after.ArrivalAccumulator);
        Assert.Equal(before.NextWorkItemId, after.NextWorkItemId);
        Assert.Equal(before.ArrivalRandomState, after.ArrivalRandomState);
        Assert.Equal(before.DiscoveryRandomState, after.DiscoveryRandomState);
        Assert.Equal(before.ReworkRandomState, after.ReworkRandomState);
        Assert.Equal(before.Configuration with { Team = before.Configuration.Team with { DeveloperCount = 5000 } }, after.Configuration);
        var change = Assert.Single(after.Changes);
        Assert.Equal(101, change.Day); Assert.Equal(5, change.Before.Team.DeveloperCount); Assert.Equal(5000, change.After.Team.DeveloperCount);
        var control = new LiveSimulation(SimulationSession.Restore(before));
        ToDay(live, 110); ToDay(control, 110);
        Assert.All(live.Session.Days.Take(101), d => Assert.Equal(5, d.AvailableDeveloperCapacity));
        Assert.All(live.Session.Days.Skip(101), d => Assert.Equal(5000, d.AvailableDeveloperCapacity));
        Assert.Equal(Json(before.Days), Json(live.Session.Days.Take(101)));
        Assert.Equal(control.Session.Capture().ArrivalAccumulator, live.Session.Capture().ArrivalAccumulator);
        Assert.Equal(control.Session.Capture().ArrivalRandomState, live.Session.Capture().ArrivalRandomState);
        Assert.Equal(control.Session.WorkItems.Select(w => (w.Id, w.CreatedDay, w.DevelopmentEffort)), live.Session.WorkItems.Select(w => (w.Id, w.CreatedDay, w.DevelopmentEffort)));
    }

    [Fact]
    public void SuppliedFlowPropagatesThroughEveryStageAndIncreasesTesting()
    {
        var live = LiveSimulation.Start(Supplied, rate: 4m); ToDay(live, 100);
        var control = new LiveSimulation(SimulationSession.Restore(live.Session.Capture()));
        Increase(live); ToDay(live, 140); ToDay(control, 140);
        foreach (var d in live.Session.Days.Skip(98).Take(12))
            output.WriteLine($"Day {d.Day + 1}: dev {d.UsedDeveloperCapacity}/{d.AvailableDeveloperCapacity}; active dev/review/test {d.DevelopmentWip}/{d.ReviewWip}/{d.TestingWip}; waiting review/test {d.WaitingForCodeReviewCount}/{d.WaitingForTestingCount}; test {d.UsedTesterCapacity}/{d.AvailableTesterCapacity}; done {d.DoneCount}");
        Report("Supplied before", live, 81, 100); Report("Supplied after", live, 101, 120); Report("Supplied settled", live, 121, 140); Report("Low-developer control", control, 121, 140);
        var high = LivePerformance.Rolling(live.Session); var low = LivePerformance.Rolling(control.Session);
        Assert.True(high.TesterUtilization > low.TesterUtilization);
        Assert.Equal(1, high.TesterUtilization);
        Assert.True(high.Testing.Average > low.Testing.Average);
        Assert.True(high.Completed > low.Completed);
        int Transitions(LiveSimulation l, WorkItemStatus status) => l.Session.WorkItems.SelectMany(w => w.Transitions).Count(t => t.To == status && (status is WorkItemStatus.Development or WorkItemStatus.CodeReview or WorkItemStatus.Testing ? t.Day >= 100 : t.Day > 100));
        foreach (var stage in new[] { WorkItemStatus.Development, WorkItemStatus.WaitingForCodeReview, WorkItemStatus.CodeReview, WorkItemStatus.WaitingForTesting, WorkItemStatus.Testing, WorkItemStatus.Done })
        {
            var highCount = Transitions(live, stage); var lowCount = Transitions(control, stage);
            output.WriteLine($"Transitions since intervention to {stage}: high={highCount}, low={lowCount}");
            Assert.True(highCount > lowCount, $"Propagation did not increase at {stage}: {highCount} vs {lowCount}");
        }
        var completedAfter = live.Session.WorkItems.Where(w => w.DoneDay > 100).ToArray();
        Assert.NotEmpty(completedAfter);
        foreach (var item in completedAfter)
        {
            Assert.Equal(new[] { WorkItemStatus.Development, WorkItemStatus.WaitingForCodeReview, WorkItemStatus.CodeReview, WorkItemStatus.WaitingForTesting, WorkItemStatus.Testing, WorkItemStatus.Done }, item.Transitions.Select(t => t.To));
            Assert.True(item.CodeReviewStartedDay >= item.DevelopmentCompletedDay);
            Assert.True(item.TestingStartedDay >= item.CodeReviewCompletedDay);
            Assert.Equal(2, live.Session.Days.SelectMany(d => d.Items).Where(w => w.Id == item.Id).Sum(w => w.TestingWork));
        }
        foreach (var d in live.Session.Days)
        {
            Assert.Equal(d.Items.Sum(w => w.TestingWork), d.UsedTesterCapacity);
            Assert.Equal(2, d.AvailableTesterCapacity);
            Assert.InRange(d.UsedTesterCapacity, 0, 2);
            Assert.All(d.Items, w => Assert.True(w.TestingWork == 0 || w.StateDuringDay == WorkItemStatus.Testing));
        }
    }

    [Fact]
    public void RollingTesterUtilizationMovesWithRealHistoryAndChangingDenominators()
    {
        var live = LiveSimulation.Start(Supplied, rate: 4m); ToDay(live, 100); Increase(live);
        var values = new List<double?>();
        for (var day = 101; day <= 145; day++)
        {
            if (day == 115) live.Session.ApplyChanges(live.Session.Configuration with { Team = live.Session.Configuration.Team with { TesterCount = 4 } }, "Test denominator");
            live.Step(); var p = LivePerformance.Rolling(live.Session);
            var days = live.Session.Days.Skip(day - 20).Take(20).ToArray();
            Assert.Equal(day - 19, p.FirstDay); Assert.Equal(day, p.LastDay); Assert.Equal(20, p.AvailableDays);
            Assert.Equal(days.Sum(d => d.UsedTesterCapacity) / days.Sum(d => d.AvailableTesterCapacity), p.TesterUtilization);
            values.Add(p.TesterUtilization);
            if (day == 120)
            {
                Assert.NotEqual(days.Average(d => d.UsedTesterCapacity / d.AvailableTesterCapacity), p.TesterUtilization);
                Assert.Equal(52, days.Sum(d => d.AvailableTesterCapacity)); // 14 × 2 + 6 × 4.
            }
        }
        Assert.True(values.Distinct().Count() > 2);
        Assert.Equal(1, values[^1]);
    }

    [Theory]
    [InlineData(1)] [InlineData(5000)]
    public void FixedBacklogRunAndLiveHaveIdenticalDailyFlow(int developers)
    {
        var request = Supplied with { DeveloperCount = developers, NumberOfWorkItems = 200, SimulationDays = 50 };
        var run = new SimulationEngine().Run(request.ToScenario());
        var live = LiveSimulation.Start(request, WorkArrivalMode.FixedBacklog); ToDay(live, 50);
        Assert.Equal(Json(run), Json(live.Session.GetResult()));
    }
}
