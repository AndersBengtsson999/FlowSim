namespace Simulation.Core;

/// <summary>Observed raw capacity, equal-weight developer/tester units. Never used for allocation.</summary>
public sealed record DeliveryCost(double Development = 0, double CodeReview = 0, double Rework = 0,
    double Testing = 0, bool IsComplete = true)
{
    public double Total => Development + CodeReview + Rework + Testing;
    internal DeliveryCost Add(WorkItemStatus stage, double consumed) => stage switch
    {
        WorkItemStatus.Development => this with { Development = Development + consumed },
        WorkItemStatus.CodeReview => this with { CodeReview = CodeReview + consumed },
        WorkItemStatus.Rework => this with { Rework = Rework + consumed },
        WorkItemStatus.Testing => this with { Testing = Testing + consumed },
        _ => throw new InvalidOperationException("Only active item stages consume delivery capacity.")
    };
    internal void Validate()
    {
        if (new[] { Development, CodeReview, Rework, Testing, Total }.Any(v => !double.IsFinite(v) || v < 0))
            throw new ScenarioValidationException("Delivery cost must be finite and nonnegative.");
    }
}
