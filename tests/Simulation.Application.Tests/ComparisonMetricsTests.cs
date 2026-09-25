using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Xunit;

namespace Simulation.Application.Tests;

public sealed class ComparisonMetricsTests
{
    [Fact]
    public void ReusableComparisonsChangeOnlySpecifiedConfigurationAndProduceDeterministicMetrics()
    {
        var baseline = BaselineScenario.Create();
        var engine = new SimulationEngine();
        var original = engine.Run(baseline);
        var scenarios = ComparisonScenarios.Create();
        Assert.Equal(1, scenarios[0].Team.TesterCount);
        Assert.Equal(8, scenarios[1].Team.DeveloperCount);
        Assert.Equal(3, scenarios[2].DevelopmentWipLimit);
        foreach (var s in scenarios)
        {
            Assert.Equal(JsonSerializer.Serialize(baseline.WorkItems), JsonSerializer.Serialize(s.WorkItems));
            var r = engine.Run(s);
            Assert.Equal(JsonSerializer.Serialize(r), JsonSerializer.Serialize(engine.Run(s)));
            Assert.Equal(100, r.Days.Count);
            Assert.All(r.Days, d => Assert.Equal(30, d.BacklogCount + d.TotalWip + d.DoneCount));
            Assert.InRange(r.DeveloperUtilization, 0, 1);
            Assert.InRange(r.TesterUtilization, 0, 1);
        }
        var a = engine.Run(scenarios[0]);
        var b = engine.Run(scenarios[1]);
        var c = engine.Run(scenarios[2]);
        Assert.True(a.Days.Sum(d => d.AvailableTesterCapacity) < original.Days.Sum(d => d.AvailableTesterCapacity));
        Assert.True(b.Days.Sum(d => d.AvailableDeveloperCapacity) > original.Days.Sum(d => d.AvailableDeveloperCapacity));
        Assert.All(c.Days, d => Assert.InRange(d.DevelopmentWip, 0, 3));
        Assert.NotEqual(original.AverageLeadTime, a.AverageLeadTime);
        Assert.NotEqual(original.DeveloperUtilization, b.DeveloperUtilization);
        Assert.NotEqual(original.AverageLeadTime, c.AverageLeadTime);
    }
}
