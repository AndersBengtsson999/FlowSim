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
    private static string Output => "docs/verification/game-view-b6-5-1" + (Headless ? "/headless" : "");
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
    private static async Task Verify(MainWindowViewModel main, Window window)
    {
        Require(window.ActualThemeVariant==ThemeVariant.Dark,"Permanent dark window");
        foreach(var key in new[]{"FlowApp","FlowSidebar","FlowPanel","FlowText","FlowAccent","FlowError"})
            Require(Avalonia.Application.Current!.TryFindResource(key,out _),"Shared resource "+key);
        var vm=main.Simple.Live;
        var acceptance=LiveSimulation.Start(new(){NumberOfWorkItems=40,DeveloperCount=5,TesterCount=1,
            DevelopmentEffort=1,CodeReviewEffort=1,TestingEffort=2,Release=new(Simulation.Core.ReleaseMode.Scheduled,8,10)},Simulation.Core.WorkArrivalMode.ContinuousArrival,2);
        for(int i=0;i<42;i++) acceptance.Step();
        vm.Load(acceptance);main.Simple.OpenLiveCommand.Execute(null);vm.Visualization="Game View";
        await Task.Delay(300);window.UpdateLayout();
        var office=window.GetVisualDescendants().OfType<GameView>().Single(v=>v.IsEffectivelyVisible);
        var canvas=office.GetVisualDescendants().OfType<GameWorldControl>().Single();
        var balance=vm.LaunchCostHistory.Current;
        Require(balance.WorkInProgress>0 && balance.AwaitingLaunch>0,"Day42 has WIP and awaiting allocation");
        Require(vm.ReleaseEconomicContext!.AwaitingCost==balance.AwaitingLaunch,"Inspection excludes WIP");
        Save(window,"manual-day42");
        var hit=typeof(GameWorldControl).GetMethod("StationAt",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
        var van=Simulation.UI.Controls.GameWorld.VehicleInspection.Bounds(null);
        Require((string?)hit.Invoke(canvas,new object[]{canvas.Camera.ToScreen(van.Center)})=="Release","Vehicle hit opens release inspection");
        canvas.SelectedStation="Testing queue";await Task.Delay(100);Save(window,"testing-day42");
        canvas.SelectedStation="Release";await Task.Delay(150);
        Require(office.FindControl<TextBlock>("ReleaseEconomics")!.IsVisible,"Vehicle economics visible");
        Save(window,"release-day42");Save(office.FindControl<Border>("StationPanel")!,"release-inspection");
        canvas.ZoomBy(1.2);var zoom=canvas.Camera.Zoom;
        acceptance.Step();vm.Load(acceptance);await Task.Delay(100);
        Require(canvas.Camera.Zoom==zoom,"LIVE refresh retains manual zoom");
        canvas.FitCamera();canvas.SelectedStation=null;
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
            await Task.Delay(100);Save(window,$"game-pending-day-{day}");
            var beforeLaunch=vm.LaunchCostHistory.Current;
            Require(vm.UnlaunchedCostCard!.Observation.Value==(double)beforeLaunch.Unlaunched,"Pinned KPI equals authoritative balance");
            vm.LaunchProductCommand.Execute(null);
            var afterLaunch=vm.LaunchCostHistory.Current;
            Require(afterLaunch.Incurred==beforeLaunch.Incurred && afterLaunch.WorkInProgress==beforeLaunch.WorkInProgress,"Launch leaves expense and WIP unchanged");
            Require(afterLaunch.Unlaunched==beforeLaunch.Unlaunched-beforeLaunch.AwaitingLaunch,"Launch removes only included allocated cost");
            Require(vm.LaunchNotices.Current?.Day==day,"Separate customer launch notification");
            Require(live.Session.ProductDelivery.Versions[^1].PlannedDay==day+60,"Successor launch anchored to actual day");
            await Task.Delay(200);Save(window,$"launch-day-{day}");vm.LaunchNotices.DismissCommand.Execute(null);
        }
        await vm.RunToDayAsync();Require(vm.Day==200,"Continuous advance after third launch");
        Require(live.Session.ProductDelivery.CustomerLaunches.Count==3,"Three permanent launches");
        details.IsExpanded=true;await Task.Delay(100);
        var tabs=launchView.GetVisualDescendants().OfType<TabControl>().Single();tabs.SelectedIndex=2;await Task.Delay(100);Save(game,"three-launch-history");
        tabs.SelectedIndex=1;await Task.Delay(100);Save(game,"three-launch-economics");
        var c=vm.LaunchCostHistory.Current;
        Require(Math.Abs(c.Incurred-c.WorkInProgress-c.AwaitingLaunch-c.CustomerLaunched-c.Unallocated)<.000001m,"Economic reconciliation");
        window.Width=980;window.Height=820;details.IsExpanded=false;await Task.Delay(100);Save(game,"recurring-narrow");
        var worldBefore=game.GetVisualDescendants().OfType<GameWorldControl>().Single();worldBefore.SelectedStation="Testing queue";await Task.Delay(150);Save(window,"inspection");
        var context=game.FindControl<ItemsControl>("ContextKpis")!;
        Require(context.ItemsSource!.Cast<KpiCard>().Any(c=>c.Observation.Definition.Id==TeamKpi.TesterUtilization),"Testing KPI mapping");
        vm.KpiWindow=40;await Task.Delay(50);Require(context.ItemsSource!.Cast<KpiCard>().All(c=>c.Observation.Window==40),"Context follows selected KPI window");vm.KpiWindow=20;
        var related=game.GetVisualDescendants().OfType<Expander>().Single(e=>e.Header?.ToString()=="Related KPIs · open Explorer");
        related.IsExpanded=true;await Task.Delay(100);Save(window,"context-kpis");Save(game.FindControl<Border>("StationPanel")!,"station-context");
        var list=game.FindControl<ListBox>("InspectionList")!;
        Require(list.Bounds.Height>=80,"Work-item inspection remains accessible");
        var kpiButton=context.GetVisualDescendants().OfType<Button>().First();kpiButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(100);
        var contextExplorer=((IClassicDesktopStyleApplicationLifetime)Avalonia.Application.Current!.ApplicationLifetime!).Windows.Single(w=>w.Title=="FLOW · KPI Explorer");
        Require(worldBefore.SelectedStation=="Testing queue","KPI navigation preserves queue selection");contextExplorer.Close();vm.CloseExplorer();
        related.IsExpanded=false;
        foreach(var size in new[]{new Size(980,720),new Size(1440,1000),new Size(1920,1080)})
        {
            window.Width=size.Width;window.Height=size.Height;await Task.Delay(150);Save(window,$"layout-{size.Width}");
            Require(game.FindControl<Border>("WorldViewport")!.Bounds.Height>200,"Usable workshop viewport");
            Require(list.Bounds.Height>=80,"Usable item list after resize");
            Require(worldBefore.SelectedStation=="Testing queue","Resize preserves queue selection");
        }
        window.Width=980;window.Height=820;
        vm.SelectKpi(TeamKpi.ReworkEffort,false);vm.SelectKpi(TeamKpi.WorkVolumePerSek,true);
        vm.OpenExplorer(TeamKpi.UnlaunchedDevelopmentCost);vm.ComparisonKpi=vm.ExplorerOptions.Single(d=>d.Id==TeamKpi.WorkVolumePerSek);
        var dashboard=game.GetVisualDescendants().OfType<KpiDashboardView>().Single();dashboard.OpenExpandedExplorer();await Task.Delay(150);
        var explorer=((IClassicDesktopStyleApplicationLifetime)Avalonia.Application.Current!.ApplicationLifetime!).Windows.Single(w=>w.Title=="FLOW · KPI Explorer");
        Require(explorer.ActualThemeVariant==ThemeVariant.Dark,"Detached Explorer dark");Save(explorer,"kpi-explorer");
        Require(vm.ExplorerPrimary!.IsBalance && vm.ExplorerSecondary!.Unit.Contains("1,000"),"Stock and scaled ratio Explorer");
        explorer.Close();vm.CloseExplorer();
        worldBefore.SelectedStation=null;window.Width=1440;window.Height=1000;await Task.Delay(100);Save(window,"compact-game");Save(dashboard,"kpi-cards");VerifyCards(dashboard);
        window.Width=980;await Task.Delay(100);Save(dashboard,"kpi-cards-narrow");VerifyCards(dashboard);
        vm.Visualization="Flow Board";await Task.Delay(150);Save(window,"flow-board");
        Require(window.GetVisualDescendants().OfType<KpiDashboardView>().Any(v=>v.IsEffectivelyVisible),"Shared Flow Board KPI component");
        vm.Visualization="Game View";await Task.Delay(100);
        var help=window.GetVisualDescendants().OfType<Button>().First(b=>b.IsEffectivelyVisible && b.Flyout is not null);
        help.Flyout!.ShowAt(help);await Task.Delay(150);Save(window,"flyout");
        if(help.Flyout is MenuFlyout menu && menu.Items.OfType<MenuItem>().FirstOrDefault() is { } item && TopLevel.GetTopLevel(item) is { } popup)
        { Require(popup.ActualThemeVariant==ThemeVariant.Dark,"Menu popup dark");Save(popup,"menu-popup"); }
        help.Flyout.Hide();
        var combo=window.GetVisualDescendants().OfType<ComboBox>().First(c=>c.IsEffectivelyVisible);
        combo.IsDropDownOpen=true;await Task.Delay(150);
        if(combo.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.Popup>().FirstOrDefault()?.Child is { } popupChild && TopLevel.GetTopLevel(popupChild) is { } dropdown)
        { Require(dropdown.ActualThemeVariant==ThemeVariant.Dark,"Dropdown dark");Save(dropdown,"dropdown-popup"); }
        combo.IsDropDownOpen=false;
        var saved=Simulation.Infrastructure.LiveSessionJson.Save(live);vm.Load(Simulation.Infrastructure.LiveSessionJson.Load(saved));
        Require(vm.ProductCompactSummary.Contains("240") && !vm.LaunchNotices.HasNotification,"Load retains future schedule without replaying notices");
        var automatic=new SimulationRunner().Run(request);
        Require(automatic.ProductDelivery!.CustomerLaunches.Select(l=>l.Day).SequenceEqual(new[]{60,120,180}),"Run recurring launch sequence");
        var notices=new CustomerLaunchPresentation();notices.Observe(automatic.ProductDelivery);
        foreach(var day in new[]{60,120,180}) {Require(notices.Current?.Day==day,"Unattended event queue");notices.DismissCommand.Execute(null);}
        var world=game.GetVisualDescendants().OfType<GameWorldControl>().Single();world.SelectedStation="Testing queue";world.ZoomBy(1.2);world.FitCamera();Require(world.SelectedStation=="Testing queue","Station and camera retained");
        var postponed=LiveSimulation.Start(request,request.ArrivalMode,request.WorkItemsPerDay);vm.Load(postponed);
        await vm.RunToDayAsync();vm.LaunchDelay="2";vm.PlanningUnit="Sprints";vm.PlanningSprintLength="10";vm.PlanLaunchCommand.Execute(null);
        await vm.RunToDayAsync();Require(vm.Day==80,"Postponed exact boundary");vm.LaunchProductCommand.Execute(null);
        Require(postponed.Session.ProductDelivery.Versions[^1].PlannedDay==140,"Next interval uses actual day 80");
        Console.WriteLine((Headless?"Headless":"Native")+" PASS: recurring LIVE stops 60/120/180; separate notices; next target 240; 200 continuous days; history/economics; narrow layout; save/load; automatic Run 60/120/180; station/Fit; postpone 60 to 80 then next 140.");
    }

    private static void VerifyCards(KpiDashboardView dashboard)
    {
        var cards=dashboard.GetVisualDescendants().OfType<Button>().Where(b=>b.IsEffectivelyVisible && b.DataContext is KpiCard).ToArray();
        Require(cards.Length==5,"Pinned balance plus four chosen cards");
        foreach(var card in cards)
        {
            var origin=card.TranslatePoint(default,dashboard)!.Value;
            Require(origin.X>=0 && origin.X+card.Bounds.Width<=dashboard.Bounds.Width,"No horizontally clipped cards");
        }
    }
    private static void Require(bool condition, string message) { if(!condition) throw new InvalidOperationException(message); }
    private static void Save(Control control, string name)
    {
        control.UpdateLayout(); using var bitmap = new RenderTargetBitmap(new((int)Math.Ceiling(control.Bounds.Width), (int)Math.Ceiling(control.Bounds.Height)));
        bitmap.Render(control); Directory.CreateDirectory(Path.Combine(Output,"screenshots")); bitmap.Save(Path.Combine(Output,"screenshots", name+".png"));
    }
}
