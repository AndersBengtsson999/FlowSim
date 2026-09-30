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
        TrendApp.OutputDirectory = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(Path.GetTempPath(), "flowsim-trend");
        Directory.CreateDirectory(TrendApp.OutputDirectory);
        return AppBuilder.Configure<TrendApp>().UsePlatformDetect().WithInterFont().StartWithClassicDesktopLifetime([]);
    }
}

public sealed class TrendApp : Avalonia.Application
{
    public static string OutputDirectory { get; set; } = "";
    public override void Initialize() { Styles.Add(new FluentTheme()); RequestedThemeVariant = ThemeVariant.Light; }
    public override void OnFrameworkInitializationCompleted()
    {
        var desktop = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
        var model = new MainWindowViewModel();
        var window = new MainWindow { DataContext = model, Width = 1240, Height = 1000, Title = "FlowSim — Performance Trend verification" };
        desktop.MainWindow = window;
        window.Opened += async (_, _) =>
        {
            try
            {
                model.Simple.OpenLiveCommand.Execute(null);
                var vm = model.Simple.Live;
                vm.Setup.LoadConfiguration(new Simulation.Application.SimulationRequest { NumberOfWorkItems = 200, DeveloperCount = 5, TesterCount = 10,
                    DevelopmentWipLimit = 5, CodeReviewWipLimit = 10, TestingWipLimit = 10, DevelopmentEffort = 10, CodeReviewEffort = .2, TestingEffort = .2 });
                vm.ArrivalMode = "Fixed Backlog"; vm.Start(); vm.Pause();
                for (var i = 0; i < 100; i++) vm.Step();
                await Task.Delay(250);
                var metricSelector = window.GetVisualDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == "Trend metric");
                var rangeSelector = window.GetVisualDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == "Trend time range");
                rangeSelector.SelectedItem = "Full Session";
                await Task.Delay(100);
                var chart = window.GetVisualDescendants().OfType<Simulation.UI.Controls.LivePerformanceTrendChart>().Single();
                Check(vm.Trend.Points.Count == 100, "Native range selector");
                foreach (var metric in vm.TrendMetrics)
                {
                    metricSelector.SelectedItem = metric; await Task.Delay(60);
                    Check(vm.TrendMetric == metric && chart.Unit == metric.Unit, "Native metric binding");
                    Check(chart.Series!.Points.Count == 100, "Baseline points");
                    chart.BringIntoView(); await Task.Delay(60); Save(window, "baseline-" + metric.Metric);
                }
                var before = System.Text.Json.JsonSerializer.Serialize(vm.Live!.Session.Days);
                vm.CheckpointCommand.Execute(null); vm.SelectedCheckpoint = vm.Checkpoints.Single();
                vm.BeginChange(); vm.Draft.DevelopmentWipLimit = "2"; vm.ChangeLabel = "WIP 5 to 2"; vm.ApplyChanges();
                Check(vm.Trend.Interventions.Single().Day == 100 && chart.SelectedDay == 100, "Marker at recorded day");
                vm.Step(); Check(vm.Day == 101 && vm.Live.Session.Configuration.DevelopmentWipLimit == 2, "Effective day 101");
                while (vm.Day < 140) vm.Step();
                Check(before == System.Text.Json.JsonSerializer.Serialize(vm.Live.Session.Days.Take(100).ToArray()), "Prior history unchanged");
                foreach (var metric in vm.TrendMetrics)
                {
                    metricSelector.SelectedItem = metric; await Task.Delay(80);
                    Check(chart.Series!.Interventions.Single().Day == 100, "Persistent intervention marker");
                    chart.BringIntoView(); await Task.Delay(80); Save(window, "intervention-" + metric.Metric);
                }
                var day = vm.Live.CurrentSnapshot;
                Check(vm.Live.Session.Days.Skip(100).Any(d => d.UsedDevelopmentCapacity > d.DevelopmentWork), "Post-intervention collaboration");
                Console.WriteLine($"Day 140: capacity consumed={day.UsedDevelopmentCapacity}, effective work={day.DevelopmentWork}; marker Day 100, effect Day 101; earlier history unchanged.");
                foreach (var range in vm.TrendRanges)
                {
                    rangeSelector.SelectedItem = range; await Task.Delay(50);
                    Check(vm.TrendRange == range, "Range binding");
                }
                vm.RestoreCommand.Execute(null); Check(vm.Trend.Points.Count == 100 && vm.Trend.Interventions.Count == 0, "Checkpoint rebuild");
                vm.Reset(); Check(vm.Trend.Points.Count == 0, "Reset clears history");
                var longLive = Simulation.Application.LiveSimulation.Start(new() { NumberOfWorkItems = 20 }, Simulation.Core.WorkArrivalMode.FixedBacklog);
                for (var i = 0; i < 10000; i++) longLive.Step();
                var watch = System.Diagnostics.Stopwatch.StartNew();
                vm.Load(longLive); vm.TrendRange = "Full Session";
                watch.Stop(); Console.WriteLine($"10,000-day history loaded and projected in {watch.Elapsed.TotalMilliseconds:0.0} ms.");
                await Task.Delay(150);
                chart.BringIntoView(); await Task.Delay(100);
                watch.Restart(); Save(window, "full-session-10000"); watch.Stop();
                Check(chart.Series!.Points.Count == 10000, "Full authoritative history retained");
                Console.WriteLine($"Native window render and PNG save: {watch.Elapsed.TotalMilliseconds:0.0} ms.");
                vm.Dispose();
                Console.WriteLine("PASS: baseline metrics, WIP intervention, collaboration, range selectors, checkpoints, Reset and 10,000-day native chart.");
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
