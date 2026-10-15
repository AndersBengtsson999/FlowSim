using Simulation.Sprites;
using Simulation.UI.ViewModels;

namespace Simulation.UI.Controls.GameWorld;

/// <summary>Aggregate stack dissolve; no item identity, journey or fabricated arrival event.</summary>
public sealed class QueueStackMotion
{
    private SpriteWorkshop? previous;
    private readonly List<(SpriteObject Sprite, bool Added)> changes = [];
    private double started;
    public IReadOnlySet<string> Hidden { get; private set; } = new HashSet<string>();
    public int Count => changes.Count;
    public void Cancel() { changes.Clear(); Hidden = new HashSet<string>(); }
    public void Observe(GameSceneState? state, double seconds, bool animate, double speed)
    {
        if (ReferenceEquals(previous?.State, state)) return;
        var old = previous;
        previous = state is { HasSession: true } ? new(state) : null;
        Cancel();
        if (!animate || speed > 2 || previous is null || old is null || old.State.SessionIdentity != state!.SessionIdentity || state.Day != old.State.Day + 1) return;
        var before = old.Queues.Where(q => !q.Active).SelectMany(q => q.Sprites()).ToDictionary(x => x.Id);
        var after = previous.Queues.Where(q => !q.Active).SelectMany(q => q.Sprites()).ToDictionary(x => x.Id);
        foreach (var item in after.Values) if (!before.ContainsKey(item.Id)) changes.Add((item, true));
        foreach (var item in before.Values) if (!after.ContainsKey(item.Id)) changes.Add((item, false));
        Hidden = changes.Select(x => x.Sprite.Id).ToHashSet(); started = seconds;
    }
    public IReadOnlyList<SpriteObject> Sample(double seconds)
    {
        if (changes.Count == 0) return [];
        var t = Math.Clamp((seconds - started) / .35, 0, 1);
        if (t >= 1) { Cancel(); return []; }
        return changes.Select(x => x.Sprite with { Opacity = x.Added ? t : 1 - t }).ToArray();
    }
}
