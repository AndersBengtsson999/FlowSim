using Simulation.Core;
namespace Simulation.Application;

public enum TeamKpi { ReleasedThroughput, CycleTime, CostPerDelivery, ReworkEffort, TotalTeamCost, DeveloperUtilization, TesterUtilization, TotalWip, DevelopmentQueue, ReviewQueue, TestingQueue, TechnicalDebt, CycleTimeP85, DeliveryFrequency, DeliveryVariability, WipAge, ReviewQueueGrowth, ReworkCost, ReworkProportion, DebtGrowth, DebtRepayment, CostPerWorkSize, WorkVolumePerSek, UnlaunchedDevelopmentCost }
public sealed record KpiDashboardSettings(int Window = 20, IReadOnlyList<TeamKpi>? Selected = null, bool Visible = true, TeamKpi? Explorer = null, TeamKpi? Comparison = null)
{
    public static IReadOnlyList<TeamKpi> Defaults { get; } = Array.AsReadOnly(new[] { TeamKpi.ReleasedThroughput, TeamKpi.CycleTime, TeamKpi.CostPerDelivery, TeamKpi.ReworkEffort });
    public KpiDashboardSettings Normalize() => this with { Window = Window is 10 or 20 or 40 ? Window : 20,
        Explorer = Explorer is { } e && Enum.IsDefined(e) ? e : null, Comparison = Comparison is { } c && Enum.IsDefined(c) ? c : null,
        Selected = Array.AsReadOnly((Selected ?? Defaults).Where(Enum.IsDefined).Distinct().Take(4).ToArray()) };
}
public sealed record TeamKpiDefinition(TeamKpi Id, string Name, string Unit, string Kind, string Description, LiveTrendMetric? Trend = null);
/// <summary>Read-only values separated from presentation, allowing future baseline/experiment columns.</summary>
public sealed record TeamKpiObservation(TeamKpiDefinition Definition, double? Value, double? Previous, int FirstDay, int LastDay,
    int AvailableDays, int Window, LiveTrendSeries Trend)
{
    public LaunchCostHistory? BalanceHistory { get; init; }
    public double? Difference => Value - Previous;
}
public static partial class KpiDashboard
{
    public static IReadOnlyList<TeamKpiDefinition> Definitions { get; } = Array.AsReadOnly(new[] {
        new TeamKpiDefinition(TeamKpi.ReleasedThroughput,"Released throughput","items / 5 days","Period rate","Internally released items / observed days × 5. Partial history uses observed days only.",LiveTrendMetric.Throughput),
        new(TeamKpi.CycleTime,"Cycle time","days","Released cohort average","Development start to release, averaged over items released in the window. N/A without releases.",LiveTrendMetric.CycleTime),
        new(TeamKpi.CostPerDelivery,"Cost / delivered item","SEK / item","Period ratio","Recorded team cost / internally released items. N/A without releases or complete monetary observations.",LiveTrendMetric.TeamCostPerDelivery),
        new(TeamKpi.ReworkEffort,"Rework effort","capacity units","Period total","Actual raw developer capacity consumed by Rework. Not a percentage or inferred defect effort."),
        new(TeamKpi.TotalTeamCost,"Total team cost","SEK","Period total","Sum of recorded daily staffing cost, including idle available staff. N/A if any monetary observation is missing."),
        new(TeamKpi.DeveloperUtilization,"Developer utilization","%","Period ratio","Total used / total available developer capacity. N/A when available capacity is zero.",LiveTrendMetric.DeveloperUtilization),
        new(TeamKpi.TesterUtilization,"Tester utilization","%","Period ratio","Total used / total available tester capacity. N/A when available capacity is zero.",LiveTrendMetric.TesterUtilization),
        new(TeamKpi.TotalWip,"Total WIP","items","End-of-period snapshot","Started but unreleased work at the final observed day, including queues and Ready for Release."),
        new(TeamKpi.DevelopmentQueue,"Development queue","items","End-of-period snapshot","Existing Backlog count, including dependency waiting; not active Development."),
        new(TeamKpi.ReviewQueue,"Code Review queue","items","End-of-period snapshot","Waiting for Code Review at the final observed day.",LiveTrendMetric.ReviewQueue),
        new(TeamKpi.TestingQueue,"Testing queue","items","End-of-period snapshot","Waiting for Testing at the final observed day.",LiveTrendMetric.TestingQueue),
        new(TeamKpi.TechnicalDebt,"Technical debt","effort units","End-of-period snapshot","Recorded outstanding debt amount, not cumulative debt creation. N/A without a debt observation."),
        new(TeamKpi.CycleTimeP85,"Cycle time · P85","days","Released cohort percentile","Nearest-rank 85th percentile of start-to-release duration for releases in the window; small cohorts are unstable."),
        new(TeamKpi.DeliveryFrequency,"Delivery frequency","% of days","Period ratio","100 × days with at least one release / observed days. Measures release-day frequency, not batch size or customer value."),
        new(TeamKpi.DeliveryVariability,"Delivery variability","items / day","Population standard deviation","Population standard deviation of daily released counts, including zero-release days. Batching can raise variability."),
        new(TeamKpi.WipAge,"Age of WIP","days","End-of-period average","Mean elapsed days since Development start among started, unreleased items at the final day. Excludes Backlog and does not predict completion."),
        new(TeamKpi.ReviewQueueGrowth,"Review queue growth","items / day","Period slope","Least-squares slope of daily waiting-for-review counts, requiring three observations. Not a causal diagnosis."),
        new(TeamKpi.ReworkCost,"Rework cost","SEK","Period total","Sum of recorded allocated rework cost. Included in team cost, not an extra charge; not lost revenue."),
        new(TeamKpi.ReworkProportion,"Rework proportion","%","Period ratio","100 × consumed rework capacity / total consumed developer capacity. Not a defect rate."),
        new(TeamKpi.DebtGrowth,"Technical debt growth","effort units","Period net change","Sum of recorded debt created minus debt repaid. Can be negative; not a monetary liability."),
        new(TeamKpi.DebtRepayment,"Debt repayment effort","capacity units","Period total","Sum of recorded raw repayment capacity. Not the amount of debt removed when productivity differs."),
        new(TeamKpi.CostPerWorkSize,"Cost / delivered work size","SEK / effort unit","Period ratio","Recorded team cost / sum of original Development effort of released items. Base effort is a size proxy, not business value."),
        new(TeamKpi.WorkVolumePerSek,"Delivered work / 1,000 SEK","effort units / SEK","Period ratio","Original Development effort of internally released items / recorded team cost. Customer launches do not define this numerator. Displayed per 1,000 SEK. Zero deliveries gives zero when cost is positive; not business value."),
        new(TeamKpi.UnlaunchedDevelopmentCost,"Unlaunched Development Cost","SEK","Current accumulated balance","Estimated allocated WIP plus internally released work awaiting customer launch. Excludes unallocated overhead; not a rolling-period cost. Missing historical allocations are excluded, never invented.")
    });
    public static IReadOnlyList<TeamKpiObservation> Project(SimulationSession session, KpiDashboardSettings settings, int range = 40, LaunchCostHistory? launchCosts = null)
    {
        settings = settings.Normalize();
        var end = session.CurrentDay; var window = settings.Window; var start = Math.Max(1, end - window + 1);
        var current = end == 0 ? null : LivePerformance.Period(session, start, end);
        var previous = end < 2 * window ? null : LivePerformance.Period(session, end - 2 * window + 1, end - window);
        var trendStart = Math.Max(1, end - range + 1);
        if (settings.Selected!.Contains(TeamKpi.UnlaunchedDevelopmentCost)) launchCosts ??= LaunchEconomics.Calculate(session.Days, session.ProductDelivery);
        return settings.Selected!.Select(id =>
        {
            var definition = Definitions.Single(d => d.Id == id);
            if (id == TeamKpi.UnlaunchedDevelopmentCost)
            {
                var history = launchCosts!;
                return new TeamKpiObservation(definition, (double)history.Current.Unlaunched, null,
                    end, end, end == 0 ? 0 : 1, 0,
                    new(history.Points.Where(p => p.Day >= trendStart).Select(p => new LiveTrendPoint(p.Day, (double)p.Unlaunched)).ToArray(), []))
                    { BalanceHistory = history };
            }
            var trend = definition.Trend is { } metric ? LivePerformanceTrend.Project(session, metric, window, range) :
                new LiveTrendSeries(Enumerable.Range(trendStart, Math.Max(0, end - trendStart + 1)).Select(day =>
                    new LiveTrendPoint(day, ObservedValue(id, session, Math.Max(1, day - window + 1), day))).ToArray(),
                    session.Changes.Where(c => c.Day >= trendStart && c.Day <= end).ToArray());
            return new TeamKpiObservation(definition, Value(id,current,session), Value(id,previous,session),
                start,end,current?.AvailableDays ?? 0,window,trend);
        }).ToArray();
    }
    public static double? PeriodValue(TeamKpi id, SimulationSession session, int first, int last) => last < first ? null : Value(id, LivePerformance.Period(session, first, last), session);
    private static double? Value(TeamKpi id, PerformancePeriod? period, SimulationSession session)
    {
        if (period is null || period.AvailableDays == 0) return null;
        return id switch {
            TeamKpi.ReleasedThroughput => period.Throughput, TeamKpi.CycleTime => period.CycleTime,
            TeamKpi.CostPerDelivery => (double?)period.Economics.CostPerDelivery,
            TeamKpi.TotalTeamCost => (double?)period.Economics.TotalTeamCost,
            TeamKpi.DeveloperUtilization => period.DeveloperUtilization * 100,
            TeamKpi.TesterUtilization => period.TesterUtilization * 100,
            TeamKpi.ReviewQueue => period.Review.Current, TeamKpi.TestingQueue => period.Testing.Current,
            _ => ObservedValue(id, session, period.FirstDay, period.LastDay)
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
