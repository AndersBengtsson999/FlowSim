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
    private Simulation.UI.ViewModels.LiveViewModel? contextOwner;
    private (object? Session, int Day, int Launches, int Window, string? Station)? contextKey;
    public GameView()
    {
        InitializeComponent();
        // Let the list own bring-into-view requests; never bubble them into the application page.
        StationPanel.AddHandler(Control.RequestBringIntoViewEvent, (_, e) => e.Handled = true);
        GameWorld.Inspection.Updating += CaptureInspectionAnchor;
        GameWorld.Inspection.Updated += RestoreInspectionAnchor;
        GameWorld.PropertyChanged += (_, e) => { if (e.Property == Simulation.UI.Controls.GameWorldControl.DetailsProperty) RefreshContext(); };
        DataContextChanged += (_, _) => ObserveContext();
        AttachedToVisualTree += (_, _) => ObserveContext();
        DetachedFromVisualTree += (_, _) => { if (contextOwner is not null) contextOwner.PropertyChanged -= ContextChanged; contextOwner = null; contextKey = null; };
        LayoutUpdated += (_, _) => FitWorkshop();
    }
    private void ObserveContext()
    {
        if (contextOwner is not null) contextOwner.PropertyChanged -= ContextChanged;
        contextOwner = DataContext as Simulation.UI.ViewModels.LiveViewModel;
        if (contextOwner is not null) contextOwner.PropertyChanged += ContextChanged;
        contextKey = null; RefreshContext();
    }
    private void ContextChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => RefreshContext();
    private void RefreshContext()
    {
        var vm = DataContext as Simulation.UI.ViewModels.LiveViewModel;
        var next = ((object?)vm?.Live?.Session, vm?.Day ?? 0, vm?.Live?.Session.ProductDelivery.CustomerLaunches.Count ?? 0, vm?.KpiWindow ?? 20, GameWorld.SelectedStation);
        ReleaseEconomics.IsVisible = GameWorld.SelectedStation == "Release";
        if (ReleaseEconomics.IsVisible) ReleaseEconomics.Text = vm?.ReleaseInspectionText ?? "";
        if (contextKey == next) return;
        contextKey = next;
        ContextKpis.ItemsSource = vm?.StationPerformance(GameWorld.SelectedStation);
        LaunchContext.IsVisible = LaunchDetailsLink.IsVisible = GameWorld.SelectedStation is "Release" or "Ready for Release";
    }
    private void ContextKpiClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: Simulation.UI.ViewModels.KpiCard card } && DataContext is Simulation.UI.ViewModels.LiveViewModel vm)
        { vm.OpenExplorer(card.Observation.Definition.Id); KpiPanel.OpenExpandedExplorer(); }
    }
    private void LaunchDetailsClicked(object? sender, RoutedEventArgs e)
    { if (DataContext is Simulation.UI.ViewModels.LiveViewModel vm) { vm.ProductDetailsExpanded = true; this.GetVisualDescendants().OfType<ProductLaunchView>().FirstOrDefault()?.BringIntoView(); } }
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
        var side = StationMap.SidePanel(Math.Max(1, Bounds.Width));
        if (sidePanel != side)
        {
            sidePanel = side;
            WorkshopContent.ColumnDefinitions = new(side ? "*,300" : "*");
            WorkshopContent.RowDefinitions = new(side ? "Auto" : "Auto,Auto");
            Grid.SetColumn(StationPanel, side ? 1 : 0); Grid.SetRow(StationPanel, side ? 0 : 1);
            StationPanel.Margin = side ? new Thickness(12, 0, 0, 0) : new Thickness(0, 10, 0, 0);
        }
        // Collapse the unused sidebar; selection does not steal width when the panel is closed.
        if (side) WorkshopContent.ColumnDefinitions[1].Width = new GridLength(GameWorld.HasSelection ? 300 : 0);
        var scroll = this.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
        var bottom = scroll?.TranslatePoint(new Point(0, scroll.Bounds.Height), root)?.Y ?? root.Bounds.Height;
        // Reserve the complete compact KPI strip; the workshop uses the remaining viewport.
        var dashboardHeight = KpiPanel.IsVisible ? KpiPanel.Bounds.Height : 0;
        var minimumHeight = Bounds.Width < 900 || (!side && GameWorld.HasSelection) ? 300 : 180;
        var height = StationMap.FitHeight(Math.Min(root.Bounds.Height, bottom) - Math.Max(0, position.Y) - dashboardHeight, WorkshopHeader.Bounds.Height, side, GameWorld.HasSelection, minimumHeight);
        var sceneHeight = GameWorld.UseSprites ? StationMap.SceneHeight : 680;
        var width = Math.Max(1, WorkshopContent.Bounds.Width - (side && GameWorld.HasSelection ? 300 : 0));
        // Preserve readable sprites; short windows may scroll the dashboard instead of miniaturizing the workshop.
        var fittedHeight = Math.Min(Math.Max(180, height), width * sceneHeight / 1080);
        if (Math.Abs(WorldViewport.Height - fittedHeight) > 1 || double.IsNaN(WorldViewport.Height)) WorldViewport.Height = fittedHeight;
        // Reserve a usable work-item list beneath the bounded contextual section.
        var panelHeight = side ? Math.Max(360, fittedHeight) : 420;
        if (Math.Abs(StationPanel.Height - panelHeight) > 1 || double.IsNaN(StationPanel.Height)) StationPanel.Height = panelHeight;

    }
    private List<(string Id, double Y)>? inspectionAnchors;
    private bool resettingInspection;
    private void CaptureInspectionAnchor(bool reset)
    {
        resettingInspection |= reset;
        if (inspectionAnchors is not null) return;
        var scroll = InspectionList.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        inspectionAnchors = scroll is null ? [] : InspectionList.GetVisualDescendants().OfType<ListBoxItem>()
            .Where(c => c.DataContext is Simulation.UI.ViewModels.InspectionItemViewModel)
            .Select(c => (Id: ((Simulation.UI.ViewModels.InspectionItemViewModel)c.DataContext!).Id, Y: c.TranslatePoint(default, scroll)?.Y ?? -10000))
            .Where(p => p.Y >= 0 && p.Y < scroll.Viewport.Height).OrderBy(p => p.Y).ToList();
    }
    private void RestoreInspectionAnchor(bool reset)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (inspectionAnchors is null) return;
            var anchors = inspectionAnchors; inspectionAnchors = null;
            var scroll = InspectionList.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
            if (resettingInspection) { resettingInspection = false; if (scroll is not null) scroll.Offset = default; return; }
            if (scroll is null) return;
            InspectionList.UpdateLayout();
            foreach (var anchor in anchors)
            {
                var control = InspectionList.GetVisualDescendants().OfType<ListBoxItem>().FirstOrDefault(c =>
                    c.DataContext is Simulation.UI.ViewModels.InspectionItemViewModel item && item.Id == anchor.Id);
                if (control?.TranslatePoint(default, scroll) is not { } point) continue;
                if (Math.Abs(point.Y - anchor.Y) > .5) scroll.Offset = new(0, Math.Max(0, scroll.Offset.Y + point.Y - anchor.Y));
                break;
            }
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

}
