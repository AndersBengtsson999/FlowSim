using Simulation.Application;
using Simulation.UI.Help;
using Simulation.UI.Controls.GameWorld;
using Xunit;
namespace Simulation.UI.Tests;
public class GuideTests
{
    [Fact] public void AllChaptersSectionsLinksMetricsAndImagesArePackaged()
    {
        var guide=new GuideCatalog();guide.Validate(requireImages:true);
        Assert.Equal(8,guide.Registry.Chapters.Length);
        Assert.Equal(guide.Registry.Sections.Length,guide.Registry.Sections.Select(s=>s.Id).Distinct().Count());
        Assert.All(guide.Registry.Chapters,c=>Assert.NotEmpty(guide.Source(c.Id)));
        Assert.All(Enum.GetValues<TeamKpi>(),k=>Assert.StartsWith("kpi.",guide.Metric(k)));
        Assert.All(StationMap.InspectionOptions,s=>Assert.NotEqual("live-game-view.station-inspection",guide.Station(s)));
    }
    [Theory]
    [InlineData("CYCLE TIME","kpi.cycle-time")]
    [InlineData("unlaunched","kpi.unlaunched-cost")]
    [InlineData("85,000","economics.resource-costs")]
    [InlineData("FIFO","reference.glossary")]
    [InlineData("postpon","releases.scheduled")]
    public void SearchIndexesTitlesBodyGlossaryAndPartialTerms(string query,string id)
    {var results=GuideCatalog.Default.Search(query);Assert.Contains(results,r=>r.Id==id);Assert.All(results,r=>Assert.NotEmpty(r.Snippet));}
    [Fact] public void SearchIsCaseInsensitiveAndTitlesRankBeforeBody()
    {
        var guide=GuideCatalog.Default;
        Assert.Equal(guide.Search("cycle time"),guide.Search("CYCLE TIME"));
        Assert.Equal("kpi.cycle-time",guide.Search("cycle time")[0].Id);
        Assert.Empty(guide.Search("zzzznonexistentterm"));Assert.Empty(guide.Search(" "));
    }
    [Fact] public void NavigationUsesStableIdsAndPreservesHistory()
    {
        var navigation=new GuideNavigation(GuideCatalog.Default);
        navigation.Go("workflow.testing");navigation.Go("kpi.cycle-time");navigation.Back();Assert.Equal("workflow.testing",navigation.Current);
        navigation.Forward();Assert.Equal("kpi.cycle-time",navigation.Current);
        navigation.Back();navigation.Go("economics.cost-allocation");Assert.False(navigation.CanForward);
        navigation.Go("unknown");Assert.Equal("getting-started",navigation.Current);
        Assert.Equal("kpis-performance",GuideCatalog.Default.Metric((TeamKpi)999));
    }
    [Fact] public void ResourcePathsCannotEscapeTheGuide()
    {Assert.Throws<InvalidDataException>(()=>GuideCatalog.Open("../secret"));Assert.Throws<InvalidDataException>(()=>GuideCatalog.Open("https://example.com/image.png"));}
}
