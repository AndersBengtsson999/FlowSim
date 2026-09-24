using Simulation.Core;

namespace Simulation.Application;

public sealed record SimulationRequest
{
    public int NumberOfDevelopers { get; init; } = 5;
    public int NumberOfTesters { get; init; } = 2;
    public double DeveloperCapacity { get; init; } = 2;
    public double TesterCapacity { get; init; } = 2;
    public int WipLimit { get; init; } = 8;
    public int NumberOfWorkItems { get; init; } = 50;
    public int DurationDays { get; init; } = 60;
    public int SprintLength { get; init; } = 10;
    public int ReleaseInterval { get; init; } = 10;
    public int RandomSeed { get; init; } = 42;
    public double Size { get; init; } = 5;
    public double Complexity { get; init; } = 1.5;
    public double DependencyProbability { get; init; } = 0.15;

    public SimulationScenario ToScenario()
    {
        if (NumberOfWorkItems < 0 || NumberOfWorkItems > 2000 || DurationDays > 3650
            || (long)NumberOfWorkItems * DurationDays > 1_000_000)
            throw new ArgumentException("Max 2 000 objekt, 3 650 dagar och 1 000 000 objekt-dagar per körning.");
        if (!double.IsFinite(DependencyProbability) || DependencyProbability is < 0 or > 1)
            throw new ArgumentException("Beroendesannolikhet måste vara mellan 0 och 1.");
        if (!double.IsFinite(Size) || Size <= 0 || !double.IsFinite(Complexity) || Complexity <= 0)
            throw new ArgumentException("Storlek och komplexitet måste vara positiva, ändliga tal.");
        var random = new Random(RandomSeed);
        var items = Enumerable.Range(1, NumberOfWorkItems)
            .Select(id => new WorkItem(id, $"Work item {id}", Size, Complexity)).ToArray();
        var dependencies = new List<Dependency>();
        for (var id = 2; id <= NumberOfWorkItems; id++)
            if (random.NextDouble() < DependencyProbability)
                dependencies.Add(new Dependency(id, random.Next(1, id)));
        var scenario = new SimulationScenario(
            new Organization("Simulated organization", [new Team("Team 1", NumberOfDevelopers, NumberOfTesters, WipLimit)]),
            items, dependencies, DurationDays, DeveloperCapacity, TesterCapacity,
            SprintLength, ReleaseInterval, RandomSeed);
        ScenarioValidator.Validate(scenario);
        return scenario;
    }
}
