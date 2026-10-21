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
    private static string Output => "docs/verification/game-view-b6-3-1" + (Headless ? "/headless" : "");
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
        var request = new SimulationRequest { Name="B6.3 acceptance", DeveloperCount=5,TesterCount=2,DevelopmentEffort=1,CodeReviewEffort=1,TestingEffort=1,
            NumberOfWorkItems=50, SimulationDays=100,ArrivalMode=Simulation.Core.WorkArrivalMode.ContinuousArrival,WorkItemsPerDay=2,
            Release=new(Simulation.Core.ReleaseMode.Scheduled,3,10),ProductLaunch=new("Version 2.0",10,6,Simulation.Core.LaunchPlanUnit.Sprints) };
        var live=LiveSimulation.Start(request,request.ArrivalMode,request.WorkItemsPerDay);
        vm.Load(live);main.Simple.OpenLiveCommand.Execute(null);vm.Visualization="Game View";
        vm.TargetDay="200";await vm.RunToDayAsync();
        Require(vm.Day==60 && vm.LaunchDecisionPending && !vm.CanResume,"Accelerated LIVE stops after day 60");
        Require(live.Session.ProductDelivery.InternalReleases.Count>1 && live.Session.GetResult().CustomerDelivery.LaunchedItems==0,"Internal releases accumulate before customer delivery");
        await Task.Delay(150);window.UpdateLayout();
        var game=window.GetVisualDescendants().OfType<GameView>().Single(v=>v.IsEffectivelyVisible);
        var launchView=game.GetVisualDescendants().OfType<ProductLaunchView>().Single();
        var details=launchView.GetVisualDescendants().OfType<Expander>().Single();
        Require(!details.IsExpanded && launchView.Bounds.Height <= 60,"Compact collapsed product toolbar");
        Require(vm.ProductCompactSummary.Contains("DECISION REQUIRED"),"Collapsed summary exposes pending decision");
        Save(game,"compact-day-60");
        details.IsExpanded=true;await Task.Delay(100);window.UpdateLayout();
        var launchButton=launchView.GetVisualDescendants().OfType<Button>().Single(b=>b.Content as string=="Launch Now");
        Require(launchButton.IsEffectivelyVisible && launchButton.IsEnabled,"Expanded launch controls accessible");
        var tabs=launchView.GetVisualDescendants().OfType<TabControl>().Single();tabs.SelectedIndex=1;await Task.Delay(100);
        var before=vm.LaunchCostHistory.Current;Require(before.AwaitingLaunch>0 && before.WorkInProgress>0,"Allocated awaiting and WIP cost");
        Save(game,"economics-before-launch");
        vm.CheckpointCommand.Execute(null);vm.SelectedCheckpoint=vm.Checkpoints.Last();
        vm.LaunchProductCommand.Execute(null);Require(live.Session.ProductDelivery.CustomerLaunches.Single().Day==60,"Manual launch on day 60");
        Require(live.Session.GetResult().CustomerDelivery.LaunchedItems>0,"Customer metrics after launch");
        Require(vm.LaunchNotices.HasNotification,"Manual customer launch notification");
        var after=vm.LaunchCostHistory.Current;Require(after.Incurred==before.Incurred && after.CustomerLaunched==before.AwaitingLaunch,"Launch reclassifies without expenditure");
        await Task.Delay(350);Save(game,"customer-launch-economics");
        details.IsExpanded=false;await Task.Delay(80);Save(game,"launch-notice");
        vm.LaunchNotices.DismissCommand.Execute(null);Require(!vm.LaunchNotices.HasNotification,"Dismiss notification");
        vm.RestoreCommand.Execute(null);vm.LaunchDelay="2";vm.PlanningUnit="Sprints";vm.PlanningSprintLength="10";vm.PlanLaunchCommand.Execute(null);
        Require(live.Session.ProductDelivery.Versions[0].PlannedDay==80,"Postpone by two sprints targets day 80");
        await vm.RunToDayAsync();Require(vm.Day==80 && vm.LaunchDecisionPending,"Second exact stop");
        Require(live.Session.Days.Select(d=>d.Day).SequenceEqual(Enumerable.Range(0,80)),"No repeated or skipped days");
        window.Width=980;window.Height=820;await Task.Delay(120);Save(game,"compact-narrow");
        details.IsExpanded=true;tabs.SelectedIndex=1;await Task.Delay(120);Save(game,"economics-narrow");
        var saved=Simulation.Infrastructure.LiveSessionJson.Save(live);vm.Load(Simulation.Infrastructure.LiveSessionJson.Load(saved));
        Require(vm.LaunchDecisionPending && !vm.CanResume,"Loaded pending decision remains blocked");
        vm.AnimateGameActivity=false;vm.LaunchProductCommand.Execute(null);Require(vm.LaunchNotices.HasNotification,"Text confirmation without animation");vm.Step();Require(vm.Day==81,"Resume after resolved decision");
        var automatic=new SimulationRunner().Run(request);Require(automatic.ProductDelivery!.CustomerLaunches.Single().Day==60,"Unattended Run launches on day 60");
        Require(automatic.CustomerDelivery.LaunchedItems>0,"Run customer metrics distinct");
        var unattended = new CustomerLaunchPresentation();unattended.Observe(automatic.ProductDelivery);Require(unattended.HasNotification,"Automatic launches presented after run");
        var world=game.GetVisualDescendants().OfType<GameWorldControl>().Single();world.SelectedStation="Testing";world.ZoomBy(1.2);world.FitCamera();Require(world.SelectedStation=="Testing","Inspection and Fit preserved");
        var panel=game.GetVisualDescendants().OfType<KpiDashboardView>().Single();vm.OpenExplorer(TeamKpi.CycleTime);panel.OpenExpandedExplorer();await Task.Delay(100);
        var desktop=(IClassicDesktopStyleApplicationLifetime)Avalonia.Application.Current!.ApplicationLifetime!;desktop.Windows.Single(w=>w.Title=="FLOW · KPI Explorer").Close();
        vm.Reset();Require(!vm.LaunchDecisionPending,"Reset clears decision");
        Console.WriteLine((Headless?"Headless":"Native")+" PASS: compact toolbar, expanded economics, allocation reconciliation, notification/dismissal, disabled animation confirmation, automatic launch presentation; initial 6 × 10 = Day 60; internal releases and zero pre-launch customer delivery; exact accelerated stop; launch at 60; postpone to 80; pending save/load; continuation at 81; Run auto-launch at 60; narrow layout; station/Fit/Explorer preserved.");
    }

    private static void Require(bool condition, string message) { if(!condition) throw new InvalidOperationException(message); }
    private static void Save(Control control, string name)
    {
        control.UpdateLayout(); using var bitmap = new RenderTargetBitmap(new((int)Math.Ceiling(control.Bounds.Width), (int)Math.Ceiling(control.Bounds.Height)));
        bitmap.Render(control); Directory.CreateDirectory(Path.Combine(Output,"screenshots")); bitmap.Save(Path.Combine(Output,"screenshots", name+".png"));
    }
}
