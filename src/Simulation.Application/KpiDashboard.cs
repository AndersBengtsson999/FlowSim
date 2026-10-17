using Simulation.Core;
namespace Simulation.Application;

public enum TeamKpi { ReleasedThroughput, CycleTime, CostPerDelivery, ReworkEffort, TotalTeamCost, DeveloperUtilization, TesterUtilization, TotalWip, DevelopmentQueue, ReviewQueue, TestingQueue, TechnicalDebt }
public sealed record KpiDashboardSettings(int Window = 20, IReadOnlyList<TeamKpi>? Selected = null, bool Visible = true)
{
    public static IReadOnlyList<TeamKpi> Defaults { get; } = Array.AsReadOnly(new[] { TeamKpi.ReleasedThroughput, TeamKpi.CycleTime, TeamKpi.CostPerDelivery, TeamKpi.ReworkEffort });
    public KpiDashboardSettings Normalize() => this with { Window = Window is 10 or 20 or 40 ? Window : 20,
        Selected = Array.AsReadOnly((Selected ?? Defaults).Where(Enum.IsDefined).Distinct().Take(4).ToArray()) };
}
public sealed record TeamKpiDefinition(TeamKpi Id, string Name, string Unit, string Kind, string Description, LiveTrendMetric? Trend = null);
/// <summary>Read-only values separated from presentation, allowing future baseline/experiment columns.</summary>
public sealed record TeamKpiObservation(TeamKpiDefinition Definition, double? Value, double? Previous, int FirstDay, int LastDay,
    int AvailableDays, int Window, LiveTrendSeries Trend)
{
    public double? Difference => Value - Previous;
}
public static class KpiDashboard
{
    public static IReadOnlyList<TeamKpiDefinition> Definitions { get; } = Array.AsReadOnly(new[] {
        new TeamKpiDefinition(TeamKpi.ReleasedThroughput,"Released throughput","items / 5 days","Period rate","Actually released items / observed days × 5. Partial history uses observed days only.",LiveTrendMetric.Throughput),
        new(TeamKpi.CycleTime,"Cycle time","days","Released cohort average","Development start to release, averaged over items released in the window. N/A without releases.",LiveTrendMetric.CycleTime),
        new(TeamKpi.CostPerDelivery,"Cost / delivered item","SEK / item","Period ratio","Recorded team cost / actually released items. N/A without releases or complete monetary observations.",LiveTrendMetric.TeamCostPerDelivery),
        new(TeamKpi.ReworkEffort,"Rework effort","capacity units","Period total","Actual raw developer capacity consumed by Rework. Not a percentage or inferred defect effort."),
        new(TeamKpi.TotalTeamCost,"Total team cost","SEK","Period total","Sum of recorded daily staffing cost, including idle available staff. N/A if any monetary observation is missing."),
        new(TeamKpi.DeveloperUtilization,"Developer utilization","%","Period ratio","Total used / total available developer capacity. N/A when available capacity is zero.",LiveTrendMetric.DeveloperUtilization),
        new(TeamKpi.TesterUtilization,"Tester utilization","%","Period ratio","Total used / total available tester capacity. N/A when available capacity is zero.",LiveTrendMetric.TesterUtilization),
        new(TeamKpi.TotalWip,"Total WIP","items","End-of-period snapshot","Started but unreleased work at the final observed day, including queues and Ready for Release."),
        new(TeamKpi.DevelopmentQueue,"Development queue","items","End-of-period snapshot","Existing Backlog count, including dependency waiting; not active Development."),
        new(TeamKpi.ReviewQueue,"Code Review queue","items","End-of-period snapshot","Waiting for Code Review at the final observed day.",LiveTrendMetric.ReviewQueue),
        new(TeamKpi.TestingQueue,"Testing queue","items","End-of-period snapshot","Waiting for Testing at the final observed day.",LiveTrendMetric.TestingQueue),
        new(TeamKpi.TechnicalDebt,"Technical debt","effort units","End-of-period snapshot","Recorded outstanding debt amount, not cumulative debt creation. N/A without a debt observation.")
    });
    public static IReadOnlyList<TeamKpiObservation> Project(SimulationSession session, KpiDashboardSettings settings)
    {
        settings = settings.Normalize();
        var end = session.CurrentDay; var window = settings.Window; var start = Math.Max(1, end - window + 1);
        var current = end == 0 ? null : LivePerformance.Period(session, start, end);
        var previous = end < 2 * window ? null : LivePerformance.Period(session, end - 2 * window + 1, end - window);
        var trendStart = Math.Max(1, end - 39);
        return settings.Selected!.Select(id =>
        {
            var definition = Definitions.Single(d => d.Id == id);
            var trend = definition.Trend is { } metric ? LivePerformanceTrend.Project(session, metric, window, 40) :
                new LiveTrendSeries(Enumerable.Range(trendStart, Math.Max(0, end - trendStart + 1)).Select(day =>
                    new LiveTrendPoint(day, RawValue(id, session.Days, Math.Max(1, day - window + 1), day))).ToArray(),
                    session.Changes.Where(c => c.Day >= trendStart && c.Day <= end).ToArray());
            return new TeamKpiObservation(definition, Value(id,current,session.Days), Value(id,previous,session.Days),
                start,end,current?.AvailableDays ?? 0,window,trend);
        }).ToArray();
    }
    private static double? Value(TeamKpi id, PerformancePeriod? period, IReadOnlyList<DailySnapshot> history)
    {
        if (period is null || period.AvailableDays == 0) return null;
        return id switch {
            TeamKpi.ReleasedThroughput => period.Throughput, TeamKpi.CycleTime => period.CycleTime,
            TeamKpi.CostPerDelivery => (double?)period.Economics.CostPerDelivery,
            TeamKpi.TotalTeamCost => (double?)period.Economics.TotalTeamCost,
            TeamKpi.DeveloperUtilization => period.DeveloperUtilization * 100,
            TeamKpi.TesterUtilization => period.TesterUtilization * 100,
            TeamKpi.ReviewQueue => period.Review.Current, TeamKpi.TestingQueue => period.Testing.Current,
            _ => RawValue(id, history, period.FirstDay, period.LastDay)
        };
    }
    private static double? RawValue(TeamKpi id, IReadOnlyList<DailySnapshot> history, int first, int last)
    {
        if(last < 1 || last > history.Count) return null;
        var end = history[last - 1];
        return id switch {
            TeamKpi.ReworkEffort => history.Skip(first - 1).Take(last - first + 1).Sum(d => d.UsedReworkDeveloperCapacity),
            TeamKpi.TotalTeamCost => (double?)TeamEconomics.From(history.Skip(first - 1).Take(last - first + 1).ToArray(), 0).TotalTeamCost,
            TeamKpi.TotalWip => end.TotalWip, TeamKpi.DevelopmentQueue => end.BacklogCount,
            TeamKpi.TechnicalDebt => end.Debt?.State.Amount,
            _ => throw new ArgumentOutOfRangeException(nameof(id))
        };
    }
}
