namespace Simulation.Core;

public interface IRandomSource
{
    double NextUnitDouble();
}

/// <summary>SplitMix64 with explicit seed; stable algorithm, independent of System.Random/runtime versions.</summary>
public sealed class SeededRandom(int seed) : IRandomSource
{
    private ulong state = unchecked((uint)seed);

    public double NextUnitDouble()
    {
        unchecked
        {
            state += 0x9E3779B97F4A7C15UL;
            var z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            return (z >> 11) * (1.0 / 9007199254740992.0);
        }
    }
}

public interface IEffortDistribution
{
    double Sample(IRandomSource random);
}

public sealed record FixedEffort : IEffortDistribution
{
    public double Effort { get; }
    public FixedEffort(double effort)
    {
        if (!double.IsFinite(effort) || effort < 0)
            throw new ScenarioValidationException("Fixed effort must be finite and nonnegative.");
        Effort = effort;
    }
    public double Sample(IRandomSource random) => Effort;
}

public sealed record TriangularEffort : IEffortDistribution
{
    public double Minimum { get; }
    public double MostLikely { get; }
    public double Maximum { get; }
    public TriangularEffort(double minimum, double mostLikely, double maximum)
    {
        if (!double.IsFinite(minimum) || !double.IsFinite(mostLikely) || !double.IsFinite(maximum)
            || minimum <= 0 || minimum > mostLikely || mostLikely > maximum)
            throw new ScenarioValidationException("Triangular effort requires finite values with 0 < Minimum <= Most Likely <= Maximum.");
        Minimum = minimum; MostLikely = mostLikely; Maximum = maximum;
    }
    public double Sample(IRandomSource random)
    {
        if (Minimum == Maximum) return Minimum;
        var u = random.NextUnitDouble();
        if (!double.IsFinite(u) || u < 0 || u >= 1)
            throw new ArgumentOutOfRangeException(nameof(random), "Random samples must be in [0, 1).");
        var width = Maximum - Minimum;
        var split = (MostLikely - Minimum) / width;
        var value = u < split
            ? Minimum + width * Math.Sqrt(u * split)
            : Maximum - width * Math.Sqrt((1 - u) * (1 - split));
        return Math.Clamp(value, Minimum, Maximum);
    }
}

public static class EffortGenerator
{
    public static IReadOnlyList<WorkItem> Generate(int count, IEffortDistribution development,
        IEffortDistribution review, IEffortDistribution testing, int seed)
    {
        if (count < 0) throw new ScenarioValidationException("Work Item count must be nonnegative.");
        ArgumentNullException.ThrowIfNull(development);
        ArgumentNullException.ThrowIfNull(review);
        ArgumentNullException.ThrowIfNull(testing);
        var random = new SeededRandom(seed);
        // Stable input order: item 1 Development, Code Review, Testing; then item 2, etc.
        return Array.AsReadOnly(Enumerable.Range(1, count).Select(id => new WorkItem($"STORY-{id}", $"Story {id}",
            development.Sample(random), review.Sample(random), testing.Sample(random))).ToArray());
    }
}
