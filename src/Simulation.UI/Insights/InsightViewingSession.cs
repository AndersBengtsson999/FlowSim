using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Simulation.UI.Insights;

public sealed class InsightDisplayEntry : INotifyPropertyChanged
{
    public TrackedInsight Item { get; private set; }
    public int Group { get; }
    public string? Heading { get; internal set; }
    public bool Available { get; private set; } = true;
    public string Id => Item.Insight.Id;
    public string Title => InsightPresentation.Title(Item);
    public string Status => Available ? Item.Secondary : "Unavailable · no longer in this session";
    public string Label => Item.Label;
    public event PropertyChangedEventHandler? PropertyChanged;
    public InsightDisplayEntry(TrackedInsight item, int group, string? heading)
    { Item=item; Group=group; Heading=heading; }
    internal void Update(TrackedInsight? item)
    {
        if(item==Item && Available==(item is not null))return;
        if(item is not null)Item=item;
        Available=item is not null;
        PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(null));
    }
}

/// <summary>Window-local placement and identity; analytical lifecycle remains authoritative.</summary>
public sealed class InsightViewingSession
{
    public ObservableCollection<InsightDisplayEntry> Entries { get; } = [];
    public string? SelectedId { get; private set; }
    private bool initialized;
    public void Update(IEnumerable<TrackedInsight> source)
    {
        var current=InsightPresentation.Ordered(source);
        var byId=current.ToDictionary(i=>i.Insight.Id,StringComparer.Ordinal);
        foreach(var entry in Entries)entry.Update(byId.GetValueOrDefault(entry.Id));
        var known=Entries.Select(e=>e.Id).ToHashSet(StringComparer.Ordinal);
        foreach(var item in current.Where(i=>!known.Contains(i.Insight.Id))) {
            var group=InsightPresentation.Group(item);
            var sameGroup=Entries.Any(e=>e.Group==group);
            var heading=sameGroup?null:group switch {0=>"Active findings",1=>"Other current observations",_=>"Previous findings"};
            var index=0;
            while(index<Entries.Count && Entries[index].Group<=group)index++;
            Entries.Insert(index,new(item,group,heading));
        }
        if(!initialized && current.Count>0) {
            SelectedId=InsightPresentation.Selection(current,null);initialized=true;
        }
    }
    public bool Select(string id)
    {
        if(!Entries.Any(e=>e.Id==id))return false;
        SelectedId=id;return true;
    }
}
