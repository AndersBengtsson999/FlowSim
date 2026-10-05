using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using Simulation.UI.Controls;
using Simulation.UI.Views;
using Simulation.UI.ViewModels;

internal static class Program
{
    [STAThread] public static int Main(string[] args) => AppBuilder.Configure<LabelApp>().UsePlatformDetect().WithInterFont().StartWithClassicDesktopLifetime([]);
}
public sealed class LabelApp : Avalonia.Application
{
    public override void Initialize() { Styles.Add(new FluentTheme()); RequestedThemeVariant = ThemeVariant.Light; }
    public override void OnFrameworkInitializationCompleted()
    {
        var desktop = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
        var main = new MainWindowViewModel(); var vm = main.Simple.Live;
        vm.Setup.ShortcutRate = "50";
        var window = new MainWindow { DataContext = main, Width = 1100, Height = 850 }; desktop.MainWindow = window;
        window.Opened += async (_, _) =>
        {
            try
            {
                await Task.Delay(250);
                var view = window.GetVisualDescendants().OfType<LiveView>().Single();
                var scroll = view.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "LiveScroll");
                Invoke("Start"); Invoke("Pause"); vm.TargetDay = "100"; await vm.RunToDayAsync();
                Invoke("Change"); await Task.Delay(150);
                Input("Shortcut Rate (%)").SetCurrentValue(TextBox.TextProperty, "0");
                Input("Change label").SetCurrentValue(TextBox.TextProperty, "  Stop shortcuts  ");
                Invoke("Apply Changes"); await Task.Delay(150);
                Check(!vm.IsEditing && vm.Live!.Session.Changes.Single().Label == "Stop shortcuts", "UI label captured and trimmed");
                scroll.Offset = default; await Task.Delay(100);
                Check(view.GetVisualDescendants().OfType<TextBlock>().Any(t => t.IsEffectivelyVisible && t.Text == vm.LatestIntervention && t.Text.Contains("Stop shortcuts\nShortcut Rate")), "Latest status renders label and facts");
                Save(window, "latest-label");
                vm.TargetDay = "120"; await vm.RunToDayAsync(); vm.TrendRange = "Full Session";
                var chart = view.GetVisualDescendants().OfType<LivePerformanceTrendChart>().Single();
                chart.BringIntoView(); await Task.Delay(150);
                // Raise a real Avalonia pointer-moved event at the Day 100 marker.
                var markerX = 55 + (100d - 1) * (chart.Bounds.Width - 70) / (120 - 1);
                var point = chart.TranslatePoint(new Point(markerX, 70), window)!.Value;
                chart.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, chart, new Pointer(1, PointerType.Mouse, true), window, point, 0, new PointerPointProperties(), KeyModifiers.None));
                var tip = ToolTip.GetTip(chart)?.ToString() ?? "";
                Check(tip.Contains("Day 100\nStop shortcuts\nShortcut Rate") && tip.Contains("Effective Day 101"), "Marker hover includes day, label, facts and effective day");
                Console.WriteLine("Marker tooltip: " + tip); Save(window, "trend-marker");
                var comparison = view.GetVisualDescendants().OfType<Expander>().Single(e => e.Header?.ToString() == "Before & After an intervention");
                comparison.IsExpanded = true; await Task.Delay(150); comparison.BringIntoView(); await Task.Delay(100);
                Check(view.GetVisualDescendants().OfType<TextBlock>().Any(t => t.IsEffectivelyVisible && t.Text == InterventionPresentation.Selection(vm.SelectedIntervention!)), "Before/After selected label and actual changes rendered");
                Save(window, "before-after-label"); comparison.IsExpanded = false;
                Invoke("Change"); await Task.Delay(100); Input("Debt Repayment (%)").SetCurrentValue(TextBox.TextProperty, "25"); Invoke("Apply Changes"); await Task.Delay(100);
                Check(vm.Live!.Session.Changes.Last().Label == null && !vm.LatestIntervention.Contains('\n'), "Unlabeled change clean single-line status");
                scroll.Offset = default; Save(window, "latest-unlabeled");
                for (var i = 0; i < 5; i++)
                {
                    Invoke("Change"); await Task.Delay(100);
                    Input("Shortcut Rate (%)").SetCurrentValue(TextBox.TextProperty, (10 + i * 10).ToString());
                    var label = "Repeated change " + (i + 1);
                    var labelInput = Input("Change label"); labelInput.Focus(); labelInput.SelectAll();
                    labelInput.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = label });
                    Check(vm.ChangeLabel == label, "Repeated input captured " + i);
                    Invoke("Apply Changes"); await Task.Delay(100);
                    Check(vm.Live!.Session.Changes.Last().Label == label, "Repeated label stored " + i);
                    Check(view.GetVisualDescendants().OfType<TextBlock>().Any(t => t.IsEffectivelyVisible && t.Text == vm.LatestIntervention && t.Text.Contains(label)), "Repeated label rendered " + i);
                    Invoke("Resume"); await Task.Delay(50); Invoke("Pause");
                    Check(vm.LatestIntervention.Contains(label), "Repeated label retained after resume " + i);
                }
                scroll.Offset = default; Save(window, "repeated-labels");
                Console.WriteLine("PASS repeated edits: five new labels captured, stored and rendered after labeled/unlabeled edits and resume/pause.");
                Console.WriteLine("PASS: native input → stored trimmed label → latest status, actual marker hover, Before/After selection; unlabeled change remains clean.");
                vm.Dispose(); desktop.Shutdown(0);
                TextBox Input(string label) => view.GetVisualDescendants().OfType<TextBox>().Single(t => t.IsEffectivelyVisible && AutomationProperties.GetName(t) == label);
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
        Directory.CreateDirectory("/tmp/flowsim-intervention-label"); window.UpdateLayout();
        using var image = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height));
        image.Render(window); image.Save("/tmp/flowsim-intervention-label/" + name + ".png");
    }
}
