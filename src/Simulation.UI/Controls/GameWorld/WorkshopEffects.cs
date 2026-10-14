using Avalonia;
using Avalonia.Media;
using Simulation.Sprites;
using static Simulation.UI.Controls.GameWorld.GameDrawing;

namespace Simulation.UI.Controls.GameWorld;

/// <summary>Small, original vector overlays on existing independent sprites. No simulated events or limb deformation.</summary>
internal sealed class WorkshopEffects : ISpriteDecoration
{
    private sealed class Monitor
    {
        internal required SpritePose Pose;
        internal required StreamGeometry Screen;
        internal required (Point A, Point B)[] Rows;
        internal required Point[] Keys;
        internal required Point[] Indicators;
        internal required Pen Pen;
        internal required IBrush Color;
        internal void Draw(DrawingContext dc, double seconds)
        {
            using (dc.PushOpacity(Pose == SpritePose.Idle ? .65 : .55)) dc.DrawGeometry(Ink("#0B2233"), null, Screen);
            if (Pose == SpritePose.Idle) return;
            var period = Pose == SpritePose.Reviewing ? 4.8 : Pose == SpritePose.Testing ? 3.6 : 3;
            for (var i = 0; i < Rows.Length; i++)
            {
                // Soft traveling emphasis, never a completion bar, pass/fail result or item count.
                using (dc.PushOpacity(.24 + .5 * Pulse(seconds, period, i * .21)))
                    dc.DrawLine(Pen, Rows[i].A, Rows[i].B);
            }
            if (Pose == SpritePose.Testing)
                for (var i = 0; i < Indicators.Length; i++)
                    using (dc.PushOpacity(.3 + .5 * Pulse(seconds, 3.6, i * .25)))
                        dc.DrawEllipse(Color, null, Indicators[i], 1.3, 1.3);
            if (Pose is SpritePose.Working or SpritePose.Collaborating or SpritePose.Testing)
                for (var i = 0; i < Keys.Length; i++)
                    using (dc.PushOpacity(.1 + .25 * Pulse(seconds, 1.4, i * .32)))
                        dc.DrawEllipse(Color, null, Keys[i], 1.4, .75);
        }
    }
    private readonly Dictionary<string, Monitor> monitors = new(StringComparer.Ordinal);
    private readonly Point[] idleAnchors;
    private readonly bool repayment;
    private readonly Pen maintenance = new(Ink("#DCA377"), 1.5);
    internal WorkshopEffects(SpriteWorkshop scene)
    {
        idleAnchors = scene.Workers.Where(w => w.Visual.State == SpritePose.Idle)
            .Select(w => SpriteWorkshop.Projection.Project(w.Visual.Position)).ToArray();
        repayment = scene.Activity.RepaymentObserved;
        foreach (var computer in scene.Objects.Where(o => o.AssetId == "computer"))
        {
            var station = computer.Id.Split('/')[0];
            var worker = scene.Workers.FirstOrDefault(w => w.Visual.StationId == station && w.Visual.Presentation.Seated);
            var pose = worker?.Visual.State ?? SpritePose.Idle;
            var definition = SpriteCatalog.Assets["computer"];
            var destination = SpritePlacement.Destination(computer, definition, SpriteWorkshop.Projection);
            var source = definition.Frame(SpritePose.Idle, 0).Source;
            var scale = computer.Scale * definition.PixelsToDip;
            Point Pixel(double x, double y) => new(destination.X + (x - source.X) * scale, destination.Y + (y - source.Y) * scale);
            var a = Pixel(367, 275); var b = Pixel(930, 533); var d = Pixel(367, 555); var c = Pixel(930, 833);
            Point Screen(double x, double y) => a + (b - a) * x + (d - a) * y;
            var screen = new StreamGeometry();
            using (var path = screen.Open()) { path.BeginFigure(a, true); path.LineTo(b); path.LineTo(c); path.LineTo(d); path.EndFigure(true); }
            var color = Ink(pose switch { SpritePose.Reviewing => "#C3B8ED", SpritePose.Testing => "#73DCD2", SpritePose.Collaborating => "#EACB66", _ => "#81C8DD" });
            monitors[computer.Id] = new()
            {
                Pose = pose, Screen = screen, Color = color, Pen = new(color, 1.3, lineCap: PenLineCap.Round),
                Rows = Enumerable.Range(0, 4).Select(i => (Screen(.14, .20 + i * .18), Screen(.6 + (i % 2) * .2, .20 + i * .18))).ToArray(),
                Indicators = [Screen(.84, .22), Screen(.84, .45), Screen(.84, .68)],
                Keys = [Pixel(365, 837), Pixel(480, 890), Pixel(540, 945)]
            };
        }
    }
    internal static double Pulse(double seconds, double period, double phase = 0) => .5 - .5 * Math.Cos(2 * Math.PI * (seconds / period + phase));
    internal void DrawGround(DrawingContext dc, double seconds)
    {
        // Low-contrast idle presence; the body remains anchored and never looks as if it is typing.
        for (var i = 0; i < idleAnchors.Length; i++)
            using (dc.PushOpacity(.06 + .05 * Pulse(seconds, 4.8, i * .19)))
                dc.DrawEllipse(Ink("#C7DEDB"), null, idleAnchors[i], 11, 4);
    }
    public void DrawAfterSprite(DrawingContext context, string id, double seconds)
    {
        if (monitors.TryGetValue(id, out var monitor)) monitor.Draw(context, seconds);
        if (id == "debt/maintenance" && repayment)
        {
            // An examination diagram, not a completion percentage or a fictional debt work item.
            using (context.PushOpacity(.45 + .35 * Pulse(seconds, 3.5)))
            {
                context.DrawLine(maintenance, new(446, 257), new(464, 266));
                context.DrawLine(maintenance, new(464, 266), new(477, 258));
                context.DrawLine(maintenance, new(477, 258), new(489, 264));
                context.DrawEllipse(null, maintenance, new(464, 266), 3, 2);
                var x = 449 + 30 * Pulse(seconds, 3.5);
                context.DrawEllipse(Ink("#FFE59B"), null, new(x, 281 + (x - 449) * .48), 2.4, 1.7);
            }
        }
    }
}
