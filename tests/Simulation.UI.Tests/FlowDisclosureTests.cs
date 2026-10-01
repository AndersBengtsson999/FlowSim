using System.Text.Json;
using Simulation.Core;
using Simulation.UI.ViewModels;
using Xunit;

namespace Simulation.UI.Tests;

public sealed class FlowDisclosureTests
{
    [Fact]
    public void DisclosureStartsCollapsedTogglesChevronAndDoesNotChangeSession()
    {
        using var vm = new LiveViewModel(); vm.WorkSupply = "Always available"; vm.Start(); vm.Pause(); vm.Step();
        var row = vm.Flow.Single(r => r.State == WorkItemStatus.Development);
        var before = JsonSerializer.Serialize(vm.Live!.Capture());
        Assert.False(row.IsExpanded); Assert.Equal("›", row.DisclosureChevron); Assert.Empty(row.VisibleItems);
        row.IsExpanded = true; Assert.Equal("⌄", row.DisclosureChevron); Assert.Equal(row.Count, row.VisibleItems.Count);
        Assert.All(row.VisibleItems, i => Assert.Equal(WorkItemStatus.Development, i.State));
        row.IsExpanded = false; Assert.Equal("›", row.DisclosureChevron); Assert.Empty(row.VisibleItems);
        Assert.Equal(before, JsonSerializer.Serialize(vm.Live.Capture()));
    }

    [Fact]
    public void EveryRowUsesExactEndOfDayStateAndUpdatesWithoutCollapsing()
    {
        using var vm = new LiveViewModel(); vm.Preset = "Defects & Rework Example"; vm.WorkSupply = "Always available"; vm.Start(); vm.Pause();
        foreach (var row in vm.Flow) row.IsExpanded = true;
        var idsChanged = false; string previous = "";
        for (var day = 0; day < 120; day++)
        {
            vm.Step();
            foreach (var row in vm.Flow)
            {
                Assert.True(row.IsExpanded);
                var expected = vm.Live!.CurrentSnapshot.Items.Where(i => i.CreatedDay <= vm.Live.CurrentSnapshot.Day && i.State == row.State).Select(i => i.Id);
                Assert.Equal(expected, row.VisibleItems.Select(i => i.Id)); Assert.Equal(row.Count, row.VisibleItems.Count);
            }
            var current = string.Join(",", vm.Flow.Single(r => r.State == WorkItemStatus.Development).VisibleItems.Select(i => i.Id));
            idsChanged |= day > 0 && previous != current; previous = current;
        }
        Assert.True(idsChanged); Assert.Equal(9, vm.Flow.Count);
    }

    [Fact]
    public async Task EmptyBacklogAndLargeQueueExposeAllActualItemsWithoutGeneratingWork()
    {
        using var vm = new LiveViewModel(); vm.WorkSupply = "Always available"; vm.Setup.TesterAvailability = "0";
        vm.Start(); vm.Pause(); vm.TargetDay = "120"; await vm.RunToDayAsync();
        var before = JsonSerializer.Serialize(vm.Live!.Capture());
        var backlog = vm.Flow.Single(r => r.State == WorkItemStatus.Backlog); backlog.IsExpanded = true;
        Assert.True(backlog.IsEmpty); Assert.Empty(backlog.VisibleItems);
        var queue = vm.Flow.Single(r => r.State == WorkItemStatus.WaitingForTesting); queue.IsExpanded = true;
        Assert.True(queue.Count >= 44); Assert.Equal(queue.Count, queue.VisibleItems.Count);
        Assert.Equal(before, JsonSerializer.Serialize(vm.Live.Capture()));
        vm.Step(); Assert.True(vm.Flow.Single(r => r.State == queue.State).IsExpanded);
        Assert.True(vm.Flow.Single(r => r.State == backlog.State).IsEmpty);
    }

    [Fact]
    public void ExpansionSurvivesNavigationAndResetRestoresCollapsedRows()
    {
        var main = new MainWindowViewModel(); using var vm = main.Simple.Live;
        vm.Start(); vm.Pause(); vm.Flow.Single(r => r.State == WorkItemStatus.Testing).IsExpanded = true;
        main.Simple.OpenAnalyzeCommand.Execute(null); main.Simple.OpenLiveCommand.Execute(null);
        Assert.True(vm.Flow.Single(r => r.State == WorkItemStatus.Testing).IsExpanded);
        vm.Reset(); vm.Start(); vm.Pause(); Assert.All(vm.Flow, r => Assert.False(r.IsExpanded));
    }

    [Fact]
    public void DisclosureDoesNotChangeDeterministicContinuation()
    {
        using var a = new LiveViewModel(); using var b = new LiveViewModel();
        a.WorkSupply = b.WorkSupply = "Always available"; a.Start(); b.Start(); a.Pause(); b.Pause();
        for (var day = 0; day < 100; day++)
        {
            foreach (var row in a.Flow) row.IsExpanded = day % 2 == 0;
            a.Step(); b.Step();
        }
        Assert.Equal(JsonSerializer.Serialize(a.Live!.Capture()), JsonSerializer.Serialize(b.Live!.Capture()));
    }
}
