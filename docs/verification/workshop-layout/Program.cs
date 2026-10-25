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
    private static string Output => "docs/verification/workshop-layout/after" + (Headless ? "/headless" : "");
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
        foreach(var size in new[]{new Size(1440,900),new Size(1280,800),new Size(980,800)})
        {
            window.Width=size.Width;window.Height=size.Height;vm.ProductDetailsExpanded=false;
            await Task.Delay(180);Save(window,$"game-{size.Width}-{size.Height}");
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
    }
    private static void Require(bool condition,string message) {if(!condition)throw new InvalidOperationException(message);Console.WriteLine("PASS: "+message);}
    private static void Save(Control control,string name)
    {
        control.UpdateLayout();using var bitmap=new RenderTargetBitmap(new((int)Math.Ceiling(control.Bounds.Width),(int)Math.Ceiling(control.Bounds.Height)));
        bitmap.Render(control);Directory.CreateDirectory(Path.Combine(Output,"screenshots"));bitmap.Save(Path.Combine(Output,"screenshots",name+".png"));
    }
}
