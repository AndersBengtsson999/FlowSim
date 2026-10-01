using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Simulation.Core;
using Simulation.UI.ViewModels;
using Simulation.UI.Views;

internal static class Program
{
 [STAThread] public static int Main(string[] args) => AppBuilder.Configure<CheckApp>().UsePlatformDetect().WithInterFont().StartWithClassicDesktopLifetime([]);
}
public sealed class CheckApp : Avalonia.Application
{
 public override void Initialize() { Styles.Add(new FluentTheme()); RequestedThemeVariant=ThemeVariant.Light; }
 public override void OnFrameworkInitializationCompleted() {
  var desktop=(IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
  var main=new MainWindowViewModel(); var vm=main.Simple.Live;
  var window=new MainWindow {DataContext=main, Width=1280, Height=800}; desktop.MainWindow=window;
  window.Opened+=async (_,_)=> {try {
   ToggleButton Header(string name)=>window.GetVisualDescendants().OfType<ToggleButton>().Single(b=>b.Classes.Contains("flowDisclosure") && AutomationProperties.GetName(b)==name);
   void Toggle(string name) {
    var button=Header(name);
    var peer=ControlAutomationPeer.CreatePeerForElement(button)!;
    peer.GetProvider<IToggleProvider>()!.Toggle();
   }
   vm.WorkSupply="Always available"; vm.Start(); vm.Pause(); vm.Step(); await Task.Delay(400); window.UpdateLayout();
   Save(window,"initial");
   var before=JsonSerializer.Serialize(vm.Live!.Capture());
   Check(Header("Development").IsChecked==false,"Collapsed initial header"); Toggle("Development"); await Task.Delay(100);
   var row=vm.Flow.Single(r=>r.State==WorkItemStatus.Development);
   Check(row.IsExpanded && row.DisclosureChevron=="⌄","Native toggle expanded and chevron");
   Check(row.VisibleItems.Count==row.Count && row.Count>0,"Development content");
   Check(window.GetVisualDescendants().OfType<TextBlock>().Any(t=>row.VisibleItems.Any(i=>i.Id==t.Text)),"Actual IDs rendered");
   Save(window,"development-expanded"); Toggle("Development"); Check(!row.IsExpanded,"Second native toggle collapsed");
   Check(before==JsonSerializer.Serialize(vm.Live.Capture()),"Disclosure leaves simulation unchanged");
   Toggle("Waiting for Testing"); vm.Resume();
   for(var n=0;n<30;n++) { vm.Tick(); await Task.Delay(5); }
   vm.Pause(); await Task.Delay(100); window.UpdateLayout(); row=vm.Flow.Single(r=>r.State==WorkItemStatus.WaitingForTesting);
   Check(row.IsExpanded && Header(row.Name).IsChecked==true,"Expanded queue survives running updates");
   Check(row.VisibleItems.Select(i=>i.Id).SequenceEqual(vm.Live.CurrentSnapshot.Items.Where(i=>i.State==row.State).Select(i=>i.Id)),"Latest exact queue members");
   vm.BeginChange(); vm.Draft.TesterAvailability="0"; vm.ApplyChanges(); vm.TargetDay="160"; await vm.RunToDayAsync(); await Task.Delay(150); window.UpdateLayout();
   row=vm.Flow.Single(r=>r.State==WorkItemStatus.WaitingForTesting);
   Check(row.IsExpanded && row.Count>=44 && row.VisibleItems.Count==row.Count,"Large queue complete");
   var header=Header(row.Name); header.BringIntoView(); await Task.Delay(100); Save(window,"large-queue");
   var scroll=window.GetVisualDescendants().OfType<ScrollViewer>().Single(v=>v.Name=="LiveScroll");
   Check(scroll.Extent.Width<=scroll.Viewport.Width+1,"No horizontal overflow");
   var last=row.VisibleItems[^1].Id;
   var lastText=window.GetVisualDescendants().OfType<TextBlock>().Single(t=>t.Text==last);
   lastText.BringIntoView(); await Task.Delay(100); Save(window,"large-queue-last-item");
   Check(row.IsExpanded,"Interacting with content does not collapse");
   scroll.Offset=default; await Task.Delay(150); window.UpdateLayout(); Toggle("Waiting for Testing"); await Task.Delay(100); Toggle("Backlog");
   Check(vm.Flow.Single(r=>r.State==WorkItemStatus.Backlog).IsEmpty,"No fictitious backlog");
   var state=JsonSerializer.Serialize(vm.Live.Capture());
   main.Simple.OpenAnalyzeCommand.Execute(null); main.Simple.OpenLiveCommand.Execute(null);
   Check(vm.Flow.Single(r=>r.State==WorkItemStatus.Backlog).IsExpanded,"Navigation retains expansion");
   Check(state==JsonSerializer.Serialize(vm.Live.Capture()),"Navigation leaves session intact");
   await Task.Delay(100); Save(window,"empty-backlog");
   Console.WriteLine($"PASS: native disclosure toggle twice, exact Development items, running queue updates, {row.Count} queue items including last, no truncation/extra backlog, navigation and unchanged session.");
   vm.Dispose();desktop.Shutdown(0);
  }catch(Exception e){Console.Error.WriteLine(e);vm.Dispose();desktop.Shutdown(1);}};
  base.OnFrameworkInitializationCompleted();
 }
 private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
 private static void Save(Window window,string name){Directory.CreateDirectory("/tmp/flowsim-disclosure");window.UpdateLayout();using var bitmap=new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width,(int)window.Bounds.Height));bitmap.Render(window);bitmap.Save($"/tmp/flowsim-disclosure/{name}.png");}
}
