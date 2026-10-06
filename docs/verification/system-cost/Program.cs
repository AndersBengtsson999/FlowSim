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

internal static class Program
{
    [STAThread] public static int Main(string[] args) => AppBuilder.Configure<SystemCostApp>().UsePlatformDetect().With(new AvaloniaNativePlatformOptions { RenderingMode = [AvaloniaNativeRenderingMode.Software] }).WithInterFont().StartWithClassicDesktopLifetime([]);
}
public sealed class SystemCostApp : Avalonia.Application
{
    public override void Initialize() { Styles.Add(new FluentTheme()); RequestedThemeVariant = ThemeVariant.Light; }
    public override void OnFrameworkInitializationCompleted()
    {
        var desktop=(IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
        var main=new MainWindowViewModel(); var vm=main.Simple.Live;
        var window=new MainWindow { DataContext=main,Width=1280,Height=850 };desktop.MainWindow=window;
        var baseline=LiveSimulation.Demo with { DeveloperCount=1,TesterCount=1,DevelopmentWipLimit=1,DevelopmentEffort=5,CodeReviewEffort=1,TestingEffort=2,Debt=new(),Productivity=new(),Quality=new() };
        window.Opened+=async(_,_)=>
        {
            try
            {
                await Task.Delay(200);
                var view=window.GetVisualDescendants().OfType<LiveView>().Single();
                var scroll=view.GetVisualDescendants().OfType<ScrollViewer>().Single(s=>s.Name=="LiveScroll");
                await Run(baseline,"baseline");
                var cost=vm.Performance!.AverageDeliveryCost!;Check(cost==new DeliveryCost(5,1,0,2),"Baseline exact lifecycle breakdown");
                foreach(var item in vm.Live!.Session.WorkItems.Where(w=>w.State==WorkItemStatus.Done).Take(3))
                    Console.WriteLine($"Completed {item.Id}: Dev {item.DeliveryCost.Development}, Review {item.DeliveryCost.CodeReview}, Rework {item.DeliveryCost.Rework}, Test {item.DeliveryCost.Testing}, Total {item.DeliveryCost.Total}");
                vm.Flow.Single(r=>r.State==WorkItemStatus.Done).IsExpanded=true;await Task.Delay(100);
                var itemText=view.GetVisualDescendants().OfType<TextBlock>().First(t=>t.Text?.StartsWith("LIVE-")==true && ToolTip.GetTip(t)?.ToString()?.Contains("Observed item capacity")==true);
                Check(ToolTip.GetTip(itemText)!.ToString()!.Contains("Total"),"Completed item tooltip breakdown");
                vm.Flow.Single(r=>r.State==WorkItemStatus.Done).IsExpanded=false;
                await Run(baseline with{Productivity=new(2,1,1)},"productivity");
                Check(vm.Performance!.AverageDeliveryCost==new DeliveryCost(2.5,1,0,2),"Productivity reduces consumed cost to 2.5/5.5");
                await Run(baseline with{Quality=new(){Enabled=true,CodeReviewDefectProbability=.4,TestingDefectProbability=.2}},"rework");
                Check(vm.Performance!.AverageDeliveryCost!.Rework>0 && vm.Performance.DeliveryCostPerDoneItem>8,"Repeated rework appears in full lifecycle cost");
                await Run(baseline with{DeveloperCount=2},"collaboration");
                Check(vm.Live!.Session.Days.Any(d=>d.CollaborationDevelopmentCapacity>0),"Collaboration used");
                foreach(var item in vm.Live.Session.WorkItems)
                    Check(Math.Abs(item.DeliveryCost.Development-item.Events.Where(e=>e.EventType==WorkItemEventType.CapacityApplied && e.FromState==WorkItemStatus.Development).Sum(e=>e.CapacityConsumed))<1e-9,"All raw collaboration capacity attributed");
                Check(vm.Performance!.AverageDeliveryCost!.Development>5,"Collaboration raw cost exceeds base effort");
                await Run(baseline with{DeveloperCount=3,Debt=new(){ShortcutRate=.5,Repayment=.3}},"repayment");
                var repayment=vm.Live!.Session.Days.Sum(d=>d.UsedDebtRepaymentCapacity);Check(repayment>0,"Repayment consumes system capacity");
                var attributed=vm.Live.Session.WorkItems.Sum(w=>w.DeliveryCost.Total);
                var used=vm.Live.Session.Days.Sum(d=>d.UsedDeveloperCapacity+d.UsedTesterCapacity);
                Check(Math.Abs(used-attributed-repayment)<1e-8,"Repayment excluded, all other consumption attributed");
                Console.WriteLine($"Repayment: total developer+tester use {used:0.###}; item costs {attributed:0.###}; system repayment {repayment:0.###}.");
                vm.TrendMetric=vm.TrendMetrics.Single(m=>m.Metric==LiveTrendMetric.SystemCost);
                var chart=view.GetVisualDescendants().OfType<LivePerformanceTrendChart>().Single();
                foreach(var width in new[]{1280d,960d})
                {
                    window.Width=width;window.Height=850;scroll.Offset=default;await Task.Delay(150);
                    var label=view.GetVisualDescendants().OfType<TextBlock>().Single(t=>t.Text=="Cost/Item");
                    var group=label.GetVisualAncestors().OfType<StackPanel>().First();
                    Check(ToolTip.GetTip(group)?.ToString()?.Contains("Development")==true,"Aggregate tooltip uses same completion cohort");
                    Check(ToolTip.GetTip(group)!.ToString()!.Contains("System Cost / Done"), "System cost in tooltip");
                    var withCost=chart.TranslatePoint(default,window)!.Value.Y;var height=chart.Bounds.Height;
                    group.IsVisible=false;window.UpdateLayout();var withoutCost=chart.TranslatePoint(default,window)!.Value.Y;
                    group.IsVisible=true;window.UpdateLayout();
                    Check(Math.Abs(withCost-withoutCost)<1,"Cost does not move chart down at "+width);
                    Check(height==chart.Bounds.Height,"Chart not shrunk");
                    Console.WriteLine($"Width {width}: chart Y with/without Cost = {withCost}/{withoutCost}; height {height}.");
                    Save(window,"layout-"+width);
                }
                vm.BeginChange();vm.Draft.DevelopmentProductivity="2";vm.ChangeLabel="Productivity assumption";vm.ApplyChanges();
                vm.TargetDay="120";await vm.RunToDayAsync();
                var comparison=view.GetVisualDescendants().OfType<Expander>().Single(e=>e.Header?.ToString()=="Before & After an intervention");
                comparison.IsExpanded=true;await Task.Delay(150);comparison.BringIntoView();await Task.Delay(100);Save(window,"before-after");
                Check(vm.ComparisonRows.Any(r=>r.Metric.StartsWith("Delivery Cost / Done Item")),"Before/After cost row");
                Check(vm.ComparisonRows.Any(r=>r.Metric.StartsWith("System Cost / Done Item")),"Before/After system cost row");
                Console.WriteLine("PASS native: baseline completed items, productivity, rework, collaboration, debt repayment exclusion, item/aggregate breakdowns, selected trend, Before/After and unchanged chart position/height.");
                vm.Dispose();desktop.Shutdown(0);
                async Task Run(SimulationRequest request,string name)
                {
                    vm.Reset();vm.Setup.LoadConfiguration(request);vm.WorkSupply="Always available";
                    var start=view.GetVisualDescendants().OfType<Button>().Single(b=>b.Content?.ToString()=="Start");ControlAutomationPeer.CreatePeerForElement(start)!.GetProvider<IInvokeProvider>()!.Invoke();
                    vm.Pause();vm.TargetDay="100";await vm.RunToDayAsync();await Task.Delay(100);scroll.Offset=default;
                    Console.WriteLine(name+": "+LivePerformancePresentation.PeriodCostDetails(vm.Performance));Save(window,name);
                }
            }
            catch(Exception e){Console.Error.WriteLine(e);vm.Dispose();desktop.Shutdown(1);}
        };
        base.OnFrameworkInitializationCompleted();
    }
    static void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);}
    static void Save(Window w,string name)
    {
        Directory.CreateDirectory("/tmp/flowsim-system-cost");w.UpdateLayout();
        using var image=new RenderTargetBitmap(new PixelSize((int)w.Bounds.Width,(int)w.Bounds.Height));image.Render(w);image.Save("/tmp/flowsim-system-cost/"+name+".png");
    }
}
