using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
namespace Simulation.UI.Views;
public partial class KpiDashboardView : UserControl
{
    public static readonly StyledProperty<Orientation> LayoutOrientationProperty = AvaloniaProperty.Register<KpiDashboardView,Orientation>(nameof(LayoutOrientation),Orientation.Horizontal);
    public Orientation LayoutOrientation { get=>GetValue(LayoutOrientationProperty);set=>SetValue(LayoutOrientationProperty,value); }
    public KpiDashboardView()=>InitializeComponent();
    private void OpenExplorerClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    { if(sender is Control { DataContext: Simulation.UI.ViewModels.KpiCard card } && DataContext is Simulation.UI.ViewModels.LiveViewModel vm) vm.OpenExplorer(card.Observation.Definition.Id); }
    private void CloseExplorerClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) { if(DataContext is Simulation.UI.ViewModels.LiveViewModel vm) vm.CloseExplorer(); }
    private void RemoveComparisonClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) { if(DataContext is Simulation.UI.ViewModels.LiveViewModel vm) vm.ClearComparison(); }
}
