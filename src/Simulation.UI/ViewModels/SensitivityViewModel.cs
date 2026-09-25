using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Simulation.Application;

namespace Simulation.UI.ViewModels;

public sealed record SensitivityTableRow(string Value, string Throughput, string Lead, string Cycle, string Waiting,
    string Wip, string Developer, string Tester, string ReviewQueue, string TestQueue, string Rework,
    string Selected, string Delta, string Percent, string P85, string P95);

public sealed class SensitivityViewModel : INotifyPropertyChanged
{
    private readonly Func<SimulationRequest> currentScenario;
    private SensitivityParameter parameter;
    private AnalysisMetric metric = AnalysisMetric.ThroughputPerFiveDays;
    private SensitivityAnalysisResult? result;
    private string status = "Choose a base scenario. Standard ranges are illustrative, not calibrated organizational data.";
    private string validationReport = "";
    private bool busy;
    private CancellationTokenSource? cancellation;
    public event PropertyChangedEventHandler? PropertyChanged;
    public IReadOnlyList<string> BaseScenarios { get; } = ["Steady Flow Validation", "Current Scenario form", "Baseline", "Variable Effort Example", "Defects & Rework Example", "Defect Stress Validation"];
    public string BaseScenario { get; set; } = "Steady Flow Validation";
    public IReadOnlyList<SensitivityParameter> Parameters { get; } = Enum.GetValues<SensitivityParameter>();
    public IReadOnlyList<SensitivityMode> Modes { get; } = Enum.GetValues<SensitivityMode>();
    public IReadOnlyList<AnalysisMetric> Metrics => result?.BasePoint.MeasurementWindow.Keys.ToArray() ?? Enum.GetValues<AnalysisMetric>();
    public SensitivityParameter Parameter
    {
        get => parameter;
        set { parameter = value; Values = string.Join(", ", SensitivityParameters.Defaults(value).Select(v => v.ToString(CultureInfo.InvariantCulture))); Changed(); Changed(nameof(Values)); }
    }
    public AnalysisMetric Metric { get => metric; set { metric = value; Changed(); Changed(nameof(Rows)); Changed(nameof(ChartLabel)); } }
    public string Values { get; set; } = "1, 2, 3, 4, 5, 6, 8, 10";
    public SensitivityMode Mode { get; set; }
    public string Runs { get; set; } = "100";
    public string Seed { get; set; } = "12345";
    public string WarmUpDays { get; set; } = "50";
    public bool CanEdit => !busy;
    public bool IsBusy => busy;
    public string Status => status;
    public string ValidationReport => validationReport;
    public SensitivityAnalysisResult? Result => result;
    public IReadOnlyList<AnalysisPoint> Points => result?.Points ?? [];
    public string ChartLabel => result is null ? "Run an analysis to display measurements." :
        $"{result.Parameter} → {Metric} · {(result.Mode == SensitivityMode.MonteCarlo ? "P50 across runs" : "single run")} · measurement [{result.WarmUpDays}, {result.BaseScenario.SimulationDays}) · base value {result.BasePoint.ParameterValue}";
    public string ResultConfiguration => result is null ? "" : AnalysisReport.Configuration(result.BaseScenario);
    public string Diagnostics => result is null ? "" : string.Join("\n\n", new[] { result.BasePoint }.Concat(result.Points).Select(p =>
        $"{result.Parameter} = {p.ParameterValue} (base={result.BasePoint.ParameterValue})\n" + string.Join("\n", p.MeasurementWindow.Select(k =>
            $"{k.Key}: full P50={AnalysisReport.Number(p.FullSimulation[k.Key].P50)}; window P50={AnalysisReport.Number(k.Value.P50)}, P85={AnalysisReport.Number(k.Value.P85)}, P95={AnalysisReport.Number(k.Value.P95)}; samples={k.Value.SampleCount}; Δ={AnalysisReport.Number(p.Deltas.GetValueOrDefault(k.Key)?.Absolute)}, Δ%={AnalysisReport.Number(p.Deltas.GetValueOrDefault(k.Key)?.Percentage)}"))));
    public IReadOnlyList<SensitivityTableRow> Rows => Points.Select(p =>
    {
        string F(AnalysisMetric m) => AnalysisReport.Number(p.MeasurementWindow.GetValueOrDefault(m)?.P50);
        var d = p.Deltas.GetValueOrDefault(Metric); var distribution = p.MeasurementWindow.GetValueOrDefault(Metric);
        return new SensitivityTableRow(AnalysisReport.Number(p.ParameterValue), F(AnalysisMetric.ThroughputPerFiveDays),
            F(AnalysisMetric.AverageLeadTime), F(AnalysisMetric.AverageCycleTime), F(AnalysisMetric.AverageWaitingTime), F(AnalysisMetric.AverageWip),
            F(AnalysisMetric.DeveloperUtilization), F(AnalysisMetric.TesterUtilization), F(AnalysisMetric.MaximumWaitingForCodeReviewQueue),
            F(AnalysisMetric.MaximumWaitingForTestingQueue), F(AnalysisMetric.TotalReworkEffort), F(Metric),
            AnalysisReport.Number(d?.Absolute), AnalysisReport.Number(d?.Percentage), AnalysisReport.Number(distribution?.P85), AnalysisReport.Number(distribution?.P95));
    }).ToArray();
    public AsyncCommand RunCommand { get; }
    public AsyncCommand ValidateCommand { get; }
    public RelayCommand CancelCommand { get; }
    public SensitivityViewModel(Func<SimulationRequest> currentScenario)
    {
        this.currentScenario = currentScenario;
        RunCommand = new(RunAsync, () => !busy);
        ValidateCommand = new(ValidateAsync, () => !busy);
        CancelCommand = new(() => cancellation?.Cancel(), () => busy);
    }
    private static int Integer(string s) => int.Parse(s, CultureInfo.InvariantCulture);
    public SensitivityAnalysisRequest ReadRequest()
    {
        var scenario = BaseScenario switch
        {
            "Current Scenario form" => currentScenario(), "Baseline" => BaselineScenario.CreateRequest(),
            "Variable Effort Example" => BaselineScenario.VariableEffortExample(),
            "Defects & Rework Example" => BaselineScenario.DefectsAndReworkExample(),
            "Defect Stress Validation" => ValidationScenarios.DefectStressBase(),
            _ => ValidationScenarios.SteadyFlow()
        };
        var values = Values.Split([',', ';', ' ', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToArray();
        return new(scenario with { RandomSeed = Integer(Seed) }, Parameter, values, Mode,
            Mode == SensitivityMode.SingleRun ? 1 : Integer(Runs), Integer(WarmUpDays));
    }
    public void Cancel() => cancellation?.Cancel();

    public Task RunAsync() => Execute(async token =>
    {
        var request = ReadRequest();
        var measured = await Task.Run(() => new SensitivityAnalysisRunner().Run(request, Progress(), token), token);
        result = measured;
        if (!Metrics.Contains(metric)) metric = AnalysisMetric.ThroughputPerFiveDays;
        foreach (var p in new[] { nameof(Result), nameof(Points), nameof(Rows), nameof(Metrics), nameof(Metric), nameof(ChartLabel), nameof(Diagnostics), nameof(ResultConfiguration) }) Changed(p);
        status = $"Completed {measured.Points.Count} points; {measured.RunsPerPoint} run(s) per point plus base. Results describe the captured configuration.";
    });
    public Task ValidateAsync() => Execute(async token =>
    {
        int seed = Integer(Seed), warmup = Integer(WarmUpDays);
        var report = await Task.Run(() => new ModelValidationRunner().Run(seed, warmup, Progress(), token), token);
        validationReport = AnalysisReport.Validation(report); Changed(nameof(ValidationReport));
        status = "All three extreme validation experiments completed. Read the report below.";
    });
    private IProgress<AnalysisProgress> Progress() => new Progress<AnalysisProgress>(p => { status = $"Running {p.CompletedRuns} / {p.TotalRuns}"; Changed(nameof(Status)); });
    private async Task Execute(Func<CancellationToken, Task> work)
    {
        if (busy) return;
        busy = true; cancellation = new(); Refresh();
        try { status = "Running analysis…"; Changed(nameof(Status)); await work(cancellation.Token); }
        catch (OperationCanceledException) { status = "Cancelled. Previous completed results retained; no partial analysis published."; }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException or Simulation.Core.ScenarioValidationException)
        { status = ex.Message; }
        finally { busy = false; cancellation.Dispose(); cancellation = null; Refresh(); Changed(nameof(Status)); }
    }
    private void Refresh() { Changed(nameof(CanEdit)); Changed(nameof(IsBusy)); RunCommand.Refresh(); ValidateCommand.Refresh(); CancelCommand.Refresh(); }
    private void Changed([CallerMemberName] string? property = null) => PropertyChanged?.Invoke(this, new(property));
}
