using Avalonia;
namespace Simulation.Sprites;

public static class SpriteCatalog
{
    // Curated source rectangles preserve the generated PNGs/alpha; no runtime trimming or downloads.
    public static IReadOnlyDictionary<string, SpriteDefinition> Assets { get; } = new Dictionary<string, SpriteDefinition>
    {
        ["developer-gestures"] = new("developer-gestures", new Uri("avares://Simulation.Sprites/Assets/developer-gestures.png"), .28,
            new Dictionary<SpritePose, SpriteClip>
            {
                [SpritePose.Idle] = new([new(new(0, 0, 887, 887), new(490, 820))]),
                [SpritePose.Collaborating] = GestureClip(),
                [SpritePose.Maintaining] = GestureClip()
            }),
        ["shelf"] = Asset("shelf", new(85, 180, 1110, 960), new(560, 735), .30),
        ["delivery-van"] = Asset("delivery-van", new(35, 155, 1200, 975), new(565, 735), .25),
        ["maintenance-bench"] = Asset("maintenance-bench", new(130, 10, 985, 1210), new(480, 1000), .30),
        ["developer-seated-upper"] = new("developer-seated-upper", new Uri("avares://Simulation.Sprites/Assets/developer-seated.png"), .155,
            new[] { SpritePose.Idle, SpritePose.Working, SpritePose.Collaborating, SpritePose.Reviewing, SpritePose.Testing }
                .ToDictionary(p => p, _ => new SpriteClip([new(new(260, 20, 765, 780), new(540, 1180))]))),
        ["chair"] = Asset("chair", new(245, 135, 775, 1050), new(385, 905), .135),
        ["developer-seated"] = Asset("developer-seated", new(260, 20, 765, 1270), new(540, 1180), .155, SpritePose.Idle, SpritePose.Working, SpritePose.Collaborating, SpritePose.Reviewing, SpritePose.Testing),
        ["developer-collaborating"] = Asset("developer-collaborating", new(225, 115, 725, 1340), new(275, 1235), .167, SpritePose.Collaborating),
        ["floor"] = Asset("floor", new(20, 260, 1220, 820), new(608, 370), .62),
        ["desk"] = Asset("desk", new(140, 225, 980, 890), new(488, 685), .34),
        ["computer"] = Asset("computer", new(200, 185, 815, 960), new(420, 735), .17),
        ["developer"] = Asset("developer", new(420, 70, 450, 1130), new(215, 1040), .21, SpritePose.Idle, SpritePose.Collaborating),
        ["package"] = Asset("package", new(290, 295, 680, 740), new(338, 595), .10),
        ["plant"] = Asset("plant", new(280, 195, 735, 820), new(345, 765), .18)
    };
    public static WorldPoint DeveloperPosition { get; } = new(.1, 1.65);
    public static IReadOnlyList<SpriteObject> Demo { get; } =
    [
        new("floor", "floor", new(0, 0), Layer: 0),
        new("plant", "plant", new(2.3, -1.8)),
        new("desk", "desk", new(0, 0)),
        new("computer", "computer", new(0, 0, 2.83), StackOrder: 1),
        new("developer", "developer", DeveloperPosition),
        new("package", "package", new(2.2, 1.2))
    ];
    private static SpriteClip GestureClip()
    {
        var neutral = new SpriteFrame(new(0, 0, 887, 887), new(490, 820));
        var pointing = new SpriteFrame(new(887, 0, 887, 887), new(414, 820));
        return new([neutral, neutral, neutral, pointing, pointing, neutral], 1.5);
    }
    private static SpriteDefinition Asset(string id, Rect source, Point anchor, double scale, params SpritePose[] poses) => new(id,
        new Uri($"avares://Simulation.Sprites/Assets/{id}.png"), scale,
        (poses.Length == 0 ? new[] { SpritePose.Idle } : poses).ToDictionary(p => p, _ => new SpriteClip([new(source, anchor)])));
}
