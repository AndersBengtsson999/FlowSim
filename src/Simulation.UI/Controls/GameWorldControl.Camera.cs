using Avalonia;
using Avalonia.Input;
using Simulation.UI.Controls.GameWorld;

namespace Simulation.UI.Controls;

public sealed partial class GameWorldControl
{
    public WorkshopViewport Camera { get; } = new();
    public static readonly StyledProperty<string> ZoomLabelProperty = AvaloniaProperty.Register<GameWorldControl, string>(nameof(ZoomLabel), "100%");
    public string ZoomLabel { get => GetValue(ZoomLabelProperty); private set => SetValue(ZoomLabelProperty, value); }
    private readonly WorkshopDrag cameraDrag = new();
    private static readonly Cursor PanCursor = new(StandardCursorType.SizeAll);
    private IPointer? capturedPointer;
    private Point? hoverPoint;
    private bool middleDrag;
    public void ZoomBy(double factor) { Camera.ZoomAt(factor, new(Bounds.Width / 2, Bounds.Height / 2)); CameraChanged(); }
    public void FitCamera() { Camera.Fit(); CameraChanged(); }
    private void ResizeCamera() { Camera.Resize(Bounds.Size, new(1080, UseSprites ? StationMap.SceneHeight : 680)); CameraChanged(); }
    private string? StationAt(Point point) => Scene is { HasSession: true } ? (UseSprites ? StationMap.InspectAt(Camera.ToWorld(point)) : StationMap.HitTest(Camera.ToWorld(point))) : null;
    private void CameraChanged()
    {
        ZoomLabel = $"{Camera.Zoom * 100:0}%";
        UpdateHover(); InvalidateVisual();
    }
    private void UpdateHover()
    {
        hoveredStation = !cameraDrag.Panning && hoverPoint is { } p ? StationAt(p) : null;
        Cursor = cameraDrag.Panning ? PanCursor : hoveredStation is not null ? StationCursor : DefaultCursor;
    }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (e.Delta.Y == 0) return;
        var point = e.GetPosition(this);
        if (!new Rect(Bounds.Size).Contains(point)) return;
        Camera.ZoomAt(Math.Exp(Math.Clamp(e.Delta.Y * .12, -1, 1)), point);
        hoverPoint = point; CameraChanged(); e.Handled = true;
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var properties = e.GetCurrentPoint(this).Properties;
        if (!properties.IsLeftButtonPressed && !properties.IsMiddleButtonPressed || capturedPointer is not null) return;
        Focus(NavigationMethod.Pointer);
        var point = e.GetPosition(this); middleDrag = properties.IsMiddleButtonPressed;
        cameraDrag.Begin(point, middleDrag || StationAt(point) is null);
        capturedPointer = e.Pointer; e.Pointer.Capture(this); e.Handled = true;
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this); hoverPoint = point;
        if (capturedPointer == e.Pointer && cameraDrag.Active)
        {
            var delta = cameraDrag.Move(point);
            if (delta != default) Camera.Pan(delta);
            e.Handled = true;
        }
        CameraChanged();
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (capturedPointer != e.Pointer) return;
        var point = e.GetPosition(this);
        var delta = cameraDrag.Move(point); if (delta != default) Camera.Pan(delta);
        var click = cameraDrag.End(point);
        capturedPointer = null; e.Pointer.Capture(null);
        if (click && !middleDrag) SelectedStation = StationAt(point);
        CameraChanged(); e.Handled = true;
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e); capturedPointer = null; cameraDrag.Cancel(); CameraChanged();
    }
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e); hoverPoint = null; UpdateHover(); InvalidateVisual();
    }
}
