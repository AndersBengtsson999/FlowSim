using Simulation.Core;
namespace Simulation.UI.ViewModels;

public static class ProductDeliveryPresentation
{
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
    public static string History(ProductDeliveryState? state) => state is null ? "" : string.Join("\n", state.Versions.Select(v =>
        $"{v.Name} ({v.Id}) · starts after Day {v.StartDay} · original plan {Day(v.OriginalPlannedDay)} · current plan {Day(v.PlannedDay)} · actual {Day(v.ActualLaunchDay)} · {v.Status} · {state.InternalReleases.Count(r => r.VersionId == v.Id)} internal releases · {state.CustomerLaunches.Where(l => l.VersionId == v.Id).Sum(l => l.ItemIds.Count)} customer-launched items" +
        string.Concat(v.Plans.Select(p => $"\n  Decision Day {p.DecisionDay}: {Day(p.PreviousDay)} → Day {p.TargetDay} ({p.Amount} {p.Unit}; sprint {p.SprintLength} days)"))));
    private static string Day(int? day) => day is { } n ? $"Day {n}" : "not set";
}
