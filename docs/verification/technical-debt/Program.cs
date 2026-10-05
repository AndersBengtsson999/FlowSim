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
using Simulation.UI.Controls;
using Simulation.UI.ViewModels;
using Simulation.UI.Views;
internal static class Program
{
 [STAThread] public static int Main(string[] args)=>AppBuilder.Configure<DebtApp>().UsePlatformDetect().WithInterFont().StartWithClassicDesktopLifetime([]);
}
public sealed class DebtApp:Avalonia.Application
{
 public override void Initialize(){Styles.Add(new FluentTheme());RequestedThemeVariant=ThemeVariant.Light;}
 public override void OnFrameworkInitializationCompleted(){
  var desktop=(IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;var main=new MainWindowViewModel();var vm=main.Simple.Live;
  var window=new MainWindow{DataContext=main,Width=1280,Height=800};desktop.MainWindow=window;
  window.Opened+=async(_,_)=>{try{
   await Task.Delay(300);var view=window.GetVisualDescendants().OfType<LiveView>().Single();
   var scroll=view.GetVisualDescendants().OfType<ScrollViewer>().Single(s=>s.Name=="LiveScroll");
   var settings=view.GetVisualDescendants().OfType<Expander>().Single(e=>e.Header?.ToString()=="Technical Debt");settings.IsExpanded=true;await Task.Delay(100);settings.BringIntoView();await Task.Delay(150);Save(window,"settings");
   Check(Input("Shortcut Rate (%)").Text=="0","Default zero shortcuts");Check(Input("Debt Repayment (%)").Text=="0","Default zero repayment");
   vm.WorkSupply="Always available";Invoke("Start");await Task.Delay(80);Invoke("Pause");await Advance(100);
   Check(vm.Live!.Session.DebtState.Amount==0 && vm.DebtBar.Overhead==0,"A baseline");Check(!vm.DebtVisible,"Baseline compact");Snapshot("A-baseline");
   await Change(("Shortcut Rate (%)","40"),("Shortcut Effort Reduction (%)","30"),("Debt Tolerance (%)","5"));
   await Advance(220);Check(vm.Live.Session.DebtState.Amount>0,"B debt builds");Check(vm.Live.Session.WorkItems.Any(w=>w.DevelopmentPlan?.IsShortcut==true),"B shortcut choices visible");Snapshot("B-build-debt");
   var shortcutRow=vm.Flow.First(r=>r.Items.Any(w=>w.DevelopmentPlan?.IsShortcut==true));shortcutRow.IsExpanded=true;await Task.Delay(150);
   var shortcutText=view.GetVisualDescendants().OfType<TextBlock>().First(t=>t.Text?.StartsWith("Shortcut · Base")==true);shortcutText.BringIntoView();await Task.Delay(150);Save(window,"B-shortcut-item");shortcutRow.IsExpanded=false;
   var history=JsonSerializer.Serialize(vm.Live.Session.Days);var items=JsonSerializer.Serialize(vm.Live.Session.Capture().WorkItems);
   await Change(("Debt Tolerance (%)","50"));Check(history==JsonSerializer.Serialize(vm.Live.Session.Days),"C no history rewrite");Check(items==JsonSerializer.Serialize(vm.Live.Session.Capture().WorkItems),"C active efforts fixed");
   Check(vm.DebtBar.Ratio>0 && vm.DebtBar.Overhead==0,"C debt below tolerance");await Advance(240);
   Check(vm.Live.Session.WorkItems.Where(w=>w.DevelopmentStartedDay>=220).All(w=>w.DevelopmentPlan?.Overhead is null or 0),"C future starts no overhead");Snapshot("C-within-tolerance");
   await Change(("Debt Tolerance (%)","1"));Check(vm.DebtBar.Zone=="Above tolerance" && vm.DebtBar.Overhead>0,"D excess debt");await Advance(260);Snapshot("D-above-tolerance");
   Check(vm.Live.Session.WorkItems.Any(w=>w.DevelopmentStartedDay>=240 && w.DevelopmentPlan?.Overhead>0),"D new starts get overhead");
   vm.Flow.Single(r=>r.State==WorkItemStatus.Development).IsExpanded=true;await Task.Delay(120);Snapshot("D-shortcut-items");vm.Flow.Single(r=>r.State==WorkItemStatus.Development).IsExpanded=false;
   vm.CheckpointCommand.Execute(null);var checkpoint=vm.Checkpoints.Single();var debtBefore=vm.Live.Session.DebtState.Amount;
   await Change(("Shortcut Rate (%)","0"),("Debt Repayment (%)","30"),("Development Productivity (x)","1.5"));
   await Advance(261);var d=vm.Live.CurrentSnapshot;Check(d.UsedDebtRepaymentCapacity>0,"E repayment consumes capacity");
   Check(Math.Abs(d.Debt!.Repaid-1.5*d.UsedDebtRepaymentCapacity)<1e-9,"F productivity applies without collaboration");
   Check(d.UsedDeveloperCapacity<=d.AvailableDeveloperCapacity+1e-9,"F utilization bound");Snapshot("E-F-repayment");
   var detail=view.GetVisualDescendants().OfType<Button>().Single(b=>b.Content?.ToString()=="Details");Check(detail.Flyout is not null,"G details available without Advanced");
   var flyout=(Flyout)detail.Flyout!;flyout.ShowAt(detail);await Task.Delay(150);var detailText=(TextBlock)flyout.Content!;
   Check(detailText.Text==vm.DebtDetails,"Actual bound detail text");
   using(var detailImage=new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(detailText.Bounds.Width),(int)Math.Ceiling(detailText.Bounds.Height)))){detailImage.Render(detailText);detailImage.Save("/tmp/flowsim-technical-debt/E-details.png");}
   flyout.Hide();Console.WriteLine("Repayment detail: "+vm.DebtDetails);
   await Advance(360);Check(vm.Live.Session.DebtState.Amount<debtBefore,"E debt falls");Check(vm.Live.Session.DebtState.Amount==0,"E debt exhausted");Check(vm.Live.CurrentSnapshot.UsedDebtRepaymentCapacity==0,"E no reservation without debt");Snapshot("E-debt-exhausted");
   var expected=JsonSerializer.Serialize(vm.Live.Session.Capture());Check(expected==JsonSerializer.Serialize(LiveSessionJson.Load(LiveSessionJson.Save(vm.Live)).Session.Capture()),"Saved debt state exact");
   vm.SelectedCheckpoint=checkpoint;vm.RestoreCommand.Execute(null);await Change(("Shortcut Rate (%)","0"),("Debt Repayment (%)","30"),("Development Productivity (x)","1.5"));await Advance(360);
   Check(expected==JsonSerializer.Serialize(vm.Live.Session.Capture()),"Checkpoint future exact");
   vm.TrendRange="Full Session";vm.TrendMetric=vm.TrendMetrics.Single(m=>m.Metric==LiveTrendMetric.TechnicalDebtRatio);
   view.GetVisualDescendants().OfType<LivePerformanceTrendChart>().Single().BringIntoView();await Task.Delay(150);Save(window,"G-debt-trend");
   Check(vm.Trend.Points.All(p=>p.Value==100*(vm.Live.Session.Days[p.Day-1].Debt?.State.Ratio??0)),"Daily actual ratio trend");
   vm.SelectedIntervention=vm.Live.Session.Changes.First();var comparison=view.GetVisualDescendants().OfType<Expander>().Single(e=>e.Header?.ToString()=="Before & After an intervention");comparison.IsExpanded=true;await Task.Delay(150);window.UpdateLayout();
   var before=view.GetVisualDescendants().OfType<TextBlock>().Single(t=>t.Text==vm.BeforePeriod);scroll.Offset=new Vector(0,scroll.Offset.Y+before.TranslatePoint(default,window)!.Value.Y-160);await Task.Delay(150);Save(window,"G-before-after");comparison.IsExpanded=false;
   vm.SelectedCheckpoint=checkpoint;vm.RestoreCommand.Execute(null);
   foreach(var rework in new[]{false,true}){
    if(rework){vm.BeginChange();vm.Draft.DefectsEnabled=true;vm.ApplyChanges();}
    foreach(var size in new[]{new Size(1280,800),new Size(960,720)}){
     window.Width=size.Width;window.Height=size.Height;scroll.Offset=default;await Task.Delay(200);window.UpdateLayout();
     var board=view.GetVisualDescendants().OfType<ListBox>().Single(b=>b.Name=="LiveFlow");var bottom=board.TranslatePoint(default,window)!.Value.Y+board.Bounds.Height;
     Check(bottom<=scroll.TranslatePoint(default,window)!.Value.Y+scroll.Bounds.Height,"Complete board with debt, "+size+", rework="+rework);Check(scroll.Extent.Width<=scroll.Viewport.Width+1,"No horizontal overflow");
     Save(window,$"G-size-{size.Width}-rework-{rework}");Console.WriteLine($"Board bottom {bottom}; size {window.Bounds.Size}; rework {rework}");
    }
   }
   Console.WriteLine("PASS native A–G: baseline, shortcuts/debt, zero overhead within tolerance, excess overhead on future starts, repayment/productivity/capacity, exact save/checkpoint replay, bar/details/daily trend/Before-After and compact layouts.");
   vm.Dispose();desktop.Shutdown(0);
   TextBox Input(string name)=>view.GetVisualDescendants().OfType<TextBox>().Single(t=>t.IsEffectivelyVisible && AutomationProperties.GetName(t)==name);
   void Invoke(string text){var b=view.GetVisualDescendants().OfType<Button>().Single(b=>b.Content?.ToString()==text);ControlAutomationPeer.CreatePeerForElement(b)!.GetProvider<IInvokeProvider>()!.Invoke();}
   async Task Advance(int day){vm.TargetDay=day.ToString();await vm.RunToDayAsync();await Task.Delay(120);Check(vm.Day==day,"Reached day "+day);}
   async Task Change(params (string Label,string Value)[] fields){Invoke("Change");await Task.Delay(100);foreach(var field in fields)Input(field.Label).SetCurrentValue(TextBox.TextProperty,field.Value);Invoke("Apply Changes");await Task.Delay(120);Check(!vm.IsEditing,"Change applied");}
   void Snapshot(string name){scroll.Offset=default;window.UpdateLayout();Save(window,name);Console.WriteLine(FormattableString.Invariant($"{name}: day {vm.Day}; debt {vm.Live!.Session.DebtState.Amount:0.###}; scope {vm.Live.Session.DebtState.CumulativeDevelopmentScope:0.###}; ratio {vm.DebtBar.Ratio:0.####}; tolerance {vm.DebtBar.Tolerance:0.####}; overhead {vm.DebtBar.Overhead:0.####}."));}
  }catch(Exception e){Console.Error.WriteLine(e);vm.Dispose();desktop.Shutdown(1);}};
  base.OnFrameworkInitializationCompleted();
 }
 static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
 static void Save(Window w,string name){var dir="/tmp/flowsim-technical-debt";Directory.CreateDirectory(dir);w.UpdateLayout();using var b=new RenderTargetBitmap(new PixelSize((int)w.Bounds.Width,(int)w.Bounds.Height));b.Render(w);b.Save(Path.Combine(dir,name+".png"));}
}
