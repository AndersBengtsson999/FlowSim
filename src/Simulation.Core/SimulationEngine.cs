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
        IReadOnlyDictionary<string, WorkItem> byId, DefectPolicy defects, int day, TechnicalDebtLedger debt, IRandomSource random)
    {
        debt.BeginDay();
        var team = scenario.Team;
        var productivity = scenario.Productivity;
        var explicitCapacity = productivity != StageProductivity.Default;
        bool Blocked(WorkItem item) => item.Dependencies.Any(id => byId[id].State != WorkItemStatus.Done);
        IEnumerable<WorkItem> Fifo(WorkItemStatus state, WorkItemStatus stage) => items
            .Where(w => w.State == state).OrderBy(w => w.QueueEnteredDay(stage));
        void Admit(WorkItemStatus waiting, WorkItemStatus active, int currentDay)
        {
            foreach (var item in Fifo(waiting, active))
            {
                if (item.CreatedDay > currentDay || (waiting == WorkItemStatus.Backlog && Blocked(item))) continue;
                if (!WipPolicy.CanAdmit(items, active, scenario)) break;
                if (active == WorkItemStatus.Development) item.SetDevelopmentPlan(debt.Plan(item.DevelopmentEffort, scenario.Debt, random));
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
        var devRemaining = team.AvailableDeveloperCapacity;
        var testRemaining = team.AvailableTesterCapacity;
        var work = new Dictionary<string, double>(StringComparer.Ordinal);
        var workedStage = new Dictionary<string, WorkItemStatus>(StringComparer.Ordinal);

        var capacity = new Dictionary<string, double>(StringComparer.Ordinal);
        // Clamp effective work to remaining effort to avoid floating-point overshoot on completion.
        static (double Consumed, double Effective) Contribution(double remaining, double limit, double factor)
        {
            if (factor == 0) return (0, 0); // Underflow of collaboration efficiency at subnormal productivity.
            var consumed = Math.Min(remaining / factor, limit);
            return (consumed, Math.Min(remaining, consumed * factor));
        }
        double Allocate(WorkItemStatus stage, double perPersonCapacity, double factor, ref double pool)
        {
            double total = 0;
            foreach (var item in Fifo(stage, stage))
            {
                // One person at a time; the existing 1 unit/item/day limit caps capacity, not effective work.
                var (consumed, amount) = Contribution(item.RemainingEffort, Math.Min(pool, Math.Min(1, perPersonCapacity)), factor);
                item.ApplyWork(amount, day, factor == 1 ? null : consumed);
                pool = Math.Max(0, pool - consumed);
                capacity[item.Id] = consumed;
                total += amount;
                work[item.Id] = amount;
                workedStage[item.Id] = stage;
                if (item.RemainingEffort == 0) item.CompleteStage(day + 1, defects);
            }
            return total;
        }

        // The global priority is unchanged. Collaboration belongs only to Development.
        var reviewWork = Allocate(WorkItemStatus.CodeReview, team.DeveloperCapacityPerDay, productivity.CodeReview, ref devRemaining);
        var reworkWork = Allocate(WorkItemStatus.Rework, team.DeveloperCapacityPerDay, 1, ref devRemaining);
        var repayment = debt.Repay(devRemaining, scenario.Debt, productivity.Development);
        devRemaining = Math.Max(0, devRemaining - repayment.Capacity);
        void CompleteDevelopment(WorkItem item)
        {
            debt.Complete(item);
            item.CompleteStage(day + 1, defects);
        }
        var developmentItems = Fifo(WorkItemStatus.Development, WorkItemStatus.Development).ToArray();
        var collaboration = new Dictionary<string, double>(StringComparer.Ordinal);
        var contributionLimit = Math.Min(1, team.DeveloperCapacityPerDay);
        double developmentWork = 0, collaborationCapacity = 0;
        // Cover every admitted item in FIFO order before a second contribution is considered.
        foreach (var item in developmentItems)
        {
            var (consumed, primary) = Contribution(item.RemainingDevelopmentEffort, Math.Min(devRemaining, contributionLimit), productivity.Development);
            item.ApplyWork(primary, day, productivity.Development == 1 ? null : consumed);
            capacity[item.Id] = consumed;
            devRemaining = Math.Max(0, devRemaining - consumed);
            developmentWork += primary;
            work[item.Id] = primary; workedStage[item.Id] = WorkItemStatus.Development;
            if (item.RemainingDevelopmentEffort == 0) CompleteDevelopment(item);
        }
        // Stable OrderBy preserves FIFO order for equal remaining effort after primary work.
        foreach (var item in developmentItems.Where(w => w.State == WorkItemStatus.Development)
                     .OrderBy(w => w.RemainingDevelopmentEffort))
        {
            var (consumed, effective) = Contribution(item.RemainingDevelopmentEffort, Math.Min(devRemaining, team.DeveloperCount >= 2 ? contributionLimit : 0), 0.5 * productivity.Development);
            capacity[item.Id] += consumed;
            item.ApplyWork(effective, day, consumed);
            devRemaining = Math.Max(0, devRemaining - consumed);
            collaboration[item.Id] = consumed;
            collaborationCapacity += consumed;
            developmentWork += effective; work[item.Id] += effective;
            if (item.RemainingDevelopmentEffort == 0) CompleteDevelopment(item);
        }
        var testingWork = Allocate(WorkItemStatus.Testing, team.TesterCapacityPerDay, productivity.Testing, ref testRemaining);
        double Used(WorkItem item, WorkItemStatus stage) =>
            workedStage.TryGetValue(item.Id, out var actual) && actual == stage ? work[item.Id] : 0;
        double Consumed(WorkItem item, WorkItemStatus stage) =>
            workedStage.TryGetValue(item.Id, out var actual) && actual == stage ? capacity[item.Id] : 0;
        var snapshots = items.Select(w => new WorkItemDaySnapshot(w.Id, w.State,
            w.RemainingDevelopmentEffort, w.RemainingCodeReviewEffort, w.RemainingTestingEffort,
            Used(w, WorkItemStatus.Development), Used(w, WorkItemStatus.CodeReview), Used(w, WorkItemStatus.Testing),
            w.CreatedDay, statesDuringDay[w.Id], blockedIds.Contains(w.Id), Used(w, WorkItemStatus.Rework), w.RemainingReworkEffort, collaboration.GetValueOrDefault(w.Id))
            { DeliveryCost = w.DeliveryCost, DevelopmentPlan = w.DevelopmentPlan, ConsumedCapacity = explicitCapacity ? new(Consumed(w, WorkItemStatus.Development), Consumed(w, WorkItemStatus.CodeReview), Consumed(w, WorkItemStatus.Testing)) : null }).ToArray();
        return new DailySnapshot(day, devWip, reviewWip, testWip, blocked, unfinished,
            developmentWork, reviewWork, testingWork, Array.AsReadOnly(snapshots), team.AvailableDeveloperCapacity, team.AvailableTesterCapacity, reworkWork, reworkWip, collaborationCapacity)
        { Debt = new(debt.State, scenario.Debt.Tolerance, debt.State.Overhead(scenario.Debt), debt.CreatedToday, repayment.Work, repayment.Capacity),
          ConsumedCapacity = explicitCapacity ? new(items.Sum(w => Consumed(w, WorkItemStatus.Development)),
            items.Sum(w => Consumed(w, WorkItemStatus.CodeReview)), items.Sum(w => Consumed(w, WorkItemStatus.Testing))) : null };
    }
}
