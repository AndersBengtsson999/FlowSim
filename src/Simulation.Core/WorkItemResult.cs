namespace Simulation.Core;

/// <summary>Detached immutable output; contains no mutable execution objects.</summary>
public sealed record WorkItemResult(string Id, string Name, WorkItemStatus FinalState,
    int CreatedDay, int? DevelopmentStartedDay, int? DevelopmentCompletedDay,
    int? CodeReviewStartedDay, int? CodeReviewCompletedDay, int? TestingStartedDay,
    int? TestingCompletedDay, int? DoneDay,
    int ActiveTime, int WaitingForCodeReviewTime, int WaitingForTestingTime, int BlockedTime,
    double RemainingDevelopmentEffort, double RemainingCodeReviewEffort, double RemainingTestingEffort,
    IReadOnlyList<StateTransition> Transitions,
    double DevelopmentEffort, double CodeReviewEffort, double TestingEffort)
{
    public IReadOnlyList<WorkItemEvent> Events { get; init; } = Array.Empty<WorkItemEvent>();
    public IReadOnlyList<InspectionAttempt> InspectionAttempts { get; init; } = Array.Empty<InspectionAttempt>();
    public int CodeReviewDefectsFound { get; init; }
    public int TestingDefectsFound { get; init; }
    public int DefectsFound => CodeReviewDefectsFound + TestingDefectsFound;
    public bool EverRequiredRework => DefectsFound > 0;
    public int ReworkCount { get; init; }
    public double TotalReworkEffort { get; init; }
    public double RequiredReworkEffort { get; init; }
    public double RemainingReworkEffort { get; init; }
    public int ReworkActiveTime { get; init; }
    public int WaitingForReworkTime { get; init; }
    public int CodeReviewAttempts { get; init; }
    public int TestingAttempts { get; init; }
    public int? FirstCodeReviewStartedDay => CodeReviewStartedDay;
    public int? FirstTestingStartedDay => TestingStartedDay;
    // Compatibility with existing result consumers; both are immutable final state.
    public WorkItemStatus State => FinalState;
    public int? LeadTime => DoneDay - CreatedDay;
    public int? CycleTime => DoneDay - DevelopmentStartedDay;
    public int WaitingTime => WaitingForCodeReviewTime + WaitingForTestingTime + WaitingForReworkTime;
}
