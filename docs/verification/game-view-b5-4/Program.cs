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
    private static string Output => "docs/verification/game-view-b5-4" + (Headless ? "/headless" : "");
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
        Require(panel.LayoutOrientation==Avalonia.Layout.Orientation.Horizontal,"Compact dashboard uses a row");
        var world=game.GetVisualDescendants().OfType<GameWorldControl>().Single();
        world.SelectedStation="Testing";world.ZoomBy(1.2);world.Camera.Pan(new Vector(15,10));
        var zoom=world.Camera.Zoom;
        vm.OpenExplorer(TeamKpi.CycleTime);vm.ComparisonKpi=vm.ExplorerOptions.Single(d=>d.Id==TeamKpi.TotalTeamCost);
        panel.OpenExpandedExplorer();await Task.Delay(200);
        var desktop=(IClassicDesktopStyleApplicationLifetime)Avalonia.Application.Current!.ApplicationLifetime!;
        var explorer=desktop.Windows.Single(w=>w.Title=="FLOW · KPI Explorer");
        Require(vm.ExplorerExpanded && explorer.Owner==window,"Owned non-modal expanded view");
        Require(world.SelectedStation=="Testing" && world.Camera.Zoom==zoom,"Selection and camera preserved");
        Require(state==System.Text.Json.JsonSerializer.Serialize(live.Session.Capture()),"Presentation leaves simulation unchanged");
        Save(explorer,"expanded-kpis");
        explorer.Width=680;explorer.Height=680;window.Width=980;await Task.Delay(150);Save(explorer,"expanded-narrow");
        Require(world.Camera.Zoom==zoom,"Resize preserves zoom");
        vm.Step();Require(vm.ExplorerPrimary!.Observation.LastDay==101,"Expanded view updates with LIVE");
        vm.ExplorerMode="Compare Scenarios";await Task.Delay(80);explorer.Close();await Task.Delay(100);
        Require(!vm.ExplorerExpanded && vm.ExplorerMode=="Compare Scenarios","Return preserves mode");
        Require(world.SelectedStation=="Testing","Return preserves station");
        world.FitCamera();Require(world.Camera.Zoom==1 && !world.Camera.IsManual,"Fit camera");
        vm.CloseExplorer();world.SelectedStation=null;window.Width=1440;await Task.Delay(100);Save(game,"compact-workshop");
        var mainWindow=(MainWindow)window;
        Require(mainWindow.ExitCommand.CanExecute(null),"Exit enabled");
        Require(mainWindow.KeyBindings.Any(k=>k.Gesture?.Key==Key.Q && k.Gesture.KeyModifiers==KeyModifiers.Meta),"Cmd Q gesture");
        var beforeExit=System.Text.Json.JsonSerializer.Serialize(live.Session.Capture());
        var closeRequests=0;
        void CancelClose(object? sender,WindowClosingEventArgs e){closeRequests++;e.Cancel=true;}
        window.Closing+=CancelClose;
        var quitBinding=mainWindow.KeyBindings.Single(k=>k.Gesture?.Key==Key.Q && k.Gesture.KeyModifiers==KeyModifiers.Meta);
        Require(quitBinding.Gesture!.Matches(new KeyEventArgs {Key=Key.Q,KeyModifiers=KeyModifiers.Meta}),"Cmd Q gesture matches");
        quitBinding.Command!.Execute(null);await Task.Delay(50);
        Require(closeRequests>0,"Cmd Q bound command invokes graceful exit");
        mainWindow.ExitCommand.Execute(null);await Task.Delay(50);
        Require(window.IsVisible,"Graceful exit respects cancelled window closing");
        Require(beforeExit==System.Text.Json.JsonSerializer.Serialize(live.Session.Capture()),"Exit does not reset or save simulation");
        window.Closing-=CancelClose;
        Console.WriteLine("PASS: native compact/expanded layout, resize, LIVE update, mode and station preservation, zoom/pan/Fit, Cmd+Q registration and cancellable lifecycle Exit. Invoking final Exit command.");
        mainWindow.ExitCommand.Execute(null);
    }

    private static void Require(bool condition, string message) { if(!condition) throw new InvalidOperationException(message); }
    private static void Save(Control control, string name)
    {
        control.UpdateLayout(); using var bitmap = new RenderTargetBitmap(new((int)Math.Ceiling(control.Bounds.Width), (int)Math.Ceiling(control.Bounds.Height)));
        bitmap.Render(control); Directory.CreateDirectory(Path.Combine(Output,"screenshots")); bitmap.Save(Path.Combine(Output,"screenshots", name+".png"));
    }
}
