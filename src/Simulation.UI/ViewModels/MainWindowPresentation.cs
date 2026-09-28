using Simulation.Application;

namespace Simulation.UI.ViewModels;

public sealed partial class MainWindowViewModel
{
    private SimpleWorkflowViewModel? simple;
    public SimpleWorkflowViewModel Simple => simple ??= new(this);
    public SimulationRequest CaptureSetup() => ReadRequest();
    public bool LastRunHasDefects => lastRunRequest?.Quality.Enabled ?? false;
    public SimulationRequest? LastRunRequest => lastRunRequest;
    public void CancelSimpleWork() => simple?.Cancel();
    private SimulationRequest? lastRunRequest;
    private DateTimeOffset lastRunTime;
    public AsyncCommand RunBaselineCommand { get; }
    public RelayCommand ViewFlowCommand { get; }
    public RelayCommand DuplicateCompareCommand { get; }
    // Legacy page identifiers remain stable for navigation callbacks and restored results.
    public int MainArea { get => selectedView == 5 ? 1 : selectedView >= 3 ? 2 : 0; set { if (value != MainArea) SelectedView = value == 1 ? 5 : value == 2 ? 4 : 0; } }
    public int SimulatePage { get => selectedView < 3 ? selectedView : 0; set { if (MainArea == 0) SelectedView = value; } }
    public int AnalyzePage { get => selectedView == 3 ? 2 : selectedView == 6 ? 1 : 0; set { if (MainArea == 2) SelectedView = value == 2 ? 3 : value == 1 ? 6 : 4; } }
    public IReadOnlyList<MetricRow> PrimaryMetrics => Metrics.Where(m => PresentationLabels.Primary.Any(p => PresentationLabels.Label(p) == m.Label)).ToArray();
    public IReadOnlyList<MetricRow> AdvancedMetrics => Metrics.Except(PrimaryMetrics).ToArray();
    public IReadOnlyList<MetricRow> CapacityMetrics => result is null || lastRunRequest is null ? [] : AnalysisMetrics.Measure(lastRunRequest, result, 0).Values
        .Where(p => p.Key is AnalysisMetric.MaximumWaitingForReworkQueue or AnalysisMetric.AverageAvailableDeveloperCapacity or AnalysisMetric.AverageUsedDeveloperCapacity or AnalysisMetric.AverageAvailableTesterCapacity or AnalysisMetric.AverageUsedTesterCapacity)
        .Select(p => new MetricRow(PresentationLabels.Label(p.Key), p.Value?.ToString("0.###") ?? "n/a", "Whole-run measurement, including idle days. Queue maximum is an end-of-day observation.")).ToArray();
    private FlowStateRow? selectedFlow;
    public FlowStateRow? SelectedFlow { get => selectedFlow; set { selectedFlow = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectedFlowDetail)); } }
    public string SelectedFlowDetail => SelectedFlow is not { } s ? "Select a stage or queue to inspect it." : $"{s.Name}: {FlowStates.FirstOrDefault(r => r.Name == s.Name)?.Count ?? 0} Work Items at the end of day {SelectedDay}. {s.Kind}. Waiting queues do not use active WIP slots. Active-stage occupancy does not imply capacity was applied.";
    public string LargestQueue => result is null ? "" : QueueObservation.From(result).Description;
    public string FlowWipLimits => lastRunRequest is not { } r ? "" : $"Active WIP limits: Development {r.DevelopmentWipLimit} · Code Review {r.CodeReviewWipLimit} · Testing {r.TestingWipLimit} · Rework {r.Quality.ReworkWipLimit}";
    public async Task RunOrApplyAsync()
    {
        if (!Compare.IsEditing) { await RunAsync(); return; }
        try { Compare.ApplyDraft(); await Compare.RunAsync(false); SelectedView = 5; }
        catch (Exception ex) { errorMessage = ex.Message; NotifyAll(); }
    }
    public void DuplicateAndCompare()
    {
        if (lastRunRequest is null || result is null) return;
        try { Compare.DuplicateCompleted(lastRunRequest, result, lastRunTime); }
        catch (Exception ex) { errorMessage = ex.Message; NotifyAll(); }
    }
}
