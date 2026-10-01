using System.Text.Json;
using System.Text.Json.Nodes;
using Simulation.Application;
using Simulation.Core;
using Simulation.Infrastructure;
using Simulation.UI.ViewModels;
using Xunit;
namespace Simulation.UI.Tests;

public sealed class CapacityUxTests
{
    [Theory]
    [InlineData("100", "100", "1", 5, 2, 5)]
    [InlineData("80", "75", "1", 4, 1.5, 4)]
    [InlineData("80", "100", "1.5", 4, 2, 6)]
    public void NormalConfigurationUsesPeopleAvailabilityAndIndependentProductivity(string developers, string testers, string productivity,
        double availableDev, double availableTest, double work)
    {
        using var vm = new LiveViewModel(); vm.WorkSupply = "Always available";
        vm.Setup.DeveloperAvailability = developers; vm.Setup.TesterAvailability = testers; vm.Setup.DevelopmentProductivity = productivity;
        vm.Setup.DevelopmentEffort = "100"; vm.Start(); vm.Pause(); vm.Step();
        var team = vm.Live!.Session.Configuration.Team;
        Assert.Equal(1, team.DeveloperCapacityPerDay); Assert.Equal(1, team.TesterCapacityPerDay);
        var d = vm.Live.CurrentSnapshot;
        Assert.Equal(availableDev, d.AvailableDeveloperCapacity); Assert.Equal(availableTest, d.AvailableTesterCapacity);
        Assert.Equal(availableDev, d.UsedDeveloperCapacity); Assert.Equal(work, d.DevelopmentWork);
        Assert.Equal(availableDev, vm.LiveStatus!.DeveloperAvailable); Assert.Equal(availableDev, vm.LiveStatus.DeveloperUsed);
        Assert.Equal(1, vm.LiveStatus.DeveloperUtilization); Assert.Equal(availableTest, vm.LiveStatus.TesterAvailable);
        Assert.Equal(0, vm.LiveStatus.TesterUsed); Assert.Equal(0, vm.LiveStatus.TesterUtilization);
        Assert.Empty(vm.CustomCapacityNotice); Assert.DoesNotContain("Nominal capacity per person:", vm.ConfigurationDetails);
        vm.TrendMetric = vm.TrendMetrics.Single(m => m.Metric == LiveTrendMetric.AvailableDevelopers);
        Assert.Equal(availableDev, vm.Trend.Points.Single().Value);
    }
    [Fact]
    public void AllNormalPresetsUseUnitNominalScaling()
    {
        using var vm = new LiveViewModel();
        foreach (var preset in vm.Presets)
        {
            vm.Preset = preset; var request = vm.Setup.CaptureSetup();
            Assert.Equal(1, request.DeveloperCapacityPerDay); Assert.Equal(1, request.TesterCapacityPerDay);
        }
    }
    [Fact]
    public void NormalChangesExposePeopleAvailabilityAndProductivityWithoutNominalScaling()
    {
        using var vm = new LiveViewModel(); vm.Start(); vm.Pause(); vm.BeginChange();
        foreach (var label in new[] {"Developers", "Testers", "Developer Availability (%)", "Tester Availability (%)",
                     "Development Productivity (x)", "Code Review Productivity (x)", "Testing Productivity (x)"})
            Assert.NotEmpty(vm.ChangeFields.Single(f => f.Label == label).Help);
        Assert.DoesNotContain(vm.ChangeFields, f => f.Label.Contains("Capacity"));
        var main = new MainWindowViewModel();
        Assert.DoesNotContain(main.Simple.CapacityChanges, f => f.Label.Contains("Capacity"));
    }
    [Fact]
    public void LegacyCustomScalingSurvivesEditingPersistenceAndContinuedLiveChanges()
    {
        // Old configuration schema with explicit non-unit values and no productivity field.
        var old = JsonNode.Parse(ExperimentJson.SaveScenario(new(Guid.NewGuid(), LiveSimulation.Demo)))!;
        old["SimulationModelVersion"] = "0.3";
        var configuration = old["Scenario"]!["Configuration"]!.AsObject();
        configuration.Remove("Productivity"); configuration["DeveloperCapacityPerDay"] = 1.7; configuration["TesterCapacityPerDay"] = .4;
        var scenario = ExperimentJson.LoadScenario(old.ToJsonString()); var request = scenario.Configuration;
        Assert.Equal(1.7, request.DeveloperCapacityPerDay); Assert.Equal(.4, request.TesterCapacityPerDay);
        Assert.Equal(StageProductivity.Default, request.Productivity);
        var editor = new MainWindowViewModel(); editor.LoadConfiguration(request);
        Assert.Equal(1.7, editor.CaptureSetup().DeveloperCapacityPerDay); Assert.Equal(.4, editor.CaptureSetup().TesterCapacityPerDay);
        Assert.Equal(scenario, ExperimentJson.LoadScenario(ExperimentJson.SaveScenario(scenario)));
        Assert.Equal("1.7", ScenarioParameters.Describe(request)["Developer Capacity / Day"]);
        var live = LiveSimulation.Start(request, WorkArrivalMode.AlwaysAvailable); live.Step(); var checkpoint = live.CreateCheckpoint("Custom scaling");
        using var vm = new LiveViewModel(); vm.Load(LiveSessionJson.Load(LiveSessionJson.Save(live)));
        var history = JsonSerializer.Serialize(vm.Live!.Session.Days); Assert.Contains("Advanced nominal scaling retained", vm.ConfigurationDetails);
        vm.BeginChange(); vm.ChangeFields.Single(f => f.Label == "Developer Availability (%)").Value = "80";
        vm.ChangeFields.Single(f => f.Label == "Development Productivity (x)").Value = "1.5"; vm.ApplyChanges();
        Assert.Equal(history, JsonSerializer.Serialize(vm.Live.Session.Days));
        Assert.Equal(1.7, vm.Live.Session.Configuration.Team.DeveloperCapacityPerDay); Assert.Equal(.4, vm.Live.Session.Configuration.Team.TesterCapacityPerDay);
        vm.Step(); Assert.Equal(6.8, vm.LiveStatus!.DeveloperAvailable!.Value, 12); Assert.Equal(.8, vm.LiveStatus.TesterAvailable!.Value, 12);
        var reloaded = LiveSessionJson.Load(LiveSessionJson.Save(vm.Live));
        Assert.Equal(JsonSerializer.Serialize(vm.Live.Capture()), JsonSerializer.Serialize(reloaded.Capture()));
        reloaded.RestoreCheckpoint(checkpoint.Id); Assert.Equal(1.7, reloaded.Session.Configuration.Team.DeveloperCapacityPerDay);
        Assert.Equal(.4, reloaded.Session.Configuration.Team.TesterCapacityPerDay);
    }
}
