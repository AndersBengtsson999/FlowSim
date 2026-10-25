using Avalonia;
using Simulation.UI.Views;
using Simulation.UI.Controls.GameWorld;
using Xunit;
namespace Simulation.UI.Tests;
public class WorkshopLayoutTests
{
    [Theory][InlineData(1200,300)][InlineData(1000,180)][InlineData(750,-400)]
    public void DetailsAndKpisCannotShrinkWorkshopBelowReadableAspectBound(double width,double remaining)
    {
        var height=WorkshopLayout.Height(width,620,remaining);
        Assert.Equal(Math.Min(400,width*620/1080),height,5);
        Assert.True(height<=width*620/1080);
    }
    [Fact] public void ContainerResizeKeepsManualZoomAndFitCentersWholeScene()
    {
        var camera=new WorkshopViewport();camera.Resize(new(1200,WorkshopLayout.Height(1200,620,500)),new(1080,620));
        camera.ZoomAt(1.6,new(600,250));camera.Pan(new(20,10));
        camera.Resize(new(750,WorkshopLayout.Height(750,620,150)),new(1080,620));
        Assert.Equal(1.6,camera.Zoom);Assert.True(camera.IsManual);
        camera.Fit();Assert.Equal(1,camera.Zoom);
        var center=camera.ToScreen(new(540,310));Assert.Equal(camera.View.Width/2,center.X,5);Assert.Equal(camera.View.Height/2,center.Y,5);
        Assert.True(camera.World.Width*camera.Scale<=camera.View.Width);Assert.True(camera.World.Height*camera.Scale<=camera.View.Height);
    }
}
