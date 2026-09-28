using System.Globalization;
using Simulation.Application;
using Simulation.Core;

namespace Simulation.UI.ViewModels;

public sealed record ChangedParameter(string Name, string Before, string After)
{
    public string Change => $"{Before} → {After}";
    public static IReadOnlyList<ChangedParameter> Between(SimulationRequest baseline, SimulationRequest alternative)
    {
        var a = ScenarioParameters.Describe(baseline); var b = ScenarioParameters.Describe(alternative);
        return a.Where(p => p.Key != "Name" && p.Value != b[p.Key]).Select(p => new ChangedParameter(p.Key, p.Value, b[p.Key])).ToArray();
    }
}
public sealed record SimpleMetric(string Name, string Before, string After, string Difference)
{
    public bool HasDifference => Name != "Largest Queue";
    public string Explanation => Name switch {
        "Largest Queue" => "Largest observed end-of-day waiting queue during the simulation, including Rework. This is not a within-day peak.",
        "Throughput" or "Throughput / 5 days" => "Work Items completed per five simulated working days, including idle days.",
        "Average Lead Time" => "Average working days from creation until Done, completed items only.",
        "Cycle Time" or "Average Cycle Time" => "Average working days from Development start until Done, completed items only.",
        "Work in Progress" or "Average WIP" => "Average number of started Work Items that are not yet Done, including waiting queues.",
        "Developer Utilization" => "Share of available developer capacity used by Development, Code Review and Rework. Difference is in percentage points.",
        "Tester Utilization" => "Share of available tester capacity used. Difference is in percentage points.",
        _ => "Rework capacity divided by used developer capacity; difference is in percentage points."
    };
}
public sealed record QueueObservation(string Name, int Count)
{
    public string Description => $"{Name} · {Count} items (maximum end-of-day count)";
    public static QueueObservation From(SimulationResult r) => r.Days.SelectMany(d => new[] {
        new QueueObservation("Waiting for Code Review", d.WaitingForCodeReviewCount),
        new QueueObservation("Waiting for Testing", d.WaitingForTestingCount),
        new QueueObservation("Waiting for Rework", d.WaitingForReworkCount) }).OrderByDescending(q => q.Count).First();
}
public sealed record FlowComparisonRow(string Name, int Before, int After, double Scale);

public sealed partial class CompareViewModel
{
    public RelayCommand EditAlternativeCommand { get; private set; } = null!;
    public AsyncCommand RunPairCommand { get; private set; } = null!;
    private void InitializeSimpleCommands()
    {
        EditAlternativeCommand = new(() => { if (Alternative is { } a) { Selected = a; Change(BeginEdit); } }, () => !busy && !IsEditing && Alternative is not null);
        RunPairCommand = new(async () => {
            if (Alternative is not { } a) return;
            var baselineId = session.Experiment.BaselineId; var otherId = a.Id;
            Selected = Scenarios.Single(s => s.Id == baselineId); await RunAsync(false);
            Selected = Scenarios.Single(s => s.Id == otherId); await RunAsync(false);
        }, () => !busy && !IsEditing && Alternative is not null);
    }
    private Guid? alternativeId;
    private ScenarioComparisonResult? simpleComparison;
    private int flowDay = 1;
    public IReadOnlyList<ScenarioEntry> Alternatives => Scenarios.Where(s => s.Id != session.Experiment.BaselineId).ToArray();
    public ScenarioEntry? Alternative
    {
        get => Alternatives.FirstOrDefault(s => s.Id == alternativeId) ?? Alternatives.FirstOrDefault();
        set { if (value is null || synchronizing || value.Id == session.Experiment.BaselineId || value.Id == Alternative?.Id) return; alternativeId = value.Id; Selected = value; RefreshSimple(); }
    }
    public IReadOnlyList<ChangedParameter> ChangedParameters => Alternative is not { } a ? [] : ChangedParameter.Between(Baseline!.Scenario.Configuration, a.Scenario.Configuration);
    public string ChangeSummary => Alternative is null ? "Duplicate a scenario to create an alternative." : ChangedParameters.Count == 0 ? "No parameter changes. Names are not simulation parameters." : $"{ChangedParameters.Count} parameter(s) changed";
    public string PairStatus => simpleComparison is null ? "Run the baseline and alternative to see current results. Drafts and outdated results are hidden." : simpleComparison.ExperimentSnapshot.Options.Mode == ExperimentRunMode.MonteCarlo ? "Typical results (median across runs). Differences are alternative minus baseline; no value is ranked." : "Single-run results · difference = alternative minus baseline.";
    public IReadOnlyList<SimpleMetric> SimpleMetrics => simpleComparison is not { } c || Alternative is not { } a ? [] : c.Metrics
        .Where(r => PresentationLabels.Primary.Contains(r.Metric) || (r.Metric == AnalysisMetric.ReworkDeveloperCapacityShare && c.Runs.Any(x => x.EffectiveConfiguration.Quality.Enabled)))
        .Select(r => {
            var basis = r.Cells.Single(x => x.ScenarioId == c.ExperimentSnapshot.BaselineId); var other = r.Cells.Single(x => x.ScenarioId == a.Id);
            string V(double? n) => n?.ToString(ScenarioComparisonRunner.IsRatio(r.Metric) ? "P1" : "0.###", CultureInfo.InvariantCulture) ?? "n/a";
            string D(double? n) => n?.ToString("+0.###;-0.###;0", CultureInfo.InvariantCulture) ?? "n/a";
            return new SimpleMetric(PresentationLabels.Label(r.Metric), V(basis.Distribution.P50), V(other.Distribution.P50),
                other.PercentagePoints.HasValue ? D(other.PercentagePoints) + " percentage points" : D(other.Delta.Absolute));
        }).ToArray();
    private ScenarioRun? PairRun(bool baseline) => simpleComparison?.Runs.FirstOrDefault(r => r.ScenarioSnapshot.Id == (baseline ? session.Experiment.BaselineId : Alternative?.Id));
    public string BaselineQueue => QueueDescription(true);
    public string AlternativeQueue => QueueDescription(false);
    private string QueueDescription(bool baseline)
    {
        var run = PairRun(baseline);
        if (run?.SingleRun is { } r) return QueueObservation.From(r).Description;
        if (run?.MonteCarlo is { } m)
        {
            var review = m.MaximumWaitingForCodeReviewQueue.P50;
            var testing = m.MaximumWaitingForTestingQueue.P50;
            return $"{(review >= testing ? "Waiting for Code Review" : "Waiting for Testing")} · {(review >= testing ? review : testing):0.###} items (largest median queue maximum among Review and Testing; Rework maximum is unavailable in Monte Carlo).";
        }
        return "Run this pair to inspect queue observations.";
    }
    public bool HasPairFlow => PairRun(true)?.SingleRun is not null && PairRun(false)?.SingleRun is not null;
    public int FlowLastDay => HasPairFlow ? Math.Min(PairRun(true)!.SingleRun!.Days.Count, PairRun(false)!.SingleRun!.Days.Count) : 1;
    public int FlowDay { get => Math.Clamp(flowDay, 1, FlowLastDay); set { flowDay = Math.Clamp(value, 1, FlowLastDay); Changed(nameof(FlowDay)); Changed(nameof(SimpleFlow)); Changed(nameof(FlowDayLabel)); } }
    public string FlowDayLabel => $"End of day {FlowDay} · shared days 1–{FlowLastDay}. Both columns use the same scale. Initial day shows the largest observed queue within the shared horizon.";
    public IReadOnlyList<FlowComparisonRow> SimpleFlow
    {
        get {
            if (!HasPairFlow) return [];
            var a = PairRun(true)!.SingleRun!.Days[FlowDay - 1]; var b = PairRun(false)!.SingleRun!.Days[FlowDay - 1];
            var scale = Math.Max(1, Math.Max(PairRun(true)!.SingleRun!.TotalWorkItems, PairRun(false)!.SingleRun!.TotalWorkItems));
            var rows = new List<FlowComparisonRow> { new("Development (active)", a.DevelopmentCount,b.DevelopmentCount,scale), new("Waiting for Code Review",a.WaitingForCodeReviewCount,b.WaitingForCodeReviewCount,scale), new("Code Review (active)",a.CodeReviewCount,b.CodeReviewCount,scale),new("Waiting for Testing",a.WaitingForTestingCount,b.WaitingForTestingCount,scale),new("Testing (active)",a.TestingCount,b.TestingCount,scale) };
            if (simpleComparison!.Runs.Any(r => r.EffectiveConfiguration.Quality.Enabled)) { rows.Add(new("Waiting for Rework",a.WaitingForReworkCount,b.WaitingForReworkCount,scale)); rows.Add(new("Rework (active)",a.ReworkCount,b.ReworkCount,scale)); }
            return rows;
        }
    }
    public void DuplicateCompleted(SimulationRequest request, SimulationResult result, DateTimeOffset executedAt)
    {
        if (busy || IsEditing) throw new ArgumentException("Finish the current comparison run or draft first.");
        if (session.Experiment.Scenarios.Count > 18) throw new ArgumentException("This experiment needs two free scenario slots. Save it and start another experiment, or remove two scenarios.");
        var basis = session.Add(request); var copy = session.Duplicate(basis.Id);
        synchronizing = true; mode = ExperimentRunMode.SingleRun; common = true; seed = request.RandomSeed.ToString(CultureInfo.InvariantCulture); synchronizing = false;
        session.SetOptions(ReadOptions()); optionsValid = true; session.SetBaseline(basis.Id);
        session.Store(new(Guid.NewGuid(), basis, request, session.Experiment.Options, SimulationModel.Version, executedAt, result, null));
        alternativeId = copy.Id; Refresh(copy.Id); Changed(null); BeginEdit();
    }
    private void RefreshSimple()
    {
        EditAlternativeCommand?.Refresh(); RunPairCommand?.Refresh();
        simpleComparison = null;
        if (Alternative is { } a && optionsValid)
            try { simpleComparison = session.Compare([a.Id]); } catch (ScenarioValidationException) { }
        if (HasPairFlow)
        {
            int Queue(DailySnapshot d) => Math.Max(d.WaitingForCodeReviewCount, Math.Max(d.WaitingForTestingCount, d.WaitingForReworkCount));
            flowDay = Enumerable.Range(0, FlowLastDay).OrderByDescending(i => Math.Max(Queue(PairRun(true)!.SingleRun!.Days[i]),Queue(PairRun(false)!.SingleRun!.Days[i]))).First() + 1;
        }
        foreach (var p in new[] { nameof(Alternatives), nameof(Alternative), nameof(ChangedParameters), nameof(ChangeSummary), nameof(PairStatus), nameof(SimpleMetrics), nameof(BaselineQueue), nameof(AlternativeQueue), nameof(HasPairFlow), nameof(FlowDay), nameof(FlowLastDay), nameof(FlowDayLabel), nameof(SimpleFlow) }) Changed(p);
    }
}
