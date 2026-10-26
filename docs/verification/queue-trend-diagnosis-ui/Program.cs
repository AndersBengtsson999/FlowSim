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
    private static string Output => "docs/verification/queue-trend-diagnosis-ui" + (Headless ? "/headless" : "");
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
        var live=LiveSimulation.Start(new(){NumberOfWorkItems=0,RandomSeed=12345,TestingEffort=5,TesterCount=1},Simulation.Core.WorkArrivalMode.ContinuousArrival,.4m);
        for(var n=0;n<70;n++)live.Step();
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
        insights.Select("queue.testing-wait");await Task.Delay(100);
        Require(vm.Insights.Items.Single(i=>i.Insight.Id=="queue.testing-wait").Insight.QueueDirection!.Label=="Growing","Insights reads corrected Growing trend");
        var evidence=insights.GetVisualDescendants().OfType<Expander>().Single(e=>e.Header?.ToString()?.StartsWith("Supporting evidence")==true);
        evidence.IsExpanded=true;await Task.Delay(100);
        Require(insights.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text?.Contains("+0.2 items/day")==true && t.Text.Contains("9 → 11")),"Insights exposes actual endpoints and slope");
        var reader=insights.GetVisualDescendants().OfType<ScrollViewer>().First(v=>v.Content is StackPanel);
        reader.Offset=new Vector(0,480);await Task.Delay(100);Save(insights,"testing-trend-evidence");
        view.OpenInsightStation("Testing");await Task.Delay(100);
        var world=view.GetVisualDescendants().OfType<GameWorldControl>().Single();
        Require(world.Scene!.QueueTrends["testing-wait"].Label=="Growing","Workshop uses same trend");
        Require(world.WorkflowSummary.Contains("Testing: Growing"),"Workflow summary uses same trend");
        Require(world.Details.Observation.Contains("+0.2 items/day") && world.Details.Observation.Contains("9 → 11"),"Station details expose same evidence");
        Save(window,"testing-station-day70");
        Require(before==System.Text.Json.JsonSerializer.Serialize(live.Session.Capture()),"Inspection leaves simulation unchanged");
        insights.Close();
    }

    private static void Require(bool condition,string message) {if(!condition)throw new InvalidOperationException(message);Console.WriteLine("PASS: "+message);}
    private static void Save(Control control,string name)
    {
        control.UpdateLayout();using var bitmap=new RenderTargetBitmap(new((int)Math.Ceiling(control.Bounds.Width),(int)Math.Ceiling(control.Bounds.Height)));
        bitmap.Render(control);Directory.CreateDirectory(Path.Combine(Output,"screenshots"));bitmap.Save(Path.Combine(Output,"screenshots",name+".png"));
    }
}
