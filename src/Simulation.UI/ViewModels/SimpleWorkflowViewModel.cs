using System.ComponentModel;
using Simulation.Application;

namespace Simulation.UI.ViewModels;

public sealed class SimpleChangeField(string label, string current, Func<string> read, Action<string> write, string help = "")
{
    public string Label { get; } = label;
    public string Current { get; } = current;
    public string Value { get => read(); set => write(value); }
    public string Help { get; } = help;
}

/// <summary>Question-oriented navigation and orchestration; all numerical work stays in existing application services.</summary>
public sealed class SimpleWorkflowViewModel : INotifyPropertyChanged
{
    public MainWindowViewModel Owner { get; }
    public LiveViewModel Live { get; } = new();
    public bool LiveVisible => page == "Live";
    public RelayCommand OpenLiveCommand { get; }
    public MainWindowViewModel Try { get; } = new();
    public CompareViewModel Pair { get; }
    public SensitivityViewModel Explore { get; }
    private SimulationRequest starting = BaselineScenario.CreateRequest();
    private string page = "Home", startingPoint = "Baseline", status = "";
    private bool busy;
    public event PropertyChangedEventHandler? PropertyChanged;
    public IReadOnlyList<string> StartingPoints { get; } = ["Baseline", "Last simulation"];
    public string StartingPoint { get => startingPoint; set { if (busy || value is not ("Baseline" or "Last simulation") || value == startingPoint) return; if (value == "Last simulation" && Owner.LastRunRequest is null) { status = "Run a simulation first to use Last simulation."; Notify(); return; } startingPoint = value; PrepareChanges(); } }
    public IReadOnlyList<string> Presets { get; } = ["Baseline", "Variable Effort Example", "Defects & Rework Example"];
    private string preset = "Baseline";
    public string Preset { get => preset; set { if (value is null || !Presets.Contains(value) || busy) return; preset = value; Owner.LoadConfiguration(value switch { "Variable Effort Example" => BaselineScenario.VariableEffortExample(), "Defects & Rework Example" => BaselineScenario.DefectsAndReworkExample(), _ => BaselineScenario.CreateRequest() }); Notify(); } }
    public bool HomeVisible => page == "Home";
    public bool RunVisible => page == "Run";
    public bool ResultsVisible => page == "Results";
    public bool FlowVisible => page == "Flow";
    public bool ChangesVisible => page == "Changes";
    public bool ComparisonVisible => page == "Comparison";
    public bool ExploreVisible => page == "Explore";
    public bool AdvancedVisible => page == "Advanced";
    public bool CanEdit => !busy && !Owner.IsBusy && !Owner.Compare.IsBusy && !Explore.IsBusy;
    public bool IsBusy => !CanEdit;
    public string Status => status;
    public string CurrentDefects => starting.Quality.Enabled ? "On" : "Off";
    public IReadOnlyList<SimpleChangeField> TeamChanges { get; private set; } = [];
    public IReadOnlyList<SimpleChangeField> FlowChanges { get; private set; } = [];
    public IReadOnlyList<SimpleChangeField> CapacityChanges { get; private set; } = [];
    public IReadOnlyList<MetricRow> RunCards => SimpleResultPresentation.Run(Owner.Result);
    public IReadOnlyList<SimpleMetric> CompareCards => SimpleResultPresentation.Compare(Pair.Comparison, Pair.Alternative?.Id, Pair.BaselineQueue, Pair.AlternativeQueue);
    public string FlowObservation
    {
        get
        {
            var c = Pair.Comparison;
            var before = c?.Runs.FirstOrDefault(r => r.ScenarioSnapshot.Id == c.ExperimentSnapshot.BaselineId)?.SingleRun;
            var after = c?.Runs.FirstOrDefault(r => r.ScenarioSnapshot.Id == Pair.Alternative?.Id)?.SingleRun;
            return before is null || after is null ? "" : SimpleFlowObservation.Describe(FlowObservationValues.From(before), FlowObservationValues.From(after));
        }
    }
    public bool HasFlowObservation => FlowObservation.Length > 0;
    public bool HasExploreResults => Explore.Result is not null;
    public string ExploreTitle => Explore.Result is { } r ? $"{PresentationLabels.Label(r.Parameter)} → {PresentationLabels.Label(Explore.Metric)}" + (r.Mode == SensitivityMode.MonteCarlo ? " · typical result (median)" : "") : "";
    public string ExploreStatus => Explore.Status.StartsWith("Choose a base scenario.") ? "" : Explore.Status;
    public IReadOnlyList<CompareTextRow> MoreComparisonResults => SimpleResultPresentation.More(Pair.Comparison);
    public string FlowCaption => $"End of day {Pair.FlowDay} · same day and scale for Before and After. Move the slider to inspect another day.";
    public string ChangesTitle => Pair.ChangedParameters.Count == 0 ? "You changed nothing" : Pair.ChangedParameters.Count == 1 ? "You changed" : $"You changed {Pair.ChangedParameters.Count} things";
    public IReadOnlyList<AnalysisMetric> ExploreMetrics { get; } = [AnalysisMetric.ThroughputPerFiveDays, AnalysisMetric.AverageCycleTime, AnalysisMetric.AverageLeadTime, AnalysisMetric.AverageWip, AnalysisMetric.DeveloperUtilization, AnalysisMetric.TesterUtilization, AnalysisMetric.MaximumWaitingForTestingQueue, AnalysisMetric.MaximumWaitingForCodeReviewQueue];
    public IReadOnlyList<SensitivityParameter> ExploreParameters { get; } = [SensitivityParameter.Developers, SensitivityParameter.Testers, SensitivityParameter.DevelopmentWipLimit, SensitivityParameter.TestingWipLimit];
    public RelayCommand HomeCommand { get; }
    public RelayCommand OpenRunCommand { get; }
    public RelayCommand OpenChangesCommand { get; }
    public RelayCommand OpenExploreCommand { get; }
    public RelayCommand AdvancedCommand { get; }
    public RelayCommand ResultsCommand { get; }
    public RelayCommand FlowCommand { get; }
    public RelayCommand BackChangesCommand { get; }
    public RelayCommand AdvancedPairCommand { get; }
    public RelayCommand CancelCommand { get; }
    public AsyncCommand RunCommand { get; }
    public AsyncCommand CompareCommand { get; }
    public AsyncCommand ExploreCommand { get; }
    public SimpleWorkflowViewModel(MainWindowViewModel owner)
    {
        Owner = owner;
        OpenLiveCommand = new(() => Navigate("Live"));
        Pair = new(() => Try.CaptureSetup(), Try.LoadConfiguration, index => Navigate(index == 0 ? "Changes" : "Comparison"));
        Explore = new(() => Owner.LastRunRequest ?? Owner.CaptureSetup()) { BaseScenario = "Current Scenario form", WarmUpDays = "0" };
        HomeCommand = new(() => Navigate("Home")); OpenRunCommand = new(() => Navigate("Run"));
        OpenChangesCommand = new(() => { startingPoint = Owner.LastRunRequest is null ? "Baseline" : "Last simulation"; PrepareChanges(); Navigate("Changes"); }, () => CanEdit);
        OpenExploreCommand = new(() => Navigate("Explore")); AdvancedCommand = new(() => Navigate("Advanced"));
        ResultsCommand = new(() => Navigate("Results")); FlowCommand = new(() => Navigate("Flow")); BackChangesCommand = new(() => Navigate("Changes"));
        AdvancedPairCommand = new(() => { ShowPairInAdvanced = true; Navigate("Advanced"); });
        CancelCommand = new(Cancel); RunCommand = new(RunAsync, () => CanEdit); CompareCommand = new(RunComparisonAsync, () => CanEdit); ExploreCommand = new(ExploreAsync, () => CanEdit);
        Owner.PropertyChanged += (_, _) => Notify(); Pair.PropertyChanged += (_, _) => Notify(); Explore.PropertyChanged += (_, _) => Notify();
        PrepareChanges();
    }
    public bool ShowPairInAdvanced { get; private set; }
    public void Navigate(string destination) { if (destination != "Live") Live.Pause(); page = destination; status = ""; Notify(); }
    public void PrepareChanges()
    {
        starting = startingPoint == "Last simulation" ? Owner.LastRunRequest ?? BaselineScenario.CreateRequest() : BaselineScenario.CreateRequest();
        Try.LoadConfiguration(starting);
        TeamChanges = [new("Developers", Try.NumberOfDevelopers, () => Try.NumberOfDevelopers, v => Try.NumberOfDevelopers = v), new("Testers", Try.NumberOfTesters, () => Try.NumberOfTesters, v => Try.NumberOfTesters = v)];
        FlowChanges = [new("Development WIP", Try.DevelopmentWipLimit, () => Try.DevelopmentWipLimit, v => Try.DevelopmentWipLimit = v, "Maximum active Development items."), new("Testing WIP", Try.TestingWipLimit, () => Try.TestingWipLimit, v => Try.TestingWipLimit = v, "Maximum active Testing items.")];
        CapacityChanges = [new("Developer Capacity / day", Try.DeveloperCapacity, () => Try.DeveloperCapacity, v => Try.DeveloperCapacity = v, "Abstract work units per developer per day, not hours."), new("Tester Capacity / day", Try.TesterCapacity, () => Try.TesterCapacity, v => Try.TesterCapacity = v, "Abstract work units per tester per day, not hours.")];
        Notify();
    }
    public SimulationRequest StartingConfiguration => starting;
    public SimulationRequest AlternativeConfiguration() => Try.CaptureSetup() with { Name = starting.Name + " — Try" };
    public async Task RunAsync()
    {
        await Owner.RunAsync();
        if (Owner.HasResults) Navigate("Results"); else { status = Owner.ErrorMessage; Notify(); }
    }
    public async Task RunComparisonAsync()
    {
        if (!CanEdit) return;
        busy = true; status = "Running comparison…"; Notify();
        try
        {
            if (Pair.IsEditing) Pair.DiscardDraft();
            var before = ScenarioDefinition.Create(starting); var after = ScenarioDefinition.Create(AlternativeConfiguration());
            Pair.LoadExperiment(new(Guid.NewGuid(), "Change & Compare", "Simple before/after experiment", [before, after], before.Id, new(ExperimentRunMode.SingleRun, true, starting.RandomSeed)));
            Pair.Alternative = Pair.Scenarios.Single(s => s.Id == after.Id);
            await Pair.RunAsync(true);
            if (Pair.Comparison is { } comparison)
            {
                // Choose an informative observation, without altering or synthesizing any snapshot.
                var beforeDays = comparison.Runs[0].SingleRun!.Days; var afterDays = comparison.Runs[1].SingleRun!.Days;
                int QueueDifference(int i) => Math.Max(Math.Abs(beforeDays[i].WaitingForCodeReviewCount - afterDays[i].WaitingForCodeReviewCount), Math.Max(Math.Abs(beforeDays[i].WaitingForTestingCount - afterDays[i].WaitingForTestingCount), Math.Abs(beforeDays[i].WaitingForReworkCount - afterDays[i].WaitingForReworkCount)));
                var day = Enumerable.Range(0, Math.Min(beforeDays.Count, afterDays.Count)).OrderByDescending(QueueDifference).First();
                if (QueueDifference(day) > 0) Pair.FlowDay = day + 1;
                Navigate("Comparison");
            }
            else status = Pair.Status;
        }
        catch (Exception ex) { status = ex.Message; }
        finally { busy = false; Notify(); }
    }
    public async Task ExploreAsync() { await Explore.RunAsync(); Notify(); }
    public void Cancel() { Live.Dispose(); Owner.Cancel(); Pair.Cancel(); Explore.Cancel(); }
    private void Notify()
    {
        PropertyChanged?.Invoke(this, new(string.Empty));
        RunCommand?.Refresh(); CompareCommand?.Refresh(); ExploreCommand?.Refresh(); OpenChangesCommand?.Refresh();
    }
}
