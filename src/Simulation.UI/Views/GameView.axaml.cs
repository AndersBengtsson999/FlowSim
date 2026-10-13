using Avalonia;
using Avalonia.Controls;
namespace Simulation.UI.Views;
public partial class GameView : UserControl
{
    public GameView()
    {
        InitializeComponent();
        LayoutUpdated += (_, _) =>
        {
            if (!IsEffectivelyVisible || TopLevel.GetTopLevel(this) is not { } root || this.TranslatePoint(default, root) is not { } position) return;
            // Fit comfortably when possible; short windows keep a readable world and use the existing Live scroll viewer.
            var height = Math.Max(390, root.Bounds.Height - Math.Max(0, position.Y) - 240);
            if (Math.Abs(WorldViewport.MaxHeight - height) > 1) WorldViewport.MaxHeight = height;
        };
    }
}
