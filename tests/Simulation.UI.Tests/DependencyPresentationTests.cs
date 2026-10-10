using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.UI.ViewModels;
using Xunit;
namespace Simulation.UI.Tests;
public sealed class DependencyPresentationTests
{
    [Fact]
    public void DerivedQueuePartitionsBacklogAndUsesSharedAttentionWithoutActiveWip()
    {
        using var vm=new LiveViewModel();vm.Setup.DependencyRate="100";vm.Setup.DependencyWaitingDays="50";vm.WorkSupply="Always available";
        vm.Start();vm.Pause();for(var i=0;i<20;i++)vm.Step();
        var d=vm.Live!.CurrentSnapshot;var queue=vm.Flow.Single(r=>r.IsDependencyQueue);var backlog=vm.Flow.Single(r=>r.Name=="Backlog");
        Assert.Equal(d.BacklogCount,queue.Count+backlog.Count);Assert.Equal(queue.Count,queue.Items.Count);Assert.Equal(backlog.Count,backlog.Items.Count);
        Assert.Empty(queue.Items.Select(w=>w.Id).Intersect(backlog.Items.Select(w=>w.Id)));Assert.False(queue.HasWipLimit);Assert.True(queue.IsWaiting);
        Assert.Equal(new QueueAttention(queue.Count,vm.Live.Session.Configuration.DevelopmentWipLimit,"Development WIP limit",vm.Performance!.WaitingForDependency.Trend).State,queue.Queue!.State);
        Assert.Equal(d.TotalWip,vm.Flow.Where(r=>r.State!=WorkItemStatus.Backlog && !r.IsCompleted).Sum(r=>r.Count));
        vm.SelectedFlow=queue;Assert.Contains("Waiting for Dependency",vm.QueueDetail);
        queue.IsExpanded=true;vm.Step();Assert.True(vm.Flow.Single(r=>r.IsDependencyQueue).IsExpanded);Assert.False(vm.Flow.Single(r=>r.Name=="Backlog").IsExpanded);
        Assert.Contains(vm.TrendMetrics,m=>m.Metric==LiveTrendMetric.WaitingForDependency);
        Assert.All(vm.Flow,r=>Assert.Equal(23,r.RowMinHeight));
    }
    [Fact]
    public void LiveFieldsRoundTripAndInterventionsPreserveExistingAssignments()
    {
        using var vm=new LiveViewModel();vm.WorkSupply="Always available";vm.Start();vm.Pause();for(var i=0;i<10;i++)vm.Step();
        var before=JsonSerializer.Serialize(vm.Live!.Session.WorkItems.Select(w=>w.ResidualDependency));
        vm.BeginChange();vm.ChangeFields.Single(f=>f.Label=="Dependency Rate (%)").Value="50";vm.ChangeFields.Single(f=>f.Label=="Dependency Waiting Time (days)").Value="5";vm.ChangeLabel="Residual dependencies";vm.ApplyChanges();
        Assert.Equal(new DependencySettings(.5,5),vm.Live.Session.Configuration.ResidualDependencies);Assert.Equal(before,JsonSerializer.Serialize(vm.Live.Session.WorkItems.Select(w=>w.ResidualDependency)));
        Assert.Equal("Residual dependencies",vm.Live.Session.Changes.Single().Label);
        for(var i=0;i<30;i++)vm.Step();Assert.Contains(vm.ComparisonRows,r=>r.Metric=="Average Waiting for Dependency");
        vm.BeginChange();Assert.Equal("50",vm.Draft.DependencyRate);Assert.Equal("5",vm.Draft.DependencyWaitingDays);
    }
    [Fact]
    public void DisabledFeaturePreservesBoardAndZeroScaleIsSafe()
    {
        using var vm=new LiveViewModel();vm.Start();vm.Pause();vm.Step();Assert.DoesNotContain(vm.Flow,r=>r.IsDependencyQueue);Assert.All(vm.Flow,r=>Assert.Equal(27,r.RowMinHeight));
        var row=new FlowStateRow("Waiting for Dependency",5,"","","");
        var projected=QueueAttention.Apply(row,new(new(),0),[]);Assert.True(double.IsFinite(projected.Queue!.RelativeSize));Assert.Null(projected.Queue.Slope);
    }
}
