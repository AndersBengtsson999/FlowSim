using System.Text.Json;
using System.Text.Json.Nodes;
using Simulation.Application;
using Simulation.Infrastructure;
using Simulation.UI.ViewModels;
using Xunit;

namespace Simulation.UI.Tests;

public sealed class InterventionLabelTests
{
    [Theory]
    [InlineData("  Stop shortcuts  ", "Stop shortcuts")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    public void LabelFlowsFromEditorToHistoryAndAllPresentations(string input, string? expected)
    {
        using var vm = Started();
        vm.BeginChange(); vm.Draft.ShortcutRate = "0"; vm.ChangeLabel = input; vm.ApplyCommand.Execute(null);
        var change = vm.Live!.Session.Changes.Single();
        Assert.Equal(expected, change.Label);
        Assert.Same(change, vm.SelectedIntervention);
        foreach (var text in new[] { vm.LatestIntervention, InterventionPresentation.Marker(change), InterventionPresentation.Selection(change) })
        {
            Assert.Contains("Shortcut Rate", text); Assert.Contains("→", text);
            if (expected != null) Assert.Contains(expected, text);
            else { Assert.DoesNotContain("\n\n", text); Assert.DoesNotContain("null", text); }
        }
        Assert.Contains("effective Day 21", vm.LatestIntervention);
        Assert.Contains("Effective Day 21", InterventionPresentation.Marker(change));
        vm.Step();
        Assert.Equal(expected, vm.Trend.Interventions.Single().Label);
        vm.Resume(); vm.Tick(); vm.Pause();
        Assert.Equal(expected, vm.Live.Session.Changes.Single().Label);
    }

    [Fact]
    public void MultipleChangesShareOneLabelAndLabelAloneCreatesNoIntervention()
    {
        using var vm = Started();
        vm.BeginChange(); vm.ChangeLabel = "Just metadata"; vm.ApplyCommand.Execute(null);
        Assert.Empty(vm.Live!.Session.Changes);
        vm.BeginChange(); vm.ChangeLabel = "Reduce delivery pressure";
        vm.Draft.ShortcutRate = "10"; vm.Draft.DebtRepayment = "25"; vm.Draft.DevelopmentWipLimit = "3"; vm.ApplyCommand.Execute(null);
        var change = vm.Live.Session.Changes.Single();
        var text = InterventionPresentation.Selection(change);
        Assert.Equal(1, text.Split("Reduce delivery pressure").Length - 1);
        Assert.Contains("Shortcut Rate", text); Assert.Contains("Debt Repayment", text); Assert.Contains("Development WIP", text);
    }

    [Fact]
    public void LabelsSurviveCheckpointAndReloadWhileMissingHistoricalLabelsLoadNormally()
    {
        using var vm = Started(); vm.BeginChange(); vm.Draft.ShortcutRate = "0"; vm.ChangeLabel = "Stop shortcuts"; vm.ApplyCommand.Execute(null);
        var live = vm.Live!; var checkpoint = live.CreateCheckpoint("After change");
        for (var i = 0; i < 5; i++) live.Step();
        var loaded = LiveSessionJson.Load(LiveSessionJson.Save(live));
        Assert.Equal("Stop shortcuts", loaded.Session.Changes.Single().Label);
        loaded.RestoreCheckpoint(checkpoint.Id);
        Assert.Equal("Stop shortcuts", loaded.Session.Changes.Single().Label);
        Assert.Equal(20, loaded.Session.CurrentDay);
        // Simulate a historical document where optional label fields are absent.
        var json = JsonNode.Parse(LiveSessionJson.Save(live))!;
        RemoveLabels(json);
        var legacy = LiveSessionJson.Load(json.ToJsonString());
        Assert.Null(legacy.Session.Changes.Single().Label);
        Assert.Contains("Shortcut Rate", InterventionPresentation.Selection(legacy.Session.Changes.Single()));
    }

    [Fact]
    public void LabelIsPureMetadataAcrossIdenticalSeededContinuations()
    {
        using var labeled = Started(); using var plain = Started();
        foreach (var vm in new[] { labeled, plain })
        {
            vm.BeginChange(); vm.Draft.ShortcutRate = "0"; vm.Draft.DebtRepayment = "25";
            vm.ChangeLabel = ReferenceEquals(vm, labeled) ? "Stop shortcuts" : ""; vm.ApplyCommand.Execute(null);
            for (var i = 0; i < 60; i++) vm.Step();
        }
        var a = labeled.Live!.Session.Capture(); var b = plain.Live!.Session.Capture();
        Assert.Equal(JsonSerializer.Serialize(a with { Changes = a.Changes.Select(c => c with { Label = null }).ToArray() }), JsonSerializer.Serialize(b));
        Assert.Equal(JsonSerializer.Serialize(labeled.Live.Session.GetResult()), JsonSerializer.Serialize(plain.Live.Session.GetResult()));
    }

    private static LiveViewModel Started()
    {
        var vm = new LiveViewModel(); vm.Setup.ShortcutRate = "50"; vm.Start(); vm.Pause();
        for (var i = 0; i < 20; i++) vm.Step();
        return vm;
    }
    private static void RemoveLabels(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            // Leave checkpoint labels alone; remove only ConfigurationChange.Label.
            if (obj.Any(p => p.Key.Equals("Before", StringComparison.OrdinalIgnoreCase)))
                foreach (var key in obj.Select(p => p.Key).Where(k => k.Equals("Label", StringComparison.OrdinalIgnoreCase)).ToArray()) obj.Remove(key);
            foreach (var child in obj.Select(p => p.Value).OfType<JsonNode>()) RemoveLabels(child);
        }
        else if (node is JsonArray array) foreach (var child in array.OfType<JsonNode>()) RemoveLabels(child);
    }
}
