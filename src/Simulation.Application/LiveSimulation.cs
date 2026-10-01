using Simulation.Core;

namespace Simulation.Application;

public sealed record RollingMetrics(int Days, int Completed, double ThroughputPerFiveDays, double AverageWip,
    double DeveloperUtilization, double TesterUtilization)
{
    public static RollingMetrics From(IReadOnlyList<DailySnapshot> history, int window = 20)
    {
        if (window <= 0) throw new ArgumentOutOfRangeException(nameof(window));
        var days = history.TakeLast(window).ToArray();
        if (days.Length == 0) return new(0, 0, 0, 0, 0, 0);
        var before = history.Count > window ? history[history.Count - window - 1].DoneCount : 0;
        var completed = days[^1].DoneCount - before;
        static double Ratio(double used, double available) => available > 0 ? used / available : 0;
        return new(days.Length, completed, 5.0 * completed / days.Length, days.Average(d => d.TotalWip),
            Ratio(days.Sum(d => d.UsedDeveloperCapacity), days.Sum(d => d.AvailableDeveloperCapacity)),
            Ratio(days.Sum(d => d.UsedTesterCapacity), days.Sum(d => d.AvailableTesterCapacity)));
    }
}

public sealed record QueueInspection(int Count, int OldestDays, IReadOnlyList<string> ItemIds);

public sealed record LiveCheckpoint(Guid Id, string Label, SimulationSessionState State);
public sealed record LiveSessionDocument(int SchemaVersion, string SimulationModelVersion, SimulationSessionState State,
    IReadOnlyList<LiveCheckpoint> Checkpoints, int SafetyLimit = 10000, int RollingWindow = 20, string? OriginalModelVersion = null);

/// <summary>Session/checkpoint lifecycle and analysis; playback belongs to the UI.</summary>
public sealed class LiveSimulation
{
    public string OriginalModelVersion { get; private set; } = SimulationModel.Version;
    private readonly List<LiveCheckpoint> checkpoints = [];
    public SimulationSession Session { get; private set; }
    private int safetyLimit = 10000, rollingWindow = 20;
    public int SafetyLimit { get => safetyLimit; set => safetyLimit = value > 0 ? value : throw new ScenarioValidationException("Safety limit must be positive."); }
    public int RollingWindow { get => rollingWindow; set => rollingWindow = value > 0 ? value : throw new ScenarioValidationException("Rolling window must be positive."); }
    public bool LimitReached => Session.CurrentDay >= SafetyLimit;
    public IReadOnlyList<LiveCheckpoint> Checkpoints => checkpoints.AsReadOnly();
    public RollingMetrics Recent => RollingMetrics.From(Session.Days, RollingWindow);
    public DailySnapshot CurrentSnapshot => Session.Days.LastOrDefault() ?? new DailySnapshot(0, 0, 0, 0, 0, 0, 0, 0, 0,
        Session.WorkItems.Select(w => new WorkItemDaySnapshot(w.Id, w.State, w.RemainingDevelopmentEffort,
            w.RemainingCodeReviewEffort, w.RemainingTestingEffort, 0, 0, 0, w.CreatedDay, w.State, false)).ToArray(), 0, 0);
    public QueueInspection Inspect(WorkItemStatus state)
    {
        var items = Session.WorkItems.Where(w => w.State == state && w.CreatedDay <= Math.Max(0, Session.CurrentDay - 1)).ToArray();
        var oldest = items.Length == 0 ? 0 : items.Max(w => Session.CurrentDay - (w.Transitions.LastOrDefault(t => t.To == state)?.Day ?? w.CreatedDay));
        return new(items.Length, oldest, Array.AsReadOnly(items.Take(8).Select(w => w.Id).ToArray()));
    }
    public LiveSimulation(SimulationSession session) => Session = session;
    public bool Step()
    {
        if (LimitReached) return false;
        Session.AdvanceOneDay(); return true;
    }
    public LiveCheckpoint CreateCheckpoint(string label)
    {
        var checkpoint = new LiveCheckpoint(Guid.NewGuid(), string.IsNullOrWhiteSpace(label) ? $"Day {Session.CurrentDay}" : label.Trim(), Session.Capture());
        checkpoints.Add(checkpoint); return checkpoint;
    }
    public void RestoreCheckpoint(Guid id) => Session = SimulationSession.Restore(checkpoints.Single(c => c.Id == id).State);
    public void DeleteCheckpoint(Guid id) => checkpoints.RemoveAll(c => c.Id == id);
    public LiveSessionDocument Capture() => new(1, SimulationModel.Version, Session.Capture(), checkpoints.ToArray(), SafetyLimit, RollingWindow, OriginalModelVersion);
    public static LiveSimulation Restore(LiveSessionDocument document)
    {
        if (document.SchemaVersion != 1 || !SimulationModel.CanLoad(document.SimulationModelVersion))
            throw new ScenarioValidationException("Unsupported Live session schema or simulation model version.");
        if (document.SafetyLimit <= 0 || document.RollingWindow <= 0)
            throw new ScenarioValidationException("Safety limit and rolling window must be positive.");
        var live = new LiveSimulation(SimulationSession.Restore(document.State)) { SafetyLimit = document.SafetyLimit, RollingWindow = document.RollingWindow, OriginalModelVersion = document.OriginalModelVersion ?? document.SimulationModelVersion };
        foreach (var checkpoint in document.Checkpoints)
        {
            var copy = checkpoint with { State = SimulationSession.Restore(checkpoint.State).Capture() };
            if (live.checkpoints.Any(c => c.Id == copy.Id)) throw new ScenarioValidationException("Duplicate checkpoint ID.");
            live.checkpoints.Add(copy);
        }
        return live;
    }
    public static LiveSimulation Start(SimulationRequest request, WorkArrivalMode arrivalMode = WorkArrivalMode.ContinuousArrival, decimal rate = 0.8m)
    {
        var config = new SessionConfiguration(new(request.DeveloperCount, request.TesterCount, request.DeveloperCapacityPerDay, request.TesterCapacityPerDay) { DeveloperAvailability = request.DeveloperAvailability, TesterAvailability = request.TesterAvailability },
            request.DevelopmentWipLimit, request.CodeReviewWipLimit, request.TestingWipLimit)
        {
            Quality = request.Quality, Productivity = request.Productivity, ArrivalMode = arrivalMode, WorkItemsPerDay = rate,
            DevelopmentEffort = request.DevelopmentDistribution ?? new FixedEffort(request.DevelopmentEffort),
            CodeReviewEffort = request.CodeReviewDistribution ?? new FixedEffort(request.CodeReviewEffort),
            TestingEffort = request.TestingDistribution ?? new FixedEffort(request.TestingEffort)
        };
        return new(new SimulationSession(request.ToScenario(), config));
    }
    public static SimulationRequest Demo => new() { Name = "Live Flow Demo", NumberOfWorkItems = 0 };
}
