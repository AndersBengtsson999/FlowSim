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
public sealed partial class GameWorldControl : Control
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
    public static readonly StyledProperty<LaunchNotice?> CustomerLaunchProperty = AvaloniaProperty.Register<GameWorldControl, LaunchNotice?>(nameof(CustomerLaunch));
    public LaunchNotice? CustomerLaunch { get => GetValue(CustomerLaunchProperty); set => SetValue(CustomerLaunchProperty, value); }
    private double launchStarted = double.NegativeInfinity;
    private bool LaunchAnimating => AnimateActivity && CustomerLaunch is not null && monotonic.Elapsed.TotalSeconds - launchStarted < 3.5;
    private ReleaseVisual? CustomerVehicle()
    {
        if (!LaunchAnimating || CustomerLaunch is not { } launch) return null;
        var t = Math.Clamp((monotonic.Elapsed.TotalSeconds - launchStarted) / 3.5, 0, 1);
        return new(launch.Items, launch.Day, launch.Day, ReleasePhase.Departing, t * t * (3 - 2 * t), 1) { CustomerVersion = launch.Version };
    }
    private readonly PackageMotion packages = new();
    private readonly WorkerMotion workers = new();
    private readonly QueueStackMotion queues = new();
    public int ChangingStackCount => queues.Count;
    public int MovingWorkerCount => workers.MovingCount;
    public IReadOnlyList<LivingWorker> CurrentWorkers => workers.Sample(animation.Seconds, false);
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
        Focusable = true;
        ClipToBounds = true;
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
    static GameWorldControl() => AffectsRender<GameWorldControl>(SceneProperty, UseSpritesProperty, AnimateActivityProperty, SimulationRunningProperty, SelectedStationProperty);
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CustomerLaunchProperty) { launchStarted = CustomerLaunch is null || !AnimateActivity ? double.NegativeInfinity : monotonic.Elapsed.TotalSeconds; SynchronizeAnimation(); InvalidateVisual(); }
        if (change.Property == AnimateActivityProperty && !AnimateActivity) launchStarted = double.NegativeInfinity;
        if (change.Property == BoundsProperty || change.Property == UseSpritesProperty) ResizeCamera();
        if (change.Property == SceneProperty || change.Property == SelectedStationProperty) RefreshStationDetails();
        if (change.Property == PlaybackSpeedProperty) { packages.Cancel(); queues.Cancel(); workers.Synchronize(animation.Seconds); }
        if (change.Property == SceneProperty)
        {
            renderer.ClearLabels();
            if (Scene is null || Scene.Day < previousDay) animation.Reset(monotonic.Elapsed);
            previousDay = Scene?.Day ?? 0;
            packages.Observe(Scene, animation.Seconds, animation.IsRunning && SimulationRunning, PlaybackSpeed);
            workers.Observe(Scene, animation.Seconds, animation.IsRunning && SimulationRunning, PlaybackSpeed);
            queues.Observe(Scene, animation.Seconds, animation.IsRunning && SimulationRunning, PlaybackSpeed);
        }
        if (change.Property == SceneProperty || change.Property == UseSpritesProperty || change.Property == AnimateActivityProperty ||
            change.Property == SimulationRunningProperty || change.Property == IsVisibleProperty || change.Property == PlaybackSpeedProperty) SynchronizeAnimation();
    }
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));
        using var cameraTransform = context.PushTransform(Camera.Matrix);
        if (UseSprites) (sprites ??= new()).Render(context, Scene, AnimateActivity ? animation.Seconds : 0, AnimateActivity, packages.Sample(animation.Seconds), CustomerVehicle() ?? packages.Release(animation.Seconds), workers.Sample(animation.Seconds, animation.IsRunning && PlaybackSpeed <= 2), queues, SelectedStation, hoveredStation, IsFocused, Camera.Scale);
        else { renderer.Render(context, Scene); StationMap.Highlight(context, SelectedStation, hoveredStation, IsFocused); }
        StationMap.SelectionOutline(context, SelectedStation, Camera.Scale);
    }
    private void SynchronizeAnimation()
    {
        if (animationTimer is null) return;
        var visible = IsEffectivelyVisible && TopLevel.GetTopLevel(this) is not Window { WindowState: WindowState.Minimized };
        var run = PresentationClock.ShouldRun(attached, visible, inViewport, UseSprites, AnimateActivity, SimulationRunning, Scene is { HasSession: true });
        if (!attached || !visible || !inViewport || !UseSprites || !AnimateActivity) { packages.Cancel(); queues.Cancel(); workers.Synchronize(animation.Seconds); }
        animation.SetRunning(run, monotonic.Elapsed);
        if (run || (attached && visible && inViewport && UseSprites && LaunchAnimating)) animationTimer.Start(); else animationTimer.Stop();
    }
    private void AnimationTick(object? sender, EventArgs e)
    {
        SynchronizeAnimation();
        if (!animation.IsRunning && !LaunchAnimating) { InvalidateVisual(); return; }
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
