using Simulation.Application;
using Simulation.Core;
using Simulation.UI.ViewModels;
using System.Globalization;
var output="docs/verification/stable-queue-trends/examples.csv";
using var file=new StreamWriter(output);
file.WriteLine("scenario,day,queue,count,daily_direction,stable_direction,slope_items_per_day,observations");
foreach(var testers in new[]{1,3,6}) {
    var live=LiveSimulation.Start(new(){NumberOfWorkItems=80,DeveloperCount=6,TesterCount=testers,DevelopmentEffort=1,CodeReviewEffort=1,TestingEffort=5,DevelopmentWipLimit=20,CodeReviewWipLimit=20,TestingWipLimit=6},WorkArrivalMode.ContinuousArrival,3);
    for(var day=1;day<=60;day++) {
        live.Step();var trends=QueueTrend.From(live);var pressures=QueuePressure.From(live);
        foreach(var key in new[]{"review-wait","testing-wait"}) {
            var count=key=="review-wait"?live.CurrentSnapshot.WaitingForCodeReviewCount:live.CurrentSnapshot.WaitingForTestingCount;
            var t=trends[key];file.WriteLine($"testers-{testers},{day},{key},{count},{pressures[key].Trend},{t.Label},{t.Slope?.ToString("0.####",CultureInfo.InvariantCulture)},{t.Samples}");
        }
    }
}
Console.WriteLine(output);
