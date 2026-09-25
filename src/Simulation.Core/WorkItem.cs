namespace Simulation.Core;

/// <summary>Configuration is immutable; only domain logic can advance execution state.</summary>
public sealed class WorkItem
{
    private readonly List<StateTransition> transitions = [];
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
        WorkItemStatus.CodeReview => DevelopmentCompletedDay!.Value,
        WorkItemStatus.Testing => CodeReviewCompletedDay!.Value,
        _ => throw new ArgumentOutOfRangeException(nameof(stage))
    };

    internal void Enter(WorkItemStatus next, int day)
    {
        var expected = State switch
        {
            WorkItemStatus.Backlog => WorkItemStatus.Development,
            WorkItemStatus.Development => WorkItemStatus.WaitingForCodeReview,
            WorkItemStatus.WaitingForCodeReview => WorkItemStatus.CodeReview,
            WorkItemStatus.CodeReview => WorkItemStatus.WaitingForTesting,
            WorkItemStatus.WaitingForTesting => WorkItemStatus.Testing,
            WorkItemStatus.Testing => WorkItemStatus.Done,
            _ => throw new InvalidOperationException("Done items cannot transition further.")
        };
        if (next != expected) throw new InvalidOperationException($"Cannot transition from {State} to {next}.");
        transitions.Add(new StateTransition(State, next, day));
        State = next;
        switch (next)
        {
            case WorkItemStatus.Development: DevelopmentStartedDay = day; break;
            case WorkItemStatus.WaitingForCodeReview: DevelopmentCompletedDay = day; break;
            case WorkItemStatus.CodeReview: CodeReviewStartedDay = day; break;
            case WorkItemStatus.WaitingForTesting: CodeReviewCompletedDay = day; break;
            case WorkItemStatus.Testing: TestingStartedDay = day; break;
            case WorkItemStatus.Done: TestingCompletedDay = DoneDay = day; break;
        }
    }

    internal double RemainingEffort => State switch
    {
        WorkItemStatus.Development => RemainingDevelopmentEffort,
        WorkItemStatus.CodeReview => RemainingCodeReviewEffort,
        WorkItemStatus.Testing => RemainingTestingEffort,
        _ => throw new InvalidOperationException("Only active stages can receive work.")
    };

    internal void ApplyWork(double work)
    {
        if (!double.IsFinite(work) || work < 0 || work > 1 || work > RemainingEffort)
            throw new InvalidOperationException("Invalid work allocation.");
        switch (State)
        {
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
