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
using Simulation.UI.Help;
using Simulation.UI.Insights;
using Avalonia.Automation;

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
    private static string Output => "docs/verification/rc1" + (Headless ? "/headless" : "");
    public override void Initialize()
    {
        Resources.MergedDictionaries.Add(new Avalonia.Markup.Xaml.Styling.ResourceInclude(new Uri("avares://Simulation.UI/")) { Source=new Uri("avares://Simulation.UI/Styles/FlowColors.axaml") });
        Styles.Add(new FluentTheme());
        Styles.Add(new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://Simulation.UI/")) { Source=new Uri("avares://Simulation.UI/Styles/FlowControls.axaml") });
        RequestedThemeVariant=ThemeVariant.Dark;
    }
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
    private static async Task Verify(MainWindowViewModel main,Window window)
    {
        var vm=main.Simple.Live;
        var live=LiveSimulation.Start(new(){NumberOfWorkItems=80,DeveloperCount=6,TesterCount=1,
            DevelopmentEffort=1,CodeReviewEffort=1,TestingEffort=5,DevelopmentWipLimit=20,CodeReviewWipLimit=20,TestingWipLimit=6},Simulation.Core.WorkArrivalMode.ContinuousArrival,3);
        for(var n=0;n<35;n++)live.Step();
        vm.Load(live);main.Simple.OpenLiveCommand.Execute(null);vm.Visualization="Game View";
        await Task.Delay(250);window.UpdateLayout();
        var view=window.GetVisualDescendants().OfType<LiveView>().Single(v=>v.IsEffectivelyVisible);
        var game=window.GetVisualDescendants().OfType<GameView>().Single(g=>g.IsEffectivelyVisible);
        var world=game.GetVisualDescendants().OfType<GameWorldControl>().Single();
        Require(!vm.ProductDetailsExpanded,"Customer Launch defaults to compact");
        var initial=System.Text.Json.JsonSerializer.Serialize(live.Session.Capture());
        foreach(var size in Enumerable.Range(0,3).SelectMany(_ => new[]{new Size(1440,900),new Size(1280,800),new Size(980,800),new Size(960,680)}))
        {
            window.Width=size.Width;window.Height=size.Height;vm.ProductDetailsExpanded=false;
            await Task.Delay(180);Save(window,$"game-{size.Width}-{size.Height}");
            Console.WriteLine($"LAYOUT requested={size} actual={window.ClientSize} world={world.Bounds} viewport={game.FindControl<Border>("WorldViewport")!.Height} expectedMin={Math.Min(size.Height<850?340:400,world.Bounds.Width*620/1080)-2}");
            Require(world.Bounds.Height>=Math.Min(size.Height<850?340:400,world.Bounds.Width*620/1080)-2,"Readable workshop after resize");
            world.ZoomBy(1.2);var zoom=world.Camera.Zoom;
            vm.ProductDetailsExpanded=true;await Task.Delay(80);vm.ProductDetailsExpanded=false;await Task.Delay(80);
            Require(world.Camera.Zoom==zoom,"Manual zoom survives launch disclosure");
            window.Height+=20;await Task.Delay(80);Require(world.Camera.Zoom==zoom,"Manual zoom survives window resize");
            world.FitCamera();Require(world.Camera.Zoom==1 && !world.Camera.IsManual,"Fit resets manual zoom");
            var center=world.Camera.ToScreen(new Point(540,310));
            Require(Math.Abs(center.X-world.Camera.View.Width/2)<1 && Math.Abs(center.Y-world.Camera.View.Height/2)<1,"Fit centers the scene");
            Require(game.GetVisualDescendants().OfType<KpiDashboardView>().Single().IsEffectivelyVisible,"KPI dashboard available");
        }
        vm.ProductDetailsExpanded=true;await Task.Delay(100);Save(window,"expanded");
        Require(initial==System.Text.Json.JsonSerializer.Serialize(live.Session.Capture()),"Layout interactions leave simulation unchanged");
        vm.ProductDetailsExpanded=false;vm.Resume();await Task.Delay(80);Save(window,"running-compact");Require(window.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.IsEffectivelyVisible && t.Text=="Pause the simulation to enable customer launch."),"Pause explanation visible while collapsed");Require(vm.LaunchNowHelp=="Pause the simulation to enable customer launch.","Exact running explanation");Require(!vm.LaunchProductCommand.CanExecute(null) && !vm.PlanLaunchCommand.CanExecute(null),"Launch still requires pause");vm.Pause();
        await Soak(vm,window);
    }
    private static async Task Soak(LiveViewModel vm,Window window)
    {
        vm.Load(LiveSimulation.Start(LiveSimulation.Demo,Simulation.Core.WorkArrivalMode.ContinuousArrival,1m));
        vm.TargetDay="2000";
        var pause=Task.Run(async()=>{await Task.Delay(100);Avalonia.Threading.Dispatcher.UIThread.Post(vm.Pause);});
        await vm.RunToDayAsync();await pause;
        Require(vm.Day>0 && vm.Day<2000,"Queued Pause stops fast advance before target");
        Console.WriteLine("PAUSE delivered at day "+vm.Day);
        vm.Load(LiveSimulation.Start(LiveSimulation.Demo,Simulation.Core.WorkArrivalMode.ContinuousArrival,1m));
        vm.TargetDay="2000";vm.Visualization="Game View";window.Width=1280;window.Height=800;
        var process=System.Diagnostics.Process.GetCurrentProcess();process.Refresh();
        long peak=process.WorkingSet64;var clock=System.Diagnostics.Stopwatch.StartNew();
        var gaps=new List<double>();
        using var sampling=new CancellationTokenSource();
        // Use wall-clock posts: headless DispatcherTimer produced no samples in the initial run.
        var wall=System.Diagnostics.Stopwatch.StartNew();
        var sampler=Task.Run(async ()=>
        {
            try
            {
                while(!sampling.IsCancellationRequested)
                {
                    await Task.Delay(100,sampling.Token);
                    process.Refresh();peak=Math.Max(peak,process.WorkingSet64);
                    var posted=wall.Elapsed.TotalMilliseconds;
                    Avalonia.Threading.Dispatcher.UIThread.Post(()=>gaps.Add(wall.Elapsed.TotalMilliseconds-posted));
                }
            }
            catch(OperationCanceledException) { }
        });
        await vm.RunToDayAsync();clock.Stop();
        sampling.Cancel();await sampler;await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(()=>{});
        Require(gaps.Count>0,"Wall-clock dispatcher latency samples available");
        Require(gaps.Max()<2000,"Dispatcher remains responsive within 2 seconds during soak");
        Require(vm.Day==2000,"Soak reached 2000 days");
        var runSeconds=clock.Elapsed.TotalSeconds;
        var file=Path.Combine(Path.GetTempPath(),"FLOW-RC1-soak.json");clock.Restart();
        await Simulation.Infrastructure.LiveSessionJson.SaveAsync(file,vm.Live!);clock.Stop();var saveSeconds=clock.Elapsed.TotalSeconds;
        clock.Restart();var loaded=await Simulation.Infrastructure.LiveSessionJson.LoadAsync(file);clock.Stop();var reloadSeconds=clock.Elapsed.TotalSeconds;
        Require(Simulation.Infrastructure.LiveSessionJson.Save(vm.Live!)==Simulation.Infrastructure.LiveSessionJson.Save(loaded),"Soak save/reload preserves entire session");
        vm.Live!.Step();loaded.Step();
        Require(Simulation.Infrastructure.LiveSessionJson.Save(vm.Live)==Simulation.Infrastructure.LiveSessionJson.Save(loaded),"Soak deterministic next-day continuation");
        process.Refresh();peak=Math.Max(peak,process.WorkingSet64);
        var result=new{days=2000,arrivalPerDay=1,runSeconds,representativePeakWorkingSetBytes=peak,
            savedBytes=new FileInfo(file).Length,saveSeconds,reloadSeconds,dispatcherSamples=gaps.Count,
            maximumDispatcherLatencyMs=gaps.DefaultIfEmpty(0).Max(),meanDispatcherLatencyMs=gaps.DefaultIfEmpty(0).Average(),
            mode=Headless?"headless":"native Avalonia software rendering",note="UI dispatcher wall-clock post latency and process working-set samples, not physical input latency or absolute peak memory. Includes startup/resize rendering and save/reload memory.",sessionFile=file};
        Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"soak.json"),System.Text.Json.JsonSerializer.Serialize(result,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("SOAK "+System.Text.Json.JsonSerializer.Serialize(result));
    }
    private static void Require(bool condition,string message) {if(!condition)throw new InvalidOperationException(message);Console.WriteLine("PASS: "+message);}
    private static void Save(Control control,string name)
    {
        control.UpdateLayout();using var bitmap=new RenderTargetBitmap(new((int)Math.Ceiling(control.Bounds.Width),(int)Math.Ceiling(control.Bounds.Height)));
        bitmap.Render(control);Directory.CreateDirectory(Path.Combine(Output,"screenshots"));bitmap.Save(Path.Combine(Output,"screenshots",name+".png"));
    }
}
