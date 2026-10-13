using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Simulation.Sprites;

/// <summary>Window-owned bitmap cache. Resources are embedded, decoded once and explicitly disposed.</summary>
public sealed class SpriteAssets : IDisposable
{
    private readonly Dictionary<string, Bitmap> cache = new(StringComparer.Ordinal);
    private readonly HashSet<string> validated = new(StringComparer.Ordinal);
    private bool disposed;
    public int DecodeCount { get; private set; }
    public Bitmap Get(SpriteDefinition definition)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var key = definition.Resource.AbsoluteUri;
        if (!cache.TryGetValue(key, out var bitmap))
        {
            using var stream = AssetLoader.Open(definition.Resource);
            bitmap = new Bitmap(stream); cache.Add(key, bitmap); DecodeCount++;
        }
        if (!validated.Contains(definition.Id))
        {
            foreach (var frame in definition.Clips.Values.SelectMany(c => c.Frames))
                if (frame.Source.Width <= 0 || frame.Source.Height <= 0 || frame.Source.X < 0 || frame.Source.Y < 0 ||
                    frame.Source.Right > bitmap.PixelSize.Width || frame.Source.Bottom > bitmap.PixelSize.Height)
                    throw new InvalidDataException($"Frame outside {definition.Id} texture.");
            validated.Add(definition.Id);
        }
        return bitmap;
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var bitmap in cache.Values) bitmap.Dispose();
        cache.Clear();
    }
}
