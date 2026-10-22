using Avalonia;
using Simulation.Sprites;
namespace Simulation.UI.Controls.GameWorld;

public static class VehicleInspection
{
    // Same sprite placement as rendering, including the moving vehicle; no animation-derived delivery data.
    public static Rect Bounds(ReleaseVisual? visual)
    {
        var sprite = visual is { Phase: not ReleasePhase.FeedbackOnly }
            ? visual.Sprites().First(s => s.Id == "release/van")
            : new SpriteObject("release/van", "delivery-van", SpriteWorkshop.Ground(214, 350), .52);
        return SpritePlacement.Destination(sprite, SpriteCatalog.Assets[sprite.AssetId], SpriteWorkshop.Projection);
    }
}
