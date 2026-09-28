using Simulation.Core;

namespace Simulation.UI.ViewModels;

public static class FlowPresentation
{
    public static IReadOnlyList<FlowStateRow> Rows(DailySnapshot d, SessionConfiguration? configuration = null, bool rework = false)
    {
        string Active(string description, int count, int? limit) => limit is null ? description : $"{count} / {limit} · {description}";
        var rows = new List<FlowStateRow>
        {
            new("Backlog", d.BacklogCount, "Not started", "#F1F5F9", "↓"),
            new("Development", d.DevelopmentCount, Active("Active stage · developer capacity", d.DevelopmentCount, configuration?.DevelopmentWipLimit), "#E8F1F7", "↓"),
            new("Waiting for Code Review", d.WaitingForCodeReviewCount, "QUEUE · awaiting admission", "#FFF2D8", "↓"),
            new("Code Review", d.CodeReviewCount, Active("Active stage · shared developer pool", d.CodeReviewCount, configuration?.CodeReviewWipLimit), "#E8F1F7", "↓"),
            new("Waiting for Testing", d.WaitingForTestingCount, "QUEUE · awaiting admission", "#FFF2D8", "↓"),
            new("Testing", d.TestingCount, Active("Active stage · tester capacity", d.TestingCount, configuration?.TestingWipLimit), "#E8F1F7", "↓"),
            new("Done", d.DoneCount, "Completed", "#E6F2ED", "")
        };
        if (rework)
        {
            rows.Add(new("Waiting for Rework", d.WaitingForReworkCount, "QUEUE · feedback from inspections", "#FFF2D8", "↓"));
            rows.Add(new("Rework", d.ReworkCount, Active("Returns to Code Review", d.ReworkCount, configuration?.Quality.ReworkWipLimit), "#E8F1F7", "↩"));
        }
        return rows;
    }
}
