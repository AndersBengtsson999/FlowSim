using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Simulation.Application;
using Simulation.Core;
using Simulation.UI.Controls;
using Simulation.UI.Controls.GameWorld;
using Simulation.UI.ViewModels;

internal static class Program
{
    [STAThread] public static int Main() => AppBuilder.Configure<PreviewApp>().UsePlatformDetect()
        .With(new AvaloniaNativePlatformOptions { RenderingMode = [AvaloniaNativeRenderingMode.Software] })
        .WithInterFont().StartWithClassicDesktopLifetime([]);
}
public sealed class PreviewApp : Avalonia.Application
{
    private const string Output = "docs/verification/game-view-b3-1";
    public override void Initialize() => Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        var desktop = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
        var world = new GameWorldControl { Width = 1080, Height = 680 };
        var canvas = new Border { Width = 1080, Height = 680, Background = new SolidColorBrush(Color.Parse("#101A25")), Child = world };
        var window = new Window { Title = "B3.1 native queue verification", Width = 1150, Height = 740, Content = new Viewbox { Child = canvas } };
        desktop.MainWindow = window;
        window.Opened += async (_, _) =>
        {
            try { await Verify(world, canvas, window); desktop.Shutdown(0); }
            catch (Exception e) { Console.Error.WriteLine(e); desktop.Shutdown(1); }
        };
        base.OnFrameworkInitializationCompleted();
    }
    private static async Task Verify(GameWorldControl world, Border canvas, Window window)
    {
        var request = BaselineScenario.CreateRequest() with { NumberOfWorkItems = 38, DevelopmentEffort = 1, CodeReviewEffort = 1, TestingEffort = 2,
            DeveloperCount = 5, TesterCount = 0, DevelopmentWipLimit = 20, CodeReviewWipLimit = 20, TestingWipLimit = 3 };
        LiveSimulation Run(SimulationRequest r, int days, WorkArrivalMode mode = WorkArrivalMode.FixedBacklog)
        { var live = LiveSimulation.Start(r, mode, r.WorkItemsPerDay); for (var i = 0; i < days; i++) live.Step(); return live; }
        var balanced = Run(request with { TesterCount = 3, NumberOfWorkItems = 0, WorkItemsPerDay = 1m }, 20, WorkArrivalMode.ContinuousArrival);
        var testing = Run(request, 100);
        var review = Run(request with { TesterCount = 5, NumberOfWorkItems = 200, CodeReviewEffort = 30, CodeReviewWipLimit = 1 }, 20);
        var release = Run(request with { TesterCount = 5, Release = new(ReleaseMode.Scheduled, 5, 100) }, 30);
        var huge = Run(request with { NumberOfWorkItems = 2000, DeveloperCount = 200, DeveloperCapacityPerDay = 100, DevelopmentWipLimit = 2000, CodeReviewWipLimit = 2000 }, 8);
        async Task Capture(string name, LiveSimulation live)
        {
            var before = JsonSerializer.Serialize(live.Capture()); world.Scene = GameSceneState.From(live); await Task.Delay(120);
            Save(canvas, name); Save(canvas, name + "-2x", 2);
            Require(before == JsonSerializer.Serialize(live.Capture()), name + " capture unchanged");
            var state = world.Scene!; var scene = new SpriteWorkshop(state);
            Console.WriteLine($"{name}: Day {state.Day}; review {state.Areas[2].Waiting}; testing {state.Areas[3].Waiting}; ready {state.Areas[4].Waiting}; objects {scene.Objects.Count}; " +
                string.Join("; ", state.QueuePressures.Where(p => p.Value.Kind != QueuePressureKind.Normal).Select(p => p.Key + "=" + p.Value.Kind + ":" + p.Value.Label)));
            File.WriteAllText(Path.Combine(Output, name + ".json"), JsonSerializer.Serialize(new { state.Day, Queues = scene.Queues.Select(q => new { q.Id, q.Count, q.Visible, q.Aggregated }), state.QueuePressures }, new JsonSerializerOptions { WriteIndented = true }));
        }
        Require(GameSceneState.From(balanced).QueuePressures.Values.All(p => p.Kind is QueuePressureKind.Normal), "Balanced has no warnings"); await Capture("balanced", balanced);
        Require(GameSceneState.From(testing).Areas[3].Waiting == 35, "Exactly 35 waiting with zero testers"); await Capture("testing-blocked", testing);
        Require(GameSceneState.From(review).QueuePressures["review-wait"].Kind == QueuePressureKind.Constrained, "Review constraint observed");
        Require(GameSceneState.From(review).QueuePressures["testing-wait"].Kind == QueuePressureKind.Normal, "No misplaced testing warning"); await Capture("review-constrained", review);
        Require(GameSceneState.From(release).QueuePressures["ready"].Kind == QueuePressureKind.Batching, "Scheduled accumulation is batching"); await Capture("scheduled-batch", release);
        Require(GameSceneState.From(huge).Areas[3].Waiting >= 1900, "Real thousands queue"); await Capture("huge-queue", huge);
        var watch = Stopwatch.StartNew(); for (var i = 0; i < 30; i++) { using var target = new RenderTargetBitmap(new(1080, 680)); target.Render(canvas); } watch.Stop();
        Console.WriteLine($"Large queue: 30 native renders {watch.ElapsedMilliseconds} ms; decoded {world.SpriteDecodeCount}; objects {world.SpriteObjectCount}");
        testing.Session.ApplyChanges(testing.Session.Configuration with { Team = testing.Session.Configuration.Team with { TesterCount = 5 } }, "Recovery");
        for (var i = 0; i < 8; i++) testing.Step(); await Capture("recovery-progress", testing);
        for (var i = 0; i < 100; i++) testing.Step(); await Capture("recovered", testing);
        Require(GameSceneState.From(testing).Areas[3].Waiting == 0, "Recovery drains actual queue");
        // Actual shared clock and lifecycle with aggregate fades alongside worker/package/release controllers.
        var activity = LiveSimulation.Start(request with { NumberOfWorkItems = 100, TesterCount = 3, Release = new(ReleaseMode.Scheduled, 3, 5) }, WorkArrivalMode.FixedBacklog);
        world.Scene = GameSceneState.From(activity); world.SimulationRunning = true; await Task.Delay(80);
        for (var i = 0; i < 25 && world.ChangingStackCount == 0; i++) { activity.Step(); world.Scene = GameSceneState.From(activity); }
        Require(world.ChangingStackCount > 0, "Real queue starts aggregate fade");
        world.SimulationRunning = false; var frozen = RenderHash(canvas); await Task.Delay(160); Require(frozen == RenderHash(canvas), "Paused fades and pixels freeze");
        world.AnimateActivity = false; Require(world.ChangingStackCount == 0, "Reduced motion cancels fade");
        world.Scene = GameSceneState.From(huge); Save(canvas, "huge-reduced-motion");
        world.AnimateActivity = true; world.SimulationRunning = true; world.PlaybackSpeed = 10;
        huge.Step(); world.Scene = GameSceneState.From(huge); Require(world.ChangingStackCount == 0, "Fast playback immediately shows current stacks");
        world.PlaybackSpeed = 1; world.UseSprites = false; Require(world.ChangingStackCount == 0, "Vector mode cancels"); world.UseSprites = true;
        world.Scene = GameSceneState.From(LiveSimulation.Restore(huge.Capture())); Require(world.ChangingStackCount == 0, "Checkpoint restore cancels stale stack animation");
        var stable = JsonSerializer.Serialize(huge.Capture());
        window.Width = 850; window.Height = 590; await Task.Delay(150); Save(window, "compact-window");
        window.WindowState = WindowState.Minimized; await Task.Delay(100); Require(!world.IsAnimationRunning, "Minimized stops shared clock"); window.WindowState = WindowState.Normal;
        world.IsVisible = false; Require(!world.IsAnimationRunning, "Hidden stops shared clock"); world.IsVisible = true;
        canvas.Child = null; Require(!world.IsAnimationRunning && world.SpriteDecodeCount == 0, "Detach stops clock and releases assets");
        Require(stable == JsonSerializer.Serialize(huge.Capture()), "Lifecycle leaves full simulation capture intact");
        Console.WriteLine("PASS native macOS A–F, zero-testing activity, static warnings, paused pixels, high speed, restore, mode, reduced motion, resize, cache and unchanged capture.");
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Save(Control control, string name, double scale = 1)
    {
        control.UpdateLayout(); using var bitmap = new RenderTargetBitmap(new((int)Math.Ceiling(control.Bounds.Width * scale), (int)Math.Ceiling(control.Bounds.Height * scale)), new(96 * scale, 96 * scale));
        bitmap.Render(control); bitmap.Save(Path.Combine(Output, "screenshots", name + ".png"));
    }
    private static string RenderHash(Control control)
    {
        using var bitmap = new RenderTargetBitmap(new((int)control.Bounds.Width, (int)control.Bounds.Height)); bitmap.Render(control);
        using var bytes = new MemoryStream(); bitmap.Save(bytes); return Convert.ToHexString(SHA256.HashData(bytes.ToArray()));
    }
}
