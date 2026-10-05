using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Simulation.UI.ViewModels;
namespace Simulation.UI.Controls;

public sealed class TechnicalDebtBar : Control
{
    public static readonly StyledProperty<DebtBarState?> StateProperty = AvaloniaProperty.Register<TechnicalDebtBar, DebtBarState?>(nameof(State));
    public DebtBarState? State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }
    static TechnicalDebtBar() => AffectsRender<TechnicalDebtBar>(StateProperty);
    public override void Render(DrawingContext context)
    {
        base.Render(context); if (State is not { } s) return;
        var width = Math.Max(1, Bounds.Width - 12); const double x = 6, y = 4, height = 8;
        var green = width * s.GreenEnd; var tolerance = width * s.YellowEnd;
        context.DrawRectangle(Brush.Parse("#659B78"), null, new(x, y, green, height));
        context.DrawRectangle(Brush.Parse("#DFC16B"), null, new(x + green, y, tolerance - green, height));
        context.DrawRectangle(Brush.Parse("#C77C77"), null, new(x + tolerance, y, width - tolerance, height));
        context.DrawLine(new Pen(Brushes.Black, 1, DashStyle.Dash), new(x + tolerance, 0), new(x + tolerance, 17));
        var current = x + width * s.Marker;
        context.DrawLine(new Pen(Brushes.Black, 3), new(current, 0), new(current, 17));
        void Label(string text, double left) => context.DrawText(new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, new Typeface("Inter"), 10, Brush.Parse("#435563")), new(left, 18));
        Label("0", x); Label($"Tolerance {s.Tolerance:P1}", Math.Clamp(x + tolerance - 38, 18, Math.Max(18,width-160)));
        Label($"{s.ScaleMaximum:P1}", Math.Max(x, width-55));
    }
}
