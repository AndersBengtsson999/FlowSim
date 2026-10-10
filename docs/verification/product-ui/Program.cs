using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using Simulation.Application;
using Simulation.Core;
using Simulation.UI.Views;
using Simulation.UI.ViewModels;
using ModelValidationV2;
internal static class Program
{
 [STAThread] public static int Main()=>AppBuilder.Configure<PreviewApp>().UsePlatformDetect().With(new AvaloniaNativePlatformOptions{RenderingMode=[AvaloniaNativeRenderingMode.Software]}).WithInterFont().StartWithClassicDesktopLifetime([]);
}
public sealed class PreviewApp:Avalonia.Application
{
 public override void Initialize(){Styles.Add(new FluentTheme());RequestedThemeVariant=ThemeVariant.Light;}
 public override void OnFrameworkInitializationCompleted(){
 var desktop=(IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;var main=new MainWindowViewModel();var vm=main.Simple.Live;var w=new MainWindow{DataContext=main,Width=1280,Height=900};desktop.MainWindow=w;
 w.Opened+=async(_,_)=>{try{
 await Task.Delay(300);Save(w,"setup-1280");
 var live=LiveSimulation.Start(Validation.Complex,WorkArrivalMode.AlwaysAvailable);Validation.Until(live,150);vm.Load(live);vm.RollingWindow=50;await Task.Delay(200);Save(w,"live-1280");
 Validation.Require(vm.Instruments.Select(i=>i.Metric).SequenceEqual(vm.Metrics.Take(4)),"Instruments preserve authoritative values");
 foreach(var instrument in vm.Instruments){Validation.Require(instrument.Trend.Points.Count==20,"Sparkline history length");Validation.Require(instrument.Trend.Points.Last().Day==150,"Sparkline current day");}
 var before=Validation.Hash(live.Session.Capture());
 var view=w.GetVisualDescendants().OfType<LiveView>().Single();var scroll=view.GetVisualDescendants().OfType<ScrollViewer>().Single(s=>s.Name=="LiveScroll");
 var row=vm.Flow.First(r=>r.Count>0);var toggle=view.GetVisualDescendants().OfType<ToggleButton>().Single(t=>t.Classes.Contains("flowDisclosure")&&ReferenceEquals(t.DataContext,row));toggle.IsChecked=true;await Task.Delay(100);Validation.Require(row.IsExpanded,"Disclosure two-way binding");toggle.IsChecked=false;
 w.Width=960;w.Height=760;await Task.Delay(250);Save(w,"live-960");
 var nav=w.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="NavigationToggle");nav.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(100);Validation.Require(!w.GetVisualDescendants().OfType<Border>().Single(b=>b.Name=="Sidebar").IsVisible,"Sidebar collapse");Save(w,"live-collapsed-960");nav.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
 vm.BeginChange();await Task.Delay(100);scroll.Offset=default;Save(w,"change-960");vm.CancelChangeCommand.Execute(null);Validation.Require(before==Validation.Hash(live.Session.Capture()),"Presentation mutated simulation");
 main.Simple.OpenChangesCommand.Execute(null);await Task.Delay(100);Validation.Require(main.Simple.ChangesVisible&&!vm.IsRunning,"Compare navigation pauses Live");Save(w,"compare-960");
 main.Simple.OpenRunCommand.Execute(null);await Task.Delay(100);Save(w,"scenarios-960");
 main.Simple.OpenLiveCommand.Execute(null);vm.Reset();await Task.Delay(100);scroll.Offset=default;Save(w,"setup-960");
 main.Simple.AdvancedCommand.Execute(null);await Task.Delay(100);Save(w,"settings-960");
 main.Simple.OpenExploreCommand.Execute(null);await Task.Delay(100);Save(w,"explore-960");
 main.Simple.OpenRunCommand.Execute(null);await main.Simple.RunAsync();await Task.Delay(150);Validation.Require(main.Simple.ResultsVisible&&main.HasResults,"Scenario run and Results navigation");Save(w,"results-960");
 main.Simple.OpenLiveCommand.Execute(null);var congested=LiveSimulation.Start(Validation.Request("B"),WorkArrivalMode.AlwaysAvailable);Validation.Until(congested,150);vm.Load(congested);w.Width=1280;w.Height=900;scroll.Offset=default;await Task.Delay(150);
 var waiting=vm.Flow.Single(r=>r.Name=="Waiting for Testing");Validation.Require(waiting.QueueStrong,"Expected severe queue");
 var queueToggle=view.GetVisualDescendants().OfType<ToggleButton>().Single(t=>t.Classes.Contains("flowDisclosure")&&ReferenceEquals(t.DataContext,waiting));Validation.Require(queueToggle.Background is Avalonia.Media.ISolidColorBrush brush&&brush.Color==Avalonia.Media.Color.Parse("#F1DAD7"),"Queue severity distinct from brand yellow: "+queueToggle.Background);Save(w,"queue-warning-1280");
 scroll.Offset=new Vector(0,420);await Task.Delay(100);Save(w,"performance-1280");
 Console.WriteLine("PASS native setup/live/compare/scenarios/results/explore/settings; 1280 and 960; read-only instruments, disclosure, navigation, scenario run, sidebar collapse and separate queue warning colors.");vm.Dispose();desktop.Shutdown(0);
 }catch(Exception e){Console.Error.WriteLine(e);vm.Dispose();desktop.Shutdown(1);}};base.OnFrameworkInitializationCompleted();}
 static void Save(Window w,string name){w.UpdateLayout();using var bitmap=new RenderTargetBitmap(new PixelSize((int)w.Bounds.Width,(int)w.Bounds.Height));bitmap.Render(w);bitmap.Save(Path.Combine("docs/verification/product-ui/screenshots",name+".png"));}
}
