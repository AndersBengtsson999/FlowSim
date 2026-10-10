using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
    [STAThread] public static int Main(string[] args)
    {
        var builder=AppBuilder.Configure<ReleaseApp>();
        builder=args.Contains("--headless") ? builder.UseHeadless(new AvaloniaHeadlessPlatformOptions {UseHeadlessDrawing=false}).UseSkia() : builder.UsePlatformDetect().With(new AvaloniaNativePlatformOptions{RenderingMode=[AvaloniaNativeRenderingMode.Software]});
        return builder.WithInterFont().StartWithClassicDesktopLifetime([]);
    }
}
public sealed class ReleaseApp : Avalonia.Application
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
                Button Button(string text)=>view.GetVisualDescendants().OfType<Button>().Single(b=>b.Content?.ToString()==text);
                void Click(string text)=>ControlAutomationPeer.CreatePeerForElement(Button(text))!.GetProvider<IInvokeProvider>()!.Invoke();
                async Task Run(string name, SimulationRequest request)
                {
                    vm.Reset(); vm.Setup.LoadConfiguration(request); vm.WorkSupply="Always available";
                    Click("Start"); vm.Pause(); vm.RollingWindow=20; vm.TargetDay="100"; await vm.RunToDayAsync();
                    await Task.Delay(100); scroll.Offset=default; await Task.Delay(100);
                    foreach(var row in vm.Flow.Where(r=>r.Queue is not null || r.SpecialistQueue is not null))
                        Console.WriteLine($"{name}: {row.Name}: {row.Queue ?? row.SpecialistQueue}");
                    var chart=view.GetVisualDescendants().OfType<LivePerformanceTrendChart>().Single();
                    Console.WriteLine($"{name}: chart Y={chart.TranslatePoint(default,window)!.Value.Y}, height={chart.Bounds.Height}");
                    Check(chart.TranslatePoint(default,window)!.Value.Y<=559 && chart.Bounds.Height==210,"No additional vertical space");
                    Save(window,name);
                }
                await Run("baseline",Scenarios.BaselineRequest);
                Check(vm.Flow.Where(r=>r.Queue is not null).Count(r=>r.Queue!.State==QueueAttentionState.Neutral)>=3,"Baseline mostly quiet");
                await Run("testing",Scenarios.BaselineRequest with {TesterCount=1,TestingEffort=10});
                Check(vm.Flow.Single(r=>r.State==WorkItemStatus.WaitingForTesting).QueueStrong,"Testing buildup");
                vm.BeginChange(); vm.Draft.NumberOfTesters="30"; vm.Draft.TestingWipLimit="30"; vm.ApplyChanges();
                vm.TargetDay="120"; await vm.RunToDayAsync(); await Task.Delay(100); Save(window,"recovery");
                var recovering=vm.Flow.Single(r=>r.State==WorkItemStatus.WaitingForTesting).Queue!;
                Console.WriteLine("Recovery: "+recovering);
                Check(recovering.Arrow=="↓","Recovery decreasing");
                await Run("release",Scenarios.BaselineRequest with {Release=new(ReleaseMode.Scheduled,1,5)});
                Check(vm.Flow.Single(r=>r.State==WorkItemStatus.ReadyForRelease).QueueStrong,"Release buildup");
                Check(vm.Performance!.CompletionRate>vm.Performance.Throughput,"Completion exceeds release");
                window.Width=960; await Task.Delay(100); Save(window,"release-960");
                var releaseButton=view.GetVisualDescendants().OfType<ToggleButton>().First(b=>b.DataContext is FlowStateRow r && r.State==WorkItemStatus.ReadyForRelease);
                releaseButton.Focus(Avalonia.Input.NavigationMethod.Tab); await Task.Delay(100); Save(window,"release-focus");
                RequestedThemeVariant=ThemeVariant.Dark; await Task.Delay(100); Save(window,"release-dark");
                RequestedThemeVariant=ThemeVariant.Light; window.Width=1280;
                await Run("specialists",Scenarios.BaselineRequest with {Skills=new(1,1)});
                Check(vm.Flow.Single(r=>r.IsDevelopment).SpecialistQueue!.State!=QueueAttentionState.Neutral,"Specialist waiting visible");
                window.Width=960; await Task.Delay(100); Save(window,"specialists-960");
                window.Width=1280;
                await Run("attention",Scenarios.BaselineRequest with {TesterCount=1,TestingEffort=10,TestingWipLimit=30});
                Check(vm.Flow.Single(r=>r.State==WorkItemStatus.WaitingForTesting).QueueAttention,"Amber attention level");
                await Run("multiple",Scenarios.BaselineRequest with {TesterCount=1,TestingEffort=10,Release=new(Capacity:0)});
                Check(vm.Flow.Count(r=>r.QueueStrong)>=2,"Multiple queues independently highlighted");
                Console.WriteLine("PASS baseline, testing, release, specialists, recovery, multiple queues and chart density.");
                vm.Dispose(); desktop.Shutdown(0);
            } catch(Exception e){Console.Error.WriteLine(e);vm.Dispose();desktop.Shutdown(1);}
        };
        base.OnFrameworkInitializationCompleted();
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Save(Window window,string name){Directory.CreateDirectory("/tmp/flowsim-queue");window.UpdateLayout();using var image=new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width,(int)window.Bounds.Height));image.Render(window);image.Save("/tmp/flowsim-queue/"+name+".png");}
}
