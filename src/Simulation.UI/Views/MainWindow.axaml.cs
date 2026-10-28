using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Simulation.UI.ViewModels;
namespace Simulation.UI.Views;

public partial class MainWindow : Window
{
    public RelayCommand ExitCommand { get; }
    public AsyncCommand AboutCommand { get; }
    public KeyGesture ExitGesture { get; } = OperatingSystem.IsMacOS() ? new(Key.Q, KeyModifiers.Meta) : new(Key.F4, KeyModifiers.Alt);
    public MainWindow()
    {
        ExitCommand = new(RequestExit);
        AboutCommand = new(() => ProductDialogs.ShowAbout(this), () => true);
        InitializeComponent();
        if (OperatingSystem.IsMacOS())
        {
            KeyBindings.Add(new KeyBinding { Gesture=ExitGesture, Command=ExitCommand });
            var menu = new NativeMenu();
            var appMenu = new NativeMenu();
            appMenu.Items.Add(new NativeMenuItem("About FLOW") { Command=AboutCommand });
            appMenu.Items.Add(new NativeMenuItem("Exit Application") { Command=ExitCommand, Gesture=ExitGesture });
            menu.Items.Add(new NativeMenuItem("FLOW") { Menu=appMenu });
            NativeMenu.SetMenu(this,menu);
        }
    }
    private void RequestExit()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.TryShutdown();
        else Close();
    }
    private void OpenGuide(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Simulation.UI.Help.GuideService.Open(this);
    private void ToggleNavigation(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Sidebar.IsVisible = !Sidebar.IsVisible;
}
