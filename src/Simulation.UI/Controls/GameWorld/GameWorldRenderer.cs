using Avalonia.Media;
using Simulation.UI.ViewModels;
namespace Simulation.UI.Controls.GameWorld;

internal sealed class GameWorldRenderer
{
    private readonly GameDrawing labels = new();
    internal void ClearLabels() => labels.ClearLabels();
    internal void Render(DrawingContext dc, GameSceneState? scene)
    {
        if (scene is not { HasSession: true })
        {
            labels.Text(dc, "Start a Live session to populate the workshop.", 540, 270, 24, centered: true);
            return;
        }
        GameWorldLayout.Draw(dc);
        foreach (var station in GameWorldLayout.Stations.OrderBy(s => s.Position.Y))
        {
            GameWorkstations.Draw(dc, station, scene);
            var area = scene.Areas[station.AreaIndex]; var (x, y) = station.Position;
            labels.Text(dc, $"{station.AreaIndex + 1:00}  {area.Name.ToUpperInvariant()}", x, y - 89, 19, station.Accent, true);
            if (station.AreaIndex != 5) GamePackages.Draw(dc, labels, area, new(x, y + 45), station.AreaIndex is 0 or 4);
            else
            {
                labels.Text(dc, $"{area.Count:N0} internally released", x, y + 57, 20, "#FFEF86", true);
                labels.Text(dc, $"{scene.ReleasedToday:N0} today · internal release van", x, y + 82, 13, centered: true);
            }
        }
        GameWorkstations.Maintenance(dc);
        labels.Text(dc, "TECHNICAL DEBT", 505, 252, 17, "#FFD0A7", true);
        labels.Text(dc, $"Debt {scene.Debt:0.##} effort · {scene.DebtRatio:P1}", 505, 327, 15, "#FFE4C9", true);
        // Compact operational captions remain outside the floor instead of covering workstations.
        labels.Text(dc, $"TEAM   {scene.Developers:N0} developers · {scene.Specialists:N0} specialists · {scene.Testers:N0} testers", 35, 610, 18);
        labels.Text(dc, "Development · " + scene.Areas[1].Detail, 35, 636, 14, "#BECDDA");
        labels.Text(dc, scene.ReleasePolicy, 35, 658, 14, "#BECDDA");
        labels.Text(dc, scene.Areas[0].Detail, 805, 636, 14, "#BECDDA", true);
    }
}
