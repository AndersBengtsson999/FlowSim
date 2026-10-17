using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
namespace Simulation.UI.Views;
public partial class KpiDashboardView : UserControl
{
    public static readonly StyledProperty<Orientation> LayoutOrientationProperty = AvaloniaProperty.Register<KpiDashboardView,Orientation>(nameof(LayoutOrientation),Orientation.Horizontal);
    public Orientation LayoutOrientation { get=>GetValue(LayoutOrientationProperty);set=>SetValue(LayoutOrientationProperty,value); }
    public KpiDashboardView()=>InitializeComponent();
}
