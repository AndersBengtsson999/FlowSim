using System.ComponentModel;
using System.Globalization;
using Avalonia.Threading;
using Simulation.Application;
using Simulation.Core;

namespace Simulation.UI.ViewModels;

public sealed record LiveChangeRow(int Day, string Description);
public sealed record CheckpointRow(Guid Id, string Display);
public sealed record LiveQueuePoint(int Day, int Review, int Testing, int Rework);

/// <summary>Playback timing and input presentation only; Core owns each simulated day.</summary>
public sealed class LiveViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DispatcherTimer timer;
    private readonly List<LiveQueuePoint> queueHistory = [];
    private bool running, stopped, editing;
    private double speed = 1;
    private string preset = "Live Flow Demo", status = "";
    public event PropertyChangedEventHandler? PropertyChanged;
    public MainWindowViewModel Setup { get; } = new();
    public MainWindowViewModel Draft { get; } = new();
    public LiveSimulation? Live { get; private set; }
    public IReadOnlyList<string> Presets { get; } = ["Live Flow Demo", "Baseline", "Variable Effort Example", "Defects & Rework Example"];
    public string Preset { get => preset; set { if (HasSession || !Presets.Contains(value)) return; preset = value; Setup.LoadConfiguration(value switch { "Baseline" => BaselineScenario.CreateRequest(), "Variable Effort Example" => BaselineScenario.VariableEffortExample(), "Defects & Rework Example" => BaselineScenario.DefectsAndReworkExample(), _ => LiveSimulation.Demo }); Notify(); } }
    public IReadOnlyList<string> WorkSupplyChoices { get; } = ["Fixed rate", "Always available"];
    public IReadOnlyList<string> ArrivalModes { get; } = ["Fixed rate", "Fixed Backlog", "Always available"];
    private string arrivalMode = "Continuous", draftSupply = "Continuous";
    public string ArrivalMode { get => arrivalMode; set { arrivalMode = value; Notify(); } }
    public string WorkSupply { get => arrivalMode == "Always available" ? "Always available" : "Fixed rate"; set { ArrivalMode = value == "Always available" ? value : "Continuous"; } }
    public bool FixedBacklog { get => arrivalMode == "Fixed Backlog"; set { ArrivalMode = value ? "Fixed Backlog" : "Continuous"; } }
    public bool SupplySelectorEnabled => !FixedBacklog;
    public bool ShowArrivalRate => arrivalMode == "Continuous";
    public string WorkSupplyHelp => FixedBacklog ? "Only the initial backlog is supplied." : WorkSupply == "Always available" ? "Work is available when a Development slot can pull it. WIP and capacity still constrain flow." : "Work enters at the configured average rate.";
    public string DraftSupply { get => draftSupply; set { draftSupply = value; Notify(); } }
    public bool ShowDraftArrivalRate => draftSupply is "Continuous" or "Fixed rate";
    private static WorkArrivalMode SupplyMode(string value) => value == "Always available" ? WorkArrivalMode.AlwaysAvailable : value == "Fixed Backlog" ? WorkArrivalMode.FixedBacklog : WorkArrivalMode.ContinuousArrival;
    public string ArrivalRate { get; set; } = "0.8";
    public string DraftArrivalRate { get; set; } = "0.8";
    public string ChangeLabel { get; set; } = "";
    public string CheckpointLabel { get; set; } = "";
    public string SafetyLimit { get; set; } = "10000";
    public IReadOnlyList<int> RollingWindows { get; } = [10, 20, 50, 100];
    private int rollingWindow = 20;
    public int RollingWindow { get => rollingWindow; set { if (!RollingWindows.Contains(value)) return; rollingWindow = value; if (Live is not null) Live.RollingWindow = value; RefreshPerformance(); Notify(); } }
    public IReadOnlyList<LiveTrendMetricOption> TrendMetrics { get; private set; } = LivePerformanceTrend.Metrics.Where(m => m.Metric != LiveTrendMetric.ReworkQueue).ToArray();
    public IReadOnlyList<string> TrendRanges { get; } = ["Last 10 days", "Last 20 days", "Last 50 days", "Last 100 days", "Full Session"];
    private string trendRange = "Last 20 days";
    private LiveTrendMetricOption trendMetric = LivePerformanceTrend.Metrics[0];
    public string TrendRange { get => trendRange; set { if (value == trendRange || !TrendRanges.Contains(value)) return; trendRange = value; RefreshTrend(); Notify(); } }
    public LiveTrendMetricOption TrendMetric { get => trendMetric; set { if (value is null || value == trendMetric || !TrendMetrics.Contains(value)) return; trendMetric = value; RefreshTrend(); Notify(); } }
    public LiveTrendSeries Trend { get; private set; } = new([], []);
    public string TrendDescription => $"{TrendMetric.Name} · {TrendMetric.Unit} · " + (TrendMetric.Rolling ? $"Rolling {RollingWindow} days ending on each plotted day; available days only." : "Daily observation for each completed simulated day.") + " Missing values are gaps.";
    public int SelectedTrendInterventionDay => SelectedIntervention?.Day ?? -1;
    private void RefreshTrend()
    {
        var rework = ShowRework || Live?.Session.InitialConfiguration.Quality.Enabled == true || Live?.Session.Changes.Any(c => c.After.Quality.Enabled) == true;
        if (rework != TrendMetrics.Any(m => m.Metric == LiveTrendMetric.ReworkQueue))
            TrendMetrics = LivePerformanceTrend.Metrics.Where(m => m.Metric != LiveTrendMetric.ReworkQueue || rework).ToArray();
        if (!TrendMetrics.Contains(trendMetric)) trendMetric = LivePerformanceTrend.Metrics[0];
        int? range = trendRange == "Full Session" ? null : int.Parse(trendRange.Split(' ')[1]);
        Trend = Live is null ? new([], []) : LivePerformanceTrend.Project(Live.Session, TrendMetric.Metric, RollingWindow, range);
    }
    public IReadOnlyList<double> Speeds { get; } = [0.5, 1, 2, 5, 10];
    public double Speed { get => speed; set { if (!Speeds.Contains(value)) return; speed = value; timer.Interval = TimeSpan.FromSeconds(1 / speed); Notify(); } }
    public bool HasSession => Live is not null;
    private bool setupExpanded = true;
    public bool SetupExpanded { get => setupExpanded; set { setupExpanded = value; Notify(); } }
    public string ConfigurationSummary => Live is not { } live ? "Setup / Configuration" :
        $"Configuration · {live.Session.Configuration.Team.DeveloperCount} Dev · {live.Session.Configuration.Team.TesterCount} Test · WIP {live.Session.Configuration.DevelopmentWipLimit}/{live.Session.Configuration.CodeReviewWipLimit}/{live.Session.Configuration.TestingWipLimit} · {LiveStatusProjection.Supply(live.Session.Configuration)} · Availability {live.Session.Configuration.Team.DeveloperAvailability:P0} / {live.Session.Configuration.Team.TesterAvailability:P0}";
    public string ConfigurationHeading => HasSession ? "Configuration" : "Setup / Configuration";
    public string ConfigurationValues => HasSession ? " · " + ConfigurationSummary["Configuration · ".Length..] : "";
    public string ConfigurationDetails => Live is not { } live ? "" :
        $"Productivity: Development {FlowPresentation.Productivity(live.Session.Configuration.Productivity.Development)} · Code Review {FlowPresentation.Productivity(live.Session.Configuration.Productivity.CodeReview)} · Testing {FlowPresentation.Productivity(live.Session.Configuration.Productivity.Testing)}. WIP: Development / Code Review / Testing. Rework WIP {live.Session.Configuration.Quality.ReworkWipLimit}. Defects {(live.Session.Configuration.Quality.Enabled ? "enabled" : "disabled")}. Seed {live.Session.RandomSeed}. Use Change for an intervention; it takes effect on the next day. Shortcuts {live.Session.Configuration.Debt.ShortcutRate:P0}; reduction {live.Session.Configuration.Debt.ShortcutEffortReduction:P0}; debt tolerance {live.Session.Configuration.Debt.Tolerance:P0}; repayment {live.Session.Configuration.Debt.Repayment:P0}." + $" Specialists: {live.Session.Configuration.Skills.Specialists} of {live.Session.Configuration.Team.DeveloperCount} developers; Specialist Work Rate {live.Session.Configuration.Skills.SpecialistWorkRate:P0}." + CustomCapacityNotice;
    public string CustomCapacityNotice => Live is { } live &&
        (live.Session.Configuration.Team.DeveloperCapacityPerDay != 1 || live.Session.Configuration.Team.TesterCapacityPerDay != 1)
        ? $" Advanced nominal scaling retained: Developer Capacity per Person / Day {live.Session.Configuration.Team.DeveloperCapacityPerDay:G}; Tester Capacity per Person / Day {live.Session.Configuration.Team.TesterCapacityPerDay:G}. Available capacity includes these saved factors."
        : "";
    public bool DebtVisible => Live is { } live && (live.Session.Configuration.Debt.ShortcutRate > 0 || live.Session.Configuration.Debt.Repayment > 0
        || live.Session.DebtState.Amount > 0 || live.Session.Days.Any(d => d.Debt is { Created: > 0 }));
    public DebtBarState DebtBar => DebtBarState.From(Live?.Session.DebtState ?? new(), Live?.Session.Configuration.Debt ?? new());
    public string DebtText => $"Technical Debt: {DebtBar.Ratio:P1} · Tolerance: {DebtBar.Tolerance:P1} · Development Overhead: +{DebtBar.Overhead:P1} · Repayment: {Live?.Session.Configuration.Debt.Repayment ?? 0:P0}";
    public string DebtDetails => Live is not { } live ? "" :
        $"Debt {live.Session.DebtState.Amount:0.###} effort · Developed scope {live.Session.DebtState.CumulativeDevelopmentScope:0.###}. Latest day capacity: Review {live.CurrentSnapshot.UsedReviewCapacity:0.###}, Rework {live.CurrentSnapshot.UsedReworkDeveloperCapacity:0.###}, Development {live.CurrentSnapshot.UsedDevelopmentCapacity:0.###}, Debt Work {live.CurrentSnapshot.UsedDebtRepaymentCapacity:0.###}; debt removed {live.CurrentSnapshot.Debt?.Repaid ?? 0:0.###}. Green/yellow: within configured tolerance, zero overhead. Red: excess above tolerance adds effort to future starts. Scale 0–{DebtBar.ScaleMaximum:P1}.";
    public bool SetupVisible => !HasSession;
    private bool fastAdvancing, stopFastAdvance;
    public string TargetDay { get; set; } = "100";
    public bool IsFastAdvancing => fastAdvancing;
    public bool IsRunning => running || fastAdvancing;
    public AsyncCommand RunToDayCommand { get; }
    public bool CanResume => HasSession && !running && !fastAdvancing && !editing && !stopped && !(Live?.LimitReached ?? false);
    public bool IsEditing => editing;
    public bool CanChange => HasSession && !fastAdvancing && !editing && !stopped;
    public int Day => Live?.Session.CurrentDay ?? 0;
    public LiveStatusSnapshot? LiveStatus { get; private set; }
    private static string Arrow(double? slope)
    {
        var text = LivePerformancePresentation.Trend(slope);
        return text.StartsWith("Rising") ? "↑" : text.StartsWith("Falling") ? "↓" : text.StartsWith("Stable") ? "→" : "";
    }
    private static string CapacityNumber(double? value) => value is null ? "—" : value.Value.ToString("0.###");
    public IReadOnlyList<MetricRow> StatusPrimaryGroups => LiveStatus is not { } s ? [] : [
        new("Day", s.Day.ToString(), "Completed simulated day."),
        new("Done", s.Done.ToString(), "Completed Work Items."),
        new("WIP", $"{s.Wip} {Arrow(Performance?.WipTrend)}", "Current started unfinished items. Arrow is the existing neutral OLS trend."),
        new($"Throughput · {RollingWindow}d", Performance?.AvailableDays > 0 ? $"{Performance.Throughput:0.0} / 5d" : "—", "Completions per five days in the selected rolling window."),
        new($"Cycle time · {RollingWindow}d", LivePerformancePresentation.Number(Performance?.CycleTime) + (Performance?.CycleTime is null ? "" : "d"), "Full cycle time for items completed in the selected rolling window."),
        new("Cost/Item", Performance?.DeliveryCostPerDoneItem is { } cost ? cost.ToString("0.0") : "—", LivePerformancePresentation.PeriodCostDetails(Performance))
    ];
    public IReadOnlyList<MetricRow> StatusSecondaryGroups => LiveStatus is not { } s ? [] : [
        new("Dev", $"{CapacityNumber(s.DeveloperUsed)} / {CapacityNumber(s.DeveloperAvailable)} · {LivePerformancePresentation.Percent(s.DeveloperUtilization)}", $"Day {s.Day}: used / available capacity · utilization. Normally available = people × availability. Saved advanced nominal scaling is preserved when present."),
        new("Test", $"{CapacityNumber(s.TesterUsed)} / {CapacityNumber(s.TesterAvailable)} · {LivePerformancePresentation.Percent(s.TesterUtilization)}", $"Day {s.Day}: used / available capacity · utilization. Zero available capacity means unavailable."),
        new("Queues", $"Review {s.ReviewQueue} {Arrow(Performance?.Review.Trend)} · Testing {s.TestingQueue} {Arrow(Performance?.Testing.Trend)}" + (ShowRework ? $" · Rework {s.ReworkQueue} {Arrow(Performance?.Rework.Trend)}" : ""), "Current waiting items. Arrows describe the existing rolling OLS trend, not good/bad performance."),
        new("Work", s.WorkSupply, "Current work supply for the next simulated day.")
    ];
    public string PlaybackState => fastAdvancing ? "Advancing" : running ? "Running" : stopped ? "Stopped" : HasSession ? "Paused" : "Ready";
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(status) && !status.StartsWith("Paused") && status != "Running" && !status.StartsWith("Advancing") && !status.StartsWith("Changes recorded") && status != "Ready to start.";
    public string StatusDelivery => LiveStatus is not { } s ? "" : $"Day {s.Day} · Done {s.Done} · WIP {s.Wip} {Arrow(Performance?.WipTrend)} | Recent {RollingWindow}d: Throughput {(Performance?.AvailableDays > 0 ? $"{Performance.Throughput:0.0}" : "—")} / 5d · Cycle Time {LivePerformancePresentation.Number(Performance?.CycleTime)} d";
    public string StatusCapacity => LiveStatus is not { } s ? "" : $"Day {s.Day} capacity · Dev {CapacityNumber(s.DeveloperUsed)} / {CapacityNumber(s.DeveloperAvailable)} · {LivePerformancePresentation.Percent(s.DeveloperUtilization)} | Test {CapacityNumber(s.TesterUsed)} / {CapacityNumber(s.TesterAvailable)} · {LivePerformancePresentation.Percent(s.TesterUtilization)}";
    public string StatusQueues => LiveStatus is not { } s ? "" : $"Queues · Review {s.ReviewQueue} {Arrow(Performance?.Review.Trend)} · Testing {s.TestingQueue} {Arrow(Performance?.Testing.Trend)}" + (ShowRework ? $" · Rework {s.ReworkQueue} {Arrow(Performance?.Rework.Trend)}" : "") + $" | Work: {s.WorkSupply}";
    public string LatestIntervention => Live?.Session.Changes.LastOrDefault() is { } c ? InterventionPresentation.Latest(c) : "";
    public string DayLabel => $"Day {Day}";
    public string Status => status;
    public string WindowLabel => Performance is null ? $"Last {RollingWindow} days" : $"{RollingWindow}-day window · {LivePerformancePresentation.Period(Performance)}";
    public IReadOnlyList<MetricRow> DevelopmentAllocations => FlowPresentation.DevelopmentAllocations(Live?.Session.Days.LastOrDefault());
    public IReadOnlyList<FlowStateRow> Flow { get; private set; } = [];
    private FlowStateRow? selectedFlow;
    public FlowStateRow? SelectedFlow { get => selectedFlow; set { selectedFlow = value; Notify(); } }
    public string QueueDetail
    {
        get
        {
            if (selectedFlow is null || Live is null) return "Select a queue or stage to inspect it.";
            var state = Enum.GetValues<WorkItemStatus>().First(s => Human(s) == selectedFlow.Name);
            var queue = Live.Inspect(state);
            return $"{selectedFlow.Name}: {queue.Count} items · oldest {queue.OldestDays} days in this state. " + string.Join(", ", queue.ItemIds) + (queue.Count > queue.ItemIds.Count ? " …" : "");
        }
    }
    public IReadOnlyList<LiveQueuePoint> QueueHistory => queueHistory;
    public bool ShowRework => Live is not null && (Live.Session.Configuration.Quality.Enabled || queueHistory.Any(p => p.Rework > 0) || Live.CurrentSnapshot.ReworkCount > 0);
    public IReadOnlyList<MetricRow> Metrics { get; private set; } = [];
    public PerformancePeriod? Performance { get; private set; }
    public IReadOnlyList<MetricRow> FlowMetrics { get; private set; } = [];
    public IReadOnlyList<MetricRow> CapacityMetrics { get; private set; } = [];
    public IReadOnlyList<MetricRow> QualityMetrics { get; private set; } = [];
    public bool ShowPerformanceQuality => Performance?.QualityRelevant == true || Live?.Session.Configuration.Quality.Enabled == true;
    public IReadOnlyList<ConfigurationChange> Interventions { get; private set; } = [];
    private ConfigurationChange? selectedIntervention;
    public ConfigurationChange? SelectedIntervention
    {
        get => selectedIntervention;
        set
        {
            // ItemsSource refreshes can transiently clear ComboBox selection.
            if (value is null && Interventions.Count > 0 || Equals(value, selectedIntervention)) return;
            selectedIntervention = value; RefreshComparison(); Notify();
        }
    }
    public PerformanceComparison? Comparison { get; private set; }
    public IReadOnlyList<PerformanceComparisonRow> ComparisonRows { get; private set; } = [];
    public string BeforePeriod => Comparison is null ? "" : "Before: " + LivePerformancePresentation.Period(Comparison.Before);
    public string AfterPeriod => Comparison is null ? "" : "After: " + LivePerformancePresentation.Period(Comparison.After);
    public string ComparisonStatus => Comparison is null ? "Select an intervention."
        : (!Comparison.Before.IsComplete || !Comparison.After.IsComplete ? "Incomplete periods · values use only available days. " : "Complete periods. ")
          + "Differences are After − Before; pp = percentage points. This comparison does not establish causality."
          + (Comparison.OtherInterventions > 0 ? $" {Comparison.OtherInterventions} other intervention(s) overlap these periods." : "");
    public string Observations => Comparison is null ? "" : LivePerformancePresentation.Observations(Comparison);
    public string Details { get; private set; } = "";
    public IReadOnlyList<LiveChangeRow> Changes => Live?.Session.Changes.Select(c => new LiveChangeRow(c.Day, Describe(c))).ToArray() ?? [];
    public bool HasChanges => Live?.Session.Changes.Count > 0;
    public IReadOnlyList<CheckpointRow> Checkpoints => Live?.Checkpoints.Select(c => new CheckpointRow(c.Id, $"Day {c.State.CurrentDay} — {c.Label}")).ToArray() ?? [];
    public CheckpointRow? SelectedCheckpoint { get; set; }
    public IReadOnlyList<SimpleChangeField> ChangeFields { get; private set; } = [];
    public IReadOnlyList<ChangeFieldGroup> ChangeGroups { get; private set; } = [];
    public IReadOnlyList<SimpleChangeField> AdvancedDebtChanges { get; private set; } = [];
    public string CurrentQuality => Live is null ? "" : $"Current: defects {(Live.Session.Configuration.Quality.Enabled ? "on" : "off")}; review {Live.Session.Configuration.Quality.CodeReviewDefectProbability:P0}; testing {Live.Session.Configuration.Quality.TestingDefectProbability:P0}.";
    public RelayCommand StartCommand { get; }
    public RelayCommand PauseCommand { get; }
    public RelayCommand ResumeCommand { get; }
    public RelayCommand StepCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand ChangeCommand { get; }
    public RelayCommand ApplyCommand { get; }
    public RelayCommand CancelChangeCommand { get; }
    public RelayCommand CheckpointCommand { get; }
    public RelayCommand RestoreCommand { get; }
    public RelayCommand DeleteCheckpointCommand { get; }
    public RelayCommand DetailsCommand { get; }
    public RelayCommand LimitCommand { get; }
    public LiveViewModel()
    {
        Setup.LoadConfiguration(LiveSimulation.Demo);
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => Tick();
        StartCommand = new(() => Guard(Start), () => !HasSession && !fastAdvancing);
        RunToDayCommand = new(RunToDayAsync, () => CanResume);
        PauseCommand = new(Pause, () => IsRunning);
        ResumeCommand = new(Resume, () => CanResume);
        StepCommand = new(Step, () => CanResume);
        StopCommand = new(() => { Pause(); stopped = true; status = "Stopped. Save results or Reset to start again."; Notify(); }, () => HasSession && !editing && !stopped);
        ResetCommand = new(Reset);
        ChangeCommand = new(() => Guard(BeginChange), () => CanChange);
        ApplyCommand = new(() => Guard(ApplyChanges), () => editing);
        CancelChangeCommand = new(() => { editing = false; status = "Changes discarded. Resume when ready."; Notify(); });
        CheckpointCommand = new(() => Guard(() => { Pause(); Live!.CreateCheckpoint(CheckpointLabel); status = "Checkpoint created."; Notify(); }), () => HasSession && !editing && !fastAdvancing);
        RestoreCommand = new(() => Guard(() => { Pause(); if (SelectedCheckpoint is null) throw new ArgumentException("Select a checkpoint."); Live!.RestoreCheckpoint(SelectedCheckpoint.Id); stopped = false; editing = false; RebuildHistory(); Refresh(); status = "Checkpoint restored. Resume when ready."; Notify(); }));
        DeleteCheckpointCommand = new(() => { if (SelectedCheckpoint is { } c) Live?.DeleteCheckpoint(c.Id); SelectedCheckpoint = null; Notify(); });
        DetailsCommand = new(() => { Pause(); var r = Live?.Session.GetResult(); Details = r is null ? "" : $"All-time · {r.SimulationDays} days\nLead Time {r.AverageLeadTime:0.0} · Cycle Time {r.AverageCycleTime:0.0} · Waiting Time {r.AverageWaitingTime:0.0}\nThroughput {r.ThroughputPerFiveDays:0.0} / 5 days · Average WIP {r.AverageWip:0.0}\nDefects {r.TotalDefectsFound} · Rework effort {r.TotalReworkEffort:0.0}\nDeveloper utilization {r.DeveloperUtilization:P0} · Tester utilization {r.TesterUtilization:P0}"; Notify(); });
        LimitCommand = new(() => Guard(() => { if (Live is not null) Live.SafetyLimit = Positive(SafetyLimit, "Safety Limit"); status = "Safety limit updated."; Notify(); }));
    }
    public void Start()
    {
        if (HasSession || fastAdvancing) return;
        var limit = Positive(SafetyLimit, "Safety Limit");
        var setup = Setup.CaptureSetup();
        setup = setup with { Quality = ReadQuality(Setup) };
        Live = LiveSimulation.Start(setup, SupplyMode(ArrivalMode), Rate(ArrivalRate));
        Live.SafetyLimit = limit; Live.RollingWindow = RollingWindow; setupExpanded = false;
        stopped = false; queueHistory.Clear(); Refresh(); Resume();
    }
    public void Pause() { stopFastAdvance = true; running = false; timer.Stop(); if (HasSession) status = "Paused."; Notify(); }
    /// <summary>Presentation batching only: every interval uses the existing Live Step.</summary>
    public async Task RunToDayAsync()
    {
        if (!CanResume || Live is null) return;
        var session = Live;
        if (!int.TryParse(TargetDay, out var target) || target <= Day || target > session.SafetyLimit)
        { status = $"Run to Day: enter a whole day greater than {Day} and at most {session.SafetyLimit}."; Notify(); return; }
        fastAdvancing = true; stopFastAdvance = false; timer.Stop();
        status = $"Advancing to Day {target}… Pause to stop."; Notify();
        try
        {
            while (!stopFastAdvance && ReferenceEquals(Live, session) && session.Session.CurrentDay < target)
            {
                if (!session.Step()) break;
                AppendHistory();
                if (session.Session.CurrentDay % 25 == 0)
                {
                    Refresh();
                    // Yield to the Avalonia dispatcher so Pause/navigation remain responsive.
                    await Task.Delay(1);
                }
            }
            if (ReferenceEquals(Live, session)) status = session.LimitReached ? "Live simulation safety limit reached." : $"Paused at Day {Day}.";
        }
        catch (Exception ex) { status = ex.Message; }
        finally { fastAdvancing = false; Refresh(); Notify(); }
    }
    public void Resume() { if (!CanResume) return; running = true; status = "Running"; timer.Start(); Notify(); }
    public void Tick() { if (running) Advance(); }
    public void Step() { if (!CanResume) return; Advance(); }
    private void Advance() => Guard(() =>
    {
        if (Live is null || editing || stopped) return;
        if (Live.Step()) { AppendHistory(); Refresh(); }
        if (Live.LimitReached) { Pause(); status = "Live simulation safety limit reached."; Notify(); }
    });
    public void Reset() { Pause(); Live = null; setupExpanded = true; stopped = false; editing = false; queueHistory.Clear(); Flow = []; Metrics = []; Details = ""; SelectedCheckpoint = null; selectedFlow = null; selectedIntervention = null; RefreshPerformance(); status = "Ready to start."; Notify(); }
    public void BeginChange()
    {
        if (!CanChange) return;
        Pause(); editing = true;
        var c = Live!.Session.Configuration;
        Draft.LoadConfiguration(new SimulationRequest { DeveloperCount = c.Team.DeveloperCount, TesterCount = c.Team.TesterCount,
            DeveloperCapacityPerDay = c.Team.DeveloperCapacityPerDay, TesterCapacityPerDay = c.Team.TesterCapacityPerDay,
            DeveloperAvailability = c.Team.DeveloperAvailability, TesterAvailability = c.Team.TesterAvailability,
            DevelopmentWipLimit = c.DevelopmentWipLimit, CodeReviewWipLimit = c.CodeReviewWipLimit, TestingWipLimit = c.TestingWipLimit,
            Skills = c.Skills, Productivity = c.Productivity, Debt = c.Debt, Quality = c.Quality, NumberOfWorkItems = 0 });
        draftSupply = c.ArrivalMode == WorkArrivalMode.AlwaysAvailable ? "Always available" : c.ArrivalMode == WorkArrivalMode.FixedBacklog ? "Fixed Backlog" : "Fixed rate";
        DraftArrivalRate = c.WorkItemsPerDay.ToString(CultureInfo.InvariantCulture); ChangeLabel = "";
        SimpleChangeField F(string name, Func<string> get, Action<string> set, string help = "") => new(name, get(), get, set, help, name is "Developers" or "Testers" or "Specialists" || name.EndsWith(" WIP") ? ChangeNumberKind.Integer : ChangeNumberKind.Number);
        ChangeFields = [F("Developers", () => Draft.NumberOfDevelopers, v => Draft.NumberOfDevelopers = v, FlowPresentation.DeveloperCountHelp),
            F("Specialists", () => Draft.Specialists, v => Draft.Specialists = v, "Subset of Developers; total developer capacity does not increase."),
            F("Specialist Work Rate (%)", () => Draft.SpecialistWorkRate, v => Draft.SpecialistWorkRate = v, "Future workload classifications only; existing items retain their requirement."),
            F("Testers", () => Draft.NumberOfTesters, v => Draft.NumberOfTesters = v, FlowPresentation.TesterCountHelp),
            F("Developer Availability (%)", () => Draft.DeveloperAvailability, v => Draft.DeveloperAvailability = v, FlowPresentation.DeveloperAvailabilityHelp),
            F("Tester Availability (%)", () => Draft.TesterAvailability, v => Draft.TesterAvailability = v, FlowPresentation.TesterAvailabilityHelp),
            F("Development Productivity (x)", () => Draft.DevelopmentProductivity, v => Draft.DevelopmentProductivity = v, FlowPresentation.DevelopmentProductivityHelp),
            F("Code Review Productivity (x)", () => Draft.CodeReviewProductivity, v => Draft.CodeReviewProductivity = v, FlowPresentation.CodeReviewProductivityHelp),
            F("Testing Productivity (x)", () => Draft.TestingProductivity, v => Draft.TestingProductivity = v, FlowPresentation.TestingProductivityHelp),
            F("Shortcut Rate (%)", () => Draft.ShortcutRate, v => Draft.ShortcutRate = v, "Probability at a new Development start. Existing choices stay fixed."),
            F("Shortcut Effort Reduction (%)", () => Draft.ShortcutEffortReduction, v => Draft.ShortcutEffortReduction = v, "Reduction after debt overhead; saved effort creates debt on Development completion."),
            F("Debt Tolerance (%)", () => Draft.DebtTolerance, v => Draft.DebtTolerance = v, "Ratio at or below tolerance adds no effort. Applies to future Development starts."),
            F("Debt Repayment (%)", () => Draft.DebtRepayment, v => Draft.DebtRepayment = v, "Share of developer capacity remaining after Review and Rework. Unused allocation returns to Development."),
            F("Development WIP", () => Draft.DevelopmentWipLimit, v => Draft.DevelopmentWipLimit = v),
            F("Code Review WIP", () => Draft.CodeReviewWipLimit, v => Draft.CodeReviewWipLimit = v),
            F("Testing WIP", () => Draft.TestingWipLimit, v => Draft.TestingWipLimit = v),
            F("Rework WIP", () => Draft.ReworkWipLimit, v => Draft.ReworkWipLimit = v)];
        ChangeGroups = [new("Team & Capacity", ChangeFields.Take(6).ToArray()),
            new("Productivity", ChangeFields.Skip(6).Take(3).ToArray()),
            new("Technical Debt", ChangeFields.Skip(9).Take(4).ToArray()),
            new("WIP", ChangeFields.Skip(13).ToArray())];
        AdvancedDebtChanges = [F("Debt Creation Factor (x)", () => Draft.DebtCreationFactor, v => Draft.DebtCreationFactor = v,
            "Debt per unit of shortcut effort saved. A scenario calibration assumption. Locked at Development start; existing debt and active plans stay unchanged.")];
        status = "Paused while editing. Changes apply from the next simulated day."; Notify();
    }
    public void ApplyChanges()
    {
        if (!editing || Live is null) return;
        var r = Draft.CaptureSetup();
        var quality = ReadQuality(Draft);
        var previousChanges = Live.Session.Changes.Count;
        Live.Session.ApplyChanges(Live.Session.Configuration with { Team = new(r.DeveloperCount, r.TesterCount, r.DeveloperCapacityPerDay, r.TesterCapacityPerDay) { DeveloperAvailability = r.DeveloperAvailability, TesterAvailability = r.TesterAvailability },
            DevelopmentWipLimit = r.DevelopmentWipLimit, CodeReviewWipLimit = r.CodeReviewWipLimit, TestingWipLimit = r.TestingWipLimit,
            Skills = r.Skills, Productivity = r.Productivity, Debt = r.Debt, Quality = quality, ArrivalMode = SupplyMode(DraftSupply), WorkItemsPerDay = Rate(DraftArrivalRate) }, ChangeLabel);
        if (Live.Session.Changes.Count > previousChanges) selectedIntervention = Live.Session.Changes[^1];
        editing = false; status = Live.Session.Changes.Count == previousChanges ? "No parameters changed. Resume when ready." : $"Changes recorded at Day {Day}; effective Day {Day + 1}. Resume when ready."; Refresh();
    }
    public void Load(LiveSimulation live)
    { Pause(); Live = live; setupExpanded = false; stopped = false; editing = false; rollingWindow = live.RollingWindow; SafetyLimit = live.SafetyLimit.ToString(); SelectedCheckpoint = null; selectedFlow = null; Details = ""; RebuildHistory(); Refresh(); status = "Live simulation opened. Resume when ready."; Notify(); }
    public void NotifyStatus(string message) { status = message; Notify(); }
    private void RebuildHistory() { queueHistory.Clear(); if (Live is not null) foreach (var d in Live.Session.Days) queueHistory.Add(Point(d)); }
    private static LiveQueuePoint Point(DailySnapshot d) => new(d.Day + 1, d.WaitingForCodeReviewCount, d.WaitingForTestingCount, d.WaitingForReworkCount);
    private void AppendHistory() { if (Live?.Session.Days.LastOrDefault() is { } d) queueHistory.Add(Point(d)); }
    private void Refresh()
    {
        if (Live is null) return;
        var d = Live.CurrentSnapshot;
        var expanded = Flow.Where(row => row.IsExpanded).Select(row => row.State).ToHashSet();
        Flow = FlowPresentation.Rows(d, Live.Session.Configuration, ShowRework);
        foreach (var row in Flow) row.IsExpanded = expanded.Contains(row.State);
        RefreshPerformance();
        Notify();
    }
    private void RefreshPerformance()
    {
        LiveStatus = Live is null ? null : LiveStatusProjection.From(Live.Session);
        var changes = Live?.Session.Changes ?? [];
        if (!Interventions.SequenceEqual(changes)) Interventions = changes.ToArray();
        Performance = Live is null ? null : LivePerformance.Rolling(Live.Session, RollingWindow);
        Metrics = Performance is null ? [] : LivePerformancePresentation.Delivery(Performance);
        FlowMetrics = Performance is null ? [] : LivePerformancePresentation.Flow(Performance, ShowPerformanceQuality);
        CapacityMetrics = Performance is null ? [] : LivePerformancePresentation.Capacity(Performance);
        QualityMetrics = Performance is null ? [] : LivePerformancePresentation.Quality(Performance);
        if (Live is null || selectedIntervention is null || !Live.Session.Changes.Contains(selectedIntervention))
            selectedIntervention = Live?.Session.Changes.LastOrDefault();
        RefreshComparison();
        RefreshTrend();
    }
    private void RefreshComparison()
    {
        Comparison = Live is not null && selectedIntervention is not null && Live.Session.Changes.Contains(selectedIntervention)
            ? LivePerformance.Compare(Live.Session, selectedIntervention, RollingWindow) : null;
        ComparisonRows = Comparison is null ? [] : LivePerformancePresentation.Comparison(Comparison);
    }
    private void Guard(Action action) { try { action(); } catch (Exception ex) { Pause(); status = ex.Message; Notify(); } }
    private static DefectSettings ReadQuality(MainWindowViewModel editor)
    {
        static double Probability(string text) => double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value / 100 : throw new ArgumentException("Defect probability: enter a percentage between 0 and 100.");
        return new() { Enabled = editor.DefectsEnabled, ReworkWipLimit = Positive(editor.ReworkWipLimit, "Rework WIP"),
            CodeReviewDefectProbability = Probability(editor.CodeReviewDefectProbability), TestingDefectProbability = Probability(editor.TestingDefectProbability),
            CodeReviewReworkEffortDistribution = editor.CodeReviewReworkDistribution.Read(), TestingReworkEffortDistribution = editor.TestingReworkDistribution.Read() };
    }
    private static decimal Rate(string text) => decimal.TryParse(text.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var r) ? r : throw new ArgumentException("New Work / day: enter a number.");
    private static int Positive(string text, string name) => int.TryParse(text, out var value) && value > 0 ? value : throw new ArgumentException($"{name} must be positive.");
    public static string Human(WorkItemStatus state) => state switch { WorkItemStatus.WaitingForCodeReview => "Waiting for Code Review", WorkItemStatus.WaitingForTesting => "Waiting for Testing", WorkItemStatus.CodeReview => "Code Review", WorkItemStatus.WaitingForRework => "Waiting for Rework", _ => state.ToString() };
    public static string Describe(ConfigurationChange c) =>
        (string.IsNullOrWhiteSpace(c.Label) ? "" : c.Label.Trim() + " · ") + DescribeParameters(c);
    public static string DescribeParameters(ConfigurationChange c)
    {
        var parts = new List<string>();
        void Add<T>(string label, T a, T b) { if (!Equals(a, b)) parts.Add($"{label} {a} → {b}"); }
        Add("Developers", c.Before.Team.DeveloperCount, c.After.Team.DeveloperCount); Add("Testers", c.Before.Team.TesterCount, c.After.Team.TesterCount);
        Add("Developer Capacity per Person / Day", c.Before.Team.DeveloperCapacityPerDay, c.After.Team.DeveloperCapacityPerDay); Add("Tester Capacity per Person / Day", c.Before.Team.TesterCapacityPerDay, c.After.Team.TesterCapacityPerDay);
        Add("Developer Availability", $"{c.Before.Team.DeveloperAvailability:P0}", $"{c.After.Team.DeveloperAvailability:P0}");
        Add("Tester Availability", $"{c.Before.Team.TesterAvailability:P0}", $"{c.After.Team.TesterAvailability:P0}");
        Add("Specialists", c.Before.Skills.Specialists, c.After.Skills.Specialists);
        Add("Specialist Work Rate", $"{c.Before.Skills.SpecialistWorkRate:P1}", $"{c.After.Skills.SpecialistWorkRate:P1}");
        Add("Development Productivity", FlowPresentation.Productivity(c.Before.Productivity.Development), FlowPresentation.Productivity(c.After.Productivity.Development));
        Add("Code Review Productivity", FlowPresentation.Productivity(c.Before.Productivity.CodeReview), FlowPresentation.Productivity(c.After.Productivity.CodeReview));
        Add("Testing Productivity", FlowPresentation.Productivity(c.Before.Productivity.Testing), FlowPresentation.Productivity(c.After.Productivity.Testing));
        Add("Shortcut Rate", $"{c.Before.Debt.ShortcutRate:P1}", $"{c.After.Debt.ShortcutRate:P1}");
        Add("Shortcut Effort Reduction", $"{c.Before.Debt.ShortcutEffortReduction:P1}", $"{c.After.Debt.ShortcutEffortReduction:P1}");
        Add("Debt Tolerance", $"{c.Before.Debt.Tolerance:P1}", $"{c.After.Debt.Tolerance:P1}");
        Add("Debt Repayment", $"{c.Before.Debt.Repayment:P1}", $"{c.After.Debt.Repayment:P1}");
        Add("Debt Creation Factor", c.Before.Debt.CreationFactor, c.After.Debt.CreationFactor);
        Add("Debt Impact Factor", c.Before.Debt.ImpactFactor, c.After.Debt.ImpactFactor);
        Add("Work Supply", LiveStatusProjection.Supply(c.Before), LiveStatusProjection.Supply(c.After));
        Add("Development WIP", c.Before.DevelopmentWipLimit, c.After.DevelopmentWipLimit); Add("Code Review WIP", c.Before.CodeReviewWipLimit, c.After.CodeReviewWipLimit); Add("Testing WIP", c.Before.TestingWipLimit, c.After.TestingWipLimit); Add("Rework WIP", c.Before.Quality.ReworkWipLimit, c.After.Quality.ReworkWipLimit);
        Add("Defects", c.Before.Quality.Enabled, c.After.Quality.Enabled);
        Add("Review defect probability", c.Before.Quality.CodeReviewDefectProbability, c.After.Quality.CodeReviewDefectProbability); Add("Testing defect probability", c.Before.Quality.TestingDefectProbability, c.After.Quality.TestingDefectProbability);
        string Effort(IEffortDistribution d) => d switch { FixedEffort f => $"Fixed {f.Effort:0.###}", TriangularEffort t => $"Triangular {t.Minimum:0.###} / {t.MostLikely:0.###} / {t.Maximum:0.###}", _ => "Custom" };
        Add("Review rework effort", Effort(c.Before.Quality.CodeReviewReworkEffortDistribution), Effort(c.After.Quality.CodeReviewReworkEffortDistribution)); Add("Testing rework effort", Effort(c.Before.Quality.TestingReworkEffortDistribution), Effort(c.After.Quality.TestingReworkEffortDistribution));
        return string.Join("; ", parts);
    }
    private void Notify()
    {
        PropertyChanged?.Invoke(this, new(string.Empty));
        RunToDayCommand?.Refresh(); StartCommand?.Refresh(); PauseCommand?.Refresh(); ResumeCommand?.Refresh(); StepCommand?.Refresh(); StopCommand?.Refresh(); ChangeCommand?.Refresh(); ApplyCommand?.Refresh(); CheckpointCommand?.Refresh();
    }
    public void Dispose() { stopFastAdvance = true; timer.Stop(); }
}
