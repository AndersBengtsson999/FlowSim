using Simulation.Application;
namespace Simulation.UI.ViewModels;

/// <summary>Links to existing metrics only. Stage work-in-progress stays in StationDetails.</summary>
public static class StationKpis
{
    public static IReadOnlyList<TeamKpi> For(string? station) => station switch
    {
        "Backlog" or "Dependency queue" => [TeamKpi.DevelopmentQueue, TeamKpi.DeveloperUtilization],
        "Development" or "Rework queue" => [TeamKpi.DeveloperUtilization, TeamKpi.DevelopmentQueue, TeamKpi.ReworkEffort],
        "Code Review" or "Review queue" => [TeamKpi.ReviewQueue, TeamKpi.DeveloperUtilization],
        "Testing" or "Testing queue" => [TeamKpi.TestingQueue, TeamKpi.TesterUtilization],
        "Ready for Release" or "Release" => [TeamKpi.ReleasedThroughput, TeamKpi.UnlaunchedDevelopmentCost],
        "Technical Debt" => [TeamKpi.TechnicalDebt, TeamKpi.DebtRepayment, TeamKpi.DeveloperUtilization],
        _ => []
    };
}
public sealed partial class LiveViewModel
{
    public IReadOnlyList<KpiCard> StationPerformance(string? station) => Live is null ? [] :
        KpiDashboard.Project(Live.Session, KpiSettings with { Selected = StationKpis.For(station) }, launchCosts: LaunchCostHistory)
            .Select(o => new KpiCard(o)).ToArray();
}
