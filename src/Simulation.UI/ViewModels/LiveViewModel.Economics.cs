using Simulation.Application;
namespace Simulation.UI.ViewModels;
public sealed partial class LiveViewModel
{
    public string EconomicHeader => "Economics · " + (Live is null ? "SEK" : $"{TeamEconomics.Money(Live.Session.Configuration.RoleCosts.Staffing(Live.Session.Configuration.Team, Live.Session.Configuration.Skills).Total)} / next day");
    public string EconomicWindow => Performance is not { } p || p.AvailableDays == 0 ? "No observed days yet." : $"Rolling {RollingWindow} days · Days {p.FirstDay}–{p.LastDay} · {p.AvailableDays} observed days · SEK";
    public string EconomicAssumptions => TeamEconomics.Assumptions;
    public IReadOnlyList<MetricRow> EconomicRows => Performance is not { } p ? [] : [
        new("Total team cost", TeamEconomics.Money(p.Economics.TotalTeamCost), EconomicAssumptions),
        new("Released items", p.Completed.ToString(), "Actual deliveries during the same selected days."),
        new("Cost / delivered item", TeamEconomics.Money(p.Economics.CostPerDelivery), "N/A when no items were released or monetary history is missing."),
        new("Allocated rework cost", TeamEconomics.Money(p.Economics.ReworkCost), EconomicAssumptions)];
    public LiveTrendSeries EconomicTrend { get; private set; } = new([], []);
}
