using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Simulation.Sprites;

/// <summary>Prepared, depth-sorted image commands. No decoding, sorting or scene allocation in Draw.</summary>
public sealed class SpriteBatch
{
    private sealed record ImageCommand(Bitmap Image, Rect Source, Rect Destination);
    private readonly ImageCommand[] commands;
    public SpriteBatch(IEnumerable<SpriteObject> objects, IsometricProjection projection, SpriteAssets assets)
    {
        commands = SpritePlacement.InDrawOrder(objects).Select(item =>
        {
            var definition = SpriteCatalog.Assets[item.AssetId];
            return new ImageCommand(assets.Get(definition), definition.Frame(item.Pose, item.FrameIndex).Source,
                SpritePlacement.Destination(item, definition, projection));
        }).ToArray();
    }
    public void Draw(DrawingContext context)
    {
        foreach (var command in commands) context.DrawImage(command.Image, command.Source, command.Destination);
    }
}
