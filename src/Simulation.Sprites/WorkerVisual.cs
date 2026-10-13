namespace Simulation.Sprites;

// Appearance/state selection is presentation data, not simulation assignment or scheduling.
public sealed record CharacterPose(string AssetId, bool Seated = false, string? UpperLayer = null, bool InFrontOfWorkstation = false);
public sealed record CharacterAppearance(string Id, IReadOnlyDictionary<SpritePose, CharacterPose> Poses)
{
    public CharacterPose PoseFor(SpritePose pose) => Poses.TryGetValue(pose, out var visual) ? visual :
        throw new InvalidOperationException($"{Id} has no {pose} artwork.");
}
public sealed record WorkerVisual(string Id, CharacterAppearance Appearance, SpritePose State, WorldPoint Position, double Scale, string StationId)
{
    public CharacterPose Presentation => Appearance.PoseFor(State);
    public SpriteObject ToSprite() => new(Id, Presentation.AssetId, Position, Scale, Pose: State);
    public IEnumerable<SpriteObject> Compose(WorldPoint workstation)
    {
        yield return Presentation.InFrontOfWorkstation ? ToSprite() with { SortAnchor = workstation, StackOrder = 4 } : ToSprite();
        if (Presentation.UpperLayer is { } asset)
            yield return new(Id + "/upper-body", asset, Position, Scale, StackOrder: 3, Pose: State, SortAnchor: workstation);
    }
}
