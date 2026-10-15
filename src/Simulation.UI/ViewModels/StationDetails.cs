namespace Simulation.UI.ViewModels;

public sealed record StationMetric(string Label, string Value);
public sealed record StationDetails(string Title, IReadOnlyList<StationMetric> Rows, string Explanation)
{
    public static readonly StationDetails Empty = new("", [], "");
    public static StationDetails For(string? station, GameSceneState? s)
    {
        if (station is null || s is not { HasSession: true }) return Empty;
        var rows = new List<StationMetric>();
        void Add(string label, object value) => rows.Add(new(label, value.ToString() ?? ""));
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
        var explanation = "Counts are exact. Stacks are bounded samples; grouped packages do not represent individual items. Worker poses and trips are illustrative.";
        switch (station)
        {
            case "Backlog":
                Add("Waiting · total", s.Areas[0].Waiting); Add("Dependency waiting · included", s.DependencyWaiting);
                Add("Other backlog", Math.Max(0, s.Areas[0].Waiting - s.DependencyWaiting)); Pressure("backlog"); break;
            case "Development":
                Add("Active development / WIP limit", $"{s.ActiveDevelopment} / {s.DevelopmentWipLimit}");
                Add("Rework · active / waiting", $"{s.ActiveRework} / {s.WaitingRework}");
                SharedDevelopers();
                if (s.Day > 0) { Add("Development capacity used · last day", Number(s.UsedDevelopmentCapacity)); Add("Rework capacity used · last day", Number(s.UsedReworkCapacity)); Add("Collaboration capacity · last day", Number(s.CollaborationCapacity)); }
                Pressure("rework-wait"); explanation += " Development, review, rework and debt repayment share developer capacity. The queue change shown here refers to waiting rework."; break;
            case "Code Review":
                Add("Waiting", s.Areas[2].Waiting); Add("Active WIP / limit", $"{s.Areas[2].Active} / {s.ReviewWipLimit}");
                if (s.Day > 0) Add("Review capacity used · last day", Number(s.UsedReviewCapacity)); SharedDevelopers(); Pressure("review-wait");
                explanation += " There is no independent review capacity allocation. A shared-capacity warning is an observation, not proof of a unique root bottleneck."; break;
            case "Testing":
                Add("Waiting", s.Areas[3].Waiting); Add("Active WIP / limit", $"{s.Areas[3].Active} / {s.TestingWipLimit}"); Add("Testers · configured", s.Testers);
                if (s.Day > 0) Add("Tester used / available · last day", $"{Number(s.UsedTesterCapacity)} / {Number(s.AvailableTesterCapacity)}");
                Pressure("testing-wait"); explanation += " Active WIP counts admitted work, even when no testers are available to process it."; break;
            case "Ready for Release":
                Add("Waiting for release", s.Areas[4].Waiting); Add("Release policy", s.ReleasePolicy); Add("Released today", s.ReleasedToday); Pressure("ready");
                explanation += " Scheduled accumulation can be expected batching; its size alone is not a capacity diagnosis."; break;
            case "Release":
                Add("Delivered · cumulative", s.Areas[5].Count); Add("Released today", s.ReleasedToday); Add("Ready for release", s.Areas[4].Waiting); Add("Release policy", s.ReleasePolicy); Pressure("ready");
                explanation += " The vehicle represents observed releases; a trip may combine several days at fast playback."; break;
            case "Technical Debt":
                Add("Debt · effort", Number(s.Debt)); Add("Debt ratio", s.DebtRatio.ToString("P1"));
                if (s.Day > 0) Add("Repayment capacity used · last day", Number(s.UsedDebtRepaymentCapacity));
                SharedDevelopers(); explanation += " Maintenance uses the same developer budget. No extra employee or work item is created by the illustration."; break;
            default: return Empty;
        }
        if (s.Day == 0) Add("Capacity observations", "No completed day yet");
        return new(station, rows, explanation);
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
