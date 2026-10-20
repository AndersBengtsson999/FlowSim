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
    private static string Output => "docs/verification/game-view-b6-1" + (Headless ? "/headless" : "");
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
        var vm=main.Simple.Live;
        var live=LiveSimulation.Start(new(){DeveloperCount=10,TesterCount=2,TestingEffort=5,NumberOfWorkItems=100},Simulation.Core.WorkArrivalMode.ContinuousArrival,4);
        for(int i=0;i<100;i++)live.Step();
        vm.Load(live);main.Simple.OpenLiveCommand.Execute(null);vm.Visualization="Game View";
        await Task.Delay(350);window.UpdateLayout();
        var game=window.GetVisualDescendants().OfType<GameView>().Single(v=>v.IsEffectivelyVisible);
        var panel=game.GetVisualDescendants().OfType<KpiDashboardView>().Single();
        var world=game.GetVisualDescendants().OfType<GameWorldControl>().Single();
        var before=System.Text.Json.JsonSerializer.Serialize(live.Capture());
        world.SelectedStation="Testing queue";
        await Task.Delay(100);
        Require(world.HasSelection && world.Details.Title=="Testing queue","Queue selected");
        Require(world.Details.Items.Count>0,"Queue items available");
        var expander=game.GetVisualDescendants().OfType<Expander>().First(e=>e.DataContext is InspectedWorkItem);
        expander.IsExpanded=true; expander.BringIntoView(); await Task.Delay(100);
        Save(game,"queue-inspection");
        var inspectionScroll=expander.GetVisualAncestors().OfType<ScrollViewer>().First();
        inspectionScroll.Offset=new Vector(0,200);await Task.Delay(100);Save(game,"work-item-inspection");
        world.ZoomBy(1.4);world.Camera.Pan(new(25,10));
        Require(before==System.Text.Json.JsonSerializer.Serialize(live.Capture()),"Inspection is read only");
        var id=world.Details.Items.First().Id;
        vm.Step();await Task.Delay(100);
        Require(world.SelectedStation=="Testing queue","LIVE preserves queue");
        Require(world.Details.Items.All(i=>live.Session.WorkItems.Any(w=>w.Id==i.Id && w.State==Simulation.Core.WorkItemStatus.WaitingForTesting)),"No stale item membership");
        vm.Pause();vm.Resume();await Task.Delay(160);vm.Pause();
        Require(world.SelectedStation=="Testing queue","Run pause resume preserves queue");
        window.Width=980;window.Height=760;await Task.Delay(150);Save(game,"inspection-narrow");
        vm.OpenExplorer(TeamKpi.CycleTime);panel.OpenExpandedExplorer();await Task.Delay(100);
        Require(world.SelectedStation=="Testing queue","Explorer preserves queue");
        var desktop=(IClassicDesktopStyleApplicationLifetime)Avalonia.Application.Current!.ApplicationLifetime!;
        desktop.Windows.Single(w=>w.Title=="FLOW · KPI Explorer").Close();
        vm.CheckpointCommand.Execute(null);vm.SelectedCheckpoint=vm.Checkpoints.Last();vm.Step();vm.RestoreCommand.Execute(null);await Task.Delay(100);
        Require(world.SelectedStation=="Testing queue","Restoration keeps queue");
        vm.Reset();await Task.Delay(100);Require(!world.HasSelection && world.Details.Items.Count==0,"Reset clears inspection");
        Console.WriteLine((Headless ? "Headless " : "Native ") + "PASS: queue inspection, item expansion, read-only state, LIVE run/pause/resume/step, camera, resize, Explorer, checkpoint and reset.");
    }

    private static void Require(bool condition, string message) { if(!condition) throw new InvalidOperationException(message); }
    private static void Save(Control control, string name)
    {
        control.UpdateLayout(); using var bitmap = new RenderTargetBitmap(new((int)Math.Ceiling(control.Bounds.Width), (int)Math.Ceiling(control.Bounds.Height)));
        bitmap.Render(control); Directory.CreateDirectory(Path.Combine(Output,"screenshots")); bitmap.Save(Path.Combine(Output,"screenshots", name+".png"));
    }
}
