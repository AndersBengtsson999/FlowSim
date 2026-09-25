using Simulation.Application;
using Xunit;

namespace Simulation.Application.Tests;

public sealed class DistributionStatisticsTests
{
    [Fact]
    public void KnownDataUsesLinearInterpolation()
    {
        var s = DistributionStatistics.Summarize([30, 0, 20, 10]);
        Assert.Equal(15d, s.P50); Assert.Equal(22.5, s.P75);
        Assert.Equal(25.5, s.P85!.Value, 12); Assert.Equal(28.5, s.P95!.Value, 12);
        Assert.Equal(0d, DistributionStatistics.Percentile([30, 0, 20, 10], 0));
        Assert.Equal(30d, DistributionStatistics.Percentile([30, 0, 20, 10], 1));
    }
    [Fact]
    public void EmptySingletonAndRepeatedObservationsAreDefined()
    {
        Assert.Null(DistributionStatistics.Percentile([], .5));
        Assert.Equal(7d, DistributionStatistics.Percentile([7], .95));
        Assert.Equal(7d, DistributionStatistics.Percentile([7, 7, 7], .85));
        Assert.Equal(0, DistributionStatistics.Summarize([]).SampleCount);
    }
    [Fact]
    public void HistogramIncludesMaximumAndEveryObservationExactlyOnce()
    {
        var s = DistributionStatistics.Summarize(Enumerable.Range(0, 11).Select(i => (double)i));
        Assert.Equal(10, s.Histogram.Count);
        Assert.Equal(11, s.Histogram.Sum(b => b.Count));
        Assert.Equal(2, s.Histogram[^1].Count);
        Assert.Equal(0, s.Histogram[0].Minimum);
        Assert.Equal(10, s.Histogram[^1].Maximum);
    }
    [Fact]
    public void InvalidStatisticsInputsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DistributionStatistics.Percentile([1], -.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => DistributionStatistics.Percentile([1], double.NaN));
        Assert.Throws<ArgumentException>(() => DistributionStatistics.Summarize([double.PositiveInfinity]));
    }
}
