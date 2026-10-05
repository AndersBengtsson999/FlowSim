using System.Text.Json;
using Simulation.Application;
using Simulation.Core;
using Simulation.UI.ViewModels;
using Xunit;
namespace Simulation.UI.Tests;

public sealed class TechnicalDebtViewModelTests
{
    [Theory]
    [InlineData(0,.1,"Within tolerance",0)] [InlineData(.05,.1,"Within tolerance",0)]
    [InlineData(.07,.1,"Upper tolerance range",0)] [InlineData(.1,.1,"Upper tolerance range",0)]
    [InlineData(.15,.1,"Above tolerance",.05)] [InlineData(0,0,"Within tolerance",0)] [InlineData(.5,0,"Above tolerance",.5)]
    public void BarZonesAndOverheadUseConfiguredToleranceWithTextAndUnclippedScale(double ratio,double tolerance,string zone,double overhead)
    {
        var bar=DebtBarState.From(new(ratio*100,100),new(){Tolerance=tolerance});
        Assert.Equal(zone,bar.Zone);Assert.Equal(overhead,bar.Overhead,12);Assert.True(bar.ScaleMaximum>ratio);
        Assert.Equal(tolerance/bar.ScaleMaximum,bar.YellowEnd);Assert.Equal(.7*tolerance/bar.ScaleMaximum,bar.GreenEnd);
        Assert.InRange(bar.Marker,0,1);Assert.InRange(bar.YellowEnd,0,1);
    }
    [Fact]
    public void ConfigurationInterventionsBarTrendAndCheckpointNotificationsTrackLiveState()
    {
        using var vm=new LiveViewModel();vm.WorkSupply="Always available";vm.Start();vm.Pause();Assert.False(vm.DebtVisible);
        for(int i=0;i<100;i++)vm.Step();Assert.Equal(0,vm.Live!.Session.DebtState.Amount);
        vm.CheckpointCommand.Execute(null);var empty=vm.Checkpoints.Single();var notifications=0;vm.PropertyChanged+=(_,_)=>notifications++;
        vm.BeginChange();vm.ChangeFields.Single(f=>f.Label=="Shortcut Rate (%)").Value="40";vm.ChangeFields.Single(f=>f.Label=="Debt Tolerance (%)").Value="5";vm.ApplyChanges();
        Assert.Contains("Shortcut Rate",vm.LatestIntervention);Assert.Contains("effective Day 101",vm.LatestIntervention);Assert.True(vm.DebtVisible);
        for(int i=0;i<100;i++)vm.Step();Assert.True(notifications>100);Assert.True(vm.Live.Session.DebtState.Amount>0);
        Assert.Equal(vm.Live.Session.DebtState.Ratio,vm.DebtBar.Ratio);Assert.Contains("Technical Debt:",vm.DebtText);Assert.Contains("Developed scope",vm.DebtDetails);
        vm.TrendRange="Full Session";vm.TrendMetric=vm.TrendMetrics.Single(m=>m.Metric==LiveTrendMetric.TechnicalDebtRatio);
        Assert.Equal(100*vm.DebtBar.Ratio,vm.Trend.Points.Last().Value);var state=JsonSerializer.Serialize(vm.Live.Capture());
        _=vm.DebtBar;_=vm.DebtDetails;Assert.Equal(state,JsonSerializer.Serialize(vm.Live.Capture()));
        vm.BeginChange();vm.Draft.DebtTolerance="50";vm.ApplyChanges();Assert.Equal(.5,vm.DebtBar.Tolerance);Assert.Equal(0,vm.DebtBar.Overhead);
        vm.SelectedCheckpoint=empty;vm.RestoreCommand.Execute(null);Assert.Equal(100,vm.Day);Assert.Equal(0,vm.DebtBar.Ratio);Assert.False(vm.DebtVisible);
        Assert.All(vm.Trend.Points,p=>Assert.Equal(0,p.Value));vm.Reset();Assert.Empty(vm.Trend.Points);
    }
    [Fact]
    public void CommonEditorRoundTripsDebtIncludingHiddenFactorsAndRejectsInvalidPercentages()
    {
        var editor=new MainWindowViewModel();var request=new SimulationRequest{Debt=new(){ShortcutRate=.4,ShortcutEffortReduction=.25,Tolerance=.12,Repayment=.3,CreationFactor=.8,ImpactFactor=1.2}};
        editor.LoadConfiguration(request);Assert.Equal(request.Debt,editor.CaptureSetup().Debt);
        editor.DebtRepayment="101";Assert.Throws<ScenarioValidationException>(()=>editor.CaptureSetup().ToScenario());
        editor.ResetToBaseline();Assert.Equal(new TechnicalDebtSettings(),editor.CaptureSetup().Debt);
    }
}
