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
    private static string Output => "docs/verification/kpi-explorer-redesign" + (Headless ? "/headless" : "");
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
        var live=LiveSimulation.Start(new(){NumberOfWorkItems=40,DeveloperCount=5,TesterCount=2,DevelopmentEffort=1,CodeReviewEffort=1,TestingEffort=1},Simulation.Core.WorkArrivalMode.ContinuousArrival,2);
        for(var n=0;n<30;n++)live.Step();
        live.Session.LaunchProductVersion("V1");for(var n=0;n<10;n++)live.Step();
        vm.Load(live);main.Simple.OpenLiveCommand.Execute(null);vm.Visualization="Game View";
        await Task.Delay(250);
        var dashboard=window.GetVisualDescendants().OfType<GameView>().Single(v=>v.IsEffectivelyVisible).GetVisualDescendants().OfType<KpiDashboardView>().First(v=>v.IsEffectivelyVisible);
        vm.OpenExplorer(TeamKpi.UnlaunchedDevelopmentCost);dashboard.OpenExpandedExplorer();await Task.Delay(200);
        var desktop=(IClassicDesktopStyleApplicationLifetime)Current!.ApplicationLifetime!;
        var explorer=desktop.Windows.Single(w=>w.Title=="FLOW · KPI Explorer");
        var view=explorer.GetVisualDescendants().OfType<KpiDashboardView>().Single();
        var state=System.Text.Json.JsonSerializer.Serialize(live.Session.Capture());
        Require(vm.ExploreKpi,"Explore is default");
        Require(!view.FindControl<ContentControl>("SecondaryMetric")!.IsEffectivelyVisible,"Comparison hidden in Explore");
        var chart=view.GetVisualDescendants().OfType<LaunchCostChart>().First(c=>c.IsEffectivelyVisible);
        Require(chart.History!.Points.Any(p=>p.CustomerLaunch),"Customer launch marker retained");
        Require(chart.DescribeAtDay(30).Contains("Customer launch"),"Launch hover details retained");
        Require(view.GetVisualDescendants().OfType<Expander>().Where(e=>e.IsEffectivelyVisible).All(e=>!e.IsExpanded),"Details collapsed by default");
        Save(explorer,"explore-balance");
        var scroll=view.FindControl<ScrollViewer>("ExplorerScroll")!;
        scroll.Offset=new Vector(0,150);await Task.Delay(60);var offset=scroll.Offset;
        var existingChart=chart;
        vm.KpiWindow=10;await Task.Delay(80);
        Require(ReferenceEquals(existingChart,view.GetVisualDescendants().OfType<LaunchCostChart>().First(c=>c.IsEffectivelyVisible)),"Chart control reused on data update");
        Require(Math.Abs(scroll.Offset.Y-offset.Y)<1,"Scroll retained on window update");
        vm.ExplorerMode="Compare KPIs";vm.ComparisonKpi=vm.ExplorerOptions.Single(d=>d.Id==TeamKpi.ReleasedThroughput);
        explorer.Width=1200;explorer.Height=900;scroll.Offset=default;await Task.Delay(200);
        Require(view.FindControl<ContentControl>("SecondaryMetric")!.IsEffectivelyVisible,"Comparison chart visible");
        Require(vm.ExplorerPrimary!.Unit!=vm.ExplorerSecondary!.Unit,"Different units retained");
        Save(explorer,"compare-kpis");
        vm.ExplorerMode="Explore one KPI";vm.PrimaryKpi=vm.ExplorerOptions.Single(d=>d.Id==TeamKpi.CycleTime);
        explorer.Width=860;await Task.Delay(150);Save(explorer,"explore-period");
        foreach(var size in new[]{new Size(620,700),new Size(860,800),new Size(1200,850)}) {
            explorer.Width=size.Width;explorer.Height=size.Height;await Task.Delay(150);
            Require(scroll.Bounds.Width<=explorer.ClientSize.Width,"Scroll fits window width");
            Require(view.FindControl<Button>("CloseExplorerButton")!.IsEffectivelyVisible,"Compact return accessible");
        }
        explorer.Width=620;explorer.Height=700;await Task.Delay(100);Save(explorer,"explore-narrow");
        var calculations=view.GetVisualDescendants().OfType<Expander>().First(e=>e.IsEffectivelyVisible && e.Header?.ToString()=="How it's calculated");
        calculations.IsExpanded=true;Require(calculations.IsExpanded,"Calculation disclosure works");
        vm.ExplorerMode="Compare Scenarios";await Task.Delay(80);Require(vm.CompareScenarios,"Scenario comparison remains available");
        Require(state==System.Text.Json.JsonSerializer.Serialize(live.Session.Capture()),"Exploration does not change simulation state");
        view.FindControl<Button>("CloseExplorerButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(80);
        Require(!vm.ExplorerExpanded && !explorer.IsVisible,"Return to compact closes expanded window");
    }

    private static void Require(bool condition,string message) {if(!condition)throw new InvalidOperationException(message);Console.WriteLine("PASS: "+message);}
    private static void Save(Control control,string name)
    {
        control.UpdateLayout();using var bitmap=new RenderTargetBitmap(new((int)Math.Ceiling(control.Bounds.Width),(int)Math.Ceiling(control.Bounds.Height)));
        bitmap.Render(control);Directory.CreateDirectory(Path.Combine(Output,"screenshots"));bitmap.Save(Path.Combine(Output,"screenshots",name+".png"));
    }
}
