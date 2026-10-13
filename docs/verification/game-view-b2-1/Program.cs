using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using ModelValidationV2;
using Simulation.Application;
using Simulation.Core;
using Simulation.Sprites;
using Simulation.UI.Controls;
using Simulation.UI.Controls.GameWorld;
using Simulation.UI.ViewModels;
using Simulation.UI.Views;

internal static class Program
{
    [STAThread] public static int Main(string[] args)
    {
        var builder = AppBuilder.Configure<PreviewApp>();
        builder = args.Contains("--headless") ? builder.UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).UseSkia()
            : builder.UsePlatformDetect().With(new AvaloniaNativePlatformOptions { RenderingMode = [AvaloniaNativeRenderingMode.Software] });
        return builder.WithInterFont().StartWithClassicDesktopLifetime([]);
    }
}

public sealed class PreviewApp : Avalonia.Application
{
    private const string Output = "docs/verification/game-view-b2-1";
    public override void Initialize() { Styles.Add(new FluentTheme()); RequestedThemeVariant = ThemeVariant.Light; }
    public override void OnFrameworkInitializationCompleted()
    {
        var desktop = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
        var main = new MainWindowViewModel(); var vm = main.Simple.Live;
        var window = new MainWindow { DataContext = main, Width = 1280, Height = 1000 };
        desktop.MainWindow = window;
        window.Opened += async (_, _) =>
        {
            try { await Verify(main, window); vm.Dispose(); desktop.Shutdown(0); }
            catch (Exception e) { Console.Error.WriteLine(e); vm.Dispose(); desktop.Shutdown(1); }
        };
        base.OnFrameworkInitializationCompleted();
    }
    private static async Task Verify(MainWindowViewModel main, Window window)
    {
        var live = LiveSimulation.Start(Validation.Complex with
        {
            DeveloperCount = 8, TesterCount = 3, DevelopmentWipLimit = 3, CodeReviewWipLimit = 2, TestingWipLimit = 3,
            DevelopmentEffort = 12, CodeReviewEffort = 4, TestingEffort = 6, Skills = new(2, .3)
        }, WorkArrivalMode.AlwaysAvailable);
        while (live.Session.CurrentDay < 300)
        {
            live.Step(); var activity = WorkshopActivity.From(GameSceneState.From(live));
            if (activity.Developers.Contains(SpritePose.Collaborating) && activity.Developers.Contains(SpritePose.Reviewing) && activity.Testers.Contains(SpritePose.Testing)) break;
        }
        Require(live.Session.CurrentDay < 300, "Real fixture covers collaboration, review and testing");
        live.CreateCheckpoint("Animation verification checkpoint");
        var fixture = live.Capture();
        var vm = main.Simple.Live; vm.Load(live);
        await Task.Delay(200);
        window.GetVisualDescendants().OfType<ComboBox>().Single(c => c.Name == "LiveVisualization").SelectedItem = "Game View";
        await Task.Delay(200);
        var game = window.GetVisualDescendants().OfType<GameView>().Single();
        var world = game.GetVisualDescendants().OfType<GameWorldControl>().Single();
        var checkbox = game.GetVisualDescendants().OfType<CheckBox>().Single(c => c.Name == "AnimateGameActivity");
        var selector = game.GetVisualDescendants().OfType<ComboBox>().Single(c => c.Name == "GameRenderingMode");
        var capture = Validation.Hash(live.Capture());
        Require(!world.IsAnimationRunning && checkbox.IsChecked == true, "Paused by default; animation enabled independently");
        var pausedHash = RenderHash(world); await Task.Delay(180); Require(pausedHash == RenderHash(world), "Paused pixels frozen");
        checkbox.IsChecked = false; Require(!vm.AnimateGameActivity && !world.AnimateActivity, "Accessible toggle bound");
        checkbox.IsChecked = true;
        selector.SelectedItem = "Legacy vectors"; await Task.Delay(80); Require(!world.IsAnimationRunning && !world.UseSprites, "Vector fallback stops animation"); Save(game, "legacy");
        selector.SelectedItem = "Sprites"; await Task.Delay(80);
        Require(capture == Validation.Hash(live.Capture()), "Pause/toggle/mode switches preserve complete capture");
        vm.Speed = .5; vm.Resume(); await Task.Delay(250); Require(world.IsAnimationRunning && world.AnimationTicks > 0, "Actual LIVE playback activates animation binding");
        vm.Pause(); await AssertStopped(world, "LIVE Pause");
        vm.BeginChange(); vm.Draft.NumberOfTesters = "4"; vm.ChangeLabel = "Pending draft";
        vm.AnimateGameActivity = false; vm.AnimateGameActivity = true; selector.SelectedItem = "Legacy vectors"; selector.SelectedItem = "Sprites";
        Require(vm.IsEditing && vm.Draft.NumberOfTesters == "4" && vm.ChangeLabel == "Pending draft", "Draft survives animation/mode switching");
        vm.CancelChangeCommand.Execute(null);
        Save(window, "live-1280"); Save(game, "scene-2x", 2);
        window.Width = 960; window.Height = 820; await Task.Delay(160); Save(window, "live-960");
        var scroll = window.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "LiveScroll");
        Require(game.TranslatePoint(new(0, game.Bounds.Height), window)!.Value.Y <= scroll.TranslatePoint(new(0, scroll.Bounds.Height), window)!.Value.Y, "Full 960 viewport includes animation control and legend");
        vm.Visualization = "Flow Board"; await AssertStopped(world, "Flow Board"); vm.Visualization = "Game View";
        main.Simple.OpenAnalyzeCommand.Execute(null); await AssertStopped(world, "Navigate away"); main.Simple.OpenLiveCommand.Execute(null);
        vm.Speed = 10; vm.TargetDay = (vm.Day + 100).ToString(); var fast = Stopwatch.StartNew(); await vm.RunToDayAsync(); fast.Stop();
        await AssertStopped(world, "Fast advance ends paused");
        Require(world.Scene!.Day == vm.Day, "Latest snapshot after fast advance");
        Console.WriteLine($"Actual LIVE fast advance 100 days: {fast.ElapsedMilliseconds} ms");
        // A second actual GameWorldControl consumes one immutable real snapshot. Its presentation
        // timer runs while the original simulation remains paused, proving clock separation.
        vm.Load(LiveSimulation.Restore(fixture)); live = vm.Live!;
        var snapshot = GameSceneState.From(live); var frozenCapture = Validation.Hash(live.Capture());
        var animated = new GameWorldControl { Width = 1080, Height = 680, Scene = snapshot, SimulationRunning = true };
        var host = new Border { Background = new SolidColorBrush(Color.Parse("#101A25")), Child = animated };
        var preview = new Window { Title = "B2.1 verification · read-only activity preview", Width = 1100, Height = 720, Content = host };
        preview.Show(); await Task.Delay(200); Require(animated.IsAnimationRunning, "Attached preview runs");
        var first = RenderHash(animated); await Task.Delay(450); var second = RenderHash(animated);
        Require(first != second, "Actual rendered effects change with presentation time");
        Require(frozenCapture == Validation.Hash(live.Capture()), "Animation ticks do not advance paused simulation");
        var decoded = animated.SpriteDecodeCount;
        var frames = new List<object>(); Directory.CreateDirectory("/tmp/flowsim-b21-frames");
        for (var i = 0; i < 48; i++)
        {
            var path = $"/tmp/flowsim-b21-frames/{i:000}.png";
            SaveFile(host, path); frames.Add(new { frame = i, seconds = animated.AnimationSeconds });
            await Task.Delay(100);
        }
        File.WriteAllText(Path.Combine(Output, "capture-timing.json"), JsonSerializer.Serialize(frames, new JsonSerializerOptions { WriteIndented = true }));
        animated.SimulationRunning = false; await AssertStopped(animated, "Simulation freeze");
        var freezePixels = RenderHash(animated); await Task.Delay(150); Require(freezePixels == RenderHash(animated), "Frozen animation pixels stable");
        animated.SimulationRunning = true; animated.AnimateActivity = false; await AssertStopped(animated, "Reduced motion toggle");
        animated.AnimateActivity = true; await Task.Delay(100); Require(animated.IsAnimationRunning, "Toggle resumes");
        animated.UseSprites = false; await AssertStopped(animated, "Vector mode"); animated.UseSprites = true;
        host.IsVisible = false; await AssertStopped(animated, "Hidden ancestor"); host.IsVisible = true; await Task.Delay(150); Require(animated.IsAnimationRunning, "Visible ancestor resumes");
        preview.WindowState = WindowState.Minimized; await AssertStopped(animated, "Minimized window"); preview.WindowState = WindowState.Normal;
        await Task.Delay(200); Require(animated.IsAnimationRunning, "Restored window resumes");
        var container = new StackPanel(); host.Child = null; container.Children.Add(animated); container.Children.Add(new Border { Height = 1000 });
        var scroller = new ScrollViewer { Content = container, Height = 300 }; host.Child = scroller; await Task.Delay(150);
        scroller.Offset = new Vector(0, 900); await AssertStopped(animated, "Outside scroll viewport");
        scroller.Offset = default; await Task.Delay(150); Require(animated.IsAnimationRunning, "Scroll back resumes");
        host.Child = null; container.Children.Remove(animated); host.Child = animated; await Task.Delay(150);
        Require(animated.IsAnimationRunning, "Reattached view resumes without duplicate timers");
        var large = LiveSimulation.Start(Validation.Baseline with { DeveloperCount = 200, TesterCount = 100, NumberOfWorkItems = 2000, Skills = new(40, .3) }, WorkArrivalMode.FixedBacklog);
        for (var i = 0; i < 30; i++) large.Step(); animated.Scene = GameSceneState.From(large); await Task.Delay(100); Save(animated, "large-team");
        Require(animated.SpriteObjectCount <= 70, "Bounded large scene");
        var watch = Stopwatch.StartNew(); for (var i = 0; i < 30; i++) { using var target = new RenderTargetBitmap(new(1080, 680)); target.Render(animated); } watch.Stop();
        Require(animated.SpriteDecodeCount <= 11, "No per-frame texture decoding");
        Console.WriteLine($"30 offscreen scene renders: {watch.ElapsedMilliseconds} ms; {animated.SpriteObjectCount} objects; {decoded} initial decoded textures");
        animated.Scene = GameSceneState.From(null); await AssertStopped(animated, "Reset/no session");
        preview.Close(); await AssertStopped(animated, "Closed/detached"); Require(animated.SpriteDecodeCount == 0, "Detached assets disposed");
        Require(frozenCapture == Validation.Hash(live.Capture()), "Complete history/checkpoint capture unchanged through all animation probes");
        vm.Reset(); Require(!world.IsAnimationRunning, "Main LIVE reset stops clock");
        Console.WriteLine($"PASS native macOS bindings, real fixture Day {snapshot.Day}, pixel animation/freeze, lifecycle, viewport, cache, large team, full capture invariant and LIVE controls.");
    }
    private static async Task AssertStopped(GameWorldControl world, string name)
    {
        await Task.Delay(100); Require(!world.IsAnimationRunning, name + " stops clock");
        var ticks = world.AnimationTicks; var seconds = world.AnimationSeconds;
        await Task.Delay(130); Require(ticks == world.AnimationTicks && seconds == world.AnimationSeconds, name + " schedules no ticks");
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Save(Control control, string name, double scale = 1) => SaveFile(control, Path.Combine(Output, "screenshots", name + ".png"), scale);
    private static void SaveFile(Control control, string path, double scale = 1)
    {
        control.UpdateLayout(); using var bitmap = new RenderTargetBitmap(new((int)Math.Ceiling(control.Bounds.Width * scale), (int)Math.Ceiling(control.Bounds.Height * scale)), new(96 * scale, 96 * scale));
        bitmap.Render(control); bitmap.Save(path);
    }
    private static string RenderHash(Control control)
    {
        using var bitmap = new RenderTargetBitmap(new((int)control.Bounds.Width, (int)control.Bounds.Height)); bitmap.Render(control);
        using var bytes = new MemoryStream(); bitmap.Save(bytes); return Convert.ToHexString(SHA256.HashData(bytes.ToArray()));
    }
}
