namespace Simulation.Core;

/// <summary>Configuration is immutable; only domain logic can advance execution state.</summary>
public sealed class WorkItem
{
    private readonly List<StateTransition> transitions = [];
    private readonly List<WorkItemEvent> events = [];
    private readonly List<InspectionAttempt> attempts = [];
    private int reviewQueueDay, testingQueueDay, reworkQueueDay;
    private double currentReworkEffort;
    public IReadOnlyList<WorkItemEvent> Events => events.AsReadOnly();
    public IReadOnlyList<InspectionAttempt> InspectionAttempts => attempts.AsReadOnly();
    public double RemainingReworkEffort { get; private set; }
    public string Id { get; }
    public string Name { get; }
    public double DevelopmentEffort { get; }
    public double CodeReviewEffort { get; }
    public double TestingEffort { get; }
    public double RemainingDevelopmentEffort { get; private set; }
    public double RemainingCodeReviewEffort { get; private set; }
    public double RemainingTestingEffort { get; private set; }
    public IReadOnlyList<string> Dependencies { get; }
    public WorkItemStatus State { get; private set; } = WorkItemStatus.Backlog;
    public int CreatedDay { get; }
    public int? DevelopmentStartedDay { get; private set; }
    public int? DevelopmentCompletedDay { get; private set; }
    public int? CodeReviewStartedDay { get; private set; }
    public int? CodeReviewCompletedDay { get; private set; }
    public int? TestingStartedDay { get; private set; }
    public int? TestingCompletedDay { get; private set; }
    public int? DoneDay { get; private set; }
    public IReadOnlyList<StateTransition> Transitions => transitions.AsReadOnly();

    public WorkItem(string id, string name, double developmentEffort, double codeReviewEffort,
        double testingEffort, IEnumerable<string>? dependencies = null, int createdDay = 0)
    {
        Id = id;
        Name = name;
        DevelopmentEffort = RemainingDevelopmentEffort = developmentEffort;
        CodeReviewEffort = RemainingCodeReviewEffort = codeReviewEffort;
        TestingEffort = RemainingTestingEffort = testingEffort;
        Dependencies = Array.AsReadOnly((dependencies ?? []).ToArray());
        CreatedDay = createdDay;
    }

    internal WorkItem CopyForRun() => new(Id, Name, DevelopmentEffort, CodeReviewEffort,
        TestingEffort, Dependencies, CreatedDay);

    // Queue entry times remain stable while an item waits or receives partial work.
    internal int QueueEnteredDay(WorkItemStatus stage) => stage switch
    {
        WorkItemStatus.Development => DevelopmentStartedDay ?? CreatedDay,
        WorkItemStatus.CodeReview => reviewQueueDay,
        WorkItemStatus.Testing => testingQueueDay,
        WorkItemStatus.Rework => reworkQueueDay,
        _ => throw new ArgumentOutOfRangeException(nameof(stage))
    };

    internal void Enter(WorkItemStatus next, int day)
    {
        if (!WorkItemFlow.Allows(State, next)) throw new InvalidOperationException($"Cannot transition from {State} to {next}.");
        var previous = State;
        transitions.Add(new StateTransition(previous, next, day));
        events.Add(new(day, Id, WorkItemEventType.Transition, previous, next));
        State = next;
        switch (next)
        {
            case WorkItemStatus.Development: DevelopmentStartedDay = day; break;
            case WorkItemStatus.WaitingForCodeReview:
                if (previous == WorkItemStatus.Development) DevelopmentCompletedDay = day;
                reviewQueueDay = day; break;
            case WorkItemStatus.CodeReview:
                CodeReviewStartedDay ??= day;
                RemainingCodeReviewEffort = CodeReviewEffort;
                StartAttempt(DefectSource.CodeReview, day); break;
            case WorkItemStatus.WaitingForTesting: testingQueueDay = day; break;
            case WorkItemStatus.Testing:
                TestingStartedDay ??= day;
                RemainingTestingEffort = TestingEffort;
                StartAttempt(DefectSource.Testing, day); break;
            case WorkItemStatus.WaitingForRework: reworkQueueDay = day; break;
            case WorkItemStatus.Done: DoneDay = day; break;
        }
    }

    private void StartAttempt(DefectSource stage, int day) =>
        attempts.Add(new(stage, attempts.Count(a => a.Stage == stage) + 1, day));

    internal void CompleteStage(int day, DefectPolicy defects)
    {
        if (RemainingEffort != 0) throw new InvalidOperationException("Only finished work may complete a stage.");
        if (State is WorkItemStatus.Development or WorkItemStatus.Rework)
        {
            Enter(WorkItemStatus.WaitingForCodeReview, day);
            return;
        }
        var source = State == WorkItemStatus.CodeReview ? DefectSource.CodeReview : DefectSource.Testing;
        var effort = defects.Inspect(source);
        var index = attempts.FindLastIndex(a => a.Stage == source && a.CompletedDay is null);
        attempts[index] = attempts[index] with { CompletedDay = day, DefectFound = effort.HasValue };
        if (source == DefectSource.CodeReview) CodeReviewCompletedDay = day;
        else TestingCompletedDay = day;
        if (effort is double required)
        {
            currentReworkEffort = RemainingReworkEffort = required;
            events.Add(new(day, Id, WorkItemEventType.DefectFound, State, WorkItemStatus.WaitingForRework,
                DefectSource: source, RequiredReworkEffort: required));
            Enter(WorkItemStatus.WaitingForRework, day);
        }
        else Enter(source == DefectSource.CodeReview ? WorkItemStatus.WaitingForTesting : WorkItemStatus.Done, day);
    }

    internal double RemainingEffort => State switch
    {
        WorkItemStatus.Development => RemainingDevelopmentEffort,
        WorkItemStatus.CodeReview => RemainingCodeReviewEffort,
        WorkItemStatus.Testing => RemainingTestingEffort,
        WorkItemStatus.Rework => RemainingReworkEffort,
        _ => throw new InvalidOperationException("Only active stages can receive work.")
    };

    internal void ApplyWork(double work, int day)
    {
        if (!double.IsFinite(work) || work < 0 || work > 1 || work > RemainingEffort)
            throw new InvalidOperationException("Invalid work allocation.");
        if (work > 0) events.Add(new(day, Id, WorkItemEventType.CapacityApplied, State, State, work));
        switch (State)
        {
            case WorkItemStatus.Rework:
                RemainingReworkEffort = Subtract(RemainingReworkEffort, work, currentReworkEffort); break;
            case WorkItemStatus.Development:
                RemainingDevelopmentEffort = Subtract(RemainingDevelopmentEffort, work, DevelopmentEffort); break;
            case WorkItemStatus.CodeReview:
                RemainingCodeReviewEffort = Subtract(RemainingCodeReviewEffort, work, CodeReviewEffort); break;
            case WorkItemStatus.Testing:
                RemainingTestingEffort = Subtract(RemainingTestingEffort, work, TestingEffort); break;
        }
    }

    private static double Subtract(double remaining, double work, double initial)
    {
        var result = Math.Max(0, remaining - work);
        // Discard floating-point residue after actual work, never complete positive work for free.
        return work > 0 && result <= initial * 1e-12 ? 0 : result;
    }
}
