namespace Simulation.Core;

public sealed class SimulationEngine
{
    private sealed class State(WorkItem item)
    {
        public WorkItem Item { get; set; } = item;
        public double DevelopmentRemaining { get; set; } = item.Size * item.Complexity;
        public double TestingRemaining { get; set; } = item.Size;
    }

    public SimulationResult Run(SimulationScenario scenario, CancellationToken cancellationToken = default)
    {
        ScenarioValidator.Validate(scenario);
        var random = new Random(scenario.RandomSeed);
        var team = scenario.Organization.Teams[0];
        var states = scenario.WorkItems.OrderBy(w => w.Id).Select(w => new State(w)).ToArray();
        var byId = states.ToDictionary(s => s.Item.Id);
        var dependencies = scenario.Dependencies.Distinct().ToLookup(d => d.WorkItemId, d => d.DependsOnWorkItemId);
        var devCapacity = team.NumberOfDevelopers * scenario.DeveloperCapacityPerDay;
        var testCapacity = team.NumberOfTesters * scenario.TesterCapacityPerDay;
        var days = new List<DailySnapshot>();
        var releases = new List<Release>();
        var released = new HashSet<int>();

        for (var day = 0; day < scenario.DurationDays; day++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool IsBlocked(State s) => dependencies[s.Item.Id].Any(id => byId[id].Item.Status != WorkItemStatus.Done);
            var unfinished = states.Count(s => s.Item.CreatedAt <= day && s.Item.Status != WorkItemStatus.Done);
            var blocked = states.Count(s => s.Item.CreatedAt <= day && s.Item.Status == WorkItemStatus.Backlog && IsBlocked(s));
            var wip = states.Count(s => IsActive(s.Item.Status));
            // Seeded tie-breaking only: capacity is a strict upper bound, never randomized.
            var ordered = states.Select(s => (State: s, Tie: random.Next()))
                .OrderByDescending(x => x.State.Item.Priority).ThenBy(x => x.Tie).ThenBy(x => x.State.Item.Id)
                .Select(x => x.State).ToArray();
            foreach (var state in ordered)
            {
                if (wip >= team.WipLimit) break;
                if (state.Item.Status != WorkItemStatus.Backlog || state.Item.CreatedAt > day || IsBlocked(state)) continue;
                state.Item = state.Item with { Status = WorkItemStatus.Development, StartedAt = day };
                wip++;
            }
            var devRemaining = devCapacity;
            var testRemaining = testCapacity;
            foreach (var state in ordered)
            {
                switch (state.Item.Status)
                {
                    case WorkItemStatus.Development:
                        var devWork = Math.Min(devRemaining, state.DevelopmentRemaining);
                        devRemaining -= devWork;
                        state.DevelopmentRemaining -= devWork;
                        if (state.DevelopmentRemaining <= 0)
                            state.Item = state.Item with { Status = WorkItemStatus.CodeReview };
                        break;
                    case WorkItemStatus.CodeReview:
                        state.Item = state.Item with { Status = WorkItemStatus.Testing };
                        break;
                    case WorkItemStatus.Testing:
                        var testWork = Math.Min(testRemaining, state.TestingRemaining);
                        testRemaining -= testWork;
                        state.TestingRemaining -= testWork;
                        if (state.TestingRemaining <= 0)
                            state.Item = state.Item with { Status = WorkItemStatus.Done, CompletedAt = day + 1 };
                        break;
                }
            }
            days.Add(new DailySnapshot(day, wip, blocked, unfinished,
                devCapacity - devRemaining, testCapacity - testRemaining,
                states.ToDictionary(s => s.Item.Id, s => s.Item.Status)));
            if ((day + 1) % scenario.ReleaseInterval == 0)
            {
                var included = states.Where(s => s.Item.Status == WorkItemStatus.Done && released.Add(s.Item.Id))
                    .Select(s => s.Item).ToArray();
                releases.Add(new Release(day + 1, included));
            }
        }
        var completed = states.Where(s => s.Item.Status == WorkItemStatus.Done).Select(s => s.Item).ToArray();
        var sprints = new List<Sprint>();
        for (long start = 0; start < scenario.DurationDays; start += scenario.SprintLength)
            sprints.Add(new Sprint((int)start, (int)Math.Min(start + scenario.SprintLength, scenario.DurationDays)));
        var unfinishedDays = days.Sum(d => (double)d.UnfinishedItems);
        return new SimulationResult(scenario.RandomSeed, completed.Length,
            (double)completed.Length / scenario.DurationDays,
            completed.Length == 0 ? 0 : completed.Average(w => w.CompletedAt!.Value - w.CreatedAt),
            completed.Length == 0 ? 0 : completed.Average(w => w.CompletedAt!.Value - w.StartedAt!.Value),
            days.Average(d => d.Wip),
            unfinishedDays == 0 ? 0 : days.Sum(d => (double)d.BlockedItems) / unfinishedDays,
            devCapacity == 0 ? 0 : days.Average(d => d.DevelopmentWork / devCapacity),
            testCapacity == 0 ? 0 : days.Average(d => d.TestingWork / testCapacity),
            states.Select(s => s.Item).ToArray(), days, sprints, releases);
    }

    private static bool IsActive(WorkItemStatus status) => status is
        WorkItemStatus.Development or WorkItemStatus.CodeReview or WorkItemStatus.Testing;
}
