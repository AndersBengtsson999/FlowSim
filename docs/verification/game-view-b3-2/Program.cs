using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using Simulation.Application;
using Simulation.Core;
using Simulation.UI.Controls;
using Simulation.UI.Controls.GameWorld;
using Simulation.UI.ViewModels;
using Simulation.UI.Views;

internal static class Program
{
    [STAThread] public static int Main() => AppBuilder.Configure<PreviewApp>().UsePlatformDetect()
        .With(new AvaloniaNativePlatformOptions { RenderingMode = [AvaloniaNativeRenderingMode.Software] })
        .WithInterFont().StartWithClassicDesktopLifetime([]);
}
public sealed class PreviewApp : Avalonia.Application
{
    private const string Output = "docs/verification/game-view-b3-2";
    public override void Initialize() { Styles.Add(new FluentTheme()); RequestedThemeVariant = ThemeVariant.Light; }
    public override void OnFrameworkInitializationCompleted()
    {
        var desktop = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
        var main = new MainWindowViewModel(); var window = new MainWindow { DataContext = main, Width = 1440, Height = 1060 };
        desktop.MainWindow = window;
        window.Opened += async (_, _) =>
        {
            try { await Verify(main, window); main.Simple.Live.Dispose(); desktop.Shutdown(0); }
            catch (Exception e) { Console.Error.WriteLine(e); main.Simple.Live.Dispose(); desktop.Shutdown(1); }
        };
        base.OnFrameworkInitializationCompleted();
    }
    private static async Task Verify(MainWindowViewModel main, Window window)
    {
        var vm = main.Simple.Live;
        var request = BaselineScenario.CreateRequest() with { NumberOfWorkItems = 38, DeveloperCount = 5, TesterCount = 0, DevelopmentEffort = 1,
            CodeReviewEffort = 1, TestingEffort = 2, DevelopmentWipLimit = 20, CodeReviewWipLimit = 20, TestingWipLimit = 3 };
        LiveSimulation Run(SimulationRequest r, int days, WorkArrivalMode mode = WorkArrivalMode.FixedBacklog)
        { var live = LiveSimulation.Start(r, mode, r.WorkItemsPerDay); for (var i = 0; i < days; i++) live.Step(); return live; }
        var blocked = Run(request, 100); blocked.CreateCheckpoint("Before recovery"); var saved = blocked.Capture();
        vm.Load(blocked); main.Simple.OpenLiveCommand.Execute(null); vm.Visualization = "Game View"; await Task.Delay(350);
        var game = window.GetVisualDescendants().OfType<GameView>().Single();
        var world = game.GetVisualDescendants().OfType<GameWorldControl>().Single();
        var panel = game.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "StationPanel");
        var picker = game.GetVisualDescendants().OfType<ComboBox>().Single(b => b.Name == "StationPicker");
        var close = game.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "CloseStation");
        var before = JsonSerializer.Serialize(vm.Live!.Capture());
        window.Activate(); world.InvalidateVisual(); await Task.Delay(250);
        PointerSelect(window, world, new(710, 404)); await Task.Delay(100);
        Require(world.SelectedStation == "Testing" && panel.IsVisible && world.Details.Rows.Any(r => r.Label == "Waiting" && r.Value == "35"), "Pointer opens exact Testing details");
        Save(game, "testing-selected"); Save(window, "testing-window");
        PointerSelect(window, world, new(555, 165)); await Task.Delay(100);
        Require(world.Details.Title == "Development", "Switch station"); Save(game, "development-selected");
        close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Require(!world.HasSelection && world.IsFocused, "Explicit close and focus return");
        world.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Home });
        Require(world.SelectedStation == "Backlog", "Keyboard Home opens first station");
        world.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right }); Require(world.SelectedStation == "Development", "Arrow selects next station");
        world.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape }); Require(!world.HasSelection, "Escape closes");
        picker.SelectedItem = "Technical Debt"; Require(world.Details.Title == "Technical Debt", "Keyboard-accessible picker is bound");
        PointerSelect(window, world, new(20, 20)); Require(!world.HasSelection, "Outside station closes");
        Require(before == JsonSerializer.Serialize(vm.Live.Capture()), "All selection routes preserve history/checkpoints and state");
        foreach (var size in new[] { new Size(960, 820), new Size(1440, 1060), new Size(1840, 1120) })
        {
            window.Width = size.Width; window.Height = size.Height; await Task.Delay(500); window.UpdateLayout(); world.InvalidateVisual(); await Task.Delay(150);
            PointerSelect(window, world, new(670, 471)); await Task.Delay(120);
            Require(world.SelectedStation == "Testing", "Scaled queue hit aligned");
            var origin = world.TranslatePoint(default, game)!.Value; var corner = world.TranslatePoint(new(1080, 620), game)!.Value;
            var sx = (corner.X - origin.X) / 1080; var sy = (corner.Y - origin.Y) / 620;
            Require(Math.Abs(sx - sy) < .0001 && sx > 0, "Uniform fit, no sprite distortion");
            Require(corner.X <= game.Bounds.Width + 1 && origin.X >= -1, "World fits width");
            var liveScroll = window.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "LiveScroll");
            var worldBottom = world.TranslatePoint(new(1080, 620), window)!.Value.Y;
            var scrollBottom = liveScroll.TranslatePoint(new(0, liveScroll.Bounds.Height), window)!.Value.Y;
            Require(worldBottom <= scrollBottom + 1, "Entire workshop fits actual LIVE viewport");
            Console.WriteLine($"Window {size}: world scale {sx:0.000}, world height {corner.Y - origin.Y:0}, panel {panel.Bounds}");
            Save(window, "window-" + size.Width);
        }
        // Live values update with the same selected station; configuration and observed capacity remain separate.
        vm.BeginChange(); vm.Draft.NumberOfTesters = "5"; vm.ChangeLabel = "Recovery"; vm.ApplyChanges(); vm.Step();
        Require(world.SelectedStation == "Testing" && world.Details.Rows.Any(r => r.Label == "Testers · configured" && r.Value == "5"), "Selection survives LIVE updates");
        Require(world.Details.Rows.Any(r => r.Label == "Tester used / available · last day" && r.Value.EndsWith(" / 5")), "New completed-day capacity mapped");
        vm.Load(LiveSimulation.Restore(saved)); Require(world.SelectedStation == "Testing" && world.Details.Rows.Any(r => r.Value == "No testers"), "Restore refreshes selected panel immediately");
        foreach (var enabled in new[] { false, true })
        {
            vm.AnimateGameActivity = enabled; PointerSelect(window, world, new(555, 165)); Require(world.Details.Title == "Development", "Selection independent of motion toggle");
        }
        vm.GameRenderingMode = "Legacy vectors"; PointerSelect(window, world, new(680, 390)); Require(world.Details.Title == "Testing", "Legacy remains selectable"); vm.GameRenderingMode = "Sprites";
        vm.Speed = .5; vm.Resume(); await Task.Delay(80);
        var runningBefore = JsonSerializer.Serialize(vm.Live!.Capture());
        for (var i = 0; i < 14; i++) picker.SelectedItem = StationMap.Names[i % 7];
        Require(vm.IsRunning && world.IsAnimationRunning, "Selection does not pause shared animation or LIVE");
        Require(runningBefore == JsonSerializer.Serialize(vm.Live.Capture()), "Running selection causes no simulation event"); vm.Pause();
        vm.Speed = 10; vm.TargetDay = (vm.Day + 10).ToString(); await vm.RunToDayAsync(); Require(world.SelectedStation == "Technical Debt", "Fast advance keeps selection");
        var review = Run(request with { NumberOfWorkItems = 200, TesterCount = 5, CodeReviewEffort = 30, CodeReviewWipLimit = 1 }, 20);
        vm.Load(review); picker.SelectedItem = "Code Review"; await Task.Delay(100); Require(world.Details.Rows.Any(r => r.Value == "Shared Dev capacity busy"), "Review reports shared capacity correctly"); Save(game, "review-selected");
        var release = Run(request with { TesterCount = 5, Release = new(ReleaseMode.Scheduled, 5, 100) }, 30);
        vm.Load(release); picker.SelectedItem = "Ready for Release"; await Task.Delay(100); Require(world.Details.Rows.Any(r => r.Value == "Scheduled batch"), "Scheduled batching is not a false bottleneck"); Save(game, "release-selected");
        var balanced = Run(request with { NumberOfWorkItems = 0, TesterCount = 3, WorkItemsPerDay = 1 }, 20, WorkArrivalMode.ContinuousArrival);
        vm.Load(balanced); close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Delay(100); Save(game, "clean-balanced");
        vm.Reset(); Require(!world.HasSelection && world.Details.Rows.Count == 0, "Reset clears stale selection details");
        Console.WriteLine("PASS native macOS routed pointer/keyboard, seven station choices, scaled queue hits, open/switch/close, observed metrics, LIVE updates, restore, animation/speed independence, legacy mode and full capture invariance.");
    }
    private static void PointerSelect(Window window, GameWorldControl world, Point point)
    {
        var position = world.TranslatePoint(point, window)!.Value;
        var hit = window.InputHitTest(position);
        if (!ReferenceEquals(hit, world)) Save(window, "hit-failure");
        Require(ReferenceEquals(hit, world), $"Actual visual hit target at {position} is {hit?.GetType().Name}");
        using var pointer = new Pointer(1, PointerType.Mouse, true);
        world.RaiseEvent(new PointerPressedEventArgs(world, pointer, window, position, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None, 1));
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Save(Control control, string name)
    {
        control.UpdateLayout(); using var bitmap = new RenderTargetBitmap(new((int)Math.Ceiling(control.Bounds.Width), (int)Math.Ceiling(control.Bounds.Height)));
        bitmap.Render(control); bitmap.Save(Path.Combine(Output, "screenshots", name + ".png"));
    }
}
