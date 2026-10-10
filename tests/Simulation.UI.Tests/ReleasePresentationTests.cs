using Simulation.Application;
using Simulation.Core;
using Simulation.UI.ViewModels;
using Xunit;
namespace Simulation.UI.Tests;
public sealed class ReleasePresentationTests
{
    [Fact]
    public void UnlimitedCapacityRoundTripsAcrossSetupAndLiveChange()
    {
        var setup = new MainWindowViewModel();
        Assert.Equal("Unlimited", setup.ReleaseCapacity);
        Assert.Equal(int.MaxValue, setup.CaptureSetup().Release.Capacity);
        setup.ReleaseCapacity = "0";
        Assert.Equal(0, setup.CaptureSetup().Release.Capacity);
        setup.ResetToBaseline();
        Assert.Equal("Unlimited", setup.ReleaseCapacity);
        setup.LoadConfiguration(new() { Release = new(ReleaseMode.Scheduled) });
        Assert.Equal("Unlimited", setup.ReleaseCapacity);
        setup.ReleaseCapacity = " unlimited ";
        Assert.Equal(int.MaxValue, setup.CaptureSetup().Release.Capacity);
        Assert.Contains("Unlimited", setup.CaptureSetup().Release.Description);
        using var live = new LiveViewModel();
        live.Start(); live.Pause(); live.BeginChange();
        Assert.Equal("Unlimited", live.CurrentReleaseCapacity);
        Assert.Equal("Unlimited", live.Draft.ReleaseCapacity);
        live.Draft.ReleaseCapacity = "2";
        live.ApplyChanges();
        var description = LiveViewModel.DescribeParameters(live.Live!.Session.Changes.Single());
        Assert.Contains("Unlimited", description);
        Assert.DoesNotContain("2147483647", description);
    }
    [Fact]
    public void ConfigurationRoundTripsUnitsAndRejectsFractionalCapacity()
    {
        var vm=new MainWindowViewModel();vm.LoadConfiguration(new(){Release=new(ReleaseMode.Scheduled,10,5)});
        Assert.True(vm.ScheduledRelease);Assert.Equal("items/release",vm.ReleaseCapacityUnit);Assert.Equal(new ReleaseSettings(ReleaseMode.Scheduled,10,5),vm.CaptureSetup().Release);
        vm.ReleaseCapacity="1.5";Assert.Throws<ArgumentException>(()=>vm.CaptureSetup());vm.ReleaseCapacity="2";vm.ReleaseMode="Flow-based";
        Assert.False(vm.ScheduledRelease);Assert.Equal("items/day",vm.ReleaseCapacityUnit);
    }
    [Fact]
    public void LiveChangeFlowAndSecondaryMetricsUseReleaseSemantics()
    {
        using var vm=new LiveViewModel();vm.WorkSupply="Always available";vm.Setup.ReleaseMode="Scheduled";vm.Setup.ReleaseCapacity="1";vm.Setup.ReleaseInterval="5";
        vm.Start();vm.Pause();for(var i=0;i<40;i++)vm.Step();
        Assert.Contains(vm.StatusPrimaryGroups,r=>r.Label=="Released");Assert.DoesNotContain(vm.StatusPrimaryGroups,r=>r.Label=="Done");
        var queue=vm.Flow.Single(r=>r.State==WorkItemStatus.ReadyForRelease);Assert.True(queue.IsWaiting);Assert.False(queue.HasWipLimit);Assert.True(queue.Count>0);vm.SelectedFlow=queue;Assert.Contains("Ready for Release:",vm.QueueDetail);
        Assert.True(vm.Flow.Single(r=>r.State==WorkItemStatus.Released).IsCompleted);
        vm.BeginChange();vm.Draft.ReleaseMode="Flow-based";vm.Draft.ReleaseCapacity="3";vm.ChangeLabel="Release assumption";vm.ApplyChanges();
        Assert.Equal(new ReleaseSettings(ReleaseMode.FlowBased,3,5),vm.Live!.Session.Configuration.Release);Assert.Equal("Release assumption",vm.Live.Session.Changes.Single().Label);
        Assert.Contains("Release Mode",LiveViewModel.DescribeParameters(vm.Live.Session.Changes.Single()));vm.Step();
        Assert.Contains(vm.ComparisonRows,r=>r.Metric.StartsWith("Release Wait Time"));Assert.Contains(vm.ComparisonRows,r=>r.Metric.StartsWith("Development Cycle Time"));
        Assert.Contains(vm.TrendMetrics,m=>m.Metric==LiveTrendMetric.ReadyForRelease);Assert.Contains(vm.TrendMetrics,m=>m.Metric==LiveTrendMetric.CompletionRate);
    }
}
