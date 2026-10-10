using Avalonia;
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
using ModelValidationV2;
internal static class Program
{
    [STAThread] public static int Main()=>AppBuilder.Configure<ValidationApp>().UsePlatformDetect().With(new AvaloniaNativePlatformOptions{RenderingMode=[AvaloniaNativeRenderingMode.Software]}).WithInterFont().StartWithClassicDesktopLifetime([]);
}
public sealed class ValidationApp:Avalonia.Application
{
    public override void Initialize(){Styles.Add(new FluentTheme());RequestedThemeVariant=ThemeVariant.Light;}
    public override void OnFrameworkInitializationCompleted()
    {
        var desktop=(IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
        var main=new MainWindowViewModel();var vm=main.Simple.Live;var window=new MainWindow{DataContext=main,Width=1280,Height=850};desktop.MainWindow=window;
        window.Opened+=async(_,_)=>{
            try{
                await Task.Delay(100);var view=window.GetVisualDescendants().OfType<LiveView>().Single();var scroll=view.GetVisualDescendants().OfType<ScrollViewer>().Single(s=>s.Name=="LiveScroll");
                foreach(var scenario in new[]{"A","B","C","D"})
                {
                    var live=Validation.Run(scenario);vm.Load(live);vm.RollingWindow=50;
                    vm.TrendMetric=vm.TrendMetrics.Single(m=>m.Metric==(scenario=="B"||scenario=="D"?LiveTrendMetric.TestingQueue:LiveTrendMetric.ReadyForRelease));
                    await Task.Delay(100);scroll.Offset=default;await Task.Delay(100);
                    Validation.Require(vm.Flow.Sum(r=>r.Count)==live.Session.WorkItems.Count,"UI count conservation");
                    var p=LivePerformance.Rolling(live.Session,50);Validation.Near(vm.Performance!.Throughput,p.Throughput,"UI period matches model");
                    foreach(var row in vm.Flow.Where(r=>r.Queue is not null))Validation.Require(view.GetVisualDescendants().OfType<TextBlock>().Any(t=>ReferenceEquals(t.DataContext,row)&&t.Text==row.QueueCountText),"Visible bound count and arrow");
                    Save(window,scenario+"-1280");window.Width=960;await Task.Delay(100);Save(window,scenario+"-960");window.Width=1280;
                    Console.WriteLine($"PASS native {scenario}: visible bound queue counts, arrows and rolling period.");
                }
                var comparison=view.GetVisualDescendants().OfType<Expander>().Single(e=>e.Header?.ToString()=="Before & After an intervention");comparison.IsExpanded=true;await Task.Delay(100);comparison.BringIntoView();await Task.Delay(100);Save(window,"D-before-after");
                Validation.Require(vm.Comparison!.Before.FirstDay==101&&vm.Comparison.Before.LastDay==150&&vm.Comparison.After.FirstDay==151&&vm.Comparison.After.LastDay==200,"UI Before/After boundaries");
                vm.Dispose();desktop.Shutdown(0);
            }catch(Exception e){Console.Error.WriteLine(e);vm.Dispose();desktop.Shutdown(1);}
        };
        base.OnFrameworkInitializationCompleted();
    }
    static void Save(Window window,string name){var path="/tmp/flowsim-validation-v2";Directory.CreateDirectory(path);window.UpdateLayout();using var image=new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width,(int)window.Bounds.Height));image.Render(window);image.Save(Path.Combine(path,name+".png"));}
}
