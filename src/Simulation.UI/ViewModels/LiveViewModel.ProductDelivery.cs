using Simulation.Core;
namespace Simulation.UI.ViewModels;
public sealed partial class LiveViewModel
{
    public bool ShowFlowProduct => HasSession && FlowBoardSelected;
    public bool LaunchDecisionPending => Live?.Session.PendingLaunchDecision is not null;
    public bool CanEditLaunch => HasSession && !IsRunning && !editing && !stopped;
    public bool CanLaunchProduct => CanEditLaunch && (LaunchDecisionPending || Live!.Session.ProductDelivery.InternalReleases.Any(r => r.VersionId == Live.Session.ProductDelivery.Versions[^1].Id));
    public string VersionNameDraft { get; set; } = "";
    public string LaunchDelay { get; set; } = "2";
    public string PlanningSprintLength { get; set; } = "10";
    public string PlanningUnit { get; set; } = "Sprints";
    public IReadOnlyList<string> PlanningUnits { get; } = ["Working days", "Sprints"];
    public RelayCommand LaunchProductCommand { get; private set; } = null!;
    public RelayCommand PlanLaunchCommand { get; private set; } = null!;
    public RelayCommand RenameVersionCommand { get; private set; } = null!;
    public string ProductVersionSummary
    {
        get
        {
            if (Live is null) return "Start LIVE to track product versions.";
            var state = Live.Session.ProductDelivery; var v = state.Versions[^1];
            var releases = state.InternalReleases.Where(r => r.VersionId == v.Id).ToArray();
            return $"{v.Name} ({v.Id}) · Day {Day} · " + (v.PlannedDay is { } planned ? $"Customer launch Day {planned} · {Math.Max(0, planned - Day)} working days remaining" : "Manual customer launch · no planned day") +
                $"\n{releases.Length} internal releases · {releases.Sum(r => r.ItemIds.Count)} internally released items awaiting customer launch";
        }
    }
    public string ProductDecisionText => LaunchDecisionPending ? $"Customer launch decision pending at the end of Day {Day}. No next day can run until you launch or postpone. Launch includes only work already internally released, including today's releases." : "Plan/postpone from the current day. Launch Now includes only internally released work. A successor version starts automatically; unfinished work keeps flowing. Pause LIVE to edit.";
    public string LiveCustomerSummary => ProductDeliveryPresentation.Summary(Live?.Session.ProductDelivery, Live?.Session.WorkItems.ToDictionary(w => w.Id, w => w.DevelopmentStartedDay) ?? []);
    public string LiveCustomerHistory => ProductDeliveryPresentation.History(Live?.Session.ProductDelivery);
    private void InitializeProductDelivery()
    {
        LaunchProductCommand = new(() => Guard(() =>
        {
            if (!CanLaunchProduct) return;
            Live!.Session.LaunchProductVersion(Live.Session.ProductDelivery.Versions[^1].Id);
            status = $"Customer launch recorded on Day {Day}. The next version is active; Resume when ready."; Refresh();
        }), () => CanLaunchProduct);
        PlanLaunchCommand = new(() => Guard(() =>
        {
            if (!CanEditLaunch) return;
            Live!.Session.PlanCustomerLaunch(Positive(LaunchDelay, "Launch delay"), PlanningUnit == "Sprints" ? LaunchPlanUnit.Sprints : LaunchPlanUnit.WorkingDays, Positive(PlanningSprintLength, "Sprint length"));
            status = "Customer launch plan updated. Resume when ready."; Refresh();
        }), () => CanEditLaunch);
        RenameVersionCommand = new(() => Guard(() =>
        { if (!CanEditLaunch) return; Live!.Session.RenameActiveVersion(VersionNameDraft); Refresh(); }), () => CanEditLaunch);
    }
}
