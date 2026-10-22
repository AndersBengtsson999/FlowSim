using Simulation.UI.Theming;
using System.ComponentModel;
using System.Globalization;
using Simulation.Application;
namespace Simulation.UI.ViewModels;

public sealed class KpiChoice(TeamKpiDefinition definition, LiveViewModel owner) : INotifyPropertyChanged
{
    public TeamKpiDefinition Definition { get; } = definition;
    public string Name => Definition.Name;
    public string Help => Definition.Description;
    public bool Selected { get => owner.KpiSettings.Selected!.Contains(Definition.Id); set => owner.SelectKpi(Definition.Id,value); }
    public bool CanSelect => Selected || owner.KpiSettings.Selected!.Count < 4;
    public event PropertyChangedEventHandler? PropertyChanged;
    internal void Refresh() => PropertyChanged?.Invoke(this,new(string.Empty));
}
public sealed record KpiCard(TeamKpiObservation Observation)
{
    public bool Selected { get; init; }
    public string SelectionBorder => Selected ? FlowPalette.Accent : FlowPalette.Border;
    public string Name => Observation.Definition.Name;
    public string Unit => KpiDisplay.Unit(Observation.Definition);
    public string NumberFormat => KpiDisplay.NumberFormat(Observation.Definition.Id);
    public bool IsBalance => Observation.Definition.Id == TeamKpi.UnlaunchedDevelopmentCost;
    public bool IsPeriod => !IsBalance;
    public Simulation.Core.LaunchCostHistory? BalanceHistory => Observation.BalanceHistory;
    public Simulation.Core.LaunchCostHistory? ExplorerBalanceHistory => BalanceHistory is { } h ? h with { Points = h.Points.TakeLast(120).ToArray() } : null;
    public string Components => BalanceHistory is { } h ? $"WIP: {h.Current.WorkInProgress:N0} SEK\nAwaiting customer launch: {h.Current.AwaitingLaunch:N0} SEK" : "";
    public string AllocationNote => BalanceHistory is { } h ? $"Estimated allocation · excludes overhead. Missing cost/capacity days: {h.MissingCostDays}/{h.MissingCapacityDays}." : "";
    public string Kind => Observation.Definition.Id == TeamKpi.WorkVolumePerSek ? "Internal release · period ratio" : Observation.Definition.Kind;
    public string ExplorerDescription => Observation.Definition.Description + " " + KpiDashboard.Interpretation(Observation.Definition.Id);
    public string Help => Observation.Definition.Description + " " + ComparisonHelp + (IsBalance ? " Daily balance, not a rolling ratio." : " Trends show observed samples only; each rolling point ends on its labelled day.");
    private string Format(double? value) => KpiDisplay.Format(Observation.Definition.Id, value);
    public string Value => Format(Observation.Value);
    public string Period => IsBalance ? $"Balance at Day {Observation.LastDay}" : Observation.AvailableDays == 0 ? "No observed days" : $"Days {Observation.FirstDay}–{Observation.LastDay}" + (Observation.AvailableDays < Observation.Window ? $" · {Observation.AvailableDays}/{Observation.Window}d" : $" · {Observation.Window}d");
    public string Change => IsBalance ? (BalanceHistory is { } h && (h.MissingCostDays > 0 || h.MissingCapacityDays > 0) ? "Partial allocation · missing history" : "Estimated allocation · excludes overhead") : Observation.Definition.Id == TeamKpi.WorkVolumePerSek ? KpiDisplay.Percentage(Observation.Value, Observation.Previous) : Observation.Difference is not { } delta ? "Δ N/A · preceding window" : $"Δ {(delta > 0 ? "+" : "")}{Format(delta)} {DeltaUnit}";
    private string DeltaUnit => Observation.Definition.Id is TeamKpi.DeveloperUtilization or TeamKpi.TesterUtilization or TeamKpi.ReworkProportion or TeamKpi.DeliveryFrequency ? "pp" : Unit;
    public string ComparisonHelp => IsBalance ? AllocationNote : Observation.LastDay < 2 * Observation.Window ? "A comparison requires two complete equal-length windows." :
        $"Compared with Days {Observation.LastDay - 2 * Observation.Window + 1}–{Observation.LastDay - Observation.Window}. N/A if either value is undefined. Changes are neutral, not performance scores.";
    public LiveTrendSeries Trend => KpiDisplay.Trend(Observation.Definition.Id, Observation.Trend);
    private string FormatTrend(double value) => value.ToString(NumberFormat, CultureInfo.CurrentCulture);
    public string TrendCaption => Trend.Points.Count == 0 ? "No trend yet" : $"Day {Trend.Points[0].Day}–{Trend.Points[^1].Day} · scale {FormatTrend(Math.Min(0,Trend.Points.Min(p=>p.Value ?? 0)))}–{FormatTrend((Trend.Points.All(p=>(p.Value ?? 0)==0) ? 1 : Math.Max(0,Trend.Points.Max(p=>p.Value ?? 0))))}";
}
public sealed partial class LiveViewModel
{
    public KpiDashboardSettings KpiSettings { get; private set; } = new KpiDashboardSettings().Normalize();
    public IReadOnlyList<KpiChoice> KpiChoices { get; private set; } = [];
    public IReadOnlyList<KpiCard> KpiCards { get; private set; } = [];
    public IReadOnlyList<int> KpiWindows { get; } = [10,20,40];
    public bool DashboardVisible { get => KpiSettings.Visible; set { KpiSettings=KpiSettings with{Visible=value}; SaveKpis(); } }
    public int KpiWindow { get => KpiSettings.Window; set { if(!KpiWindows.Contains(value))return; KpiSettings=KpiSettings with{Window=value}; SaveKpis(); } }
    public string KpiSelectionSummary => $"{KpiSettings.Selected!.Count}/4 selected · plus pinned unlaunched balance · uncheck one to replace it";
    public KpiCard? UnlaunchedCostCard { get; private set; }
    public IReadOnlyList<KpiCard> DisplayCards => UnlaunchedCostCard is null ? KpiCards : new[] { UnlaunchedCostCard }.Concat(KpiCards.Where(c => !c.IsBalance)).ToArray();
    public bool NoKpis => KpiSettings.Selected!.Count == 0;
    private void InitializeKpis() => KpiChoices = KpiDashboard.Definitions.Select(d=>new KpiChoice(d,this)).ToArray();
    public void SelectKpi(TeamKpi metric, bool selected)
    {
        if(!Enum.IsDefined(metric))return;
        var ids=KpiSettings.Selected!.ToList();
        if(selected && !ids.Contains(metric) && ids.Count<4)ids.Add(metric);
        else if(!selected)ids.Remove(metric);
        KpiSettings=(KpiSettings with{Selected=ids}).Normalize(); SaveKpis();
    }
    private void SaveKpis()
    {
        if(Live is not null)Live.Dashboard=KpiSettings;
        RefreshKpis();foreach(var choice in KpiChoices)choice.Refresh();Notify();
    }
    private void LoadKpis() { KpiSettings=Live!.Dashboard.Normalize();foreach(var choice in KpiChoices)choice.Refresh(); }
    private void RefreshKpis() { UnlaunchedCostCard = Live is null ? null : new KpiCard(KpiDashboard.Project(Live.Session, KpiSettings with { Selected = [TeamKpi.UnlaunchedDevelopmentCost] }, launchCosts: LaunchCostHistory).Single()) { Selected=KpiSettings.Explorer==TeamKpi.UnlaunchedDevelopmentCost };
        KpiCards=Live is null ? [] : KpiDashboard.Project(Live.Session,KpiSettings, launchCosts: LaunchCostHistory).Select(o=>new KpiCard(o) { Selected=KpiSettings.Explorer==o.Definition.Id }).ToArray(); RefreshExplorer(); }
}

public sealed partial class LiveViewModel
{
    private bool explorerExpanded;
    public bool ExplorerExpanded { get=>explorerExpanded; set { if(explorerExpanded==value)return; explorerExpanded=value;Notify(); } }
    public string WorkshopFeedback
    {
        get {
            if(Live is null || Day==0)return "Start or step LIVE to observe releases and changes.";
            var released=Live.Session.WorkItems.Count(w=>w.DoneDay==Day);
            var change=Live.Session.Changes.LastOrDefault();
            var effective=change is null ? "" : $" · Change {(change.Day >= Day ? "takes effect" : "effective")} Day {change.Day+1}";
            var complete=guidedExperiment is {InvalidReason:null} e && e.BaselineReady && Day>=e.EndDay ? " · Experiment complete" : "";
            return $"Day {Day} · Released {released} item(s){effective}{complete}";
        }
    }
    public bool ExplorerOpen => KpiSettings.Explorer.HasValue;
    public IReadOnlyList<TeamKpiDefinition> ExplorerOptions => KpiDashboard.Definitions;
    public TeamKpiDefinition? PrimaryKpi
    {
        get => ExplorerOptions.FirstOrDefault(d => d.Id == KpiSettings.Explorer);
        set { if (value is null || value.Id == KpiSettings.Explorer) return; KpiSettings = KpiSettings with { Explorer = value.Id, Comparison = KpiSettings.Comparison == value.Id ? null : KpiSettings.Comparison }; SaveKpis(); }
    }
    public TeamKpiDefinition? ComparisonKpi
    {
        get => ExplorerOptions.FirstOrDefault(d => d.Id == KpiSettings.Comparison);
        set { KpiSettings = KpiSettings with { Comparison = value?.Id == KpiSettings.Explorer ? null : value?.Id }; SaveKpis(); }
    }
    public KpiCard? ExplorerPrimary { get; private set; }
    public KpiCard? ExplorerSecondary { get; private set; }
    public bool HasComparison => ExplorerSecondary is not null;
    public string ExplorerHelp => KpiSettings.Explorer is { } id ? KpiDashboard.Interpretation(id) : "";
    public string ComparisonHelp => KpiSettings.Comparison is { } id ? KpiDashboard.Interpretation(id) : "";
    public void OpenExplorer(TeamKpi metric) { KpiSettings = KpiSettings with { Explorer = metric }; SaveKpis(); }
    public void CloseExplorer() { KpiSettings = KpiSettings with { Explorer = null }; SaveKpis(); }
    public void ClearComparison() { ComparisonKpi = null; }
    private void RefreshExplorer()
    {
        ExplorerPrimary = null; ExplorerSecondary = null;
        if (Live is null || KpiSettings.Explorer is not { } metric) return;
        var ids = new[] { metric, KpiSettings.Comparison ?? metric }.Distinct().ToArray();
        var cards = KpiDashboard.Project(Live.Session, KpiSettings with { Selected = ids }, 120, LaunchCostHistory).Select(o => new KpiCard(o)).ToArray();
        ExplorerPrimary = cards.First();
        ExplorerSecondary = KpiSettings.Comparison is { } second && second != metric ? cards.First(c => c.Observation.Definition.Id == second) : null;
    }
}
