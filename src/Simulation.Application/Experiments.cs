using Simulation.Core;

namespace Simulation.Application;

public enum ExperimentRunMode { SingleRun, MonteCarlo }
public sealed record ComparisonOptions(ExperimentRunMode Mode = ExperimentRunMode.SingleRun,
    bool CommonRandomNumbers = true, int BaseSeed = 12345, int MonteCarloRuns = 500);
public sealed record ScenarioDefinition(Guid Id, SimulationRequest Configuration)
{
    public string Name => Configuration.Name;
    public static ScenarioDefinition Create(SimulationRequest configuration) => new(Guid.NewGuid(), configuration);
    public ScenarioDefinition Duplicate() => Create(Configuration with { Name = Name + " Copy" });
}
public sealed record Experiment(Guid Id, string Name, string Description, IReadOnlyList<ScenarioDefinition> Scenarios,
    Guid BaselineId, ComparisonOptions Options, string SimulationModelVersion = SimulationModel.Version)
{
    public static Experiment Baseline()
    {
        var scenario = ScenarioDefinition.Create(BaselineScenario.CreateRequest() with { Name = "Baseline" });
        return new(Guid.NewGuid(), "New Experiment", "", Array.AsReadOnly(new[] { scenario }), scenario.Id, new());
    }
    public void Validate()
    {
        if (SimulationModelVersion != SimulationModel.Version) throw new ScenarioValidationException($"Unsupported simulation model version '{SimulationModelVersion}'. Expected {SimulationModel.Version}; no automatic migration is performed.");
        if (Id == Guid.Empty || string.IsNullOrWhiteSpace(Name) || Description is null)
            throw new ScenarioValidationException("An experiment requires an identity, name and description (which may be empty).");
        if (Scenarios is null || Scenarios.Count is < 1 or > 20 || Scenarios.Any(s => s is null || s.Id == Guid.Empty || s.Configuration is null)
            || Scenarios.Select(s => s.Id).Distinct().Count() != Scenarios.Count || !Scenarios.Any(s => s.Id == BaselineId))
            throw new ScenarioValidationException("An experiment requires 1–20 scenarios with unique identities and a baseline in that collection.");
        if (Options is null || !Enum.IsDefined(Options.Mode) || Options.MonteCarloRuns is < 1 or > 10000)
            throw new ScenarioValidationException("Invalid run mode or Monte Carlo run count (1–10,000).");
        foreach (var s in Scenarios) s.Configuration.ToScenario();
    }
}
public static class DemonstrationExperiments
{
    public static IReadOnlyList<string> Names { get; } = Array.AsReadOnly(new[] { "Developer Capacity", "Testing Capacity", "Development WIP", "Quality / Rework" });
    public static Experiment Create(string name)
    {
        var basis = ValidationScenarios.SteadyFlow() with { NumberOfWorkItems = 500 };
        ScenarioDefinition S(string title, SimulationRequest s) => ScenarioDefinition.Create(s with { Name = title });
        var scenarios = name switch
        {
            "Developer Capacity" => new[] { 1, 3, 5, 8, 10 }.Select(n => S($"{n} Developers", basis with { DeveloperCount = n })).ToArray(),
            "Testing Capacity" => new[] { 1, 2, 3, 5, 8 }.Select(n => S($"{n} Testers", basis with { TesterCount = n })).ToArray(),
            "Development WIP" => new[] { 2, 3, 5, 8, 12 }.Select(n => S($"WIP {n}", basis with { DevelopmentWipLimit = n })).ToArray(),
            "Quality / Rework" => new[] { 0d, .1, .2, .4 }.Select(p => S($"Review + Testing {p * 100:0}%", basis with
            { Quality = ValidationScenarios.DefectStressBase().Quality with { CodeReviewDefectProbability = p, TestingDefectProbability = p } })).ToArray(),
            _ => throw new ScenarioValidationException("Unknown demonstration experiment.")
        };
        int baseline = name == "Developer Capacity" || name == "Development WIP" ? 2 : name == "Testing Capacity" ? 1 : 0;
        return new(Guid.NewGuid(), name + " — Demonstration Experiment", "Illustrative values, not calibrated recommendations. Quality examples change BOTH discovery probabilities.",
            Array.AsReadOnly(scenarios), scenarios[baseline].Id, new());
    }
}
