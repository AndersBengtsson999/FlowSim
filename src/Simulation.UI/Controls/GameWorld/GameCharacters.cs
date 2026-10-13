using Avalonia;
using Avalonia.Media;
using static Simulation.UI.Controls.GameWorld.GameDrawing;
namespace Simulation.UI.Controls.GameWorld;

internal enum WorkerAppearance { Developer, Specialist, Tester }
internal sealed record WorkerFigure(string Slot, Point Position, WorkerAppearance Appearance);
internal static class GameCharacters
{
    internal static void Draw(DrawingContext dc, WorkerFigure worker)
    {
        var (x, y) = worker.Position;
        var specialist = worker.Appearance == WorkerAppearance.Specialist;
        var tester = worker.Appearance == WorkerAppearance.Tester;
        var shirt = tester ? "#68CEAF" : "#7DADD9";
        dc.DrawEllipse(Ink("#485551"), null, new(x + 2, y + 23), 13, 5);
        // Boots, separate legs, sleeves, a turned face and badge remain legible at small scale.
        dc.DrawLine(new Pen(Ink("#283944"), 6), new(x - 4, y + 10), new(x - 5, y + 21));
        dc.DrawLine(new Pen(Ink("#344B59"), 6), new(x + 4, y + 10), new(x + 6, y + 21));
        dc.DrawEllipse(Ink("#192B33"), null, new(x - 6, y + 23), 5, 3);
        dc.DrawEllipse(Ink("#192B33"), null, new(x + 7, y + 23), 5, 3);
        dc.DrawRectangle(Ink(shirt), null, new(x - 10, y - 6, 20, 21), 6, 6);
        dc.DrawLine(new Pen(Ink(shirt), 6, lineCap: PenLineCap.Round), new(x - 9, y - 2), new(x - 14, y + 8));
        dc.DrawLine(new Pen(Ink(shirt), 6, lineCap: PenLineCap.Round), new(x + 8, y - 2), new(x + 14, y + 5));
        dc.DrawEllipse(Ink("#E9BB98"), null, new(x + 15, y + 5), 3, 3);
        dc.DrawEllipse(Ink("#E9BB98"), null, new(x, y - 15), 9, 10);
        dc.DrawEllipse(Ink("#F3CDA7"), null, new(x + 8, y - 13), 3, 3);
        dc.DrawRectangle(Ink(specialist ? "#FFEB00" : "#3D4142"), null, new(x - 10, y - 25, 19, 8), 4, 4);
        if (specialist) dc.DrawLine(new Pen(Ink("#E9C727"), 4), new(x + 2, y - 18), new(x + 13, y - 16));
        if (tester) dc.DrawLine(new Pen(Ink("#244451"), 2), new(x - 5, y - 15), new(x + 9, y - 13));
        dc.DrawRectangle(Ink("#F5EDD5"), null, new(x + 3, y, 4, 6));
    }
}
