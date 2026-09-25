using Simulation.Core;

namespace Simulation.Application;

/// <summary>Simple UI request; explicit heterogeneous work/dependencies can be supplied to Core.</summary>
public sealed record SimulationRequest
{
    public string Name { get; init; } = "Simulation Model v0.1 baseline";
    public int DeveloperCount { get; init; } = 5;
    public int TesterCount { get; init; } = 2;
    public double DeveloperCapacityPerDay { get; init; } = 1;
    public double TesterCapacityPerDay { get; init; } = 1;
    public int DevelopmentWipLimit { get; init; } = 5;
    public int CodeReviewWipLimit { get; init; } = 3;
    public int TestingWipLimit { get; init; } = 3;
    public int NumberOfWorkItems { get; init; } = 30;
    public int SimulationDays { get; init; } = 100;
    public double DevelopmentEffort { get; init; } = 5;
    public double CodeReviewEffort { get; init; } = 1;
    public double TestingEffort { get; init; } = 2;

    public SimulationScenario ToScenario()
    {
        if (NumberOfWorkItems < 0 || NumberOfWorkItems > 2000 || SimulationDays > 3650
            || (long)NumberOfWorkItems * SimulationDays > 1_000_000)
            throw new ScenarioValidationException("Maximum 2,000 items, 3,650 days and 1,000,000 item-days per run.");
        // Validate effort settings even when there are no generated items.
        if (new[] { DevelopmentEffort, CodeReviewEffort, TestingEffort }.Any(e => !double.IsFinite(e) || e < 0))
            throw new ScenarioValidationException("Efforts must be finite and nonnegative.");
        var scenario = new SimulationScenario(Name, SimulationDays,
            new Team(DeveloperCount, TesterCount, DeveloperCapacityPerDay, TesterCapacityPerDay),
            DevelopmentWipLimit, CodeReviewWipLimit, TestingWipLimit,
            Enumerable.Range(1, NumberOfWorkItems).Select(id =>
                new WorkItem($"STORY-{id}", $"Story {id}", DevelopmentEffort, CodeReviewEffort, TestingEffort)).ToArray());
        ScenarioValidator.Validate(scenario);
        return scenario;
    }
}
