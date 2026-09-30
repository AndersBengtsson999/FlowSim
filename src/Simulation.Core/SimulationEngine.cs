namespace Simulation.Core;

public sealed class SimulationEngine
{
    public SimulationResult Run(SimulationScenario scenario, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var session = new SimulationSession(scenario);
        for (var day = 0; day < scenario.SimulationDays; day++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            session.AdvanceOneDay();
        }
        return session.GetResult();
    }

    internal static DailySnapshot AdvanceOneDay(SimulationScenario scenario, IReadOnlyList<WorkItem> items,
        IReadOnlyDictionary<string, WorkItem> byId, DefectPolicy defects, int day)
    {
        var team = scenario.Team;
        bool Blocked(WorkItem item) => item.Dependencies.Any(id => byId[id].State != WorkItemStatus.Done);
        IEnumerable<WorkItem> Fifo(WorkItemStatus state, WorkItemStatus stage) => items
            .Where(w => w.State == state).OrderBy(w => w.QueueEnteredDay(stage));
        void Admit(WorkItemStatus waiting, WorkItemStatus active, int currentDay)
        {
            foreach (var item in Fifo(waiting, active))
            {
                if (item.CreatedDay > currentDay || (waiting == WorkItemStatus.Backlog && Blocked(item))) continue;
                if (!WipPolicy.CanAdmit(items, active, scenario)) break;
                item.Enter(active, currentDay);
            }
        }
        var unfinished = items.Count(w => w.CreatedDay <= day && w.State != WorkItemStatus.Done);
        var blocked = items.Count(w => w.CreatedDay <= day && w.State == WorkItemStatus.Backlog && Blocked(w));
        // All admissions precede all work. New completions wait until tomorrow's admission.
        Admit(WorkItemStatus.Backlog, WorkItemStatus.Development, day);
        Admit(WorkItemStatus.WaitingForCodeReview, WorkItemStatus.CodeReview, day);
        Admit(WorkItemStatus.WaitingForTesting, WorkItemStatus.Testing, day);
        Admit(WorkItemStatus.WaitingForRework, WorkItemStatus.Rework, day);
        var reworkWip = WipPolicy.Count(items, WorkItemStatus.Rework);
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

        double Allocate(WorkItemStatus stage, double perPersonCapacity, ref double pool)
        {
            double total = 0;
            foreach (var item in Fifo(stage, stage))
            {
                // One person at a time, and a hard upper bound of 1 unit/item/day.
                var amount = Math.Min(item.RemainingEffort, Math.Min(pool, Math.Min(1, perPersonCapacity)));
                item.ApplyWork(amount, day);
                pool = Math.Max(0, pool - amount);
                total += amount;
                work[item.Id] = amount;
                workedStage[item.Id] = stage;
                if (item.RemainingEffort == 0) item.CompleteStage(day + 1, defects);
            }
            return total;
        }

        // The global priority is unchanged. Collaboration belongs only to Development.
        var reviewWork = Allocate(WorkItemStatus.CodeReview, team.DeveloperCapacityPerDay, ref devRemaining);
        var reworkWork = Allocate(WorkItemStatus.Rework, team.DeveloperCapacityPerDay, ref devRemaining);
        var developmentItems = Fifo(WorkItemStatus.Development, WorkItemStatus.Development).ToArray();
        var collaboration = new Dictionary<string, double>(StringComparer.Ordinal);
        var contributionLimit = Math.Min(1, team.DeveloperCapacityPerDay);
        double developmentWork = 0, collaborationCapacity = 0;
        // Cover every admitted item in FIFO order before a second contribution is considered.
        foreach (var item in developmentItems)
        {
            var primary = Math.Min(item.RemainingDevelopmentEffort, Math.Min(devRemaining, contributionLimit));
            item.ApplyWork(primary, day);
            devRemaining = Math.Max(0, devRemaining - primary);
            developmentWork += primary;
            work[item.Id] = primary; workedStage[item.Id] = WorkItemStatus.Development;
            if (item.RemainingDevelopmentEffort == 0) item.CompleteStage(day + 1, defects);
        }
        // Stable OrderBy preserves FIFO order for equal remaining effort after primary work.
        foreach (var item in developmentItems.Where(w => w.State == WorkItemStatus.Development)
                     .OrderBy(w => w.RemainingDevelopmentEffort))
        {
            var consumed = Math.Min(item.RemainingDevelopmentEffort / 0.5, Math.Min(devRemaining, team.DeveloperCount >= 2 ? contributionLimit : 0));
            var effective = consumed * 0.5;
            item.ApplyWork(effective, day, consumed);
            devRemaining = Math.Max(0, devRemaining - consumed);
            collaboration[item.Id] = consumed;
            collaborationCapacity += consumed;
            developmentWork += effective; work[item.Id] += effective;
            if (item.RemainingDevelopmentEffort == 0) item.CompleteStage(day + 1, defects);
        }
        var testingWork = Allocate(WorkItemStatus.Testing, team.TesterCapacityPerDay, ref testRemaining);
        double Used(WorkItem item, WorkItemStatus stage) =>
            workedStage.TryGetValue(item.Id, out var actual) && actual == stage ? work[item.Id] : 0;
        var snapshots = items.Select(w => new WorkItemDaySnapshot(w.Id, w.State,
            w.RemainingDevelopmentEffort, w.RemainingCodeReviewEffort, w.RemainingTestingEffort,
            Used(w, WorkItemStatus.Development), Used(w, WorkItemStatus.CodeReview), Used(w, WorkItemStatus.Testing),
            w.CreatedDay, statesDuringDay[w.Id], blockedIds.Contains(w.Id), Used(w, WorkItemStatus.Rework), w.RemainingReworkEffort, collaboration.GetValueOrDefault(w.Id))).ToArray();
        return new DailySnapshot(day, devWip, reviewWip, testWip, blocked, unfinished,
            developmentWork, reviewWork, testingWork, Array.AsReadOnly(snapshots), team.TotalDeveloperCapacity, team.TotalTesterCapacity, reworkWork, reworkWip, collaborationCapacity);
    }
}
