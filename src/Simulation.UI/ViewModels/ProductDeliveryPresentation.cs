using Simulation.Core;
namespace Simulation.UI.ViewModels;

public static class ProductDeliveryPresentation
{
    public static IReadOnlyList<string> Modes { get; } = ["Manual", "One-time", "Recurring"];
    public static string ModeName(LaunchPlanningMode mode) => mode switch { LaunchPlanningMode.Manual => "Manual", LaunchPlanningMode.OneTime => "One-time", _ => "Recurring" };
    public static LaunchPlanningMode ParseMode(string mode) => mode switch { "Manual" => LaunchPlanningMode.Manual, "One-time" => LaunchPlanningMode.OneTime, "Recurring" => LaunchPlanningMode.Recurring, _ => throw new ArgumentException("Select a launch mode.") };
    public static string Rule(LaunchSchedule rule) => rule.Mode == LaunchPlanningMode.Recurring
        ? $"Recurring: every {rule.Interval} {(rule.Unit == LaunchPlanUnit.Sprints ? "sprints" : "days")}"
        : ModeName(rule.Mode);
    public static string Summary(ProductDeliveryState? state, IReadOnlyDictionary<string, int?> starts)
    {
        if (state is null) return "Customer launch history is unavailable for this result. Existing delivery metrics describe internal releases.";
        var metrics = CustomerDeliveryMetrics.From(state, starts);
        string Days(double? value) => value is { } n ? $"{n:0.##} days" : "N/A";
        return $"Customer-launched items: {metrics.LaunchedItems} · Internal releases awaiting customer launch: {metrics.InternalReleasesAwaitingLaunch} · Internally released items awaiting launch: {metrics.InternallyReleasedItemsAwaitingLaunch}\n" +
            $"Internal release → customer launch: {Days(metrics.AverageInternalReleaseToCustomerDays)} · Work start → customer launch: {Days(metrics.AverageWorkStartToCustomerDays)}\n" +
            "Time averages include customer-launched items only. Existing throughput and cost/delivery continue to measure internal release; Launch Economics separately estimates allocated staffing cost." +
            (state.ImportedLegacy ? "\nImported save: historical internal releases were reconstructed from recorded delivery dates; no customer launches were invented." : "");
    }
    public static string History(ProductDeliveryState? state, LaunchCostHistory? economics = null)
    {
        if (state is null) return "";
        var allocated = new Dictionary<string, decimal>();
        decimal previous = 0;
        foreach (var launch in state.CustomerLaunches)
        {
            var point = economics?.Points.FirstOrDefault(p => p.Day == launch.Day);
            if (point is null) continue;
            allocated[launch.VersionId] = point.CustomerLaunched - previous; previous = point.CustomerLaunched;
        }
        return string.Join("\n", state.Versions.Select(v =>
            $"{v.Name} ({v.Id}) · starts after Day {v.StartDay} · original plan {Day(v.OriginalPlannedDay)} · final/current plan {Day(v.PlannedDay)} · actual {Day(v.ActualLaunchDay)} · {v.Status} · {Rule(v.EffectiveSchedule)} · {state.InternalReleases.Count(r => r.VersionId == v.Id)} internal releases · {state.CustomerLaunches.Where(l => l.VersionId == v.Id).Sum(l => l.ItemIds.Count)} customer-launched items" +
            (allocated.TryGetValue(v.Id, out var cost) ? $" · estimated allocated cost {cost:N0} SEK (recorded scope)" : "") +
            string.Concat(v.Plans.Select(p => p.Cancelled ? $"\n  Decision Day {p.DecisionDay}: schedule cancelled; Manual" : $"\n  Decision Day {p.DecisionDay}: {Day(p.PreviousDay)} → Day {p.TargetDay} ({p.Amount} {p.Unit}; sprint {p.SprintLength} days)"))));
    }
    private static string Day(int? day) => day is { } n ? $"Day {n}" : "not set";
}
