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
        BoundaryApp.OutputDirectory = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.Combine(Path.GetTempPath(), "flowsim-step13-boundaries");
        Directory.CreateDirectory(BoundaryApp.OutputDirectory);
        return AppBuilder.Configure<BoundaryApp>().UsePlatformDetect().WithInterFont().StartWithClassicDesktopLifetime([]);
    }
}

public sealed class BoundaryApp : Avalonia.Application
{
    public static string OutputDirectory { get; set; } = "";
    public override void Initialize() { Styles.Add(new FluentTheme()); RequestedThemeVariant = ThemeVariant.Light; }
    public override void OnFrameworkInitializationCompleted()
    {
        var desktop = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
        var model = new MainWindowViewModel();
        var window = new MainWindow { DataContext = model, Width = 1240, Height = 1000, Title = "FlowSim — Before/After verification" };
        desktop.MainWindow = window;
        window.Opened += async (_, _) =>
        {
            try
            {
                await Task.Delay(400);
                model.Simple.OpenLiveCommand.Execute(null);
                var vm = model.Simple.Live;
                vm.StartCommand.Execute(null); vm.PauseCommand.Execute(null);
                for (var i = 0; i < 100; i++) vm.StepCommand.Execute(null);
                vm.ChangeCommand.Execute(null);
                await Task.Delay(100);
                var testers = window.GetVisualDescendants().OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == "Testers");
                testers.Text = "3";
                await Task.Delay(100);
                Check(vm.Draft.NumberOfTesters == "3", "Native edit binding");
                vm.ChangeLabel = "Add tester at Day 100";
                vm.ApplyCommand.Execute(null);
                Check(vm.SelectedIntervention?.Day == 100, "Intervention day");
                await Task.Delay(100);
                var panel = window.GetVisualDescendants().OfType<Expander>().Single(e => e.Header?.ToString() == "Before & After an intervention");
                panel.IsExpanded = true;
                await Task.Delay(150);
                var selector = window.GetVisualDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == "Compare intervention");
                selector.SelectedItem = vm.Interventions.Single();
                foreach (var day in new[] { 100, 101, 110, 119, 120, 140 })
                {
                    while (vm.Day < day) vm.StepCommand.Execute(null);
                    await Task.Delay(150);
                    panel.BringIntoView(); await Task.Delay(150);
                    var expectedBefore = "Before: Days 81–100 · 20 of 20 days available";
                    var expectedAfter = $"After: Days 101–120 · {Math.Min(day - 100, 20)} of 20 days available";
                    var texts = panel.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsVisible).Select(t => t.Text).ToArray();
                    Check(texts.Contains(expectedBefore), "Visible Before at Day " + day);
                    Check(texts.Contains(expectedAfter), "Visible After at Day " + day);
                    Check(texts.Any(t => t?.StartsWith(day < 120 ? "Incomplete periods" : "Complete periods") == true), "Visible completeness status");
                    Console.WriteLine($"Day {day}: {expectedBefore} | {expectedAfter}");
                    Save(window, $"day-{day}");
                }
                var windows = window.GetVisualDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == "Performance window · days");
                foreach (var (size, first, last) in new[] { (10, 91, 110), (50, 51, 150), (100, 1, 200), (20, 81, 120) })
                {
                    windows.SelectedItem = size; await Task.Delay(150);
                    Check(vm.RollingWindow == size, "Native window selector binding");
                    var texts = panel.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsVisible).Select(t => t.Text).ToArray();
                    Check(texts.Contains($"Before: Days {first}–100 · {size} of {size} days available"), "Window Before");
                    Check(texts.Contains($"After: Days 101–{last} · {Math.Min(40, size)} of {size} days available"), "Window After");
                    Console.WriteLine($"Window {size}: Before {first}–100; After 101–{last}; available {Math.Min(40, size)}/{size}");
                    panel.BringIntoView(); await Task.Delay(150); Save(window, $"window-{size}");
                }
                vm.Dispose();
                Console.WriteLine("PASS: native Avalonia window, real bindings, all boundary/progress checks.");
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
