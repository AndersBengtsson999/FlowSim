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
    private static string Output => "docs/verification/game-view-b6-3-2" + (Headless ? "/headless" : "");
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
        var request=new SimulationRequest { Name="B6.3.2 recurring acceptance",SimulationDays=200,NumberOfWorkItems=40,
            DeveloperCount=5,TesterCount=2,DevelopmentEffort=1,CodeReviewEffort=1,TestingEffort=1,
            ArrivalMode=Simulation.Core.WorkArrivalMode.ContinuousArrival,WorkItemsPerDay=2,
            Release=new(Simulation.Core.ReleaseMode.Scheduled,8,10),
            ProductLaunch=new("Version 1.0",10,6,Simulation.Core.LaunchPlanUnit.Sprints){Mode=Simulation.Core.LaunchPlanningMode.Recurring} };
        var live=LiveSimulation.Start(request,request.ArrivalMode,request.WorkItemsPerDay);
        vm.Load(live);main.Simple.OpenLiveCommand.Execute(null);vm.Visualization="Game View";vm.TargetDay="200";
        await Task.Delay(100);window.UpdateLayout();
        var game=window.GetVisualDescendants().OfType<GameView>().Single(v=>v.IsEffectivelyVisible);
        for(var attempt=0;attempt<30 && !game.GetVisualDescendants().OfType<ProductLaunchView>().Any();attempt++) { await Task.Delay(100);window.UpdateLayout(); }
        var launchView=game.GetVisualDescendants().OfType<ProductLaunchView>().Single();
        var details=launchView.GetVisualDescendants().OfType<Expander>().Single();
        foreach(var day in new[]{60,120,180})
        {
            await vm.RunToDayAsync();
            Require(vm.Day==day && vm.LaunchDecisionPending && !vm.CanResume,"Exact recurring boundary");
            Require(vm.ProductCompactSummary.Contains("Recurring"),"Recurring mode visible");
            await Task.Delay(100);Save(game,$"pending-day-{day}");
            vm.LaunchProductCommand.Execute(null);
            Require(vm.LaunchNotices.Current?.Day==day,"Separate customer launch notification");
            Require(live.Session.ProductDelivery.Versions[^1].PlannedDay==day+60,"Successor launch anchored to actual day");
            await Task.Delay(200);Save(game,$"launch-day-{day}");vm.LaunchNotices.DismissCommand.Execute(null);
        }
        await vm.RunToDayAsync();Require(vm.Day==200,"Continuous advance after third launch");
        Require(live.Session.ProductDelivery.CustomerLaunches.Count==3,"Three permanent launches");
        details.IsExpanded=true;await Task.Delay(100);
        var tabs=launchView.GetVisualDescendants().OfType<TabControl>().Single();tabs.SelectedIndex=2;await Task.Delay(100);Save(game,"three-launch-history");
        tabs.SelectedIndex=1;await Task.Delay(100);Save(game,"three-launch-economics");
        var c=vm.LaunchCostHistory.Current;
        Require(Math.Abs(c.Incurred-c.WorkInProgress-c.AwaitingLaunch-c.CustomerLaunched-c.Unallocated)<.000001m,"Economic reconciliation");
        window.Width=980;window.Height=820;details.IsExpanded=false;await Task.Delay(100);Save(game,"recurring-narrow");
        var saved=Simulation.Infrastructure.LiveSessionJson.Save(live);vm.Load(Simulation.Infrastructure.LiveSessionJson.Load(saved));
        Require(vm.ProductCompactSummary.Contains("240") && !vm.LaunchNotices.HasNotification,"Load retains future schedule without replaying notices");
        var automatic=new SimulationRunner().Run(request);
        Require(automatic.ProductDelivery!.CustomerLaunches.Select(l=>l.Day).SequenceEqual(new[]{60,120,180}),"Run recurring launch sequence");
        var notices=new CustomerLaunchPresentation();notices.Observe(automatic.ProductDelivery);
        foreach(var day in new[]{60,120,180}) {Require(notices.Current?.Day==day,"Unattended event queue");notices.DismissCommand.Execute(null);}
        var world=game.GetVisualDescendants().OfType<GameWorldControl>().Single();world.SelectedStation="Testing";world.ZoomBy(1.2);world.FitCamera();Require(world.SelectedStation=="Testing","Station and camera retained");
        var postponed=LiveSimulation.Start(request,request.ArrivalMode,request.WorkItemsPerDay);vm.Load(postponed);
        await vm.RunToDayAsync();vm.LaunchDelay="2";vm.PlanningUnit="Sprints";vm.PlanningSprintLength="10";vm.PlanLaunchCommand.Execute(null);
        await vm.RunToDayAsync();Require(vm.Day==80,"Postponed exact boundary");vm.LaunchProductCommand.Execute(null);
        Require(postponed.Session.ProductDelivery.Versions[^1].PlannedDay==140,"Next interval uses actual day 80");
        Console.WriteLine((Headless?"Headless":"Native")+" PASS: recurring LIVE stops 60/120/180; separate notices; next target 240; 200 continuous days; history/economics; narrow layout; save/load; automatic Run 60/120/180; station/Fit; postpone 60 to 80 then next 140.");
    }

    private static void Require(bool condition, string message) { if(!condition) throw new InvalidOperationException(message); }
    private static void Save(Control control, string name)
    {
        control.UpdateLayout(); using var bitmap = new RenderTargetBitmap(new((int)Math.Ceiling(control.Bounds.Width), (int)Math.Ceiling(control.Bounds.Height)));
        bitmap.Render(control); Directory.CreateDirectory(Path.Combine(Output,"screenshots")); bitmap.Save(Path.Combine(Output,"screenshots", name+".png"));
    }
}
