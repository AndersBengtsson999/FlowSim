using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Simulation.Application;
using Simulation.Core;
using Simulation.Infrastructure;

namespace Simulation.UI.ViewModels;

public sealed class ScenarioEntry : INotifyPropertyChanged
{
    public ScenarioDefinition Scenario { get; }
    public Guid Id => Scenario.Id;
    public string Name => Scenario.Name;
    public string Status { get; }
    private bool included;
    private readonly Action refresh;
    public bool Included { get => included; set { included = value; PropertyChanged?.Invoke(this, new(nameof(Included))); refresh(); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    public ScenarioEntry(ScenarioDefinition scenario, string status, bool included, Action refresh)
    { Scenario = scenario; Status = status; this.included = included; this.refresh = refresh; }
}
public sealed record CompareTextCell(string Text, bool Different = false);
public sealed record CompareTextRow(string Label, IReadOnlyList<CompareTextCell> Cells);
public sealed record CompareChartValue(string Name, double? Value);
public sealed record ComparisonFlowPoint(int Day, double Value);
public sealed record ComparisonFlowSeries(string Name, IReadOnlyList<ComparisonFlowPoint> Points);
public enum ComparisonFlowMetric { TotalWip, WaitingForCodeReview, WaitingForTesting, WaitingForRework }

public sealed partial class CompareViewModel : INotifyPropertyChanged
{
    private ExperimentSession session = new();
    private readonly Func<SimulationRequest> readForm;
    private readonly Action<SimulationRequest> loadForm;
    private readonly Action<int> navigate;
    private ScenarioEntry? selected;
    private Guid? editing;
    private bool busy, synchronizing;
    private bool optionsValid = true;
    private CancellationTokenSource? cancellation;
    private ScenarioComparisonResult? comparison;
    private string status = "Duplicate Baseline, edit alternatives, then Run All Scenarios. Results never rank scenarios.";
    private AnalysisMetric metric = AnalysisMetric.ThroughputPerFiveDays;
    private ComparisonFlowMetric flowMetric;
    private bool common = true;
    private string seed = "12345", runs = "500";
    private ExperimentRunMode mode;
    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<ScenarioEntry> Scenarios { get; } = [];
    public ExperimentSession Session => session;
    public ScenarioComparisonResult? Comparison => comparison;
    public string ExperimentName { get; set; } = "New Experiment";
    public string Description { get; set; } = "";
    public string ScenarioName { get; set; } = "Baseline";
    public IReadOnlyList<string> Demonstrations => DemonstrationExperiments.Names;
    public string Demonstration { get; set; } = "Developer Capacity";
    public IReadOnlyList<ExperimentRunMode> Modes { get; } = Enum.GetValues<ExperimentRunMode>();
    public ExperimentRunMode Mode { get => mode; set { mode = value; OptionsChanged(); Changed(); } }
    public bool CommonRandomNumbers { get => common; set { common = value; OptionsChanged(); Changed(); } }
    public string BaseSeed { get => seed; set { seed = value; OptionsChanged(); Changed(); } }
    public string MonteCarloRuns { get => runs; set { runs = value; OptionsChanged(); Changed(); } }
    public bool IsBusy => busy;
    public bool CanEdit => !busy;
    public bool IsEditing => editing.HasValue;
    public string EditingLabel => editing.HasValue ? "Editing comparison scenario: " + session.Find(editing.Value).Name + ". Apply or discard this draft before comparing." : "";
    public string Status => status;
    public ScenarioEntry? Selected
    {
        get => selected;
        set { selected = value; ScenarioName = value?.Name ?? ""; Changed(); Changed(nameof(ScenarioName)); Changed(nameof(Traceability)); }
    }
    public ScenarioEntry? Baseline
    {
        get => Scenarios.FirstOrDefault(s => s.Id == session.Experiment.BaselineId);
        set { if (value is null || synchronizing) return; session.SetBaseline(value.Id); Changed(); Recompare(); }
    }
    public AnalysisMetric Metric { get => metric; set { metric = value; Changed(); Changed(nameof(ChartValues)); Changed(nameof(ChartLabel)); } }
    public IReadOnlyList<AnalysisMetric> Metrics => comparison?.Metrics.Select(m => m.Metric).ToArray() ?? ScenarioComparisonRunner.DisplayMetrics;
    public ComparisonFlowMetric FlowMetric { get => flowMetric; set { flowMetric = value; Changed(); Changed(nameof(FlowSeries)); } }
    public IReadOnlyList<ComparisonFlowMetric> FlowMetrics { get; } = Enum.GetValues<ComparisonFlowMetric>();
    public string ChartLabel => $"{PresentationLabels.Label(Metric)} — {(comparison?.ExperimentSnapshot.Options.Mode == ExperimentRunMode.MonteCarlo ? "P50 across runs" : "single-run value")}; ratios use 0–1";
    public IReadOnlyList<CompareChartValue> ChartValues => comparison?.Metrics.FirstOrDefault(r => r.Metric == metric)?.Cells
        .Select(c => new CompareChartValue(comparison.Runs.Single(r => r.ScenarioSnapshot.Id == c.ScenarioId).ScenarioSnapshot.Name, c.Distribution.P50)).ToArray() ?? [];
    public IReadOnlyList<CompareTextCell> Headers => comparison?.Runs.Select(r => new CompareTextCell(r.ScenarioSnapshot.Name + (r.ScenarioSnapshot.Id == comparison.ExperimentSnapshot.BaselineId ? " [reference]" : ""))).ToArray() ?? [];
    public IReadOnlyList<CompareTextRow> Rows => comparison?.Metrics.Select(row => new CompareTextRow(PresentationLabels.Label(row.Metric), row.Cells.Select(c =>
    {
        string V(double? v) => v is null ? "n/a" : ScenarioComparisonRunner.IsRatio(row.Metric) ? v.Value.ToString("P2", CultureInfo.InvariantCulture) : AnalysisReport.Number(v);
        var value = V(c.Distribution.P50);
        var delta = c.PercentagePoints.HasValue ? $"Δ {AnalysisReport.Number(c.PercentagePoints)} pp" : $"Δ {AnalysisReport.Number(c.Delta.Absolute)} ({AnalysisReport.Number(c.Delta.Percentage)}%)";
        var text = value + "\n" + delta;
        if (comparison.ExperimentSnapshot.Options.Mode == ExperimentRunMode.MonteCarlo)
            text += $"\nP75 {V(c.Distribution.P75)}\nP85 {V(c.Distribution.P85)}\nP95 {V(c.Distribution.P95)}\nn={c.Distribution.SampleCount}";
        return new CompareTextCell(text);
    }).ToArray())).ToArray() ?? [];
    public IReadOnlyList<CompareTextRow> PairedRows => comparison?.ExperimentSnapshot.Options is { Mode: ExperimentRunMode.MonteCarlo, CommonRandomNumbers: true }
        ? comparison.Metrics.Select(r => new CompareTextRow(PresentationLabels.Label(r.Metric), r.Cells.Select(c => new CompareTextCell(c.PairedDelta is { } d
            ? $"P50 {AnalysisReport.Number(d.P50)}\nP75 {AnalysisReport.Number(d.P75)}\nP85 {AnalysisReport.Number(d.P85)}\nP95 {AnalysisReport.Number(d.P95)}\npaired n={d.SampleCount}" : "n/a")).ToArray())).ToArray() : [];
    public IReadOnlyList<CompareTextCell> ParameterHeaders => IncludedScenarios().Select(s => new CompareTextCell(s.Name + (s.Id == session.Experiment.BaselineId ? " [reference]" : ""))).ToArray();
    public IReadOnlyList<CompareTextRow> ParameterRows
    {
        get
        {
            var scenarios = IncludedScenarios(); var basis = ScenarioParameters.Describe(session.Find(session.Experiment.BaselineId).Configuration);
            return basis.Keys.Select(k => new CompareTextRow(k, scenarios.Select(s =>
            { var value = ScenarioParameters.Describe(s.Configuration)[k]; return new CompareTextCell(value, value != basis[k]); }).ToArray())).ToArray();
        }
    }
    public IReadOnlyList<ComparisonFlowSeries> FlowSeries => comparison?.Runs.Take(3).Where(r => r.SingleRun is not null)
        .Select(r => new ComparisonFlowSeries(r.ScenarioSnapshot.Name, r.SingleRun!.Days.Select(d => new ComparisonFlowPoint(d.Day + 1, FlowMetric switch
        {
            ComparisonFlowMetric.TotalWip => d.TotalWip, ComparisonFlowMetric.WaitingForCodeReview => d.WaitingForCodeReviewCount,
            ComparisonFlowMetric.WaitingForTesting => d.WaitingForTestingCount, _ => d.WaitingForReworkCount
        })).ToArray())).ToArray() ?? [];
    public string Traceability => Selected is null ? "" : session.Results.FirstOrDefault(r => r.ScenarioSnapshot.Id == Selected.Id) is { } r
        ? $"{session.Status(Selected.Id)} · run {r.RunId} · model {r.SimulationModelVersion} · {r.ExecutedAtUtc:O}\n{r.Options.Mode} · runs {r.MonteCarlo?.NumberOfRuns ?? 1} · common seeds {r.Options.CommonRandomNumbers}\nOriginal: {AnalysisReport.Configuration(r.ScenarioSnapshot.Configuration)}\nExecuted: {AnalysisReport.Configuration(r.EffectiveConfiguration)}"
        : "Selected scenario has no stored result.";
    public AsyncCommand RunSelectedCommand { get; }
    public AsyncCommand RunAllCommand { get; }
    public AsyncCommand MonteCarloCommand { get; }
    public RelayCommand AddCommand { get; }
    public RelayCommand DuplicateCommand { get; }
    public RelayCommand RenameCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand ApplyCommand { get; }
    public RelayCommand DiscardCommand { get; }
    public RelayCommand DemonstrationCommand { get; }
    public RelayCommand CancelCommand { get; }
    public CompareViewModel(Func<SimulationRequest> readForm, Action<SimulationRequest> loadForm, Action<int> navigate)
    {
        InitializeSimpleCommands();
        this.readForm = readForm; this.loadForm = loadForm; this.navigate = navigate;
        AddCommand = new(() => Change(() => Refresh(session.Add().Id)), () => !busy && !IsEditing);
        DuplicateCommand = new(() => Change(() => Refresh(session.Duplicate(RequireSelected()).Id)), () => !busy && !IsEditing);
        RenameCommand = new(() => Change(() => { session.Rename(RequireSelected(), ScenarioName); Refresh(); }), () => !busy && !IsEditing);
        DeleteCommand = new(() => Change(() => { session.Delete(RequireSelected()); Refresh(); }), () => !busy && !IsEditing);
        ResetCommand = new(() => Change(() => { session.Reset(RequireSelected()); Refresh(); }), () => !busy && !IsEditing);
        EditCommand = new(() => Change(BeginEdit), () => !busy && !IsEditing);
        ApplyCommand = new(() => Change(ApplyDraft), () => !busy && IsEditing);
        DiscardCommand = new(DiscardDraft, () => !busy && IsEditing);
        DemonstrationCommand = new(() => Change(() => LoadExperiment(DemonstrationExperiments.Create(Demonstration))), () => !busy && !IsEditing);
        RunSelectedCommand = new(() => RunAsync(false), () => !busy && !IsEditing);
        RunAllCommand = new(() => RunAsync(true), () => !busy && !IsEditing);
        MonteCarloCommand = new(async () => { Mode = ExperimentRunMode.MonteCarlo; await RunAsync(true); }, () => !busy && !IsEditing);
        CancelCommand = new(Cancel, () => busy);
        Refresh();
    }
    private Guid RequireSelected() => Selected?.Id ?? throw new ArgumentException("Select a scenario.");
    private IReadOnlyList<ScenarioDefinition> IncludedScenarios() => session.Experiment.Scenarios.Where(s => s.Id == session.Experiment.BaselineId || Scenarios.Any(e => e.Id == s.Id && e.Included)).ToArray();
    private ComparisonOptions ReadOptions() => new(Mode, CommonRandomNumbers, int.Parse(BaseSeed, CultureInfo.InvariantCulture), int.Parse(MonteCarloRuns, CultureInfo.InvariantCulture));
    private void OptionsChanged()
    {
        if (synchronizing) return;
        optionsValid = false;
        Change(() => { session.SetOptions(ReadOptions()); optionsValid = true; Refresh(); });
    }
    private void BeginEdit()
    {
        var id = RequireSelected(); loadForm(session.Find(id).Configuration); editing = id; session.BeginEdit(id); Refresh(); navigate(0);
    }
    public void ApplyDraft()
    {
        if (editing is not { } id) return;
        session.Update(id, readForm() with { Name = session.Find(id).Name }); editing = null; Refresh(id); navigate(5);
    }
    public void DiscardDraft()
    { if (editing is { } id) { session.CancelEdit(id); editing = null; Refresh(id); navigate(5); } }
    public Experiment CaptureExperiment()
    {
        if (IsEditing) throw new ArgumentException("Apply or discard the scenario draft before saving or running.");
        session.SetOptions(ReadOptions()); session.SetDetails(ExperimentName, Description); return session.Experiment;
    }
    public void LoadExperiment(Experiment experiment)
    {
        if (busy || IsEditing) throw new ArgumentException("Finish or discard the current edit/run before loading an experiment.");
        var replacement = new ExperimentSession(experiment); session = replacement;
        synchronizing = true; ExperimentName = experiment.Name; Description = experiment.Description;
        mode = experiment.Options.Mode; common = experiment.Options.CommonRandomNumbers; seed = experiment.Options.BaseSeed.ToString(CultureInfo.InvariantCulture); runs = experiment.Options.MonteCarloRuns.ToString(CultureInfo.InvariantCulture);
        optionsValid = true; synchronizing = false; Refresh(experiment.BaselineId); status = "Experiment loaded. Configurations retained; run to create results."; Changed(null);
    }
    public void ImportScenario(ScenarioDefinition scenario)
    { if (busy || IsEditing) throw new ArgumentException("Finish the current edit/run first."); Refresh(session.Add(scenario.Configuration).Id); }
    public void Cancel() => cancellation?.Cancel();
    public async Task RunAsync(bool all)
    {
        if (busy) return;
        using var source = new CancellationTokenSource(); cancellation = source;
        try
        {
            var experiment = CaptureExperiment(); var id = RequireSelected(); busy = true; status = "Running scenarios…"; Refresh();
            var progress = new Progress<ExperimentProgress>(p => { if (!ReferenceEquals(cancellation, source)) return; status = $"Running scenario {p.ScenarioNumber} of {p.ScenarioCount}: {p.ScenarioName} · run {p.CompletedRuns}/{p.RunsPerScenario}"; Changed(nameof(Status)); });
            IReadOnlyList<ScenarioRun> completed;
            if (all) completed = await Task.Run(() => new ScenarioComparisonRunner().RunAll(experiment, progress, source.Token), source.Token);
            else completed = new[] { await Task.Run(() => new ScenarioComparisonRunner().Run(session.Find(id), experiment.Options, cancellationToken: source.Token), source.Token) };
            session.StoreAll(completed); status = $"Completed {completed.Count} scenario(s). Values are simulated measurements, not recommendations.";
        }
        catch (OperationCanceledException) { status = "Cancelled. Previous results retained; no partial batch published."; }
        catch (Exception ex) { status = ex.Message; }
        finally { cancellation = null; busy = false; Refresh(); }
    }
    public void NotifyFileStatus(string message) { status = message; Changed(nameof(Status)); }
    private void Change(Action action)
    {
        try { action(); }
        catch (Exception ex) { comparison = null; status = ex.Message; RefreshSimple(); NotifyResults(); }
        Changed(nameof(Status));
    }
    private void Refresh(Guid? selection = null)
    {
        var id = selection ?? selected?.Id; var old = Scenarios.ToDictionary(s => s.Id, s => s.Included);
        synchronizing = true; Scenarios.Clear();
        foreach (var s in session.Experiment.Scenarios) Scenarios.Add(new(s, session.Status(s.Id), old.GetValueOrDefault(s.Id, true), Recompare));
        Selected = Scenarios.FirstOrDefault(s => s.Id == id) ?? Scenarios.First(); synchronizing = false;
        Changed(nameof(Baseline)); Changed(nameof(IsBusy)); Changed(nameof(CanEdit)); Changed(nameof(IsEditing)); Changed(nameof(EditingLabel)); Changed(nameof(Status));
        foreach (var c in new[] { AddCommand, DuplicateCommand, RenameCommand, DeleteCommand, ResetCommand, EditCommand, ApplyCommand, DiscardCommand, DemonstrationCommand, CancelCommand }) c?.Refresh();
        RunSelectedCommand?.Refresh(); RunAllCommand?.Refresh(); MonteCarloCommand?.Refresh(); Recompare();
    }
    private void Recompare()
    {
        if (synchronizing) return;
        try { comparison = optionsValid ? session.Compare(IncludedScenarios().Select(s => s.Id)) : null; }
        catch (ScenarioValidationException) { comparison = null; }
        if (!Metrics.Contains(metric)) metric = AnalysisMetric.ThroughputPerFiveDays;
        RefreshSimple();
        NotifyResults();
    }
    private void NotifyResults()
    {
        foreach (var p in new[] { nameof(Comparison), nameof(Headers), nameof(Rows), nameof(PairedRows), nameof(ParameterHeaders), nameof(ParameterRows), nameof(Metrics), nameof(Metric), nameof(ChartValues), nameof(ChartLabel), nameof(FlowSeries), nameof(Traceability) }) Changed(p);
    }
    private void Changed([CallerMemberName] string? property = null) => PropertyChanged?.Invoke(this, new(property));
}
