using System.Globalization;
using Simulation.Application;
using Simulation.Core;
using Simulation.UI.ViewModels;

namespace Simulation.UI.Insights;

public enum InsightCategory { QueuePressure, CapacityImbalance }
public enum EvidenceStrength { InsufficientEvidence, ObservedCondition, ObservableTrend, TrendWithCapacityEvidence }
public sealed record InsightPeriod(int? FirstDay, int? LastDay, int Observations, int RequestedDays)
{
    public override string ToString() => Observations == 0 ? "No completed-day observations" : $"Days {FirstDay}–{LastDay} · {Observations}/{RequestedDays} observations";
}
public sealed record InsightMetric(string Name, double? Value, string Unit)
{
    public override string ToString() => $"{Name}: {(Value.HasValue ? Value.Value.ToString("0.##", CultureInfo.InvariantCulture) : "N/A")} {Unit}";
}
public sealed record SimulationInsight(string Id, InsightCategory Category, string Title, string Observation,
    string Evidence, InsightPeriod Period, string Explanation, EvidenceStrength Strength, string Experiment,
    string[] Stations, TeamKpi[] Kpis, string Topic, bool Relevant, string MaterialState,
    IReadOnlyList<InsightMetric> Metrics);
public sealed record InsightReport(int Day, InsightPeriod CapacityPeriod, IReadOnlyList<SimulationInsight> Insights);

/// <summary>Read-only presentation analysis. Reuses station pressure unchanged and recorded raw capacity.
/// No engine operations, random draws, intervention execution or alternative KPI calculations.</summary>
public static class SimulationInsights
{
    public const string Topic = "experiments.simulation-insights";
    public const int CapacityWindow = 20;
    public static InsightReport Analyze(LiveSimulation live)
    {
        var session = live.Session;
        var change = session.Changes.LastOrDefault()?.Day ?? -1;
        var days = session.Days.TakeLast(CapacityWindow).Where(d => d.Day >= change).ToArray();
        var queueDays = days.TakeLast(4).ToArray();
        var period = Period(days, CapacityWindow);
        var pressures = QueuePressure.From(live);
        var results = new List<SimulationInsight>();
        var stages = new[] {
            (Key:"backlog", Name:"Development", Station:"Development", Kpi:TeamKpi.DevelopmentQueue),
            (Key:"review-wait", Name:"Code Review", Station:"Code Review", Kpi:TeamKpi.ReviewQueue),
            (Key:"testing-wait", Name:"Testing", Station:"Testing", Kpi:TeamKpi.TestingQueue) };
        foreach (var stage in stages)
        {
            var p = pressures[stage.Key];
            var relevant = p.Kind is QueuePressureKind.Growing or QueuePressureKind.Constrained or QueuePressureKind.Blocked;
            var strength = p.Kind == QueuePressureKind.Constrained ? EvidenceStrength.TrendWithCapacityEvidence
                : p.Kind == QueuePressureKind.Blocked ? EvidenceStrength.ObservedCondition
                : p.Kind is QueuePressureKind.Growing or QueuePressureKind.Shrinking ? EvidenceStrength.ObservableTrend
                : queueDays.Length < 4 ? EvidenceStrength.InsufficientEvidence : EvidenceStrength.ObservedCondition;
            var count = queueDays.Length == 0 ? (double?)null : Queue(queueDays[^1], stage.Key);
            var stable = queueDays.Length == 4 && queueDays.Select(d => Queue(d,stage.Key)).Distinct().Count()==1;
            var condition = p.Kind == QueuePressureKind.Normal && queueDays.Length < 4 ? "Insufficient observation history"
                : p.Kind == QueuePressureKind.Normal && stable ? "Stable queue" : p.Condition;
            var observation = $"{stage.Name}: {condition}.";
            var explanation = stage.Key == "backlog"
                ? "Development pressure uses eligible Backlog (excluding unresolved dependencies). Station Inspection deliberately does not classify this queue as a capacity bottleneck. Arrival rate, WIP limits and shared developer work may affect it."
                : stage.Key == "review-wait"
                ? "Code Review shares developer capacity with Development, Rework and debt repayment. There is no independent reviewer staffing pool."
                : "Available tester capacity, work supply, WIP limits and rework may affect this queue.";
            results.Add(new($"queue.{stage.Key}", InsightCategory.QueuePressure, observation, observation,
                p.Evidence + " " + p.Period, Period(queueDays,4), explanation + " The evidence does not establish the underlying cause.", strength,
                Experiment(stage.Key), [stage.Station], [stage.Kpi, stage.Key=="testing-wait"?TeamKpi.TesterUtilization:TeamKpi.DeveloperUtilization],
                Topic, relevant, p.Kind.ToString(), [new("Waiting items (eligible Backlog for Development)",count,"items"),
                    new("Queue change since preceding observation",queueDays.Length<2?null:Queue(queueDays[^1],stage.Key)-Queue(queueDays[^2],stage.Key),"items"),
                    new("Observed pool available (queue period)",queueDays.Length==0?null:queueDays.Sum(d=>stage.Key=="testing-wait"?d.AvailableTesterCapacity:d.AvailableDeveloperCapacity),"raw units"),
                    new("Observed pool used (queue period)",queueDays.Length==0?null:queueDays.Sum(d=>stage.Key=="testing-wait"?d.UsedTesterCapacity:d.UsedDeveloperCapacity),"raw units")]));
        }
        // Flow counts come from recorded transitions, not differences between unlike effort units.
        if(days.Length >= 4)
        {
            var first = days[0]; var last = days[^1];
            var transitions = session.WorkItems.SelectMany(w=>w.Transitions).Where(t=>t.Day>=first.Day+1 && t.Day<=last.Day+1).ToArray();
            var developmentOutput = transitions.Count(t=>t.From==WorkItemStatus.Development && t.To==WorkItemStatus.WaitingForCodeReview);
            var reviewOutput = transitions.Count(t=>t.From==WorkItemStatus.CodeReview && t.To==WorkItemStatus.WaitingForTesting);
            var testingOutput = transitions.Count(t=>t.From==WorkItemStatus.Testing && t.To==WorkItemStatus.ReadyForRelease);
            var released = transitions.Count(t=>t.To is WorkItemStatus.Released or WorkItemStatus.Done);
            var metrics = new List<InsightMetric> {
                new("Development exits to review",developmentOutput,"transitions"), new("Review exits to testing",reviewOutput,"transitions"),
                new("Testing exits to release queue",testingOutput,"transitions"), new("Internal releases",released,"items"),
                new("Developer capacity available",days.Sum(d=>d.AvailableDeveloperCapacity),"raw units"),
                new("Developer capacity used (all shared work)",days.Sum(d=>d.UsedDeveloperCapacity),"raw units"),
                new("Development capacity used",days.Sum(d=>d.UsedDevelopmentCapacity),"raw units"),
                new("Review capacity used (shared developers)",days.Sum(d=>d.UsedReviewCapacity),"raw units"),
                new("Tester capacity available",days.Sum(d=>d.AvailableTesterCapacity),"raw units"),
                new("Tester capacity used",days.Sum(d=>d.UsedTesterCapacity),"raw units") };
            // No arbitrary "low utilization" threshold: only exact observed unused capacity and
            // absence of a stage queue/active work on the same completed days are described.
            var idleTesting = days.Where(d=>d.AvailableTesterCapacity>0 && d.UsedTesterCapacity<d.AvailableTesterCapacity-1e-8
                && d.TestingCount==0 && d.WaitingForTestingCount==0).ToArray();
            if(idleTesting.Length>0)
                results.Add(new("imbalance.testing-supply",InsightCategory.CapacityImbalance,"Unused Testing capacity with no end-of-day testing work",
                    $"{idleTesting.Length} of {days.Length} observed days had unused tester capacity and no active or waiting Testing items at day end.",
                    "End-of-day occupancy does not establish whether work was available earlier that day. This is not proof of a staffing excess. " +
                    $"Days with unresolved dependency waiting: {days.Count(d=>d.WaitingForDependencyCount>0)}; days at the configured Testing WIP limit after admission: {days.Count(d=>d.TestingWip>=session.Configuration.TestingWipLimit)}.",
                    period,"Upstream supply, admission timing, dependencies or WIP constraints may contribute. The data does not isolate which explanation applies.",
                    EvidenceStrength.ObservedCondition,"Inspect upstream stations and use the existing scenario comparison workflow to test one WIP or supply setting at a time. Do not assume reducing testers will improve delivery.",
                    ["Development","Code Review","Testing"],[TeamKpi.TesterUtilization,TeamKpi.TestingQueue,TeamKpi.ReleasedThroughput],Topic,true,"unused-without-work",metrics));
            foreach(var key in new[]{"review-wait","testing-wait"})
            {
                var delta = Queue(last,key)-Queue(first,key);
                var upstream = key=="review-wait"?developmentOutput:reviewOutput;
                if(delta<=0 || upstream<=0) continue;
                var p=pressures[key]; var testing=key=="testing-wait";
                var title=testing?"Review output accompanies Testing queue growth":"Development output accompanies Code Review queue growth";
                results.Add(new($"imbalance.{key}",InsightCategory.CapacityImbalance,title,
                    $"The waiting queue increased by {delta} items between the first and last snapshots; {upstream} upstream exits were recorded during the period.",
                    $"Endpoint growth is not necessarily continuous growth. Current station classification: {p.Condition}. {p.Period}. Flow counts include repeat review/testing attempts; effort and raw capacity are not interchangeable. Internal release is not customer launch.",
                    period, testing?"Downstream capacity pressure may contribute, but work size, WIP constraints, rework and arrival timing can also contribute. Correlation does not establish cause.":"Review consumes the same developer pool as Development, Rework and debt repayment. More developers do not imply a separate increase in review staffing. The observations do not establish a cause.",
                    p.Kind==QueuePressureKind.Constrained?EvidenceStrength.TrendWithCapacityEvidence:EvidenceStrength.ObservableTrend,
                    Experiment(key),testing?["Code Review","Testing"]:["Development","Code Review"],
                    testing?[TeamKpi.TestingQueue,TeamKpi.TesterUtilization,TeamKpi.ReleasedThroughput]:[TeamKpi.ReviewQueue,TeamKpi.DeveloperUtilization,TeamKpi.ReleasedThroughput],
                    Topic,true,p.Kind.ToString(),metrics.Concat(new[]{new InsightMetric("Queue change (last minus first)",delta,"items")}).ToArray()));
            }
        }
        return new(session.CurrentDay,period,results);
    }
    private static int Queue(DailySnapshot d,string key)=>key switch { "backlog"=>Math.Max(0,d.BacklogCount-d.WaitingForDependencyCount),"review-wait"=>d.WaitingForCodeReviewCount,_=>d.WaitingForTestingCount };
    private static InsightPeriod Period(DailySnapshot[] days,int requested)=>new(days.Length==0?null:days[0].Day+1,days.Length==0?null:days[^1].Day+1,days.Length,requested);
    private static string Experiment(string key)=>key switch {
        "testing-wait"=>"Use the existing guided comparison to compare a baseline with one additional tester. Keep other settings fixed and inspect queue, utilization and released throughput. This tests a hypothesis; improvement is not guaranteed.",
        "review-wait"=>"Use the existing scenario comparison workflow to investigate Code Review WIP or productivity, one setting at a time. Inspect the shared developer pool and downstream throughput. This tests a hypothesis; improvement is not guaranteed.",
        _=>"Use the existing scenario comparison workflow to compare Development WIP limits, one setting at a time. Inspect downstream queues and released throughput. This tests a hypothesis; improvement is not guaranteed." };
}
