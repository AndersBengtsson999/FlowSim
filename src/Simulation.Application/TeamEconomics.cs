using Simulation.Core;
namespace Simulation.Application;

public sealed record EconomicPeriod(decimal? TotalTeamCost, int Released, decimal? ReworkCost)
{
    public decimal? CostPerDelivery => Released > 0 ? TotalTeamCost / Released : null;
    public static decimal? IncrementalCostPerAdditionalDelivery(EconomicPeriod baseline, EconomicPeriod intervention) =>
        intervention.Released > baseline.Released ? (intervention.TotalTeamCost - baseline.TotalTeamCost) / (intervention.Released - baseline.Released) : null;
}
public static class TeamEconomics
{
    public static EconomicPeriod From(IReadOnlyList<DailySnapshot> days, int released)
    {
        // Old history has no monetary observation; never back-price it with today's rates.
        if (days.Count == 0 || days.Any(d => d.Economics is null)) return new(null, released, null);
        return new(days.Sum(d => d.Economics!.Total), released, days.Sum(d => d.Economics!.Rework));
    }
    public static string Money(decimal? value) => value?.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("sv-SE")) + (value is null ? "N/A" : " SEK");
    public const string Assumptions = "Illustrative fully loaded employer costs in SEK, not company salaries. Cost is allocated to configured headcount × availability, including idle staff. Specialists replace developers in the cost total. Rework is the developer pool cost allocated by observed rework capacity / available developer capacity; included in total, not an extra charge. N/A means no deliveries or missing monetary history; older days are never repriced.";
}
