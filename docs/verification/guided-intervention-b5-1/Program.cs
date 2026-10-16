using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using Simulation.Application;
using Simulation.UI.Controls;
using Simulation.UI.ViewModels;
using Simulation.UI.Views;

internal static class Program
{
    [STAThread] public static int Main(string[] args)
    {
        PreviewApp.Headless = args.Contains("--headless");
        var builder = AppBuilder.Configure<PreviewApp>();
        builder = PreviewApp.Headless ? builder.UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).UseSkia()
            : builder.UsePlatformDetect().With(new AvaloniaNativePlatformOptions { RenderingMode = [AvaloniaNativeRenderingMode.Software] });
        return builder.WithInterFont().StartWithClassicDesktopLifetime([]);
    }
}
public sealed class PreviewApp : Avalonia.Application
{
    public static bool Headless { get; set; }
    private static string Output => "docs/verification/guided-intervention-b5-1" + (Headless ? "/headless" : "");
    public override void Initialize() { Styles.Add(new FluentTheme()); RequestedThemeVariant = ThemeVariant.Light; }
    public override void OnFrameworkInitializationCompleted()
    {
        var desktop = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
        var main = new MainWindowViewModel(); var window = new MainWindow { DataContext = main, Width = 1440, Height = 1000 };
        desktop.MainWindow = window;
        window.Opened += async (_, _) =>
        {
            try { await Verify(main, window); main.Simple.Live.Dispose(); desktop.Shutdown(0); }
            catch(Exception e) { Console.Error.WriteLine(e); main.Simple.Live.Dispose(); desktop.Shutdown(1); }
        };
        base.OnFrameworkInitializationCompleted();
    }
    private static async Task Verify(MainWindowViewModel main, Window window)
    {
        var vm = main.Simple.Live;
        var live = LiveSimulation.Start(LiveSimulation.Demo with { DeveloperCount = 10, TesterCount = 2, TestingEffort = 5, NumberOfWorkItems = 100, WorkItemsPerDay = 4 });
        for(int i=0;i<100;i++) live.Step();
        vm.Load(live); main.Simple.OpenLiveCommand.Execute(null); vm.Visualization = "Game View";
        await Task.Delay(400); window.UpdateLayout();
        var game = window.GetVisualDescendants().OfType<GameView>().Single();
        var world = game.GetVisualDescendants().OfType<GameWorldControl>().Single();
        world.SelectedStation = "Testing"; await Task.Delay(100);
        var button = game.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "TestingExperiment");
        Require(button.IsVisible && button.IsEnabled, "Testing experiment action is available");
        button.Focus(); button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter }); await Task.Delay(100);
        Require(vm.GuidedPending && vm.IsEditing && !vm.IsRunning, "Existing editor opens and pauses LIVE");
        var testers = window.GetVisualDescendants().OfType<TextBox>().Single(t => t.DataContext is SimpleChangeField f && f.Label == "Testers");
        testers.Text = "3"; testers.Focus(); await Task.Delay(100);
        Require(vm.Draft.NumberOfTesters == "3", "Tester editor binding updates the proposal");
        Require(window.GetVisualDescendants().OfType<TextBlock>().Any(t=> t.Text?.Contains("Testing experiment: 2 → 3") == true), "Proposal banner refreshes while editing");
        Save(window, "guided-editor");
        vm.ApplyCommand.Execute(null); await vm.BaselineTask;
        Require(vm.Day == 100 && !vm.IsRunning, "Baseline leaves LIVE day and playback unchanged");
        for(int i=0;i<12;i++) vm.Step();
        var summary = game.GetVisualDescendants().OfType<Expander>().Single(); summary.IsExpanded = true;
        await Task.Delay(150); window.UpdateLayout();
        var liveScroll = game.GetVisualAncestors().OfType<ScrollViewer>().First();
        liveScroll.Offset = new Vector(0, 0); await Task.Delay(100);
        Require(vm.GuidedQueueSeries.All(s=>s.Points[0].Day == 100 && s.Points[^1].Day == 112), "Matched prefixes and checkpoint queue");
        Save(game, "partial-comparison"); Save(window, "partial-window");
        vm.GuidedDetailsCommand.Execute(null); await Task.Delay(100);
        var details = window.GetVisualDescendants().OfType<GuidedComparisonView>().First(v=>v.IsEffectivelyVisible);
        Require(ReferenceEquals(details.DataContext, vm), "Existing comparison page uses LIVE experiment");
        Save(details, "detailed-comparison");
        vm.GuidedReturnCommand.Execute(null);
        for(int i=0;i<8;i++) vm.Step(); await Task.Delay(100);
        Require(vm.GuidedProgress.StartsWith("Complete"), "Completion waits for all twenty LIVE days");
        Save(game, "complete-comparison");
        vm.SelectedCheckpoint = vm.Checkpoints.Last(); vm.RestoreCommand.Execute(null); await Task.Delay(100);
        Require(vm.GuidedProgress.StartsWith("Invalidated") && vm.GuidedRows.Count == 0, "Restore clears incompatible results");
        Save(game, "invalidated-comparison");
        Console.WriteLine("PASS: native guided editor, baseline independence, matched days, detailed comparison, completion and restoration.");
    }
    private static void Require(bool condition, string message) { if(!condition) throw new InvalidOperationException(message); }
    private static void Save(Control control, string name)
    {
        control.UpdateLayout(); using var bitmap = new RenderTargetBitmap(new((int)Math.Ceiling(control.Bounds.Width), (int)Math.Ceiling(control.Bounds.Height)));
        bitmap.Render(control); Directory.CreateDirectory(Path.Combine(Output,"screenshots")); bitmap.Save(Path.Combine(Output,"screenshots", name+".png"));
    }
}
