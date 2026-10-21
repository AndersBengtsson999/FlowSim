using Simulation.Core;
namespace Simulation.UI.ViewModels;
public sealed partial class MainWindowViewModel
{
    public CustomerLaunchPresentation RunLaunchNotices { get; } = new();
    public string ProductVersionName { get; set; } = "Version 1.0";
    public string LaunchAfter { get; set; } = "";
    public string LaunchSprintLength { get; set; } = "10";
    public IReadOnlyList<string> LaunchUnits { get; } = ["Working days", "Sprints"];
    public string LaunchUnit { get; set; } = "Working days";
    private ProductLaunchSettings CaptureProductLaunch()
    {
        var settings = new ProductLaunchSettings(ProductVersionName, Integer(LaunchSprintLength, "Sprint length"), string.IsNullOrWhiteSpace(LaunchAfter) ? null : Integer(LaunchAfter, "Launch after"), LaunchUnit == "Sprints" ? LaunchPlanUnit.Sprints : LaunchPlanUnit.WorkingDays);
        settings.Validate(); return settings;
    }
    private void LoadProductLaunch(ProductLaunchSettings settings)
    { ProductVersionName = settings.Name; LaunchAfter = settings.After?.ToString() ?? ""; LaunchSprintLength = settings.SprintLength.ToString(); LaunchUnit = settings.Unit == LaunchPlanUnit.Sprints ? "Sprints" : "Working days"; }
    public string CustomerLaunchSummary => ProductDeliveryPresentation.Summary(result?.ProductDelivery, result?.WorkItems.ToDictionary(w => w.Id, w => w.DevelopmentStartedDay) ?? []);
    public string CustomerLaunchHistory => ProductDeliveryPresentation.History(result?.ProductDelivery);
}
