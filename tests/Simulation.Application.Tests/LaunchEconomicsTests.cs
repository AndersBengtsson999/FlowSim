using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.Infrastructure;
using Xunit;
namespace Simulation.Application.Tests;
public class LaunchEconomicsTests
{
    private static SimulationRequest Request() => new() { NumberOfWorkItems=35, SimulationDays=65, DeveloperCount=5, TesterCount=2, DevelopmentEffort=1,CodeReviewEffort=1,TestingEffort=1, ProductLaunch=new("Version 2.0",10,60),Release=new(ReleaseMode.Scheduled,3,10) };
    [Fact] public void LaunchTransfersCostsWithoutExpenseAndPostponementDoesNotChangeThem()
    {
        var live=LiveSimulation.Start(Request(),WorkArrivalMode.ContinuousArrival,2);
        while(live.Session.CurrentDay<60)Assert.True(live.Step());
        var before=LaunchEconomics.Calculate(live.Session.Days,live.Session.ProductDelivery);
        Assert.True(before.Current.WorkInProgress>0);Assert.True(before.Current.AwaitingLaunch>0);Assert.Equal(0,before.Current.CustomerLaunched);
        live.Session.PlanCustomerLaunch(2,LaunchPlanUnit.Sprints,10);
        Assert.Equal(JsonSerializer.Serialize(before),JsonSerializer.Serialize(LaunchEconomics.Calculate(live.Session.Days,live.Session.ProductDelivery)));
        live.Session.LaunchProductVersion("V1");
        var after=LaunchEconomics.Calculate(live.Session.Days,live.Session.ProductDelivery);
        Assert.Equal(before.Current.Incurred,after.Current.Incurred);Assert.Equal(before.Current.WorkInProgress,after.Current.WorkInProgress);
        Assert.Equal(before.Current.AwaitingLaunch,after.Current.CustomerLaunched);Assert.Equal(0,after.Current.AwaitingLaunch);Assert.True(after.Current.CustomerLaunch);
        Assert.Equal(before.Points.Take(59),after.Points.Take(59));
        foreach(var p in after.Points) Assert.Equal(p.Incurred,p.WorkInProgress+p.AwaitingLaunch+p.CustomerLaunched+p.Unallocated);
        var loaded=LiveSessionJson.Load(LiveSessionJson.Save(live));Assert.Equal(JsonSerializer.Serialize(after),JsonSerializer.Serialize(LaunchEconomics.Calculate(loaded.Session.Days,loaded.Session.ProductDelivery)));
    }
    [Fact] public void DefaultCapacityDeltasAndExplicitCapacityProduceEquivalentEstimates()
    {
        var result=new SimulationRunner().Run(Request());Assert.All(result.Days,d=>Assert.Null(d.ConsumedCapacity));
        var calculated=LaunchEconomics.Calculate(result.Days,result.ProductDelivery);Assert.Equal(0,calculated.MissingCapacityDays);Assert.True(calculated.Current.CustomerLaunched>0);
        var repeat=new SimulationRunner().Run(Request());Assert.Equal(JsonSerializer.Serialize(calculated),JsonSerializer.Serialize(LaunchEconomics.Calculate(repeat.Days,repeat.ProductDelivery)));
    }
    private static WorkItemDaySnapshot Item(string id,WorkItemStatus state,double development,double review,double testing,double rework=0) => new(id,state,0,0,0,development,review,testing,0,state,false,rework) { ConsumedCapacity=new(development,review,testing) };
    private static DailySnapshot Day(params WorkItemDaySnapshot[] items) => new(0,0,0,0,0,0,0,0,0,items,4,2) { Economics=new(new(),300,200,100,125) };
    [Fact] public void BlendedSpecialistsReviewReworkTestingAndIdleReconcile()
    {
        var day=Day(Item("A",WorkItemStatus.Development,1,.5,0,.5), Item("B",WorkItemStatus.Released,0,0,1));
        var costs=LaunchEconomics.Calculate([day],null).Current;
        Assert.Equal(250,costs.WorkInProgress);Assert.Equal(50,costs.AwaitingLaunch);Assert.Equal(300,costs.Unallocated);Assert.Equal(600,costs.Incurred);
        // Rework is included in raw developer usage, never added as a second expense.
        Assert.Equal(300,costs.Unlaunched);
    }
    [Fact] public void MissingHistoricalObservationsAreNotInvented()
    {
        var noRaw=Item("A",WorkItemStatus.Development,1,0,0) with{ConsumedCapacity=null,DeliveryCost=null};
        var history=LaunchEconomics.Calculate([Day(noRaw),Day(noRaw) with{Day=1,Economics=null}],null);
        Assert.Equal(1,history.MissingCostDays);Assert.Equal(1,history.MissingCapacityDays);Assert.Equal(600,history.Current.Unallocated);Assert.Equal(0,history.Current.Unlaunched);
    }
    [Fact] public void ReleaseTransfersOnlyTheRecordedItemAndLaunchMembershipIsExact()
    {
        var first=Day(Item("A",WorkItemStatus.Development,1,0,0),Item("B",WorkItemStatus.Development,1,0,0));
        var second=Day(Item("A",WorkItemStatus.Released,0,0,0),Item("B",WorkItemStatus.Development,0,0,0)) with{Day=1};
        var state=new ProductDeliveryState([new("V1","Version",0,null,null,2,[])],[new("IR-2","V1",2,["A"])],[new("CL-1","V1",2,["A"])],null);
        var history=LaunchEconomics.Calculate([first,second],state);
        Assert.Equal(250,history.Points[0].WorkInProgress);Assert.Equal(125,history.Current.WorkInProgress);Assert.Equal(125,history.Current.CustomerLaunched);Assert.Equal(0,history.Current.AwaitingLaunch);
    }
}
