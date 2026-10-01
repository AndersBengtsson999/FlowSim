using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Simulation.Application;
using Simulation.Core;
using Simulation.UI.ViewModels;
using Simulation.UI.Views;

internal static class Program
{
 [STAThread] public static int Main(string[] args) { VisualApp.Phase=args.FirstOrDefault()??"after"; return AppBuilder.Configure<VisualApp>().UsePlatformDetect().WithInterFont().StartWithClassicDesktopLifetime([]); }
}
public sealed class VisualApp : Avalonia.Application
{
 public static string Phase="after";
 public override void Initialize(){Styles.Add(new FluentTheme());RequestedThemeVariant=ThemeVariant.Light;}
 public override void OnFrameworkInitializationCompleted(){
  var desktop=(IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!; var main=new MainWindowViewModel(); var vm=main.Simple.Live;
  var window=new MainWindow{DataContext=main,Width=1280,Height=800};desktop.MainWindow=window;
  window.Opened+=async(_,_)=>{try{
   await Task.Delay(300); Save(window,"setup");
   vm.Start(); await Task.Delay(150); Save(window,"running-fixed-rate");vm.Pause();vm.TargetDay="100";await vm.RunToDayAsync();await Task.Delay(150);
   Save(window,"paused-fixed-rate");vm.SetupExpanded=true;await Task.Delay(100);Save(window,"configuration");vm.SetupExpanded=false;
   vm.BeginChange();vm.DraftSupply="Always available";vm.Draft.DeveloperAvailability="85";vm.ApplyChanges();vm.TargetDay="140";await vm.RunToDayAsync();
   vm.TrendRange="Full Session";vm.TrendMetric=vm.TrendMetrics.Single(m=>m.Metric==LiveTrendMetric.AvailableDevelopers);
   var scroll=window.GetVisualDescendants().OfType<ScrollViewer>().Single(s=>s.Name=="LiveScroll");
   foreach(var includeRework in new[]{false,true}) {
   if(includeRework){vm.BeginChange();vm.Draft.DefectsEnabled=true;vm.ApplyChanges();}
   foreach(var size in new[]{new Size(1280,800),new Size(1600,960),new Size(1920,1000),new Size(960,720)}){
    window.Width=size.Width;window.Height=size.Height;scroll.Offset=default;await Task.Delay(180);window.UpdateLayout();
    var board=window.GetVisualDescendants().OfType<ListBox>().Single(l=>l.Name=="LiveFlow");var y=board.TranslatePoint(default,window)!.Value.Y;
    var bottom=scroll.TranslatePoint(default,window)!.Value.Y+scroll.Bounds.Height;
    var trend=window.GetVisualDescendants().OfType<TextBlock>().Single(t=>t.Name=="TrendHeading");var ty=trend.TranslatePoint(default,window)!.Value.Y;
    Console.WriteLine($"{Phase} {window.Bounds.Size} rework={includeRework}: board={y}–{y+board.Bounds.Height}; viewport={bottom}; Trend={ty}");
    if(Phase=="after"){Check(y+board.Bounds.Height<=bottom,"Complete board visible");Check(ty+trend.Bounds.Height<=bottom,"Trend heading visible");Check(scroll.Extent.Width<=scroll.Viewport.Width+1,"No horizontal overflow");}
    Save(window,$"size-{size.Width}"+(includeRework?"-rework":""));
   }
   }
   window.Width=1280;window.Height=800;vm.Flow.Single(r=>r.State==WorkItemStatus.Development).IsExpanded=true;await Task.Delay(150);scroll.Offset=default;Save(window,"development-expanded");
   vm.Flow.Single(r=>r.State==WorkItemStatus.Development).IsExpanded=false;
   vm.BeginChange();vm.Draft.TesterAvailability="0";vm.ApplyChanges();vm.TargetDay="205";await vm.RunToDayAsync();await Task.Delay(150);
   vm.Flow.Single(r=>r.State==WorkItemStatus.WaitingForTesting).IsExpanded=true;vm.Flow.Single(r=>r.State==WorkItemStatus.Development).IsExpanded=true;await Task.Delay(150);scroll.Offset=default;Save(window,"multiple-expanded");
   var queue=vm.Flow.Single(r=>r.State==WorkItemStatus.WaitingForTesting);Check(queue.Count>=40,"Large queue");Console.WriteLine("Large queue="+queue.Count);
   foreach(var row in vm.Flow)row.IsExpanded=false;
   var state=JsonSerializer.Serialize(vm.Live!.Capture());
   var chart=window.GetVisualDescendants().OfType<Simulation.UI.Controls.LivePerformanceTrendChart>().Single();chart.BringIntoView();await Task.Delay(150);Save(window,"trend");
   foreach(var name in new[]{"Team Performance","Before & After an intervention"}){
    var panel=window.GetVisualDescendants().OfType<Expander>().Single(e=>e.Header?.ToString()==name);panel.IsExpanded=true;await Task.Delay(100);panel.BringIntoView();await Task.Delay(100);Save(window,name.Split(' ')[0]);panel.IsExpanded=false;
   }
   main.Simple.OpenAnalyzeCommand.Execute(null);main.Simple.OpenLiveCommand.Execute(null);Check(state==JsonSerializer.Serialize(vm.Live.Capture()),"Presentation preserves session");
   vm.StopCommand.Execute(null);scroll.Offset=default;await Task.Delay(150);Save(window,"stopped");
   Console.WriteLine("PASS: real native Live setup/running/paused/stopped, supply modes, configuration, expanded rows/large queue, chart/interventions/analysis, window sizes and session preservation.");vm.Dispose();desktop.Shutdown(0);
  }catch(Exception e){Console.Error.WriteLine(e);vm.Dispose();desktop.Shutdown(1);}};
  base.OnFrameworkInitializationCompleted();
 }
 private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
 private static void Save(Window w,string name){var dir="/tmp/flowsim-visual-polish/"+Phase;Directory.CreateDirectory(dir);w.UpdateLayout();using var b=new RenderTargetBitmap(new PixelSize((int)w.Bounds.Width,(int)w.Bounds.Height));b.Render(w);b.Save(Path.Combine(dir,name+".png"));}
}
