namespace Simulation.Core;

public sealed record CustomerDeliveryMetrics(int LaunchedItems, int InternalReleasesAwaitingLaunch, int InternallyReleasedItemsAwaitingLaunch,
    double? AverageInternalReleaseToCustomerDays, double? AverageWorkStartToCustomerDays)
{
    public static CustomerDeliveryMetrics From(ProductDeliveryState? state, IReadOnlyList<WorkItemResult> items)
        => From(state, items.ToDictionary(w => w.Id, w => w.DevelopmentStartedDay));
    public static CustomerDeliveryMetrics From(ProductDeliveryState? state, IReadOnlyDictionary<string, int?> startsById)
    {
        if (state is null) return new(0, 0, 0, null, null);
        var launches = state.CustomerLaunches.SelectMany(l => l.ItemIds.Select(id => (Id: id, l.Day))).ToDictionary(x => x.Id, x => x.Day);
        var launchedVersions = state.CustomerLaunches.Select(l => l.VersionId).ToHashSet();
        var released = state.InternalReleases.SelectMany(r => r.ItemIds.Select(id => (Id: id, r.Day))).ToArray();
        var waits = released.Where(x => launches.ContainsKey(x.Id)).Select(x => (double)(launches[x.Id] - x.Day)).ToArray();
        var starts = launches.Where(x => startsById.TryGetValue(x.Key, out var start) && start.HasValue).Select(x => (double)(x.Value - startsById[x.Key]!.Value)).ToArray();
        return new(launches.Count, state.InternalReleases.Count(r => !launchedVersions.Contains(r.VersionId)), released.Count(x => !launches.ContainsKey(x.Id)),
            waits.Length == 0 ? null : waits.Average(), starts.Length == 0 ? null : starts.Average());
    }
}
