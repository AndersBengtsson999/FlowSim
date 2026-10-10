namespace Simulation.Core;

public sealed record DependencySettings(double Rate = 0, double MeanWaitingDays = 0)
{
    public void Validate()
    {
        if (!double.IsFinite(Rate) || Rate < 0 || Rate > 1 || !double.IsFinite(MeanWaitingDays) || MeanWaitingDays < 0)
            throw new ScenarioValidationException("Dependency Rate must be 0–100% and Dependency Waiting Time must be finite and nonnegative.");
    }
    // Geometric number of whole waiting days (support 0,1,...), with the configured mean.
    // Saturation at int.MaxValue protects pathological means; resolution uses a long boundary.
    internal ResidualDependency? Assign(int arrival, IRandomSource random)
    {
        if (Rate == 0 || Rate < 1 && random.NextUnitDouble() >= Rate) return null;
        var sample = MeanWaitingDays == 0 ? 0 : Math.Floor(Math.Log(1 - random.NextUnitDouble()) / -Math.Log(1 + 1 / MeanWaitingDays));
        var days = sample >= int.MaxValue || !double.IsFinite(sample) ? int.MaxValue : (int)Math.Max(0, sample);
        return new(days, (long)arrival + days);
    }
}
public sealed record ResidualDependency(int WaitingDays, long ResolutionDay);
