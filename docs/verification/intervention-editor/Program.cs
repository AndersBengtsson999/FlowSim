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
using Simulation.UI.Views;
using Simulation.UI.ViewModels;

internal static class Program
{
    [STAThread] public static int Main(string[] args) => AppBuilder.Configure<VerificationApp>().UsePlatformDetect().WithInterFont().StartWithClassicDesktopLifetime([]);
}
public sealed class VerificationApp : Avalonia.Application
{
    public override void Initialize() { Styles.Add(new FluentTheme()); RequestedThemeVariant = ThemeVariant.Light; }
    public override void OnFrameworkInitializationCompleted()
    {
        var desktop = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
        var main = new MainWindowViewModel(); var vm = main.Simple.Live;
        var window = new MainWindow { DataContext = main, Width = 1280, Height = 900 };
        desktop.MainWindow = window;
        window.Opened += async (_, _) =>
        {
            try
            {
                await Task.Delay(250);
                var view = window.GetVisualDescendants().OfType<LiveView>().Single();
                var scroll = view.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "LiveScroll");
                Invoke("Start"); Invoke("Pause"); Invoke("Change"); await Task.Delay(150);
                var panel = view.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("card") && b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Paused · Change something"));
                panel.BringIntoView(); await Task.Delay(150);
                Check(panel.Bounds.Width <= 600, "Editor max width");
                var current = panel.GetVisualDescendants().OfType<TextBlock>().Single(t => AutomationProperties.GetName(t) == "Current Development Productivity (x)");
                Check(current.Text!.EndsWith(" x"), "Read-only Current with unit");
                var input = Input("Development Productivity (x)");
                input.SetCurrentValue(TextBox.TextProperty, "1.00");
                Check(!vm.ChangeFields.Single(f => f.Label == "Development Productivity (x)").IsChanged, "Equivalent numeric formatting stays quiet");
                input.SetCurrentValue(TextBox.TextProperty, "1.5");
                Input("Developer Availability (%)").SetCurrentValue(TextBox.TextProperty, "80");
                Input("Shortcut Rate (%)").SetCurrentValue(TextBox.TextProperty, "40");
                input.Focus(); await Task.Delay(150);
                var row = input.GetVisualAncestors().OfType<Border>().First(b => b.Classes.Contains("interventionRow"));
                Check(row.IsKeyboardFocusWithin && row.Background != null, "Whole focused row highlighted");
                Check(row.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Changed" && t.IsVisible), "Accessible changed text");
                Check(vm.ChangeFields.Count(f => f.IsChanged) == 3, "Three simultaneous changes");
                Save(window, "desktop-focused");
                input.SetCurrentValue(TextBox.TextProperty, "1.0");
                Check(!vm.ChangeFields.Single(f => f.Label == "Development Productivity (x)").IsChanged, "Changed cleared on return");
                foreach (var width in new[] { 960d, 720d })
                {
                    window.MinWidth = 600; window.Width = width; window.Height = 800;
                    await Task.Delay(150); panel.BringIntoView(); input.Focus(); await Task.Delay(100);
                    Check(scroll.Extent.Width <= scroll.Viewport.Width + 1, "No horizontal overflow at " + width);
                    Check(input.TranslatePoint(default, panel)!.Value.X + input.Bounds.Width <= panel.Bounds.Width, "Try stays in panel");
                    Console.WriteLine($"Width {width}: panel {panel.Bounds.Width}, label/current/try gap bounded, focus and changed labels visible.");
                    Save(window, "width-" + width);
                }
                var advanced = panel.GetVisualDescendants().OfType<Expander>().Single(e => e.Header?.ToString() == "Advanced · Technical Debt");
                advanced.IsExpanded = true; await Task.Delay(150);
                var factor = Input("Debt Creation Factor (x)"); factor.SetCurrentValue(TextBox.TextProperty, "2.0"); factor.BringIntoView(); factor.Focus(); await Task.Delay(150);
                Check(vm.AdvancedDebtChanges.Single().IsChanged, "Advanced factor bound and changed");
                Save(window, "advanced-factor");
                RequestedThemeVariant = ThemeVariant.Dark; await Task.Delay(200);
                factor = Input("Debt Creation Factor (x)"); factor.Focus(); await Task.Delay(100);
                var factorRow = factor.GetVisualAncestors().OfType<Border>().First(b => b.Classes.Contains("interventionRow"));
                Check(factorRow.IsKeyboardFocusWithin && factorRow.Background != null, "Theme-aware focus brush");
                Save(window, "dark-focus");
                Invoke("Apply Changes"); await Task.Delay(100);
                Check(!vm.IsEditing && vm.Live!.Session.Configuration.Debt.CreationFactor == 2, "Native Apply preserves factor");
                Check(vm.Live!.Session.Configuration.Team.DeveloperAvailability == .8, "Availability binding applied");
                Check(vm.Live.Session.Configuration.Debt.ShortcutRate == .4, "Shortcut binding applied");
                Console.WriteLine("PASS: native Current/Try grouping, units, focus, changed/reset, multiple changes, Advanced factor, Apply, desktop/narrow layouts and theme-aware row highlight.");
                vm.Dispose(); desktop.Shutdown(0);
                TextBox Input(string label) => panel.GetVisualDescendants().OfType<TextBox>().Single(t => t.IsEffectivelyVisible && AutomationProperties.GetName(t) == label);
                void Invoke(string content)
                {
                    var button = view.GetVisualDescendants().OfType<Button>().Single(b => b.Content?.ToString() == content);
                    ControlAutomationPeer.CreatePeerForElement(button)!.GetProvider<IInvokeProvider>()!.Invoke();
                }
            }
            catch (Exception e) { Console.Error.WriteLine(e); vm.Dispose(); desktop.Shutdown(1); }
        };
        base.OnFrameworkInitializationCompleted();
    }
    private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    private static void Save(Window window, string name)
    {
        Directory.CreateDirectory("/tmp/flowsim-intervention-editor"); window.UpdateLayout();
        using var image = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height));
        image.Render(window); image.Save("/tmp/flowsim-intervention-editor/" + name + ".png");
    }
}
