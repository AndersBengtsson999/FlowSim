using Simulation.Application;
using Simulation.UI.ViewModels;
using Xunit;
namespace Simulation.UI.Tests;
public sealed class DeliveryCostPresentationTests
{
    [Fact]
    public void CompactStatusAndExistingAnalysisSurfacesExposeCostWithoutExtraPanel()
    {
        using var vm=new LiveViewModel();vm.Start();vm.Pause();
        Assert.Equal("—",vm.StatusPrimaryGroups.Single(r=>r.Label=="Cost/Item").Value);
        for(var i=0;i<50;i++)vm.Step();
        var row=vm.StatusPrimaryGroups.Single(r=>r.Label=="Cost/Item");
        Assert.NotEqual("—",row.Value);Assert.Contains("Development",row.Explanation);Assert.Contains("Debt repayment is excluded",row.Explanation);
        Assert.Contains("System Cost / Released Item",row.Explanation);
        Assert.DoesNotContain(vm.StatusPrimaryGroups,r=>r.Label.StartsWith("System"));
        vm.TrendMetric=vm.TrendMetrics.Single(m=>m.Metric==LiveTrendMetric.SystemCost);
        Assert.Equal("capacity units / released item",vm.TrendMetric.Unit);
        vm.TrendMetric=vm.TrendMetrics.Single(m=>m.Metric==LiveTrendMetric.DeliveryCost);
        Assert.Equal("capacity units / item",vm.TrendMetric.Unit);
        vm.BeginChange();vm.Draft.DevelopmentProductivity="2";vm.ApplyChanges();for(var i=0;i<20;i++)vm.Step();
        Assert.Contains(vm.ComparisonRows,r=>r.Metric=="System Cost / Released Item · capacity units");
        Assert.Contains(vm.ComparisonRows,r=>r.Metric=="Delivery Work Cost / Item · capacity units");
        Assert.Contains(LivePerformancePresentation.Delivery(vm.Performance!),r=>r.Label=="Delivery Work Cost / Item");
    }
    [Fact]
    public void MissingHistoricalCostUsesUnavailableText()
    {
        Assert.Contains("unavailable",LivePerformancePresentation.CostDetails(null));
        Assert.Contains("unavailable",LivePerformancePresentation.CostDetails(new(IsComplete:false)));
    }
}
