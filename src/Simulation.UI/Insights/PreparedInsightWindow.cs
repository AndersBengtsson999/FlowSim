using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Simulation.UI.Views;
using Simulation.UI.ViewModels;
using Simulation.UI.Help;
using Simulation.UI.Theming;
using System.Text.Json;
namespace Simulation.UI.Insights;

/// <summary>Preparation surface for the existing guided comparison view; no run on open.</summary>
public sealed class PreparedInsightWindow : Window
{
    public InsightExperimentProposal Proposal { get; }
    private readonly StackPanel content=new(){Spacing=14,Margin=new Thickness(20)};
    private readonly TextBox testers=new(){Width=120,HorizontalAlignment=HorizontalAlignment.Left};
    private readonly ComboBox period=new(){ItemsSource=new[]{10,20,40},SelectedItem=20,Width=120};
    private readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap};
    private readonly TextBlock proposedSummary=new(){TextWrapping=TextWrapping.Wrap};
    private readonly TextBlock proposedConfig=new(){TextWrapping=TextWrapping.Wrap};
    private readonly Button run=new(){Content="Confirm and run comparison"};
    public PreparedInsightWindow(InsightExperimentProposal proposal,Action returnToLive)
    {
        Proposal=proposal;Title="FLOW · Prepare guided comparison";Width=1000;Height=850;MinWidth=620;MinHeight=500;
        Background=Brush.Parse(FlowPalette.Content);
        Text("Prepare comparison",26);
        Text($"Source: {proposal.Source.Title}\nOriginal evidence: {proposal.Source.Period}\nFrozen baseline: Day {proposal.BaselineDay} · Seed {proposal.Seed}. LIVE may continue independently.");
        Text(proposal.Question);
        Text("Both branches use the captured work items, history, configuration and random-stream state. Only Tester count changes. No existing scenario or LIVE checkpoint is overwritten.");
        Text($"Baseline Testers: {proposal.BaselineConfiguration.Team.TesterCount}. Proposed Testers (editable):");
        testers.Text=proposal.ProposedTesters.ToString(System.Globalization.CultureInfo.InvariantCulture);content.Children.Add(testers);
        Avalonia.Automation.AutomationProperties.SetName(testers,"Proposed tester count");
        content.Children.Add(proposedSummary);
        Text("Matched evaluation period (simulated workdays):");content.Children.Add(period);
        Text("No simulation runs until you confirm. This is an isolated continuation, not a change to LIVE. Both branches use the existing automatic customer-launch decision policy used by guided baselines. Policies can consume randomness differently after intervention; common starting state does not guarantee paired later events.");
        var configurations=new StackPanel{Spacing=10};
        configurations.Children.Add(new TextBlock{Text="Baseline configuration",FontWeight=FontWeight.SemiBold});
        configurations.Children.Add(new TextBlock{Text=Describe(proposal.BaselineConfiguration),TextWrapping=TextWrapping.Wrap});
        configurations.Children.Add(new TextBlock{Text="Proposed configuration",FontWeight=FontWeight.SemiBold});configurations.Children.Add(proposedConfig);
        content.Children.Add(new Expander{Header="Inspect all baseline and proposed assumptions",Content=configurations,HorizontalAlignment=HorizontalAlignment.Stretch});
        var actions=new WrapPanel();actions.Children.Add(run);
        var cancel=new Button{Content="Cancel / close",Margin=new Thickness(8,0)};cancel.Click+=(_,_)=>Close();actions.Children.Add(cancel);
        var help=new Button{Content="Help · Prepared comparisons"};help.Click+=(_,_)=>GuideService.Open(this,"experiments.insight-evidence");actions.Children.Add(help);
        content.Children.Add(actions);content.Children.Add(status);
        run.Click+=async(_,_)=>await RunAsync(returnToLive);
        testers.TextChanged+=(_,_)=>UpdateDraft();period.SelectionChanged+=(_,_)=>UpdateDraft();
        Closed+=(_,_)=>proposal.Dispose();Content=new ScrollViewer{Content=content,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled};UpdateDraft();
    }
    private static string Describe(object config)=>JsonSerializer.Serialize(config,new JsonSerializerOptions{WriteIndented=true});
    private void Text(string text,int size=14)=>content.Children.Add(new TextBlock{Text=text,FontSize=size,TextWrapping=TextWrapping.Wrap});
    private bool UpdateDraft()
    {
        try
        {
            if(!int.TryParse(testers.Text,out var count))throw new ArgumentException("Enter a whole tester count.");
            Proposal.ProposedTesters=count;Proposal.Window=(int)(period.SelectedItem??20);
            var config=Proposal.ProposedConfiguration;
            proposedSummary.Text=$"Testers: {Proposal.BaselineConfiguration.Team.TesterCount} → {count} · Evaluation Days {Proposal.BaselineDay+1}–{Proposal.BaselineDay+Proposal.Window}. All other assumptions unchanged.";
            proposedConfig.Text=Describe(config);run.IsEnabled=!Proposal.Confirmed && !Proposal.Cancelled;status.Text="Ready for review. Preparing or cancelling changes nothing in LIVE.";return true;
        }
        catch(ArgumentException ex){status.Text=ex.Message;run.IsEnabled=false;proposedConfig.Text="Invalid proposal";return false;}
    }
    public async Task RunAsync(Action returnToLive)
    {
        if(!UpdateDraft() || Proposal.Confirmed || Proposal.Cancelled)return;
        run.IsEnabled=false;testers.IsEnabled=period.IsEnabled=false;status.Text="Running isolated branches using the existing guided comparison…";
        try
        {
            await Proposal.ConfirmAndRunAsync();
            if(Proposal.Cancelled)return;
            var model=Proposal.Result!;model.ReturnToLive=returnToLive;
            // Reuse the existing guided comparison surface and its authoritative result projection.
            content.Children.Add(new GuidedComparisonView{DataContext=model,MinHeight=700});
            status.Text="Comparison complete. Intervention refers to the isolated proposed branch; the original LIVE session is unchanged.";
        }
        catch(OperationCanceledException){status.Text="Cancelled. LIVE is unchanged.";}
        catch(Exception ex){status.Text="Comparison could not complete: "+ex.Message;}
    }
}
