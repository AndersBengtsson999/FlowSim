using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.UI.ViewModels;
using Xunit;
namespace Simulation.UI.Tests;
public sealed class StageProductivityViewModelTests
{
    [Fact]
    public void SetupChangeMarkersAndBeforeAfterUseIndependentStageFactors()
    {
        using var vm=new LiveViewModel();vm.WorkSupply="Always available";vm.Start();vm.Pause();for(int i=0;i<100;i++)vm.Step();
        Assert.Equal(StageProductivity.Default,vm.Live!.Session.Configuration.Productivity);
        foreach(var (label,value,expected) in new[]{("Development","1.5",new StageProductivity(1.5,1,1)),("Code Review","1.3",new StageProductivity(1.5,1.3,1)),("Testing","1.4",new StageProductivity(1.5,1.3,1.4))})
        {
            var before=JsonSerializer.Serialize(vm.Live.Session.Days);vm.BeginChange();vm.ChangeFields.Single(f=>f.Label==label+" Productivity (x)").Value=value;vm.ChangeLabel="Assumption";vm.ApplyChanges();
            Assert.Equal(before,JsonSerializer.Serialize(vm.Live.Session.Days));Assert.Equal(expected,vm.Live.Session.Configuration.Productivity);
            Assert.Contains(label+" Productivity 1.00x → ",LiveViewModel.Describe(vm.SelectedIntervention!));
            Assert.Contains("Productivity: Development",vm.ConfigurationDetails);Assert.Contains("0 of 20",vm.AfterPeriod);
            for(int i=0;i<20;i++)vm.Step();Assert.StartsWith("Complete periods",vm.ComparisonStatus);
        }
        Assert.Equal(new[]{100,120,140},vm.Live.Session.Changes.Select(c=>c.Day));
        Assert.Contains("Testing 1.40x",vm.ConfigurationDetails);
    }
    [Fact]
    public void EditorLoadsCapturesAndResetsProductivityAndRejectsInvalidChanges()
    {
        var editor=new MainWindowViewModel();editor.LoadConfiguration(new SimulationRequest{Productivity=new(1.5,1.2,1.4)});
        Assert.Equal(new StageProductivity(1.5,1.2,1.4),editor.CaptureSetup().Productivity);
        editor.ResetToBaseline();Assert.Equal(StageProductivity.Default,editor.CaptureSetup().Productivity);
        using var vm=new LiveViewModel();vm.Setup.DevelopmentProductivity="1.5";vm.Start();vm.Pause();
        Assert.Equal(1.5,vm.Live!.Session.Configuration.Productivity.Development);var old=JsonSerializer.Serialize(vm.Live.Capture());
        vm.BeginChange();vm.Draft.TestingProductivity="0";Assert.Throws<ScenarioValidationException>(()=>vm.ApplyChanges());
        Assert.Equal(old,JsonSerializer.Serialize(vm.Live.Capture()));
    }
}
