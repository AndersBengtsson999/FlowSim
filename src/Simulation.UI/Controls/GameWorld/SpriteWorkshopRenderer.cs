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
    internal int DecodeCount => assets.DecodeCount;
    internal int ObjectCount => workshop?.Objects.Count ?? 0;
    internal void Render(DrawingContext dc, GameSceneState? state)
    {
        if (state is not { HasSession: true })
        {
            workshop = null; batch = null; labels.ClearLabels();
            labels.Text(dc, "Start a Live session to populate the workshop.", 540, 270, 24, centered: true);
            return;
        }
        if (!ReferenceEquals(workshop?.State, state))
        {
            workshop = new(state); batch = new(workshop.Objects, SpriteWorkshop.Projection, assets); labels.ClearLabels();
        }
        GameWorldLayout.Draw(dc, spriteDecoration: true);
        foreach (var queue in workshop!.Queues)
        {
            var p = queue.Position;
            Box(dc, p.X, p.Y + 8, 64, 30, 3, queue.Active ? "#547C70" : queue.Count > queue.Limit ? "#A88645" : "#7A7051", "#394B48", "#4F5D50");
        }
        // Loading bay outline and debt side area are fixtures, never extra simulation stages.
        dc.DrawLine(new Pen(Ink("#FFE36A"), 4), new(140, 367), new(205, 400));
        dc.DrawLine(new Pen(Ink("#FFE36A"), 4), new(205, 400), new(298, 353));
        dc.DrawLine(new Pen(Ink("#D49D7C"), 3, new DashStyle([3, 3], 0)), new(505, 269), new(450, 298));
        batch!.Draw(dc);
        foreach (var worker in workshop.Workers)
        {
            var p = worker.BadgePosition; var color = worker.Role == "T" ? "#72DCD6" : worker.Role == "S" ? "#FFE644" : "#CFDEE7";
            dc.DrawEllipse(Ink("#182D37"), new Pen(Ink(color), 1.5), p, 9, 9);
            labels.Text(dc, worker.Role, p.X, p.Y - 8, 12, color, true);
        }
        Title(dc, "BACKLOG", 307, 117, "#E9D9B8");
        Title(dc, "DEVELOPMENT", 555, 36, "#FFEB00");
        Title(dc, "CODE REVIEW", 817, 133, "#A8D5FA");
        Title(dc, "TESTING", 710, 309, "#8BE1C4");
        Title(dc, "READY FOR RELEASE", 449, 370, "#E9D9B8");
        Title(dc, "RELEASE", 214, 244, "#FFEB00");
        Title(dc, "TECHNICAL DEBT", 450, 200, "#FFD0A7");
        labels.Text(dc, $"{state.Debt:0.##} effort · {state.DebtRatio:P1}", 450, 329, 14, "#FFE4C9", true);
        labels.Text(dc, "Maintenance · side activity", 450, 347, 11, "#BFCAC8", true);
        Count(dc, $"{state.Areas[0].Count:N0} waiting", 306, 276, false);
        labels.Text(dc, $"Dependencies {state.DependencyWaiting:N0} (included)", 740, 640, 14, "#E6D2AB", true);
        Count(dc, $"{state.ActiveDevelopment:N0} active", 542, 233, true);
        labels.Text(dc, $"Rework {state.ActiveRework:N0} active / {state.WaitingRework:N0} wait", 650, 253, 12, "#EBD3A3", true);
        Count(dc, $"Active {state.Areas[2].Active:N0}", 823, 310, true);
        Count(dc, $"Waiting for Code Review · {state.Areas[2].Waiting:N0}", 929, 341, false);
        Count(dc, $"Active {state.Areas[3].Active:N0}", 773, 466, true);
        Count(dc, $"Waiting for Testing · {state.Areas[3].Waiting:N0}", 708, 499, false);
        Count(dc, $"{state.Areas[4].Count:N0} waiting", 449, 520, false);
        Count(dc, $"{state.Areas[5].Count:N0} delivered", 214, 398, true);
        labels.Text(dc, $"{state.ReleasedToday:N0} today · vehicle parked", 214, 422, 12, "#F4E6B2", true);
        labels.Text(dc, "Samples of today's deliveries", 290, 378, 10, "#DAE8DF", true);
        labels.Text(dc, $"TEAM   {state.Developers:N0} developers · {state.Specialists:N0} specialists included · {state.Testers:N0} testers", 35, 590, 18);
        labels.Text(dc, $"Figures: {workshop.Workers.Count(w => w.Role != "T")} Dev + {workshop.HiddenDevelopers:N0} grouped · {workshop.Workers.Count(w => w.Role == "T")} Test + {workshop.HiddenTesters:N0} grouped. Poses are illustrative.", 35, 617, 14, "#BECDDA");
        labels.Text(dc, state.ReleasePolicy, 35, 640, 14, "#BECDDA");
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
