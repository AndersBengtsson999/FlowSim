using Avalonia;
using Simulation.Sprites;
using Simulation.UI.ViewModels;

namespace Simulation.UI.Controls.GameWorld;

public enum ReleasePhase { Loading, Departing, Away, Returning, FeedbackOnly }
public sealed record ReleaseVisual(int Count, int FirstDay, int LastDay, ReleasePhase Phase, double Offset, double LoadProgress)
{
    public string? CustomerVersion { get; init; }
    public string Caption => CustomerVersion is not null ? $"CUSTOMER LAUNCH · {CustomerVersion} · Day {LastDay} · {Count} items" : FirstDay == LastDay ? $"+{Count:N0} internally released · Day {FirstDay}" : $"+{Count:N0} internally released · Days {FirstDay}–{LastDay}";
    public static IReadOnlySet<string> ReplacedObjects { get; } = new HashSet<string> { "release/van", "queue/delivered/0", "queue/delivered/1", "queue/delivered/2" };
    public IReadOnlyList<SpriteObject> Sprites()
    {
        var result = new List<SpriteObject> { new("release/van", "delivery-van", SpriteWorkshop.Ground(214 - 100 * Offset, 350 + 50 * Offset), .52) };
        if (Phase == ReleasePhase.Loading)
            for (var i = 0; i < Math.Min(3, Count); i++)
            {
                var t = Math.Clamp((LoadProgress - i * .15) / .7, 0, 1);
                if (t >= 1) continue;
                result.Add(new($"loading/{i}", "package", SpriteWorkshop.Ground(284 + i * 9 - 38 * t, 354 + i * 5 - 32 * t), .26));
            }
        return result;
    }
}

// Part of the same B2.2 controller, using exactly the same elapsed clock and cancellation boundary.
public sealed partial class PackageMotion
{
    private int releaseCount, firstReleaseDay, lastReleaseDay;
    private double releaseStarted, loadingEnds;
    private bool feedbackOnly;
    private void ObserveRelease(GameSceneState old, GameSceneState next, double seconds, double speed)
    {
        // Both observations must agree. Intervals/configuration alone cannot create a delivery.
        if (old.Areas.Count < 6 || next.Areas.Count < 6 || next.ReleasedToday <= 0 ||
            next.Areas[5].Count - old.Areas[5].Count != next.ReleasedToday) return;
        if (Release(seconds) is null)
        {
            releaseStarted = seconds; firstReleaseDay = next.Day;
            feedbackOnly = speed > 2; loadingEnds = .85 / speed + .35;
            releaseCount = 0;
        }
        // Coalesce flow-based releases during a trip/feedback window, never enqueue another vehicle.
        releaseCount += next.ReleasedToday; lastReleaseDay = next.Day;
    }
    public ReleaseVisual? Release(double seconds)
    {
        if (releaseCount == 0) return null;
        var t = Math.Max(0, seconds - releaseStarted);
        if (t >= (feedbackOnly ? .8 : loadingEnds + 1.5)) { releaseCount = 0; return null; }
        if (feedbackOnly) return new(releaseCount, firstReleaseDay, lastReleaseDay, ReleasePhase.FeedbackOnly, 0, 0);
        var phase = t < loadingEnds ? ReleasePhase.Loading : t < loadingEnds + .6 ? ReleasePhase.Departing : t < loadingEnds + .9 ? ReleasePhase.Away : ReleasePhase.Returning;
        var offset = phase switch { ReleasePhase.Loading => 0, ReleasePhase.Departing => Ease((t - loadingEnds) / .6), ReleasePhase.Away => 1, _ => 1 - Ease((t - loadingEnds - .9) / .6) };
        return new(releaseCount, firstReleaseDay, lastReleaseDay, phase, offset, Math.Clamp((t - loadingEnds + .35) / .35, 0, 1));
    }
    private static double Ease(double t) { t = Math.Clamp(t, 0, 1); return t * t * (3 - 2 * t); }
}
