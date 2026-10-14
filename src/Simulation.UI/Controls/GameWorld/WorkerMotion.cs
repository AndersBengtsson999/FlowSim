using Avalonia;
using Simulation.Sprites;
using Simulation.UI.ViewModels;
using static Simulation.UI.Controls.GameWorld.SpriteWorkshop;

namespace Simulation.UI.Controls.GameWorld;

public sealed record LivingWorker(WorkshopWorker Worker, SpritePose Pose, WorldPoint Position, string Station,
    bool Walking, int Facing, int Frame, bool Ambient);

/// <summary>Bounded visual choreography, never an employee assignment model. Reads immutable aggregate snapshots only.</summary>
public sealed class WorkerMotion
{
    private sealed class Actor(WorkshopWorker worker, int slot, double now)
    {
        public WorkshopWorker Worker = worker;
        public readonly int Slot = slot;
        public string Location = "home";
        public string Destination = "home";
        public WorldPoint Position = Home(slot);
        public WorldPoint[] Path = [];
        public double Started;
        public double Duration;
        public double NextAmbient = now + 14 + slot * 3.7;
        public double RestUntil;
        public bool Ambient;
        public int Facing;
        public int Frame;
    }
    private readonly List<Actor> actors = [];
    private GameSceneState? previous;
    private double lastTime;
    public int MovingCount => actors.Count(a => a.Path.Length > 0);
    public int Count => actors.Count;
    public static WorldPoint Home(int slot) => slot switch
    {
        0 => Ground(467, 194), 1 => Ground(610, 176), 2 => Ground(752, 276),
        3 => Ground(376, 329), 4 => Ground(623, 415), _ => Ground(807, 422)
    };
    private static WorldPoint Station(string station) => station switch
    {
        "development" => Ground(555, 165), "review" => Ground(823, 256), "testing" => Ground(710, 404),
        "partner" => Ground(501, 152), "debt" => Ground(403, 305), _ => throw new ArgumentException(station)
    };
    private static WorldPoint Seat(string station) => station is "partner" or "debt" ? Station(station) :
        WorkshopFurniture.Offset(Station(station), -.86 * .34 / .7, -.35 * .34 / .7);
    private static string Target(Actor a) => a.Worker.Visual.State switch
    {
        SpritePose.Idle => "home", SpritePose.Reviewing => "review", SpritePose.Testing => "testing",
        SpritePose.Maintaining => "debt", SpritePose.Collaborating when a.Slot == 1 => "partner", _ => "development"
    };
    // Each station has a dedicated chair-side approach. The central aisle is shared by ONE walker.
    private static WorldPoint[] Access(string location, int slot) => location switch
    {
        "home" => slot switch
        {
            0 => [Home(slot), Ground(490, 240), Ground(520, 360)],
            1 => [Home(slot), Ground(680, 195), Ground(710, 230), Ground(735, 330), Ground(650, 360)],
            2 => [Home(slot), Ground(735, 330), Ground(650, 360)],
            3 => [Home(slot), Ground(395, 365), Ground(490, 360)],
            4 => [Home(slot), Ground(610, 360)],
            _ => [Home(slot), Ground(820, 380), Ground(735, 330), Ground(650, 360)]
        },
        "development" => [Seat(location), Ground(520, 177), Ground(490, 195), Ground(490, 240), Ground(520, 360)],
        "partner" => [Seat(location), Ground(470, 165), Ground(430, 185), Ground(430, 225), Ground(490, 240), Ground(520, 360)],
        "review" => [Seat(location), Ground(780, 225), Ground(745, 250), Ground(735, 330), Ground(650, 360)],
        "testing" => [Seat(location), Ground(670, 385), Ground(635, 405), Ground(610, 360)],
        "debt" => [Seat(location), Ground(380, 320), Ground(395, 365), Ground(490, 360)],
        _ => throw new ArgumentException(location)
    };
    public void Observe(GameSceneState? scene, double seconds, bool animate, double speed = 1)
    {
        if (scene is not { HasSession: true }) { actors.Clear(); previous = scene; return; }
        var discontinuous = previous is null || previous.SessionIdentity != scene.SessionIdentity || scene.Day < previous.Day || scene.Day > previous.Day + 1;
        if (ReferenceEquals(previous, scene)) return;
        Advance(seconds, false);
        var workers = new SpriteWorkshop(scene).Workers;
        actors.RemoveAll(a => workers.All(w => w.Visual.Id != a.Worker.Visual.Id));
        foreach (var worker in workers)
        {
            var actor = actors.FirstOrDefault(a => a.Worker.Visual.Id == worker.Visual.Id);
            if (actor is null)
            {
                var slot = worker.Role == "T" ? 4 + int.Parse(worker.Visual.Id.Split('/')[1]) : int.Parse(worker.Visual.Id.Split('/')[1]);
                actors.Add(actor = new(worker, slot, seconds));
            }
            actor.Worker = worker;
        }
        previous = scene;
        if (discontinuous || !animate || speed > 2) Synchronize(seconds);
    }
    public void Synchronize(double seconds)
    {
        foreach (var a in actors)
        {
            a.Location = a.Destination = Target(a); a.Position = At(a, a.Location); a.Path = [];
            a.Ambient = false; a.RestUntil = 0; a.NextAmbient = seconds + 14 + a.Slot * 3.7;
        }
        lastTime = seconds;
    }
    private static WorldPoint At(Actor a, string location) => location == "home" ? Home(a.Slot) : Seat(location);
    public IReadOnlyList<LivingWorker> Sample(double seconds, bool allowMovement = true)
    {
        Advance(seconds, allowMovement);
        return actors.Select(a => new LivingWorker(a.Worker,
            a.Path.Length > 0 ? SpritePose.Walking : a.Location == Target(a) && !a.Ambient ? a.Worker.Visual.State : SpritePose.Idle,
            a.Position, a.Location, a.Path.Length > 0, a.Facing, a.Frame, a.Ambient)).ToArray();
    }
    private void Advance(double seconds, bool allowMovement)
    {
        if (seconds < lastTime) Synchronize(seconds);
        lastTime = seconds;
        foreach (var a in actors.Where(a => a.Path.Length > 0))
        {
            var progress = Math.Clamp((seconds - a.Started) / a.Duration, 0, 1);
            // Ease departure/arrival without slowing down at each intermediate waypoint.
            var eased = progress * progress * (3 - 2 * progress);
            a.Position = Interpolate(a.Path, eased, out var facing); a.Facing = facing;
            a.Frame = (int)((seconds - a.Started) * 5) % 2;
            if (progress >= 1)
            {
                a.Path = []; a.Location = a.Destination; a.Position = At(a, a.Location);
                a.RestUntil = seconds + (a.Ambient ? 2.5 : .45);
            }
        }
        if (!allowMovement || MovingCount > 0) return;
        // Latest desired location only; no simulation-day queue and no independent production scheduling.
        foreach (var a in actors)
        {
            if (seconds < a.RestUntil) continue;
            var target = Target(a);
            if (a.Location != target) { Begin(a, target, seconds); return; }
            a.Ambient = false;
        }
        foreach (var a in actors.OrderBy(a => a.NextAmbient))
        {
            if (seconds < a.NextAmbient || seconds < a.RestUntil) continue;
            a.NextAmbient = seconds + 23 + a.Slot * 2.9;
            // Brief stand-up/return for occupied stations. Idle workers gesture at their own home.
            if (a.Location != "home") { a.Ambient = true; Begin(a, "home", seconds); return; }
        }
    }
    private static void Begin(Actor a, string destination, double seconds)
    {
        var source = Access(a.Location, a.Slot); var target = Access(destination, a.Slot);
        var sourceEnd = source.Length; var targetEnd = target.Length;
        for (var i = 0; i < source.Length; i++)
        {
            var common = Array.IndexOf(target, source[i]);
            if (common < 0) continue;
            sourceEnd = i + 1; targetEnd = common; break;
        }
        var points = source.Take(sourceEnd).Concat(target.Take(targetEnd).Reverse()).ToArray();
        points[0] = a.Position;
        a.Path = points; a.Destination = destination; a.Started = seconds;
        a.Duration = Math.Max(.5, Length(points) / 70);
    }
    private static double Distance(WorldPoint a, WorldPoint b)
    {
        var d = Projection.Project(a) - Projection.Project(b);
        return Math.Sqrt(d.X * d.X + d.Y * d.Y);
    }
    private static double Length(IReadOnlyList<WorldPoint> path) => Enumerable.Range(1, path.Count - 1).Sum(i => Distance(path[i - 1], path[i]));
    public static WorldPoint Interpolate(IReadOnlyList<WorldPoint> path, double progress, out int facing)
    {
        var remaining = Length(path) * Math.Clamp(progress, 0, 1); facing = 0;
        for (var i = 1; i < path.Count; i++)
        {
            var length = Distance(path[i - 1], path[i]);
            if (remaining <= length || i == path.Count - 1)
            {
                var delta = Projection.Project(path[i]) - Projection.Project(path[i - 1]);
                facing = delta.Y >= 0 ? delta.X >= 0 ? 0 : 1 : delta.X < 0 ? 2 : 3;
                var t = length < .001 ? 1 : remaining / length;
                return new(path[i - 1].X + (path[i].X - path[i - 1].X) * t, path[i - 1].Y + (path[i].Y - path[i - 1].Y) * t);
            }
            remaining -= length;
        }
        return path[0];
    }
    public static IEnumerable<SpriteObject> Sprites(LivingWorker worker, double seconds)
    {
        var visual = worker.Worker.Visual;
        if (worker.Walking)
        {
            yield return new(visual.Id, "developer-walk-" + worker.Facing, worker.Position, visual.Scale, Pose: SpritePose.Walking, FrameIndex: worker.Frame);
            yield break;
        }
        var standing = worker.Pose == SpritePose.Idle || worker.Station is "partner" or "debt";
        visual = visual with { Position = worker.Position, State = worker.Pose,
            Appearance = standing ? WorkshopFurniture.StandingCompanion : WorkshopFurniture.SeatedDeveloper };
        if (standing && worker.Pose == SpritePose.Idle && seconds > 0 &&
            (seconds + int.Parse(visual.Id.Split('/')[1]) * 4.3) % 19 > 17)
            visual = visual with { State = SpritePose.Collaborating };
        foreach (var sprite in visual.Compose(standing ? worker.Position : Station(worker.Station)))
        {
            var clip = SpriteCatalog.Assets[sprite.AssetId].Clips[sprite.Pose];
            yield return sprite with { FrameIndex = SpriteFrameSampling.Index(clip.Frames.Count, clip.FramesPerSecond, seconds + int.Parse(visual.Id.Split('/')[1]) * .7) };
        }
    }
}
