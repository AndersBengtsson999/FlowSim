using Avalonia;

namespace Simulation.Sprites;

public enum SpritePose { Idle, Working, Walking, Collaborating, Reviewing, Testing, Maintaining }
public readonly record struct WorldPoint(double X, double Y, double Elevation = 0);

/// <summary>2:1 ground axes; elevation is vertical screen displacement, never a simulation value.</summary>
public readonly record struct IsometricProjection(Point Origin, double TileWidth = 96, double TileHeight = 48)
{
    public Point Project(WorldPoint p) => new(Origin.X + (p.X - p.Y) * TileWidth / 2,
        Origin.Y + (p.X + p.Y) * TileHeight / 2 - p.Elevation * TileHeight);
}

// Pixel rectangle and anchor are relative to the original PNG, with the anchor relative to Source.
public sealed record SpriteFrame(Rect Source, Point Anchor);
public sealed record SpriteClip(IReadOnlyList<SpriteFrame> Frames, double FramesPerSecond = 4);
public sealed record SpriteDefinition(string Id, Uri Resource, double PixelsToDip, IReadOnlyDictionary<SpritePose, SpriteClip> Clips)
{
    public SpriteFrame Frame(SpritePose pose, int index)
    {
        if (!Clips.TryGetValue(pose, out var clip)) throw new InvalidOperationException($"{Id} has no {pose} artwork.");
        if ((uint)index >= clip.Frames.Count) throw new ArgumentOutOfRangeException(nameof(index));
        return clip.Frames[index];
    }
}

/// <summary>Stable presentation object. Depth stays on the ground even for an elevated desktop object.</summary>
public sealed record SpriteObject(string Id, string AssetId, WorldPoint Position, double Scale = 1,
    int Layer = 1, int StackOrder = 0, SpritePose Pose = SpritePose.Idle, int FrameIndex = 0, WorldPoint? SortAnchor = null, double Opacity = 1)
{
    public double GroundDepth => (SortAnchor ?? Position).X + (SortAnchor ?? Position).Y;
}

public static class SpritePlacement
{
    public static SpriteObject[] InDrawOrder(IEnumerable<SpriteObject> objects) => objects
        .OrderBy(o => o.Layer).ThenBy(o => o.GroundDepth).ThenBy(o => o.StackOrder)
        .ThenBy(o => o.Id, StringComparer.Ordinal).ToArray();

    public static Rect Destination(SpriteObject item, SpriteDefinition asset, IsometricProjection projection)
    {
        var frame = asset.Frame(item.Pose, item.FrameIndex);
        var anchor = projection.Project(item.Position);
        var scale = item.Scale * asset.PixelsToDip;
        if (!(scale > 0) || !double.IsFinite(scale)) throw new ArgumentOutOfRangeException(nameof(item));
        return new(anchor.X - frame.Anchor.X * scale, anchor.Y - frame.Anchor.Y * scale,
            frame.Source.Width * scale, frame.Source.Height * scale);
    }
}
