using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Simulation.UI.Theming;

namespace Simulation.UI.Help;

public sealed class GuideWindow : Window
{
    private readonly GuideCatalog catalog;
    public GuideNavigation Navigation { get; }
    private readonly GuideMarkdown markdown;
    private readonly Grid columns=new(){ColumnDefinitions=new("270,*,240")};
    private readonly StackPanel chapters=new(){Spacing=5,Margin=new(10)};
    private readonly StackPanel context=new(){Spacing=8,Margin=new(12)};
    private readonly ScrollViewer chapterScroll=new(){HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
    private readonly ScrollViewer contextScroll=new(){HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
    public ScrollViewer Reading { get; }=new(){HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Margin=new(20,12)};
    public TextBox SearchBox { get; }=new(){Watermark="Search the user guide... (e.g. cycle time, WIP, launch)",Margin=new(0,8,0,0)};
    private readonly ComboBox selector=new(){HorizontalAlignment=HorizontalAlignment.Stretch,Margin=new(0,8,0,0)};
    private readonly Button back=Button("←", "Back"), forward=Button("→","Forward");
    private readonly Dictionary<string,Button> sectionButtons=[];
    private readonly Dictionary<string,Expander> expanders=[];
    private string? renderedChapter;
    private bool selecting;
    public string CurrentSection=>Navigation.Current;
    private static IBrush Ink(string color)=>Brush.Parse(color);
    public GuideWindow(GuideCatalog catalog,GuideNavigation navigation)
    {
        this.catalog=catalog;Navigation=navigation;markdown=new(Navigate);
        Title="FLOW User Guide";Width=1320;Height=900;MinWidth=540;MinHeight=400;Background=Ink(FlowPalette.App);Foreground=Ink(FlowPalette.Text);RequestedThemeVariant=ThemeVariant.Dark;
        var focus=new Style(x=>x.OfType<Button>().Class(":focus-visible"));focus.Setters.Add(new Setter(BorderBrushProperty,Ink(FlowPalette.Accent)));focus.Setters.Add(new Setter(BorderThicknessProperty,new Thickness(2)));Styles.Add(focus);
        var root=new Grid{RowDefinitions=new("Auto,*")};var toolbar=new StackPanel{Margin=new(16,12),Spacing=0};
        var top=new Grid{ColumnDefinitions=new("*,Auto,Auto,Auto")};top.Children.Add(new TextBlock{Text="FLOW User Guide",FontSize=22,FontWeight=FontWeight.SemiBold,Foreground=Ink(FlowPalette.Text),VerticalAlignment=VerticalAlignment.Center});
        var close=Button("Close","Close user guide");close.Click+=(_,_)=>Close();back.Click+=(_,_)=>{Navigation.Back();ShowCurrent();};forward.Click+=(_,_)=>{Navigation.Forward();ShowCurrent();};
        foreach(var (button,index) in new[]{(back,1),(forward,2),(close,3)}){Grid.SetColumn(button,index);top.Children.Add(button);}
        toolbar.Children.Add(top);toolbar.Children.Add(SearchBox);toolbar.Children.Add(selector);root.Children.Add(toolbar);Grid.SetRow(columns,1);root.Children.Add(columns);Content=root;
        AutomationProperties.SetName(SearchBox,"Search the user guide");AutomationProperties.SetName(selector,"Choose guide chapter");AutomationProperties.SetName(Reading,"Guide content");
        chapterScroll.Content=chapters;contextScroll.Content=context;Grid.SetColumn(Reading,1);Grid.SetColumn(contextScroll,2);columns.Children.Add(chapterScroll);columns.Children.Add(Reading);columns.Children.Add(contextScroll);
        foreach(var chapter in catalog.Registry.Chapters)
        {
            var items=new StackPanel{Spacing=3};
            foreach(var section in catalog.Registry.Sections.Where(s=>s.Chapter==chapter.Id))
            {var link=Topic(section);items.Children.Add(link);sectionButtons.Add(section.Id,link);}
            var expander=new Expander{Header=GuideMarkdown.Text(chapter.Title,14),Content=items,HorizontalAlignment=HorizontalAlignment.Stretch};expanders.Add(chapter.Id,expander);chapters.Children.Add(expander);
        }
        selector.ItemsSource=catalog.Registry.Chapters.Select(c=>c.Title).ToArray();selector.SelectionChanged+=(_,_)=>{if(!selecting && selector.SelectedIndex>=0)Navigate(catalog.Registry.Chapters[selector.SelectedIndex].Id);};
        Reading.SizeChanged+=(_,_)=>ConstrainReadingWidth();
        SearchBox.TextChanged+=(_,_)=>Search();SizeChanged+=(_,_)=>Responsive();Opened+=(_,_)=>{Responsive();ShowCurrent();};Closed+=(_,_)=>markdown.Dispose();
        KeyDown+=(_,e)=>
        {
            if(e.Key==Key.F && (e.KeyModifiers.HasFlag(KeyModifiers.Control)||e.KeyModifiers.HasFlag(KeyModifiers.Meta))){SearchBox.Focus();SearchBox.SelectAll();e.Handled=true;}
            else if(e.Key==Key.Left && e.KeyModifiers.HasFlag(KeyModifiers.Alt)){Navigation.Back();ShowCurrent();e.Handled=true;}
            else if(e.Key==Key.Right && e.KeyModifiers.HasFlag(KeyModifiers.Alt)){Navigation.Forward();ShowCurrent();e.Handled=true;}
            else if(e.Key==Key.Escape){if(!string.IsNullOrWhiteSpace(SearchBox.Text))SearchBox.Text="";else Close();e.Handled=true;}
        };
    }
    private static Button Button(string label,string accessible)=>new(){Content=label,Margin=new(4,0,0,0),Padding=new(9,5),MinHeight=30, [AutomationProperties.NameProperty]=accessible};
    private Button Topic(GuideSection section)
    {
        var button=new Button{Content=GuideMarkdown.Text(section.Title,14),HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Left,Padding=new(7,5),Background=Brushes.Transparent,BorderThickness=new(1),BorderBrush=Brushes.Transparent,Cursor=new(StandardCursorType.Hand)};
        AutomationProperties.SetName(button,section.Title);button.Click+=(_,_)=>Navigate(section.Id);return button;
    }
    public void Navigate(string? id){Navigation.Go(id);ShowCurrent();}
    private void ShowCurrent()
    {
        if(!string.IsNullOrEmpty(SearchBox.Text)){selecting=true;SearchBox.Text="";selecting=false;renderedChapter=null;}
        var section=catalog.Resolve(Navigation.Current);
        if(renderedChapter!=section.Chapter){Reading.Content=markdown.Render(catalog.Source(section.Chapter));renderedChapter=section.Chapter;Reading.Offset=default;}
        foreach(var (id,button) in sectionButtons)
        {button.Background=id==section.Id?Ink(FlowPalette.Selected):Brushes.Transparent;button.BorderBrush=id==section.Id?Ink(FlowPalette.Accent):Brushes.Transparent;((TextBlock)button.Content!).Text=(id==section.Id?"› ":"")+catalog.Sections[id].Title;}
        expanders[section.Chapter].IsExpanded=true;
        selecting=true;selector.SelectedIndex=Array.FindIndex(catalog.Registry.Chapters,c=>c.Id==section.Chapter);selecting=false;
        context.Children.Clear();context.Children.Add(GuideMarkdown.Text("On this page",16));
        foreach(var topic in catalog.Registry.Sections.Where(s=>s.Chapter==section.Chapter))context.Children.Add(Topic(topic));
        context.Children.Add(GuideMarkdown.Text("Related topics",16));
        foreach(var id in section.Related)context.Children.Add(Topic(catalog.Sections[id]));
        if(section.Related.Length==0)context.Children.Add(GuideMarkdown.Text("Use chapter navigation to explore the guide.",13));
        ConstrainReadingWidth();
        back.IsEnabled=Navigation.CanBack;forward.IsEnabled=Navigation.CanForward;
        Dispatcher.UIThread.Post(()=>{if(CurrentSection==section.Id && markdown.Anchors.TryGetValue(section.Id,out var anchor) && Reading.Content is Control content){Reading.UpdateLayout();var point=anchor.TranslatePoint(default,content);if(point is { } p)Reading.Offset=new(0,Math.Max(0,p.Y));}},DispatcherPriority.Loaded);
    }
    private void Search()
    {
        if(selecting)return;
        if(string.IsNullOrWhiteSpace(SearchBox.Text)){renderedChapter=null;ShowCurrent();return;}
        var results=catalog.Search(SearchBox.Text);var panel=new StackPanel{Spacing=12,MaxWidth=780};panel.Children.Add(GuideMarkdown.Text($"Search results · {results.Count}",20));
        if(results.Count==0)panel.Children.Add(GuideMarkdown.Text("No matching topics. Try a shorter word, such as WIP, review, launch or SEK."));
        foreach(var result in results)
        {var link=Topic(catalog.Sections[result.Id]);panel.Children.Add(link);panel.Children.Add(GuideMarkdown.Text(result.Snippet,14));}
        Reading.Content=panel;ConstrainReadingWidth();Reading.Offset=default;renderedChapter=null;
    }
    private void ConstrainReadingWidth()
    { if(Reading.Content is Control content && Reading.Bounds.Width>0)content.Width=Math.Max(1,Math.Min(780,Reading.Bounds.Width-18)); }
    private void Responsive()
    {
        var wide=ClientSize.Width>=1200;var narrow=ClientSize.Width<850;
        chapterScroll.IsVisible=!narrow;selector.IsVisible=narrow;contextScroll.IsVisible=wide;
        columns.ColumnDefinitions[0].Width=new(narrow?0:wide?270:245);columns.ColumnDefinitions[2].Width=new(wide?240:0);
    }
}
