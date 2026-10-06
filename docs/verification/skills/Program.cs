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
    [STAThread] public static int Main(string[] args) => AppBuilder.Configure<SkillsApp>().UsePlatformDetect()
        .With(new AvaloniaNativePlatformOptions{RenderingMode=[AvaloniaNativeRenderingMode.Software]}).WithInterFont().StartWithClassicDesktopLifetime([]);
}
public sealed class SkillsApp : Avalonia.Application
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
                vm.Setup.LoadConfiguration(Scenarios.BaselineRequest);vm.WorkSupply="Always available";
                Button Button(string text)=>view.GetVisualDescendants().OfType<Button>().Single(b=>b.Content?.ToString()==text);
                void Click(string text)=>ControlAutomationPeer.CreatePeerForElement(Button(text))!.GetProvider<IInvokeProvider>()!.Invoke();
                Click("Start");vm.Pause();vm.TargetDay="200";await vm.RunToDayAsync();
                var chart=view.GetVisualDescendants().OfType<LivePerformanceTrendChart>().Single();
                var positions=new Dictionary<double,(double Y,double Height)>();
                foreach(var width in new[]{1280d,960d}) {
                    window.Width=width;scroll.Offset=default;await Task.Delay(100);positions[width]=(chart.TranslatePoint(default,window)!.Value.Y,chart.Bounds.Height);
                }
                vm.Reset();vm.Setup.LoadConfiguration(Scenarios.BaselineRequest with{Skills=new(1,.4)});vm.WorkSupply="Always available";
                Click("Start");vm.Pause();vm.RollingWindow=50;vm.TargetDay="200";await vm.RunToDayAsync();
                Print("Before: Specialists 1",vm);
                vm.TrendMetric=vm.TrendMetrics.Single(m=>m.Metric==LiveTrendMetric.SpecialistWorkWaiting);
                foreach(var width in new[]{1280d,960d}) {
                    window.Width=width;scroll.Offset=default;await Task.Delay(100);
                    var pos=chart.TranslatePoint(default,window)!.Value.Y;
                    Check(Math.Abs(pos-positions[width].Y)<1 && chart.Bounds.Height==positions[width].Height,"Chart position/height unchanged at "+width);
                    Console.WriteLine($"Width {width}: chart Y {pos}, height {chart.Bounds.Height}, unchanged from Skills disabled.");Save(window,"layout-"+width);
                }
                var dev=vm.Flow.Single(r=>r.State==WorkItemStatus.Development);dev.IsExpanded=true;await Task.Delay(100);Save(window,"items");dev.IsExpanded=false;
                Click("Change");await Task.Delay(100);Save(window,"change");
                var field=vm.ChangeFields.Single(f=>f.Label=="Specialists");
                var box=view.GetVisualDescendants().OfType<TextBox>().Single(b=>ReferenceEquals(b.DataContext,field));
                box.Text="2";vm.ChangeLabel="Specialists 1 to 2";Click("Apply Changes");
                Check(vm.Live!.Session.Configuration.Skills.Specialists==2,"Bound intervention applied");
                vm.TargetDay="400";await vm.RunToDayAsync();Print("After: Specialists 2",vm);
                Check(vm.Live.Session.Changes.Single().Day==200,"Intervention recorded Day 200");
                Check(vm.Live.Session.WorkItems.Any(w=>w.RequiresSpecialist),"Classification exists");
                vm.Reset();vm.Setup.LoadConfiguration(Scenarios.BaselineRequest with{Skills=new(2,.4)});
                var config=view.GetVisualDescendants().OfType<Expander>().Single(e=>e.Name=="SetupSection");
                config.IsExpanded=true;await Task.Delay(100);config.BringIntoView();await Task.Delay(100);Save(window,"configuration");
                Console.WriteLine("PASS native configuration, bound Change + label, specialist item markers, waiting trend and preserved chart density.");
                vm.Dispose();desktop.Shutdown(0);
            } catch(Exception e){Console.Error.WriteLine(e);vm.Dispose();desktop.Shutdown(1);}
        };
        base.OnFrameworkInitializationCompleted();
    }
    static void Print(string scenario,LiveViewModel vm){var p=vm.Performance!;Console.WriteLine(JsonSerializer.Serialize(new{Scenario=scenario,Day=vm.Live!.Session.CurrentDay,p.FirstDay,p.LastDay,Done=vm.Live.Session.WorkItems.Count(w=>w.DoneDay!=null),DoneInPeriod=p.Completed,p.Throughput,p.CycleTime,p.AverageWip,CurrentWip=vm.Live.CurrentSnapshot.TotalWip,p.DeveloperUtilization,p.TesterUtilization,vm.Live.CurrentSnapshot.SpecialistWorkWaiting,AverageSpecialistWaiting=vm.Live.Session.Days.TakeLast(50).Average(d=>d.SpecialistWorkWaiting),MaxSpecialistWaiting=vm.Live.Session.Days.TakeLast(50).Max(d=>d.SpecialistWorkWaiting),p.DeliveryCostPerDoneItem,p.SystemCostPerDoneItem}));}
    static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    static void Save(Window window,string name){Directory.CreateDirectory("/tmp/flowsim-skills");window.UpdateLayout();using var image=new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width,(int)window.Bounds.Height));image.Render(window);image.Save("/tmp/flowsim-skills/"+name+".png");}
}
