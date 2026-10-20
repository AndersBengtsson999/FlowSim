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
    private static string Output => "docs/verification/game-view-b6-2" + (Headless ? "/headless" : "");
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
        var live=LiveSimulation.Start(new(){DeveloperCount=5,TesterCount=0,TestingEffort=5,NumberOfWorkItems=120},Simulation.Core.WorkArrivalMode.FixedBacklog);
        for(int i=0;i<100;i++)live.Step();
        vm.Load(live);main.Simple.OpenLiveCommand.Execute(null);vm.Visualization="Game View";
        window.Width=1600;window.Height=1100;
        await Task.Delay(250);window.UpdateLayout();
        var game=window.GetVisualDescendants().OfType<GameView>().Single(v=>v.IsEffectivelyVisible);
        var world=game.GetVisualDescendants().OfType<GameWorldControl>().Single();
        world.SelectedStation="Testing queue";await Task.Delay(100);
        var list=game.FindControl<ListBox>("InspectionList")!;
        var scroll=list.GetVisualDescendants().OfType<ScrollViewer>().First();
        var expander=list.GetVisualDescendants().OfType<Expander>().First();
        var row=(InspectionItemViewModel)expander.DataContext!;
        expander.IsExpanded=true;await Task.Delay(100);
        Require(row.Expanded,"Two-way expansion state");
        scroll.Offset=new(0,80);await Task.Delay(100);
        var offset=scroll.Offset.Y;
        for(int i=0;i<30;i++) {vm.Step();await Task.Delay(15);Require(ReferenceEquals(row,world.Inspection.Items.Single(r=>r.Id==row.Id)),"Stable item identity");Require(row.Expanded,"Expansion survives LIVE");Require(Math.Abs(scroll.Offset.Y-offset)<1,"Stable list offset");}
        Require(list.GetVisualDescendants().OfType<Expander>().Contains(expander),"Stable expander control");
        var outer=game.GetVisualAncestors().OfType<ScrollViewer>().First();
        var pageOffset=outer.Offset;
        row.Expanded=false;await Task.Delay(50);row.Expanded=true;await Task.Delay(50);
        Require(outer.Offset==pageOffset,"Expansion does not scroll page");
        scroll.Offset=default;await Task.Delay(100);
        var visibleExpanded=list.GetVisualDescendants().OfType<Expander>().First(e=>e.DataContext==row);
        Require(visibleExpanded.IsExpanded,"Expanded item visible after scrolling back");
        Console.WriteLine($"Expanded row bounds: {visibleExpanded.Bounds}; content: {(visibleExpanded.Content as Control)?.Bounds}");
        Save(game,"stable-inspection");
        vm.Speed=5;vm.Resume();await Task.Delay(200);vm.Pause();
        Require(row.Expanded && world.SelectedStation=="Testing queue","Accelerated run/pause preserves interaction");
        if(Headless)
        {
            var listPoint=scroll.TranslatePoint(new Point(scroll.Bounds.Width/2,20),window)!.Value;
            scroll.Offset=default;var outerBefore=outer.Offset;
            window.MouseWheel(listPoint,new Vector(0,1),Avalonia.Input.RawInputModifiers.None);await Task.Delay(30);
            Require(outer.Offset==outerBefore,"List boundary does not chain to page");
            var point=world.TranslatePoint(new Point(world.Bounds.Width/2,world.Bounds.Height/2),window)!.Value;
            bool? handled=null;world.AddHandler(InputElement.PointerWheelChangedEvent,(_,e)=>handled=e.Handled,RoutingStrategies.Bubble,true);
            var zoom=world.Camera.Zoom;
            window.MouseWheel(point,new Vector(1,0),Avalonia.Input.RawInputModifiers.None);await Task.Delay(30);
            Require(world.Camera.Zoom==zoom && handled==false,"Ordinary horizontal wheel is not consumed");
            window.MouseWheel(point,new Vector(0,-1),Avalonia.Input.RawInputModifiers.None);await Task.Delay(30);
            Require(world.Camera.Zoom==zoom && handled==false,"Ordinary wheel bubbles without zoom");
            // Restore the page location before targeting the workshop again.
            outer.Offset=pageOffset;await Task.Delay(30);
            point=world.TranslatePoint(new Point(world.Bounds.Width/2,world.Bounds.Height/2),window)!.Value;
            window.MouseWheel(point,new Vector(0,1),OperatingSystem.IsMacOS()?Avalonia.Input.RawInputModifiers.Meta:Avalonia.Input.RawInputModifiers.Control);await Task.Delay(30);
            Require(world.Camera.Zoom>zoom && handled==true,"Explicit modifier wheel zooms and is handled");
        }
        world.FitCamera();Require(world.Camera.Zoom==1,"Fit still works");world.ZoomBy(1.4);world.Camera.Pan(new(20,10));
        vm.BeginChange();vm.Draft.NumberOfDevelopers="4";vm.ApplyChanges();vm.Step();await Task.Delay(50);Require(row.Expanded,"Intervention preserves expansion");
        var saved=Simulation.Infrastructure.LiveSessionJson.Save(live);
        vm.CheckpointCommand.Execute(null);vm.SelectedCheckpoint=vm.Checkpoints.Last();vm.Step();vm.RestoreCommand.Execute(null);await Task.Delay(50);
        Require(world.SelectedStation=="Testing queue" && !world.Inspection.Items.Any(i=>i.Expanded),"Restoration retains station and discards old expansion");
        vm.Load(Simulation.Infrastructure.LiveSessionJson.Load(saved));await Task.Delay(50);Require(world.SelectedStation=="Testing queue","Save/load preserves station");
        window.Width=980;window.Height=760;await Task.Delay(100);Save(game,"narrow-inspection");
        var dashboard=game.GetVisualDescendants().OfType<KpiDashboardView>().Single();vm.OpenExplorer(TeamKpi.CycleTime);dashboard.OpenExpandedExplorer();await Task.Delay(100);
        Require(world.SelectedStation=="Testing queue","Explorer keeps inspection");
        var desktop=(IClassicDesktopStyleApplicationLifetime)Avalonia.Application.Current!.ApplicationLifetime!;
        desktop.Windows.Single(w=>w.Title=="FLOW · KPI Explorer").Close();
        vm.Reset();await Task.Delay(50);Require(!world.HasSelection && world.Inspection.Items.Count==0,"Reset clears inspection");
        Console.WriteLine((Headless?"Headless":"Native")+" PASS: 30-day stable identity, expansion, control reuse and scroll; page scroll ownership; run/pause/resume, accelerated playback, intervention, checkpoint, save/load, reset, layout, Explorer and camera.");
    }

    private static void Require(bool condition, string message) { if(!condition) throw new InvalidOperationException(message); }
    private static void Save(Control control, string name)
    {
        control.UpdateLayout(); using var bitmap = new RenderTargetBitmap(new((int)Math.Ceiling(control.Bounds.Width), (int)Math.Ceiling(control.Bounds.Height)));
        bitmap.Render(control); Directory.CreateDirectory(Path.Combine(Output,"screenshots")); bitmap.Save(Path.Combine(Output,"screenshots", name+".png"));
    }
}
