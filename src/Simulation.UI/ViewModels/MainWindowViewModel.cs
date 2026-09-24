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
    public string DeveloperCapacity { get; set; } = "2";
    public string TesterCapacity { get; set; } = "2";
    public string WipLimit { get; set; } = "8";
    public string NumberOfWorkItems { get; set; } = "50";
    public string DurationDays { get; set; } = "60";
    public string SprintLength { get; set; } = "10";
    public string ReleaseInterval { get; set; } = "10";
    public string RandomSeed { get; set; } = "42";
    public string Size { get; set; } = "5";
    public string Complexity { get; set; } = "1.5";
    public string DependencyProbability { get; set; } = "0.15";
    public string DevelopersB { get; set; } = "5";
    public string TestersB { get; set; } = "3";
    public string DeveloperCapacityB { get; set; } = "2";
    public string TesterCapacityB { get; set; } = "2";
    public string WipLimitB { get; set; } = "8";
    public bool HasResults => experiment is not null;
    public bool HasComparison => experiment?.B is not null;
    public bool IsSingleRun => experiment?.A.RunCount == 1;
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
    public string HistoryNote => experiment?.A.RunCount > 1
        ? "Mean number of items at each day end across 100 runs. Both charts use the same scale."
        : "Number of items at each day end. Both charts use the same scale.";
    public string UnfinishedNote => experiment?.A.RunCount > 1
        ? "Means across 100 runs. Oldest age is the mean of each run’s oldest unfinished item; no individual-item list is shown for batches."
        : "At the simulation horizon. Age = days since creation; cycle age = days since entering Development. Up to 20 oldest items per scenario.";
    public bool IsBusy => isBusy;
    public bool CanEdit => !isBusy;
    public string StatusMessage => statusMessage;
    public string ErrorMessage => errorMessage;
    public bool HasError => errorMessage.Length > 0;
    public string ResultTitle => resultTitle;
    public string CompletedWorkItems => Format(metrics?.CompletedWorkItems);
    public string Throughput => Format(metrics?.Throughput, "0.000", " items/day");
    public string AverageLeadTime => Format(metrics?.AverageLeadTime, "0.00", " days");
    public string AverageCycleTime => Format(metrics?.AverageCycleTime, "0.00", " days");
    public string AverageWip => Format(metrics?.AverageWip);
    public string BlockedTime => Format(metrics?.BlockedTimeFraction * 100, "0.0", " %");
    public string DeveloperUtilization => Format(metrics?.DeveloperUtilization * 100, "0.0", " %");
    public string TesterUtilization => Format(metrics?.TesterUtilization * 100, "0.0", " %");
    public AsyncCommand RunCommand { get; }
    public AsyncCommand RunBatchCommand { get; }
    public RelayCommand CancelCommand { get; }
    public AsyncCommand CompareCommand { get; }
    public AsyncCommand CompareBatchCommand { get; }

    public MainWindowViewModel()
    {
        RunCommand = new AsyncCommand(() => RunAsync(false), () => !isBusy);
        RunBatchCommand = new AsyncCommand(() => RunAsync(true), () => !isBusy);
        CancelCommand = new RelayCommand(Cancel, () => isBusy);
        CompareCommand = new AsyncCommand(() => RunAsync(false, true), () => !isBusy);
        CompareBatchCommand = new AsyncCommand(() => RunAsync(true, true), () => !isBusy);
    }

    public void Cancel() => cancellation?.Cancel();

    public async Task RunAsync(bool batch, bool compare = false)
    {
        if (isBusy) return;
        isBusy = true;
        errorMessage = "";
        metrics = null;
        experiment = null;
        selectedDay = 0;
        resultTitle = (compare ? "A / B comparison" : "Scenario A") + (batch ? " · means of 100 runs" : " · single run");
        statusMessage = "Running…";
        NotifyAll();
        using var source = new CancellationTokenSource();
        cancellation = source;
        try
        {
            var request = ReadRequest();
            var alternative = compare ? new TeamParameters(Integer(DevelopersB, "B developers"),
                Integer(TestersB, "B testers"), Number(DeveloperCapacityB, "B developer capacity"),
                Number(TesterCapacityB, "B tester capacity"), Integer(WipLimitB, "B WIP limit")) : null;
            var progress = new Progress<int>(count =>
            {
                if (!ReferenceEquals(cancellation, source)) return;
                statusMessage = $"Running {count}/{(batch ? 100 : 1)}{(compare ? " pairs" : " runs")}…";
                OnPropertyChanged(nameof(StatusMessage));
            });
            experiment = await Task.Run(() => runner.Run(request, alternative, batch ? 100 : 1, progress, source.Token), source.Token);
            metrics = experiment.A.Metrics;
            selectedDay = LastDayIndex;
            statusMessage = batch
                ? $"Completed 100 {(compare ? "pairs" : "runs")} · seeds {request.RandomSeed} through {unchecked(request.RandomSeed + 99)} · arithmetic means"
                : $"Completed {(compare ? "A / B" : "A")} · seed {request.RandomSeed} · {request.DurationDays} simulated days";
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
        return $"Backlog {p.Backlog:0.##} · Development {p.Development:0.##} · Review {p.CodeReview:0.##} · Testing {p.Testing:0.##} · Done {p.Done:0.##}";
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
            Row("Code Review", a.Unfinished.CodeReview, b?.Unfinished.CodeReview),
            Row("Testing", a.Unfinished.Testing, b?.Unfinished.Testing),
            Row("Average age (days)", a.Unfinished.AverageAge, b?.Unfinished.AverageAge),
            Row("Oldest age (days)", a.Unfinished.OldestAge, b?.Unfinished.OldestAge)
        ];
        return
        [
            Row("Completed items", a.Metrics.CompletedWorkItems, b?.Metrics.CompletedWorkItems),
            Row("Throughput (items/day)", a.Metrics.Throughput, b?.Metrics.Throughput, "0.000"),
            Row("Lead time (days, Done only)", a.Metrics.AverageLeadTime, b?.Metrics.AverageLeadTime),
            Row("Cycle time (days, Done only)", a.Metrics.AverageCycleTime, b?.Metrics.AverageCycleTime),
            Row("Average WIP", a.Metrics.AverageWip, b?.Metrics.AverageWip),
            Row("Blocked time (%, Δ pp)", a.Metrics.BlockedTimeFraction * 100, b?.Metrics.BlockedTimeFraction * 100),
            Row("Developer use (%, Δ pp)", a.Metrics.DeveloperUtilization * 100, b?.Metrics.DeveloperUtilization * 100),
            Row("Tester use (%, Δ pp)", a.Metrics.TesterUtilization * 100, b?.Metrics.TesterUtilization * 100),
            Row("Unfinished items", a.Unfinished.Count, b?.Unfinished.Count)
        ];
    }
    private static MetricRow Row(string label, double a, double? b, string format = "0.00") => new(label,
        Format(a, format), Format(b, format), b is null ? "—" :
        (b.Value - a).ToString("+" + format + ";-" + format + ";" + format, CultureInfo.CurrentCulture));

    private SimulationRequest ReadRequest() => new()
    {
        NumberOfDevelopers = Integer(NumberOfDevelopers, "Number of developers"),
        NumberOfTesters = Integer(NumberOfTesters, "Number of testers"),
        DeveloperCapacity = Number(DeveloperCapacity, "Developer capacity"),
        TesterCapacity = Number(TesterCapacity, "Tester capacity"),
        WipLimit = Integer(WipLimit, "WIP limit"),
        NumberOfWorkItems = Integer(NumberOfWorkItems, "Number of work items"),
        DurationDays = Integer(DurationDays, "Simulation duration"),
        SprintLength = Integer(SprintLength, "Sprint length"),
        ReleaseInterval = Integer(ReleaseInterval, "Release interval"),
        RandomSeed = Integer(RandomSeed, "Random seed"),
        Size = Number(Size, "Size"), Complexity = Number(Complexity, "Complexity"),
        DependencyProbability = Number(DependencyProbability, "Dependency probability")
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
        RunCommand.Refresh(); RunBatchCommand.Refresh(); CancelCommand.Refresh();
        CompareCommand.Refresh(); CompareBatchCommand.Refresh();
    }
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed record MetricRow(string Label, string A, string B, string Difference);
