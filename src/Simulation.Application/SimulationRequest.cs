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

    public int RandomSeed { get; init; } = 12345;
    public DefectSettings Quality { get; init; } = new();
    // Null preserves the original per-stage fixed effort setting and mixed-effort Core scenarios.
    public IEffortDistribution? DevelopmentDistribution { get; init; }
    public IEffortDistribution? CodeReviewDistribution { get; init; }
    public IEffortDistribution? TestingDistribution { get; init; }

    public SimulationScenario ToScenario()
    {
        if (NumberOfWorkItems < 0 || NumberOfWorkItems > 2000 || SimulationDays > 3650
            || (long)NumberOfWorkItems * SimulationDays > 1_000_000)
            throw new ScenarioValidationException("Maximum 2,000 items, 3,650 days and 1,000,000 item-days per run.");
        var development = DevelopmentDistribution ?? new FixedEffort(DevelopmentEffort);
        var review = CodeReviewDistribution ?? new FixedEffort(CodeReviewEffort);
        var testing = TestingDistribution ?? new FixedEffort(TestingEffort);
        var scenario = new SimulationScenario(Name, SimulationDays,
            new Team(DeveloperCount, TesterCount, DeveloperCapacityPerDay, TesterCapacityPerDay),
            DevelopmentWipLimit, CodeReviewWipLimit, TestingWipLimit,
            EffortGenerator.Generate(NumberOfWorkItems, development, review, testing, RandomSeed), RandomSeed) { Quality = Quality };
        ScenarioValidator.Validate(scenario);
        return scenario;
    }
}
