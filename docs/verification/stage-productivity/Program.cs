using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Simulation.Application;
using Simulation.Core;
using Simulation.Infrastructure;
using Simulation.UI.ViewModels;
using Simulation.UI.Views;
internal static class Program
{
 [STAThread] public static int Main(string[] args) => AppBuilder.Configure<ProductivityApp>().UsePlatformDetect().WithInterFont().StartWithClassicDesktopLifetime([]);
}
public sealed class ProductivityApp : Avalonia.Application
{
 public override void Initialize(){Styles.Add(new FluentTheme());RequestedThemeVariant=ThemeVariant.Light;}
 public override void OnFrameworkInitializationCompleted(){
  var desktop=(IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;var main=new MainWindowViewModel();var vm=main.Simple.Live;
  var window=new MainWindow{DataContext=main,Width=1280,Height=800};desktop.MainWindow=window;
  window.Opened+=async(_,_)=>{try{
   await Task.Delay(300);Input("Live Testers").SetCurrentValue(TextBox.TextProperty,"5");vm.Setup.DevelopmentEffort="4";vm.Setup.CodeReviewEffort="2";vm.Setup.TestingEffort="3";Input("Live Testing WIP").SetCurrentValue(TextBox.TextProperty,"5");
   vm.WorkSupply="Always available";
   var setup=window.GetVisualDescendants().OfType<Expander>().Single(e=>e.Header?.ToString()=="Productivity · Development / Code Review / Testing");
   setup.IsExpanded=true;setup.BringIntoView();await Task.Delay(150);
   foreach(var stage in new[]{"Development","Code Review","Testing"})Check(double.Parse(Input(stage+" Productivity").Text!,CultureInfo.InvariantCulture)==1,"Default setup "+stage);
   Input("Testing Productivity").BringIntoView();await Task.Delay(150);Save(window,"setup");Invoke("Start");await Task.Delay(100);Invoke("Pause");await Advance(100);Invoke("Checkpoint");
   var scroll=window.GetVisualDescendants().OfType<ScrollViewer>().Single(s=>s.Name=="LiveScroll");scroll.Offset=default;await Task.Delay(150);Save(window,"day100-baseline");
   var baseline=JsonSerializer.Serialize(vm.Live!.Session.Days);
   foreach(var (stage,value,day) in new[]{("Development","1.5",120),("Code Review","1.3",140),("Testing","1.4",160)}){
    Invoke("Change");await Task.Delay(100);var input=Input(stage+" Productivity (x)");input.BringIntoView();await Task.Delay(100);
    input.SetCurrentValue(TextBox.TextProperty,value);Input("Change label").SetCurrentValue(TextBox.TextProperty,"AI-assisted "+stage.ToLowerInvariant());
    await Task.Delay(100);Save(window,"change-"+day);Invoke("Apply Changes");await Task.Delay(100);
    Check(!vm.IsEditing,"Change applied");Check(vm.Live.Session.Changes.Last().Day==day-20,"Recorded day");
    Check(vm.AfterPeriod.Contains("0 of 20"),"Initially partial After");await Advance(day);Check(vm.Comparison!.After.IsComplete,"Complete After");
    Check(vm.Live.Session.Configuration.Productivity==new StageProductivity(1.5,day>=140?1.3:1,day>=160?1.4:1),"Independent stage values");
    scroll.Offset=default;await Task.Delay(150);Save(window,"day"+day+"-flow");
    var ds=vm.Live.Session.Days.Skip(day-20).Take(20).ToArray();
    Console.WriteLine(FormattableString.Invariant($"Days {day-19}–{day}: Dev capacity {ds.Sum(d=>d.UsedDevelopmentCapacity):0.###}, work {ds.Sum(d=>d.DevelopmentWork):0.###}; Review capacity {ds.Sum(d=>d.UsedReviewCapacity):0.###}, work {ds.Sum(d=>d.ReviewWork):0.###}; Test capacity {ds.Sum(d=>d.UsedTesterCapacity):0.###}, work {ds.Sum(d=>d.TestingWork):0.###}; completed {vm.Comparison.After.Completed}; average review queue {vm.Comparison.After.Review.Average:0.###}; average test queue {vm.Comparison.After.Testing.Average:0.###}."));
   }
   Check(baseline==JsonSerializer.Serialize(vm.Live.Session.Days.Take(100).ToArray()),"History unchanged");
   vm.SetupExpanded=true;scroll.Offset=default;await Task.Delay(150);Save(window,"configuration");vm.SetupExpanded=false;
   vm.TrendRange="Full Session";vm.SelectedIntervention=vm.Live.Session.Changes.First();
   foreach(var metric in new[]{LiveTrendMetric.DevelopmentCapacity,LiveTrendMetric.DevelopmentWork,LiveTrendMetric.DeveloperUtilization,LiveTrendMetric.TesterUtilization,LiveTrendMetric.ReviewQueue,LiveTrendMetric.TestingQueue,LiveTrendMetric.Throughput,LiveTrendMetric.CycleTime}){
    vm.TrendMetric=vm.TrendMetrics.Single(m=>m.Metric==metric);await Task.Delay(80);
    window.GetVisualDescendants().OfType<Simulation.UI.Controls.LivePerformanceTrendChart>().Single().BringIntoView();await Task.Delay(100);Save(window,"trend-"+metric);
    Check(vm.Trend.Interventions.Count==3,"All trend markers");
   }
   var comparison=window.GetVisualDescendants().OfType<Expander>().Single(e=>e.Header?.ToString()=="Before & After an intervention");comparison.IsExpanded=true;await Task.Delay(150);window.UpdateLayout();
   var beforeText=window.GetVisualDescendants().OfType<TextBlock>().Single(t=>t.Text==vm.BeforePeriod);
   scroll.Offset=new Vector(0,scroll.Offset.Y+beforeText.TranslatePoint(default,window)!.Value.Y-220);await Task.Delay(150);
   Check(window.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text=="Before: Days 81–100 · 20 of 20 days available"),"Visible Before boundary");
   Check(window.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text=="After: Days 101–120 · 20 of 20 days available"),"Visible After boundary");Save(window,"before-after");comparison.IsExpanded=false;
   foreach(var size in new[]{new Size(1280,800),new Size(960,720)}){
    window.Width=size.Width;window.Height=size.Height;scroll.Offset=default;await Task.Delay(200);window.UpdateLayout();
    var board=window.GetVisualDescendants().OfType<ListBox>().Single(b=>b.Name=="LiveFlow");
    Check(board.TranslatePoint(default,window)!.Value.Y+board.Bounds.Height<=scroll.TranslatePoint(default,window)!.Value.Y+scroll.Bounds.Height,"Board fits "+size);
    Check(scroll.Extent.Width<=scroll.Viewport.Width+1,"No horizontal overflow");Save(window,"size-"+size.Width);
   }
   var state=JsonSerializer.Serialize(vm.Live.Session.Capture());var saved=LiveSessionJson.Save(vm.Live);Check(state==JsonSerializer.Serialize(LiveSessionJson.Load(saved).Session.Capture()),"Persistence");
   vm.SelectedCheckpoint=vm.Checkpoints.Single();vm.RestoreCommand.Execute(null);Check(vm.Day==100,"Checkpoint restored");
   foreach(var (stage,value,day) in new[]{("Development","1.5",120),("Code Review","1.3",140),("Testing","1.4",160)}){
    vm.BeginChange();vm.ChangeFields.Single(f=>f.Label==stage+" Productivity (x)").Value=value;vm.ChangeLabel="AI-assisted "+stage.ToLowerInvariant();vm.ApplyChanges();await Advance(day);
   }
   Check(state==JsonSerializer.Serialize(vm.Live.Session.Capture()),"Checkpoint replay exact");
   Console.WriteLine("PASS: actual native setup and Change textbox bindings/buttons, independent 100/120/140 interventions, history, trend markers, visible Before/After boundaries, laptop/narrow board, persistence and checkpoint replay.");
   vm.Dispose();desktop.Shutdown(0);
   TextBox Input(string name)=>window.GetVisualDescendants().OfType<TextBox>().Single(t=>AutomationProperties.GetName(t)==name);
   void Invoke(string text){var b=window.GetVisualDescendants().OfType<Button>().Single(b=>b.Content?.ToString()==text);Check(b.IsEnabled,text+" enabled");ControlAutomationPeer.CreatePeerForElement(b)!.GetProvider<IInvokeProvider>()!.Invoke();}
   async Task Advance(int day){vm.TargetDay=day.ToString();await vm.RunToDayAsync();await Task.Delay(100);Check(vm.Day==day,"Reached day");}
  }catch(Exception e){Console.Error.WriteLine(e);vm.Dispose();desktop.Shutdown(1);}};
  base.OnFrameworkInitializationCompleted();
 }
 private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
 private static void Save(Window w,string name){var dir="/tmp/flowsim-stage-productivity";Directory.CreateDirectory(dir);w.UpdateLayout();using var b=new RenderTargetBitmap(new PixelSize((int)w.Bounds.Width,(int)w.Bounds.Height));b.Render(w);b.Save(Path.Combine(dir,name+".png"));}
}
