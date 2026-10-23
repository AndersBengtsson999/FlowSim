using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Markdig.Renderers.Html;
using Simulation.Application;

namespace Simulation.UI.Help;

public sealed record GuideChapter(string Id, string File, string Title);
public sealed record GuideSection(string Id, string Chapter, string File, string Title, string Keywords, string[] Related);
public sealed record GuideRegistry(GuideChapter[] Chapters, GuideSection[] Sections, Dictionary<string,string> Metrics);
public sealed record GuideSearchResult(string Id, string Title, string Snippet, int Score);

/// <summary>One offline source for rendering, stable navigation, contextual mappings and search.</summary>
public sealed class GuideCatalog
{
    public static MarkdownPipeline Pipeline { get; } = new MarkdownPipelineBuilder().UsePipeTables().UseGenericAttributes().DisableHtml().Build();
    public static GuideCatalog Default { get; } = new();
    private readonly Dictionary<string,string> bodies = new();
    private readonly Dictionary<string,string> sources = new();
    public GuideRegistry Registry { get; }
    public IReadOnlyDictionary<string,GuideSection> Sections { get; }
    public GuideCatalog()
    {
        Registry=JsonSerializer.Deserialize<GuideRegistry>(Read("registry.json"))!;
        Sections=Registry.Sections.ToDictionary(s=>s.Id,StringComparer.Ordinal);
        foreach(var chapter in Registry.Chapters)
        {
            var source=Read(chapter.File);sources.Add(chapter.Id,source);
            var headings=Markdown.Parse(source,Pipeline).OfType<HeadingBlock>().ToArray();
            for(var i=0;i<headings.Length;i++)
            {
                var id=headings[i].GetAttributes().Id ?? throw new InvalidDataException("Guide heading needs a stable ID.");
                if(!Sections.TryGetValue(id,out var section) || section.Chapter!=chapter.Id || section.File!=chapter.File)
                    throw new InvalidDataException("Guide heading is absent from registry: "+id);
                var end=i+1<headings.Length?headings[i+1].Span.Start:source.Length;
                bodies.Add(id,source[(headings[i].Span.End+1)..end].Trim());
            }
        }
        Validate();
    }
    public static Stream Open(string path)
    {
        if(path.Contains("..",StringComparison.Ordinal) || path.StartsWith('/') || path.Contains(':')) throw new InvalidDataException("Only packaged guide resources are supported.");
        return typeof(GuideCatalog).Assembly.GetManifestResourceStream("Guide/"+path) ?? throw new FileNotFoundException("Missing guide resource: "+path);
    }
    public static string Read(string path) { using var stream=Open(path);using var reader=new StreamReader(stream);return reader.ReadToEnd(); }
    public string Source(string chapter)=>sources[chapter];
    public string Body(string id)=>bodies[Resolve(id).Id];
    public GuideSection Resolve(string? id)=>Sections.TryGetValue(id??"",out var section)?section:Sections["getting-started"];
    public string Metric(TeamKpi metric)=>Registry.Metrics.GetValueOrDefault(metric.ToString(),"kpis-performance");
    public string Station(string? station)=>station switch
    {
        "Backlog"=>"workflow.backlog", "Development"=>"workflow.development", "Code Review" or "Review queue"=>"workflow.code-review",
        "Testing" or "Testing queue"=>"workflow.testing", "Ready for Release" or "Release"=>"releases.internal-release",
        "Technical Debt"=>"workflow.technical-debt", "Dependency queue"=>"workflow.dependencies", "Rework queue"=>"workflow.rework", _=>"live-game-view.station-inspection"
    };
    public IReadOnlyList<GuideSearchResult> Search(string? query)
    {
        var terms=(query??"").Split(' ',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        if(terms.Length==0)return [];
        return Registry.Sections.Select(section=>
        {
            var body=Plain(Body(section.Id));
            bool Has(string text,string term)=>text.Contains(term,StringComparison.OrdinalIgnoreCase);
            if(!terms.All(t=>Has(section.Title,t)||Has(section.Keywords,t)||Has(body,t)))return null;
            var score=terms.Sum(t=>(Has(section.Title,t)?100:0)+(Has(section.Keywords,t)?30:0)+(Has(body,t)?5:0));
            var match=terms.Select(t=>body.IndexOf(t,StringComparison.OrdinalIgnoreCase)).Where(i=>i>=0).DefaultIfEmpty(0).Min();
            var start=Math.Max(0,match-45);var snippet=body.Substring(start,Math.Min(180,body.Length-start));
            return new GuideSearchResult(section.Id,section.Title,(start>0?"…":"")+snippet+(start+snippet.Length<body.Length?"…":""),score);
        }).OfType<GuideSearchResult>().OrderByDescending(r=>r.Score).ThenBy(r=>r.Title).Take(40).ToArray();
    }
    public static string Plain(string markdown)=>Regex.Replace(Regex.Replace(markdown,@"!?\[([^\]]+)\]\([^)]*\)","$1"),@"[\s*`|#]+"," ").Trim();
    public void Validate(bool requireImages = false)
    {
        if(Registry.Chapters.Length!=8 || bodies.Count!=Sections.Count)throw new InvalidDataException("Guide chapters or sections are incomplete.");
        foreach(var section in Registry.Sections)
        {
            foreach(var related in section.Related)if(!Sections.ContainsKey(related))throw new InvalidDataException("Unknown related topic: "+related);
            foreach(var link in Markdown.Parse(Body(section.Id),Pipeline).Descendants<LinkInline>())
            {
                var url=link.Url??"";
                if(link.IsImage){ if(requireImages){using var image=Open(url);} }
                else if(!url.StartsWith('#') || !Sections.ContainsKey(url[1..]))throw new InvalidDataException("Unknown internal guide link: "+url);
            }
        }
        foreach(var metric in Enum.GetValues<TeamKpi>())
            if(!Registry.Metrics.TryGetValue(metric.ToString(),out var id)||!Sections.ContainsKey(id))throw new InvalidDataException("Missing KPI guide mapping: "+metric);
    }
}

/// <summary>History contains topic identities, never document offsets or simulation references.</summary>
public sealed class GuideNavigation(GuideCatalog catalog)
{
    private readonly List<string> history=["getting-started"];
    private int index;
    public string Current=>history[index];
    public bool CanBack=>index>0;
    public bool CanForward=>index<history.Count-1;
    public void Go(string? id)
    {
        var next=catalog.Resolve(id).Id;if(next==Current)return;
        history.RemoveRange(index+1,history.Count-index-1);history.Add(next);index++;
    }
    public void Back(){if(CanBack)index--;}
    public void Forward(){if(CanForward)index++;}
}
