using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Automation;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Markdig.Extensions.Tables;
using Markdig.Renderers.Html;
using Simulation.UI.Theming;
using System.Text.Json;

namespace Simulation.UI.Help;

/// <summary>Native, offline Markdown AST renderer. No browser, scripts, HTML or network fetches.</summary>
public sealed class GuideMarkdown(Action<string> navigate) : IDisposable
{
    private readonly Dictionary<string,Bitmap> images=[];
    public Dictionary<string,Control> Anchors { get; }=[];
    private static IBrush Ink(string color)=>Brush.Parse(color);
    public static TextBlock Text(string text,double size=15)=>new(){Text=text,FontSize=size,TextWrapping=TextWrapping.Wrap,Foreground=Ink(FlowPalette.Text),LineHeight=size*1.5};
    public StackPanel Render(string source)
    {
        Anchors.Clear();var panel=new StackPanel{Spacing=12,MaxWidth=780,HorizontalAlignment=HorizontalAlignment.Stretch};
        foreach(var block in Markdown.Parse(source,GuideCatalog.Pipeline))panel.Children.Add(Block(block));return panel;
    }
    private Control Block(Block block)
    {
        switch(block)
        {
            case HeadingBlock heading:
                var title=Rich(heading.Inline,heading.Level==1?26:20);title.FontWeight=FontWeight.SemiBold;title.Margin=new(0,heading.Level==1?0:16,0,2);
                if(heading.GetAttributes().Id is { } id)Anchors.Add(id,title);return title;
            case ParagraphBlock paragraph:
                if(paragraph.Inline?.FirstChild is LinkInline{IsImage:true} image && image.NextSibling is null)return Picture(image);
                return Rich(paragraph.Inline);
            case ListBlock list:
                var rows=new StackPanel{Spacing=6};int number=int.TryParse(list.OrderedStart,out var start)?start:1;
                foreach(var item in list.OfType<ListItemBlock>())
                {
                    var row=new Grid{ColumnDefinitions=new("28,*")};row.Children.Add(Text(list.IsOrdered?$"{number++}.":"•"));
                    var body=new StackPanel{Spacing=6};Grid.SetColumn(body,1);foreach(var child in item)body.Children.Add(Block(child));row.Children.Add(body);rows.Children.Add(row);
                }return rows;
            case Table table:
                var grid=new Grid{ColumnDefinitions=new(string.Join(',',table.ColumnDefinitions.Select(_=>"*")))};
                var rowIndex=0;
                foreach(var row in table.OfType<TableRow>())
                {
                    grid.RowDefinitions.Add(new(GridLength.Auto));int column=0;
                    foreach(var cell in row.OfType<TableCell>())
                    {
                        var content=new StackPanel{Spacing=4};foreach(var child in cell)content.Children.Add(Block(child));
                        var border=new Border{Child=content,Padding=new(8),BorderBrush=Ink(FlowPalette.Border),BorderThickness=new(0,0,0,1),Background=row.IsHeader?Ink(FlowPalette.Elevated):null};
                        Grid.SetRow(border,rowIndex);Grid.SetColumn(border,column++);grid.Children.Add(border);
                    }rowIndex++;
                }return grid;
            case QuoteBlock quote:
                var quoted=new StackPanel{Spacing=6};foreach(var child in quote)quoted.Children.Add(Block(child));return new Border{Child=quoted,Padding=new(12),Background=Ink(FlowPalette.Panel),BorderBrush=Ink(FlowPalette.Accent),BorderThickness=new(3,0,0,0)};
            case CodeBlock code:return new Border{Child=Text(code.Lines.ToString(),14),Padding=new(10),Background=Ink(FlowPalette.Panel)};
            case ThematicBreakBlock:return new Border{Height=1,Background=Ink(FlowPalette.Border),Margin=new(0,8)};
            default:return Text("");
        }
    }
    private TextBlock Rich(ContainerInline? content,double size=15)
    {
        var text=Text("",size);text.Inlines=new InlineCollection();
        if(content is not null)foreach(var inline in content)Add(text.Inlines,inline);
        return text;
    }
    private void Add(InlineCollection target,Markdig.Syntax.Inlines.Inline inline)
    {
        switch(inline)
        {
            case LiteralInline literal:target.Add(new Run(literal.Content.ToString()));break;
            case CodeInline code:target.Add(new Run(code.Content){FontFamily=new("monospace"),Foreground=Ink(FlowPalette.Accent)});break;
            case LineBreakInline:target.Add(new Run(" "));break;
            case EmphasisInline emphasis:
                var span=new Span{FontWeight=emphasis.DelimiterCount>=2?FontWeight.SemiBold:FontWeight.Normal,FontStyle=emphasis.DelimiterCount==1?FontStyle.Italic:FontStyle.Normal};
                foreach(var child in emphasis)Add(span.Inlines,child);target.Add(span);break;
            case LinkInline link:
                var caption=InlineText(link);var button=new Button{Content=Text(caption,14),Padding=new(2,0),Background=Brushes.Transparent,Foreground=Ink(FlowPalette.Accent),Cursor=new(Avalonia.Input.StandardCursorType.Hand)};
                AutomationProperties.SetName(button,"Open topic: "+caption);
                button.Click+=(_,_)=>{if(link.Url is { } url && url.StartsWith('#'))navigate(url[1..]);};
                target.Add(new InlineUIContainer(button));break;
            case ContainerInline container:foreach(var child in container)Add(target,child);break;
        }
    }
    private static string InlineText(ContainerInline container)=>string.Concat(container.Select(i=>i is LiteralInline text?text.Content.ToString():i is ContainerInline nested?InlineText(nested):""));
    private Control Picture(LinkInline image)
    {
        var alt=InlineText(image);var path=image.Url??"";
        try
        {
            if(!images.TryGetValue(path,out var bitmap)){using var stream=GuideCatalog.Open(path);images.Add(path,bitmap=new Bitmap(stream));}
            var picture=new Image{Source=bitmap,Stretch=Stretch.Uniform,MaxHeight=480};AutomationProperties.SetName(picture,alt);
            var grid=new Grid();grid.Children.Add(picture);
            var annotations=JsonSerializer.Deserialize<Dictionary<string,double[][]>>(GuideCatalog.Read("assets/images/annotations.json"))!;
            if(annotations.TryGetValue(Path.GetFileName(path),out var points))
            {
                var canvas=new Canvas{IsHitTestVisible=false};grid.Children.Add(canvas);
                foreach(var (point,index) in points.Select((p,i)=>(p,i)))canvas.Children.Add(new Border{Width=25,Height=25,CornerRadius=new(13),Background=Ink(FlowPalette.Accent),BorderBrush=Ink(FlowPalette.App),BorderThickness=new(2),Child=new TextBlock{Text=(index+1).ToString(),Foreground=Ink(FlowPalette.OnAccent),FontWeight=FontWeight.Bold,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}});
                grid.SizeChanged+=(_,_)=>{var scale=Math.Min(grid.Bounds.Width/bitmap.PixelSize.Width,grid.Bounds.Height/bitmap.PixelSize.Height);var w=bitmap.PixelSize.Width*scale;var h=bitmap.PixelSize.Height*scale;for(var i=0;i<points.Length;i++){Canvas.SetLeft(canvas.Children[i],(grid.Bounds.Width-w)/2+points[i][0]*w-12);Canvas.SetTop(canvas.Children[i],(grid.Bounds.Height-h)/2+points[i][1]*h-12);}};
            }
            return new StackPanel{Spacing=4,Children={grid,Text(alt,12)}};
        }
        catch(Exception e) when(e is IOException or ArgumentException){return Text("Illustration unavailable. "+alt,13);}
    }
    public void Dispose(){foreach(var image in images.Values)image.Dispose();images.Clear();}
}
