using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Simulation.Infrastructure;
using Simulation.UI.ViewModels;

namespace Simulation.UI.Views;

public partial class LiveView : UserControl
{
    public LiveView() => InitializeComponent();
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
    private async void OpenSession(object? sender, RoutedEventArgs e)
    {
        Model.Pause();
        try
        {
            var files = await TopLevel.GetTopLevel(this)!.StorageProvider.OpenFilePickerAsync(new()
            { Title = "Open Live Session", AllowMultiple = false, FileTypeFilter = [new("Live simulation") { Patterns = ["*.json"] }] });
            if (files.FirstOrDefault()?.TryGetLocalPath() is { } path) await LoadFromAsync(path);
        }
        catch (Exception ex) { Model.NotifyStatus(ex.Message); }
    }
}
