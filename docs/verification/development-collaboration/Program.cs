using Avalonia;
using Avalonia.Automation;
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
    [STAThread]
    public static int Main(string[] args)
    {
        CollaborationApp.OutputDirectory = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(Path.GetTempPath(), "flowsim-development-collaboration");
        Directory.CreateDirectory(CollaborationApp.OutputDirectory);
        return AppBuilder.Configure<CollaborationApp>().UsePlatformDetect().WithInterFont().StartWithClassicDesktopLifetime([]);
    }
}

public sealed class CollaborationApp : Avalonia.Application
{
    public static string OutputDirectory { get; set; } = "";
    public override void Initialize() { Styles.Add(new FluentTheme()); RequestedThemeVariant = ThemeVariant.Light; }
    public override void OnFrameworkInitializationCompleted()
    {
        var desktop = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
        var model = new MainWindowViewModel();
        var window = new MainWindow { DataContext = model, Width = 1240, Height = 1000, Title = "FlowSim — Collaboration verification" };
        desktop.MainWindow = window;
        window.Opened += async (_, _) =>
        {
            try
            {
                foreach (var (wip, used, effective) in new[] { (1, 2d, 1.5), (2, 4d, 3d), (3, 5d, 4d), (5, 5d, 5d), (10, 5d, 5d) })
                {
                    model = new MainWindowViewModel(); window.DataContext = model;
                    model.Simple.OpenLiveCommand.Execute(null);
                    var vm = model.Simple.Live;
                    vm.Setup.LoadConfiguration(new Simulation.Application.SimulationRequest {
                        DeveloperCount = 5, TesterCount = 10, DevelopmentWipLimit = wip,
                        CodeReviewWipLimit = 10, TestingWipLimit = 10, NumberOfWorkItems = 200,
                        DevelopmentEffort = 10, CodeReviewEffort = .2, TestingEffort = .2, SimulationDays = 100 });
                    vm.ArrivalMode = "Fixed Backlog";
                    vm.StartCommand.Execute(null); vm.PauseCommand.Execute(null); vm.StepCommand.Execute(null);
                    await Task.Delay(300);
                    var day = vm.Live!.CurrentSnapshot;
                    Check(day.DevelopmentCount == wip && day.UsedDevelopmentCapacity == used && day.DevelopmentWork == effective, "Day 1 allocation");
                    var panel = window.GetVisualDescendants().OfType<Expander>().Single(e => e.Header?.ToString() == "Development allocations");
                    panel.IsExpanded = false;
                    foreach (var scroll in window.GetVisualDescendants().OfType<ScrollViewer>()) scroll.Offset = default;
                    await Task.Delay(150);
                    Save(window, "wip-" + wip + "-summary");
                    panel.IsExpanded = true; await Task.Delay(150); panel.BringIntoView(); await Task.Delay(150);
                    var texts = window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsVisible).Select(t => t.Text ?? "").ToArray();
                    Check(texts.Any(t => t.Contains($"Capacity used {used:0.##} · Effective work {effective:0.##}")), "Visible summary");
                    Check(texts.Any(t => t.Contains("Remaining") && t.Contains("Primary") && t.Contains("Collaboration")), "Visible item allocation");
                    Save(window, "wip-" + wip);
                    while (vm.Day < 100) vm.StepCommand.Execute(null);
                    var r = vm.Live.Session.GetResult();
                    Console.WriteLine($"WIP {wip}: day 1 active={day.DevelopmentCount}, consumed={used}, effective={effective}; 100 days utilization={r.DeveloperUtilization:P2}, completed={r.CompletedWorkItems}, throughput/5d={r.ThroughputPerFiveDays:0.###}, cycle={r.AverageCycleTime:0.###}, backlog={r.Days[^1].BacklogCount}, max review queue={r.Days.Max(d => d.WaitingForCodeReviewCount)}, max testing queue={r.Days.Max(d => d.WaitingForTestingCount)}");
                    vm.Dispose();
                }
                Console.WriteLine("PASS: five native Avalonia scenarios and visible bindings.");
                desktop.Shutdown(0);
            }
            catch (Exception e) { Console.Error.WriteLine(e); desktop.Shutdown(1); }
        };
        base.OnFrameworkInitializationCompleted();
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Save(Window window, string name)
    {
        window.UpdateLayout();
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height));
        bitmap.Render(window); bitmap.Save(Path.Combine(OutputDirectory, $"{name}.png"));
    }
}
