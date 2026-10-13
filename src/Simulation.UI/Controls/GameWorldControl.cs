using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Simulation.UI.ViewModels;
using Simulation.UI.Controls.GameWorld;

namespace Simulation.UI.Controls;

/// <summary>Read-only scene host: no clock, simulation reference or commands.</summary>
public sealed class GameWorldControl : Control
{
    public static readonly StyledProperty<GameSceneState?> SceneProperty = AvaloniaProperty.Register<GameWorldControl, GameSceneState?>(nameof(Scene));
    public GameSceneState? Scene { get => GetValue(SceneProperty); set => SetValue(SceneProperty, value); }
    public static readonly StyledProperty<bool> UseSpritesProperty = AvaloniaProperty.Register<GameWorldControl, bool>(nameof(UseSprites), true);
    public bool UseSprites { get => GetValue(UseSpritesProperty); set => SetValue(UseSpritesProperty, value); }
    private readonly GameWorldRenderer renderer = new();
    private SpriteWorkshopRenderer? sprites;
    public int SpriteDecodeCount => sprites?.DecodeCount ?? 0;
    public int SpriteObjectCount => sprites?.ObjectCount ?? 0;
    public GameWorldControl() => RenderOptions.SetBitmapInterpolationMode(this, Avalonia.Media.Imaging.BitmapInterpolationMode.HighQuality);
    static GameWorldControl() => AffectsRender<GameWorldControl>(SceneProperty, UseSpritesProperty);
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SceneProperty) renderer.ClearLabels();
    }
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (UseSprites) (sprites ??= new()).Render(context, Scene);
        else renderer.Render(context, Scene);
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        sprites?.Dispose(); sprites = null;
        base.OnDetachedFromVisualTree(e);
    }
}
