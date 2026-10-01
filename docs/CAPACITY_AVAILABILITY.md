# Capacity Availability, Work Supply and Live Status — model 0.3

This bounded change adds availability and work supply semantics while retaining Development Collaboration Model v1. No other flow rules change. [Native verification](verification/capacity-availability/RESULTS.md) records the observed scenarios A–G.

## Previous behavior

Model 0.2 used headcount × per-person capacity as each daily pool. Developer work was allocated to Code Review, Rework, then Development; tester work used its separate pool. Utilization already used consumed capacity, including full collaboration consumption, divided by available capacity. Rolling utilization was a ratio of sums. Live supported a fixed initial backlog or continuous arrivals using a decimal accumulator and a seeded arrival stream.

## Capacity and utilization

Both availability settings default to 100%, are entered as percentages, and are stored as finite fractions in [0,1]. They represent capacity available to the modeled value stream, without simulating the reasons for unavailable capacity.

```
nominal developer capacity = developer count × capacity/developer/day
available developer capacity = nominal developer capacity × developer availability
available tester capacity = tester count × capacity/tester/day × tester availability
utilization = used capacity / available capacity
rolling utilization = sum(used capacity) / sum(available capacity)
```

For five developers, capacity/person 1 and availability 85%, the daily pool is 4.25. Consuming 4 gives 94.117647% utilization; consuming 4.25 gives 100%. Two testers at 75% have 1.5 units. No rounding is applied to allocation. UI formatting alone rounds numbers for presentation. No artificial utilization ceiling or recommended availability exists.

Availability scales the shared pool **before** the existing priority. It does not scale per-item or per-contribution caps. Development still gives every active item a primary opportunity before a second contribution, requires at least two developers for collaboration, orders collaboration by remaining effort after primary work, and discounts that contribution to 50% effective effort. For WIP 3 and pool 4.25 with sufficient effort: primary 3, collaboration 1.25, effective effort 3.625, consumed capacity 4.25. Review, Rework and Testing retain their existing one-unit/per-person caps. Admissions still occur before all daily work; no replacement admission occurs halfway through a day.

Positive effort cannot advance with zero available capacity. Zero-effort stages retain their established transitions. Live Status, Team Performance and Performance Trend show unavailable/gaps when their capacity denominator is zero. Legacy non-nullable lifetime/result/Recent numeric projections retain their existing finite-zero convention for compatibility; this is not presented as a measured zero-utilization percentage in the new Status. No NaN or Infinity is introduced.

## Work supply

**Fixed rate** is existing ContinuousArrival behavior. Each day adds the configured decimal items/day to the accumulator and generates an item for each whole unit. Fractional remainder is retained. It asks what happens when demand arrives at the configured rate.

**Always available** asks what the system delivers under its configured constraints. At day start, count free Development slots, subtract existing eligible backlog, and generate only the remaining number of items. Eligibility follows the existing CreatedDay and dependency rules. Generated items use the existing IDs, ordered arrival generator and seeded arrival random stream; no new random stream is introduced. Items/day has no effect and is hidden in Live, or disabled in the Advanced editor.

Generation does not guarantee utilization. Active Development WIP, per-item caps, developer priority, tester capacity, stage WIP, defects and rework still constrain flow. As before, admission is not conditional on a positive resource pool: at zero availability a finite set can enter Development, then positive effort stalls without generating more items into occupied slots. Existing backlog is not removed. Completed-item history and real downstream queues can grow; no fictitious infinite backlog is allocated.

The legacy fixed initial backlog remains available under More settings and in Advanced. Analyze request execution also supports the three modes. The existing `LiveSimulation.Start` API retains its historical optional ContinuousArrival/rate defaults; callers supplying a saved request's supply explicitly pass that mode/rate. The Live UI does so.

Neither mode is characterized as preferable or more realistic.

## Interventions and persistence

Availability and work supply are editable through the existing intervention workflow. A change recorded on Day N affects the interval displayed as Day N+1. Prior daily capacity, work, utilization, remaining efforts and random history are not recomputed. Before/After still uses Days 81–100 and 101–120 for Day 100 and window 20.

When leaving Fixed rate, the fractional arrival accumulator is frozen. Returning resumes that remainder with the new rate, with no catch-up arrivals. Always available uses random draws only for items actually generated. Checkpoints and saved sessions retain configuration, accumulator, ID counter and random state.

Model version is 0.3. Schema 1 is retained because new fields have compatible defaults. Model 0.2 scenarios, experiments and Live sessions load with availability 100% when absent and preserve their original supply behavior (fixed backlog for batch requests; their explicit mode for Live). Original Live model version is retained separately as provenance. Historical observations are restored from their stored ledger, not regenerated. New execution/results use model 0.3. Model 0.1 remains rejected because its Development collaboration semantics differ.

Real fixtures captured using the unchanged model 0.2 binaries prove that a Day 15 saved session continues to Day 30 with identical items, events, random outcomes, daily ledgers and numeric results after normalizing only the result's model-version label. Historical result files are not rewritten.

Analyze uses existing scenario parameters to distinguish developer/tester availability and Work Supply. Experiment team sweeps preserve availability when replacing headcount or per-person capacity.

## Live Status

One compact band above the Flow Board contains:

- current Day, Done and WIP; recent throughput per five days and completion-based cycle time using the selected Team Performance window;
- latest completed day's developer and tester used / available capacity and utilization;
- current waiting Review and Testing queues, and Rework when applicable; current work supply;
- the latest intervention, its recorded day and effective day, only when an intervention exists.

The capacity line explicitly names its day, while delivery names its rolling window. Current supply denotes configuration for the next step, so immediately after a paused intervention it may describe new supply while the capacity line still reports the last completed day. Tooltips explain this distinction and nominal/available/used capacity. Collaboration consumed-versus-effective work remains visible in Flow details and selectable Performance Trend series.

Queue counts and capacity come directly from the latest daily snapshot. Delivery uses LivePerformance. WIP and queue arrows reuse existing OLS results and the existing presentation threshold: absolute slope below 0.05 items/day is stable; fewer than three days gives no arrow. Arrows mean increasing, decreasing or stable, never good/bad and never causality.

Performance Trend adds selectable Available Developer Capacity and Available Tester Capacity from historical daily ledgers. Existing utilization and consumed/effective series remain unchanged. Intervention markers remain on the recorded Day N, while changed capacity first appears at N+1. Detailed Team Performance, Trend and Before/After remain reachable below the overview. Navigation does not reset Live.

## Architecture and verification

Core: Team and SimulationScenario configuration, ScenarioValidator, SimulationSession supply, SimulationEngine pool initialization and daily denominator ledger. DeveloperCapacityPolicy is unchanged.

Application: SimulationRequest, LiveSimulation lifecycle/provenance, LiveStatusProjection (read-only snapshot projection), LivePerformanceTrend, ScenarioParameters, AnalysisReport and ExperimentRunner's team-copy behavior. Existing LivePerformance formulas remain unchanged.

Infrastructure: compatible version checks in ExperimentJson and LiveSessionJson; default-initialized new fields and retained Live origin version.

UI: MainWindowViewModel configuration, LiveViewModel status/editing, SimpleWorkflowViewModel comparison fields, LiveView and AdvancedWorkspace. UI does not allocate capacity or simulate supply.

Automated coverage includes availability 0/85/100%, tester 0/75/100%, fractional use and collaboration, priority, invalid availability, zero denominator, ratio-of-sums, lazy supply at WIP 1/3/5/10, rate independence, existing backlog, fixed-rate remainder across mode switches, random-stream reuse, Run/Live/checkpoint/save determinism, real legacy continuation, experiment round-trip, comparison descriptors, status, intervention boundaries, historical trend values, and navigation.

Final result: **361 tests pass** (25 added; 110 Core, 159 Application, 92 UI). Release build has **0 warnings and 0 errors**. Native scenarios A–G pass.

Run full tests and native verification:

```sh
dotnet build -c Release
dotnet test -c Release --no-build
dotnet run --project docs/verification/capacity-availability/NativeVerification.csproj -c Release -- /tmp/flowsim-availability
```

No utilization targets, recommended availability, bottleneck scores/recommendations, individual calendars or productivity models were introduced. Development Collaboration Model v1 is unchanged. Technical Debt has not started.
