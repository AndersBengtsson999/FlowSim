# Live Performance Trend

Added after Development Collaboration Model v1 as an observability change. Simulation Model remains **0.2**. No allocation, WIP, defect, intervention or Before/After semantics change.

## Using the chart

In **Live → Team Performance**, Performance Trend appears beneath the compact performance metrics and the Flow/Capacity/Quality expander. The Flow Board remains the primary visualization. Choose one metric and a visible range: **Last 10, 20, 50, 100 days, or Full Session**. The default visible range is 20 days, matching the initial Team Performance window.

The Team Performance **Window · days** selector controls rolling calculations, independently of visible range. For example, a 20-day rolling metric with Full Session shows one trailing 20-day value at every completed simulated day. Changing the visible range never truncates a point's lookback. Seven available days with range 20 produce exactly seven points; no future or fabricated zero observations appear.

X is completed simulated day (one-based); Y is the selected metric, with units shown above the plot. Hover for the original day and value. Missing/undefined values are gaps, not zeros. One blue line and neutral gray intervention markers carry no good/bad classification. Chart selection does not rerun or alter the simulation.

## Metric definitions

| Metric | Series | Unit and definition |
|---|---|---|
| Throughput / 5 days | Rolling | Completions in the trailing window / available days × 5; items / 5 days |
| Cycle Time | Rolling | Mean `DoneDay − DevelopmentStartedDay` for items completed within that trailing window; simulated days. No completions means undefined. |
| Average WIP | Rolling | Mean authoritative end-of-day TotalWip across the trailing window; items |
| Waiting for Code Review | Daily | End-of-day waiting queue count; items |
| Waiting for Testing | Daily | End-of-day waiting queue count; items |
| Waiting for Rework | Daily, when relevant | End-of-day waiting queue count; items. Available for current or historical quality/rework relevance. |
| Developer Utilization | Rolling | 100 × summed consumed developer capacity / summed available developer capacity; percent |
| Tester Utilization | Rolling | 100 × summed consumed tester capacity / summed available tester capacity; percent |
| Development Capacity Used | Daily | Authoritative `DailySnapshot.UsedDevelopmentCapacity`; consumed capacity units/day |
| Effective Development Work | Daily | Authoritative `DailySnapshot.DevelopmentWork`; effective effort units/day |

Rolling metrics use the existing Step 13 definitions, with 10/20/50/100-day trailing windows and only available days at the start of a session. Utilization is a ratio of sums, never an average of daily percentages. Zero available capacity is undefined, matching `LivePerformance.Period`. No new quality score or composite metric is introduced.

**Development Capacity Used** includes primary plus collaboration consumption. **Effective Development Work** is the effort actually removed from remaining Development effort. Under Collaboration Model v1, the second contribution is 50% effective. With five developers and WIP 3, a deterministic first day consumes **5.0 capacity** and performs **4.0 effective effort**. These are independent selectable series with different units, never combined on one axis.

## Intervention markers and Before/After

A change recorded on **Day N** is marked at **N**, and takes effect on **N+1**. Marker hover includes recorded day, parameter changes, old/new values and optional label using the existing change description. Multiple changes near the pointer are all reported. Markers outside the visible range are omitted; a Day 0 marker can appear at the start of a full early timeline.

The intervention selected by the existing Before/After control has a thicker dashed marker when in range. Selecting it does not change calculations. Existing Before/After remains authoritative: Day 100/window 20 still means Before 81–100 and After 101–120. The chart does not introduce a second comparison engine or period aggregate.

The chart shows simulation observations. Intervention markers show timing, **not causality**. No metric is a Team Performance Score, and the simulator does not recommend a WIP level.

## Architecture and rendering

`Simulation.Application.LivePerformanceTrend` reads immutable daily snapshots, completion timestamps and configuration-change history. Daily series project only the visible observations. Rolling series use completion buckets and prefix totals in O(days + items), giving the same numerical definitions as Step 13 without repeatedly scanning history for every plotted point. Tests compare each rolling point against `LivePerformance.Period` for all supported windows. Floating-point comparisons use numerical tolerance.

`LiveViewModel` owns metric/range selection and refreshes the projection on simulation progress, window changes, Apply Changes, load, Reset and checkpoint restore. Pause/Resume retains the timeline. Reset removes stale points and markers; checkpoint restore reprojects restored history. Chart preferences are presentation state, not a new persisted simulation schema.

`LivePerformanceTrendChart` uses the existing Avalonia custom-drawing approach already used by `LiveQueueChart`. **No dependency was added.** A single drawing surface avoids one UI control per point. Long series use per-pixel buckets: retain segment endpoints and minimum/maximum envelopes for rendering, preserve gaps, and retain every underlying observation for hover. The rendered envelope is a display reduction, not a change to metrics or persisted history. Selected-marker highlighting and axis mapping use actual simulated days.

## Verification

The unmodified starting point built with zero warnings/errors and passed 313 tests.

New tests cover daily queues, both collaboration measures, every rolling metric against Step 13, all five visible ranges, lookback outside the visible range, seven-day partial history, undefined cycle/utilization values, marker day/old/new/label, unchanged underlying session state, UI refreshes, stable selector options, Pause/Resume, Reset, checkpoint restore and tooltip values.

Native verification uses [the repeatable host](verification/live-performance-trend/Program.cs), which opens the production MainWindow and exercises real ComboBox bindings and Live commands. This is automated native UI validation plus visual screenshot inspection, not a claim of manually clicking every scenario.

```sh
dotnet run --project docs/verification/live-performance-trend/NativeVerification.csproj -- docs/verification/live-performance-trend
```

- **Baseline:** five developers, ten testers, 200 fixed-backlog items, Development/Review/Testing effort 10/0.2/0.2, WIP 5/10/10, defects off. Advance to Day 100 and select all available metrics. Each series has 100 points under Full Session; daily and rolling labels/units update with selection.
- **Intervention:** record Development WIP 5 → 2 on Day 100, advance to Day 140. Marker remains at 100; new configuration applies from 101; serialized first-100-day snapshots remain identical. Existing active items are not evicted by a reduced WIP limit. No preferred WIP is inferred.
- **Collaboration:** post-intervention history contains consumed capacity greater than effective effort. On Day 140, the Flow Board and respective series both show **3.0 consumed / 2.5 effective**. These values are distinct from the separate isolated first-day 5.0/4.0 example.
- **Lifecycle:** all five range selector bindings, restored Day-100 checkpoint, removal of future intervention, and Reset were checked in the running window.
- **Long session:** 10,000 completed days with a 20-item fixed backlog; all 10,000 chart values remain available. Measured on this machine: loading/projecting the existing history approximately **13.1 ms**; native window render plus PNG save approximately **40.1 ms**. This is a local measurement for that workload, not a throughput guarantee for arbitrary simulations.

Representative screenshots: [Development capacity](verification/live-performance-trend/intervention-DevelopmentCapacity.png), [effective work](verification/live-performance-trend/intervention-DevelopmentWork.png), [cycle time](verification/live-performance-trend/intervention-CycleTime.png), [10,000 days](verification/live-performance-trend/full-session-10000.png).

Development Collaboration Model v1, Code Review, Rework, WIP rules, capacity priority and Before/After boundaries are unchanged. Technical Debt has not started.

Final Release verification: **327 passing tests** (110 Core, 136 Application, 81 UI), including 14 new cases; **0 warnings, 0 errors**. `git diff --check` is clean. The native scenarios passed after fixing selector-list identity during Avalonia binding refreshes.
