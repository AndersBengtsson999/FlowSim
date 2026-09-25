using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Simulation.Application;

namespace Simulation.UI.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly ExperimentRunner runner = new();
    private ExperimentResult? experiment;
    private int selectedDay;
    private CancellationTokenSource? cancellation;
    private bool isBusy;
    private string statusMessage = "Ready · Configure a scenario and run the simulation.";
    private string errorMessage = "";
    private string resultTitle = "Simulation results";
    private RunMetrics? metrics;
    public event PropertyChangedEventHandler? PropertyChanged;

    // Text inputs are parsed together on Run, so incomplete edits never silently reuse old values.
    public string NumberOfDevelopers { get; set; } = "5";
    public string NumberOfTesters { get; set; } = "2";
    public string DeveloperCapacity { get; set; } = "1";
    public string TesterCapacity { get; set; } = "1";
    public string DevelopmentWipLimit { get; set; } = "5";
    public string CodeReviewWipLimit { get; set; } = "3";
    public string TestingWipLimit { get; set; } = "3";
    public string NumberOfWorkItems { get; set; } = "30";
    public string DurationDays { get; set; } = "100";
    public string DevelopmentEffort { get; set; } = "5";
    public string CodeReviewEffort { get; set; } = "1";
    public string TestingEffort { get; set; } = "2";
    public string DevelopersB { get; set; } = "5";
    public string TestersB { get; set; } = "2";
    public string DeveloperCapacityB { get; set; } = "1";
    public string TesterCapacityB { get; set; } = "1";
    public string DevelopmentWipLimitB { get; set; } = "5";
    public string CodeReviewWipLimitB { get; set; } = "3";
    public string TestingWipLimitB { get; set; } = "3";
    public bool HasResults => experiment is not null;
    public bool HasComparison => experiment?.B is not null;
    public bool IsSingleRun => HasResults;
    public IReadOnlyList<StatusPoint> HistoryA => experiment?.A.History ?? [];
    public IReadOnlyList<StatusPoint> HistoryB => experiment?.B?.History ?? [];
    public IReadOnlyList<MetricRow> ComparisonRows => BuildRows(false);
    public IReadOnlyList<MetricRow> UnfinishedRows => BuildRows(true);
    public IReadOnlyList<UnfinishedItem> OldestA => experiment?.A.OldestItems ?? [];
    public IReadOnlyList<UnfinishedItem> OldestB => experiment?.B?.OldestItems ?? [];
    public int LastDayIndex => Math.Max(0, HistoryA.Count - 1);
    public int SelectedDay
    {
        get => selectedDay;
        set
        {
            selectedDay = Math.Clamp(value, 0, LastDayIndex);
            OnPropertyChanged(); OnPropertyChanged(nameof(DayLabel));
            OnPropertyChanged(nameof(DayDetailA)); OnPropertyChanged(nameof(DayDetailB));
        }
    }
    public string DayLabel => $"End of day {selectedDay + 1}";
    public string DayDetailA => DayDetail(HistoryA);
    public string DayDetailB => DayDetail(HistoryB);
    public string HistoryNote => "End-of-day state counts. Waiting and active work are separate; both charts use the same scale.";
    public string UnfinishedNote => "At the simulation horizon. Age = days since creation; cycle age = days since Development admission. Up to 20 oldest items per scenario.";
    public bool IsBusy => isBusy;
    public bool CanEdit => !isBusy;
    public string StatusMessage => statusMessage;
    public string ErrorMessage => errorMessage;
    public bool HasError => errorMessage.Length > 0;
    public string ResultTitle => resultTitle;
    public string CompletedWorkItems => Format(metrics?.CompletedWorkItems);
    public string Throughput => Format(metrics?.ThroughputPerFiveDays, "0.000", " items/5 days");
    public string AverageLeadTime => Format(metrics?.AverageLeadTime, "0.00", " days");
    public string AverageCycleTime => Format(metrics?.AverageCycleTime, "0.00", " days");
    public string AverageWip => Format(metrics?.AverageWip);
    public string BlockedTime => Format(metrics?.BlockedTimeFraction * 100, "0.0", " %");
    public string DeveloperUtilization => Format(metrics?.DeveloperUtilization * 100, "0.0", " %");
    public string TesterUtilization => Format(metrics?.TesterUtilization * 100, "0.0", " %");
    public AsyncCommand RunCommand { get; }
    public RelayCommand CancelCommand { get; }
    public AsyncCommand CompareCommand { get; }

    public MainWindowViewModel()
    {
        RunCommand = new AsyncCommand(() => RunAsync(), () => !isBusy);
        CancelCommand = new RelayCommand(Cancel, () => isBusy);
        CompareCommand = new AsyncCommand(() => RunAsync(true), () => !isBusy);
    }

    public void Cancel() => cancellation?.Cancel();

    public async Task RunAsync(bool compare = false)
    {
        if (isBusy) return;
        isBusy = true;
        errorMessage = "";
        metrics = null;
        experiment = null;
        selectedDay = 0;
        resultTitle = (compare ? "A / B comparison" : "Scenario A") + " · deterministic v0.1";
        statusMessage = "Running…";
        NotifyAll();
        using var source = new CancellationTokenSource();
        cancellation = source;
        try
        {
            var request = ReadRequest();
            var alternative = compare ? new TeamParameters(Integer(DevelopersB, "B developers"),
                Integer(TestersB, "B testers"), Number(DeveloperCapacityB, "B developer capacity"),
                Number(TesterCapacityB, "B tester capacity"), Integer(DevelopmentWipLimitB, "B Development WIP"),
                Integer(CodeReviewWipLimitB, "B Code Review WIP"), Integer(TestingWipLimitB, "B Testing WIP")) : null;
            experiment = await Task.Run(() => runner.Run(request, alternative, source.Token), source.Token);
            metrics = experiment.A.Metrics;
            selectedDay = LastDayIndex;
            statusMessage = $"Completed {(compare ? "A / B" : "A")} · FIFO · {request.SimulationDays} working days";
        }
        catch (OperationCanceledException) { statusMessage = "Cancelled."; }
        catch (Exception ex)
        {
            errorMessage = ex is ArgumentException ? ex.Message : $"Simulation failed: {ex.Message}";
            statusMessage = "Check the scenario parameters and try again.";
        }
        finally
        {
            cancellation = null;
            isBusy = false;
            NotifyAll();
        }
    }

    private string DayDetail(IReadOnlyList<StatusPoint> history)
    {
        if (history.Count == 0) return "Run a scenario to see its flow.";
        var p = history[Math.Min(selectedDay, history.Count - 1)];
        return $"Backlog {p.Backlog:0.##} · Development {p.Development:0.##} · Waiting review {p.WaitingForCodeReview:0.##} · Review {p.CodeReview:0.##} · Waiting test {p.WaitingForTesting:0.##} · Testing {p.Testing:0.##} · Done {p.Done:0.##}";
    }

    private IReadOnlyList<MetricRow> BuildRows(bool unfinished)
    {
        if (experiment is null) return [];
        var a = experiment.A;
        var b = experiment.B;
        if (unfinished) return
        [
            Row("Unfinished items", a.Unfinished.Count, b?.Unfinished.Count),
            Row("Backlog", a.Unfinished.Backlog, b?.Unfinished.Backlog),
            Row("↳ Dependency-blocked", a.Unfinished.DependencyBlocked, b?.Unfinished.DependencyBlocked),
            Row("↳ Ready to start", a.Unfinished.ReadyBacklog, b?.Unfinished.ReadyBacklog),
            Row("Development", a.Unfinished.Development, b?.Unfinished.Development),
            Row("Waiting for Code Review", a.Unfinished.WaitingForCodeReview, b?.Unfinished.WaitingForCodeReview),
            Row("Code Review", a.Unfinished.CodeReview, b?.Unfinished.CodeReview),
            Row("Waiting for Testing", a.Unfinished.WaitingForTesting, b?.Unfinished.WaitingForTesting),
            Row("Testing", a.Unfinished.Testing, b?.Unfinished.Testing),
            Row("Average age (days)", a.Unfinished.AverageAge, b?.Unfinished.AverageAge),
            Row("Oldest age (days)", a.Unfinished.OldestAge, b?.Unfinished.OldestAge)
        ];
        return
        [
            Row("Completed items", a.Metrics.CompletedWorkItems, b?.Metrics.CompletedWorkItems),
            Row("Throughput (items/5 days)", a.Metrics.ThroughputPerFiveDays, b?.Metrics.ThroughputPerFiveDays, "0.000"),
            Row("Lead time (days, Done only)", a.Metrics.AverageLeadTime, b?.Metrics.AverageLeadTime),
            Row("Cycle time (days, Done only)", a.Metrics.AverageCycleTime, b?.Metrics.AverageCycleTime),
            Row("Average WIP (including queues)", a.Metrics.AverageWip, b?.Metrics.AverageWip),
            Row("Blocked time (%, Δ pp)", a.Metrics.BlockedTimeFraction * 100, b?.Metrics.BlockedTimeFraction * 100),
            Row("Developer use (%, Δ pp)", a.Metrics.DeveloperUtilization * 100, b?.Metrics.DeveloperUtilization * 100),
            Row("↳ Development (%, Δ pp)", a.Metrics.DevelopmentUtilization * 100, b?.Metrics.DevelopmentUtilization * 100),
            Row("↳ Code review (%, Δ pp)", a.Metrics.ReviewUtilization * 100, b?.Metrics.ReviewUtilization * 100),
            Row("Average active review WIP", a.Metrics.AverageReviewWip, b?.Metrics.AverageReviewWip),
            Row("Review time (days, exited only)", a.Metrics.AverageReviewTime, b?.Metrics.AverageReviewTime),
            Row("Tester use (%, Δ pp)", a.Metrics.TesterUtilization * 100, b?.Metrics.TesterUtilization * 100),
            Row("Unfinished items", a.Unfinished.Count, b?.Unfinished.Count)
        ];
    }
    private static MetricRow Row(string label, double a, double? b, string format = "0.00") => new(label,
        Format(a, format), Format(b, format), b is null ? "—" :
        (b.Value - a).ToString("+" + format + ";-" + format + ";" + format, CultureInfo.CurrentCulture));

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
        DevelopmentEffort = Number(DevelopmentEffort, "Development effort"),
        CodeReviewEffort = Number(CodeReviewEffort, "Code Review effort"),
        TestingEffort = Number(TestingEffort, "Testing effort")
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
        RunCommand.Refresh(); CancelCommand.Refresh();
        CompareCommand.Refresh();
    }
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed record MetricRow(string Label, string A, string B, string Difference);
