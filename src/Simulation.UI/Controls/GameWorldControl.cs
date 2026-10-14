using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Diagnostics;
using Simulation.Sprites;
using Simulation.UI.ViewModels;
using Simulation.UI.Controls.GameWorld;

namespace Simulation.UI.Controls;

/// <summary>Read-only scene host with one presentation timer. No simulation reference or commands.</summary>
public sealed class GameWorldControl : Control
{
    public static readonly StyledProperty<GameSceneState?> SceneProperty = AvaloniaProperty.Register<GameWorldControl, GameSceneState?>(nameof(Scene));
    public GameSceneState? Scene { get => GetValue(SceneProperty); set => SetValue(SceneProperty, value); }
    public static readonly StyledProperty<bool> UseSpritesProperty = AvaloniaProperty.Register<GameWorldControl, bool>(nameof(UseSprites), true);
    public bool UseSprites { get => GetValue(UseSpritesProperty); set => SetValue(UseSpritesProperty, value); }
    public static readonly StyledProperty<bool> AnimateActivityProperty = AvaloniaProperty.Register<GameWorldControl, bool>(nameof(AnimateActivity), true);
    public bool AnimateActivity { get => GetValue(AnimateActivityProperty); set => SetValue(AnimateActivityProperty, value); }
    public static readonly StyledProperty<bool> SimulationRunningProperty = AvaloniaProperty.Register<GameWorldControl, bool>(nameof(SimulationRunning));
    public bool SimulationRunning { get => GetValue(SimulationRunningProperty); set => SetValue(SimulationRunningProperty, value); }
    public static readonly StyledProperty<double> PlaybackSpeedProperty = AvaloniaProperty.Register<GameWorldControl, double>(nameof(PlaybackSpeed), 1);
    public double PlaybackSpeed { get => GetValue(PlaybackSpeedProperty); set => SetValue(PlaybackSpeedProperty, value); }
    private readonly PackageMotion packages = new();
    public ReleaseVisual? CurrentRelease => packages.Release(animation.Seconds);
    public int MovingPackageCount => packages.Count;
    private readonly GameWorldRenderer renderer = new();
    private SpriteWorkshopRenderer? sprites;
    private readonly PresentationClock animation = new();
    private readonly Stopwatch monotonic = Stopwatch.StartNew();
    private readonly DispatcherTimer animationTimer;
    private readonly List<Visual> watchedAncestors = [];
    private bool attached;
    private bool inViewport = true;
    private int previousDay;
    public int SpriteDecodeCount => sprites?.DecodeCount ?? 0;
    public int SpriteObjectCount => sprites?.ObjectCount ?? 0;
    public bool IsAnimationRunning => animation.IsRunning;
    public long AnimationTicks { get; private set; }
    public double AnimationSeconds => animation.Seconds;
    public GameWorldControl()
    {
        RenderOptions.SetBitmapInterpolationMode(this, Avalonia.Media.Imaging.BitmapInterpolationMode.HighQuality);
        // Low-priority shared scene clock. DispatcherTimer coalesces overdue ticks; no per-worker tasks.
        animationTimer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1.0 / 30) };
        animationTimer.Tick += AnimationTick;
        EffectiveViewportChanged += (_, e) =>
        {
            inViewport = e.EffectiveViewport.Intersects(new Rect(Bounds.Size));
            SynchronizeAnimation();
        };
    }
    static GameWorldControl() => AffectsRender<GameWorldControl>(SceneProperty, UseSpritesProperty, AnimateActivityProperty, SimulationRunningProperty);
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PlaybackSpeedProperty) packages.Cancel();
        if (change.Property == SceneProperty)
        {
            renderer.ClearLabels();
            if (Scene is null || Scene.Day < previousDay) animation.Reset(monotonic.Elapsed);
            previousDay = Scene?.Day ?? 0;
            packages.Observe(Scene, animation.Seconds, animation.IsRunning && SimulationRunning, PlaybackSpeed);
        }
        if (change.Property == SceneProperty || change.Property == UseSpritesProperty || change.Property == AnimateActivityProperty ||
            change.Property == SimulationRunningProperty || change.Property == IsVisibleProperty || change.Property == PlaybackSpeedProperty) SynchronizeAnimation();
    }
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (UseSprites) (sprites ??= new()).Render(context, Scene, AnimateActivity ? animation.Seconds : 0, AnimateActivity, packages.Sample(animation.Seconds), packages.Release(animation.Seconds));
        else renderer.Render(context, Scene);
    }
    private void SynchronizeAnimation()
    {
        if (animationTimer is null) return;
        var visible = IsEffectivelyVisible && TopLevel.GetTopLevel(this) is not Window { WindowState: WindowState.Minimized };
        var run = PresentationClock.ShouldRun(attached, visible, inViewport, UseSprites, AnimateActivity, SimulationRunning, Scene is { HasSession: true });
        if (!attached || !visible || !inViewport || !UseSprites || !AnimateActivity) packages.Cancel();
        animation.SetRunning(run, monotonic.Elapsed);
        if (run) animationTimer.Start(); else animationTimer.Stop();
    }
    private void AnimationTick(object? sender, EventArgs e)
    {
        SynchronizeAnimation();
        if (!animation.IsRunning) return;
        AnimationTicks++;
        InvalidateVisual();
    }
    private void AncestorChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty || e.Property == Window.WindowStateProperty) SynchronizeAnimation();
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        attached = true; inViewport = true;
        foreach (var ancestor in this.GetVisualAncestors())
        {
            watchedAncestors.Add(ancestor); ancestor.PropertyChanged += AncestorChanged;
        }
        SynchronizeAnimation();
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        attached = false; SynchronizeAnimation();
        foreach (var ancestor in watchedAncestors) ancestor.PropertyChanged -= AncestorChanged;
        watchedAncestors.Clear();
        sprites?.Dispose(); sprites = null;
        base.OnDetachedFromVisualTree(e);
    }
}
