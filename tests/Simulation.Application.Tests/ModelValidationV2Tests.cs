using ModelValidationV2;
using Simulation.Application;
using Xunit;
namespace Simulation.Application.Tests;
public sealed class ModelValidationV2Tests
{
    [Theory]
    [InlineData("A")][InlineData("B")][InlineData("C")][InlineData("D")]
    public void CombinedScenariosConserveItemsCapacityDebtAndMetricPopulations(string scenario)
    {
        var live=Validation.Run(scenario);var before=Validation.Hash(live.Session.Capture());Validation.Check(live);
        Assert.Equal(before,Validation.Hash(live.Session.Capture()));
    }
    [Fact]
    public void ZeroAvailabilityConsumesNothingAndReportsUnavailableUtilization()
    {
        var live=LiveSimulation.Start(Validation.Baseline with{DeveloperAvailability=0,TesterAvailability=0},Simulation.Core.WorkArrivalMode.AlwaysAvailable);
        Validation.Until(live,30);var p=LivePerformance.Rolling(live.Session,20);
        Assert.All(live.Session.Days,d=>Assert.Equal(0,d.UsedDeveloperCapacity+d.UsedTesterCapacity));
        Assert.Null(p.DeveloperUtilization);Assert.Null(p.TesterUtilization);Assert.Null(p.SystemCostPerReleasedItem);Assert.Null(p.DeliveryWorkCostPerItem);
        Assert.Equal(0,p.Throughput);Assert.Equal(0,p.CompletionRate);
    }
    [Fact]
    public void NoReleasesDoesNotEraseWorkCompletionCost()
    {
        var live=LiveSimulation.Start(Validation.Complex with{Release=new(Capacity:0)},Simulation.Core.WorkArrivalMode.AlwaysAvailable);Validation.Until(live,100);
        Validation.CheckPeriod(live.Session,51,100);var p=LivePerformance.Rolling(live.Session,50);Assert.Equal(0,p.Completed);Assert.Null(p.SystemCostPerReleasedItem);Assert.NotNull(p.DeliveryWorkCostPerItem);
    }
}
