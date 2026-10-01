using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Simulation.Application;
using Simulation.Core;
using Simulation.UI.ViewModels;
using Simulation.UI.Views;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        VerificationApp.OutputDirectory = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(Path.GetTempPath(), "flowsim-availability");
        Directory.CreateDirectory(VerificationApp.OutputDirectory);
        return AppBuilder.Configure<VerificationApp>().UsePlatformDetect().WithInterFont().StartWithClassicDesktopLifetime([]);
    }
}
public sealed class VerificationApp : Avalonia.Application
{
    public static string OutputDirectory { get; set; } = "";
    public override void Initialize() { Styles.Add(new FluentTheme()); RequestedThemeVariant = ThemeVariant.Light; }
    public override void OnFrameworkInitializationCompleted()
    {
        var desktop = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
        var model = new MainWindowViewModel(); var vm = model.Simple.Live;
        var window = new MainWindow { DataContext = model, Width = 1240, Height = 1000, Title = "FlowSim — Capacity, Work Supply and Status verification" };
        desktop.MainWindow = window;
        window.Opened += async (_, _) => {
            try {
                await Task.Delay(200);
                TextBox Field(string name) => window.GetVisualDescendants().OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == name);
                var supply = window.GetVisualDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == "Work supply");
                Check(vm.Setup.DeveloperAvailability == "100" && vm.Setup.TesterAvailability == "100", "Full availability defaults");
                supply.SelectedItem = "Always available"; await Task.Delay(50); Check(!vm.ShowArrivalRate, "Rate hidden");
                Field("Developer Availability (%)").Text = "85"; Field("Tester Availability (%)").Text = "75";
                await Task.Delay(50); Check(vm.Setup.DeveloperAvailability == "85" && vm.Setup.TesterAvailability == "75", "Availability fields bound");
                Save(window, "setup");
                async Task Run(string name, string work, string developers, string testers, int wip, int target = 200) {
                    vm.Reset(); vm.WorkSupply = work; vm.Setup.DeveloperAvailability = developers; vm.Setup.TesterAvailability = testers;
                    vm.Setup.NumberOfDevelopers = "5"; vm.Setup.NumberOfTesters = "2"; vm.Setup.DevelopmentWipLimit = wip.ToString();
                    vm.Start(); vm.Pause(); vm.TargetDay = target.ToString(); await vm.RunToDayAsync();
                    Check(vm.Day == target, name + " reached day");
                    var d = vm.Live!.CurrentSnapshot;
                    Check(d.AvailableDeveloperCapacity == 5 * double.Parse(developers) / 100, name + " dev capacity");
                    Check(d.AvailableTesterCapacity == 2 * double.Parse(testers) / 100, name + " test capacity");
                    Check(vm.Live.Session.Days.All(x => x.DevelopmentWip <= wip && x.ReviewWip <= 3 && x.TestingWip <= 3), name + " WIP limits");
                    if (work == "Always available") Check(vm.Live.Session.Days.All(x => x.BacklogCount == 0), name + " lazy supply");
                    await Task.Delay(100);
                    Check(window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == vm.StatusCapacity), "Rendered capacity binding");
                    Console.WriteLine(name + ": " + vm.StatusDelivery + " | " + vm.StatusCapacity + " | " + vm.StatusQueues);
                    Save(window, name);
                }
                await Run("A-fixed-rate", "Fixed rate", "100", "100", 5);
                await Run("B-always", "Always available", "100", "100", 5);
                await Run("C-dev80", "Always available", "80", "100", 5);
                await Run("C-dev85", "Always available", "85", "100", 5);
                await Run("D-test75", "Always available", "100", "75", 5);
                foreach (var wip in new[] {1, 3, 5, 10}) await Run("E-wip" + wip, "Always available", "100", "100", wip);
                await Run("F-before", "Always available", "100", "100", 5, 100);
                var prior = JsonSerializer.Serialize(vm.Live!.Session.Days);
                vm.BeginChange(); vm.Draft.DeveloperAvailability = "80"; vm.ChangeLabel = "Availability change"; vm.ApplyChanges();
                Check(vm.SelectedIntervention!.Day == 100 && vm.LiveStatus!.DeveloperAvailable == 5, "Intervention day keeps historic capacity");
                vm.TargetDay = "140"; await vm.RunToDayAsync();
                Check(JsonSerializer.Serialize(vm.Live.Session.Days.Take(100).ToArray()) == prior, "History unchanged");
                Check(vm.Live.Session.Days[100].AvailableDeveloperCapacity == 4, "Effect Day101");
                Check(vm.LatestIntervention.Contains("Day 100") && vm.LatestIntervention.Contains("effective Day 101"), "Latest change text");
                Save(window, "F-after");
                vm.TrendRange = "Full Session"; vm.TrendMetric = vm.TrendMetrics.Single(m => m.Metric == LiveTrendMetric.AvailableDevelopers);
                var chart = window.GetVisualDescendants().OfType<Simulation.UI.Controls.LivePerformanceTrendChart>().Single();
                chart.BringIntoView(); await Task.Delay(100); Save(window, "F-trend");
                Check(chart.Series!.Interventions.Single().Day == 100, "Marker Day100");
                Check(chart.Series.Points[99].Value == 5 && chart.Series.Points[100].Value == 4, "Capacity series exact day");
                var comparison = window.GetVisualDescendants().OfType<Expander>().Single(e => e.Header?.ToString() == "Before & After an intervention");
                comparison.IsExpanded = true; await Task.Delay(100); comparison.BringIntoView(); await Task.Delay(100); Save(window, "F-before-after");
                Check(vm.BeforePeriod.Contains("81–100") && vm.AfterPeriod.Contains("101–120"), "Before/After intact");
                comparison.IsExpanded = false;
                vm.TargetDay = "1000"; await vm.RunToDayAsync();
                window.GetVisualDescendants().OfType<Border>().Single(t => AutomationProperties.GetName(t) == "Live Status").BringIntoView(); await Task.Delay(100);
                Save(window, "G-long-session");
                Check(vm.Day == 1000 && vm.Live.CurrentSnapshot.BacklogCount == 0, "Long run no fictitious backlog");
                Console.WriteLine("G-long: " + vm.StatusDelivery + " | " + vm.StatusCapacity + " | " + vm.StatusQueues);
                var state = JsonSerializer.Serialize(vm.Live.Capture());
                model.Simple.OpenAnalyzeCommand.Execute(null); model.Simple.OpenLiveCommand.Execute(null);
                Check(state == JsonSerializer.Serialize(vm.Live.Capture()), "Navigation preserves state");
                Console.WriteLine("PASS: A–G actual Avalonia window, bound fields, status, WIP, DayN+1 capacity, markers, Before/After and navigation.");
                vm.Dispose(); desktop.Shutdown(0);
            } catch (Exception e) { Console.Error.WriteLine(e); vm.Dispose(); desktop.Shutdown(1); }
        };
        base.OnFrameworkInitializationCompleted();
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Save(Window window, string name) {
        window.UpdateLayout(); using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height));
        bitmap.Render(window); bitmap.Save(Path.Combine(OutputDirectory, name + ".png"));
    }
}
