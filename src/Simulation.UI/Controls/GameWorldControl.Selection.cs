using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Simulation.UI.Controls.GameWorld;
using Simulation.UI.ViewModels;

namespace Simulation.UI.Controls;

public sealed partial class GameWorldControl
{
    public static readonly StyledProperty<string?> SelectedStationProperty = AvaloniaProperty.Register<GameWorldControl, string?>(nameof(SelectedStation));
    public string? SelectedStation { get => GetValue(SelectedStationProperty); set => SetValue(SelectedStationProperty, value); }
    public static readonly StyledProperty<StationDetails> DetailsProperty = AvaloniaProperty.Register<GameWorldControl, StationDetails>(nameof(Details), StationDetails.Empty);
    public StationDetails Details { get => GetValue(DetailsProperty); private set => SetValue(DetailsProperty, value); }
    public static readonly StyledProperty<bool> HasSelectionProperty = AvaloniaProperty.Register<GameWorldControl, bool>(nameof(HasSelection));
    public bool HasSelection { get => GetValue(HasSelectionProperty); private set => SetValue(HasSelectionProperty, value); }
    public static readonly StyledProperty<string> WorkflowSummaryProperty = AvaloniaProperty.Register<GameWorldControl, string>(nameof(WorkflowSummary), "Start LIVE to inspect the workshop.");
    public string WorkflowSummary { get => GetValue(WorkflowSummaryProperty); private set => SetValue(WorkflowSummaryProperty, value); }
    public IReadOnlyList<string> StationOptions => StationMap.Names;
    private string? hoveredStation;
    private static readonly Cursor StationCursor = new(StandardCursorType.Hand);
    private static readonly Cursor DefaultCursor = new(StandardCursorType.Arrow);
    private void RefreshStationDetails()
    {
        if (Scene is not { HasSession: true } && SelectedStation is not null) SelectedStation = null;
        Details = StationDetails.For(SelectedStation, Scene);
        HasSelection = Details.Title.Length > 0;
        WorkflowSummary = StationDetails.Summary(Scene);
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Scene is not { HasSession: true } || e.KeyModifiers != KeyModifiers.None) return;
        switch (e.Key)
        {
            case Key.Left: case Key.Up: SelectedStation = StationMap.Move(SelectedStation, -1); break;
            case Key.Right: case Key.Down: SelectedStation = StationMap.Move(SelectedStation, 1); break;
            case Key.Home: SelectedStation = StationMap.Names[0]; break;
            case Key.End: SelectedStation = StationMap.Names[^1]; break;
            case Key.Enter: case Key.Space: SelectedStation ??= StationMap.Names[0]; break;
            case Key.Escape: SelectedStation = null; break;
            default: return;
        }
        e.Handled = true;
    }
    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        ScrollViewer.SetBringIntoViewOnFocusChange(this, e.NavigationMethod != NavigationMethod.Pointer);
        base.OnGotFocus(e); InvalidateVisual();
    }
    protected override void OnLostFocus(RoutedEventArgs e) { base.OnLostFocus(e); InvalidateVisual(); }
}
