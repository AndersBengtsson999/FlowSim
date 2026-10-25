namespace Simulation.UI.Views;

/// <summary>Container sizing only. Overflow belongs to the LIVE page, never to a nested workshop scroll area.</summary>
public static class WorkshopLayout
{
    public static double Height(double width,double sceneHeight,double remainingHeight,double windowHeight=900)
    {
        var aspectHeight=Math.Max(1,width)*sceneHeight/1080;
        return Math.Min(aspectHeight,Math.Max(windowHeight<850?340:400,remainingHeight));
    }
}
