using Avalonia;
using Simulation.Sprites;
using Simulation.UI.ViewModels;
using static Simulation.Sprites.WorkshopFurniture;

namespace Simulation.UI.Controls.GameWorld;

public sealed record WorkshopWorker(WorkerVisual Visual, string Role, Point BadgePosition);
public sealed record WorkshopQueue(string Id, string Caption, int Count, Point Position, bool Active, int Limit = 4)
{
    public int Visible => Math.Min(Limit, Count);
    public int Grouped => Count - Visible;
}

/// <summary>Pure, bounded presentation projection. No item IDs, person assignments or mutable session reference.</summary>
public sealed class SpriteWorkshop
{
    public static readonly IsometricProjection Projection = new(new(540, 17));
    public static WorldPoint Ground(double x, double y) => new((x - 540) / 96 + (y - 17) / 48, (y - 17) / 48 - (x - 540) / 96);
    public IReadOnlyList<SpriteObject> Objects { get; }
    public IReadOnlyList<WorkshopWorker> Workers { get; }
    public IReadOnlyList<WorkshopQueue> Queues { get; }
    public GameSceneState State { get; }
    public WorkshopActivity Activity { get; }
    public const int MaximumDevelopers = 3;
    public const int MaximumTesters = 2;
    public int HiddenDevelopers => State.Developers - Workers.Count(w => w.Role != "T");
    public int HiddenTesters => State.Testers - Workers.Count(w => w.Role == "T");
    public bool IllustratesCollaboration => Workers.Count(w => w.Visual.State == SpritePose.Collaborating) == 2;

    public SpriteWorkshop(GameSceneState state)
    {
        State = state;
        Activity = WorkshopActivity.From(state);
        if (!state.HasSession) { Objects = []; Workers = []; Queues = []; return; }
        Queues =
        [
            new("backlog", "Backlog", state.Areas[0].Waiting, new(306, 253), false, 6),
            new("development", "Development", state.ActiveDevelopment, new(553, 211), true),
            new("review-active", "Code Review", state.Areas[2].Active, new(850, 292), true),
            new("review-wait", "Waiting for Code Review", state.Areas[2].Waiting, new(937, 282), false),
            new("testing-active", "Testing", state.Areas[3].Active, new(741, 444), true),
            new("testing-wait", "Waiting for Testing", state.Areas[3].Waiting, new(670, 471), false),
            new("ready", "Ready for Release", state.Areas[4].Waiting, new(448, 492), false, 6),
            new("rework-active", "Rework active", state.ActiveRework, new(612, 211), true, 2),
            new("rework-wait", "Rework waiting", state.WaitingRework, new(639, 225), false, 2),
            // Released today is an event sample, explicitly not another WIP queue.
            new("delivered", "Delivered today", state.ReleasedToday, new(284, 354), true, 3)
        ];
        var objects = new List<SpriteObject>();
        var workers = new List<WorkshopWorker>();
        const double scale = .34;
        var development = Ground(555, 165); var review = Ground(823, 256); var testing = Ground(710, 404);
        var seatedDev = Offset(development, -.86 * scale / .7, -.35 * scale / .7);
        var seatedTest = Offset(testing, -.86 * scale / .7, -.35 * scale / .7);
        var seatedReview = Offset(review, -.86 * scale / .7, -.35 * scale / .7);
        for (var i = 0; i < Math.Min(MaximumDevelopers, state.Developers); i++)
        {
            var pose = Activity.Developers[i];
            var position = i == 0 ? seatedDev : i == 1 ? Ground(501, 152) : seatedReview;
            var appearance = i == 1 ? StandingCompanion : SeatedDeveloper;
            var station = i == 2 ? "review" : "development";
            var worker = new WorkerVisual($"developer/{i}", appearance, pose, position, scale, station);
            var point = Projection.Project(position);
            workers.Add(new(worker, i < state.Specialists ? "S" : "D", new(point.X, point.Y + 9)));
            objects.AddRange(worker.Compose(i == 2 ? review : development));
        }
        for (var i = 0; i < Math.Min(MaximumTesters, state.Testers); i++)
        {
            var pose = Activity.Testers[i];
            var position = i == 0 ? seatedTest : Ground(778 + i * 29, 409 + i * 13);
            var worker = new WorkerVisual($"tester/{i}", i == 0 ? SeatedDeveloper : Developer, pose, position, scale, "testing");
            var point = Projection.Project(position);
            workers.Add(new(worker, "T", new(point.X, point.Y + 9)));
            objects.AddRange(worker.Compose(testing));
        }
        objects.AddRange(Workstation("development", development, scale, state.Developers > 0 ? seatedDev : null));
        objects.AddRange(Workstation("review", review, scale, state.Developers >= 3 ? seatedReview : null));
        objects.AddRange(Workstation("testing", testing, scale, state.Testers > 0 ? seatedTest : null));
        objects.Add(new("backlog/shelf", "shelf", Ground(307, 209), .34));
        objects.Add(new("ready/shelf", "shelf", Ground(449, 448), .34));
        objects.Add(new("release/van", "delivery-van", Ground(214, 350), .52));
        objects.Add(new("debt/maintenance", "maintenance-bench", Ground(450, 310), .30));
        objects.Add(new("plant/north", "plant", Ground(413, 118), .35));
        objects.Add(new("plant/south", "plant", Ground(350, 451), .35));
        foreach (var queue in Queues)
            for (var i = 0; i < queue.Visible; i++)
            {
                var column = i % 3; var row = i / 3;
                objects.Add(new($"queue/{queue.Id}/{i}", "package", Ground(queue.Position.X + column * 14 - 14,
                    queue.Position.Y + column * 7 - row * 15), .26, SortAnchor: Ground(queue.Position.X, queue.Position.Y + 10), StackOrder: row));
            }
        Objects = SpritePlacement.InDrawOrder(objects);
        Workers = workers.AsReadOnly();
    }
}
