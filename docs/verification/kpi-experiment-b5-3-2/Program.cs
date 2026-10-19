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
    private static string Output => "docs/verification/kpi-experiment-b5-3-2" + (Headless ? "/headless" : "");
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
        Require(vm.KpiCards.Count==4,"Default four cards");
        Require(vm.KpiCards.All(c=>c.Period.Contains("81–100")),"Twenty-day labels");
        Save(game,"dashboard-wide");
        var state=System.Text.Json.JsonSerializer.Serialize(live.Session.Capture());
        vm.OpenExplorer(TeamKpi.CycleTime);await Task.Delay(100);
        var modes=panel.GetVisualDescendants().OfType<ComboBox>().Single(b=>b.Name=="ExplorerModePicker");
        modes.SelectedItem="Compare Scenarios";await Task.Delay(100);Require(vm.CompareScenarios,"Mode binding");
        vm.EvaluationDays=40;vm.BeginGuidedExperiment();vm.Draft.NumberOfDevelopers="13";vm.ApplyChanges();
        var snapshot=System.Text.Json.JsonSerializer.Serialize(live.Session.Capture());await vm.BaselineTask;
        Require(snapshot==System.Text.Json.JsonSerializer.Serialize(live.Session.Capture()),"Baseline isolation");
        for(int i=0;i<20;i++)vm.Step();await Task.Delay(100);Save(game,"partial-scenario-comparison");
        Require(vm.ScenarioComparison!.ObservedDays==20,"Matched partial progress");
        var metric=panel.GetVisualDescendants().OfType<ComboBox>().Single(b=>b.Name=="ScenarioMetricPicker");
        metric.SelectedItem=vm.ScenarioOptions.Single(d=>d.Id==TeamKpi.TotalTeamCost);
        for(int i=0;i<20;i++)vm.Step();await Task.Delay(150);Save(game,"complete-scenario-comparison");
        var explorerScroll=panel.GetVisualDescendants().OfType<ScrollViewer>().Single(s=>s.MaxHeight==580);
        explorerScroll.Offset=new Vector(0,explorerScroll.Extent.Height);await Task.Delay(100);Save(panel,"economic-summary");
        explorerScroll.Offset=default;
        Require(vm.ScenarioRows.Count==7,"Summary rows");Require(vm.ScenarioComparison!.ObservedDays==40,"Completion");
        var chart=panel.GetVisualDescendants().OfType<LivePerformanceTrendChart>().Single(c=>c.ComparisonSeries is not null);
        Require(chart.DescribeAtDay(140).Contains("Difference:"),"Scenario tooltip");
        var world=game.GetVisualDescendants().OfType<GameWorldControl>().Single();world.ZoomBy(1.2);Require(world.Camera.Zoom>1,"Zoom");world.FitCamera();
        world.SelectedStation="Testing";await Task.Delay(100);Require(world.HasSelection,"Station selection");world.SelectedStation=null;
        modes.SelectedItem="Compare KPIs";Require(vm.CompareKpis,"Return to original mode");
        modes.SelectedItem="Compare Scenarios";
        vm.Load(Simulation.Infrastructure.LiveSessionJson.Load(Simulation.Infrastructure.LiveSessionJson.Save(live)));await Task.Delay(100);
        Require(vm.ScenarioRows.Count==0 && vm.ScenarioComparison!.Unavailable is not null,"Load invalidation");Save(game,"invalidated-comparison");
        Console.WriteLine("PASS: native scenario mode, isolated baseline, developer intervention, partial/completed comparison, metric binding, shared chart tooltip, zoom/stations, mode switching and load invalidation.");
    }

    private static void Require(bool condition, string message) { if(!condition) throw new InvalidOperationException(message); }
    private static void Save(Control control, string name)
    {
        control.UpdateLayout(); using var bitmap = new RenderTargetBitmap(new((int)Math.Ceiling(control.Bounds.Width), (int)Math.Ceiling(control.Bounds.Height)));
        bitmap.Render(control); Directory.CreateDirectory(Path.Combine(Output,"screenshots")); bitmap.Save(Path.Combine(Output,"screenshots", name+".png"));
    }
}
