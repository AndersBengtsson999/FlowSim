using System.Reflection;
using System.Text.Json;
using Simulation.Infrastructure;
using Simulation.UI.Help;
using Simulation.UI.ViewModels;
using Xunit;

namespace Simulation.UI.Tests;

public class ProductPolishTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelResetPreservesExactSessionAndPlayback(bool running)
    {
        using var vm = new LiveViewModel(); vm.Start(); vm.Pause();
        for (var i = 0; i < 8; i++) vm.Step();
        vm.CheckpointLabel = "Keep this"; vm.CheckpointCommand.Execute(null);
        vm.SelectedCheckpoint = vm.Checkpoints.Single();
        if (running) vm.Resume();
        var session = vm.Live!; var state = JsonSerializer.Serialize(session.Capture()); var status = vm.Status;
        var selection = vm.SelectedCheckpoint;
        var answer = new TaskCompletionSource<bool>(); vm.ConfirmResetAsync = () => answer.Task;
        var pending = vm.RequestResetAsync();
        Assert.False(vm.ResetCommand.CanExecute(null));
        vm.Tick(); // A modal question must not advance a running simulation in the background.
        Assert.Equal(state, JsonSerializer.Serialize(session.Capture()));
        answer.SetResult(false); await pending;
        Assert.Same(session, vm.Live); Assert.Same(selection, vm.SelectedCheckpoint);
        Assert.Equal(state, JsonSerializer.Serialize(vm.Live!.Capture()));
        Assert.Equal(status, vm.Status); Assert.Equal(running, vm.IsRunning);
        Assert.True(vm.ResetCommand.CanExecute(null));
        // The same saved state still produces exactly the same next simulated day.
        var reference = LiveSessionJson.Load(LiveSessionJson.Save(session)); reference.Step();
        vm.Pause(); vm.Step();
        Assert.Equal(JsonSerializer.Serialize(reference.Capture()), JsonSerializer.Serialize(vm.Live.Capture()));
    }
    [Fact]
    public async Task ConfirmResetClearsTimelineCheckpointsAndSelections()
    {
        using var vm = new LiveViewModel(); vm.Start(); vm.Tick();
        vm.CheckpointCommand.Execute(null); vm.SelectedCheckpoint = vm.Checkpoints.Single();
        vm.ConfirmResetAsync = () => Task.FromResult(true);
        await vm.RequestResetAsync();
        Assert.Null(vm.Live); Assert.False(vm.IsRunning); Assert.Empty(vm.Checkpoints);
        Assert.Null(vm.SelectedCheckpoint); Assert.Equal(0, vm.Day); Assert.True(vm.SetupVisible);
        Assert.False(vm.ResetCommand.CanExecute(null));
        vm.Start(); vm.Pause(); Assert.Empty(vm.Checkpoints); Assert.Equal(0, vm.Day);
    }
    [Fact]
    public async Task NoSessionDoesNotAskAndUnwiredConfirmationNeverDiscards()
    {
        using var vm = new LiveViewModel(); var calls = 0;
        vm.ConfirmResetAsync = () => { calls++; return Task.FromResult(true); };
        await vm.RequestResetAsync(); Assert.Equal(0, calls);
        using var unwired = new LiveViewModel(); unwired.Start(); var session = unwired.Live;
        await unwired.RequestResetAsync(); Assert.Same(session, unwired.Live);
    }
    [Fact]
    public async Task ConfirmationFailureKeepsSessionAndPlayback()
    {
        using var vm = new LiveViewModel(); vm.Start(); var session = vm.Live;
        vm.ConfirmResetAsync = () => throw new InvalidOperationException("Dialog unavailable");
        await Assert.ThrowsAsync<InvalidOperationException>(() => vm.RequestResetAsync());
        Assert.Same(session, vm.Live); Assert.True(vm.IsRunning); Assert.True(vm.ResetCommand.CanExecute(null));
    }
    [Fact]
    public void AboutUsesEmbeddedPublicIdentityAndMatchesAssemblyVersion()
    {
        var assembly = typeof(ProductIdentity).Assembly;
        Assert.Equal("FLOW", assembly.GetCustomAttribute<AssemblyProductAttribute>()!.Product);
        var version = Version.Parse(ProductIdentity.Version);
        Assert.Equal(version.Major, assembly.GetName().Version!.Major);
        Assert.Equal(version.Minor, assembly.GetName().Version!.Minor);
        Assert.Equal(version.Build, assembly.GetName().Version!.Build);
        Assert.Contains(ProductIdentity.Version, ProductIdentity.Description);
        Assert.Contains(ProductIdentity.Commit, ProductIdentity.Description);
        Assert.Contains(ProductIdentity.SourceState, ProductIdentity.Description);
        Assert.Contains(ProductIdentity.SourceState, new[] { "clean", "local changes", "unverified" });
        if (ProductIdentity.SourceState != "unverified") Assert.Matches("^[a-f0-9]{40}$", ProductIdentity.Commit);
    }
    [Fact]
    public void GuideKeepsLinksAndExplainsTrendPressureAndStableOrder()
    {
        var guide = new GuideCatalog(); guide.Validate(requireImages: true);
        var insights = guide.Body("experiments.simulation-insights");
        Assert.Contains("10 simulated days", insights); Assert.Contains("four", insights);
        Assert.Contains("Queue trend is separate from queue pressure", insights);
        Assert.Contains("What FLOW observed", insights); Assert.Contains("What it might mean", insights);
        Assert.Contains("What to explore next", insights); Assert.Contains("Stable viewing order", insights);
        Assert.Contains("Closing and reopening", insights);
        var navigation = guide.Body("live-game-view.navigation");
        Assert.Contains("even before starting", navigation); Assert.Contains("Cancel", navigation);
        Assert.Contains("About FLOW", navigation);
    }
}
