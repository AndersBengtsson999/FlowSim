using Simulation.UI.ViewModels;
using Simulation.Core;
using Xunit;

namespace Simulation.UI.Tests;

public sealed class InterventionEditorTests
{
    [Theory]
    [InlineData("2", "2.0", false)]
    [InlineData("2", "2,0", false)]
    [InlineData("2", "2e0", false)]
    [InlineData("2", "2.000001", true)]
    [InlineData("2", "NaN", true)]
    [InlineData("2", "", true)]
    public void NumericChangesUseParsedValues(string current, string value, bool changed)
    {
        var draft = current;
        var field = new SimpleChangeField("Productivity (x)", current, () => draft, v => draft = v);
        var notifications = new List<string?>();
        field.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
        field.Value = value;
        Assert.Equal(changed, field.IsChanged);
        Assert.Contains(nameof(field.IsChanged), notifications);
        field.Value = current;
        Assert.False(field.IsChanged);
        Assert.Equal(current, field.Current);
        Assert.False(typeof(SimpleChangeField).GetProperty(nameof(field.Current))!.CanWrite);
        Assert.Equal("x", field.Unit);
        Assert.Equal("Productivity", field.DisplayLabel);
    }

    [Fact]
    public void IntegerRowsRespectExistingWholeNumberParsing()
    {
        var value = "5";
        var field = new SimpleChangeField("Developers", "5", () => value, v => value = v, kind: ChangeNumberKind.Integer);
        field.Value = "05"; Assert.False(field.IsChanged);
        field.Value = "5.0"; Assert.True(field.IsChanged); // invalid integer input, never silently treated as unchanged
    }

    [Fact]
    public void GroupsKeepEveryExistingBindingAndCalibrationSeparate()
    {
        using var vm = new LiveViewModel(); vm.Start(); vm.Pause(); vm.BeginChange();
        Assert.Equal(new[] { "Team & Capacity", "Productivity", "Technical Debt", "WIP" }, vm.ChangeGroups.Select(g => g.Title));
        Assert.Equal(vm.ChangeFields, vm.ChangeGroups.SelectMany(g => g.Fields));
        Assert.Equal(15, vm.ChangeFields.Count);
        var values = new[] { "7", "4", "80", "90", "1.5", "1.2", "2", "20", "50", "15", "30", "8", "6", "4", "2" };
        for (var i = 0; i < values.Length; i++) vm.ChangeFields[i].Value = values[i];
        vm.AdvancedDebtChanges.Single().Value = "2.0";
        var r = vm.Draft.CaptureSetup();
        Assert.Equal(7, r.DeveloperCount); Assert.Equal(4, r.TesterCount);
        Assert.Equal(.8, r.DeveloperAvailability); Assert.Equal(.9, r.TesterAvailability);
        Assert.Equal(new StageProductivity(1.5, 1.2, 2), r.Productivity);
        Assert.Equal(new TechnicalDebtSettings { ShortcutRate = .2, ShortcutEffortReduction = .5, Tolerance = .15, Repayment = .3, CreationFactor = 2 }, r.Debt);
        Assert.Equal(8, r.DevelopmentWipLimit); Assert.Equal(6, r.CodeReviewWipLimit); Assert.Equal(4, r.TestingWipLimit);
        Assert.Equal("2", vm.Draft.ReworkWipLimit);
        vm.ApplyChanges(); Assert.Equal(2, vm.Live!.Session.Configuration.Debt.CreationFactor);
    }

    [Theory]
    [InlineData("-1")] [InlineData("NaN")] [InlineData("Infinity")]
    public void CalibrationRejectsInvalidValues(string value)
    {
        var vm = new MainWindowViewModel { DebtCreationFactor = value };
        Assert.ThrowsAny<Exception>(() => vm.CaptureSetup().ToScenario());
    }

    [Theory]
    [InlineData("0", 0)] [InlineData("1.5", 1.5)] [InlineData("2.0", 2)] [InlineData("1000000", 1000000)]
    public void CalibrationSupportsNonnegativeFractionalAndLargeValues(string text, double expected)
    {
        var vm = new MainWindowViewModel { DebtCreationFactor = text };
        var request = vm.CaptureSetup(); request.ToScenario();
        var loaded = new MainWindowViewModel(); loaded.LoadConfiguration(request);
        Assert.Equal(expected, loaded.CaptureSetup().Debt.CreationFactor);
    }
}
