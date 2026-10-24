using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Simulation.Application;
using Simulation.UI.Help;
using Simulation.UI.ViewModels;
using Simulation.UI.Theming;

namespace Simulation.UI.Insights;

/// <summary>One modeless reader per LIVE workspace; selection alone acknowledges the displayed evidence.</summary>
public sealed class InsightsWindow : Window
{
    private readonly LiveViewModel model;
    private readonly Action<string> station;
    private readonly Action<TeamKpi> kpi;
    private readonly TextBlock summary=new(){TextWrapping=TextWrapping.Wrap};
    private readonly ListBox list=new(){MinHeight=100};
    private readonly StackPanel details=new(){Spacing=14,Margin=new Thickness(18)};
    private readonly Grid body=new(){ColumnDefinitions=new("320,*"),RowDefinitions=new("*")};
    private readonly ScrollViewer reader;
    private bool refreshing;
    private bool metricsExpanded;
    private string[] listLabels=[];
    public string? SelectedInsightId { get; private set; }
    public InsightsWindow(LiveViewModel model,Action<string> station,Action<TeamKpi> kpi)
    {
        this.model=model;this.station=station;this.kpi=kpi;
        Title="Simulation Insights";Width=1180;Height=820;MinWidth=620;MinHeight=500;
        Background=Brush.Parse(FlowPalette.Content);
        list.Background=Brush.Parse(FlowPalette.Panel);
        var root=new Grid {RowDefinitions=new("Auto,*"),Margin=new Thickness(18)};
        var header=new StackPanel{Spacing=8,Margin=new Thickness(0,0,0,16)};
        header.Children.Add(new TextBlock{Text="Simulation Insights",FontSize=26,FontWeight=FontWeight.SemiBold});
        header.Children.Add(summary);root.Children.Add(header);
        reader=new ScrollViewer{Content=details,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled};
        body.Children.Add(list);body.Children.Add(reader);Grid.SetColumn(reader,1);Grid.SetRow(body,1);root.Children.Add(body);Content=root;
        list.ItemTemplate=new Avalonia.Controls.Templates.FuncDataTemplate<TrackedInsight>((item,_)=>new TextBlock {Text=item?.Label,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(6),MaxWidth=280,HorizontalAlignment=HorizontalAlignment.Left});
        list.SelectionChanged+=(_,_)=>{if(!refreshing && list.SelectedItem is TrackedInsight item)Select(item.Insight.Id);};
        SizeChanged+=(_,_)=>{
            var narrow=ClientSize.Width<850;
            body.ColumnDefinitions=new(narrow?"*":"320,*");body.RowDefinitions=new(narrow?"170,*":"*");
            Grid.SetColumn(reader,narrow?0:1);Grid.SetRow(reader,narrow?1:0);
            if(list.SelectedItem is not null)list.ScrollIntoView(list.SelectedItem);
        };
        model.PropertyChanged+=Changed;
        Closed+=(_,_)=>model.PropertyChanged-=Changed;
        Refresh();
    }
    private void Changed(object? sender,System.ComponentModel.PropertyChangedEventArgs e)
    { if(e.PropertyName==nameof(LiveViewModel.InsightsReport))Refresh(); }
    private void Refresh()
    {
        var report=model.InsightsReport;
        summary.Text=report is null?"Start a LIVE session to observe evidence.":$"Day {report.Day} · Capacity evidence: {report.CapacityPeriod}\nQueue classification: last four post-change observations. {model.Insights.Items.Count(i=>i.Insight.Relevant)} active · {model.Insights.NewCount} new · {model.Insights.UpdatedCount} updated. Evidence strength is not causal confidence.";
        refreshing=true;
        var items=model.Insights.Items;
        var labels=items.Select(i=>i.Label).ToArray();
        if(!labels.SequenceEqual(listLabels)) { list.ItemsSource=items;listLabels=labels; }
        list.SelectedItem=((IEnumerable<TrackedInsight>?)list.ItemsSource)?.FirstOrDefault(i=>i.Insight.Id==SelectedInsightId);
        refreshing=false;
        if(items.FirstOrDefault(i=>i.Insight.Id==SelectedInsightId) is { } selected)Show(selected);
        else {SelectedInsightId=null;details.Children.Clear();Text("Select an insight","Select a row to review its observation, evidence and optional experiment. Nothing is applied automatically.");}
    }
    public void Select(string id)
    {
        var item=model.Insights.Items.FirstOrDefault(i=>i.Insight.Id==id);if(item is null)return;
        SelectedInsightId=id;model.ReviewInsight(id);Refresh();reader.Offset=default;
        if(list.SelectedItem is not null)list.ScrollIntoView(list.SelectedItem);
    }
    private void Show(TrackedInsight tracked)
    {
        var i=tracked.Insight;details.Children.Clear();
        Text(i.Title,$"{(i.Category==InsightCategory.QueuePressure?"Queue pressure":"Capacity imbalance")} · {tracked.Status}\nObserved: {i.Period}");
        Text("Observation",i.Observation);
        Text("Evidence",i.Evidence+"\n"+Strength(i.Strength));

        Text("Possible explanation",i.Explanation);
        Text("Suggested experiment",i.Experiment);
        if(!i.Relevant)Text("Current relevance", "No active alert. Evidence above may describe an earlier observation period; check its dates before acting.");
        var links=new WrapPanel();
        foreach(var name in i.Stations)Button(links,"Inspect "+name,()=>station(name));
        foreach(var metric in i.Kpis)Button(links,KpiDashboard.Definitions.Single(d=>d.Id==metric).Name,()=>kpi(metric));
        Button(links,"Help · Simulation Insights",()=>GuideService.Open(this,i.Topic));details.Children.Add(links);
        var metricPanel=new StackPanel{Spacing=8,Margin=new Thickness(8)};
        foreach(var metric in i.Metrics)metricPanel.Children.Add(new TextBlock{Text=metric.ToString(),TextWrapping=TextWrapping.Wrap});
        var metricDetails=new Expander{Header="Recorded metrics · "+i.Period,Content=metricPanel,IsExpanded=metricsExpanded,HorizontalAlignment=HorizontalAlignment.Stretch};
        metricDetails.Expanded+=(_,_)=>metricsExpanded=true;metricDetails.Collapsed+=(_,_)=>metricsExpanded=false;
        details.Children.Add(metricDetails);
        // A changed classification remains Updated until explicitly selected/reviewed, even if its details are visible.
        if(tracked.Status is InsightStatus.New or InsightStatus.Updated)
        { var acknowledge=new WrapPanel();Button(acknowledge,"Mark this evidence reviewed",()=>Select(i.Id));details.Children.Add(acknowledge); }
    }
    private void Text(string heading,string value)
    {
        details.Children.Add(new TextBlock{Text=heading,FontSize=18,FontWeight=FontWeight.SemiBold,TextWrapping=TextWrapping.Wrap});
        details.Children.Add(new TextBlock{Text=value,TextWrapping=TextWrapping.Wrap});
    }
    private static void Button(Panel panel,string label,Action action)
    {var button=new Button{Content=label,Margin=new Thickness(0,4,8,4)};button.Click+=(_,_)=>action();panel.Children.Add(button);}
    public static string Strength(EvidenceStrength strength)=>strength switch {
        EvidenceStrength.InsufficientEvidence=>"Insufficient evidence: fewer than four comparable observations.",
        EvidenceStrength.ObservableTrend=>"Observable trend. This is evidence strength, not the probability of a cause.",
        EvidenceStrength.TrendWithCapacityEvidence=>"Trend with supporting capacity evidence. This does not establish the underlying cause.",
        _=>"Observed condition. This does not establish the underlying cause."};
}
