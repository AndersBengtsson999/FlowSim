using System.Globalization;
using Simulation.Application;
using Simulation.Core;
namespace Simulation.UI.ViewModels;
public sealed partial class MainWindowViewModel
{
    public string DeveloperMonthlyCost { get; set; } = "85000";
    public string TesterMonthlyCost { get; set; } = "75000";
    public string SpecialistMonthlyCost { get; set; } = "100000";
    public string WorkingDaysPerMonth { get; set; } = "20";
    public string CostAssumptions => TeamEconomics.Assumptions;
    private void LoadCosts(RoleCostSettings costs)
    {
        DeveloperMonthlyCost = costs.DeveloperMonthly.ToString(CultureInfo.InvariantCulture);
        TesterMonthlyCost = costs.TesterMonthly.ToString(CultureInfo.InvariantCulture);
        SpecialistMonthlyCost = costs.SpecialistMonthly.ToString(CultureInfo.InvariantCulture);
        WorkingDaysPerMonth = costs.WorkingDaysPerMonth.ToString(CultureInfo.InvariantCulture);
    }
    private RoleCostSettings CaptureCosts()
    {
        static decimal Money(string text) => decimal.TryParse(text.Replace(',', '.'), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, CultureInfo.InvariantCulture, out var value)
            ? value : throw new ArgumentException("Monthly cost: enter a decimal number without thousands separators.");
        var costs = new RoleCostSettings(Money(DeveloperMonthlyCost), Money(TesterMonthlyCost), Money(SpecialistMonthlyCost), Integer(WorkingDaysPerMonth, "Working days per month"));
        costs.Validate(); return costs;
    }
    private IReadOnlyList<MetricRow> MonetaryResultMetrics => result is not { } r ? [] : MonetaryRows(TeamEconomics.From(r.Days, r.CompletedWorkItems));
    private static IReadOnlyList<MetricRow> MonetaryRows(EconomicPeriod period) => [
        new("Total team cost · SEK", TeamEconomics.Money(period.TotalTeamCost), TeamEconomics.Assumptions),
        new("Team cost / delivered item · SEK", TeamEconomics.Money(period.CostPerDelivery), "Full selected period cost / actually released items. N/A when none were released or monetary history is missing."),
        new("Allocated rework cost · SEK", TeamEconomics.Money(period.ReworkCost), TeamEconomics.Assumptions)];
}
