namespace SpritePrototype;

public sealed record OfficeStation(string Id, string Caption, WorldPoint Position);

public static class OfficeScene
{
    public const double FurnitureScale = .70;
    public static readonly CharacterAppearance Developer = WorkshopFurniture.Developer;
    public static readonly CharacterAppearance Colleague = WorkshopFurniture.Colleague;
    public static IReadOnlyList<OfficeStation> Stations { get; } =
    [new("A", "Working", new(-3.5, 0)), new("B", "Waiting for work", new(3.1, -1.8)), new("C", "One task · two developers", new(3.5, 5))];

    public static IReadOnlyList<WorkerVisual> Workers { get; } =
    [
        new("worker-a", Developer, SpritePose.Working, Offset(Stations[0].Position, -.86, -.35), FurnitureScale, "A"),
        new("worker-b", Developer, SpritePose.Idle, Offset(Stations[1].Position, .1, 1.7), FurnitureScale, "B"),
        new("worker-c-driver", Developer, SpritePose.Collaborating, Offset(Stations[2].Position, -.86, -.35), FurnitureScale, "C"),
        new("worker-c-partner", Colleague, SpritePose.Collaborating, Offset(Stations[2].Position, -1.95, -.3), FurnitureScale, "C")
    ];
    public static IReadOnlyList<SpriteObject> Create()
    {
        var scene = new List<SpriteObject> { new("office-floor", "floor", new(0, 0), 1.45, Layer: 0) };
        foreach (var station in Stations)
        {
            var seated = Workers.FirstOrDefault(w => w.StationId == station.Id && w.Presentation.Seated);
            scene.AddRange(WorkshopFurniture.Workstation(station.Id, station.Position, FurnitureScale, seated?.Position));

        }
        scene.AddRange(Workers.SelectMany(w => w.Compose(Stations.Single(s => s.Id == w.StationId).Position)));
        scene.Add(new("plant-north", "plant", new(-4.4, -1.6), .85));
        scene.Add(new("plant-east", "plant", new(5.1, -2.9), .80));
        scene.Add(new("plant-west", "plant", new(-.4, 5.5), .75));
        scene.Add(new("a/task", "package", Offset(Stations[0].Position, 1.4, -.2, 2.83 * FurnitureScale), .48, StackOrder: 2));
        scene.Add(new("c/shared-task", "package", Offset(Stations[2].Position, 1.4, -.2, 2.83 * FurnitureScale), .48, StackOrder: 2));
        for (var i = 0; i < 3; i++) scene.Add(new($"incoming/{i}", "package", new(-2 + i * .7, 3.7), .65));
        return scene;
    }
    public static WorldPoint Offset(WorldPoint p, double x, double y, double elevation = 0) => new(p.X + x, p.Y + y, p.Elevation + elevation);
}
