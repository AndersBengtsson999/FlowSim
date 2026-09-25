namespace Simulation.Core;

public enum DefectSource { CodeReview, Testing }
public enum WorkItemEventType { Transition, CapacityApplied, DefectFound }
public sealed record WorkItemEvent(int Day, string WorkItemId, WorkItemEventType EventType,
    WorkItemStatus FromState, WorkItemStatus ToState, double EffortApplied = 0,
    DefectSource? DefectSource = null, double? RequiredReworkEffort = null);
public sealed record InspectionAttempt(DefectSource Stage, int AttemptNumber, int StartedDay,
    int? CompletedDay = null, bool? DefectFound = null);

public sealed record DefectSettings
{
    public bool Enabled { get; init; }
    public double CodeReviewDefectProbability { get; init; }
    public double TestingDefectProbability { get; init; }
    public int ReworkWipLimit { get; init; } = 3;
    public IEffortDistribution CodeReviewReworkEffortDistribution { get; init; } = new FixedEffort(1);
    public IEffortDistribution TestingReworkEffortDistribution { get; init; } = new FixedEffort(2);

    public void Validate()
    {
        if (!Probability(CodeReviewDefectProbability) || !Probability(TestingDefectProbability))
            throw new ScenarioValidationException("Defect probabilities must be finite ratios between 0 and 1.");
        if (ReworkWipLimit <= 0) throw new ScenarioValidationException("Rework WIP limit must be positive.");
        if (CodeReviewReworkEffortDistribution is null || TestingReworkEffortDistribution is null)
            throw new ScenarioValidationException("Both Rework effort distributions are required.");
    }
    private static bool Probability(double p) => double.IsFinite(p) && p >= 0 && p <= 1;
}

/// <summary>Run-local streams independent of initial effort generation and each other.</summary>
internal sealed class DefectPolicy(DefectSettings settings, int seed)
{
    private readonly SeededRandom discovery = new(unchecked(seed ^ (int)0xD3FEC701));
    private readonly SeededRandom rework = new(unchecked(seed ^ (int)0xA11CE702));

    internal double? Inspect(DefectSource source)
    {
        if (!settings.Enabled) return null;
        var probability = source == DefectSource.CodeReview ? settings.CodeReviewDefectProbability : settings.TestingDefectProbability;
        if (probability == 0 || (probability < 1 && discovery.NextUnitDouble() >= probability)) return null;
        var effort = (source == DefectSource.CodeReview ? settings.CodeReviewReworkEffortDistribution : settings.TestingReworkEffortDistribution).Sample(rework);
        if (!double.IsFinite(effort) || effort < 0) throw new ScenarioValidationException("Sampled Rework effort must be finite and nonnegative.");
        return effort;
    }
}

public static class DeveloperCapacityPolicy
{
    public static IReadOnlyList<WorkItemStatus> Priority { get; } = Array.AsReadOnly(new[]
        { WorkItemStatus.CodeReview, WorkItemStatus.Rework, WorkItemStatus.Development });
}

internal static class WorkItemFlow
{
    internal static bool Allows(WorkItemStatus from, WorkItemStatus to) => (from, to) switch
    {
        (WorkItemStatus.Backlog, WorkItemStatus.Development) => true,
        (WorkItemStatus.Development or WorkItemStatus.Rework, WorkItemStatus.WaitingForCodeReview) => true,
        (WorkItemStatus.WaitingForCodeReview, WorkItemStatus.CodeReview) => true,
        (WorkItemStatus.CodeReview, WorkItemStatus.WaitingForTesting or WorkItemStatus.WaitingForRework) => true,
        (WorkItemStatus.WaitingForTesting, WorkItemStatus.Testing) => true,
        (WorkItemStatus.Testing, WorkItemStatus.Done or WorkItemStatus.WaitingForRework) => true,
        (WorkItemStatus.WaitingForRework, WorkItemStatus.Rework) => true,
        _ => false
    };
}
