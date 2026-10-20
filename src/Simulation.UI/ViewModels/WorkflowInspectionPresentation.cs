using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Simulation.UI.ViewModels;

/// <summary>Interaction state belongs to this view, never to the simulation or persistence document.</summary>
public sealed class InspectionItemViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public InspectedWorkItem Data { get; private set; }
    public string Id => Data.Id;
    private bool expanded;
    public bool Expanded { get => expanded; set { if (expanded == value) return; expanded = value; Changed(nameof(Expanded)); } }
    public string GroupHeading { get; private set; } = "";
    public bool ShowGroup => GroupHeading.Length > 0;
    public InspectionItemViewModel(InspectedWorkItem data) => Data = data;
    public void Update(InspectedWorkItem data)
    {
        if (Data == data) return;
        Data = data; Changed(nameof(Data));
    }
    public void SetGroupHeading(string heading)
    {
        if (GroupHeading == heading) return;
        GroupHeading = heading; Changed(nameof(GroupHeading)); Changed(nameof(ShowGroup));
    }
    private void Changed(string property) => PropertyChanged?.Invoke(this, new(property));
}

public sealed class WorkflowInspectionPresentation : INotifyPropertyChanged
{
    public ObservableCollection<InspectionItemViewModel> Items { get; } = [];
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<bool>? Updating;
    public event Action<bool>? Updated;
    private Guid session;
    private string selection = "";
    public string MembershipNotice { get; private set; } = "";
    public void Refresh(Guid identity, StationDetails details)
    {
        var reset = identity != session || selection != details.Title;
        Updating?.Invoke(reset);
        if (reset) { Items.Clear(); MembershipNotice = ""; }
        session = identity; selection = details.Title;
        var incoming = details.Items.OrderBy(i => i.GroupOrder).ToArray();
        var counts = incoming.GroupBy(i => i.Group).ToDictionary(g => g.Key, g => g.Count());
        var ids = incoming.Select(i => i.Id).ToHashSet();
        var removed = Items.Where(i => !ids.Contains(i.Id)).ToArray();
        foreach (var item in removed) Items.Remove(item);
        if (removed.Length > 0) MembershipNotice = $"Last change: {removed.Length} item(s) left this selection. The list shows current membership.";
        var existing = Items.ToDictionary(i => i.Id);
        for (var index = 0; index < incoming.Length; index++)
        {
            var data = incoming[index];
            if (!existing.TryGetValue(data.Id, out var row)) { row = new(data); Items.Insert(index, row); }
            else { row.Update(data); if (!ReferenceEquals(Items[index], row)) Items.Move(Items.IndexOf(row), index); }
            row.SetGroupHeading(index == 0 || incoming[index - 1].Group != data.Group ? $"{data.Group} · {counts[data.Group]}" : "");
        }
        PropertyChanged?.Invoke(this, new(nameof(MembershipNotice)));
        Updated?.Invoke(reset);
    }
}
