using System.Text.Json;
using Avalonia.Automation;
using Avalonia.Controls.Primitives;
using Simulation.Application;
using Simulation.Core;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Simulation.UI.ViewModels;
using Simulation.UI.Views;

internal static class Program
{
 [STAThread] public static int Main(string[] args) {
  CheckApp.Label = args.FirstOrDefault() ?? "before";
  return AppBuilder.Configure<CheckApp>().UsePlatformDetect().WithInterFont().StartWithClassicDesktopLifetime([]);
 }
}
public sealed class CheckApp : Avalonia.Application
{
 public static string Label = "before";
 public override void Initialize() { Styles.Add(new FluentTheme()); RequestedThemeVariant = ThemeVariant.Light; }
 public override void OnFrameworkInitializationCompleted() {
  var desktop = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
  var main = new MainWindowViewModel(); var vm = main.Simple.Live;
  var window = new MainWindow { DataContext = main }; desktop.MainWindow = window;
  window.Opened += async (_, _) => { try {
   bool Visible(Control c) => c.IsVisible && c.GetVisualAncestors().OfType<Control>().All(p=>p.IsVisible);
   Button Button(string content) => window.GetVisualDescendants().OfType<Button>().Single(b=>Visible(b) && b.Content?.ToString()==content);
   void Click(string content) { var b=Button(content); Check(b.Command!.CanExecute(b.CommandParameter), content + " enabled"); b.Command.Execute(b.CommandParameter); }
   var setup=window.GetVisualDescendants().OfType<Expander>().Single(e=>e.Name=="SetupSection");
   var scroll=window.GetVisualDescendants().OfType<ScrollViewer>().Single(v=>v.Name=="LiveScroll");
   window.Width=1280; window.Height=800; await Task.Delay(200);
   Check(vm.SetupExpanded && setup.IsExpanded, "Setup initially expanded"); Save(window,"setup");
   vm.WorkSupply = "Always available"; Click("Start"); Check(!setup.IsExpanded, "Auto collapse"); Click("Pause");
   Click("Step"); Check(vm.Day==1,"Step exactly one day");
   var advance=Button("Advance…"); advance.Flyout!.ShowAt(advance); await Task.Delay(100);
   var panel=(Control)((Flyout)advance.Flyout).Content!;
   var target=panel.GetVisualDescendants().OfType<TextBox>().Single(); target.Text="100"; await Task.Delay(50);
   var run=panel.GetVisualDescendants().OfType<Button>().Single(); Check(run.Command!.CanExecute(null),"Advance enabled"); run.Command.Execute(null);
   while(vm.IsFastAdvancing) await Task.Delay(10);
   Check(vm.Day==100,"Advance binding"); advance.Flyout.Hide(); Click("Checkpoint");
   Click("Change"); vm.Draft.DeveloperAvailability="85"; vm.ChangeLabel="Availability"; Click("Apply Changes");
   Check(vm.LatestIntervention.Contains("Day 100"),"Latest change");
   vm.TargetDay="140"; await vm.RunToDayAsync();
   foreach (var quality in new[] { false, true }) {
    if(quality) { vm.BeginChange(); vm.Draft.DefectsEnabled=true; vm.ApplyChanges(); }
    foreach (var size in new[] {new Size(1280,800),new Size(1600,1000),new Size(960,720)}) {
     window.Width=size.Width; window.Height=size.Height; scroll.Offset=default; await Task.Delay(200); window.UpdateLayout();
     var board=window.GetVisualDescendants().OfType<ListBox>().Single(b=>b.Name=="LiveFlow");
     var top=board.TranslatePoint(default,window)!.Value.Y;
     var bottom=top+board.Bounds.Height;
     var viewportBottom=scroll.TranslatePoint(default,window)!.Value.Y+scroll.Bounds.Height;
     var heading=window.GetVisualDescendants().OfType<TextBlock>().Single(t=>t.Name=="TrendHeading");
     var trendTop=heading.TranslatePoint(default,window)!.Value.Y;
     Console.WriteLine($"{Label} {window.Bounds.Size}, rework={quality}: board top={top}, bottom={bottom}, height={board.Bounds.Height}; viewport bottom={viewportBottom}; Trend heading={trendTop}");
     Check(bottom<=viewportBottom,"Complete board above fold"); Check(trendTop+heading.Bounds.Height<=viewportBottom,"Trend heading above fold");
     Check(scroll.Extent.Width<=scroll.Viewport.Width+1,"No horizontal overflow");
     foreach(var name in new[]{"Pause","Resume","Step","Advance…","Change","Checkpoint","Reset"}) {
      var button=Button(name); Check(button.TranslatePoint(default,window)!.Value.Y+button.Bounds.Height<=viewportBottom,"Toolbar visible: "+name);
     }
     Check(board.GetVisualDescendants().OfType<ListBoxItem>().Count()==(quality?9:7),"Every flow stage rendered");
     Save(window,$"{Label}-{size.Width}"+(quality?"-rework":""));
    }
   }
   vm.TrendMetric=vm.TrendMetrics.Single(m=>m.Metric==LiveTrendMetric.AvailableDevelopers); vm.TrendRange="Full Session";
   var state=JsonSerializer.Serialize(vm.Live!.Capture()); var status=vm.StatusDelivery+vm.StatusCapacity+vm.StatusQueues+vm.LatestIntervention;
   setup.IsExpanded=true; await Task.Delay(30); setup.IsExpanded=false;
   foreach(var name in new[]{"Team Performance","Before & After an intervention","Flow details and allocations"}) {
    var section=window.GetVisualDescendants().OfType<Expander>().Single(e=>e.Header?.ToString()==name);
    section.IsExpanded=true; await Task.Delay(40); section.BringIntoView(); await Task.Delay(40); Save(window,name.Split(' ')[0]); section.IsExpanded=false;
   }
   modelCheck();
   void modelCheck() {
    main.Simple.OpenAnalyzeCommand.Execute(null); main.Simple.OpenLiveCommand.Execute(null);
    Check(state==JsonSerializer.Serialize(vm.Live.Capture()),"Exact Live history/configuration/random/checkpoints preserved");
    Check(status==vm.StatusDelivery+vm.StatusCapacity+vm.StatusQueues+vm.LatestIntervention,"Status preserved");
    Check(vm.TrendMetric.Metric==LiveTrendMetric.AvailableDevelopers && vm.TrendRange=="Full Session","Trend selection preserved");
   }
   scroll.Offset=default; Click("Reset"); Check(vm.SetupExpanded && !vm.HasSession,"Reset restores setup");
   Console.WriteLine("PASS: startup, toolbar, advance flyout, intervention, checkpoints, laptop/desktop/narrow including rework, disclosures, exact state preservation, reset.");
   vm.Dispose(); desktop.Shutdown(0);
  } catch(Exception e) {Console.Error.WriteLine(e);desktop.Shutdown(1);} };
  base.OnFrameworkInitializationCompleted();
 }
 private static void Check(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
 private static void Save(Window window,string name) {
  Directory.CreateDirectory("/tmp/flowsim-compact"); window.UpdateLayout();
  using var bitmap=new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width,(int)window.Bounds.Height));
  bitmap.Render(window); bitmap.Save($"/tmp/flowsim-compact/{name}.png");
 }
}
