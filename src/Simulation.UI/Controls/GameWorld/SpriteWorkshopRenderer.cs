using Avalonia;
using Avalonia.Media;
using Simulation.Sprites;
using Simulation.UI.ViewModels;
using static Simulation.UI.Controls.GameWorld.GameDrawing;

namespace Simulation.UI.Controls.GameWorld;

internal sealed class SpriteWorkshopRenderer : IDisposable
{
    private readonly SpriteAssets assets = new();
    private readonly GameDrawing labels = new();
    private SpriteWorkshop? workshop;
    private SpriteBatch? batch;
    private WorkshopEffects? effects;
    internal int DecodeCount => assets.DecodeCount;
    internal int ObjectCount => workshop?.Objects.Count ?? 0;
    internal void Render(DrawingContext dc, GameSceneState? state, double seconds = 0, bool animateFrames = false, IReadOnlyList<SpriteObject>? moving = null, ReleaseVisual? release = null, IReadOnlyList<LivingWorker>? living = null, QueueStackMotion? queueMotion = null, string? selected = null, string? hovered = null, bool focused = false)
    {
        if (state is not { HasSession: true })
        {
            workshop = null; batch = null; effects = null; labels.ClearLabels();
            labels.Text(dc, "Start a Live session to populate the workshop.", 540, 270, 24, centered: true);
            return;
        }
        if (!ReferenceEquals(workshop?.State, state))
        {
            var next = new SpriteWorkshop(state);
            workshop = next; batch = new(workshop.Objects, SpriteWorkshop.Projection, assets);
            effects = new(workshop); labels.ClearLabels();
        }
        // A short 2:1 loading ramp grounds the vehicle outside the main floor during its trip.
        Poly(dc, "#34464B", new(165, 325), new(265, 375), new(120, 447), new(20, 397));
        dc.DrawLine(new Pen(Ink("#BAC4B7"), 2, new DashStyle([4, 5], 0)), new(214, 350), new(69, 422));
        GameWorldLayout.Draw(dc, spriteDecoration: true);
        StationMap.Highlight(dc, selected, hovered, focused);
        foreach (var queue in workshop!.Queues)
        {
            var p = queue.Position;
            var pressure = state.QueuePressures.GetValueOrDefault(queue.Id, QueuePressure.Normal);
            if (pressure.Kind != QueuePressureKind.Normal)
                dc.DrawEllipse(null, new Pen(Ink(pressure.Color), pressure.Kind == QueuePressureKind.Blocked ? 3 : 2), new(p.X, p.Y + 7), 42, 22);
            Box(dc, p.X, p.Y + 8, 64, 30, 3, queue.Active ? "#547C70" : queue.Aggregated ? "#A88645" : "#7A7051", "#394B48", "#4F5D50");
        }
        // Loading bay outline and debt side area are fixtures, never extra simulation stages.
        dc.DrawLine(new Pen(Ink("#FFE36A"), 4), new(140, 367), new(205, 400));
        dc.DrawLine(new Pen(Ink("#FFE36A"), 4), new(205, 400), new(298, 353));
        dc.DrawLine(new Pen(Ink("#D49D7C"), 3, new DashStyle([3, 3], 0)), new(505, 269), new(450, 298));
        if (living is null) effects!.DrawGround(dc, seconds);
        effects!.SetWorkers(living);
        var vehicle = release is { Phase: not ReleasePhase.FeedbackOnly };
        if (release is not null)
        {
            using (dc.PushOpacity(.35 + .2 * WorkshopEffects.Pulse(seconds, 1.5)))
                dc.DrawEllipse(null, new Pen(Ink("#FFEB00"), 3), new(261, 356), 42, 20);
        }
        var dynamicSprites = new List<SpriteObject>(moving ?? []);
        var hidden = new HashSet<string>();
        if (queueMotion is not null)
        {
            dynamicSprites.AddRange(queueMotion.Sample(seconds));
            hidden.UnionWith(queueMotion.Hidden);
        }
        if (vehicle) { dynamicSprites.AddRange(release!.Sprites()); hidden.UnionWith(ReleaseVisual.ReplacedObjects); }
        if (living is not null)
        {
            foreach (var worker in workshop.Workers) { hidden.Add(worker.Visual.Id); hidden.Add(worker.Visual.Id + "/upper-body"); }
            foreach (var worker in living) dynamicSprites.AddRange(WorkerMotion.Sprites(worker, seconds));
        }
        batch!.Draw(dc, seconds, animateFrames, effects, dynamicSprites, hidden);
        foreach (var queue in workshop.Queues.Where(q => !q.Active))
        {
            var pressure = state.QueuePressures.GetValueOrDefault(queue.Id, QueuePressure.Normal);
            if (pressure.Marker.Length > 0)
            {
                var p = new Point(queue.Position.X - 34, queue.Position.Y + 12);
                // Shape plus glyph remains legible with motion disabled and without color perception.
                if (pressure.Kind == QueuePressureKind.Blocked)
                    Poly(dc, pressure.Color, new(p.X, p.Y - 12), new(p.X - 11, p.Y + 8), new(p.X + 11, p.Y + 8));
                else dc.DrawRectangle(Ink(pressure.Color), null, new Rect(p.X - 10, p.Y - 10, 20, 20), 3, 3);
                labels.Text(dc, pressure.Marker, p.X, p.Y - 9, 15, "#182D37", true);
            }

        }
        foreach (var worker in workshop.Workers)
        {
            var actor = living?.FirstOrDefault(w => w.Worker.Visual.Id == worker.Visual.Id);
            var p = actor is null ? worker.BadgePosition : SpriteWorkshop.Projection.Project(actor.Position) + new Vector(0, 9);
            var color = worker.Role == "T" ? "#72DCD6" : worker.Role == "S" ? "#FFE644" : "#CFDEE7";
            dc.DrawEllipse(Ink("#182D37"), new Pen(Ink(color), 1.5), p, 9, 9);
            labels.Text(dc, worker.Role, p.X, p.Y - 8, 12, color, true);
        }
        Title(dc, "BACKLOG", 307, 104, "#E9D9B8");
        Title(dc, "DEVELOPMENT", 555, 36, "#FFEB00");
        Title(dc, "CODE REVIEW", 817, 133, "#A8D5FA");
        Title(dc, "TESTING", 710, 292, "#8BE1C4");
        if (state.QueuePressures.GetValueOrDefault("testing-wait", QueuePressure.Normal).Kind == QueuePressureKind.Blocked)
            labels.Text(dc, "INACTIVE", 710, 314, 11, "#FFC27A", true);
        Title(dc, "READY FOR RELEASE", 449, 370, "#E9D9B8");
        Title(dc, "RELEASE", 214, 244, "#FFEB00");
        Title(dc, "TECHNICAL DEBT", 432, 205, "#FFD0A7");
        Count(dc, $"Debt · {state.Debt:0.##}", 424, 335, false);
        Count(dc, $"{state.Areas[0].Count:N0} waiting{Grouped(state.Areas[0].Count)}", 307, 128, false);
        Count(dc, $"Dev WIP · {state.ActiveDevelopment:N0}", 555, 240, true);
        Count(dc, $"Queue · {state.Areas[2].Waiting:N0}{Grouped(state.Areas[2].Waiting)}", 933, 343, false);
        Status(dc, state, "review-wait", 938, 367);
        Count(dc, $"Queue · {state.Areas[3].Waiting:N0}{Grouped(state.Areas[3].Waiting)}", 708, 499, false);
        Status(dc, state, "testing-wait", 708, 523);
        Count(dc, $"{state.Areas[4].Count:N0} waiting{Grouped(state.Areas[4].Count)}", 449, 520, false);
        Status(dc, state, "ready", 449, 544);
        Count(dc, $"{state.Areas[5].Count:N0} delivered", 214, 415, true);
        if (release is not null) Count(dc, release.Caption, 170, 470, true);
    }
    private static string Grouped(int count) => count > WorkshopQueue.StackCount(count) ? " · grouped" : "";
    private void Status(DrawingContext dc, GameSceneState state, string id, double x, double y)
    {
        var pressure = state.QueuePressures.GetValueOrDefault(id, QueuePressure.Normal);
        if (pressure.Label.Length == 0) return;
        var caption = pressure.Kind == QueuePressureKind.Blocked ? "Blocked · " + pressure.Label : pressure.Label;
        var width = caption.Length * 6.2 + 14;
        dc.DrawRectangle(Ink("#182D37"), new Pen(Ink(pressure.Color), 1), new Rect(x - width / 2, y - 1, width, 20), 4, 4);
        labels.Text(dc, caption, x, y, 12, pressure.Color, true);
    }
    private void Title(DrawingContext dc, string text, double x, double y, string color)
    {
        labels.Text(dc, text, x + 1, y + 1, 17, "#172128", true);
        labels.Text(dc, text, x, y, 17, color, true);
    }
    private void Count(DrawingContext dc, string text, double x, double y, bool active)
    {
        // Opaque backing preserves readability over the continuous floor and walkways.
        var width = text.Length * 7.4 + 14;
        dc.DrawRectangle(Ink("#1A3035"), null, new Rect(x - width / 2, y - 1, width, 21), 5, 5);
        labels.Text(dc, text, x, y, 14, active ? "#99E8CD" : "#FFDF99", true);
    }
    public void Dispose() => assets.Dispose();
}
