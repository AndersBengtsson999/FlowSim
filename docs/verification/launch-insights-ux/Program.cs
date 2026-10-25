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
    private static string Output => "docs/verification/launch-insights-ux/after" + (Headless ? "/headless" : "");
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
        var before=System.Text.Json.JsonSerializer.Serialize(live.Session.Capture());
        var indicator=view.FindControl<Button>("InsightsButton")!;
        Require(indicator.IsEffectivelyVisible,"Compact LIVE entry visible");
        indicator.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(200);
        var insights=view.CurrentInsightsWindow!;Require(insights.IsVisible,"Analysis opened");
        view.OpenInsights();Require(ReferenceEquals(insights,view.CurrentInsightsWindow),"Reuses analysis window");
        Require(insights.ActualThemeVariant==ThemeVariant.Dark,"Dark theme");
        Save(window,"live-indicator");
        Require(insights.SelectedInsightId==InsightPresentation.Selection(vm.Insights.Items,null),"Default uses active finding priority");
        insights.Select("imbalance.testing-wait");await Task.Delay(100);
        Require(vm.Insights.Items.Single(i=>i.Insight.Id=="imbalance.testing-wait").Status==InsightStatus.Reviewed,"Selection reviews evidence");
        Require(insights.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text=="What it might mean"),"Separate explanation");
        Save(insights,"insights-wide");
        Require(before==System.Text.Json.JsonSerializer.Serialize(live.Session.Capture()),"Inspection did not mutate simulation state");
        var evidence=insights.GetVisualDescendants().OfType<Expander>().Single(e=>e.Header?.ToString()?.StartsWith("Supporting evidence")==true);
        Require(!evidence.IsExpanded,"Technical evidence collapsed by default");
        evidence.IsExpanded=true;await Task.Delay(100);
        var reader=insights.GetVisualDescendants().OfType<ScrollViewer>().First(v=>v.Content is StackPanel);
        reader.Offset=new Vector(0,540);await Task.Delay(100);Save(insights,"supporting-evidence");
        Require(insights.GetVisualDescendants().OfType<LivePerformanceTrendChart>().All(c=>c.SamplesOnly),"Charts use exact samples only");
        evidence.IsExpanded=false;reader.Offset=default;
        insights.Width=680;await Task.Delay(150);Save(insights,"insights-narrow");
        insights.Close();
        Require(!vm.ProductDetailsExpanded,"Game View opens compact");
        await Task.Delay(100);Save(window,"game-compact");
        var game=window.GetVisualDescendants().OfType<GameView>().Single(g=>g.IsEffectivelyVisible);
        var viewport=game.FindControl<Border>("WorldViewport")!;var compactHeight=viewport.Bounds.Height;
        vm.ProductDetailsExpanded=true;await Task.Delay(100);Save(window,"game-expanded");
        Require(compactHeight>viewport.Bounds.Height,"Compact panel gives workshop more height");
        var launchView=game.GetVisualDescendants().OfType<ProductLaunchView>().Single();
        var launch=launchView.FindControl<Button>("LaunchNowButton")!;
        var schedule=launchView.FindControl<Button>("ScheduleLaunchButton")!;
        Require(ReferenceEquals(launch.Command,vm.LaunchProductCommand) && ReferenceEquals(schedule.Command,vm.PlanLaunchCommand),"Buttons reuse existing launch commands");
        vm.Resume();await Task.Delay(50);
        Require(launch.IsEffectivelyVisible && !launch.IsEffectivelyEnabled,"Launch stays visible but disabled while running");
        Require(schedule.IsEffectivelyVisible && !schedule.IsEffectivelyEnabled,"Schedule stays visible but disabled while running");
        Save(window,"game-running");vm.Pause();
        vm.LaunchDelay="3";vm.PlanningUnit="Working days";
        schedule.Focus();window.KeyPressQwerty(PhysicalKey.Enter,RawInputModifiers.None);window.KeyReleaseQwerty(PhysicalKey.Enter,RawInputModifiers.None);await Task.Delay(50);
        Require(vm.Live!.Session.ProductDelivery.Versions[^1].PlannedDay==vm.Day+3,"Schedule button sets planned day");
        if(vm.CanLaunchProduct) {
            var awaiting=vm.AwaitingCustomerLaunch;launch.Focus();window.KeyPressQwerty(PhysicalKey.Enter,RawInputModifiers.None);window.KeyReleaseQwerty(PhysicalKey.Enter,RawInputModifiers.None);await Task.Delay(50);
            Require(vm.Live.Session.ProductDelivery.CustomerLaunches.Last().ItemIds.Count==awaiting,"Launch button delivers internally released items");
        }
        var tabs=launchView.GetVisualDescendants().OfType<TabItem>().ToArray();
        Require(new[]{"Customer delivery","Launch Economics","Launch history"}.All(name=>tabs.Any(t=>t.Header?.ToString()==name)),"Delivery economics and history remain accessible");
        vm.ProductDetailsExpanded=false;window.Width=980;await Task.Delay(150);Save(window,"game-narrow");
        Require(viewport.Bounds.Height>=180,"Narrow workshop remains usable");
    }

    private static void Require(bool condition,string message) {if(!condition)throw new InvalidOperationException(message);Console.WriteLine("PASS: "+message);}
    private static void Save(Control control,string name)
    {
        control.UpdateLayout();using var bitmap=new RenderTargetBitmap(new((int)Math.Ceiling(control.Bounds.Width),(int)Math.Ceiling(control.Bounds.Height)));
        bitmap.Render(control);Directory.CreateDirectory(Path.Combine(Output,"screenshots"));bitmap.Save(Path.Combine(Output,"screenshots",name+".png"));
    }
}
