namespace Simulation.Core;

public static class ScenarioValidator
{
    public static void Validate(SimulationScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        if (scenario.Organization?.Teams is not { Count: 1 })
            throw new ArgumentException("Inkrement 1 kräver exakt ett team.");
        var team = scenario.Organization.Teams[0];
        if (team.NumberOfDevelopers < 0 || team.NumberOfTesters < 0 || team.WipLimit <= 0)
            throw new ArgumentException("Personal får inte vara negativ och WIP måste vara större än noll.");
        if (scenario.DurationDays <= 0 || scenario.SprintLength <= 0 || scenario.ReleaseInterval <= 0)
            throw new ArgumentException("Simuleringslängd, sprintlängd och releaseintervall måste vara positiva.");
        if (!ValidCapacity(scenario.DeveloperCapacityPerDay) || !ValidCapacity(scenario.TesterCapacityPerDay)
            || !double.IsFinite(team.NumberOfDevelopers * scenario.DeveloperCapacityPerDay)
            || !double.IsFinite(team.NumberOfTesters * scenario.TesterCapacityPerDay))
            throw new ArgumentException("Kapacitet måste vara ändlig och minst noll.");
        ArgumentNullException.ThrowIfNull(scenario.WorkItems);
        ArgumentNullException.ThrowIfNull(scenario.Dependencies);
        if (scenario.WorkItems.Select(w => w.Id).Distinct().Count() != scenario.WorkItems.Count)
            throw new ArgumentException("Arbetsobjekt måste ha unika ID:n.");
        foreach (var item in scenario.WorkItems)
        {
            if (!double.IsFinite(item.Size) || item.Size <= 0 || !double.IsFinite(item.Complexity)
                || item.Complexity <= 0 || !double.IsFinite(item.Size * item.Complexity)
                || item.CreatedAt < 0 || item.Status != WorkItemStatus.Backlog
                || item.StartedAt is not null || item.CompletedAt is not null)
                throw new ArgumentException("Arbetsobjekt måste vara nya backlogobjekt med positiv, ändlig storlek och komplexitet.");
        }
        var indegree = scenario.WorkItems.ToDictionary(w => w.Id, _ => 0);
        var children = scenario.WorkItems.ToDictionary(w => w.Id, _ => new List<int>());
        foreach (var dependency in scenario.Dependencies.Distinct())
        {
            if (!indegree.ContainsKey(dependency.WorkItemId) || !indegree.ContainsKey(dependency.DependsOnWorkItemId))
                throw new ArgumentException("Beroenden måste referera till existerande arbetsobjekt.");
            indegree[dependency.WorkItemId]++;
            children[dependency.DependsOnWorkItemId].Add(dependency.WorkItemId);
        }
        var queue = new Queue<int>(indegree.Where(x => x.Value == 0).Select(x => x.Key));
        var visited = 0;
        while (queue.TryDequeue(out var id))
        {
            visited++;
            foreach (var child in children[id])
                if (--indegree[child] == 0) queue.Enqueue(child);
        }
        if (visited != scenario.WorkItems.Count)
            throw new ArgumentException("Cirkulära beroenden är inte tillåtna.");
    }

    private static bool ValidCapacity(double value) => double.IsFinite(value) && value >= 0;
}
