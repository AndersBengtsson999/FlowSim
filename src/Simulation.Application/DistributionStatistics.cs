namespace Simulation.Application;

public sealed record HistogramBin(double Minimum, double Maximum, int Count);
public sealed record MetricDistribution(int SampleCount, double? P50, double? P75, double? P85, double? P95,
    IReadOnlyList<HistogramBin> Histogram);

public static class DistributionStatistics
{
    /// <summary>Linear interpolation at (n-1)*p (Hyndman-Fan type 7); null for no observations.</summary>
    public static double? Percentile(IEnumerable<double> values, double probability)
    {
        if (!double.IsFinite(probability) || probability < 0 || probability > 1)
            throw new ArgumentOutOfRangeException(nameof(probability));
        return Interpolate(Sorted(values), probability);
    }

    public static MetricDistribution Summarize(IEnumerable<double> values)
    {
        var sorted = Sorted(values);
        return new(sorted.Length, Interpolate(sorted, .5), Interpolate(sorted, .75),
            Interpolate(sorted, .85), Interpolate(sorted, .95), Histogram(sorted));
    }

    private static double[] Sorted(IEnumerable<double> values)
    {
        var data = values.ToArray();
        if (data.Any(v => !double.IsFinite(v))) throw new ArgumentException("Observations must be finite.", nameof(values));
        Array.Sort(data);
        return data;
    }

    private static double? Interpolate(double[] sorted, double p)
    {
        if (sorted.Length == 0) return null;
        var index = (sorted.Length - 1) * p;
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);
        var weight = index - lower;
        if (sorted[lower] == sorted[upper]) return sorted[lower];
        var width = sorted[upper] - sorted[lower];
        return double.IsFinite(width) ? sorted[lower] + width * weight
            : sorted[lower] * (1 - weight) + sorted[upper] * weight;
    }

    private static IReadOnlyList<HistogramBin> Histogram(double[] sorted)
    {
        if (sorted.Length == 0) return Array.AsReadOnly(Array.Empty<HistogramBin>());
        var min = sorted[0]; var max = sorted[^1];
        if (min == max) return Array.AsReadOnly(new[] { new HistogramBin(min, max, sorted.Length) });
        const int bins = 10;
        var counts = new int[bins];
        foreach (var value in sorted)
            counts[Math.Min(bins - 1, (int)((value - min) / (max - min) * bins))]++;
        return Array.AsReadOnly(Enumerable.Range(0, bins).Select(i =>
            new HistogramBin(min + (max - min) * i / bins, min + (max - min) * (i + 1) / bins, counts[i])).ToArray());
    }
}
