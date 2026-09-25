using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Simulation.Application;
using Simulation.Core;

namespace Simulation.UI.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly SimulationRunner runner = new();
    private SimulationResult? result;
    private MonteCarloResult? monteCarlo;
    private bool defectsEnabled;
    private WorkItemResult? selectedWorkItem;
    private int selectedDay = 1;
    private int selectedView;
    private CancellationTokenSource? cancellation;
    private bool isBusy;
    private string statusMessage = "Configure a scenario, then run the simulation.";
    private string errorMessage = "";
    public event PropertyChangedEventHandler? PropertyChanged;

    // Inputs are parsed together on Run. Reset uses the same baseline factory as the engine demonstrations.
    public string NumberOfDevelopers { get; set; } = "";
    public string NumberOfTesters { get; set; } = "";
    public string DeveloperCapacity { get; set; } = "";
    public string TesterCapacity { get; set; } = "";
    public string DevelopmentWipLimit { get; set; } = "";
    public string CodeReviewWipLimit { get; set; } = "";
    public string TestingWipLimit { get; set; } = "";
    public string NumberOfWorkItems { get; set; } = "";
    public string DurationDays { get; set; } = "";
    public string DevelopmentEffort { get => DevelopmentDistribution.FixedValue; set => DevelopmentDistribution.FixedValue = value; }
    public string CodeReviewEffort { get => CodeReviewDistribution.FixedValue; set => CodeReviewDistribution.FixedValue = value; }
    public string TestingEffort { get => TestingDistribution.FixedValue; set => TestingDistribution.FixedValue = value; }
    public EffortEditorViewModel DevelopmentDistribution { get; } = new("Development");
    public EffortEditorViewModel CodeReviewDistribution { get; } = new("Code Review");
    public EffortEditorViewModel TestingDistribution { get; } = new("Testing");
    public IReadOnlyList<EffortEditorViewModel> EffortEditors => [DevelopmentDistribution, CodeReviewDistribution, TestingDistribution];
    public string RandomSeed { get; set; } = "";
    public string NumberOfRuns { get; set; } = "";
    public MonteCarloResult? MonteCarlo => monteCarlo;
    public bool HasMonteCarlo => monteCarlo is not null;
    public bool HasNoMonteCarlo => !HasMonteCarlo;
    public string SingleRunLabel => result is null ? "No single-run results." : $"Single run · seed {result.RandomSeed} · {result.SimulationDays} working days";
    public string MonteCarloLabel => monteCarlo is null ? "Run Monte Carlo to explore the range of model outcomes." :
        $"{monteCarlo.NumberOfRuns} runs · base seed {monteCarlo.RandomSeed} · {monteCarlo.RunsWithCompletions} runs with completed items";
    public IReadOnlyList<HistogramBin> LeadTimeHistogram => monteCarlo?.AverageLeadTime.Histogram ?? [];
    public IReadOnlyList<HistogramBin> ThroughputHistogram => monteCarlo?.ThroughputPerFiveDays.Histogram ?? [];
    public IReadOnlyList<PercentileRow> MonteCarloMetrics => monteCarlo is not { } m ? [] :
    [
        Percentiles("Completed Work Items", m.CompletedWorkItems),
        Percentiles("Throughput / 5 days", m.ThroughputPerFiveDays),
        Percentiles("Average Lead Time (working days)", m.AverageLeadTime),
        Percentiles("Average Cycle Time (working days)", m.AverageCycleTime),
        Percentiles("Average WIP", m.AverageWip),
        Percentiles("Developer Utilization", m.DeveloperUtilization, "P1"),
        Percentiles("Tester Utilization", m.TesterUtilization, "P1"),
        Percentiles("Maximum Waiting for Code Review Queue", m.MaximumWaitingForCodeReviewQueue),
        Percentiles("Maximum Waiting for Testing Queue", m.MaximumWaitingForTestingQueue)
    ];
    private static PercentileRow Percentiles(string name, MetricDistribution m, string format = "0.00") =>
        new(name, Format(m.P50, format), Format(m.P75, format), Format(m.P85, format), Format(m.P95, format), m.SampleCount);
    public bool DefectsEnabled { get => defectsEnabled; set { defectsEnabled = value; OnPropertyChanged(); } }
    public string CodeReviewDefectProbability { get; set; } = "";
    public string TestingDefectProbability { get; set; } = "";
    public string ReworkWipLimit { get; set; } = "";
    public EffortEditorViewModel CodeReviewReworkDistribution { get; } = new("Code Review Rework");
    public EffortEditorViewModel TestingReworkDistribution { get; } = new("Testing Rework");
    public IReadOnlyList<EffortEditorViewModel> ReworkEffortEditors => [CodeReviewReworkDistribution, TestingReworkDistribution];
    public int WaitingForReworkCount => SelectedSnapshot?.WaitingForReworkCount ?? 0;
    public int ReworkCount => SelectedSnapshot?.ReworkCount ?? 0;
    public IReadOnlyList<MetricRow> QualityMetrics => result is not { } r ? [] :
    [
        Metric("Total Defects Found", r.TotalDefectsFound, "Completed inspection attempts that discovered a defect; repeated discoveries count separately.", "0"),
        Metric("Code Review Defects", r.CodeReviewDefectsFound, "Defects discovered on completed Code Review attempts.", "0"),
        Metric("Testing Defects", r.TestingDefectsFound, "Defects discovered on completed Testing attempts. These return through Rework and Code Review.", "0"),
        Metric("Work Items With Defects", r.WorkItemsWithDefects, "Distinct Work Items with at least one discovered defect, including incomplete items.", "0"),
        Metric("Total Rework Count", r.TotalReworkCount, "Rework episodes admitted to the active Rework stage; includes incomplete episodes, excludes defects still waiting for admission.", "0"),
        Metric("Total Rework Effort", r.TotalReworkEffort, "Developer capacity actually consumed by Rework across all items, including incomplete items. Not merely assigned effort.", suffix: " units"),
        Metric("Average Rework Effort / Completed Item", r.AverageReworkEffortPerCompletedItem, "Actual Rework effort averaged over Done items only; 0 if none are Done.", suffix: " units"),
        Metric("Rework Developer Capacity Share", r.ReworkDeveloperCapacityShare, "Rework capacity divided by total USED developer capacity (Development + Code Review + Rework). Not divided by available capacity.", "P1"),
        Metric("Average Code Review Attempts", r.AverageCodeReviewAttempts, "Started Code Review attempts per created item, including incomplete attempts and items with zero attempts."),
        Metric("Average Testing Attempts", r.AverageTestingAttempts, "Started Testing attempts per created item, including incomplete attempts and items with zero attempts.")
    ];
    public IReadOnlyList<PercentileRow> MonteCarloQualityMetrics => monteCarlo is not { } m ? [] :
    [
        Percentiles("Total Defects Found", m.TotalDefectsFound),
        Percentiles("Work Items With Defects", m.WorkItemsWithDefects),
        Percentiles("Total Rework Effort (units)", m.TotalReworkEffort),
        Percentiles("Rework Developer Capacity Share", m.ReworkDeveloperCapacityShare, "P1")
    ];
    public WorkItemResult? SelectedWorkItem
    {
        get => selectedWorkItem;
        set { selectedWorkItem = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectedHistory)); }
    }
    public IReadOnlyList<HistoryRow> SelectedHistory => selectedWorkItem?.Events.Select(e => new HistoryRow(e.Day,
        e.EventType switch
        {
            WorkItemEventType.Transition => $"{e.FromState} → {e.ToState}",
            WorkItemEventType.CapacityApplied => $"{e.FromState}: {e.EffortApplied:0.###} capacity applied",
            WorkItemEventType.DefectFound => $"Defect found in {e.DefectSource}; {e.RequiredReworkEffort:0.###} Rework units assigned",
            _ => e.EventType.ToString()
        })).ToArray() ?? [];
    public SimulationResult? Result => result;
    public bool HasResults => result is not null;
    public bool HasNoResults => !HasResults;
    public bool IsBusy => isBusy;
    public bool CanEdit => !isBusy;
    public string StatusMessage => statusMessage;
    public string ErrorMessage => errorMessage;
    public bool HasError => errorMessage.Length > 0;
    public IReadOnlyList<DailySnapshot> Days => result?.Days ?? [];
    public IReadOnlyList<WorkItemResult> WorkItems => result?.WorkItems ?? [];
    public int LastDay => Math.Max(1, Days.Count);
    public int SelectedView { get => selectedView; set { selectedView = value; OnPropertyChanged(); } }
    // The UI is one-based; Core snapshots retain their original zero-based Day.
    public int SelectedDay
    {
        get => selectedDay;
        set
        {
            selectedDay = Math.Clamp(value, 1, LastDay);
            OnPropertyChanged(); OnPropertyChanged(nameof(SelectedSnapshot));
            OnPropertyChanged(nameof(DayLabel)); OnPropertyChanged(nameof(FlowStates));
            OnPropertyChanged(nameof(CapacityDetail));
            OnPropertyChanged(nameof(WaitingForReworkCount)); OnPropertyChanged(nameof(ReworkCount));
        }
    }
    public DailySnapshot? SelectedSnapshot => HasResults ? Days[selectedDay - 1] : null;
    public string DayLabel => HasResults ? $"End of simulated day {SelectedDay} of {LastDay}" : "Run a simulation to inspect its days.";
    public string CapacityDetail => SelectedSnapshot is not { } d ? "" :
        $"Selected day: developers {d.UsedDeveloperCapacity:0.##} / {d.AvailableDeveloperCapacity:0.##} units used; testers {d.UsedTesterCapacity:0.##} / {d.AvailableTesterCapacity:0.##} units used.";
    public IReadOnlyList<FlowStateRow> FlowStates => SelectedSnapshot is not { } d ? [] :
    [
        new("Backlog", d.BacklogCount, "Not started", "#F1F5F9", "↓"),
        new("Development", d.DevelopmentCount, "Active stage · developer capacity", "#E8F1F7", "↓"),
        new("Waiting for Code Review", d.WaitingForCodeReviewCount, "QUEUE · awaiting admission", "#FFF2D8", "↓"),
        new("Code Review", d.CodeReviewCount, "Active stage · shared developer pool", "#E8F1F7", "↓"),
        new("Waiting for Testing", d.WaitingForTestingCount, "QUEUE · awaiting admission", "#FFF2D8", "↓"),
        new("Testing", d.TestingCount, "Active stage · tester capacity", "#E8F1F7", "↓"),
        new("Done", d.DoneCount, "Completed", "#E6F2ED", "")
    ];
    public IReadOnlyList<MetricRow> Metrics => result is not { } r ? [] :
    [
        Metric("Completed Work Items", r.CompletedWorkItems, "Number of Work Items that reached Done within the simulation horizon.", "0"),
        Metric("Throughput / 5 days", r.ThroughputPerFiveDays, "Average number of Work Items completed per five simulated working days. Includes idle days in the simulation horizon.", "0.000", " items / 5 days"),
        Metric("Average Lead Time", r.AverageLeadTime, "Elapsed simulated time from when a Work Item enters the system until it reaches Done. Completed items only.", suffix: " working days"),
        Metric("Average Cycle Time", r.AverageCycleTime, "Elapsed simulated time from Development admission until Done. Completed items only.", suffix: " working days"),
        Metric("Average Active Time", r.AverageActiveTime, "Number of simulated days on which actual work capacity was applied to the Work Item. Each day counts once. Completed items only.", suffix: " working days"),
        Metric("Average Waiting Time", r.AverageWaitingTime, "Full intervals waiting for Code Review, Testing or Rework admission, after start-of-day admissions. Excludes active-stage stalls and ordinary backlog wait. Completed items only.", suffix: " working days"),
        Metric("Average Blocked Time", r.AverageBlockedTime, "Time an item could not start because dependencies were not Done. Excludes WIP-only backlog delay. Completed items only.", suffix: " working days"),
        Metric("Average WIP", r.AverageWip, "Work In Progress: started but not Done items, including all waiting queues. Average of end-of-day counts over the whole simulation.", suffix: " items"),
        Metric("Developer Utilization", r.DeveloperUtilization, "Share of all available developer capacity consumed by Development, Code Review and Rework. Includes idle days.", "P1"),
        Metric("Tester Utilization", r.TesterUtilization, "Share of all available tester capacity consumed by Testing. Includes idle days.", "P1"),
        Metric("Maximum Waiting for Code Review Queue", r.MaximumWaitingForCodeReviewQueue, "Largest end-of-day count in Waiting for Code Review. This is not a within-day peak.", "0", " items"),
        Metric("Maximum Waiting for Testing Queue", r.MaximumWaitingForTestingQueue, "Largest end-of-day count in Waiting for Testing. This is not a within-day peak.", "0", " items")
    ];
    public AsyncCommand RunCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand VariableEffortCommand { get; }
    public RelayCommand DefectsExampleCommand { get; }
    public AsyncCommand MonteCarloCommand { get; }

    public SensitivityViewModel Sensitivity { get; }

    public MainWindowViewModel()
    {
        Sensitivity = new SensitivityViewModel(ReadRequest);
        RunCommand = new AsyncCommand(RunAsync, () => !isBusy);
        CancelCommand = new RelayCommand(Cancel, () => isBusy);
        ResetCommand = new RelayCommand(ResetToBaseline, () => !isBusy);
        VariableEffortCommand = new RelayCommand(UseVariableEffortExample, () => !isBusy);
        MonteCarloCommand = new AsyncCommand(RunMonteCarloAsync, () => !isBusy);
        DefectsExampleCommand = new RelayCommand(UseDefectsExample, () => !isBusy);
        ResetToBaseline();
    }

    public void ResetToBaseline()
    {
        if (isBusy) return;
        var variable = BaselineScenario.VariableEffortExample();
        DevelopmentDistribution.Load(variable.DevelopmentDistribution!);
        CodeReviewDistribution.Load(variable.CodeReviewDistribution!);
        TestingDistribution.Load(variable.TestingDistribution!);
        var baseline = BaselineScenario.Create();
        var effort = baseline.WorkItems[0];
        NumberOfDevelopers = baseline.Team.DeveloperCount.ToString(CultureInfo.InvariantCulture);
        NumberOfTesters = baseline.Team.TesterCount.ToString(CultureInfo.InvariantCulture);
        DeveloperCapacity = baseline.Team.DeveloperCapacityPerDay.ToString(CultureInfo.InvariantCulture);
        TesterCapacity = baseline.Team.TesterCapacityPerDay.ToString(CultureInfo.InvariantCulture);
        DevelopmentWipLimit = baseline.DevelopmentWipLimit.ToString(CultureInfo.InvariantCulture);
        CodeReviewWipLimit = baseline.CodeReviewWipLimit.ToString(CultureInfo.InvariantCulture);
        TestingWipLimit = baseline.TestingWipLimit.ToString(CultureInfo.InvariantCulture);
        NumberOfWorkItems = baseline.WorkItems.Count.ToString(CultureInfo.InvariantCulture);
        DurationDays = baseline.SimulationDays.ToString(CultureInfo.InvariantCulture);
        DevelopmentEffort = effort.DevelopmentEffort.ToString(CultureInfo.InvariantCulture);
        CodeReviewEffort = effort.CodeReviewEffort.ToString(CultureInfo.InvariantCulture);
        TestingEffort = effort.TestingEffort.ToString(CultureInfo.InvariantCulture);
        DevelopmentDistribution.Load(new FixedEffort(effort.DevelopmentEffort));
        CodeReviewDistribution.Load(new FixedEffort(effort.CodeReviewEffort));
        TestingDistribution.Load(new FixedEffort(effort.TestingEffort));
        RandomSeed = baseline.RandomSeed.ToString(CultureInfo.InvariantCulture);
        NumberOfRuns = new MonteCarloRequest(BaselineScenario.CreateRequest()).NumberOfRuns.ToString(CultureInfo.InvariantCulture);
        var qualityExample = BaselineScenario.DefectsAndReworkExample().Quality;
        CodeReviewReworkDistribution.Load(qualityExample.CodeReviewReworkEffortDistribution);
        TestingReworkDistribution.Load(qualityExample.TestingReworkEffortDistribution);
        LoadQuality(baseline.Quality);
        monteCarlo = null;
        selectedWorkItem = null;
        result = null; selectedDay = 1; selectedView = 0; errorMessage = "";
        statusMessage = "Baseline restored. Run Simulation to create results.";
        NotifyAll();
    }

    public void UseVariableEffortExample()
    {
        if (isBusy) return;
        ResetToBaseline();
        var preset = BaselineScenario.VariableEffortExample();
        DevelopmentDistribution.Load(preset.DevelopmentDistribution!);
        CodeReviewDistribution.Load(preset.CodeReviewDistribution!);
        TestingDistribution.Load(preset.TestingDistribution!);
        statusMessage = "Variable Effort Example loaded. An illustration, not calibrated Easy-Laser data.";
        NotifyAll();
    }

    private void LoadQuality(DefectSettings settings)
    {
        defectsEnabled = settings.Enabled;
        CodeReviewDefectProbability = (settings.CodeReviewDefectProbability * 100).ToString(CultureInfo.InvariantCulture);
        TestingDefectProbability = (settings.TestingDefectProbability * 100).ToString(CultureInfo.InvariantCulture);
        ReworkWipLimit = settings.ReworkWipLimit.ToString(CultureInfo.InvariantCulture);
        CodeReviewReworkDistribution.Load(settings.CodeReviewReworkEffortDistribution);
        TestingReworkDistribution.Load(settings.TestingReworkEffortDistribution);
    }

    public void UseDefectsExample()
    {
        if (isBusy) return;
        UseVariableEffortExample();
        LoadQuality(BaselineScenario.DefectsAndReworkExample().Quality);
        statusMessage = "Defects & Rework Example loaded. Demonstration values, not calibrated Easy-Laser data.";
        NotifyAll();
    }

    public async Task RunMonteCarloAsync()
    {
        if (isBusy) return;
        isBusy = true; errorMessage = ""; monteCarlo = null;
        statusMessage = "Starting Monte Carlo…"; selectedView = 3;
        using var source = new CancellationTokenSource();
        cancellation = source;
        NotifyAll();
        try
        {
            var request = new MonteCarloRequest(ReadRequest(), Integer(NumberOfRuns, "Number of Runs"));
            // Progress captures the UI synchronization context; late callbacks are ignored after completion/cancel.
            var progress = new Progress<MonteCarloProgress>(p =>
            {
                if (!ReferenceEquals(cancellation, source)) return;
                statusMessage = $"Running simulation {p.CompletedRuns} / {p.TotalRuns}";
                OnPropertyChanged(nameof(StatusMessage));
            });
            monteCarlo = await Task.Run(() => new MonteCarloRunner().Run(request, progress, source.Token), source.Token);
            statusMessage = $"Monte Carlo complete · {monteCarlo.NumberOfRuns} runs · base seed {monteCarlo.RandomSeed}";
        }
        catch (OperationCanceledException) { statusMessage = "Cancelled. No partial Monte Carlo result is presented."; }
        catch (Exception ex)
        {
            errorMessage = ex is ArgumentException ? ex.Message : $"Monte Carlo failed: {ex.Message}";
            statusMessage = "Check the scenario parameters and try again.";
        }
        finally { cancellation = null; isBusy = false; NotifyAll(); }
    }

    public void Cancel() => cancellation?.Cancel();

    public async Task RunAsync()
    {
        if (isBusy) return;
        isBusy = true; errorMessage = ""; result = null; selectedWorkItem = null; selectedDay = 1;
        statusMessage = "Running…";
        NotifyAll();
        using var source = new CancellationTokenSource();
        cancellation = source;
        try
        {
            var request = ReadRequest();
            result = await Task.Run(() => runner.Run(request, source.Token), source.Token);
            selectedWorkItem = result.WorkItems.FirstOrDefault();
            selectedDay = 1;
            selectedView = 2;
            statusMessage = $"Completed · {result.SimulationDays} working days · results describe the last run.";
        }
        catch (OperationCanceledException) { statusMessage = "Cancelled."; }
        catch (Exception ex)
        {
            errorMessage = ex is ArgumentException ? ex.Message : $"Simulation failed: {ex.Message}";
            statusMessage = "Check the scenario parameters and try again.";
        }
        finally
        {
            cancellation = null; isBusy = false; NotifyAll();
        }
    }

    private static MetricRow Metric(string label, double value, string explanation, string format = "0.00", string suffix = "") =>
        new(label, Format(value, format, suffix), explanation);
    private SimulationRequest ReadRequest() => new()
    {
        DeveloperCount = Integer(NumberOfDevelopers, "Developers"),
        TesterCount = Integer(NumberOfTesters, "Testers"),
        DeveloperCapacityPerDay = Number(DeveloperCapacity, "Developer capacity"),
        TesterCapacityPerDay = Number(TesterCapacity, "Tester capacity"),
        DevelopmentWipLimit = Integer(DevelopmentWipLimit, "Development WIP"),
        CodeReviewWipLimit = Integer(CodeReviewWipLimit, "Code Review WIP"),
        TestingWipLimit = Integer(TestingWipLimit, "Testing WIP"),
        NumberOfWorkItems = Integer(NumberOfWorkItems, "Number of work items"),
        SimulationDays = Integer(DurationDays, "Simulation duration"),
        DevelopmentEffort = DevelopmentDistribution.IsFixed ? Number(DevelopmentEffort, "Development effort") : 0,
        CodeReviewEffort = CodeReviewDistribution.IsFixed ? Number(CodeReviewEffort, "Code Review effort") : 0,
        TestingEffort = TestingDistribution.IsFixed ? Number(TestingEffort, "Testing effort") : 0,
        DevelopmentDistribution = DevelopmentDistribution.Read(),
        CodeReviewDistribution = CodeReviewDistribution.Read(),
        TestingDistribution = TestingDistribution.Read(),
        RandomSeed = Integer(RandomSeed, "Random Seed"),
        Quality = !DefectsEnabled ? new DefectSettings() : new DefectSettings
        {
            Enabled = true,
            CodeReviewDefectProbability = Number(CodeReviewDefectProbability, "Code Review Defect Probability (%)") / 100,
            TestingDefectProbability = Number(TestingDefectProbability, "Testing Defect Probability (%)") / 100,
            ReworkWipLimit = Integer(ReworkWipLimit, "Rework WIP Limit"),
            CodeReviewReworkEffortDistribution = CodeReviewReworkDistribution.Read(),
            TestingReworkEffortDistribution = TestingReworkDistribution.Read()
        }
    };

    private static int Integer(string text, string label) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value : throw new ArgumentException($"{label}: enter a whole number.");
    private static double Number(string text, string label) =>
        double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && double.IsFinite(value) ? value : throw new ArgumentException($"{label}: enter a finite number.");
    private static string Format(double? value, string format = "0.00", string suffix = "") =>
        value is null ? "—" : value.Value.ToString(format, CultureInfo.CurrentCulture) + suffix;
    private void NotifyAll()
    {
        OnPropertyChanged(string.Empty);
        RunCommand.Refresh(); CancelCommand.Refresh(); ResetCommand.Refresh(); VariableEffortCommand.Refresh(); MonteCarloCommand.Refresh(); DefectsExampleCommand.Refresh();
    }
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed record MetricRow(string Label, string Value, string Explanation);
public sealed record FlowStateRow(string Name, int Count, string Kind, string Background, string Arrow);

public sealed record PercentileRow(string Name, string P50, string P75, string P85, string P95, int SampleCount);

public sealed record HistoryRow(int Day, string Description);
