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
    public string Name => Observation.Definition.Name;
    public string Unit => Observation.Definition.Unit;
    public string Kind => Observation.Definition.Kind;
    public string Help => Observation.Definition.Description + " " + ComparisonHelp + " Trends show observed samples only; each rolling point ends on its labelled day.";
    private string Format(double? value) => value?.ToString(Observation.Definition.Id is TeamKpi.CostPerDelivery or TeamKpi.TotalTeamCost ? "N2" : "0.##",CultureInfo.CurrentCulture) ?? "N/A";
    public string Value => Format(Observation.Value);
    public string Period => Observation.AvailableDays == 0 ? "No observed days" : $"Days {Observation.FirstDay}–{Observation.LastDay}" + (Observation.AvailableDays < Observation.Window ? $" · {Observation.AvailableDays}/{Observation.Window}d" : $" · {Observation.Window}d");
    public string Change => Observation.Difference is not { } delta ? "Δ N/A · preceding window" : $"Δ {(delta > 0 ? "+" : "")}{Format(delta)} {DeltaUnit}";
    private string DeltaUnit => Observation.Definition.Id is TeamKpi.DeveloperUtilization or TeamKpi.TesterUtilization ? "pp" : Unit;
    public string ComparisonHelp => Observation.LastDay < 2 * Observation.Window ? "A comparison requires two complete equal-length windows." :
        $"Compared with Days {Observation.LastDay - 2 * Observation.Window + 1}–{Observation.LastDay - Observation.Window}. N/A if either value is undefined. Changes are neutral, not performance scores.";
    public LiveTrendSeries Trend => Observation.Trend;
    public string TrendCaption => Trend.Points.Count == 0 ? "No trend yet" : $"Day {Trend.Points[0].Day}–{Trend.Points[^1].Day} · scale 0–{Format(Math.Max(1,Trend.Points.Max(p=>p.Value ?? 0)))}";
}
public sealed partial class LiveViewModel
{
    public KpiDashboardSettings KpiSettings { get; private set; } = new KpiDashboardSettings().Normalize();
    public IReadOnlyList<KpiChoice> KpiChoices { get; private set; } = [];
    public IReadOnlyList<KpiCard> KpiCards { get; private set; } = [];
    public IReadOnlyList<int> KpiWindows { get; } = [10,20,40];
    public bool DashboardVisible { get => KpiSettings.Visible; set { KpiSettings=KpiSettings with{Visible=value}; SaveKpis(); } }
    public int KpiWindow { get => KpiSettings.Window; set { if(!KpiWindows.Contains(value))return; KpiSettings=KpiSettings with{Window=value}; SaveKpis(); } }
    public string KpiSelectionSummary => $"{KpiSettings.Selected!.Count}/4 selected · uncheck one to replace it";
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
    private void RefreshKpis() => KpiCards=Live is null ? [] : KpiDashboard.Project(Live.Session,KpiSettings).Select(o=>new KpiCard(o)).ToArray();
}
