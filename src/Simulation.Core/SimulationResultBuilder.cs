namespace Simulation.Core;

/// <summary>Authoritative metric aggregation from observed work and states. No allocation rules.</summary>
internal static class SimulationResultBuilder
{
    private sealed class Times
    {
        public int Active, ReviewWaiting, TestingWaiting, Blocked;
    }

    internal static SimulationResult Build(SimulationScenario scenario, IReadOnlyList<WorkItem> items,
        IReadOnlyList<DailySnapshot> days)
    {
        var times = items.ToDictionary(w => w.Id, _ => new Times(), StringComparer.Ordinal);
        foreach (var day in days)
        foreach (var observation in day.Items)
        {
            if (observation.CreatedDay > day.Day) continue;
            var t = times[observation.Id];
            if (observation.DevelopmentWork > 0 || observation.CodeReviewWork > 0 || observation.TestingWork > 0) t.Active++;
            if (observation.StateDuringDay == WorkItemStatus.WaitingForCodeReview) t.ReviewWaiting++;
            if (observation.StateDuringDay == WorkItemStatus.WaitingForTesting) t.TestingWaiting++;
            if (observation.DependencyBlocked) t.Blocked++;
        }
        var results = items.Select(w =>
        {
            var t = times[w.Id];
            return new WorkItemResult(w.Id, w.Name, w.State, w.CreatedDay, w.DevelopmentStartedDay,
                w.DevelopmentCompletedDay, w.CodeReviewStartedDay, w.CodeReviewCompletedDay,
                w.TestingStartedDay, w.TestingCompletedDay, w.DoneDay, t.Active, t.ReviewWaiting,
                t.TestingWaiting, t.Blocked, w.RemainingDevelopmentEffort, w.RemainingCodeReviewEffort,
                w.RemainingTestingEffort, Array.AsReadOnly(w.Transitions.ToArray()));
        }).ToArray();
        var completed = results.Where(w => w.FinalState == WorkItemStatus.Done).ToArray();
        double Mean(Func<WorkItemResult, double> metric) => completed.Select(metric).DefaultIfEmpty(0).Average();
        static double Ratio(double used, double available) => available == 0 ? 0 : used / available;
        var developers = days.Sum(d => d.AvailableDeveloperCapacity);
        var testers = days.Sum(d => d.AvailableTesterCapacity);
        return new SimulationResult(scenario.Name, completed.Length, (double)completed.Length / scenario.SimulationDays,
            Mean(w => w.LeadTime!.Value), Mean(w => w.CycleTime!.Value), days.Average(d => d.TotalWip),
            Ratio(days.Sum(d => (double)d.BlockedItems), days.Sum(d => (double)d.UnfinishedItems)),
            Ratio(days.Sum(d => d.UsedDeveloperCapacity), developers), Ratio(days.Sum(d => d.UsedTesterCapacity), testers),
            Ratio(days.Sum(d => d.ReviewWork), developers), Ratio(days.Sum(d => d.DevelopmentWork), developers),
            days.Average(d => d.ReviewWip), results.Where(w => w.CodeReviewCompletedDay.HasValue)
                .Select(w => (double)(w.CodeReviewCompletedDay!.Value - w.CodeReviewStartedDay!.Value)).DefaultIfEmpty(0).Average(),
            Array.AsReadOnly(results), Array.AsReadOnly(days.ToArray()), scenario.SimulationDays, items.Count,
            Mean(w => w.ActiveTime), Mean(w => w.WaitingTime), Mean(w => w.BlockedTime),
            days.Max(d => d.WaitingForCodeReviewCount), days.Max(d => d.WaitingForTestingCount));
    }
}
