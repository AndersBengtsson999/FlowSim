namespace Simulation.Core;

public sealed record LaunchCostPoint(int Day, decimal WorkInProgress, decimal AwaitingLaunch,
    decimal CustomerLaunched, decimal Unallocated, decimal Incurred, bool CustomerLaunch)
{
    public decimal Unlaunched => WorkInProgress + AwaitingLaunch;
}
public sealed record LaunchCostHistory(IReadOnlyList<LaunchCostPoint> Points, int MissingCostDays, int MissingCapacityDays)
{
    public LaunchCostPoint Current => Points.LastOrDefault() ?? new(0, 0, 0, 0, 0, 0, false);
}

/// <summary>Deterministic estimate from persisted raw capacity and incurred staffing costs. No simulation mutation.</summary>
public static class LaunchEconomics
{
    public static LaunchCostHistory Calculate(IReadOnlyList<DailySnapshot> days, ProductDeliveryState? delivery)
    {
        var launchDays = (delivery?.CustomerLaunches ?? []).SelectMany(l => l.ItemIds.Select(id => (id, l.Day))).ToDictionary(x => x.id, x => x.Day);
        var eventDays = (delivery?.CustomerLaunches ?? []).Select(l => l.Day).ToHashSet();
        var costs = new Dictionary<string, decimal>();
        var released = new HashSet<string>();
        var previousCapacity = new Dictionary<string, DeliveryCost>();
        var points = new List<LaunchCostPoint>();
        decimal incurred = 0; int missingCost = 0, missingCapacity = 0;
        foreach (var day in days)
        {
            foreach (var item in day.Items.Where(i => i.State.IsDelivered())) released.Add(item.Id);
            var raw = new Dictionary<string, (decimal Dev, decimal Test)>();
            foreach (var item in day.Items)
            {
                if (item.ConsumedCapacity is { } c) raw[item.Id] = ((decimal)(c.Development + c.CodeReview + item.ReworkWork), (decimal)c.Testing);
                else if (item.DeliveryCost is { IsComplete: true } cumulative &&
                         (previousCapacity.ContainsKey(item.Id) || day.Day == 0 || item.CreatedDay >= day.Day))
                {
                    var prior = previousCapacity.GetValueOrDefault(item.Id) ?? new();
                    raw[item.Id] = ((decimal)Math.Max(0, cumulative.Development + cumulative.CodeReview + cumulative.Rework - prior.Development - prior.CodeReview - prior.Rework),
                        (decimal)Math.Max(0, cumulative.Testing - prior.Testing));
                }
                if (item.DeliveryCost is { IsComplete: true } observed) previousCapacity[item.Id] = observed;
            }
            if (day.Economics is not { } economy) missingCost++;
            else
            {
                incurred += economy.Total;
                if (day.Items.Any(i => !raw.ContainsKey(i.Id))) missingCapacity++;
                var observations = day.Items.Where(i => raw.ContainsKey(i.Id)).ToArray();
                decimal Dev(WorkItemDaySnapshot i) => raw[i.Id].Dev;
                decimal Test(WorkItemDaySnapshot i) => raw[i.Id].Test;
                var devDenominator = Math.Max((decimal)day.AvailableDeveloperCapacity, observations.Sum(Dev));
                var testDenominator = Math.Max((decimal)day.AvailableTesterCapacity, observations.Sum(Test));
                decimal remainingDev = economy.Developers + economy.Specialists, remainingTest = economy.Testers;
                foreach (var item in observations)
                {
                    var dev = devDenominator > 0 ? Math.Min(remainingDev, (economy.Developers + economy.Specialists) * Dev(item) / devDenominator) : 0;
                    var test = testDenominator > 0 ? Math.Min(remainingTest, economy.Testers * Test(item) / testDenominator) : 0;
                    remainingDev -= dev; remainingTest -= test;
                    costs[item.Id] = costs.GetValueOrDefault(item.Id) + dev + test;
                }
            }
            decimal wip = 0, awaiting = 0, launched = 0;
            foreach (var (id, cost) in costs)
            {
                if (launchDays.TryGetValue(id, out var launchDay) && launchDay <= day.Day + 1) launched += cost;
                else if (released.Contains(id)) awaiting += cost;
                else wip += cost;
            }
            points.Add(new(day.Day + 1, wip, awaiting, launched, incurred - wip - awaiting - launched, incurred, eventDays.Contains(day.Day + 1)));
        }
        return new(points.AsReadOnly(), missingCost, missingCapacity);
    }
}
