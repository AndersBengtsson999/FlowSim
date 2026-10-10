using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using Simulation.Application;
using Simulation.Core;
using Simulation.UI.Controls;
using Simulation.UI.Views;
using Simulation.UI.ViewModels;
using ModelValidation;
internal static class Program
{
    [STAThread] public static int Main(string[] args)
    {
        var builder=AppBuilder.Configure<ReleaseApp>();
        builder=args.Contains("--headless") ? builder.UseHeadless(new AvaloniaHeadlessPlatformOptions {UseHeadlessDrawing=false}).UseSkia() : builder.UsePlatformDetect().With(new AvaloniaNativePlatformOptions{RenderingMode=[AvaloniaNativeRenderingMode.Software]});
        return builder.WithInterFont().StartWithClassicDesktopLifetime([]);
    }
}
public sealed class ReleaseApp : Avalonia.Application
{
    public override void Initialize(){Styles.Add(new FluentTheme());RequestedThemeVariant=ThemeVariant.Light;}
    public override void OnFrameworkInitializationCompleted()
    {
        var desktop=(IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
        var main=new MainWindowViewModel();var vm=main.Simple.Live;
        var window=new MainWindow{DataContext=main,Width=1280,Height=850};desktop.MainWindow=window;
        window.Opened+=async(_,_)=>{
            try {
                await Task.Delay(150);
                var view=window.GetVisualDescendants().OfType<LiveView>().Single();
                var scroll=view.GetVisualDescendants().OfType<ScrollViewer>().Single(s=>s.Name=="LiveScroll");
                Button Button(string text)=>view.GetVisualDescendants().OfType<Button>().Single(b=>b.Content?.ToString()==text);
                void Click(string text)=>ControlAutomationPeer.CreatePeerForElement(Button(text))!.GetProvider<IInvokeProvider>()!.Invoke();
                async Task Run(string name, DependencySettings dependencies)
                {
                    vm.Reset(); vm.Setup.LoadConfiguration(Scenarios.BaselineRequest with {ResidualDependencies=dependencies}); vm.WorkSupply="Always available";
                    Click("Start"); vm.Pause(); vm.RollingWindow=100; vm.TargetDay="500"; await vm.RunToDayAsync();
                    await Task.Delay(100);scroll.Offset=default;await Task.Delay(100);
                    var period=vm.Performance!; var days=vm.Live!.Session.Days.TakeLast(100).ToArray();
                    Console.WriteLine(JsonSerializer.Serialize(new {Scenario=name,Days="401–500",Released=vm.Live.CurrentSnapshot.DoneCount,period.Throughput,period.DevelopmentCycleTime,DeliveryCycleTime=period.CycleTime,
                        DependencyAverage=period.WaitingForDependency.Average,DependencyMaximum=days.Max(d=>d.WaitingForDependencyCount),DevelopmentWip=days.Average(d=>d.DevelopmentWip),
                        period.DeveloperUtilization,period.TesterUtilization,period.DeliveryWorkCostPerItem,period.SystemCostPerReleasedItem,DevelopmentCost=period.AverageDeliveryCost!.Development,CollaborationCapacity=days.Sum(d=>d.CollaborationDevelopmentCapacity),
                        ObservedDependencyBlockedItems=vm.Live.Session.Days.SelectMany(d=>d.Items).Where(w=>w.DependencyBlocked && w.ResidualDependency is not null).Select(w=>w.Id).Distinct().Count()}));
                    var chart=view.GetVisualDescendants().OfType<LivePerformanceTrendChart>().Single();
                    Save(window,name);
                    Console.WriteLine($"Chart {name}: Y={chart.TranslatePoint(default,window)!.Value.Y} height={chart.Bounds.Height}");
                    Check(chart.TranslatePoint(default,window)!.Value.Y<=559 && chart.Bounds.Height==210,"Chart remains visible");
                }
                await Run("A-baseline",new());
                await Run("B-moderate",new(.2,3));
                await Run("C-stronger",new(.5,5));
                vm.TrendMetric=vm.TrendMetrics.Single(m=>m.Metric==LiveTrendMetric.WaitingForDependency);
                window.Width=960;await Task.Delay(100);Save(window,"dependency-trend-960");
                vm.BeginChange();await Task.Delay(100);
                var rate=view.GetVisualDescendants().OfType<TextBox>().Single(t=>t.DataContext is SimpleChangeField f && f.Label=="Dependency Rate (%)");
                var wait=view.GetVisualDescendants().OfType<TextBox>().Single(t=>t.DataContext is SimpleChangeField f && f.Label=="Dependency Waiting Time (days)");
                rate.Text="100";wait.Text="20";rate.BringIntoView();await Task.Delay(100);scroll.Offset=new Vector(0,scroll.Offset.Y+200);await Task.Delay(100);Save(window,"change");
                vm.ChangeLabel="Dependency assumption";vm.ApplyChanges();
                Check(vm.Live!.Session.Configuration.ResidualDependencies==new DependencySettings(1,20),"Change applied");
                vm.TargetDay="550";await vm.RunToDayAsync();scroll.Offset=default;await Task.Delay(100);Save(window,"intervention");
                Check(vm.ComparisonRows.Any(r=>r.Metric=="Average Waiting for Dependency"),"Before/After metric");
                var queue=vm.Flow.Single(r=>r.IsDependencyQueue);Check(queue.Count>0,"Observed waiting queue");
                Check(queue.Queue!.State!=QueueAttentionState.Neutral,"Shared queue highlighting");
                var button=view.GetVisualDescendants().OfType<ToggleButton>().First(b=>b.DataContext is FlowStateRow r && r.IsDependencyQueue);
                button.IsChecked=true;await Task.Delay(100);Save(window,"dependency-items");
                button.IsChecked=false;
                var comparison=view.GetVisualDescendants().OfType<Expander>().Single(e=>e.Header?.ToString()=="Before & After an intervention");comparison.IsExpanded=true;await Task.Delay(100);comparison.BringIntoView();await Task.Delay(100);Save(window,"before-after");
                vm.Reset();var config=view.GetVisualDescendants().OfType<Expander>().Single(e=>e.Name=="SetupSection");config.IsExpanded=true;
                var dependencies=view.GetVisualDescendants().OfType<Expander>().Single(e=>e.Header?.ToString()=="Dependencies");dependencies.IsExpanded=true;
                await Task.Delay(100);
                view.GetVisualDescendants().OfType<TextBox>().Single(t=>ReferenceEquals(t.DataContext,vm) && AutomationProperties.GetName(t)=="Dependency Rate (%)").Text="20";
                view.GetVisualDescendants().OfType<TextBox>().Single(t=>ReferenceEquals(t.DataContext,vm) && AutomationProperties.GetName(t)=="Dependency Waiting Time (days)").Text="3";
                Check(vm.Setup.CaptureSetup().ResidualDependencies==new DependencySettings(.2,3),"Configuration bound inputs");
                dependencies.BringIntoView();await Task.Delay(100);Save(window,"configuration");
                Console.WriteLine("PASS: dependency configuration, interventions, queue, item disclosure, trend, comparison and compact layout.");
                vm.Dispose(); desktop.Shutdown(0);
            } catch(Exception e){Console.Error.WriteLine(e);vm.Dispose();desktop.Shutdown(1);}
        };
        base.OnFrameworkInitializationCompleted();
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Save(Window window,string name){Directory.CreateDirectory("/tmp/flowsim-dependencies");window.UpdateLayout();using var image=new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width,(int)window.Bounds.Height));image.Render(window);image.Save("/tmp/flowsim-dependencies/"+name+".png");}
}
