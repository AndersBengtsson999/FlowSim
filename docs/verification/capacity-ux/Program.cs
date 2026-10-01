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
 [STAThread] public static int Main(string[] args) => AppBuilder.Configure<CapacityUxApp>().UsePlatformDetect().WithInterFont().StartWithClassicDesktopLifetime([]);
}
public sealed class CapacityUxApp : Avalonia.Application
{
 public override void Initialize(){Styles.Add(new FluentTheme());RequestedThemeVariant=ThemeVariant.Light;}
 public override void OnFrameworkInitializationCompleted(){
  var desktop=(IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;var main=new MainWindowViewModel();var vm=main.Simple.Live;
  var window=new MainWindow{DataContext=main,Width=1280,Height=960};desktop.MainWindow=window;
  window.Opened+=async(_,_)=>{try{
   await Task.Delay(300);
   var liveView=window.GetVisualDescendants().OfType<LiveView>().Single();
   var scroll=liveView.GetVisualDescendants().OfType<ScrollViewer>().Single(s=>s.Name=="LiveScroll");
   var productivity=liveView.GetVisualDescendants().OfType<Expander>().Single(e=>e.Header?.ToString()=="Productivity · Development / Code Review / Testing");productivity.IsExpanded=true;
   await Task.Delay(150);
   foreach(var name in new[]{"Live Developers","Live Testers","Developer Availability (%)","Tester Availability (%)","Development Productivity","Code Review Productivity","Testing Productivity"})Check(Input(name)!=null,"Setup contains "+name);
   Check(!liveView.GetVisualDescendants().OfType<TextBox>().Any(t=>AutomationProperties.GetName(t)?.Contains("Capacity")==true),"No per-person capacity editor anywhere in Live");
   Input("Developer Availability (%)").SetCurrentValue(TextBox.TextProperty,"80");Input("Development Productivity").SetCurrentValue(TextBox.TextProperty,"1.5");vm.WorkSupply="Always available";
   scroll.Offset=default;await Task.Delay(150);Save(window,"configuration");
   var more=liveView.GetVisualDescendants().OfType<Expander>().Single(e=>e.Header?.ToString()=="More settings");more.IsExpanded=true;await Task.Delay(100);
   Check(!liveView.GetVisualDescendants().OfType<TextBox>().Any(t=>AutomationProperties.GetName(t)?.Contains("Capacity")==true),"More settings has no nominal scaling");more.IsExpanded=false;
   Invoke("Start");await Task.Delay(80);Invoke("Pause");Invoke("Step");await Task.Delay(150);
   var d=vm.Live!.CurrentSnapshot;Check(d.AvailableDeveloperCapacity==4,"5 people × 80% = 4 available");Check(d.AvailableTesterCapacity==2,"2 testers × 100% = 2 available");
   Check(d.UsedDeveloperCapacity==4 && d.DevelopmentWork==6,"4 consumed × 1.5 = 6 work");Check(vm.LiveStatus!.DeveloperUtilization==1,"100% utilization");
   Check(window.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text==vm.StatusSecondaryGroups[0].Value),"Runtime used/available visible");
   foreach(var size in new[]{new Size(1280,800),new Size(960,720)}){
    window.Width=size.Width;window.Height=size.Height;scroll.Offset=default;await Task.Delay(180);window.UpdateLayout();
    var board=liveView.GetVisualDescendants().OfType<ListBox>().Single(b=>b.Name=="LiveFlow");Check(board.TranslatePoint(default,window)!.Value.Y+board.Bounds.Height<=scroll.TranslatePoint(default,window)!.Value.Y+scroll.Bounds.Height,"Complete Flow Board fits");
    Check(scroll.Extent.Width<=scroll.Viewport.Width+1,"No horizontal overflow");Save(window,"runtime-"+size.Width);
   }
   window.Width=1280;window.Height=960;Invoke("Change");await Task.Delay(150);
   Check(!vm.ChangeFields.Any(f=>f.Label.Contains("Capacity")),"No capacity intervention");
   foreach(var name in new[]{"Developers","Testers","Developer Availability (%)","Tester Availability (%)","Development Productivity (x)","Code Review Productivity (x)","Testing Productivity (x)"})
    Check(liveView.GetVisualDescendants().OfType<TextBox>().Any(t=>AutomationProperties.GetName(t)==name),"Change contains "+name);
   scroll.Offset=default;Save(window,"change");vm.CancelChangeCommand.Execute(null);
   // Loaded custom values remain in effect and are explained without adding normal editors.
   var custom=LiveSimulation.Start(LiveSimulation.Demo with {DeveloperCapacityPerDay=1.7,TesterCapacityPerDay=.4},WorkArrivalMode.AlwaysAvailable);custom.Step();
   vm.Load(LiveSessionJson.Load(LiveSessionJson.Save(custom)));vm.SetupExpanded=true;scroll.Offset=default;await Task.Delay(150);Save(window,"custom-loaded");
   Check(vm.ConfigurationDetails.Contains("Advanced nominal scaling retained"),"Custom scaling disclosed");vm.BeginChange();vm.Draft.DeveloperAvailability="80";vm.ApplyChanges();vm.Step();
   Check(Math.Abs(vm.LiveStatus!.DeveloperAvailable!.Value-6.8)<1e-10,"Loaded scaling survives normal change");
   main.Simple.AdvancedCommand.Execute(null);await Task.Delay(200);
   var advanced=window.GetVisualDescendants().OfType<AdvancedWorkspace>().Single();
   var panel=advanced.GetVisualDescendants().OfType<Expander>().Single(e=>e.Header?.ToString()=="Advanced · capacity, seeds and examples");panel.IsExpanded=true;await Task.Delay(150);
   foreach(var role in new[]{"Developer","Tester"}){
    var input=advanced.GetVisualDescendants().OfType<TextBox>().Single(t=>AutomationProperties.GetName(t)==role+" Capacity per Person / Day");
    Check(ToolTip.GetTip(input)?.ToString()==FlowPresentation.AdvancedCapacityHelp,"Advanced help");input.BringIntoView();
   }
   await Task.Delay(150);Save(window,"advanced");
   Console.WriteLine("PASS: actual native Live setup and Change contain all seven normal inputs and no per-person capacity editors, including More settings. 5 developers × 80% = 4 available; 4 consumed × 1.5 productivity = 6 effective work; utilization 100%; testers have 2 available. Compact runtime at 1280×800 and 960×720. Custom 1.7/.4 scaling survives load and normal intervention. Advanced editors retained with explicit labels/help.");
   vm.Dispose();desktop.Shutdown(0);
   TextBox Input(string name)=>liveView.GetVisualDescendants().OfType<TextBox>().Single(t=>AutomationProperties.GetName(t)==name);
   void Invoke(string label){var b=liveView.GetVisualDescendants().OfType<Button>().Single(b=>b.Content?.ToString()==label);Check(b.IsEnabled,label+" enabled");ControlAutomationPeer.CreatePeerForElement(b)!.GetProvider<IInvokeProvider>()!.Invoke();}
  }catch(Exception e){Console.Error.WriteLine(e);vm.Dispose();desktop.Shutdown(1);}};
  base.OnFrameworkInitializationCompleted();
 }
 private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
 private static void Save(Window w,string name){var dir="/tmp/flowsim-capacity-ux";Directory.CreateDirectory(dir);w.UpdateLayout();using var b=new RenderTargetBitmap(new PixelSize((int)w.Bounds.Width,(int)w.Bounds.Height));b.Render(w);b.Save(Path.Combine(dir,name+".png"));}
}
