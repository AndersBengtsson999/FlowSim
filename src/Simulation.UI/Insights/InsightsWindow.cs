using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Simulation.Application;
using Simulation.UI.Help;
using Simulation.UI.ViewModels;
using Simulation.UI.Theming;
using Simulation.UI.Controls;

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
    private bool evidenceExpanded;
    private bool measurementsExpanded;
    private readonly HashSet<string> groupStarts=[];
    private string[] listLabels=[];
    private string? selectedChartTitle;
    public PreparedInsightWindow? PreparedWindow { get; private set; }
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
        list.ItemTemplate=new Avalonia.Controls.Templates.FuncDataTemplate<TrackedInsight>((item,_)=>
        {
            var row=new StackPanel{Spacing=4,Margin=new Thickness(6)};
            if(item is not null && groupStarts.Contains(item.Insight.Id))
                row.Children.Add(new TextBlock{Text=InsightPresentation.GroupTitle(item),FontSize=12,Margin=new Thickness(0,10,0,4),Foreground=Brush.Parse(FlowPalette.Secondary),TextWrapping=TextWrapping.Wrap});
            row.Children.Add(new TextBlock{Text=item is null?"":InsightPresentation.Title(item),FontWeight=FontWeight.SemiBold,TextWrapping=TextWrapping.Wrap});
            row.Children.Add(new TextBlock{Text=item?.Secondary,FontSize=12,Foreground=Brush.Parse(FlowPalette.Secondary),TextTrimming=TextTrimming.CharacterEllipsis});
            ToolTip.SetTip(row,item?.Label);return row;
        });
        list.SelectionChanged+=(_,_)=>{if(!refreshing && list.SelectedItem is TrackedInsight item)Select(item.Insight.Id);};
        SizeChanged+=(_,_)=>{
            var narrow=ClientSize.Width<850;
            body.ColumnDefinitions=new(narrow?"*":"320,*");body.RowDefinitions=new(narrow?"170,*":"*");
            Grid.SetColumn(reader,narrow?0:1);Grid.SetRow(reader,narrow?1:0);
            Avalonia.Threading.Dispatcher.UIThread.Post(()=>{if(list.SelectedItem is not null)list.ScrollIntoView(list.SelectedItem);},Avalonia.Threading.DispatcherPriority.Loaded);
        };
        model.PropertyChanged+=Changed;
        Closed+=(_,_)=>{model.PropertyChanged-=Changed;PreparedWindow?.Close();};
        Refresh();
    }
    private void Changed(object? sender,System.ComponentModel.PropertyChangedEventArgs e)
    { if(e.PropertyName==nameof(LiveViewModel.InsightsReport))Refresh(); }
    private void Refresh()
    {
        var report=model.InsightsReport;
        summary.Text=report is null?"Start a LIVE session to observe evidence.":$"Day {report.Day} · {InsightPresentation.Overview(model.Insights.Items)}";
        refreshing=true;
        var items=InsightPresentation.Ordered(model.Insights.Items);
        SelectedInsightId = InsightPresentation.Selection(items,SelectedInsightId);
        var labels=items.Select(i=>i.Label+i.Secondary+InsightPresentation.Group(i)).ToArray();
        if(!labels.SequenceEqual(listLabels)) {
            groupStarts.Clear();foreach(var group in items.GroupBy(InsightPresentation.Group))groupStarts.Add(group.First().Insight.Id);
            list.ItemsSource=items;listLabels=labels;
        }
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
        Text(InsightPresentation.Title(tracked),$"{tracked.StatusLabel} · {InsightPresentation.Range(i.Period)}");
        if(!i.Relevant)Text("Current relevance", tracked.Status==InsightStatus.NoLongerRelevant
            ? "This finding is no longer active. Its evidence may describe an earlier period; check the dates before acting."
            : "This is a current observation, not an active alert.");
        Text("What FLOW observed",InsightPresentation.Plain(i.Observation));
        Text("What it might mean",i.Explanation);
        Text("What to explore next",i.Experiment);
        if(InsightExperimentProposal.Supports(i,model.Live))
        {
            var prepare=new WrapPanel();Button(prepare,"Prepare comparison",()=>PrepareComparison(i.Id));details.Children.Add(prepare);
        }

        var links=new WrapPanel();
        foreach(var name in i.Stations)Button(links,"Inspect "+name,()=>station(name));
        foreach(var metric in i.Kpis)Button(links,KpiDashboard.Definitions.Single(d=>d.Id==metric).Name,()=>kpi(metric));
        Button(links,"Help · Simulation Insights",()=>GuideService.Open(this,i.Topic));details.Children.Add(links);
        var evidence=new StackPanel{Spacing=12,Margin=new Thickness(8)};
        evidence.Children.Add(new TextBlock{Text="Observation window: "+i.Period+"\n"+i.Evidence+"\n"+Strength(i.Strength),TextWrapping=TextWrapping.Wrap});
        AddEvidenceCharts(i,evidence);
        var metricPanel=new StackPanel{Spacing=8,Margin=new Thickness(8)};
        foreach(var metric in i.Metrics)metricPanel.Children.Add(new TextBlock{Text=metric.ToString(),TextWrapping=TextWrapping.Wrap});
        var metricDetails=new Expander{Header="Recorded metrics · "+InsightPresentation.Range(i.Period),Content=metricPanel,IsExpanded=metricsExpanded,HorizontalAlignment=HorizontalAlignment.Stretch};
        metricDetails.Expanded+=(_,_)=>metricsExpanded=true;metricDetails.Collapsed+=(_,_)=>metricsExpanded=false;
        evidence.Children.Add(metricDetails);
        var supporting=new Expander{Header="Supporting evidence · charts, measurements & method",Content=evidence,IsExpanded=evidenceExpanded,HorizontalAlignment=HorizontalAlignment.Stretch};
        supporting.Expanded+=(_,_)=>evidenceExpanded=true;supporting.Collapsed+=(_,_)=>evidenceExpanded=false;
        details.Children.Add(supporting);
        // A changed classification remains Updated until explicitly selected/reviewed, even if its details are visible.
        if(tracked.Status is InsightStatus.New or InsightStatus.Updated)
        { var acknowledge=new WrapPanel();Button(acknowledge,"Mark this evidence reviewed",()=>Select(i.Id));details.Children.Add(acknowledge); }
    }
    public void PrepareComparison(string id)
    {
        if(PreparedWindow is not null){PreparedWindow.Activate();return;}
        var insight=model.Insights.Items.FirstOrDefault(x=>x.Insight.Id==id)?.Insight;
        if(insight is null || InsightExperimentProposal.Prepare(insight,model.Live) is not { } proposal)return;
        var window=new PreparedInsightWindow(proposal,()=>{ station("Testing"); });
        PreparedWindow=window;window.Closed+=(_,_)=>PreparedWindow=null;window.Show(this);
    }
    private void AddEvidenceCharts(SimulationInsight insight,StackPanel evidence)
    {
        if(insight.Charts.Count==0)
        {evidence.Children.Add(new TextBlock{Text="No recorded chart observations in this period.",TextWrapping=TextWrapping.Wrap});return;}
        var selector=new ComboBox{ItemsSource=insight.Charts,HorizontalAlignment=HorizontalAlignment.Stretch};
        selector.SelectedItem=insight.Charts.FirstOrDefault(c=>c.Title==selectedChartTitle)??insight.Charts[0];
        var panel=new StackPanel{Spacing=6};
        void Draw()
        {
            if(selector.SelectedItem is not InsightChart chart)return;
            selectedChartTitle=chart.Title;panel.Children.Clear();
            panel.Children.Add(new TextBlock{Text=$"{chart.Title} · {chart.Unit} · {InsightPresentation.Range(chart.Period)}\nRecorded samples: {chart.Period.Observations}. Station classification uses at most four post-change observations.",TextWrapping=TextWrapping.Wrap,FontSize=12});
            panel.Children.Add(new TextBlock{Text=chart.PrimaryLabel+" (blue dots)"+(chart.SecondaryLabel is null?"":" · "+chart.SecondaryLabel+" (purple squares)"),TextWrapping=TextWrapping.Wrap,FontSize=12});
            panel.Children.Add(new LivePerformanceTrendChart{Series=chart.Primary,ComparisonSeries=chart.Secondary,Unit=chart.Unit,SamplesOnly=true,Height=220});
            panel.Children.Add(new TextBlock{Text="Samples only; no interpolation. Queue size is not daily growth. Endpoint growth and correlated measurements do not establish sustained pressure or a cause.",TextWrapping=TextWrapping.Wrap,FontSize=12,Foreground=Brush.Parse(FlowPalette.Secondary)});
            var values=new StackPanel{Spacing=4};foreach(var row in chart.Measurements)values.Children.Add(new TextBlock{Text=row,TextWrapping=TextWrapping.Wrap,FontSize=12});
            var measurements=new Expander{Header="Exact chart measurements",Content=values,IsExpanded=measurementsExpanded,HorizontalAlignment=HorizontalAlignment.Stretch};
            measurements.Expanded+=(_,_)=>measurementsExpanded=true;measurements.Collapsed+=(_,_)=>measurementsExpanded=false;
            panel.Children.Add(measurements);
        }
        selector.SelectionChanged+=(_,_)=>Draw();Draw();evidence.Children.Add(selector);evidence.Children.Add(panel);
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
