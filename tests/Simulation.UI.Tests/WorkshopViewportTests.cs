using Avalonia;
using Simulation.UI.Controls.GameWorld;
using Xunit;

namespace Simulation.UI.Tests;

public sealed class WorkshopViewportTests
{
    private static WorkshopViewport Camera(double width = 1080, double height = 620)
    { var c = new WorkshopViewport(); c.Resize(new(width, height), new(1080, 620)); c.Fit(); return c; }
    private static void Near(Point a, Point b)
    { Assert.Equal(a.X, b.X, 8); Assert.Equal(a.Y, b.Y, 8); }
    [Theory] [InlineData(800, 500)] [InlineData(1500, 800)] [InlineData(350, 600)]
    public void FitCentersEntireWorldWithUniformScale(double width, double height)
    {
        var c = Camera(width, height);
        Near(new(width / 2, height / 2), c.ToScreen(new(540, 310)));
        var tl = c.ToScreen(default); var br = c.ToScreen(new(1080, 620));
        Assert.True(tl.X >= 0 && tl.Y >= 0 && br.X <= width + .001 && br.Y <= height + .001);
        Assert.False(c.IsManual); Assert.Equal(1, c.Zoom);
    }
    [Fact] public void ForwardInverseAndMatrixAgreeAfterZoomAndPan()
    {
        var c = Camera(900, 550); c.ZoomAt(2, new(500, 300)); c.Pan(new(100, -50));
        foreach (var p in new[] { new Point(0, 0), new(710, 404), new(1080, 620), new(-20, 800) })
        { Near(p, c.ToWorld(c.ToScreen(p))); Near(c.ToScreen(p), p.Transform(c.Matrix)); }
    }
    [Fact] public void FractionalWheelZoomPreservesPointerWorldPosition()
    {
        var c = Camera(); var pointer = new Point(710, 404); var world = c.ToWorld(pointer);
        for (var i = 0; i < 40; i++) c.ZoomAt(Math.Exp(.012), pointer);
        Near(world, c.ToWorld(pointer)); Assert.True(c.IsManual);
    }
    [Fact] public void ZoomLimitsAndNonFiniteInputAreSafe()
    {
        var c = Camera(); c.ZoomAt(100, new(540, 310)); Assert.Equal(2.5, c.Zoom);
        c.ZoomAt(.00001, new(540, 310)); Assert.Equal(.75, c.Zoom);
        c.ZoomAt(double.NaN, default); c.Pan(new(double.PositiveInfinity, 0)); Assert.True(double.IsFinite(c.Scale));
    }
    [Fact] public void PanIsScreenBasedAndAlwaysRecoverable()
    {
        var c = Camera(); c.ZoomAt(2, new(540, 310)); var before = c.ToScreen(new(710, 404));
        c.Pan(new(40, -30)); Near(before + new Vector(40, -30), c.ToScreen(new(710, 404)));
        foreach (var delta in new[] { new Vector(100000, 100000), new(-200000, -200000) })
        {
            c.Pan(delta); var a = c.ToScreen(default); var b = c.ToScreen(new(1080, 620));
            Assert.True(a.X < c.View.Width && b.X > 0 && a.Y < c.View.Height && b.Y > 0);
        }
        c.Fit(); Assert.Equal(1, c.Zoom); Assert.False(c.IsManual); Near(new(540, 310), c.ToScreen(new(540, 310)));
    }
    [Fact] public void ResizePreservesManualCenterAndRelativeZoomButRefitsDefault()
    {
        var c = Camera(); c.Resize(new(600, 500), c.World); Near(new(300, 250), c.ToScreen(new(540, 310)));
        c.ZoomAt(2, new(300, 250)); c.Pan(new(30, 10)); var center = c.ToWorld(new(300, 250));
        c.Resize(new(1200, 800), c.World); Near(center, c.ToWorld(new(600, 400))); Assert.Equal(2, c.Zoom);
        c.Resize(new(0, 0), c.World); Assert.Equal(new Size(1200, 800), c.View);
    }
    [Theory] [InlineData(.75)] [InlineData(1)] [InlineData(2)] [InlineData(2.5)]
    public void AllStationAndQueueHitsSurviveTransformAndResize(double zoom)
    {
        var c = Camera(800, 500); c.ZoomAt(zoom, new(400, 250)); c.Pan(new(40, 20)); c.Resize(new(1000, 650), c.World);
        foreach (var p in new[] { new Point(307, 209), new(555, 165), new(937, 282), new(670, 471), new(448, 492), new(284, 354), new(450, 310), new(20, 20) })
            Assert.Equal(StationMap.HitTest(p), StationMap.HitTest(c.ToWorld(c.ToScreen(p))));
    }
    [Fact] public void DragThresholdSuppressesClickAndDoesNotDependOnZoom()
    {
        var d = new WorkshopDrag(); d.Begin(new(20, 20), true);
        Assert.Equal(default, d.Move(new(23, 22))); Assert.True(d.End(new(23, 22)));
        d.Begin(new(20, 20), true); Assert.Equal(new Vector(10, 0), d.Move(new(30, 20)));
        Assert.True(d.Panning); Assert.Equal(new Vector(3, 4), d.Move(new(33, 24))); Assert.False(d.End(new(33, 24)));
        d.Begin(new(20, 20), false); Assert.Equal(default, d.Move(new(50, 50))); Assert.False(d.Panning); Assert.False(d.End(new(50, 50)));
        d.Begin(default, true); d.Cancel(); Assert.False(d.End(default));
    }
    [Theory] [InlineData(7)] [InlineData(12345)] [InlineData(9876)]
    public void CameraInputBetweenDaysPreservesSeededResultsHistoryAndCheckpoints(int seed)
    {
        var request = Simulation.Application.BaselineScenario.DefectsAndReworkExample() with { RandomSeed = seed };
        var a = Simulation.Application.LiveSimulation.Start(request, Simulation.Core.WorkArrivalMode.AlwaysAvailable);
        a.CreateCheckpoint("Camera invariant"); var b = Simulation.Application.LiveSimulation.Restore(a.Capture());
        var camera = Camera();
        for (var day = 0; day < 60; day++)
        {
            a.Step(); b.Step(); camera.ZoomAt(day % 2 == 0 ? 1.1 : 1 / 1.1, new(700, 400)); camera.Pan(new(5, -3));
            camera.Resize(new(800 + day, 500), camera.World);
            _ = StationMap.HitTest(camera.ToWorld(new(400, 300)));
            if (day % 10 == 0) camera.Fit();
        }
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(b.Capture()), System.Text.Json.JsonSerializer.Serialize(a.Capture()));
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(b.Session.GetResult()), System.Text.Json.JsonSerializer.Serialize(a.Session.GetResult()));
    }
}
