using Simulation.Application;
using Simulation.UI.ViewModels;
using Xunit;

namespace Simulation.UI.Tests;

public sealed class CollaborationPresentationTests
{
    [Fact]
    public async Task SelectedDayRefreshesAllocationDetailsIncludingCompletedDevelopment()
    {
        var vm = new MainWindowViewModel();
        vm.LoadConfiguration(new SimulationRequest { DeveloperCount = 5, DevelopmentWipLimit = 1,
            NumberOfWorkItems = 1, DevelopmentEffort = 1.2, SimulationDays = 3 });
        await vm.RunAsync();
        var notifications = new List<string?>();
        vm.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
        vm.SelectedDay = 1;
        var allocation = Assert.Single(vm.DevelopmentAllocations);
        Assert.Contains("completed Development", allocation.Label);
        Assert.Contains($"Collaboration {0.4:0.###}", allocation.Value);
        Assert.Contains($"Capacity used {1.4:0.###}", allocation.Value);
        Assert.Contains($"Effective work {1.2:0.###}", allocation.Value);
        Assert.Contains(nameof(vm.DevelopmentAllocations), notifications);
        vm.SelectedDay = 2;
        Assert.Empty(vm.DevelopmentAllocations);
    }
}
