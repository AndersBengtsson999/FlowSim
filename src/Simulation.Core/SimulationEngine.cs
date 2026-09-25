namespace Simulation.Core;

public sealed class SimulationEngine
{
    public SimulationResult Run(SimulationScenario scenario, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ScenarioValidator.Validate(scenario);
        var items = scenario.WorkItems.Select(w => w.CopyForRun()).ToArray();
        var byId = items.ToDictionary(w => w.Id, StringComparer.Ordinal);
        var days = new List<DailySnapshot>();
        var team = scenario.Team;

        bool Blocked(WorkItem item) => item.Dependencies.Any(id => byId[id].State != WorkItemStatus.Done);
        // LINQ's stable ordering preserves scenario order for simultaneous queue entries.
        IEnumerable<WorkItem> Fifo(WorkItemStatus state, WorkItemStatus stage) => items
            .Where(w => w.State == state).OrderBy(w => w.QueueEnteredDay(stage));

        void Admit(WorkItemStatus waiting, WorkItemStatus active, int day)
        {
            foreach (var item in Fifo(waiting, active))
            {
                if (item.CreatedDay > day || (waiting == WorkItemStatus.Backlog && Blocked(item))) continue;
                if (!WipPolicy.CanAdmit(items, active, scenario)) break;
                item.Enter(active, day);
            }
        }

        for (var day = 0; day < scenario.SimulationDays; day++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var unfinished = items.Count(w => w.CreatedDay <= day && w.State != WorkItemStatus.Done);
            var blocked = items.Count(w => w.CreatedDay <= day && w.State == WorkItemStatus.Backlog && Blocked(w));
            // All admissions precede all work. New completions wait until tomorrow's admission.
            Admit(WorkItemStatus.Backlog, WorkItemStatus.Development, day);
            Admit(WorkItemStatus.WaitingForCodeReview, WorkItemStatus.CodeReview, day);
            Admit(WorkItemStatus.WaitingForTesting, WorkItemStatus.Testing, day);
            var statesDuringDay = items.ToDictionary(w => w.Id, w => w.State, StringComparer.Ordinal);
            var blockedIds = items.Where(w => w.CreatedDay <= day && w.State == WorkItemStatus.Backlog && Blocked(w))
                .Select(w => w.Id).ToHashSet(StringComparer.Ordinal);
            var devWip = WipPolicy.Count(items, WorkItemStatus.Development);
            var reviewWip = WipPolicy.Count(items, WorkItemStatus.CodeReview);
            var testWip = WipPolicy.Count(items, WorkItemStatus.Testing);
            var devRemaining = team.TotalDeveloperCapacity;
            var testRemaining = team.TotalTesterCapacity;
            var work = new Dictionary<string, double>(StringComparer.Ordinal);
            var workedStage = new Dictionary<string, WorkItemStatus>(StringComparer.Ordinal);

            double Allocate(WorkItemStatus stage, WorkItemStatus next, double perPersonCapacity, ref double pool)
            {
                double total = 0;
                foreach (var item in Fifo(stage, stage))
                {
                    // One person at a time, and a hard upper bound of 1 unit/item/day.
                    var amount = Math.Min(item.RemainingEffort, Math.Min(pool, Math.Min(1, perPersonCapacity)));
                    item.ApplyWork(amount);
                    pool = Math.Max(0, pool - amount);
                    total += amount;
                    work[item.Id] = amount;
                    workedStage[item.Id] = stage;
                    if (item.RemainingEffort == 0) item.Enter(next, day + 1);
                }
                return total;
            }

            var reviewWork = Allocate(WorkItemStatus.CodeReview, WorkItemStatus.WaitingForTesting,
                team.DeveloperCapacityPerDay, ref devRemaining);
            var developmentWork = Allocate(WorkItemStatus.Development, WorkItemStatus.WaitingForCodeReview,
                team.DeveloperCapacityPerDay, ref devRemaining);
            var testingWork = Allocate(WorkItemStatus.Testing, WorkItemStatus.Done,
                team.TesterCapacityPerDay, ref testRemaining);
            double Used(WorkItem item, WorkItemStatus stage) =>
                workedStage.TryGetValue(item.Id, out var actual) && actual == stage ? work[item.Id] : 0;
            var snapshots = items.Select(w => new WorkItemDaySnapshot(w.Id, w.State,
                w.RemainingDevelopmentEffort, w.RemainingCodeReviewEffort, w.RemainingTestingEffort,
                Used(w, WorkItemStatus.Development), Used(w, WorkItemStatus.CodeReview), Used(w, WorkItemStatus.Testing),
                w.CreatedDay, statesDuringDay[w.Id], blockedIds.Contains(w.Id))).ToArray();
            days.Add(new DailySnapshot(day, devWip, reviewWip, testWip, blocked, unfinished,
                developmentWork, reviewWork, testingWork, Array.AsReadOnly(snapshots), team.TotalDeveloperCapacity, team.TotalTesterCapacity));
        }
        return SimulationResultBuilder.Build(scenario, items, days);
    }
}
