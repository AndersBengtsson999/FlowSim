namespace Simulation.UI.ViewModels;

public sealed record StationMetric(string Label, string Value);
public sealed record StationDetails(string Title, IReadOnlyList<StationMetric> Rows, string Explanation)
{
    public IReadOnlyList<InspectedWorkItem> Items { get; init; } = [];
    public string CompactSummary { get; init; } = "";
    public string Help => string.Join("\n", Rows.Select(r => $"{r.Label}: {r.Value}")) + "\n\n" + Explanation + "\nActive WIP means admitted work, not proof of processing today. Queue waiting excludes backlog and active-state time.";
    public string SnapshotSummary { get; init; } = "";
    public string ConditionColor { get; init; } = Simulation.UI.Theming.FlowPalette.Secondary;
    public string Condition { get; init; } = "";
    public string EvidencePeriod { get; init; } = "";
    public string Observation { get; init; } = "";
    public string ItemSummary { get; init; } = "";
    public static readonly StationDetails Empty = new("", [], "");
    public static StationDetails For(string? station, GameSceneState? s)
    {
        if (station is null || s is not { HasSession: true }) return Empty;
        var selection = station;
        station = station switch { "Dependency queue" => "Backlog", "Review queue" => "Code Review", "Testing queue" => "Testing", "Rework queue" => "Development", _ => station };
        var rows = new List<StationMetric>();
        void Add(string label, object value) => rows.Add(new(label, value.ToString() ?? ""));
        Add("Observed through", $"Day {s.Day}");
        Add("Purpose", station switch
        {
            "Backlog" => "Work awaiting development; dependency waits are included",
            "Development" => "Development and rework share the developer budget",
            "Code Review" => "Review developed work before testing",
            "Testing" => "Test reviewed work before release",
            "Ready for Release" => "Completed work awaiting release policy",
            "Release" => "Internally released work, cumulative",
            _ => "Repay aggregate technical debt using shared developer capacity"
        });
        string Number(double value) => value.ToString("0.##");
        void Pressure(string id)
        {
            var p = s.QueuePressures.GetValueOrDefault(id, QueuePressure.Normal);
            Add("Queue change · last day", p.Trend);
            if (p.Label.Length > 0) Add("Observed condition", p.Label);
        }
        void SharedDevelopers()
        {
            Add("Developers · configured", s.Developers); Add("Specialists · included", s.Specialists);
            if (s.Day > 0) Add("Shared Dev used / available · last day", $"{Number(s.UsedDeveloperCapacity)} / {Number(s.AvailableDeveloperCapacity)}");
        }
        var explanation = "Open-corner pads hold waiting work; solid mint pads show active work; gold staging holds completed work awaiting release. Counts are exact. Stacks are bounded samples; grouped packages do not represent individual items. Worker poses and trips are illustrative.";
        switch (station)
        {
            case "Backlog":
                Add("Waiting · total", s.Areas[0].Waiting); Add("Dependency waiting · included", s.DependencyWaiting);
                Add("Other backlog", Math.Max(0, s.Areas[0].Waiting - s.DependencyWaiting)); Pressure("backlog"); explanation += " Board cards illustrate planning; the waiting count is the actual backlog."; break;
            case "Development":
                Add("Active development / WIP limit", $"{s.ActiveDevelopment} / {s.DevelopmentWipLimit}");
                Add("Rework · active / waiting", $"{s.ActiveRework} / {s.WaitingRework}");
                SharedDevelopers();
                if (s.Day > 0) { Add("Development capacity used · last day", Number(s.UsedDevelopmentCapacity)); Add("Rework capacity used · last day", Number(s.UsedReworkCapacity)); Add("Collaboration capacity · last day", Number(s.CollaborationCapacity)); }
                Pressure("rework-wait"); explanation += " Development, review, rework and debt repayment share developer capacity. The queue change shown here refers to waiting rework. Dashed mauve returns appear when rework is observed; they are not part of every item’s path."; break;
            case "Code Review":
                Add("Waiting", s.Areas[2].Waiting); Add("Active WIP / limit", $"{s.Areas[2].Active} / {s.ReviewWipLimit}");
                if (s.Day > 0) Add("Review capacity used · last day", Number(s.UsedReviewCapacity)); SharedDevelopers(); Pressure("review-wait");
                explanation += " The checklist illustration identifies review, not an individual approval result. There is no independent review capacity allocation. A shared-capacity warning is an observation, not proof of a unique root bottleneck."; break;
            case "Testing":
                Add("Waiting", s.Areas[3].Waiting); Add("Active WIP / limit", $"{s.Areas[3].Active} / {s.TestingWipLimit}"); Add("Testers · configured", s.Testers);
                if (s.Day > 0) Add("Tester used / available · last day", $"{Number(s.UsedTesterCapacity)} / {Number(s.AvailableTesterCapacity)}");
                Pressure("testing-wait"); explanation += " Active WIP counts admitted work, even when no testers are available to process it."; break;
            case "Ready for Release":
                Add("Waiting for release", s.Areas[4].Waiting); Add("Release policy", s.ReleasePolicy); Add("Released today", s.ReleasedToday); Pressure("ready");
                explanation += " Scheduled accumulation can be expected batching; its size alone is not a capacity diagnosis."; break;
            case "Release":
                Add("Internally released · cumulative", s.Areas[5].Count); Add("Released today", s.ReleasedToday); Add("Ready for release", s.Areas[4].Waiting); Add("Release policy", s.ReleasePolicy); Pressure("ready");
                explanation += " The vehicle represents observed releases; a trip may combine several days at fast playback."; break;
            case "Technical Debt":
                Add("Debt · effort", Number(s.Debt)); Add("Debt ratio", s.DebtRatio.ToString("P1"));
                if (s.Day > 0) Add("Repayment capacity used · last day", Number(s.UsedDebtRepaymentCapacity));
                SharedDevelopers(); explanation += " Maintenance uses the same developer budget. This is maintenance alongside delivery, not a mandatory work-item stage. No extra employee or work item is created by the illustration."; break;
            default: return Empty;
        }
        if (s.Day == 0) Add("Capacity observations", "No completed day yet");
        var items = s.InspectionItems.Where(i => i.BelongsTo(selection)).ToArray();
        if (selection != station) Add("Selected queue · items", items.Length);
        var capacity = station == "Testing" ? s.AvailableTesterCapacity : s.AvailableDeveloperCapacity;
        var used = station == "Testing" ? s.UsedTesterCapacity : s.UsedDeveloperCapacity;
        if (station is "Testing" or "Development" or "Code Review" or "Technical Debt")
            Add(station == "Testing" ? "Tester utilization · last day" : "Shared developer utilization · last day", s.Day > 0 && capacity > 0 ? (used / capacity).ToString("P1") : "N/A");
        if (selection == "Dependency queue") Add("Queue scope", "Unresolved residual dependencies (Backlog subset)");
        var pressureId = selection == "Dependency queue" ? "dependency" : station switch { "Backlog" => "backlog", "Development" => "rework-wait", "Code Review" => "review-wait", "Testing" => "testing-wait", "Ready for Release" or "Release" => "ready", _ => "" };
        var pressure = s.QueuePressures.GetValueOrDefault(pressureId, QueuePressure.Normal);
        explanation = $"Observed queue: {pressure.Trend}. " + (pressure.Label.Length > 0 ? pressure.Label + ". " : "") + explanation;
        return new(selection, rows, explanation)
        {
            CompactSummary = $"Total items: {items.Length} · " + string.Join(" · ", items.GroupBy(i => i.Group).OrderBy(g => g.First().GroupOrder).Select(g => $"{g.Key}: {g.Count()}")) + $"\nQueue trend: {pressure.Trend}",
            SnapshotSummary = string.Join(" · ", rows.Where(r => r.Label.StartsWith("Active") || r.Label.Contains("utilization · last day")).Select(r => $"{r.Label}: {r.Value}")),
            ConditionColor = pressure.Kind == QueuePressureKind.Normal ? Simulation.UI.Theming.FlowPalette.Secondary : pressure.Color,
            Condition = pressureId.Length == 0 ? "Shared capacity activity" : pressure.Condition,
            EvidencePeriod = pressureId.Length == 0 ? $"Current Day {s.Day}" : pressure.Period,
            Observation = pressureId.Length == 0 ? "No waiting-queue pressure classification for this activity." : pressure.Evidence,
            Items = items.ToArray(),
            ItemSummary = station == "Technical Debt" ? "Aggregate maintenance effort; no individual debt work items." : $"{items.Length} items · Expand an item to inspect. Active WIP means admitted work, not proof of processing today. Queue waiting excludes backlog and active-state time."
        };
    }
    public static string Summary(GameSceneState? s)
    {
        if (s is not { HasSession: true }) return "Start LIVE to inspect the workshop.";
        (string Id, string Name)[] order = [("backlog", "Backlog"), ("review-wait", "Review"), ("testing-wait", "Testing"), ("ready", "Release"), ("rework-wait", "Rework"), ("dependency", "Dependencies")];
        var conditions = order.Where(x => s.QueuePressures.TryGetValue(x.Id, out var p) && p.Kind != QueuePressureKind.Normal)
            .Select(x => x.Name + ": " + s.QueuePressures[x.Id].Label).ToArray();
        return conditions.Length == 0 ? "No capacity warning observed · select a station for details" :
            string.Join("  ·  ", conditions.Take(3)) + (conditions.Length > 3 ? $"  ·  +{conditions.Length - 3} more in station details" : "");
    }
}
