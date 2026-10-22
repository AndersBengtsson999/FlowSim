using Avalonia;

namespace Simulation.UI.Controls.GameWorld;

/// <summary>Presentation-only camera, in logical pixels. Zoom is relative to the fitted scale.</summary>
public sealed class WorkshopViewport
{
    public const double MinZoom = .75, MaxZoom = 2.5;
    public Size World { get; private set; } = new(1080, 620);
    public Size View { get; private set; } = new(1080, 620);
    public double Zoom { get; private set; } = 1;
    public bool IsManual { get; private set; }
    public double Padding { get; private set; }
    public double FitScale => Math.Min(Math.Max(1, View.Width - 2 * Padding) / World.Width, Math.Max(1, View.Height - 2 * Padding) / World.Height);
    public double Scale => FitScale * Zoom;
    public Vector Offset { get; private set; }
    public Matrix Matrix => new(Scale, 0, 0, Scale, Offset.X, Offset.Y);
    public Point ToScreen(Point p) => new(p.X * Scale + Offset.X, p.Y * Scale + Offset.Y);
    public Point ToWorld(Point p) => new((p.X - Offset.X) / Scale, (p.Y - Offset.Y) / Scale);
    private Point Center => new(View.Width / 2, View.Height / 2);

    public void Resize(Size view, Size world, double padding = 0)
    {
        if (view.Width <= 0 || view.Height <= 0 || world.Width <= 0 || world.Height <= 0 || view == View && world == World && padding == Padding) return;
        var center = ToWorld(Center);
        View = view; World = world; Padding = Math.Clamp(double.IsFinite(padding) ? padding : 0, 0, Math.Min(view.Width, view.Height) / 4);
        if (!IsManual) Fit();
        else { Offset = new(Center.X - center.X * Scale, Center.Y - center.Y * Scale); Clamp(); }
    }
    public void Fit()
    {
        Zoom = 1; IsManual = false;
        Offset = new((View.Width - World.Width * Scale) / 2, (View.Height - World.Height * Scale) / 2);
    }
    public void ZoomAt(double factor, Point anchor)
    {
        if (!double.IsFinite(factor) || factor <= 0) return;
        var next = Math.Clamp(Zoom * factor, MinZoom, MaxZoom);
        if (Math.Abs(next - Zoom) < .000001) return;
        var world = ToWorld(anchor);
        Zoom = next; IsManual = true;
        Offset = new(anchor.X - world.X * Scale, anchor.Y - world.Y * Scale);
        Clamp();
    }
    public void Pan(Vector delta)
    {
        if (!double.IsFinite(delta.X) || !double.IsFinite(delta.Y)) return;
        IsManual = true; Offset += delta; Clamp();
    }
    private void Clamp()
    {
        // Keep at least a quarter of the smaller extent visible, with continuous edge clamping.
        var x = Math.Min(View.Width, World.Width * Scale) * .25;
        var y = Math.Min(View.Height, World.Height * Scale) * .25;
        Offset = new(Math.Clamp(Offset.X, x - World.Width * Scale, View.Width - x),
            Math.Clamp(Offset.Y, y - World.Height * Scale, View.Height - y));
    }
}

/// <summary>Threshold is measured in screen DIPs, independent of camera scale.</summary>
public sealed class WorkshopDrag
{
    public const double Threshold = 5;
    private Point start, previous;
    private bool canPan;
    public bool Active { get; private set; }
    public bool Moved { get; private set; }
    public bool Panning => Active && Moved && canPan;
    public void Begin(Point point, bool allowPan) { start = previous = point; canPan = allowPan; Active = true; Moved = false; }
    public Vector Move(Point point)
    {
        if (!Active) return default;
        if (!Moved && ((Vector)(point - start)).Length > Threshold) Moved = true;
        var delta = Moved && canPan ? point - previous : default;
        if (Moved) previous = point;
        return delta;
    }
    public bool End(Point point) { Move(point); var click = Active && !Moved; Cancel(); return click; }
    public void Cancel() { Active = false; Moved = false; }
}
