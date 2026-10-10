using System.Text.Json;
using Simulation.Core;
using Simulation.UI.ViewModels;
using Xunit;
namespace Simulation.UI.Tests;
public sealed class QueueAttentionTests
{
    [Theory]
    [InlineData(0, 1, 1, QueueAttentionState.Neutral)]
    [InlineData(1, 3, 0, QueueAttentionState.Neutral)]
    [InlineData(6, 3, 0, QueueAttentionState.Attention)]
    [InlineData(6, 3, .1, QueueAttentionState.Strong)]
    [InlineData(20, 3, .1, QueueAttentionState.Strong)]
    [InlineData(6, 3, -.1, QueueAttentionState.Attention)]
    [InlineData(6, 12, .1, QueueAttentionState.Neutral)]
    [InlineData(6, 0, .1, QueueAttentionState.Strong)]
    [InlineData(3, 3, .049, QueueAttentionState.Neutral)]
    [InlineData(3, 3, .05, QueueAttentionState.Attention)]
    public void SeverityUsesScaleAndEstablishedTrend(int count, double scale, double slope, QueueAttentionState expected)
    {
        var q = new QueueAttention(count, scale, "scale", slope);
        Assert.Equal(expected, q.State);
        Assert.True(double.IsFinite(q.RelativeSize));
    }
    [Fact]
    public void MissingHistoryIsNotPresentedAsStable()
    {
        var q = QueueAttention.For(1, 3, "scale", [], d => d.ReadyForReleaseCount);
        Assert.Null(q.Slope); Assert.Equal("—", q.Arrow); Assert.Equal(QueueAttentionState.Neutral, q.State);
        Assert.Equal(QueueAttentionState.Strong, new QueueAttention(20, 3, "scale", null).State);
    }
    [Fact]
    public void ProjectionAndWindowChangesAreReadOnlyAndSpecialistsAreSeparate()
    {
        using var vm = new LiveViewModel();
        vm.Setup.ReleaseCapacity = "0"; vm.Setup.Specialists = "1"; vm.Setup.SpecialistWorkRate = "80";
        vm.WorkSupply = "Always available"; vm.Start(); vm.Pause();
        for(var i=0;i<60;i++) vm.Step();
        var before = JsonSerializer.Serialize(vm.Live!.Session.Capture());
        var ready = vm.Flow.Single(r => r.State == WorkItemStatus.ReadyForRelease);
        Assert.Equal(QueueAttentionState.Strong, ready.Queue!.State);
        Assert.Equal(0, ready.Queue.Scale);
        var dev = vm.Flow.Single(r => r.IsDevelopment);
        Assert.Null(dev.Queue); Assert.NotNull(dev.SpecialistQueue); Assert.Equal(1, dev.SpecialistQueue.Scale);
        Assert.All(vm.Flow.Where(r => r.HasWipLimit), r => Assert.Null(r.Queue));
        var singleton = QueueAttention.For(1, 3, "scale", vm.Live.Session.Days.Take(1), d => d.ReadyForReleaseCount);
        Assert.Null(singleton.Slope);
        vm.RollingWindow = 10;
        var days = vm.Live.Session.Days.TakeLast(10);
        Assert.Equal(Simulation.Application.LivePerformance.Slope(days.Select(d => ((double)d.Day, (double)d.ReadyForReleaseCount))),vm.Flow.Single(r => r.State == WorkItemStatus.ReadyForRelease).Queue!.Slope);
        Assert.Equal(before, JsonSerializer.Serialize(vm.Live.Session.Capture()));
    }
}
