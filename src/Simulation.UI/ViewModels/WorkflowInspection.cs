using Simulation.Core;

namespace Simulation.UI.ViewModels;

/// <summary>Detached presentation data. Durations use elapsed simulated-day boundaries, not inclusive counts.</summary>
public sealed record InspectedWorkItem(string Id, WorkItemStatus State, bool ResidualBlocked, string Description)
{
    public bool Expanded { get; set; }
    public string Heading => $"{Id} · {State}";
    public static InspectedWorkItem From(WorkItem item, int day, IReadOnlySet<string> completed, WorkItemDaySnapshot? observation = null)
    {
        var history = item.Transitions;
        var cursor = item.CreatedDay; var state = WorkItemStatus.Backlog; var waiting = 0; var valid = true;
        foreach (var transition in history)
        {
            if (transition.From != state || transition.Day < cursor || transition.Day > day) valid = false;
            if (IsWaiting(state)) waiting += transition.Day - cursor;
            cursor = transition.Day; state = transition.To;
        }
        valid &= state == item.State;
        if (IsWaiting(state)) waiting += day - cursor;
        var unresolved = item.Dependencies.Where(id => !completed.Contains(id)).ToArray();
        var residual = item.DependencyUnresolved(day);
        var dependency = residual ? "Residual dependency unresolved" : unresolved.Length > 0 ? "Unfinished prerequisites: " + string.Join(", ", unresolved) : "No unresolved dependency";
        var current = valid ? Math.Max(0, day - cursor).ToString() : "N/A";
        var wait = valid ? Math.Max(0, waiting).ToString() : "N/A";
        var rework = valid ? history.Count(t => t.To == WorkItemStatus.Rework).ToString() : "N/A";
        var work = observation is null ? "N/A" : (observation.DevelopmentWork + observation.CodeReviewWork + observation.TestingWork + observation.ReworkWork).ToString("0.##");
        return new(item.Id, item.State, residual,
            $"Size · Dev {item.DevelopmentEffort:0.##} / Review {item.CodeReviewEffort:0.##} / Test {item.TestingEffort:0.##} effort\n" +
            $"Current state: {current} days · Elapsed to delivery or now: {Math.Max(0, (item.DoneDay ?? day) - item.CreatedDay)} days\n" +
            $"Queue waiting: {wait} days · Rework entries: {rework}\nWork received last completed day: {work} effort\n{dependency}");
    }
    private static bool IsWaiting(WorkItemStatus state) => state is WorkItemStatus.WaitingForCodeReview or WorkItemStatus.WaitingForTesting or WorkItemStatus.WaitingForRework or WorkItemStatus.ReadyForRelease;
    public bool BelongsTo(string station) => station switch
    {
        "Backlog" => State == WorkItemStatus.Backlog,
        "Development" => State is WorkItemStatus.Development or WorkItemStatus.Rework or WorkItemStatus.WaitingForRework,
        "Code Review" => State is WorkItemStatus.CodeReview or WorkItemStatus.WaitingForCodeReview,
        "Testing" => State is WorkItemStatus.Testing or WorkItemStatus.WaitingForTesting,
        "Ready for Release" => State == WorkItemStatus.ReadyForRelease,
        "Release" => State is WorkItemStatus.Done or WorkItemStatus.Released,
        "Dependency queue" => State == WorkItemStatus.Backlog && ResidualBlocked,
        "Review queue" => State == WorkItemStatus.WaitingForCodeReview,
        "Testing queue" => State == WorkItemStatus.WaitingForTesting,
        "Rework queue" => State == WorkItemStatus.WaitingForRework,
        _ => false
    };
}
