using Simulation.Application;
namespace Simulation.UI.ViewModels;
public sealed record ScenarioKpiDisplayRow(ScenarioKpiRow Row)
{
    public string Name => Row.Definition.Name;
    public string Unit => KpiDisplay.Unit(Row.Definition);
    public string Baseline => Format(Row.Baseline);
    public string Intervention => Format(Row.Intervention);
    public string Difference => Format(Row.Difference) + (Row.Definition.Id is TeamKpi.DeveloperUtilization or TeamKpi.TesterUtilization ? " pp" : "");
    private string Format(double? value) => KpiDisplay.Format(Row.Definition.Id, value);
}
public sealed partial class LiveViewModel
{
    private string explorerMode = "Compare KPIs";
    private TeamKpiDefinition scenarioKpi = KpiDashboard.Definitions.Single(d=>d.Id==TeamKpi.ReleasedThroughput);
    public IReadOnlyList<string> ExplorerModes { get; } = ["Compare KPIs", "Compare Scenarios"];
    public string ExplorerMode { get => explorerMode; set { if(value == explorerMode || !ExplorerModes.Contains(value))return; explorerMode=value; RefreshScenarioKpis(); Notify(); } }
    public bool CompareKpis => ExplorerMode == "Compare KPIs";
    public bool CompareScenarios => !CompareKpis;
    public IReadOnlyList<TeamKpiDefinition> ScenarioOptions { get; } = KpiDashboard.Definitions.Where(d=>GuidedInterventionExperiment.ComparisonMetrics.Contains(d.Id)).ToArray();
    public TeamKpiDefinition ScenarioKpi { get=>scenarioKpi; set { if(value is null || value == scenarioKpi || !GuidedInterventionExperiment.ComparisonMetrics.Contains(value.Id))return; scenarioKpi=value; RefreshScenarioKpis(); Notify(); } }
    public ScenarioKpiComparison? ScenarioComparison { get; private set; }
    public IReadOnlyList<ScenarioKpiDisplayRow> ScenarioRows { get; private set; } = [];
    public string ScenarioStatus => ScenarioComparison is not { } c ? guidedMessage + " No active experiment. Start a staffing experiment, change only Developers or only Testers, apply, then resume LIVE." :
        $"Evaluation Days {c.FirstDay}–{c.EndDay} · {c.ObservedDays}/{c.EndDay-c.FirstDay+1} days. " + (c.Unavailable is { } unavailable ? "Unavailable · " + unavailable : $"Matched Days {c.FirstDay}–{c.FirstDay+c.ObservedDays-1}. " + (c.FirstDay+c.ObservedDays-1==c.EndDay ? "Complete." : "In progress."));
    public LiveTrendSeries ScenarioBaseline => new(ScenarioComparison?.Points.Select(p=>new LiveTrendPoint(p.Day,KpiDisplay.Scale(ScenarioKpi.Id,p.Baseline))).ToArray() ?? [],[]);
    public LiveTrendSeries ScenarioIntervention => new(ScenarioComparison?.Points.Select(p=>new LiveTrendPoint(p.Day,KpiDisplay.Scale(ScenarioKpi.Id,p.Intervention))).ToArray() ?? [],[]);
    public string ScenarioUnit => KpiDisplay.Unit(ScenarioKpi);
    public string ScenarioNumberFormat => KpiDisplay.NumberFormat(ScenarioKpi.Id);
    public string ScenarioEconomics { get; private set; } = "";
    private void RefreshScenarioKpis()
    {
        ScenarioComparison = null; ScenarioRows=[]; ScenarioEconomics="";
        if (!CompareScenarios || guidedExperiment is null) return;
        ScenarioComparison=guidedExperiment.ObserveKpis(Live?.Session,ScenarioKpi.Id);
        ScenarioRows=ScenarioComparison.Rows.Select(r=>new ScenarioKpiDisplayRow(r)).ToArray();
        var result=guidedExperiment.Observe(Live?.Session);
        if(result.InvalidReason is not null || result.Baseline is not { } b || result.Intervention is not { } x)return;
        var bp=b.Period;var xp=x.Period;
        ScenarioEconomics=$"Released: baseline {bp.Completed}, intervention {xp.Completed}, Δ {xp.Completed-bp.Completed}.\n" +
            $"Incremental cost / additional delivery: {TeamEconomics.Money(EconomicPeriod.IncrementalCostPerAdditionalDelivery(bp.Economics,xp.Economics))}. " +
            "Δ team cost / Δ released; N/A when deliveries do not increase or costs are missing. Negative means lower cost with more deliveries. Rework cost is already included in team cost.";
    }
}
