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
    private static string Output => "docs/verification/cost-model-b5-2" + (Headless ? "/headless" : "");
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
        var liveView=window.GetVisualDescendants().OfType<LiveView>().Single();
        var economics=liveView.GetVisualDescendants().OfType<Expander>().Single(e=>e.Name=="EconomicSummary");
        economics.IsExpanded=true;await Task.Delay(100);
        Require(vm.Performance!.Economics.TotalTeamCost==1000000m,"Initial twenty-day staffing cost");
        Require(vm.EconomicTrend.Points.Count==100,"Existing chart infrastructure receives monetary trend");
        Save(economics,"live-economics");
        vm.ChangeCommand.Execute(null);await Task.Delay(100);
        var cost=window.GetVisualDescendants().OfType<TextBox>().Single(t=>t.DataContext is SimpleChangeField f && f.Label=="Tester monthly cost · SEK");
        cost.Text="90000";cost.Focus();await Task.Delay(100);
        Require(vm.Draft.TesterMonthlyCost=="90000","Existing intervention editor binds cost rates");
        Save(window,"role-cost-intervention");
        vm.ApplyCommand.Execute(null);vm.Step();
        Require(live.Session.Days[99].Economics!.Rates.TesterMonthly==75000m && live.Session.Days[100].Economics!.Rates.TesterMonthly==90000m,"Rate effective day preserves historical price");
        economics.IsExpanded=false;
        vm.BeginGuidedExperiment();vm.ChangeFields.Single(f=>f.Label=="Testers").Value="3";vm.ApplyCommand.Execute(null);await vm.BaselineTask;
        Require(vm.Day==101,"Baseline does not advance LIVE");
        for(int i=0;i<20;i++)vm.Step();
        Require(vm.GuidedProgress.StartsWith("Complete"),"Cost comparison waits for LIVE");
        Require(vm.GuidedTeamCost[0].Value==1030000 && vm.GuidedTeamCost[1].Value==1120000,"Same-checkpoint monetary branches");
        Require(vm.GuidedRows.Any(r=>r.Metric=="Cost · capacity") && vm.GuidedRows.Any(r=>r.Metric=="Team cost · SEK"),"Capacity and SEK remain separate");
        vm.GuidedDetailsCommand.Execute(null);await Task.Delay(100);
        var detail=window.GetVisualDescendants().OfType<GuidedComparisonView>().First(v=>v.IsEffectivelyVisible);
        Save(detail,"economic-comparison");
        var saved=Simulation.Infrastructure.LiveSessionJson.Save(live);
        var restored=Simulation.Infrastructure.LiveSessionJson.Load(saved);
        Require(restored.Session.Days[^1].Economics==live.Session.Days[^1].Economics,"Monetary observation survives save/load");
        vm.GuidedReturnCommand.Execute(null);vm.Load(restored);economics.IsExpanded=true;await Task.Delay(100);Save(economics,"loaded-economics");
        Console.WriteLine("PASS: native LIVE costs and trend, existing rate editor, Day N+1 pricing, unchanged historical rates, matched economic experiment and save/load.");
    }
    private static void Require(bool condition, string message) { if(!condition) throw new InvalidOperationException(message); }
    private static void Save(Control control, string name)
    {
        control.UpdateLayout(); using var bitmap = new RenderTargetBitmap(new((int)Math.Ceiling(control.Bounds.Width), (int)Math.Ceiling(control.Bounds.Height)));
        bitmap.Render(control); Directory.CreateDirectory(Path.Combine(Output,"screenshots")); bitmap.Save(Path.Combine(Output,"screenshots", name+".png"));
    }
}
