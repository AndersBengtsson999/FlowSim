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
    private HashSet<string> growing = [];
    internal int DecodeCount => assets.DecodeCount;
    internal int ObjectCount => workshop?.Objects.Count ?? 0;
    internal void Render(DrawingContext dc, GameSceneState? state, double seconds = 0, bool animateFrames = false, IReadOnlyList<SpriteObject>? moving = null, ReleaseVisual? release = null)
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
            growing = workshop is { } old && old.State.SessionIdentity == state.SessionIdentity && old.State.Day + 1 == state.Day
                ? next.Queues.Where(q => !q.Active && q.Count > old.Queues.First(x => x.Id == q.Id).Count).Select(q => q.Id).ToHashSet()
                : [];
            workshop = next; batch = new(workshop.Objects, SpriteWorkshop.Projection, assets);
            effects = new(workshop); labels.ClearLabels();
        }
        // A short 2:1 loading ramp grounds the vehicle outside the main floor during its trip.
        Poly(dc, "#34464B", new(165, 325), new(265, 375), new(120, 447), new(20, 397));
        dc.DrawLine(new Pen(Ink("#BAC4B7"), 2, new DashStyle([4, 5], 0)), new(214, 350), new(69, 422));
        GameWorldLayout.Draw(dc, spriteDecoration: true);
        foreach (var queue in workshop!.Queues)
        {
            var p = queue.Position;
            if (growing.Contains(queue.Id)) dc.DrawEllipse(null, new Pen(Ink("#E7BB59"), 2), new(p.X, p.Y + 7), 43, 21);
            Box(dc, p.X, p.Y + 8, 64, 30, 3, queue.Active ? "#547C70" : queue.Count > queue.Limit ? "#A88645" : "#7A7051", "#394B48", "#4F5D50");
        }
        // Loading bay outline and debt side area are fixtures, never extra simulation stages.
        dc.DrawLine(new Pen(Ink("#FFE36A"), 4), new(140, 367), new(205, 400));
        dc.DrawLine(new Pen(Ink("#FFE36A"), 4), new(205, 400), new(298, 353));
        dc.DrawLine(new Pen(Ink("#D49D7C"), 3, new DashStyle([3, 3], 0)), new(505, 269), new(450, 298));
        effects!.DrawGround(dc, seconds);
        var vehicle = release is { Phase: not ReleasePhase.FeedbackOnly };
        if (release is not null)
        {
            using (dc.PushOpacity(.35 + .2 * WorkshopEffects.Pulse(seconds, 1.5)))
                dc.DrawEllipse(null, new Pen(Ink("#FFEB00"), 3), new(261, 356), 42, 20);
        }
        batch!.Draw(dc, seconds, animateFrames, effects,
            vehicle ? (moving ?? []).Concat(release!.Sprites()).ToArray() : moving,
            vehicle ? ReleaseVisual.ReplacedObjects : null);
        foreach (var worker in workshop.Workers)
        {
            var p = worker.BadgePosition; var color = worker.Role == "T" ? "#72DCD6" : worker.Role == "S" ? "#FFE644" : "#CFDEE7";
            dc.DrawEllipse(Ink("#182D37"), new Pen(Ink(color), 1.5), p, 9, 9);
            labels.Text(dc, worker.Role, p.X, p.Y - 8, 12, color, true);
        }
        Title(dc, "BACKLOG", 307, 117, "#E9D9B8");
        Title(dc, "DEVELOPMENT", 555, 36, "#FFEB00");
        Title(dc, "CODE REVIEW", 817, 133, "#A8D5FA");
        Title(dc, "TESTING", 710, 292, "#8BE1C4");
        Title(dc, "READY FOR RELEASE", 449, 370, "#E9D9B8");
        Title(dc, "RELEASE", 214, 244, "#FFEB00");
        Title(dc, "TECHNICAL DEBT", 432, 205, "#FFD0A7");
        labels.Text(dc, $"{state.Debt:0.##} effort · {state.DebtRatio:P1}", 424, 327, 14, "#FFE4C9", true);
        labels.Text(dc, $"Repayment {state.UsedDebtRepaymentCapacity:0.##} · last day", 424, 347, 11, "#BFCAC8", true);
        Count(dc, $"{state.Areas[0].Count:N0} waiting", 200, 193, false);
        Count(dc, $"Dependency wait {state.DependencyWaiting:N0}", 200, 217, false);
        Count(dc, $"{state.ActiveDevelopment:N0} active", 542, 233, true);
        labels.Text(dc, $"Rework {state.ActiveRework:N0} active / {state.WaitingRework:N0} wait", 650, 276, 12, "#EBD3A3", true);
        Count(dc, $"Active {state.Areas[2].Active:N0}", 823, 310, true);
        Count(dc, $"Review queue · {state.Areas[2].Waiting:N0}", 948, 365, false);
        Count(dc, $"Active {state.Areas[3].Active:N0}", 773, 466, true);
        Count(dc, $"Test queue · {state.Areas[3].Waiting:N0}", 708, 499, false);
        Count(dc, $"{state.Areas[4].Count:N0} waiting", 449, 520, false);
        Count(dc, $"{state.Areas[5].Count:N0} delivered", 150, 465, true);
        labels.Text(dc, $"{state.ReleasedToday:N0} released today", 150, 490, 12, "#F4E6B2", true);
        if (release is not null) Count(dc, release.Caption, 170, 515, true);
        labels.Text(dc, "Loading samples", 290, 378, 10, "#DAE8DF", true);
        labels.Text(dc, $"TEAM   {state.Developers:N0} developers · {state.Specialists:N0} specialists included · {state.Testers:N0} testers", 35, 590, 18);
        labels.Text(dc, $"Figures: {workshop.Workers.Count(w => w.Role != "T")} Dev + {workshop.HiddenDevelopers:N0} grouped · {workshop.Workers.Count(w => w.Role == "T")} Test + {workshop.HiddenTesters:N0} grouped. Poses are illustrative.", 35, 617, 14, "#BECDDA");
        labels.Text(dc, state.ReleasePolicy, 35, 640, 14, "#BECDDA");
        labels.Text(dc, $"{state.ChangedItems:N0} items changed stage today · up to 12 animated · gold ring = queue grew", 740, 640, 10, "#BECDDA", true);
        labels.Text(dc, $"Collaboration capacity last day: {state.CollaborationCapacity:0.##}" + (workshop.IllustratesCollaboration ? " · pair is illustrative, no named assignment" : " · no pair shown"), 35, 662, 13, "#BECDDA");
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
