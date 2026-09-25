using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Xunit;

namespace Simulation.Application.Tests;

public sealed class SimulationRunnerTests
{
    [Fact]
    public void BaselineMatchesTheV01SpecificationExactly()
    {
        var scenario = BaselineScenario.Create();
        Assert.Equal(100, scenario.SimulationDays);
        Assert.Equal(new Team(5, 2, 1, 1), scenario.Team);
        Assert.Equal(5, scenario.DevelopmentWipLimit);
        Assert.Equal(3, scenario.CodeReviewWipLimit);
        Assert.Equal(3, scenario.TestingWipLimit);
        Assert.Equal(30, scenario.WorkItems.Count);
        Assert.Equal(30, scenario.WorkItems.Select(w => w.Id).Distinct().Count());
        Assert.All(scenario.WorkItems, w =>
        {
            Assert.Equal(5, w.DevelopmentEffort);
            Assert.Equal(1, w.CodeReviewEffort);
            Assert.Equal(2, w.TestingEffort);
            Assert.Empty(w.Dependencies);
            Assert.Equal(WorkItemStatus.Backlog, w.State);
        });
    }

    [Fact]
    public void BaselineRunsRepeatedlyWithIdenticalStatesTimingAndCapacity()
    {
        var scenario = BaselineScenario.Create();
        var engine = new SimulationEngine();
        var first = engine.Run(scenario);
        for (var run = 0; run < 5; run++)
            Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(engine.Run(scenario)));
        Assert.Equal(100, first.Days.Count);
        Assert.Equal(first.WorkItems.Count(w => w.State == WorkItemStatus.Done), first.CompletedWorkItems);
        Assert.Equal(first.CompletedWorkItems / 100.0, first.Throughput);
        Assert.All(first.Days, d =>
        {
            Assert.Equal(30, d.Items.Count);
            Assert.InRange(d.DevelopmentWork + d.ReviewWork, 0, 5);
            Assert.InRange(d.TestingWork, 0, 2);
            Assert.InRange(d.DevelopmentWip, 0, 5);
            Assert.InRange(d.ReviewWip, 0, 3);
            Assert.InRange(d.TestingWip, 0, 3);
        });
        Assert.All(scenario.WorkItems, w => Assert.Empty(w.Transitions));
    }

    [Fact]
    public void RequestEffortsAreIndependentRatherThanDerivedFromSize()
    {
        var scenario = new SimulationRequest { DevelopmentEffort = 7, CodeReviewEffort = 0.3, TestingEffort = 9 }.ToScenario();
        Assert.All(scenario.WorkItems, w =>
        {
            Assert.Equal(7, w.RemainingDevelopmentEffort);
            Assert.Equal(0.3, w.RemainingCodeReviewEffort);
            Assert.Equal(9, w.RemainingTestingEffort);
        });
    }

    [Fact]
    public void RunnerDelegatesToTheDomainEngine()
    {
        var request = new SimulationRequest();
        Assert.Equal(JsonSerializer.Serialize(new SimulationEngine().Run(request.ToScenario())),
            JsonSerializer.Serialize(new SimulationRunner().Run(request)));
    }

    [Fact]
    public void InvalidRequestsAreRejectedBeforeExecution()
    {
        var runner = new SimulationRunner();
        Assert.Throws<ScenarioValidationException>(() => runner.Run(new() { NumberOfWorkItems = -1 }));
        Assert.Throws<ScenarioValidationException>(() => runner.Run(new() { DeveloperCount = -1 }));
        Assert.Throws<ScenarioValidationException>(() => runner.Run(new() { NumberOfWorkItems = 2000, SimulationDays = 3650 }));
        Assert.Throws<ScenarioValidationException>(() => runner.Run(new() { NumberOfWorkItems = 0, TestingEffort = double.NaN }));
        Assert.Throws<ScenarioValidationException>(() => runner.Run(new() { CodeReviewWipLimit = 0 }));
    }

    [Fact]
    public void CancellationPropagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => new SimulationRunner().Run(new(), cancellation.Token));
    }
}
