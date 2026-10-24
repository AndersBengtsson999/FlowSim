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
    private static string Output => "docs/verification/b6-6-1" + (Headless ? "/headless" : "");
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
        insights.Select("imbalance.testing-wait");await Task.Delay(100);
        Require(vm.Insights.Items.Single(i=>i.Insight.Id=="imbalance.testing-wait").Status==InsightStatus.Reviewed,"Selection reviews evidence");
        Require(insights.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text=="Possible explanation"),"Separate explanation");
        Save(insights,"insights-wide");
        insights.Select("queue.testing-wait");await Task.Delay(100);Save(insights,"queue-details");
        insights.Select("imbalance.testing-wait");await Task.Delay(100);Save(insights,"capacity-details");
        insights.GetVisualDescendants().OfType<Expander>().Single().IsExpanded=true;await Task.Delay(100);Save(insights,"capacity-metrics");
        insights.GetVisualDescendants().OfType<Expander>().Single().IsExpanded=false;
        Require(before==System.Text.Json.JsonSerializer.Serialize(live.Session.Capture()),"Analysis and review did not mutate state");
        insights.Width=680;await Task.Delay(150);Save(insights,"insights-narrow");
        var help=insights.GetVisualDescendants().OfType<Button>().Single(b=>b.Content?.ToString()=="Help · Simulation Insights");
        help.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(100);
        Require(GuideService.Current is {IsVisible:true},"Context Help opens");
        Require(GuideService.Current!.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text?.Contains("Simulation Insights")==true),"Correct Help content");
        GuideService.Current!.Close();
        view.OpenInsightStation("Testing");await Task.Delay(100);
        Require(view.GetVisualDescendants().OfType<GameWorldControl>().Single().SelectedStation=="Testing","Station navigation");
        view.OpenInsightKpi(TeamKpi.TesterUtilization);await Task.Delay(100);
        var explorer=((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!).Windows.Single(w=>w.Title=="FLOW · KPI Explorer");
        Require(vm.ExplorerPrimary?.Observation.Definition.Id==TeamKpi.TesterUtilization,"Existing KPI Explorer navigation");explorer.Close();
        var count=vm.Insights.NewCount;
        insights.Close();Require(view.CurrentInsightsWindow is null,"Close releases window");view.OpenInsights();
        Require(vm.Insights.NewCount==count,"Reopening preserves acknowledgement");
        vm.Resume();var day=vm.Day;await Task.Delay(1300);
        Require(vm.IsRunning && vm.Day>day,"LIVE advances while analysis is open");vm.Pause();
        var reopened=view.CurrentInsightsWindow!;
        reopened.Select("imbalance.testing-wait");
        var old=vm.InsightsReport!;var changed=old.Insights.Select(i=>i.Id=="imbalance.testing-wait"?i with {MaterialState="Native test changed classification"}:i).ToArray();
        vm.Insights.Observe(old with {Insights=changed});
        Require(vm.Insights.UpdatedCount>0,"Changed evidence is updated, not new");
        // Synthetic lifecycle input above does not alter the simulator; reload actual timeline for UI.
        vm.Load(LiveSimulation.Restore(live.Capture()));
        Require(vm.Insights.NewCount>0,"Loading resets ephemeral review state");
        reopened.Close();
        window.Width=980;await Task.Delay(120);Save(window,"live-980");
        GuideCatalog.Default.Validate(requireImages:true);
        Console.WriteLine("PASS: " + (Headless?"Headless/Skia":"Native macOS") + " window reuse, selection/review, updated lifecycle, narrow layout, LIVE playback, navigation, Help, offline resources and read-only state.");
    }
    private static void Require(bool condition,string message) {if(!condition)throw new InvalidOperationException(message);Console.WriteLine("PASS: "+message);}
    private static void Save(Control control,string name)
    {
        control.UpdateLayout();using var bitmap=new RenderTargetBitmap(new((int)Math.Ceiling(control.Bounds.Width),(int)Math.Ceiling(control.Bounds.Height)));
        bitmap.Render(control);Directory.CreateDirectory(Path.Combine(Output,"screenshots"));bitmap.Save(Path.Combine(Output,"screenshots",name+".png"));
    }
}
