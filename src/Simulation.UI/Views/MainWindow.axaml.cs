using Avalonia.Controls;

namespace Simulation.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();
    private void ToggleNavigation(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Sidebar.IsVisible = !Sidebar.IsVisible;
    }
}
