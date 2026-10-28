using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using Simulation.Application;
using Simulation.Infrastructure;
using Simulation.UI;
using Simulation.UI.ViewModels;
using Simulation.UI.Views;

internal static class Program
{
    [STAThread] public static int Main() => AppBuilder.Configure<PreviewApp>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).UseSkia()
        .WithInterFont().StartWithClassicDesktopLifetime([]);
}
public sealed class PreviewApp : Avalonia.Application
{
    private const string Output = "docs/verification/flow-1-0-polish/screenshots";
    public override void Initialize()
    {
        Resources.MergedDictionaries.Add(new Avalonia.Markup.Xaml.Styling.ResourceInclude(new Uri("avares://Simulation.UI/")) { Source = new("avares://Simulation.UI/Styles/FlowColors.axaml") });
        Styles.Add(new FluentTheme());
        Styles.Add(new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://Simulation.UI/")) { Source = new("avares://Simulation.UI/Styles/FlowControls.axaml") });
        RequestedThemeVariant = ThemeVariant.Dark;
    }
    public override void OnFrameworkInitializationCompleted()
    {
        var desktop = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
        var main = new MainWindowViewModel(); var window = new MainWindow { DataContext = main, Width = 1280, Height = 900 };
        desktop.MainWindow = window;
        window.Opened += async (_, _) =>
        {
            try { await Verify(main, window, desktop); main.Simple.Live.Dispose(); desktop.Shutdown(0); }
            catch (Exception ex) { Console.Error.WriteLine(ex); main.Simple.Live.Dispose(); desktop.Shutdown(1); }
        };
        base.OnFrameworkInitializationCompleted();
    }
    private static async Task Settle(Window window)
    { await Task.Delay(120); window.UpdateLayout(); await Task.Delay(80); window.UpdateLayout(); }
    private static void Require(bool value, string name)
    { if (!value) throw new InvalidOperationException(name); Console.WriteLine("PASS: " + name); }
    private static Button Button(Control control, string name) => control.GetVisualDescendants().OfType<Button>().Single(b => b.Name == name);
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
    private static void Save(Control control, string name)
    {
        control.UpdateLayout(); Directory.CreateDirectory(Output);
        using var bitmap = new RenderTargetBitmap(new((int)Math.Ceiling(control.Bounds.Width), (int)Math.Ceiling(control.Bounds.Height)));
        bitmap.Render(control); bitmap.Save(Path.Combine(Output, name + ".png"));
    }
    private static async Task Verify(MainWindowViewModel main, MainWindow window, IClassicDesktopStyleApplicationLifetime desktop)
    {
        var vm = main.Simple.Live; await Settle(window);
        var view = window.GetVisualDescendants().OfType<LiveView>().Single();
        Require(window.Title == "FLOW", "Application is named FLOW");
        Require(Button(view, "OpenLiveSessionButton").IsEffectivelyVisible && Button(view, "OpenLiveSessionButton").IsEnabled, "Open available before Start");
        Require(!Button(view, "SaveLiveSessionButton").IsEnabled, "Save disabled before session");
        Save(window, "before-start");
        await view.OpenSessionFromPickerAsync(() => Task.FromResult<string?>(null));
        Require(!vm.HasSession, "Cancel opening before Start creates no session");
        var folder = Path.Combine(Path.GetTempPath(), "flow-polish-" + Guid.NewGuid()); Directory.CreateDirectory(folder);
        try
        {
            var file = Path.Combine(folder, "live.json"); var invalid = Path.Combine(folder, "invalid.json");
            var source = LiveSimulation.Start(new() { NumberOfWorkItems=80, DeveloperCount=6, TesterCount=1, DevelopmentEffort=1, CodeReviewEffort=1, TestingEffort=5, DevelopmentWipLimit=20, CodeReviewWipLimit=20, TestingWipLimit=6 }, Simulation.Core.WorkArrivalMode.ContinuousArrival, 3);
            for (var i = 0; i < 35; i++) source.Step(); source.CreateCheckpoint("Keep");
            await LiveSessionJson.SaveAsync(file, source);
            await view.OpenSessionFromPickerAsync(() => Task.FromResult<string?>(file));
            Require(LiveSessionJson.Save(source) == LiveSessionJson.Save(vm.Live!), "Open before Start restores complete session");
            var same = vm.Live; var before = LiveSessionJson.Save(vm.Live!);
            await view.OpenSessionFromPickerAsync(() => Task.FromResult<string?>(null));
            Require(ReferenceEquals(same, vm.Live) && before == LiveSessionJson.Save(vm.Live!), "Cancelled Open preserves active session");
            await File.WriteAllTextAsync(invalid, "not JSON");
            try { await view.LoadFromAsync(invalid); throw new Exception("Invalid load unexpectedly succeeded"); }
            catch (System.Text.Json.JsonException) { }
            Require(ReferenceEquals(same, vm.Live) && before == LiveSessionJson.Save(vm.Live!), "Failed Open preserves active session");
            vm.Resume(); await view.SaveToAsync(file);
            Require(!vm.IsRunning && LiveSessionJson.Save(await LiveSessionJson.LoadAsync(file)) == before, "Save while running pauses and round-trips complete session");
            vm.Visualization = "Game View"; await Settle(window);
            Require(Button(view,"SaveLiveSessionButton").IsEffectivelyVisible && Button(view,"SaveLiveSessionButton").IsEnabled, "Save discoverable during LIVE");
            Require(Button(view,"OpenLiveSessionButton").IsEffectivelyVisible, "Open discoverable during LIVE");
            Save(window, "live"); Save(view.FindControl<WrapPanel>("LiveToolbar")!, "live-session-controls");
            view.OpenInsights(); await Settle(view.CurrentInsightsWindow!); Save(view.CurrentInsightsWindow!, "simulation-insights"); view.CurrentInsightsWindow!.Close();
            vm.Resume(); before = LiveSessionJson.Save(vm.Live!); var status = vm.Status;
            var pending = vm.RequestResetAsync();
            var dialog = desktop.Windows.Single(w => w.Title == "Reset LIVE session?"); await Settle(dialog);
            vm.Tick(); Require(before == LiveSessionJson.Save(vm.Live!), "Modal Reset freezes timeline");
            Save(dialog, "reset-confirmation"); Click(Button(dialog,"CancelReset")); await pending;
            Require(vm.IsRunning && vm.Status == status && before == LiveSessionJson.Save(vm.Live!), "Dialog Cancel preserves timeline, checkpoints and playback");
            vm.Pause();
            pending = vm.RequestResetAsync(); dialog = desktop.Windows.Single(w => w.Title == "Reset LIVE session?"); dialog.Close(); await pending;
            Require(before == LiveSessionJson.Save(vm.Live!), "Title-bar dismissal cancels Reset");
            pending = vm.RequestResetAsync(); dialog = desktop.Windows.Single(w => w.Title == "Reset LIVE session?"); await Settle(dialog);
            Click(Button(dialog,"ConfirmReset")); await pending;
            Require(!vm.HasSession && vm.Checkpoints.Count == 0 && !vm.IsRunning, "Confirmed dialog clears timeline/checkpoints");
            Require(File.Exists(file), "Reset keeps saved file");
            window.AboutCommand.Execute(null); var about = desktop.Windows.Single(w => w.Title == "About FLOW"); await Settle(about);
            var text = about.GetVisualDescendants().OfType<SelectableTextBlock>().Single().Text!;
            Require(text == ProductIdentity.Description && text.Contains(ProductIdentity.Version) && text.Contains(ProductIdentity.Commit), "About shows actual embedded version/commit");
            Save(about, "about"); about.Close();
        }
        finally { Directory.Delete(folder, true); }
    }
}
