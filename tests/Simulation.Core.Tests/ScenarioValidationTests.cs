using Simulation.Core;
using Xunit;
using static Simulation.Core.Tests.SimulationEngineTests;

namespace Simulation.Core.Tests;

public sealed class ScenarioValidationTests
{
    [Fact]
    public void DuplicateIdsAreRejected() => AssertInvalid(
        Scenario(items: [new("A", "A", 1, 1, 1), new("A", "B", 1, 1, 1)]), "Duplicate");

    [Fact]
    public void UnknownDependenciesAreRejected() => AssertInvalid(
        Scenario(items: [new("A", "A", 1, 1, 1, ["missing"])]), "Unknown dependency");

    [Fact]
    public void SelfDependenciesAreRejected() => AssertInvalid(
        Scenario(items: [new("A", "A", 1, 1, 1, ["A"])]), "Self-dependency");

    [Fact]
    public void CyclesAreRejected() => AssertInvalid(
        Scenario(items: [new("A", "A", 1, 1, 1, ["C"]), new("B", "B", 1, 1, 1, ["A"]),
            new("C", "C", 1, 1, 1, ["B"])]), "Circular");

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidCapacitiesAreRejected(double value)
    {
        AssertInvalid(Scenario(team: new Team(1, 1, value, 1)), "Capacities");
        AssertInvalid(Scenario(team: new Team(1, 1, 1, value)), "Capacities");
    }

    [Fact]
    public void OverflowingTotalCapacityIsRejected() =>
        AssertInvalid(Scenario(team: new Team(2, 1, double.MaxValue)), "Capacities");

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidWipLimitsAreRejected(int value)
    {
        AssertInvalid(Scenario(developmentWip: value), "WIP");
        AssertInvalid(Scenario(reviewWip: value), "WIP");
        AssertInvalid(Scenario(testingWip: value), "WIP");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidStageEffortsAreRejected(double value)
    {
        AssertInvalid(Scenario(items: [new("A", "A", value, 1, 1)]), "Efforts");
        AssertInvalid(Scenario(items: [new("A", "A", 1, value, 1)]), "Efforts");
        AssertInvalid(Scenario(items: [new("A", "A", 1, 1, value)]), "Efforts");
    }

    [Fact]
    public void InvalidCountsDurationIdentityAndCreationAreRejected()
    {
        AssertInvalid(Scenario(team: new Team(-1, 1)), "counts");
        AssertInvalid(Scenario(team: new Team(1, -1)), "counts");
        AssertInvalid(Scenario(days: 0), "SimulationDays");
        AssertInvalid(Scenario(days: -1), "SimulationDays");
        AssertInvalid(Scenario() with { Name = " " }, "name");
        AssertInvalid(Scenario(items: [new("", "A", 1, 1, 1)]), "ID");
        AssertInvalid(Scenario(items: [new("A", "", 1, 1, 1)]), "name");
        AssertInvalid(Scenario(items: [new("A", "A", 1, 1, 1, createdDay: -1)]), "CreatedDay");
    }

    [Fact]
    public void ExecutionReturnsDetachedResultsAndLeavesInputFresh()
    {
        var scenario = Scenario();
        var result = new SimulationEngine().Run(scenario);
        Assert.IsType<WorkItemResult>(result.WorkItems[0]);
        Assert.All(scenario.WorkItems, w => Assert.Equal(WorkItemStatus.Backlog, w.State));
        ScenarioValidator.Validate(scenario);
    }

    private static void AssertInvalid(SimulationScenario scenario, string message) =>
        Assert.Contains(message, Assert.Throws<ScenarioValidationException>(() => new SimulationEngine().Run(scenario)).Message);
}
