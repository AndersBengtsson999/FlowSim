namespace Simulation.Core;

/// <summary>v0.1 counts active states only. Waiting-state membership belongs here.</summary>
public static class WipPolicy
{
    public static bool CountsToward(WorkItemStatus state, WorkItemStatus stage) => state == stage;

    public static int Count(IEnumerable<WorkItem> items, WorkItemStatus stage) =>
        items.Count(item => CountsToward(item.State, stage));

    public static bool CanAdmit(IEnumerable<WorkItem> items, WorkItemStatus stage, SimulationScenario scenario) =>
        Count(items, stage) < Limit(stage, scenario);

    private static int Limit(WorkItemStatus stage, SimulationScenario scenario) => stage switch
    {
        WorkItemStatus.Development => scenario.DevelopmentWipLimit,
        WorkItemStatus.CodeReview => scenario.CodeReviewWipLimit,
        WorkItemStatus.Rework => scenario.Quality.ReworkWipLimit,
        WorkItemStatus.Testing => scenario.TestingWipLimit,
        _ => throw new ArgumentOutOfRangeException(nameof(stage), "WIP applies to active stages.")
    };
}
