using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace SpritePrototype;

public sealed class SpriteWorldControl : Control
{
    private readonly SpriteAssets assets;
    private SpriteObject[] objects = [];
    private SpriteBatch? batch;
    private readonly Dictionary<string, FormattedText> captions = new();
    public IsometricProjection Projection { get; private set; } = new(new(640, 360));
    public bool IsOffice { get; private set; } = true;
    public bool ShowAnchors { get; set; }
    public IReadOnlyList<SpriteObject> Objects => objects;
    public SpriteWorldControl(SpriteAssets assets)
    {
        this.assets = assets;
        Width = 1280; Height = 880;
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.HighQuality);
        SetObjects(OfficeScene.Create());
    }
    public void SetOffice(bool office)
    {
        IsOffice = office;
        Width = office ? 1280 : 960; Height = office ? 880 : 570;
        Projection = new(office ? new(640, 360) : new(480, 280));
        SetObjects(office ? OfficeScene.Create() : SpriteCatalog.Demo);
    }
    public void SetObjects(IEnumerable<SpriteObject> scene)
    {
        // Precompute depth order outside Render. No clock or mutable simulation state exists here.
        objects = SpritePlacement.InDrawOrder(scene);
        batch = new SpriteBatch(objects, Projection, assets);
        InvalidateVisual();
    }
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        batch?.Draw(context);
        if (IsOffice)
            foreach (var station in OfficeScene.Stations)
            {
                var p = Projection.Project(station.Position);
                if (!captions.TryGetValue(station.Id, out var label))
                    captions[station.Id] = label = new FormattedText($"{station.Id}  {station.Caption}", CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight, new Typeface("Inter"), 21, Brushes.LightGoldenrodYellow);
                context.DrawText(label, new(p.X - label.Width / 2, p.Y - 225));
            }
        if (ShowAnchors)
            foreach (var item in objects.Where(o => o.Layer > 0))
            {
                var p = Projection.Project(item.Position);
                context.DrawEllipse(Brushes.Yellow, null, p, 3, 3);
                context.DrawLine(new Pen(Brushes.Yellow, 1), p + new Vector(-8, 0), p + new Vector(8, 0));
            }
    }
}
