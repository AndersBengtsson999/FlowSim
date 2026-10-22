using Simulation.Core;
namespace Simulation.UI.ViewModels;

public sealed record ReleaseInspection(string Version, int AwaitingItems, decimal AwaitingCost, string Mode, int? PlannedDay, int Launches, decimal LaunchedCost, bool Partial)
{
    public static ReleaseInspection From(ProductDeliveryState delivery, LaunchCostHistory history)
    {
        var metrics = CustomerDeliveryMetrics.From(delivery, new Dictionary<string, int?>());
        var current = delivery.Versions[^1];
        return new(current.Name, metrics.InternallyReleasedItemsAwaitingLaunch, history.Current.AwaitingLaunch,
            ProductDeliveryPresentation.Rule(current.EffectiveSchedule), current.PlannedDay, delivery.CustomerLaunches.Count,
            history.Current.CustomerLaunched, history.MissingCostDays > 0 || history.MissingCapacityDays > 0);
    }
    public string Text => $"Awaiting customer launch: {AwaitingItems:N0} items\nEstimated allocated cost: {AwaitingCost:N0} SEK\nVersion: {Version} · {Mode}\nNext launch: {(PlannedDay is { } day ? $"Day {day}" : "not scheduled")} · Completed launches: {Launches}\nCustomer-launched allocation: {LaunchedCost:N0} SEK\n" +
        "Awaiting cost excludes WIP and unallocated overhead. Launch reclassifies cost; it is not new expense or revenue." +
        (Partial ? " Partial history: missing allocations are excluded." : "");
}
public sealed partial class LiveViewModel
{
    public ReleaseInspection? ReleaseEconomicContext => Live is null ? null : ReleaseInspection.From(Live.Session.ProductDelivery, LaunchCostHistory);
    public string ReleaseInspectionText => ReleaseEconomicContext?.Text ?? "No LIVE session.";
}
