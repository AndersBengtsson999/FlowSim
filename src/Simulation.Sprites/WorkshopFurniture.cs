namespace Simulation.Sprites;

/// <summary>Reusable independent furniture and character layers; contains no simulation concepts.</summary>
public static class WorkshopFurniture
{
    public static readonly CharacterAppearance Developer = new("developer", new Dictionary<SpritePose, CharacterPose>
    {
        [SpritePose.Idle] = new("developer"),
        [SpritePose.Working] = new("developer-seated", true, "developer-seated-upper"),
        [SpritePose.Collaborating] = new("developer-seated", true, "developer-seated-upper")
    });
    public static readonly CharacterAppearance Colleague = new("colleague", new Dictionary<SpritePose, CharacterPose>
    { [SpritePose.Collaborating] = new("developer-collaborating", InFrontOfWorkstation: true) });
    public static WorldPoint Offset(WorldPoint p, double x, double y, double elevation = 0) => new(p.X + x, p.Y + y, p.Elevation + elevation);
    public static IEnumerable<SpriteObject> Workstation(string id, WorldPoint position, double scale, WorldPoint? occupiedSeat = null)
    {
        yield return new(id + "/desk", "desk", position, scale);
        yield return new(id + "/computer", "computer", position with { Elevation = position.Elevation + 2.83 * scale }, scale, StackOrder: 1);
        var seat = occupiedSeat ?? Offset(position, -.1 * scale / .7, 1.1 * scale / .7);
        yield return new(id + "/chair", "chair", Offset(seat, -.42 * scale / .7, .42 * scale / .7), scale, StackOrder: -1, SortAnchor: seat);
    }
}
