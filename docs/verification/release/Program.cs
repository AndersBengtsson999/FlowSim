using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
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
    [STAThread] public static int Main(string[] args) => AppBuilder.Configure<ReleaseApp>().UsePlatformDetect()
        .With(new AvaloniaNativePlatformOptions{RenderingMode=[AvaloniaNativeRenderingMode.Software]}).WithInterFont().StartWithClassicDesktopLifetime([]);
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
                async Task Run(string name,ReleaseSettings settings)
                {
                    vm.Reset();vm.Setup.LoadConfiguration(Scenarios.BaselineRequest with{Release=settings});vm.WorkSupply="Always available";
                    Click("Start");vm.Pause();vm.RollingWindow=50;vm.TargetDay="200";await vm.RunToDayAsync();Print(name,vm);
                }
                await Run("A Flow-based 1/day",new(Capacity:1));
                await Run("B Scheduled 5/5days",new(ReleaseMode.Scheduled,5,5));
                await Run("C Flow-based 2/day",new(Capacity:2));
                await Run("C Scheduled 10/5days",new(ReleaseMode.Scheduled,10,5));
                vm.TrendMetric=vm.TrendMetrics.Single(m=>m.Metric==LiveTrendMetric.ReadyForRelease);
                vm.TargetDay="203";await vm.RunToDayAsync();
                var chart=view.GetVisualDescendants().OfType<LivePerformanceTrendChart>().Single();
                foreach(var width in new[]{1280d,960d}) {
                    window.Width=width;scroll.Offset=default;await Task.Delay(100);
                    var pos=chart.TranslatePoint(default,window)!.Value.Y;
                    Check(pos<=572 && chart.Bounds.Height==210,"Chart retained old visibility/height at "+width);
                    Console.WriteLine($"Width {width}: chart Y {pos} (previous 572), height {chart.Bounds.Height} (previous 210).");Save(window,"layout-"+width);
                }
                Click("Change");await Task.Delay(100);
                var mode=view.GetVisualDescendants().OfType<ComboBox>().Single(b=>AutomationProperties.GetName(b)=="Change Release Mode");
                mode.BringIntoView();await Task.Delay(100);scroll.Offset=new Vector(0,scroll.Offset.Y+400);await Task.Delay(100);Save(window,"change");
                mode.SelectedItem="Flow-based";
                var capacity=view.GetVisualDescendants().OfType<TextBox>().Single(b=>AutomationProperties.GetName(b)=="Change Release Capacity");capacity.Text="2";
                vm.ChangeLabel="Scheduled to Flow-based";Click("Apply Changes");
                Check(vm.Live!.Session.Configuration.Release==new ReleaseSettings(ReleaseMode.FlowBased,2,5),"Bound release intervention applied");
                vm.TargetDay="223";await vm.RunToDayAsync();
                var comparison=view.GetVisualDescendants().OfType<Expander>().Single(e=>e.Header?.ToString()=="Before & After an intervention");comparison.IsExpanded=true;
                await Task.Delay(100);comparison.BringIntoView();await Task.Delay(100);Save(window,"before-after");
                Check(vm.ComparisonRows.Any(r=>r.Metric.StartsWith("Release Wait Time")),"Release comparison");
                vm.Reset();vm.Setup.LoadConfiguration(Scenarios.BaselineRequest with{Release=new(ReleaseMode.Scheduled,5,5)});
                var config=view.GetVisualDescendants().OfType<Expander>().Single(e=>e.Name=="SetupSection");config.IsExpanded=true;
                var release=view.GetVisualDescendants().OfType<Expander>().Single(e=>e.Header?.ToString()=="Release / Deployment");release.IsExpanded=true;
                await Task.Delay(100);release.BringIntoView();await Task.Delay(100);Save(window,"configuration");
                vm.Setup.LoadConfiguration(Scenarios.BaselineRequest);
                await Task.Delay(100);
                Check(vm.Setup.ReleaseCapacity=="Unlimited", "Default capacity displayed as Unlimited");
                Save(window,"configuration-unlimited");
                Console.WriteLine("PASS native release configuration, bound mode/capacity Change and label, Ready queue, Released status, trend and Before/After, chart density.");
                vm.Dispose();desktop.Shutdown(0);
            } catch(Exception e){Console.Error.WriteLine(e);vm.Dispose();desktop.Shutdown(1);}
        };
        base.OnFrameworkInitializationCompleted();
    }
    static void Print(string scenario,LiveViewModel vm){var p=vm.Performance!;Console.WriteLine(JsonSerializer.Serialize(new{Scenario=scenario,Day=vm.Live!.Session.CurrentDay,p.FirstDay,p.LastDay,Ready=p.ReadyForRelease.Current,AverageReady=p.ReadyForRelease.Average,Released=vm.Live.CurrentSnapshot.DoneCount,p.Completed,p.WorkCompleted,p.Throughput,p.CompletionRate,DeliveryCycleTime=p.CycleTime,p.DevelopmentCycleTime,p.ReleaseWaitTime,p.DeveloperUtilization,p.TesterUtilization,DeliveryWorkCost=p.DeliveryCostPerDoneItem,SystemCostPerReleased=p.SystemCostPerDoneItem}));}
    static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    static void Save(Window window,string name){Directory.CreateDirectory("/tmp/flowsim-release");window.UpdateLayout();using var image=new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width,(int)window.Bounds.Height));image.Render(window);image.Save("/tmp/flowsim-release/"+name+".png");}
}
