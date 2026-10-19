namespace Simulation.Application;

public sealed record ScenarioKpiPoint(int Day, double? Baseline, double? Intervention)
{
    public double? Difference => Intervention - Baseline;
}
public sealed record ScenarioKpiRow(TeamKpiDefinition Definition, double? Baseline, double? Intervention)
{
    public double? Difference => Intervention - Baseline;
}
public sealed record ScenarioKpiComparison(int FirstDay, int EndDay, int ObservedDays, string? Unavailable,
    IReadOnlyList<ScenarioKpiRow> Rows, IReadOnlyList<ScenarioKpiPoint> Points);

public sealed partial class GuidedInterventionExperiment
{
    public static IReadOnlyList<TeamKpi> ComparisonMetrics { get; } = Array.AsReadOnly(new[] {
        TeamKpi.ReleasedThroughput, TeamKpi.CycleTime, TeamKpi.CostPerDelivery, TeamKpi.TotalTeamCost,
        TeamKpi.DeveloperUtilization, TeamKpi.TesterUtilization, TeamKpi.ReworkCost });
    // Projections only: histories remain owned by the existing two B5.1 sessions.
    public ScenarioKpiComparison ObserveKpis(Simulation.Core.SimulationSession? current, TeamKpi metric)
    {
        if (!ComparisonMetrics.Contains(metric)) throw new ArgumentOutOfRangeException(nameof(metric));
        var observation = Observe(current);
        if (observation.InvalidReason is not null || !BaselineReady || observation.ObservedDays == 0)
            return new(StartDay+1, EndDay, observation.ObservedDays,
                observation.InvalidReason ?? (!BaselineReady ? "Preparing unchanged baseline…" : "Resume or step LIVE to observe evaluation days."), [], []);
        var last = StartDay + observation.ObservedDays;
        double? Value(Simulation.Core.SimulationSession session, TeamKpi id, int end) => KpiDashboard.PeriodValue(id, session, StartDay+1, end);
        var rows = ComparisonMetrics.Select(id => new ScenarioKpiRow(KpiDashboard.Definitions.Single(d=>d.Id==id), Value(baseline!,id,last),Value(live,id,last))).ToArray();
        // Prefixes of the evaluation period, never rolling windows containing pre-intervention days.
        var points = Enumerable.Range(StartDay+1, observation.ObservedDays).Select(day =>
            new ScenarioKpiPoint(day,Value(baseline!,metric,day),Value(live,metric,day))).ToArray();
        return new(StartDay+1, EndDay, observation.ObservedDays, null, rows, points);
    }
}
