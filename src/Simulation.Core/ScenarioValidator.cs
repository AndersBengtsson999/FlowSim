namespace Simulation.Core;

public sealed class ScenarioValidationException(string message) : ArgumentException(message);

public static class ScenarioValidator
{
    public static void Validate(SimulationScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        if (string.IsNullOrWhiteSpace(scenario.Name)) Fail("Scenario name is required.");
        if (scenario.SimulationDays <= 0) Fail("SimulationDays must be positive.");
        if (scenario.Team is null) Fail("Team configuration is required.");
        var team = scenario.Team!;
        if (team.DeveloperCount < 0 || team.TesterCount < 0) Fail("Resource counts cannot be negative.");
        if (!NonnegativeFinite(team.DeveloperCapacityPerDay) || !NonnegativeFinite(team.TesterCapacityPerDay)
            || !double.IsFinite(team.TotalDeveloperCapacity) || !double.IsFinite(team.TotalTesterCapacity))
            Fail("Capacities and total capacity must be finite and nonnegative.");
        if (scenario.DevelopmentWipLimit <= 0 || scenario.CodeReviewWipLimit <= 0 || scenario.TestingWipLimit <= 0)
            Fail("All active-stage WIP limits must be positive.");
        if (scenario.WorkItems is null) Fail("WorkItems collection is required.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in scenario.WorkItems!)
        {
            if (item is null) Fail("WorkItems cannot contain null entries.");
            if (string.IsNullOrWhiteSpace(item!.Id) || string.IsNullOrWhiteSpace(item.Name))
                Fail("WorkItem ID and name are required.");
            if (!ids.Add(item.Id)) Fail($"Duplicate WorkItem ID: {item.Id}.");
            if (!NonnegativeFinite(item.DevelopmentEffort) || !NonnegativeFinite(item.CodeReviewEffort)
                || !NonnegativeFinite(item.TestingEffort)) Fail($"Efforts must be finite and nonnegative: {item.Id}.");
            if (item.CreatedDay < 0) Fail($"CreatedDay cannot be negative: {item.Id}.");
            if (item.State != WorkItemStatus.Backlog || item.Transitions.Count != 0)
                Fail($"WorkItem must be a fresh backlog item: {item.Id}.");
        }
        var indegree = ids.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
        var children = ids.ToDictionary(id => id, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var item in scenario.WorkItems!)
        {
            foreach (var dependency in item.Dependencies.Distinct(StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(dependency) || !ids.Contains(dependency))
                    Fail($"Unknown dependency '{dependency}' on {item.Id}.");
                if (dependency == item.Id) Fail($"Self-dependency is not allowed: {item.Id}.");
                indegree[item.Id]++;
                children[dependency].Add(item.Id);
            }
        }
        var queue = new Queue<string>(indegree.Where(p => p.Value == 0).Select(p => p.Key));
        var visited = 0;
        while (queue.TryDequeue(out var id))
        {
            visited++;
            foreach (var child in children[id]) if (--indegree[child] == 0) queue.Enqueue(child);
        }
        if (visited != ids.Count) Fail("Circular dependency graph is not allowed.");
    }

    private static bool NonnegativeFinite(double value) => double.IsFinite(value) && value >= 0;
    private static void Fail(string message) => throw new ScenarioValidationException(message);
}
