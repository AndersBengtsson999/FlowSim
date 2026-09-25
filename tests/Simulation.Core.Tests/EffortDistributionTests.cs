using Simulation.Core;
using Xunit;

namespace Simulation.Core.Tests;

public sealed class EffortDistributionTests
{
    private sealed class ConstantRandom(double value) : IRandomSource { public double NextUnitDouble() => value; }
    private sealed class ForbiddenRandom : IRandomSource { public double NextUnitDouble() => throw new InvalidOperationException("Fixed must not draw randomness."); }

    [Fact]
    public void FixedReturnsExactEffortAndConsumesNoRandomness()
    {
        Assert.Equal(5.123, new FixedEffort(5.123).Sample(new ForbiddenRandom()));
        Assert.Equal(0, new FixedEffort(0).Sample(new ForbiddenRandom()));
        Assert.Throws<ScenarioValidationException>(() => new FixedEffort(-1));
        Assert.Throws<ScenarioValidationException>(() => new FixedEffort(double.NaN));
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(.3, 5)]
    [InlineData(.825, 8.5)]
    public void TriangularUsesTheInverseCdf(double sample, double expected) =>
        Assert.Equal(expected, new TriangularEffort(2, 5, 12).Sample(new ConstantRandom(sample)), 12);

    [Fact]
    public void SamplesStayWithinBoundsIncludingEndpointModes()
    {
        foreach (var distribution in new[] { new TriangularEffort(.5, 1, 3), new TriangularEffort(1, 1, 5), new TriangularEffort(1, 5, 5) })
        {
            var random = new SeededRandom(12345);
            for (var i = 0; i < 10000; i++) Assert.InRange(distribution.Sample(random), distribution.Minimum, distribution.Maximum);
        }
        Assert.Equal(2, new TriangularEffort(2, 2, 2).Sample(new ForbiddenRandom()));
    }

    [Fact]
    public void InvalidTriangularConfigurationsAreRejected()
    {
        foreach (var (min, mode, max) in new[] {(0d,1d,2d),(-1d,1d,2d),(2d,1d,3d),(1d,3d,2d),(double.NaN,1d,2d),(1d,double.PositiveInfinity,2d),(1d,2d,double.PositiveInfinity)})
            Assert.Throws<ScenarioValidationException>(() => new TriangularEffort(min, mode, max));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TriangularEffort(1, 2, 3).Sample(new ConstantRandom(1)));
    }

    [Fact]
    public void SplitMixSequenceIsPinnedAndSeeded()
    {
        var random = new SeededRandom(12345);
        Assert.Equal(0.1330796686614273, random.NextUnitDouble(), 15);
        Assert.Equal(0.20481663336165912, random.NextUnitDouble(), 15);
        var a = new SeededRandom(-123);
        var b = new SeededRandom(-123);
        for (var i = 0; i < 100; i++) Assert.Equal(a.NextUnitDouble(), b.NextUnitDouble());
    }

    [Fact]
    public void GenerationIsReproducibleAndDifferentSeedsChangeEfforts()
    {
        var dev = new TriangularEffort(2, 5, 12);
        var review = new FixedEffort(1);
        var test = new TriangularEffort(1, 2, 5);
        var a = EffortGenerator.Generate(30, dev, review, test, 12345);
        var b = EffortGenerator.Generate(30, dev, review, test, 12345);
        var c = EffortGenerator.Generate(30, dev, review, test, 54321);
        Assert.Equal(a.Select(w => w.DevelopmentEffort), b.Select(w => w.DevelopmentEffort));
        Assert.Equal(a.Select(w => w.TestingEffort), b.Select(w => w.TestingEffort));
        Assert.NotEqual(a.Select(w => w.DevelopmentEffort).ToArray(), c.Select(w => w.DevelopmentEffort).ToArray());
        Assert.All(a, w => { Assert.Equal(1, w.CodeReviewEffort); Assert.Equal(w.DevelopmentEffort, w.RemainingDevelopmentEffort); });
    }
}
