namespace Simulation.Core;

/// <summary>Detached immutable output; contains no mutable execution objects.</summary>
public sealed record WorkItemResult(string Id, string Name, WorkItemStatus FinalState,
    int CreatedDay, int? DevelopmentStartedDay, int? DevelopmentCompletedDay,
    int? CodeReviewStartedDay, int? CodeReviewCompletedDay, int? TestingStartedDay,
    int? TestingCompletedDay, int? DoneDay,
    int ActiveTime, int WaitingForCodeReviewTime, int WaitingForTestingTime, int BlockedTime,
    double RemainingDevelopmentEffort, double RemainingCodeReviewEffort, double RemainingTestingEffort,
    IReadOnlyList<StateTransition> Transitions)
{
    // Compatibility with existing result consumers; both are immutable final state.
    public WorkItemStatus State => FinalState;
    public int? LeadTime => DoneDay - CreatedDay;
    public int? CycleTime => DoneDay - DevelopmentStartedDay;
    public int WaitingTime => WaitingForCodeReviewTime + WaitingForTestingTime;
}
