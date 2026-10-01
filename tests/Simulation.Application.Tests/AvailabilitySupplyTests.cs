using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.Infrastructure;
using Xunit;

namespace Simulation.Application.Tests;

public sealed class AvailabilitySupplyTests
{
    private static SimulationRequest Request => LiveSimulation.Demo with { DeveloperCount = 5, TesterCount = 2,
        DevelopmentWipLimit = 5, DevelopmentEffort = 100, DevelopmentDistribution = new FixedEffort(100) };
    private static void Days(LiveSimulation live, int n) { for (var i = 0; i < n; i++) Assert.True(live.Step()); }
    private static string Json(object value) => JsonSerializer.Serialize(value);
    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Theory]
    [InlineData(1, 5)] [InlineData(.85, 4.25)] [InlineData(0, 0)]
    public void AvailabilityScalesPoolAndFractionalCapacityIsConsumed(double availability, double expected)
    {
        var live = LiveSimulation.Start(Request with { DeveloperAvailability = availability }, WorkArrivalMode.AlwaysAvailable);
        Days(live, 1); var d = live.CurrentSnapshot;
        Assert.Equal(expected, d.AvailableDeveloperCapacity, 12);
        Assert.Equal(expected, d.UsedDeveloperCapacity, 12);
        Assert.Equal(expected, d.DevelopmentWork, 12);
        Assert.Equal(5, live.Session.WorkItems.Count);
        if (availability == 0) {
            Assert.Null(LivePerformance.Rolling(live.Session, 20).DeveloperUtilization);
            Assert.Null(LiveStatusProjection.From(live.Session).DeveloperUtilization);
            Assert.True(double.IsFinite(live.Session.GetResult().DeveloperUtilization));
            Days(live, 50); Assert.Equal(5, live.Session.WorkItems.Count);
        } else Assert.Equal(1, LivePerformance.Rolling(live.Session, 20).DeveloperUtilization);
    }

    [Theory]
    [InlineData(2, 4, 3, 2)] [InlineData(3, 4.25, 3.625, 1.25)]
    public void CollaborationConsumesFractionalPoolWithoutScalingPerItemLimit(int wip, double used, double effective, double collaboration)
    {
        var live = LiveSimulation.Start(Request with { DeveloperAvailability = .85, DevelopmentWipLimit = wip }, WorkArrivalMode.AlwaysAvailable);
        Days(live, 1); var d = live.CurrentSnapshot;
        Assert.Equal(used, d.UsedDeveloperCapacity, 12); Assert.Equal(effective, d.DevelopmentWork, 12);
        Assert.Equal(collaboration, d.CollaborationDevelopmentCapacity, 12);
        Assert.Equal(used / 4.25, LivePerformance.Rolling(live.Session, 20).DeveloperUtilization!.Value, 12);
    }

    [Theory]
    [InlineData(.75, 1.5)] [InlineData(0, 0)] [InlineData(1, 2)]
    public void TestersUseAvailableFractionalPool(double availability, double expected)
    {
        var live = LiveSimulation.Start(Request with { TesterAvailability = availability, DevelopmentDistribution = new FixedEffort(0),
            CodeReviewDistribution = new FixedEffort(0), TestingDistribution = new FixedEffort(100) }, WorkArrivalMode.AlwaysAvailable);
        Days(live, 3);
        Assert.Equal(expected, live.CurrentSnapshot.AvailableTesterCapacity, 12);
        Assert.Equal(expected, live.CurrentSnapshot.UsedTesterCapacity, 12);
        if (expected == 0) Assert.Null(LiveStatusProjection.From(live.Session).TesterUtilization);
    }

    [Fact]
    public void AvailabilityPreservesReviewThenReworkBeforeDevelopment()
    {
        var scenario = new SimulationScenario("Priority", 10, new(5, 1), 1, 1, 1,
            [new("A", "A", 1, 2, 1), new("B", "B", 10, 2, 1)])
        { Quality = new() { Enabled = true, CodeReviewDefectProbability = 1, CodeReviewReworkEffortDistribution = new FixedEffort(2) } };
        var session = new SimulationSession(scenario); session.AdvanceOneDay();
        session.ApplyChanges(session.Configuration with { Team = scenario.Team with { DeveloperAvailability = .2 } });
        var review = session.AdvanceOneDay(); Assert.Equal(1, review.ReviewWork); Assert.Equal(0, review.UsedDevelopmentCapacity);
        session.AdvanceOneDay(); var rework = session.AdvanceOneDay();
        Assert.Equal(1, rework.UsedReworkDeveloperCapacity); Assert.Equal(0, rework.UsedDevelopmentCapacity);
    }

    [Theory]
    [InlineData(-.01)] [InlineData(1.01)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void InvalidAvailabilityIsRejected(double value)
    {
        Assert.Throws<ScenarioValidationException>(() => LiveSimulation.Start(Request with { DeveloperAvailability = value }));
        Assert.Throws<ScenarioValidationException>(() => LiveSimulation.Start(Request with { TesterAvailability = value }));
    }

    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(5)] [InlineData(10)]
    public void AlwaysAvailableIsLazyAndIndependentOfRateWithAllWipLimitsPreserved(int wip)
    {
        var request = Request with { DevelopmentWipLimit = wip, DevelopmentDistribution = new FixedEffort(2) };
        var slow = LiveSimulation.Start(request, WorkArrivalMode.AlwaysAvailable, 0);
        var fast = LiveSimulation.Start(request, WorkArrivalMode.AlwaysAvailable, 2000);
        Days(slow, 1); Assert.Equal(wip, slow.Session.WorkItems.Count);
        Days(slow, 199); Days(fast, 200);
        Assert.Equal(Json(slow.Session.GetResult()), Json(fast.Session.GetResult()));
        Assert.True(slow.Session.GetResult().CompletedWorkItems > 0);
        Assert.All(slow.Session.Days, d => {
            Assert.Equal(0, d.BacklogCount); Assert.InRange(d.DevelopmentWip, 0, wip);
            Assert.InRange(d.ReviewWip, 0, request.CodeReviewWipLimit); Assert.InRange(d.TestingWip, 0, request.TestingWipLimit);
            Assert.InRange(d.UsedDeveloperCapacity, 0, d.AvailableDeveloperCapacity + 1e-10);
        });
    }

    [Fact]
    public void FixedRateAccumulatorIsPreservedAndFrozenOutsideFixedRate()
    {
        var live = LiveSimulation.Start(Request, WorkArrivalMode.ContinuousArrival, .8m);
        Days(live, 1); Assert.Empty(live.Session.WorkItems); Assert.Equal(.8m, live.Session.Capture().ArrivalAccumulator);
        live.Session.ApplyChanges(live.Session.Configuration with { ArrivalMode = WorkArrivalMode.AlwaysAvailable });
        Days(live, 2); Assert.Equal(5, live.Session.WorkItems.Count); Assert.Equal(.8m, live.Session.Capture().ArrivalAccumulator);
        live.Session.ApplyChanges(live.Session.Configuration with { ArrivalMode = WorkArrivalMode.ContinuousArrival, WorkItemsPerDay = .3m });
        Days(live, 1); Assert.Equal(6, live.Session.WorkItems.Count); Assert.Equal(.1m, live.Session.Capture().ArrivalAccumulator);
    }

    [Fact]
    public void AlwaysAvailableUsesExistingArrivalRandomStreamAndExistingBacklogFirst()
    {
        var request = Request with { DevelopmentWipLimit = 1, DevelopmentDistribution = new TriangularEffort(1, 3, 9) };
        var fixedRate = LiveSimulation.Start(request, WorkArrivalMode.ContinuousArrival, 1);
        var always = LiveSimulation.Start(request, WorkArrivalMode.AlwaysAvailable);
        Days(fixedRate, 1); Days(always, 1);
        Assert.Equal(Json(fixedRate.Session.Capture().WorkItems), Json(always.Session.Capture().WorkItems));
        Assert.Equal(fixedRate.Session.Capture().ArrivalRandomState, always.Session.Capture().ArrivalRandomState);
        var backlog = LiveSimulation.Start(request with { NumberOfWorkItems = 10 }, WorkArrivalMode.AlwaysAvailable);
        Days(backlog, 1); Assert.Equal(10, backlog.Session.WorkItems.Count);
    }

    [Fact]
    public void InterventionsPreserveHistoryAndRollingRatioUsesSums()
    {
        var live = LiveSimulation.Start(Request with { DevelopmentWipLimit = 2 }, WorkArrivalMode.AlwaysAvailable);
        Days(live, 10); var before = Json(live.Session.Days);
        live.Session.ApplyChanges(live.Session.Configuration with { Team = live.Session.Configuration.Team with { DeveloperAvailability = .85, TesterAvailability = .75 } });
        Assert.Equal(10, live.Session.Changes.Single().Day);
        Days(live, 10); Assert.Equal(before, Json(live.Session.Days.Take(10).ToArray()));
        Assert.Equal(5, live.Session.Days[9].AvailableDeveloperCapacity); Assert.Equal(4.25, live.Session.Days[10].AvailableDeveloperCapacity);
        Assert.Equal(1.5, live.Session.Days[10].AvailableTesterCapacity);
        Assert.Equal(80 / 92.5, LivePerformance.Rolling(live.Session, 20).DeveloperUtilization!.Value, 12);
    }

    [Fact]
    public void AlwaysAvailableRunLiveCheckpointAndSaveAreDeterministic()
    {
        var request = Request with { SimulationDays = 100, ArrivalMode = WorkArrivalMode.AlwaysAvailable, DeveloperAvailability = .85, TesterAvailability = .75,
            DevelopmentDistribution = new TriangularEffort(1, 3, 7), Quality = new() { Enabled = true, CodeReviewDefectProbability = .2, TestingDefectProbability = .2 } };
        var live = LiveSimulation.Start(request, request.ArrivalMode); Days(live, 30); var checkpoint = live.CreateCheckpoint("30");
        var saved = LiveSessionJson.Save(live); var restored = LiveSessionJson.Load(saved);
        Days(live, 70); Days(restored, 70);
        Assert.Equal(Json(live.Capture()), Json(restored.Capture()));
        Assert.Equal(Json(new SimulationEngine().Run(request.ToScenario())), Json(live.Session.GetResult()));
        live.RestoreCheckpoint(checkpoint.Id); Days(live, 70);
        Assert.Equal(Json(restored.Session.GetResult()), Json(live.Session.GetResult()));
        var scenario = new ScenarioDefinition(Guid.NewGuid(), request);
        Assert.Equal(scenario, ExperimentJson.LoadScenario(ExperimentJson.SaveScenario(scenario)));
    }

    [Fact]
    public void ExperimentRoundTripAndComparisonDescribeAvailabilityAndSupply()
    {
        var baseline = new ScenarioDefinition(Guid.NewGuid(), Request with { ArrivalMode = WorkArrivalMode.ContinuousArrival });
        var changed = new ScenarioDefinition(Guid.NewGuid(), Request with { DeveloperAvailability = .8, TesterAvailability = .75, ArrivalMode = WorkArrivalMode.AlwaysAvailable });
        var experiment = new Experiment(Guid.NewGuid(), "Availability", "", new[] { baseline, changed }, baseline.Id, new());
        var loaded = ExperimentJson.LoadExperiment(ExperimentJson.SaveExperiment(experiment));
        Assert.Equal(changed, loaded.Scenarios[1]);
        var a = ScenarioParameters.Describe(baseline.Configuration); var b = ScenarioParameters.Describe(changed.Configuration);
        Assert.Equal("100%", a["Developer Availability"]); Assert.Equal("80%", b["Developer Availability"]);
        Assert.Equal("75%", b["Tester Availability"]); Assert.Equal("Fixed 0.8/day", a["Work Supply"]); Assert.Equal("Always available", b["Work Supply"]);
        var legacyScenario = ExperimentJson.LoadScenario(Fixture("model02-scenario.json"));
        var legacyExperiment = new Experiment(Guid.NewGuid(), "Legacy", "", new[] { legacyScenario }, legacyScenario.Id, new(), "0.2");
        var legacyJson = ExperimentJson.SaveExperiment(legacyExperiment).Replace("\"0.4\"", "\"0.2\"");
        Assert.Equal(1, ExperimentJson.LoadExperiment(legacyJson).Scenarios[0].Configuration.DeveloperAvailability);
    }

    [Fact]
    public void ActualModel02FixturesLoadAtFullAvailabilityAndContinueIdentically()
    {
        var scenario = ExperimentJson.LoadScenario(Fixture("model02-scenario.json"));
        Assert.Equal(1, scenario.Configuration.DeveloperAvailability); Assert.Equal(1, scenario.Configuration.TesterAvailability);
        var live = LiveSessionJson.Load(Fixture("model02-live.json"));
        Assert.Equal("0.2", live.OriginalModelVersion);
        Assert.Equal(1, live.Session.Configuration.Team.DeveloperAvailability);
        Assert.Equal(WorkArrivalMode.ContinuousArrival, live.Session.Configuration.ArrivalMode);
        var history = Json(live.Session.Days); Days(live, 15);
        var expected = JsonSerializer.Deserialize<SimulationResult>(Fixture("model02-result-day30.json"))! with { SimulationModelVersion = SimulationModel.Version };
        Assert.Equal(Json(expected), Json(live.Session.GetResult()));
        Assert.Equal(history, Json(live.Session.Days.Take(15).ToArray()));
        Assert.Equal("0.2", LiveSessionJson.Load(LiveSessionJson.Save(live)).OriginalModelVersion);
    }
}
