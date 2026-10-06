namespace Simulation.Core;

public enum WorkArrivalMode { FixedBacklog, ContinuousArrival, AlwaysAvailable }

public sealed record SessionConfiguration(Team Team, int DevelopmentWipLimit = 5,
    int CodeReviewWipLimit = 3, int TestingWipLimit = 3)
{
    public SkillSettings Skills { get; init; } = new();
    public TechnicalDebtSettings Debt { get; init; } = new();
    public StageProductivity Productivity { get; init; } = new();
    public DefectSettings Quality { get; init; } = new();
    public WorkArrivalMode ArrivalMode { get; init; } = WorkArrivalMode.FixedBacklog;
    public decimal WorkItemsPerDay { get; init; } = 0.8m;
    public IEffortDistribution DevelopmentEffort { get; init; } = new FixedEffort(5);
    public IEffortDistribution CodeReviewEffort { get; init; } = new FixedEffort(1);
    public IEffortDistribution TestingEffort { get; init; } = new FixedEffort(2);
    public void Validate()
    {
        ScenarioValidator.Validate(Scenario("Validation", 1, [], 0));
        if (!Enum.IsDefined(ArrivalMode) || WorkItemsPerDay < 0 || WorkItemsPerDay > 2000)
            throw new ScenarioValidationException("Arrival rate must be between 0 and 2,000 items per day.");
        if (DevelopmentEffort is null || CodeReviewEffort is null || TestingEffort is null)
            throw new ScenarioValidationException("All three arrival effort distributions are required.");
    }
    internal SimulationScenario Scenario(string name, int days, IReadOnlyList<WorkItem> items, int seed) =>
        new(name, days, Team, DevelopmentWipLimit, CodeReviewWipLimit, TestingWipLimit, items, seed) { Quality = Quality, Productivity = Productivity, Debt = Debt, Skills = Skills };
}

public sealed record ConfigurationChange(int Day, string? Label, SessionConfiguration Before, SessionConfiguration After);
public sealed record WorkItemState(string Id, string Name, double DevelopmentEffort, double CodeReviewEffort,
    double TestingEffort, IReadOnlyList<string> Dependencies, int CreatedDay, WorkItemStatus State,
    double RemainingDevelopmentEffort, double RemainingCodeReviewEffort, double RemainingTestingEffort,
    double RemainingReworkEffort, double CurrentReworkEffort, int ReviewQueueDay, int TestingQueueDay, int ReworkQueueDay,
    int? DevelopmentStartedDay, int? DevelopmentCompletedDay, int? CodeReviewStartedDay, int? CodeReviewCompletedDay,
    int? TestingStartedDay, int? TestingCompletedDay, int? DoneDay,
    IReadOnlyList<StateTransition> Transitions, IReadOnlyList<WorkItemEvent> Events, IReadOnlyList<InspectionAttempt> Attempts)
{
    public bool RequiresSpecialist { get; init; }
    public DeliveryCost? DeliveryCost { get; init; }
    public DevelopmentPlan? DevelopmentPlan { get; init; }
}

// Detached transport data. Restoring never aliases mutable item state or collections from this capture.
public sealed record SimulationSessionState(string Name, int RandomSeed, int CurrentDay,
    SessionConfiguration InitialConfiguration, SessionConfiguration Configuration,
    IReadOnlyList<WorkItemState> WorkItems, decimal ArrivalAccumulator, long NextWorkItemId,
    ulong ArrivalRandomState, ulong DiscoveryRandomState, ulong ReworkRandomState,
    IReadOnlyList<DailySnapshot> Days, IReadOnlyList<ConfigurationChange> Changes)
{
    public ulong? SkillRandomState { get; init; }
    public TechnicalDebtState? DebtState { get; init; }
}

/// <summary>A single evolving timeline. No clocks, UI, serialization or external services.</summary>
public sealed class SimulationSession
{
    private readonly List<WorkItem> items;
    private readonly Dictionary<string, WorkItem> byId;
    private readonly List<DailySnapshot> days = [];
    private readonly Dictionary<string, WorkItemDaySnapshot> latestObservations = new(StringComparer.Ordinal);
    private readonly List<ConfigurationChange> changes = [];
    private readonly DefectPolicy defects;
    private readonly TechnicalDebtLedger debt;
    public TechnicalDebtState DebtState => debt.State;
    private SeededRandom arrivalRandom;
    private SeededRandom skillRandom;
    private decimal accumulator;
    private long nextId = 1;
    public string Name { get; }
    public int RandomSeed { get; }
    // Number of completed day intervals; next interval is [CurrentDay, CurrentDay + 1).
    public int CurrentDay => days.Count;
    public SessionConfiguration InitialConfiguration { get; }
    public SessionConfiguration Configuration { get; private set; }
    public IReadOnlyList<DailySnapshot> Days => days.AsReadOnly();
    public IReadOnlyList<ConfigurationChange> Changes => changes.AsReadOnly();
    public IReadOnlyList<WorkItem> WorkItems => items.AsReadOnly();

    public SimulationSession(SimulationScenario scenario, SessionConfiguration? configuration = null)
    {
        ScenarioValidator.Validate(scenario);
        Name = scenario.Name; RandomSeed = scenario.RandomSeed;
        Configuration = configuration ?? new(scenario.Team, scenario.DevelopmentWipLimit, scenario.CodeReviewWipLimit,
            scenario.TestingWipLimit) { Skills = scenario.Skills, Quality = scenario.Quality, Productivity = scenario.Productivity, Debt = scenario.Debt, ArrivalMode = scenario.ArrivalMode, WorkItemsPerDay = scenario.WorkItemsPerDay,
                DevelopmentEffort = scenario.DevelopmentArrivalEffort, CodeReviewEffort = scenario.CodeReviewArrivalEffort, TestingEffort = scenario.TestingArrivalEffort };
        Configuration.Validate(); InitialConfiguration = Configuration;
        items = scenario.WorkItems.Select(w => w.CopyForRun()).ToList();
        byId = items.ToDictionary(w => w.Id, StringComparer.Ordinal);
        defects = new(Configuration.Quality, RandomSeed);
        debt = new(new());
        // Existing arrival stream also supplies shortcut decisions; defect/rework streams stay separate.
        arrivalRandom = new(unchecked(RandomSeed ^ (int)0xA771A150));
        skillRandom = new(unchecked(RandomSeed ^ (int)0x5A11C0DE));
        foreach (var item in items) Classify(item);
    }

    private SimulationSession(SimulationSessionState s)
    {
        ValidateState(s);
        Name = s.Name; RandomSeed = s.RandomSeed;
        InitialConfiguration = s.InitialConfiguration; Configuration = s.Configuration;
        items = s.WorkItems.Select(WorkItem.Restore).ToList();
        byId = items.ToDictionary(w => w.Id, StringComparer.Ordinal);
        days.AddRange(s.Days.Select(d => d with { Items = Array.AsReadOnly(d.Items.ToArray()) }));
        if (days.Count > 0) foreach (var item in days[^1].Items) latestObservations[item.Id] = item;
        changes.AddRange(s.Changes);
        accumulator = s.ArrivalAccumulator; nextId = s.NextWorkItemId;
        arrivalRandom = SeededRandom.Restore(s.ArrivalRandomState);
        skillRandom = s.SkillRandomState is { } state ? SeededRandom.Restore(state) : new(unchecked(RandomSeed ^ (int)0x5A11C0DE));
        debt = new(s.DebtState ?? new(0, items.Where(w => w.DevelopmentCompletedDay.HasValue).Sum(w => w.DevelopmentEffort)));
        debt.State.Validate();
        _ = debt.State.Overhead(Configuration.Debt);
        defects = new(Configuration.Quality, RandomSeed);
        defects.RestoreRandom(s.DiscoveryRandomState, s.ReworkRandomState);
    }

    public DailySnapshot AdvanceOneDay()
    {
        if (Configuration.ArrivalMode == WorkArrivalMode.ContinuousArrival)
        {
            accumulator += Configuration.WorkItemsPerDay;
            while (accumulator >= 1)
            {
                GenerateArrival(); accumulator -= 1;
            }
        }
        if (Configuration.ArrivalMode == WorkArrivalMode.AlwaysAvailable)
        {
            // Pull only enough to fill free Development slots; existing eligible backlog goes first.
            var active = items.Count(w => w.State == WorkItemStatus.Development);
            var eligible = items.Count(w => w.State == WorkItemStatus.Backlog && w.CreatedDay <= CurrentDay
                && w.Dependencies.All(id => byId[id].State == WorkItemStatus.Done));
            var needed = Math.Max(0, Configuration.DevelopmentWipLimit - active - eligible);
            for (var i = 0; i < needed; i++) GenerateArrival();
        }
        var day = SimulationEngine.AdvanceOneDay(Configuration.Scenario(Name, CurrentDay + 1, items, RandomSeed),
            items, byId, defects, CurrentDay, debt, arrivalRandom);
        // Reuse unchanged immutable observations (especially finished items) across days.
        var observations = day.Items.Select(w =>
        {
            if (latestObservations.TryGetValue(w.Id, out var prior) && prior == w) return prior;
            latestObservations[w.Id] = w; return w;
        }).ToArray();
        day = day with { Items = Array.AsReadOnly(observations) };
        days.Add(day);
        return day;
    }

    private void GenerateArrival()
    {
        string id;
        do { id = $"LIVE-{nextId++}"; } while (byId.ContainsKey(id));
        var item = new WorkItem(id, $"Live story {id[5..]}", Configuration.DevelopmentEffort.Sample(arrivalRandom),
            Configuration.CodeReviewEffort.Sample(arrivalRandom), Configuration.TestingEffort.Sample(arrivalRandom), createdDay: CurrentDay);
        if (new[] { item.DevelopmentEffort, item.CodeReviewEffort, item.TestingEffort }.Any(e => !double.IsFinite(e) || e < 0))
            throw new ScenarioValidationException("Arrival effort must be finite and nonnegative.");
        Classify(item);
        items.Add(item); byId.Add(id, item);
    }

    private void Classify(WorkItem item)
    {
        var rate = Configuration.Skills.SpecialistWorkRate;
        item.ClassifyDevelopment(rate == 1 || (rate > 0 && skillRandom.NextUnitDouble() < rate));
    }

    public void ApplyChanges(SessionConfiguration configuration, string? label = null)
    {
        configuration.Validate();
        _ = DebtState.Overhead(configuration.Debt);
        if (label?.Length > 100) throw new ScenarioValidationException("Change label must be at most 100 characters.");
        if (configuration == Configuration) return;
        changes.Add(new(CurrentDay, string.IsNullOrWhiteSpace(label) ? null : label.Trim(), Configuration, configuration));
        Configuration = configuration; defects.Configure(configuration.Quality);
    }

    public SimulationResult GetResult() => SimulationResultBuilder.Build(
        Configuration.Scenario(Name, CurrentDay, items, RandomSeed), items, days);

    public SimulationSessionState Capture() => new(Name, RandomSeed, CurrentDay, InitialConfiguration, Configuration,
        items.Select(w => w.Capture()).ToArray(), accumulator, nextId, arrivalRandom.State,
        defects.RandomState.Discovery, defects.RandomState.Rework,
        days.Select(d => d with { Items = Array.AsReadOnly(d.Items.ToArray()) }).ToArray(), changes.ToArray()) { DebtState = DebtState, SkillRandomState = skillRandom.State };
    public static SimulationSession Restore(SimulationSessionState state) => new(state);

    private static void ValidateState(SimulationSessionState s)
    {
        ArgumentNullException.ThrowIfNull(s);
        s.InitialConfiguration.Validate(); s.Configuration.Validate();
        s.DebtState?.Validate();
        if (s.CurrentDay < 0 || s.Days.Count != s.CurrentDay || s.ArrivalAccumulator < 0 || s.ArrivalAccumulator >= 1 || s.NextWorkItemId < 1)
            throw new ScenarioValidationException("Invalid day count or arrival continuation state.");
        var fresh = s.WorkItems.Select(w => new WorkItem(w.Id, w.Name, w.DevelopmentEffort, w.CodeReviewEffort, w.TestingEffort, w.Dependencies, w.CreatedDay)).ToArray();
        ScenarioValidator.Validate(s.Configuration.Scenario(s.Name, Math.Max(1, s.CurrentDay), fresh, s.RandomSeed));
        for (var i = 0; i < s.Days.Count; i++)
            if (s.Days[i].Day != i) throw new ScenarioValidationException("Daily history is not consecutive.");
        foreach (var w in s.WorkItems)
            if (!Enum.IsDefined(w.State) || new[] { w.RemainingDevelopmentEffort, w.RemainingCodeReviewEffort, w.RemainingTestingEffort, w.RemainingReworkEffort, w.CurrentReworkEffort }.Any(v => !double.IsFinite(v) || v < 0))
                throw new ScenarioValidationException($"Invalid execution state: {w.Id}.");
        foreach (var w in s.WorkItems) w.DeliveryCost?.Validate();
        foreach (var w in s.WorkItems)
            if (w.DevelopmentPlan is { } p && (p.BaseEffort != w.DevelopmentEffort
                || new[] { p.BaseEffort, p.DebtRatioAtStart, p.Overhead, p.EffortWithDebt, p.FinalEffort, p.SavedEffort, p.DebtToCreate }.Any(v => !double.IsFinite(v) || v < 0)))
                throw new ScenarioValidationException($"Invalid locked Development plan: {w.Id}.");
        foreach (var c in s.Changes)
        {
            if (c.Day < 0 || c.Day > s.CurrentDay) throw new ScenarioValidationException("Invalid intervention day.");
            c.Before.Validate(); c.After.Validate();
        }
    }
}
