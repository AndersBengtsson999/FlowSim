namespace Simulation.Core;

/// <summary>Authoritative metric aggregation from observed work and states. No allocation rules.</summary>
internal static class SimulationResultBuilder
{
    private sealed class Times
    {
        public int Active, ReviewWaiting, TestingWaiting, Blocked, ReworkWaiting, ReworkActive;
        public double ReworkEffort;
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
            if (observation.DevelopmentWork > 0 || observation.CodeReviewWork > 0 || observation.TestingWork > 0 || observation.ReworkWork > 0) t.Active++;
            if (observation.ReworkWork > 0) t.ReworkActive++;
            t.ReworkEffort += observation.ReworkWork;
            if (observation.StateDuringDay == WorkItemStatus.WaitingForRework) t.ReworkWaiting++;
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
                w.RemainingTestingEffort, Array.AsReadOnly(w.Transitions.ToArray()), w.DevelopmentEffort, w.CodeReviewEffort, w.TestingEffort)
            {
                Events = Array.AsReadOnly(w.Events.ToArray()),
                InspectionAttempts = Array.AsReadOnly(w.InspectionAttempts.ToArray()),
                CodeReviewDefectsFound = w.Events.Count(e => e.EventType == WorkItemEventType.DefectFound && e.DefectSource == DefectSource.CodeReview),
                TestingDefectsFound = w.Events.Count(e => e.EventType == WorkItemEventType.DefectFound && e.DefectSource == DefectSource.Testing),
                ReworkCount = w.Transitions.Count(e => e.To == WorkItemStatus.Rework),
                RequiredReworkEffort = w.Events.Where(e => e.EventType == WorkItemEventType.DefectFound).Sum(e => e.RequiredReworkEffort ?? 0),
                RemainingReworkEffort = w.RemainingReworkEffort,
                TotalReworkEffort = t.ReworkEffort, ReworkActiveTime = t.ReworkActive, WaitingForReworkTime = t.ReworkWaiting,
                CodeReviewAttempts = w.InspectionAttempts.Count(a => a.Stage == DefectSource.CodeReview),
                TestingAttempts = w.InspectionAttempts.Count(a => a.Stage == DefectSource.Testing)
            };
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
            days.Average(d => d.ReviewWip), results.SelectMany(w => w.InspectionAttempts)
                .Where(a => a.Stage == DefectSource.CodeReview && a.CompletedDay.HasValue)
                .Select(a => (double)(a.CompletedDay!.Value - a.StartedDay)).DefaultIfEmpty(0).Average(),
            Array.AsReadOnly(results), Array.AsReadOnly(days.ToArray()), scenario.SimulationDays, items.Count,
            Mean(w => w.ActiveTime), Mean(w => w.WaitingTime), Mean(w => w.BlockedTime),
            days.Max(d => d.WaitingForCodeReviewCount), days.Max(d => d.WaitingForTestingCount)) {
                RandomSeed = scenario.RandomSeed,
                TotalDefectsFound = results.Sum(w => w.DefectsFound),
                CodeReviewDefectsFound = results.Sum(w => w.CodeReviewDefectsFound),
                TestingDefectsFound = results.Sum(w => w.TestingDefectsFound),
                WorkItemsWithDefects = results.Count(w => w.EverRequiredRework),
                TotalReworkCount = results.Sum(w => w.ReworkCount),
                TotalReworkEffort = results.Sum(w => w.TotalReworkEffort),
                AverageReworkEffortPerCompletedItem = Mean(w => w.TotalReworkEffort),
                ReworkDeveloperCapacityShare = Ratio(days.Sum(d => d.UsedReworkDeveloperCapacity), days.Sum(d => d.UsedDeveloperCapacity)),
                AverageCodeReviewAttempts = results.Where(w => w.CreatedDay < scenario.SimulationDays).Select(w => (double)w.CodeReviewAttempts).DefaultIfEmpty(0).Average(),
                AverageTestingAttempts = results.Where(w => w.CreatedDay < scenario.SimulationDays).Select(w => (double)w.TestingAttempts).DefaultIfEmpty(0).Average()
            };
    }
}
