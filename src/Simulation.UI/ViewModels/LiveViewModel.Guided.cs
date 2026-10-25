using Simulation.Application;

namespace Simulation.UI.ViewModels;

public sealed record GuidedMetricRow(string Metric, string Baseline, string Experiment, string Difference);
public sealed partial class LiveViewModel
{
    private LiveCheckpoint? guidedCheckpoint;
    private GuidedInterventionExperiment? guidedExperiment;
    private CancellationTokenSource? guidedCancellation;
    private string guidedMessage = "";
    private int evaluationDays = 20;
    public IReadOnlyList<int> EvaluationPeriods { get; } = [10, 20, 40];
    public int EvaluationDays { get => evaluationDays; set { if (EvaluationPeriods.Contains(value)) { evaluationDays = value; Notify(); } } }
    public bool GuidedPending => guidedCheckpoint is not null;
    public bool HasGuidedExperiment => guidedExperiment is not null || guidedMessage.Length > 0;
    public string GuidedDraftDescription => guidedCheckpoint is null ? "" :
        $"Staffing experiment: developers {guidedCheckpoint.State.Configuration.Team.DeveloperCount} → {Draft.NumberOfDevelopers}; testers {guidedCheckpoint.State.Configuration.Team.TesterCount} → {Draft.NumberOfTesters}. Effective Day {guidedCheckpoint.State.CurrentDay + 1}. Evaluate Days {guidedCheckpoint.State.CurrentDay + 1}–{guidedCheckpoint.State.CurrentDay + EvaluationDays}. An unchanged checkpoint baseline will be available if only Testers or only Developers is changed. Apply, then resume LIVE when ready.";
    public string GuidedTitle => guidedExperiment is { } e ? $"Experiment: Developers {e.OriginalDevelopers} → {e.ProposedDevelopers}; Testers {e.OriginalTesters} → {e.ProposedTesters}" : "Staffing experiment";
    public string GuidedHeader => GuidedTitle + (guidedExperiment is { } e ? $" · {Math.Clamp(Day - e.StartDay, 0, e.Window)}/{e.Window} days" + (e.InvalidReason is not null ? " · Invalidated" : "") : "");
    public string GuidedProgress { get; private set; } = "";
    public string GuidedInterpretation { get; private set; } = "";
    public string GuidedMetricHelp => "Released = actual deliveries. Queue = waiting for Testing, checkpoint → matched end day. Cycle time = existing released-item cohort average; — means no releases. Cost = total consumed System Cost capacity, including unfinished work and debt repayment, not money; — means no observations or unavailable capacity history. Both branches use the same observed days.";
    public IReadOnlyList<GuidedMetricRow> GuidedRows { get; private set; } = [];
    public IReadOnlyList<ComparisonFlowSeries> GuidedQueueSeries { get; private set; } = [];
    public IReadOnlyList<CompareChartValue> GuidedReleased { get; private set; } = [];
    public IReadOnlyList<CompareChartValue> GuidedCycleTime { get; private set; } = [];
    public IReadOnlyList<CompareChartValue> GuidedCost { get; private set; } = [];
    public string GuidedIncrementalShort { get; private set; } = "";
    public string GuidedIncrementalCost { get; private set; } = "";
    public IReadOnlyList<CompareChartValue> GuidedTeamCost { get; private set; } = [];
    public Task BaselineTask { get; private set; } = Task.CompletedTask;
    public Action? OpenGuidedComparison { get; set; }
    public Action? ReturnToLive { get; set; }
    public RelayCommand BeginGuidedCommand { get; private set; } = null!;
    public RelayCommand GuidedDetailsCommand { get; private set; } = null!;
    public RelayCommand GuidedReturnCommand { get; private set; } = null!;
    private void InitializeGuided()
    {
        BeginGuidedCommand = new(() => Guard(BeginGuidedExperiment), () => CanChange);
        GuidedDetailsCommand = new(() => OpenGuidedComparison?.Invoke());
        GuidedReturnCommand = new(() => ReturnToLive?.Invoke());
        Draft.PropertyChanged += (_, _) => { if (GuidedPending) PropertyChanged?.Invoke(this, new(nameof(GuidedDraftDescription))); };
    }
    public void BeginGuidedExperiment()
    {
        if (!CanChange || Live is null) return;
        BeginChange();
        foreach (var field in ChangeFields)
            field.PropertyChanged += (_, _) => { if (GuidedPending) PropertyChanged?.Invoke(this, new(nameof(GuidedDraftDescription))); };
        guidedCheckpoint = Live.CreateCheckpoint($"Staffing experiment · Day {Day}");
        Notify();
    }
    private void CancelGuidedDraft() { guidedCheckpoint = null; }
    private void ApplyGuidedExperiment()
    {
        if (guidedCheckpoint is not { } checkpoint || Live is null) return;
        guidedCheckpoint = null;
        guidedCancellation?.Cancel();
        if (!GuidedInterventionExperiment.SingleStaffingChange(checkpoint.State.Configuration, Live.Session.Configuration))
        {
            guidedExperiment = null;
            guidedMessage = "Comparison not started: change only Testers or only Developers for this experiment. Your ordinary LIVE changes were applied.";
            return;
        }
        guidedExperiment = new(checkpoint, Live.Session, EvaluationDays);
        guidedMessage = "Calculating the unchanged baseline… LIVE playback remains under your control.";
        guidedCancellation = new();
        BaselineTask = PrepareGuidedAsync(guidedExperiment, guidedCancellation.Token);
    }
    private async Task PrepareGuidedAsync(GuidedInterventionExperiment experiment, CancellationToken token)
    {
        try
        {
            await experiment.PrepareBaselineAsync(token);
            if (token.IsCancellationRequested || !ReferenceEquals(experiment, guidedExperiment)) return;
            guidedMessage = "";
            RefreshGuided(); Notify();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!ReferenceEquals(experiment, guidedExperiment)) return;
            experiment.Invalidate("Baseline unavailable: " + ex.Message);
            guidedMessage = ""; RefreshGuided(); Notify();
        }
    }
    private void InvalidateGuided(string reason)
    {
        guidedCheckpoint = null;
        guidedCancellation?.Cancel();
        guidedExperiment?.Invalidate(reason);
        guidedMessage = "";
        RefreshGuided();
    }
    private void RefreshGuided()
    {
        RefreshScenarioKpis();
        GuidedIncrementalShort = ""; GuidedIncrementalCost = ""; GuidedTeamCost = []; GuidedRows = []; GuidedQueueSeries = []; GuidedReleased = []; GuidedCycleTime = []; GuidedCost = []; GuidedInterpretation = "";
        if (guidedExperiment is not { } e) { GuidedProgress = guidedMessage; return; }
        var result = e.Observe(Live?.Session);
        if (result.InvalidReason is { } reason)
        {
            guidedCancellation?.Cancel();
            GuidedProgress = "Invalidated · " + reason; return;
        }
        GuidedProgress = $"{(result.Complete ? "Complete" : "In progress")} · Day {e.StartDay + result.ObservedDays} / {e.EndDay} · {result.ObservedDays}/{e.Window} days · Evaluation {e.StartDay + 1}–{e.EndDay}. " +
            (guidedMessage.Length > 0 ? guidedMessage : result.ObservedDays == 0 ? "Resume or step LIVE to observe the first day." : $"Comparing Days {e.StartDay + 1}–{e.StartDay + result.ObservedDays} in both branches.");
        if (result.Baseline is not { } b || result.Intervention is not { } x) return;
        static string N(double? value) => value?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) ?? "—";
        static string D(double? a, double? b) => a is null || b is null ? "—" : (b.Value - a.Value).ToString("+0.##;-0.##;0", System.Globalization.CultureInfo.InvariantCulture);
        var bp = b.Period; var xp = x.Period;
        GuidedRows = [new("Released", N(bp.Completed), N(xp.Completed), D(bp.Completed, xp.Completed)),
            new("Testing queue", $"{result.InitialQueue} → {bp.Testing.Current}", $"{result.InitialQueue} → {xp.Testing.Current}", D(bp.Testing.Current, xp.Testing.Current)),
            new("Cycle time · days", N(bp.CycleTime), N(xp.CycleTime), D(bp.CycleTime, xp.CycleTime)),
            new("Cost · capacity", N(bp.ConsumedSystemCapacity?.Total), N(xp.ConsumedSystemCapacity?.Total), D(bp.ConsumedSystemCapacity?.Total, xp.ConsumedSystemCapacity?.Total))];
        GuidedRows = [.. GuidedRows,
            new("Team cost · SEK", TeamEconomics.Money(bp.Economics.TotalTeamCost), TeamEconomics.Money(xp.Economics.TotalTeamCost), TeamEconomics.Money(xp.Economics.TotalTeamCost - bp.Economics.TotalTeamCost)),
            new("Cost / delivery · SEK", TeamEconomics.Money(bp.Economics.CostPerDelivery), TeamEconomics.Money(xp.Economics.CostPerDelivery), TeamEconomics.Money(xp.Economics.CostPerDelivery - bp.Economics.CostPerDelivery)),
            new("Allocated rework · SEK", TeamEconomics.Money(bp.Economics.ReworkCost), TeamEconomics.Money(xp.Economics.ReworkCost), TeamEconomics.Money(xp.Economics.ReworkCost - bp.Economics.ReworkCost))];
        GuidedIncrementalShort = "Incremental cost per additional delivery: " + TeamEconomics.Money(EconomicPeriod.IncrementalCostPerAdditionalDelivery(bp.Economics, xp.Economics));
        GuidedIncrementalCost = GuidedIncrementalShort +
            ". Δ team cost / Δ released items, only when deliveries increase. Negative means lower total cost with more deliveries; positive means additional cost per additional delivery. N/A means no additional deliveries or missing monetary history.";
        GuidedTeamCost = [new("Unchanged baseline", (double?)bp.Economics.TotalTeamCost), new(GuidedAlternativeName, (double?)xp.Economics.TotalTeamCost)];
        GuidedQueueSeries = [new("Unchanged baseline", b.TestingQueue.Select(p => new ComparisonFlowPoint(p.Day, p.Count)).ToArray()),
            new(GuidedAlternativeName, x.TestingQueue.Select(p => new ComparisonFlowPoint(p.Day, p.Count)).ToArray())];
        GuidedReleased = [new("Unchanged baseline", bp.Completed), new(GuidedAlternativeName, xp.Completed)];
        GuidedCycleTime = [new("Unchanged baseline", bp.CycleTime), new(GuidedAlternativeName, xp.CycleTime)];
        GuidedCost = [new("Unchanged baseline", bp.ConsumedSystemCapacity?.Total), new(GuidedAlternativeName, xp.ConsumedSystemCapacity?.Total)];
        GuidedInterpretation = $"Observed {GuidedAlternativeName} minus baseline: released {D(bp.Completed, xp.Completed)} items; Testing queue {D(bp.Testing.Current, xp.Testing.Current)} items; cycle time {D(bp.CycleTime, xp.CycleTime)} days; cost {D(bp.ConsumedSystemCapacity?.Total, xp.ConsumedSystemCapacity?.Total)} capacity. These are matched-period differences, not a ranking of benefit.";
    }
}
