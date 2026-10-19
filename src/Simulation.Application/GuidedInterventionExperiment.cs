using Simulation.Core;

namespace Simulation.Application;

public sealed record GuidedQueuePoint(int Day, int Count);
public sealed record GuidedBranch(PerformancePeriod Period, IReadOnlyList<GuidedQueuePoint> TestingQueue);
public sealed record GuidedComparison(int StartDay, int EndDay, int ObservedDays, int Window,
    int InitialQueue, GuidedBranch? Baseline, GuidedBranch? Intervention, string? InvalidReason)
{
    public bool Complete => InvalidReason is null && ObservedDays == Window && Baseline is not null;
}

/// <summary>Coordinates two existing sessions; never advances or draws randomness from LIVE.
/// Checkpoint day N is the shared starting observation; evaluation is N+1..N+window.</summary>
public sealed partial class GuidedInterventionExperiment
{
    private readonly SimulationSessionState checkpoint;
    private readonly SimulationSession live;
    private readonly int expectedChanges;
    private SimulationSession? baseline;
    public int StartDay => checkpoint.CurrentDay;
    public int Window { get; }
    public int EndDay => StartDay + Window;
    public int OriginalTesters => checkpoint.Configuration.Team.TesterCount;
    public int ProposedTesters => liveConfiguration.Team.TesterCount;
    private readonly SessionConfiguration liveConfiguration;
    public string? InvalidReason { get; private set; }
    public bool BaselineReady => baseline is not null;

    public GuidedInterventionExperiment(LiveCheckpoint checkpoint, SimulationSession live, int window)
    {
        if (window is not (10 or 20 or 40)) throw new ArgumentOutOfRangeException(nameof(window));
        this.checkpoint = checkpoint.State;
        this.live = live;
        liveConfiguration = live.Configuration;
        if (live.CurrentDay != StartDay || !SingleStaffingChange(this.checkpoint.Configuration, liveConfiguration))
            throw new ArgumentException("A guided comparison supports only one developer-count or tester-count change at the checkpoint day.");
        Window = window;
        expectedChanges = live.Changes.Count;
    }
    public static bool OnlyTesterCountChanged(SessionConfiguration before, SessionConfiguration after) =>
        after with { Team = after.Team with { TesterCount = before.Team.TesterCount } } == before;

    public int OriginalDevelopers => checkpoint.Configuration.Team.DeveloperCount;
    public int ProposedDevelopers => liveConfiguration.Team.DeveloperCount;
    public static bool SingleStaffingChange(SessionConfiguration before, SessionConfiguration after) =>
        OnlyTesterCountChanged(before, after) || after with { Team = after.Team with { DeveloperCount = before.Team.DeveloperCount } } == before;

    public Task PrepareBaselineAsync(CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        var isolated = SimulationSession.Restore(checkpoint);
        for (var i = 0; i < Window; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            isolated.AdvanceOneDay();
        }
        cancellationToken.ThrowIfCancellationRequested();
        baseline = isolated;
    }, cancellationToken);

    public void Invalidate(string reason) => InvalidReason ??= reason;
    public GuidedComparison Observe(SimulationSession? current)
    {
        if (!ReferenceEquals(current, live)) Invalidate("The LIVE session was reset, loaded or restored. Start a new comparison.");
        else if (current.CurrentDay < StartDay || current.Changes.Skip(expectedChanges).Any(c => c.Day < EndDay))
            Invalidate("Another intervention changed the evaluation window. Start a new comparison.");
        var elapsed = Math.Clamp((current?.CurrentDay ?? StartDay) - StartDay, 0, Window);
        var initial = checkpoint.Days.LastOrDefault()?.WaitingForTestingCount ??
            checkpoint.WorkItems.Count(w => w.State == WorkItemStatus.WaitingForTesting);
        GuidedBranch Branch(SimulationSession session) => new(
            LivePerformance.Period(session, StartDay + 1, StartDay + elapsed),
            new[] { new GuidedQueuePoint(StartDay, initial) }.Concat(session.Days
                .Where(d => d.Day >= StartDay && d.Day < StartDay + elapsed)
                .Select(d => new GuidedQueuePoint(d.Day + 1, d.WaitingForTestingCount))).ToArray());
        return new(StartDay, EndDay, elapsed, Window, initial,
            InvalidReason is null && elapsed > 0 && baseline is not null ? Branch(baseline) : null,
            InvalidReason is null && elapsed > 0 && baseline is not null ? Branch(live) : null, InvalidReason);
    }
}
