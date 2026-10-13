using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Simulation.Sprites;

public interface ISpriteDecoration
{
    void DrawAfterSprite(DrawingContext context, string id, double seconds);
}

/// <summary>Prepared, depth-sorted image commands. No decoding, sorting or scene allocation in Draw.</summary>
public sealed class SpriteBatch
{
    private sealed record ImageCommand(string Id, Bitmap Image, SpriteClip Clip, Rect[] Destinations, int StaticFrame);
    private readonly ImageCommand[] commands;
    public SpriteBatch(IEnumerable<SpriteObject> objects, IsometricProjection projection, SpriteAssets assets)
    {
        commands = SpritePlacement.InDrawOrder(objects).Select(item =>
        {
            var definition = SpriteCatalog.Assets[item.AssetId];
            var clip = definition.Clips[item.Pose];
            return new ImageCommand(item.Id, assets.Get(definition), clip,
                Enumerable.Range(0, clip.Frames.Count).Select(i => SpritePlacement.Destination(item with { FrameIndex = i }, definition, projection)).ToArray(), item.FrameIndex);
        }).ToArray();
    }
    public void Draw(DrawingContext context, double seconds = 0, bool animateFrames = false, ISpriteDecoration? decoration = null)
    {
        foreach (var command in commands)
        {
            var frame = animateFrames ? SpriteFrameSampling.Index(command.Clip.Frames.Count, command.Clip.FramesPerSecond, seconds) : command.StaticFrame;
            context.DrawImage(command.Image, command.Clip.Frames[frame].Source, command.Destinations[frame]);
            decoration?.DrawAfterSprite(context, command.Id, seconds);
        }
    }
}
