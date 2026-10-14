using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Simulation.Sprites;

public interface ISpriteDecoration
{
    void DrawAfterSprite(DrawingContext context, string id, double seconds);
}

/// <summary>Prepared, depth-sorted image commands. Static commands are cached; optional moving objects are merged in depth order.</summary>
public sealed class SpriteBatch
{
    private sealed record ImageCommand(SpriteObject Object, Bitmap Image, SpriteClip Clip, Rect[] Destinations, int StaticFrame);
    private readonly ImageCommand[] commands;
    private readonly IsometricProjection projection;
    private readonly SpriteAssets assets;
    public SpriteBatch(IEnumerable<SpriteObject> objects, IsometricProjection projection, SpriteAssets assets)
    {
        this.projection = projection; this.assets = assets;
        commands = SpritePlacement.InDrawOrder(objects).Select(item =>
        {
            var definition = SpriteCatalog.Assets[item.AssetId];
            var clip = definition.Clips[item.Pose];
            return new ImageCommand(item, assets.Get(definition), clip,
                Enumerable.Range(0, clip.Frames.Count).Select(i => SpritePlacement.Destination(item with { FrameIndex = i }, definition, projection)).ToArray(), item.FrameIndex);
        }).ToArray();
    }
    public void Draw(DrawingContext context, double seconds = 0, bool animateFrames = false, ISpriteDecoration? decoration = null, IReadOnlyList<SpriteObject>? moving = null, IReadOnlySet<string>? hidden = null)
    {
        var dynamicObjects = moving is { Count: > 0 } ? SpritePlacement.InDrawOrder(moving) : [];
        var index = 0;
        void DrawMoving(SpriteObject item)
        {
            var definition = SpriteCatalog.Assets[item.AssetId];
            context.DrawImage(assets.Get(definition), definition.Frame(item.Pose, item.FrameIndex).Source,
                SpritePlacement.Destination(item, definition, projection));
        }
        foreach (var command in commands)
        {
            while (index < dynamicObjects.Length && Compare(dynamicObjects[index], command.Object) < 0) DrawMoving(dynamicObjects[index++]);
            if (hidden?.Contains(command.Object.Id) == true) continue;
            var frame = animateFrames ? SpriteFrameSampling.Index(command.Clip.Frames.Count, command.Clip.FramesPerSecond, seconds) : command.StaticFrame;
            context.DrawImage(command.Image, command.Clip.Frames[frame].Source, command.Destinations[frame]);
            decoration?.DrawAfterSprite(context, command.Object.Id, seconds);
        }
        while (index < dynamicObjects.Length) DrawMoving(dynamicObjects[index++]);
    }
    private static int Compare(SpriteObject a, SpriteObject b)
    {
        var c = a.Layer.CompareTo(b.Layer); if (c != 0) return c;
        c = a.GroundDepth.CompareTo(b.GroundDepth); if (c != 0) return c;
        c = a.StackOrder.CompareTo(b.StackOrder); if (c != 0) return c;
        return StringComparer.Ordinal.Compare(a.Id, b.Id);
    }
}
