using Avalonia.Controls;
using Avalonia.VisualTree;
using Simulation.UI.Insights;
using Simulation.UI.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Simulation.Infrastructure;
using Simulation.UI.ViewModels;

namespace Simulation.UI.Views;

public partial class LiveView : UserControl
{
    public InsightsWindow? CurrentInsightsWindow { get; private set; }
    private void OpenInsightsClicked(object? sender,RoutedEventArgs e)=>OpenInsights();
    public void OpenInsights()
    {
        if(CurrentInsightsWindow is not null)
        {
            if(CurrentInsightsWindow.WindowState==WindowState.Minimized)CurrentInsightsWindow.WindowState=WindowState.Normal;
            CurrentInsightsWindow.Activate();return;
        }
        if(DataContext is not LiveViewModel model || TopLevel.GetTopLevel(this) is not Window owner)return;
        var window=new InsightsWindow(model,OpenInsightStation,OpenInsightKpi);
        if(owner is MainWindow main && OperatingSystem.IsMacOS())window.KeyBindings.Add(new(){Gesture=main.ExitGesture,Command=main.ExitCommand});
        CurrentInsightsWindow=window;window.Closed+=(_,_)=>CurrentInsightsWindow=null;window.Show(owner);
    }
    public void OpenInsightStation(string name)
    {
        Model.Visualization="Game View";
        var world=this.GetVisualDescendants().OfType<GameWorldControl>().FirstOrDefault();
        if(world is not null) { world.SelectedStation=name;world.BringIntoView(); }
        (TopLevel.GetTopLevel(this) as Window)?.Activate();
    }
    public void OpenInsightKpi(Simulation.Application.TeamKpi metric)
    {
        Model.OpenExplorer(metric);
        var panels=this.GetVisualDescendants().OfType<KpiDashboardView>();
        (panels.FirstOrDefault(p=>p.IsEffectivelyVisible)??panels.FirstOrDefault())?.OpenExpandedExplorer();
    }
    private LiveViewModel? observed;
    private bool guidedEditorShown;
    public LiveView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            CurrentInsightsWindow?.Close();
            if (observed is not null) { observed.PropertyChanged -= ModelChanged; observed.ConfirmResetAsync = () => Task.FromResult(false); }
            observed = DataContext as LiveViewModel;
            if (observed is not null)
            {
                observed.PropertyChanged += ModelChanged;
                observed.ConfirmResetAsync = () => TopLevel.GetTopLevel(this) is Window owner
                    ? ProductDialogs.ConfirmReset(owner) : Task.FromResult(false);
            }
        };
    }
    private void ModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var show = observed?.GuidedPending == true && observed.IsEditing;
        if (show && !guidedEditorShown)
            Avalonia.Threading.Dispatcher.UIThread.Post(() => ChangeEditor.BringIntoView(), Avalonia.Threading.DispatcherPriority.Loaded);
        guidedEditorShown = show;
    }
    private LiveViewModel Model => (LiveViewModel)DataContext!;
    public async Task SaveToAsync(string path)
    {
        Model.Pause();
        if (Model.Live is null) throw new InvalidOperationException("Start a Live simulation first.");
        await LiveSessionJson.SaveAsync(path, Model.Live); Model.NotifyStatus("Live simulation saved.");
    }
    public async Task LoadFromAsync(string path)
    { Model.Pause(); Model.Load(await LiveSessionJson.LoadAsync(path)); }
    private async void SaveSession(object? sender, RoutedEventArgs e)
    {
        Model.Pause();
        try
        {
            var file = await TopLevel.GetTopLevel(this)!.StorageProvider.SaveFilePickerAsync(new()
            { Title = "Save Live Session", SuggestedFileName = "live-simulation.json", DefaultExtension = "json", ShowOverwritePrompt = true });
            if (file?.TryGetLocalPath() is { } path) await SaveToAsync(path);
        }
        catch (Exception ex) { Model.NotifyStatus(ex.Message); }
    }
    public async Task OpenSessionFromPickerAsync(Func<Task<string?>> selectPath)
    {
        Model.Pause();
        if (await selectPath() is { } path) await LoadFromAsync(path);
    }
    private async void OpenSession(object? sender, RoutedEventArgs e)
    {
        try
        {
            await OpenSessionFromPickerAsync(async () =>
            {
                var files = await TopLevel.GetTopLevel(this)!.StorageProvider.OpenFilePickerAsync(new()
                { Title = "Open Live Session", AllowMultiple = false, FileTypeFilter = [new("Live simulation") { Patterns = ["*.json"] }] });
                return files.FirstOrDefault()?.TryGetLocalPath();
            });
        }
        catch (Exception ex) { Model.NotifyStatus(ex.Message); }
    }
}
