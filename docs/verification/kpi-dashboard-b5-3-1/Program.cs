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
    private static string Output => "docs/verification/kpi-dashboard-b5-3-1" + (Headless ? "/headless" : "");
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
        var picker=panel.GetVisualDescendants().OfType<ComboBox>().Single();
        picker.SelectedItem=10;await Task.Delay(100);
        Require(vm.KpiWindow==10 && vm.KpiCards.All(c=>c.Period.Contains("91–100")),"Window binding");
        var choose=panel.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="ChooseKpis");
        choose.Focus();choose.RaiseEvent(new KeyEventArgs{RoutedEvent=InputElement.KeyDownEvent,Key=Key.Enter});await Task.Delay(100);
        var flyout=(Flyout)choose.Flyout!;
        Require(flyout.IsOpen,"KPI selector opens via keyboard");
        var content=(Control)flyout.Content!;
        var boxes=content.GetVisualDescendants().OfType<CheckBox>().ToArray();
        Require(boxes.Length==12,"All choices rendered");
        boxes.Single(b=>b.DataContext is KpiChoice c && c.Definition.Id==TeamKpi.ReworkEffort).IsChecked=false;
        boxes.Single(b=>b.DataContext is KpiChoice c && c.Definition.Id==TeamKpi.TechnicalDebt).IsChecked=true;
        await Task.Delay(100);Save(content.GetVisualAncestors().OfType<Control>().First(c=>c.GetType().Name=="FlyoutPresenter"),"kpi-selector");flyout.Hide();
        var world=game.GetVisualDescendants().OfType<GameWorldControl>().Single();
        world.ZoomBy(1.2);Require(world.Camera.Zoom>1,"Camera zoom remains available");world.FitCamera();
        world.SelectedStation="Testing";await Task.Delay(100);Require(world.HasSelection,"Station details remain available");world.SelectedStation=null;
        Require(vm.KpiCards.Count==4 && vm.KpiChoices.Where(c=>!c.Selected).All(c=>!c.CanSelect),"Selection limit");
        Require(state==System.Text.Json.JsonSerializer.Serialize(live.Session.Capture()),"Display edits preserve simulation");
        window.Width=980;await Task.Delay(250);window.UpdateLayout();
        Require(panel.LayoutOrientation==Avalonia.Layout.Orientation.Horizontal,"Narrow horizontal composition");
        Save(game,"dashboard-narrow");
        vm.DashboardVisible=false;await Task.Delay(100);Require(!panel.IsVisible,"Hide dashboard");
        vm.DashboardVisible=true;
        var restored=Simulation.Infrastructure.LiveSessionJson.Load(Simulation.Infrastructure.LiveSessionJson.Save(live));
        vm.Load(restored);await Task.Delay(100);
        Require(vm.KpiWindow==10 && vm.KpiSettings.Selected!.Contains(TeamKpi.TechnicalDebt),"Saved preferences");
        Console.WriteLine("PASS: native macOS dashboard renders; default periods, window binding, selection limit, responsive placement, hiding, save/load and unchanged simulation state.");
    }

    private static void Require(bool condition, string message) { if(!condition) throw new InvalidOperationException(message); }
    private static void Save(Control control, string name)
    {
        control.UpdateLayout(); using var bitmap = new RenderTargetBitmap(new((int)Math.Ceiling(control.Bounds.Width), (int)Math.Ceiling(control.Bounds.Height)));
        bitmap.Render(control); Directory.CreateDirectory(Path.Combine(Output,"screenshots")); bitmap.Save(Path.Combine(Output,"screenshots", name+".png"));
    }
}
