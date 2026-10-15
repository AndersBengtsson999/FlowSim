using ModelValidationV2;

using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Headless;
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
    [STAThread] public static int Main(string[] args)
    {
        var builder = AppBuilder.Configure<PreviewApp>();
        var headless = args.Contains("--headless");
        PreviewApp.Headless = headless;
        builder = headless ? builder.UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).UseSkia()
            : builder.UsePlatformDetect().With(new AvaloniaNativePlatformOptions { RenderingMode = [AvaloniaNativeRenderingMode.Software] });
        return builder.WithInterFont().StartWithClassicDesktopLifetime([]);
    }
}
public sealed class PreviewApp : Avalonia.Application
{
    public static bool Headless { get; set; }
    private static string Output => "docs/verification/game-view-b4-1" + (Headless ? "/headless" : "");
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
        window.UpdateLayout();
        var world = game.GetVisualDescendants().OfType<GameWorldControl>().Single();
        var panel = game.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "StationPanel");
        var picker = game.GetVisualDescendants().OfType<ComboBox>().Single(b => b.Name == "StationPicker");
        var close = game.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "CloseStation");
        var before = JsonSerializer.Serialize(vm.Live!.Capture());
        window.Activate(); world.InvalidateVisual(); Save(game, "initial-fit");
        for (var ready = 0; ready < 40; ready++)
        {
            await Task.Delay(50); window.UpdateLayout();
            if (ReferenceEquals(window.InputHitTest(world.TranslatePoint(world.Camera.ToScreen(new(710, 404)), window)!.Value), world)) break;
        }
        PointerSelect(window, world, new(710, 404)); await Task.Delay(100);
        Require(world.SelectedStation == "Testing" && panel.IsVisible && world.Details.Rows.Any(r => r.Label == "Waiting" && r.Value == "35"), "Pointer opens exact Testing details");
        Save(game, "testing-selected"); Save(window, "testing-window");
        PointerSelect(window, world, new(350, 130)); Require(world.SelectedStation == "Backlog", "Planning board face selectable");
        PointerSelect(window, world, new(910, 205)); Require(world.SelectedStation == "Code Review", "Review board face selectable");
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
            world.FitCamera();
            var origin = world.TranslatePoint(world.Camera.ToScreen(default), game)!.Value; var corner = world.TranslatePoint(world.Camera.ToScreen(new(1080, 620)), game)!.Value;
            var sx = (corner.X - origin.X) / 1080; var sy = (corner.Y - origin.Y) / 620;
            Require(Math.Abs(sx - sy) < .0001 && sx > 0, "Uniform fit, no sprite distortion");
            Require(corner.X <= game.Bounds.Width + 1 && origin.X >= -1, "World fits width");
            var liveScroll = window.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "LiveScroll");
            var worldBottom = world.TranslatePoint(world.Camera.ToScreen(new(1080, 620)), window)!.Value.Y;
            var scrollBottom = liveScroll.TranslatePoint(new(0, liveScroll.Bounds.Height), window)!.Value.Y;
            Require(worldBottom <= scrollBottom + 1, "Entire workshop fits actual LIVE viewport");
            Console.WriteLine($"Window {size}: world scale {sx:0.000}, world height {corner.Y - origin.Y:0}, panel {panel.Bounds}");
            Save(window, "window-" + size.Width);
        }
        // Camera events use real routed input at screen coordinates; every object shares the render transform.
        world.FitCamera(); picker.SelectedItem = "Testing"; await Task.Delay(100);
        var cameraState = JsonSerializer.Serialize(vm.Live!.Capture());
        var ticks = world.AnimationTicks; var seconds = world.AnimationSeconds; var decoded = world.SpriteDecodeCount;
        var testPoint = world.Camera.ToScreen(new(710, 404));
        var anchored = world.Camera.ToWorld(testPoint);
        Wheel(window, world, testPoint, Math.Log(2) / .12); await Task.Delay(80);
        Require(Math.Abs(world.Camera.Zoom - 2) < .001, "Wheel zoom reaches 200%");
        Require(((Vector)(world.Camera.ToWorld(testPoint) - anchored)).Length < .001, "Pointer world anchor is stable");
        var panelBefore = panel.Bounds;
        PointerSelect(window, world, new(710, 404)); await Task.Delay(100);
        Require(world.SelectedStation == "Testing" && panel.Bounds == panelBefore, "Testing hit and anchored panel at 200%");
        Save(window, "zoom-testing");
        var empty = Enumerable.Range(1, 10).SelectMany(x => Enumerable.Range(1, 6).Select(y => new Point(x * 50, y * 40)))
            .First(p => StationMap.HitTest(world.Camera.ToWorld(p)) is null);
        var panBefore = world.Camera.Offset;
        Drag(window, world, empty, empty + new Vector(60, 45), false);
        Require(world.Camera.Offset != panBefore, "Empty-space drag moves camera");
        Require(world.SelectedStation == "Testing", "Empty-space drag does not clear selected station");
        // Middle-drag centers Review without selecting anything along the route.
        var reviewScreen = world.Camera.ToScreen(new(823, 256));
        var center = new Point(world.Bounds.Width / 2, world.Bounds.Height / 2);
        Drag(window, world, center, center + (Vector)(center - reviewScreen), true);
        Require(world.SelectedStation == "Testing", "Middle drag preserves selection");
        PointerSelect(window, world, new(823, 256)); await Task.Delay(100);
        Require(world.SelectedStation == "Code Review", "Zoom plus pan selects Review"); Save(window, "zoom-review");
        Require(cameraState == JsonSerializer.Serialize(vm.Live.Capture()), "Camera does not modify captured simulation");
        Require(world.AnimationTicks == ticks && world.AnimationSeconds == seconds && world.SpriteDecodeCount == decoded, "Paused camera neither restarts clock nor reloads assets");
        var preservedCenter = world.Camera.ToWorld(new(world.Bounds.Width / 2, world.Bounds.Height / 2));
        window.Width = 1440; await Task.Delay(400);
        Require(world.Camera.Zoom == 2 && world.Camera.IsManual, "Resize retains manual zoom");
        Require(((Vector)(world.Camera.ToWorld(new(world.Bounds.Width / 2, world.Bounds.Height / 2)) - preservedCenter)).Length < 1, "Resize retains world center");
        Save(window, "zoom-resized");
        vm.Load(LiveSimulation.Restore(saved)); Require(world.Camera.Zoom == 2 && world.SelectedStation == "Code Review", "Restore keeps valid camera and selection");
        var buttons = game.GetVisualDescendants().OfType<Button>().ToArray();
        buttons.Single(b => Equals(b.Content, "Fit to View")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Task.Delay(120); Require(world.SelectedStation == "Code Review" && world.Camera.Zoom == 1 && !world.Camera.IsManual, "Fit preserves selection");
        buttons.Single(b => Equals(b.Content, "+")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Require(Math.Abs(world.Camera.Zoom - 1.2) < .001, "Visible zoom-in control");
        buttons.Single(b => Equals(b.Content, "−")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Require(Math.Abs(world.Camera.Zoom - 1) < .001, "Visible zoom-out control");
        var outsideZoom = world.Camera.Zoom;
        Wheel(window, world, new(-10, -10), 1); Require(world.Camera.Zoom == outsideZoom, "Outside wheel is not intercepted");
        world.FitCamera(); picker.SelectedItem = "Testing";
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
        var runningTicks = world.AnimationTicks; var runningSeconds = world.AnimationSeconds;
        for (var i = 0; i < 14; i++) { picker.SelectedItem = StationMap.Names[i % 7]; world.ZoomBy(i % 2 == 0 ? 1.1 : 1 / 1.1); }
        Require(world.AnimationSeconds >= runningSeconds && world.AnimationTicks >= runningTicks, "Camera preserves running clock");
        Require(vm.IsRunning && world.IsAnimationRunning, "Selection does not pause shared animation or LIVE");
        Require(runningBefore == JsonSerializer.Serialize(vm.Live.Capture()), "Running selection causes no simulation event"); vm.Pause();
        world.FitCamera(); vm.Speed = 10; vm.TargetDay = (vm.Day + 10).ToString(); await vm.RunToDayAsync(); Require(world.SelectedStation == "Technical Debt", "Fast advance keeps selection");
        var review = Run(request with { NumberOfWorkItems = 200, TesterCount = 5, CodeReviewEffort = 30, CodeReviewWipLimit = 1 }, 20);
        vm.Load(review); picker.SelectedItem = "Code Review"; await Task.Delay(100); Require(world.Details.Rows.Any(r => r.Value == "Shared Dev capacity busy"), "Review reports shared capacity correctly"); Save(game, "review-selected");
        var release = Run(request with { TesterCount = 5, Release = new(ReleaseMode.Scheduled, 5, 100) }, 30);
        vm.Load(release); picker.SelectedItem = "Ready for Release"; await Task.Delay(100); Require(world.Details.Rows.Any(r => r.Value == "Scheduled batch"), "Scheduled batching is not a false bottleneck"); Save(game, "release-selected");
        var balanced = Run(request with { NumberOfWorkItems = 0, TesterCount = 3, WorkItemsPerDay = 1 }, 20, WorkArrivalMode.ContinuousArrival);
        vm.Load(balanced); close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await Task.Delay(100); Save(game, "clean-balanced");
        var returns = LiveSimulation.Start(BaselineScenario.DefectsAndReworkExample(), WorkArrivalMode.AlwaysAvailable);
        var returned = false;
        for (var d = 0; d < 300 && !returned; d++)
        {
            returns.Step(); var scene = GameSceneState.From(returns);
            returned = scene.Journeys.Any(j => j.Stages.Zip(j.Stages.Skip(1)).Any(p => WorkflowGeometry.IsReturn(p.First, p.Second)));
        }
        Require(returned, "Authoritative defect fixture records a rework return");
        vm.Load(returns); picker.SelectedItem = "Development"; world.FitCamera(); await Task.Delay(120); Save(game, "observed-rework");
        var debt = LiveSimulation.Start(Validation.Complex with { DeveloperCount = 8, TesterCount = 3 }, WorkArrivalMode.AlwaysAvailable);
        for (var d = 0; d < 300 && GameSceneState.From(debt).UsedDebtRepaymentCapacity == 0; d++) debt.Step();
        Require(GameSceneState.From(debt).UsedDebtRepaymentCapacity > 0, "Observed maintenance consumes developer capacity");
        vm.Load(debt); picker.SelectedItem = "Technical Debt"; await Task.Delay(120); Save(game, "technical-debt");
        // Render camera changes between actual observed transitions while the shared clock runs.
        vm.Load(LiveSimulation.Start(request with { TesterCount = 3 }, WorkArrivalMode.FixedBacklog));
        vm.Speed = 1; vm.Resume();
        var sawPackages = false; var sawWorkers = false; var sawRelease = false;
        for (var i = 0; i < 12; i++)
        {
            vm.Tick(); await Task.Delay(45); Save(game, "active-camera");
            sawPackages |= world.MovingPackageCount > 0; sawWorkers |= world.MovingWorkerCount > 0; sawRelease |= world.CurrentRelease is not null;
            var count = world.MovingPackageCount; var workerCount = world.MovingWorkerCount; var clock = world.AnimationSeconds; var decodeCount = world.SpriteDecodeCount;
            var capture = JsonSerializer.Serialize(vm.Live!.Capture());
            world.ZoomBy(i % 2 == 0 ? 1.2 : 1 / 1.2);
            Drag(window, world, new(100, 100), new(110, 105), true);
            Require(world.MovingPackageCount == count && world.MovingWorkerCount == workerCount && world.AnimationSeconds == clock && world.SpriteDecodeCount == decodeCount,
                "Camera does not restart moving packages, workers, clock or cache");
            Require(capture == JsonSerializer.Serialize(vm.Live.Capture()), "Camera input preserves live simulation state");
        }
        Console.WriteLine($"Observed active animation: packages={sawPackages}, workers={sawWorkers}, release={sawRelease}");
        Require(sawPackages && sawWorkers && sawRelease, "Camera exercised during real worker/package/release motion");
        Save(game, "active-camera"); vm.Pause();
        vm.Reset(); Require(!world.HasSelection && world.Details.Rows.Count == 0, "Reset clears stale selection details");
        Console.WriteLine("PASS B4.1 observed rework and maintenance fixtures; all B3.3 routed camera controls: wheel anchor, 200% zoom, empty and middle drag, transformed Testing/Review/queue hits, fixed panel, resize, fit, checkpoint restore, shared clock and asset cache independence, full state invariance; B3.2 station checks retained.");
    }
    private static void PointerSelect(Window window, GameWorldControl world, Point point)
    {
        var local = world.Camera.ToScreen(point);
        var position = world.TranslatePoint(local, window)!.Value;
        var hit = window.InputHitTest(position);
        if (!ReferenceEquals(hit, world)) Save(window, "hit-failure");
        Require(ReferenceEquals(hit, world), $"Actual visual hit target at {position} is {hit?.GetType().Name}");
        using var pointer = new Pointer(1, PointerType.Mouse, true);
        world.RaiseEvent(new PointerPressedEventArgs(world, pointer, window, position, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None, 1));
        world.RaiseEvent(new PointerReleasedEventArgs(world, pointer, window, position, 1,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left));
    }
    private static void Wheel(Window window, GameWorldControl world, Point local, double delta)
    {
        using var pointer = new Pointer(5, PointerType.Mouse, true);
        world.RaiseEvent(new PointerWheelEventArgs(world, pointer, window, world.TranslatePoint(local, window)!.Value, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other), KeyModifiers.None, new Vector(0, delta)));
    }
    private static void Drag(Window window, GameWorldControl world, Point start, Point end, bool middle)
    {
        using var pointer = new Pointer(6, PointerType.Mouse, true);
        Point Position(Point p) => world.TranslatePoint(p, window)!.Value;
        var modifiers = middle ? RawInputModifiers.MiddleMouseButton : RawInputModifiers.LeftMouseButton;
        world.RaiseEvent(new PointerPressedEventArgs(world, pointer, window, Position(start), 0,
            new PointerPointProperties(modifiers, middle ? PointerUpdateKind.MiddleButtonPressed : PointerUpdateKind.LeftButtonPressed), KeyModifiers.None, 1));
        world.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, world, pointer, window, Position(end), 1,
            new PointerPointProperties(modifiers, PointerUpdateKind.Other), KeyModifiers.None));
        world.RaiseEvent(new PointerReleasedEventArgs(world, pointer, window, Position(end), 2,
            new PointerPointProperties(RawInputModifiers.None, middle ? PointerUpdateKind.MiddleButtonReleased : PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, middle ? MouseButton.Middle : MouseButton.Left));
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Save(Control control, string name)
    {
        control.UpdateLayout(); using var bitmap = new RenderTargetBitmap(new((int)Math.Ceiling(control.Bounds.Width), (int)Math.Ceiling(control.Bounds.Height)));
        bitmap.Render(control); Directory.CreateDirectory(Path.Combine(Output, "screenshots")); bitmap.Save(Path.Combine(Output, "screenshots", name + ".png"));
    }
}
