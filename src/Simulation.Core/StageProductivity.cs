namespace Simulation.Core;

/// <summary>Effective stage work per consumed capacity unit. Independent of team capacity and availability.</summary>
public sealed record StageProductivity(double Development = 1, double CodeReview = 1, double Testing = 1)
{
    public static StageProductivity Default { get; } = new();
    public void Validate()
    {
        if (new[] { Development, CodeReview, Testing }.Any(v => !double.IsFinite(v) || v <= 0))
            throw new ScenarioValidationException("Stage productivity must be finite and greater than zero.");
    }
}

/// <summary>Observed consumption, distinct from effective work; Rework retains its existing field.</summary>
public sealed record StageCapacity(double Development, double CodeReview, double Testing);
