using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Simulation.Infrastructure;
using Simulation.UI.ViewModels;

namespace Simulation.UI.Views;

public partial class CompareView : UserControl
{
    public CompareView() => InitializeComponent();
    private CompareViewModel Model => (CompareViewModel)DataContext!;
    private async Task<string?> Pick(bool save, string title, string extension)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider ?? throw new InvalidOperationException("File picker unavailable.");
        var type = new FilePickerFileType(extension.ToUpperInvariant()) { Patterns = ["*." + extension] };
        if (save)
        {
            var file = await storage.SaveFilePickerAsync(new() { Title = title, SuggestedFileName = "simulation-" + extension + "." + extension, DefaultExtension = extension, FileTypeChoices = [type], ShowOverwritePrompt = true });
            return file?.TryGetLocalPath();
        }
        var files = await storage.OpenFilePickerAsync(new() { Title = title, AllowMultiple = false, FileTypeFilter = [type] });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }
    private async Task FileAction(Func<Task> action)
    { try { await action(); } catch (Exception ex) { Model.NotifyFileStatus(ex.Message); } }
    public async Task SaveExperimentToAsync(string path)
    { await ExperimentJson.WriteAsync(path, ExperimentJson.SaveExperiment(Model.CaptureExperiment())); Model.NotifyFileStatus("Experiment configuration saved. Results remain in memory; export CSV for a result snapshot."); }
    public async Task LoadExperimentFromAsync(string path)
    { Model.LoadExperiment(ExperimentJson.LoadExperiment(await ExperimentJson.ReadAsync(path))); }
    public async Task ExportToAsync(string path)
    {
        Model.CaptureExperiment();
        var comparison = Model.Session.Compare(Model.Scenarios.Where(s => s.Included).Select(s => s.Id));
        await ExperimentJson.WriteAsync(path, ComparisonCsv.Export(comparison)); Model.NotifyFileStatus("Comparison exported with configuration snapshots, run provenance and signed paired deltas.");
    }
    private async void SaveScenario(object? sender, RoutedEventArgs args) => await FileAction(async () =>
    {
        Model.CaptureExperiment(); var selected = Model.Selected ?? throw new ArgumentException("Select a scenario.");
        var text = ExperimentJson.SaveScenario(selected.Scenario);
        if (await Pick(true, "Save Scenario", "json") is { } path) { await ExperimentJson.WriteAsync(path, text); Model.NotifyFileStatus("Scenario saved."); }
    });
    private async void LoadScenario(object? sender, RoutedEventArgs args) => await FileAction(async () =>
    { if (await Pick(false, "Load Scenario", "json") is { } path) { Model.ImportScenario(ExperimentJson.LoadScenario(await ExperimentJson.ReadAsync(path))); Model.NotifyFileStatus("Scenario imported with a new collection identity. Run to create a result."); } });
    private async void SaveExperiment(object? sender, RoutedEventArgs args) => await FileAction(async () =>
    { if (await Pick(true, "Save Experiment", "json") is { } path) await SaveExperimentToAsync(path); });
    private async void LoadExperiment(object? sender, RoutedEventArgs args) => await FileAction(async () =>
    { if (await Pick(false, "Load Experiment", "json") is { } path) await LoadExperimentFromAsync(path); });
    private async void ExportCsv(object? sender, RoutedEventArgs args) => await FileAction(async () =>
    { if (await Pick(true, "Export Comparison", "csv") is { } path) await ExportToAsync(path); });
}
