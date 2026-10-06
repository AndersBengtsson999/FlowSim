using System.Globalization;
using System.Text.Json;
using ModelValidation;
CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
var runs=Scenarios.Run();
var directory=args.FirstOrDefault()??"docs/verification/model-validation";
Directory.CreateDirectory(directory);
File.WriteAllText(Path.Combine(directory,"observations.json"),JsonSerializer.Serialize(new{Seed=Scenarios.Seed,Window=Scenarios.Window,Configuration=Scenarios.BaselineRequest,StopShortcutsDay=Scenarios.StopDay,RepaymentDay=Scenarios.RepaymentDay,runs.DebtZeroDay,runs.Observations},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine("Seed 12345; Always Available; 5 Dev, 2 Test; WIP 5/3/3; fixed effort 5/1/2; availability 100%; defects off; window 50.");
foreach(var r in runs.Observations)Console.WriteLine($"{r.Scenario} | Day {r.Day} | window {r.FirstDay}–{r.LastDay} | Done {r.Done} ({r.CompletedInWindow} in window) | TP/5d {r.ThroughputPerFiveDays:F2} | CT {r.CycleTime:F2} | WIP avg {r.AverageWip:F2}, now {r.CurrentWip} | Review Q {r.AverageReviewQueue:F2}, now {r.CurrentReviewQueue} | Test Q {r.AverageTestingQueue:F2}, now {r.CurrentTestingQueue} | Dev {r.DeveloperUtilization:P2} | Test {r.TesterUtilization:P2} | Debt {r.Debt:F3}, ratio {r.DebtRatio:P2}, overhead {r.DebtOverhead:P2} | Cost {r.CostPerItem:F3} ({r.CostBreakdown!.Development:F3}/{r.CostBreakdown.CodeReview:F3}/{r.CostBreakdown.Rework:F3}/{r.CostBreakdown.Testing:F3}) | Dev consumed/work {r.DevelopmentCapacity:F3}/{r.DevelopmentWork:F3} | Repay {r.RepaymentCapacity:F3}");
Console.WriteLine($"Debt exhausted on Day {runs.DebtZeroDay}.");

var audits=new[]{Audit.Check(runs.Baseline.Session),Audit.Check(runs.Productivity.Session),Audit.Check(runs.DebtTimeline.Session)};
var beforeRead=Audit.Fingerprint(runs.DebtTimeline.Session);
foreach(var metric in Simulation.Application.LivePerformanceTrend.Metrics) _=Simulation.Application.LivePerformanceTrend.Project(runs.DebtTimeline.Session,metric.Metric,Scenarios.Window,null);
if(beforeRead!=Audit.Fingerprint(runs.DebtTimeline.Session))throw new InvalidOperationException("Metric projections mutated state.");
var repeat=Scenarios.Run();
var fingerprints=new[]{Audit.Fingerprint(runs.Baseline.Session),Audit.Fingerprint(runs.Productivity.Session),Audit.Fingerprint(runs.DebtTimeline.Session)};
var repeated=new[]{Audit.Fingerprint(repeat.Baseline.Session),Audit.Fingerprint(repeat.Productivity.Session),Audit.Fingerprint(repeat.DebtTimeline.Session)};
if(!fingerprints.SequenceEqual(repeated))throw new InvalidOperationException("Determinism mismatch.");
File.WriteAllText(Path.Combine(directory,"audit.json"),JsonSerializer.Serialize(new{Audits=audits,Fingerprints=fingerprints,RepeatedFingerprints=repeated,MetricProjectionReadOnly=true},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine("PASS: all-day invariants, item-capacity conservation, exact repeated state fingerprints and read-only metric projections.");
foreach(var audit in audits)Console.WriteLine(JsonSerializer.Serialize(audit));

var timeline=runs.DebtTimeline.Session;
var payoff=timeline.Days[runs.DebtZeroDay-1];
var payoffOpening=timeline.Days[runs.DebtZeroDay-2].Debt!.State.Amount;
var late=timeline.WorkItems.Where(w=>w.DoneDay>=runs.DebtZeroDay+1&&w.DoneDay<=runs.DebtZeroDay+Scenarios.Window).ToArray();
var diagnostics=new{
    DebtCreatedAfterStop=timeline.Days.Skip(Scenarios.StopDay).Sum(d=>d.Debt!.Created),
    LastDebtCreationDay=timeline.Days.Last(d=>d.Debt!.Created>0).Day+1,
    StopScope=runs.AtStop.DebtState!.CumulativeDevelopmentScope,
    RepaymentStartScope=runs.AtRepayment.DebtState!.CumulativeDevelopmentScope,
    PayoffDay=runs.DebtZeroDay,PayoffOpeningDebt=payoffOpening,
    PayoffMaximumAllocation=(payoff.AvailableDeveloperCapacity-payoff.UsedReviewCapacity-payoff.UsedReworkDeveloperCapacity)*.25,
    PayoffConsumed=payoff.UsedDebtRepaymentCapacity,PayoffDevelopmentConsumed=payoff.UsedDevelopmentCapacity,
    PayoffReviewConsumed=payoff.UsedReviewCapacity,
    PostPayoffMaximumCohortStartOverhead=late.Max(w=>w.DevelopmentPlan?.Overhead??0),
    PostPayoffCohortExtraDevelopmentCapacity=late.Sum(w=>w.DeliveryCost.Development-w.DevelopmentEffort),
    PostPayoffCohortCount=late.Length,
    FasterDevelopmentWindowCollaborationCapacity=runs.Productivity.Session.Days.Skip(150).Sum(d=>d.CollaborationDevelopmentCapacity)
};
File.WriteAllText(Path.Combine(directory,"diagnostics.json"),JsonSerializer.Serialize(diagnostics,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine(JsonSerializer.Serialize(diagnostics));
