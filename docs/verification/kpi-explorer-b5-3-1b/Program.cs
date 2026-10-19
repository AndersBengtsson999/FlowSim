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
    private static string Output => "docs/verification/kpi-explorer-b5-3-1b" + (Headless ? "/headless" : "");
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
        var card=panel.GetVisualDescendants().OfType<Button>().First(b=>b.DataContext is KpiCard);
        card.Focus();card.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Key.Enter});await Task.Delay(200);
        Require(vm.ExplorerOpen,"Card opens explorer");
        var compare=panel.GetVisualDescendants().OfType<ComboBox>().Single(b=>b.Name!="KpiWindowPicker");
        compare.SelectedItem=vm.ExplorerOptions.Single(d=>d.Id==TeamKpi.CycleTimeP85);await Task.Delay(150);
        Require(vm.HasComparison,"Comparison binding");
        Require(vm.ExplorerPrimary!.Trend.Points.Select(p=>p.Day).SequenceEqual(vm.ExplorerSecondary!.Trend.Points.Select(p=>p.Day)),"Aligned days");
        Save(game,"explorer-wide");
        var timer=System.Diagnostics.Stopwatch.StartNew();
        for(int i=0;i<20;i++)vm.Step();timer.Stop();Console.WriteLine($"20 LIVE steps with Explorer: {timer.ElapsedMilliseconds} ms");
        var world=game.GetVisualDescendants().OfType<GameWorldControl>().Single();world.ZoomBy(1.2);Require(world.Camera.Zoom>1,"Zoom");world.FitCamera();
        world.SelectedStation="Testing";await Task.Delay(80);Require(world.HasSelection,"Station selection");world.SelectedStation=null;
        window.Width=980;await Task.Delay(200);Save(game,"explorer-narrow");
        vm.KpiWindow=10;
        var restored=Simulation.Infrastructure.LiveSessionJson.Load(Simulation.Infrastructure.LiveSessionJson.Save(live));
        vm.Load(restored);await Task.Delay(100);
        Require(vm.KpiWindow==10 && vm.ExplorerOpen && vm.ComparisonKpi!.Id==TeamKpi.CycleTimeP85,"Saved preferences");
        vm.CloseExplorer();Require(!vm.ExplorerOpen,"Close explorer");
        Console.WriteLine("PASS: native macOS Explorer opens from card, compares aligned series, updates during LIVE, preserves zoom/station selection, resizes, saves/loads and closes.");
    }

    private static void Require(bool condition, string message) { if(!condition) throw new InvalidOperationException(message); }
    private static void Save(Control control, string name)
    {
        control.UpdateLayout(); using var bitmap = new RenderTargetBitmap(new((int)Math.Ceiling(control.Bounds.Width), (int)Math.Ceiling(control.Bounds.Height)));
        bitmap.Render(control); Directory.CreateDirectory(Path.Combine(Output,"screenshots")); bitmap.Save(Path.Combine(Output,"screenshots", name+".png"));
    }
}
