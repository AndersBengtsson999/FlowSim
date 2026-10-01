using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
        LiveFirstApp.OutputDirectory = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(Path.GetTempPath(), "flowsim-live-first");
        Directory.CreateDirectory(LiveFirstApp.OutputDirectory);
        return AppBuilder.Configure<LiveFirstApp>().UsePlatformDetect().WithInterFont().StartWithClassicDesktopLifetime([]);
    }
}

public sealed class LiveFirstApp : Avalonia.Application
{
    public static string OutputDirectory { get; set; } = "";
    public override void Initialize() { Styles.Add(new FluentTheme()); RequestedThemeVariant = ThemeVariant.Light; }
    public override void OnFrameworkInitializationCompleted()
    {
        var desktop = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
        var model = new MainWindowViewModel();
        var window = new MainWindow { DataContext = model, Width = 1240, Height = 1000, Title = "FlowSim — Live-first verification" };
        desktop.MainWindow = window;
        window.Opened += async (_, _) =>
        {
            try
            {
                await Task.Delay(300);
                bool Visible(Control control) => control.IsVisible && control.GetVisualAncestors().OfType<Control>().All(c => c.IsVisible);
                Button Named(string name) => window.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == name);
                void Click(Button button) { Check(button.Command!.CanExecute(button.CommandParameter), "Enabled command " + button.Content); button.Command.Execute(button.CommandParameter); }
                var simple = model.Simple; var vm = simple.Live;
                Check(simple.LiveVisible && !simple.HomeVisible && !simple.RunVisible, "Startup is Live");
                var navigation = window.GetVisualDescendants().OfType<Button>().Where(b => AutomationProperties.GetName(b)?.StartsWith("Navigate ") == true).ToArray();
                Check(navigation.Select(b => b.Content!.ToString()).Order().SequenceEqual(new[] { "Advanced", "Analyze", "Live" }), "Primary navigation");
                Check(!window.GetVisualDescendants().OfType<Button>().Any(b => Visible(b) && b.Content?.ToString() is "Home" or "Run" or "Change & Compare" or "Explore"), "No old primary entries");
                Save(window, "startup-live");
                var developers = window.GetVisualDescendants().OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == "Live Developers");
                developers.Text = "5"; await Task.Delay(50); Check(vm.Setup.NumberOfDevelopers == "5", "Team edit");
                Check(window.GetVisualDescendants().OfType<Expander>().Any(e => Visible(e) && e.Header?.ToString() == "Work · effort per item"), "Work settings reachable");
                Check(window.GetVisualDescendants().OfType<Expander>().Any(e => Visible(e) && e.Header?.ToString() == "Quality · defects and rework"), "Quality settings reachable");
                Click(window.GetVisualDescendants().OfType<Button>().Single(b => Visible(b) && b.Content?.ToString() == "Start"));
                Click(window.GetVisualDescendants().OfType<Button>().Single(b => Visible(b) && b.Content?.ToString() == "Pause"));
                var advanceButton = window.GetVisualDescendants().OfType<Button>().Single(e => e.Content?.ToString() == "Advance…");
                advanceButton.Flyout!.ShowAt(advanceButton);
                var advancePanel = (Control)((Flyout)advanceButton.Flyout).Content!; await Task.Delay(100);
                var target = advancePanel.GetVisualDescendants().OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == "Run to Day target");
                var advanceCommand = advancePanel.GetVisualDescendants().OfType<Button>().Single();
                target.Text = "100"; await Task.Delay(50); Click(advanceCommand);
                while (vm.IsFastAdvancing) await Task.Delay(20);
                Check(vm.Day == 100 && !vm.IsRunning, "Run to Day 100");
                await Task.Delay(100); Save(window, "run-to-day");
                vm.CheckpointLabel = "Before change"; vm.CheckpointCommand.Execute(null);
                vm.BeginChange(); vm.Draft.NumberOfTesters = "3"; vm.ChangeLabel = "Add tester"; vm.ApplyChanges();
                target.Text = "140"; await Task.Delay(50); Click(advanceCommand);
                while (vm.IsFastAdvancing) await Task.Delay(20);
                Check(vm.Day == 140 && vm.SelectedIntervention!.Day == 100, "Continue with intervention");
                advanceButton.Flyout.Hide(); await Task.Delay(100);
                var chart = window.GetVisualDescendants().OfType<Simulation.UI.Controls.LivePerformanceTrendChart>().Single();
                chart.BringIntoView(); await Task.Delay(100); Save(window, "live-trend");
                Check(chart.Series!.Points[^1].Day == 140, "Current trend");
                var comparison = window.GetVisualDescendants().OfType<Expander>().Single(e => e.Header?.ToString() == "Before & After an intervention");
                comparison.IsExpanded = true; await Task.Delay(100); comparison.BringIntoView(); await Task.Delay(100); Save(window, "live-before-after");
                Check(vm.BeforePeriod.Contains("81–100") && vm.AfterPeriod.Contains("101–120"), "Unchanged Before/After");
                var state = Simulation.Infrastructure.LiveSessionJson.Save(vm.Live!);
                Click(Named("Navigate Analyze")); await Task.Delay(100); Check(simple.ChangesVisible, "Compare under Analyze");
                simple.Try.NumberOfTesters = "3"; await simple.RunComparisonAsync();
                Check(simple.Pair.Comparison is not null && simple.ComparisonVisible, "Existing comparison executes");
                Save(window, "analyze-compare");
                Click(Named("Analyze Explore")); await Task.Delay(100);
                window.GetVisualDescendants().OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == "Analyze parameter values").Text = "1, 2, 3";
                await Task.Delay(100); await simple.ExploreAsync();
                Check(simple.Explore.Result is not null, "Existing exploration executes");
                await Task.Delay(100); Save(window, "analyze-explore");
                Click(Named("Analyze Experiments")); await Task.Delay(100); Check(simple.ExperimentsVisible, "Experiments rehosted");
                model.Compare.DuplicateCommand.Execute(null); await model.Compare.RunAsync(true);
                Check(model.Compare.Comparison is not null, "Scenario manager still executes");
                model.Compare.EditCommand.Execute(null); await Task.Delay(100);
                Check(simple.AdvancedVisible && model.Compare.IsEditing, "Expert scenario editor reachable");
                model.Compare.DiscardDraft(); Check(simple.ExperimentsVisible, "Editor returns to Analyze");
                Click(Named("Navigate Live")); await Task.Delay(100);
                Check(state == Simulation.Infrastructure.LiveSessionJson.Save(vm.Live!), "Live state preserved across analysis");
                Click(Named("Navigate Advanced")); await Task.Delay(100); Save(window, "advanced");
                Check(window.GetVisualDescendants().OfType<TextBlock>().Any(t => Visible(t) && t.Text == "Advanced Tools"), "Expert tools reachable");
                Click(Named("Navigate Live"));
                Check(state == Simulation.Infrastructure.LiveSessionJson.Save(vm.Live!), "Live state preserved across Advanced");
                Check(Simulation.Infrastructure.LiveSessionJson.Save(Simulation.Infrastructure.LiveSessionJson.Load(state)) == state, "Saved Live document round trip");
                Console.WriteLine("PASS: Live startup; exactly Live/Analyze/Advanced; common settings; Start/Pause; Run to Day 100/140; intervention, Flow/Trend/BeforeAfter; Compare, Explore, Experiments; scenario editing; Advanced; exact Live state and persistence preserved.");
                vm.Dispose();
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
