using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Controls.Primitives;
using Simulation.UI.Controls.GameWorld;
namespace Simulation.UI.Views;
public partial class GameView : UserControl
{
    private bool? sidePanel;
    public GameView()
    {
        InitializeComponent();
        LayoutUpdated += (_, _) => FitWorkshop();
    }
    private void CloseStationClicked(object? sender, RoutedEventArgs e)
    {
        GameWorld.SelectedStation = null; GameWorld.Focus(Avalonia.Input.NavigationMethod.Pointer);
    }
    private void ZoomInClicked(object? sender, RoutedEventArgs e) => GameWorld.ZoomBy(1.2);
    private void ZoomOutClicked(object? sender, RoutedEventArgs e) => GameWorld.ZoomBy(1 / 1.2);
    private void FitClicked(object? sender, RoutedEventArgs e) => GameWorld.FitCamera();
    private void FitWorkshop()
    {
        if (!IsEffectivelyVisible || TopLevel.GetTopLevel(this) is not { } root || this.TranslatePoint(default, root) is not { } position) return;
        var dashboardSide = Bounds.Width >= 1100;
        var explorer = DataContext is Simulation.UI.ViewModels.LiveViewModel { ExplorerOpen: true };
        var dashboardWidth = dashboardSide && KpiPanel.IsVisible ? (explorer ? 400 : 260) : 0;
        if (DashboardLayout.ColumnDefinitions.Count != 2) DashboardLayout.ColumnDefinitions = new("*,0");
        if (DashboardLayout.ColumnDefinitions[1].Width.Value != dashboardWidth) DashboardLayout.ColumnDefinitions[1].Width = new GridLength(dashboardWidth);
        Grid.SetColumn(KpiPanel, dashboardSide ? 1 : 0); Grid.SetRow(KpiPanel, dashboardSide ? 0 : 1);
        KpiPanel.LayoutOrientation = dashboardSide && !explorer ? Avalonia.Layout.Orientation.Vertical : Avalonia.Layout.Orientation.Horizontal;
        KpiPanel.Margin = dashboardSide ? new Thickness(10,0,0,0) : new Thickness(0,8,0,0);
        var side = StationMap.SidePanel(Math.Max(1, Bounds.Width - dashboardWidth));
        if (sidePanel != side)
        {
            sidePanel = side;
            WorkshopContent.ColumnDefinitions = new(side ? "*,300" : "*");
            WorkshopContent.RowDefinitions = new(side ? "Auto" : "Auto,Auto");
            Grid.SetColumn(StationPanel, side ? 1 : 0); Grid.SetRow(StationPanel, side ? 0 : 1);
            StationPanel.Margin = side ? new Thickness(12, 0, 0, 0) : new Thickness(0, 10, 0, 0);
        }
        var metrics = StationMetrics.GetVisualDescendants().OfType<UniformGrid>().FirstOrDefault();
        if (metrics is not null && metrics.Columns != (side ? 1 : 2)) metrics.Columns = side ? 1 : 2;
        // Collapse the unused sidebar; selection does not steal width when the panel is closed.
        if (side) WorkshopContent.ColumnDefinitions[1].Width = new GridLength(GameWorld.HasSelection ? 300 : 0);
        var scroll = this.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
        var bottom = scroll?.TranslatePoint(new Point(0, scroll.Bounds.Height), root)?.Y ?? root.Bounds.Height;
        var height = StationMap.FitHeight(Math.Min(root.Bounds.Height, bottom) - Math.Max(0, position.Y), WorkshopHeader.Bounds.Height, side, GameWorld.HasSelection);
        var sceneHeight = GameWorld.UseSprites ? StationMap.SceneHeight : 680;
        var width = Math.Max(1, WorkshopContent.Bounds.Width - (side && GameWorld.HasSelection ? 300 : 0));
        var fittedHeight = Math.Min(height, width * sceneHeight / 1080);
        if (Math.Abs(WorldViewport.Height - fittedHeight) > 1 || double.IsNaN(WorldViewport.Height)) WorldViewport.Height = fittedHeight;
        var panelHeight = side ? height : 200;
        if (Math.Abs(StationPanel.MaxHeight - panelHeight) > 1) StationPanel.MaxHeight = panelHeight;

    }
}
