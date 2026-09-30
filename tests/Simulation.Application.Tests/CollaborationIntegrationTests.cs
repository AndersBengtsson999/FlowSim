using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.Infrastructure;
using Xunit;

namespace Simulation.Application.Tests;

public class CollaborationIntegrationTests
{
    private static SimulationRequest Request => new() { NumberOfWorkItems = 40, DevelopmentWipLimit = 2, SimulationDays = 80,
        DevelopmentEffort = 10, CodeReviewEffort = .2, TestingEffort = .2 };
    private static void Days(LiveSimulation live, int count) { for (var i = 0; i < count; i++) Assert.True(live.Step()); }
    private static string Result(LiveSimulation live) => JsonSerializer.Serialize(live.Session.GetResult());

    [Fact]
    public void RunLiveSavedCheckpointAndContinuationHaveIdenticalCollaborationHistory()
    {
        var request = Request with { DevelopmentDistribution = new TriangularEffort(5, 10, 15),
            Quality = new() { Enabled = true, CodeReviewDefectProbability = .3, TestingDefectProbability = .2 } };
        var live = LiveSimulation.Start(request, WorkArrivalMode.FixedBacklog);
        Days(live, 20); var checkpoint = live.CreateCheckpoint("Collaboration");
        Assert.Contains(live.Session.Days, d => d.CollaborationDevelopmentCapacity > 0);
        var loaded = LiveSessionJson.Load(LiveSessionJson.Save(live));
        Assert.Equal(Result(live), Result(loaded));
        Days(live, 60); Days(loaded, 60);
        Assert.Equal(JsonSerializer.Serialize(new SimulationEngine().Run(request.ToScenario())), Result(live));
        Assert.Equal(Result(live), Result(loaded));
        live.RestoreCheckpoint(checkpoint.Id); loaded.RestoreCheckpoint(checkpoint.Id);
        foreach (var session in new[] { live, loaded })
        {
            session.Session.ApplyChanges(session.Session.Configuration with { DevelopmentWipLimit = 3 }, "WIP 3");
            Days(session, 60);
        }
        Assert.Equal(Result(live), Result(loaded));
        Assert.Equal("0.2", live.Session.GetResult().SimulationModelVersion);
    }

    [Fact]
    public void LifetimeRollingAndBeforeAfterUtilizationUseConsumedCapacity()
    {
        var live = LiveSimulation.Start(Request with { DevelopmentEffort = 100 }, WorkArrivalMode.FixedBacklog);
        Days(live, 2);
        Assert.Equal(.8, live.Recent.DeveloperUtilization, 12);
        Assert.Equal(.8, live.Session.GetResult().DeveloperUtilization, 12);
        Assert.Equal(.8, LivePerformance.Rolling(live.Session, 2).DeveloperUtilization!.Value, 12);
        Assert.Equal(3, live.CurrentSnapshot.DevelopmentWork);
        Assert.Equal(4, live.CurrentSnapshot.UsedDevelopmentCapacity);
        live.Session.ApplyChanges(live.Session.Configuration with { DevelopmentWipLimit = 3 });
        Days(live, 2);
        var comparison = LivePerformance.Compare(live.Session, live.Session.Changes.Single(), 2);
        Assert.Equal(.8, comparison.Before.DeveloperUtilization!.Value, 12);
        Assert.Equal(1, comparison.After.DeveloperUtilization!.Value, 12);
    }

    [Fact]
    public void OldModelDocumentsAreExplicitlyRejected()
    {
        var scenario = new ScenarioDefinition(Guid.NewGuid(), Request);
        var experiment = new Experiment(Guid.NewGuid(), "Old model", "", new[] { scenario }, scenario.Id, new());
        static string Old(string json) => json.Replace("\"0.2\"", "\"0.1\"");
        Assert.Throws<JsonException>(() => ExperimentJson.LoadScenario(Old(ExperimentJson.SaveScenario(scenario))));
        Assert.Throws<JsonException>(() => ExperimentJson.LoadExperiment(Old(ExperimentJson.SaveExperiment(experiment))));
        var live = LiveSimulation.Start(Request); Days(live, 2);
        Assert.Throws<JsonException>(() => LiveSessionJson.Load(Old(LiveSessionJson.Save(live))));
        Assert.Throws<ScenarioValidationException>(() => LiveSimulation.Restore(live.Capture() with { SimulationModelVersion = "0.1" }));
        Assert.Equal(scenario, ExperimentJson.LoadScenario(ExperimentJson.SaveScenario(scenario)));
    }
}
