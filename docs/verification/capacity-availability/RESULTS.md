# Native verification — capacity, work supply and Live Status

Verified on macOS with the actual Avalonia MainWindow, native desktop lifetime, Fluent theme, bound controls and current ViewModels. The executable host sets the configuration, advances the shared simulation, checks rendered bindings and history, and renders window screenshots. Screenshots were visually inspected. This is automated native UI verification plus visual inspection, not a claim that all scenarios were manually clicked through.

Reproduce with the command in [implementation report](../../CAPACITY_AVAILABILITY.md). Screenshots are written to `/tmp/flowsim-availability` by default. The harness exits nonzero on assertion failure.

Baseline: Live Flow Demo, five developers, two testers, Development WIP 5 unless stated, Review/Testing WIP 3, fixed efforts 5/1/2, no defects, no initial backlog, rolling window 20. A–E end at Day 200. Capacity figures below are latest-day observations; throughput and cycle time are recent-window measures. These observations do not identify an optimal configuration.

| Scenario | Done | Throughput / 5d | Cycle time | Current WIP | Waiting review / testing | Dev used / available | Test used / available |
|---|---:|---:|---:|---:|---|---|---|
| A Fixed rate 0.8/day, 100%/100% | 154 | 4.0 | 8.0 | 6 | 0 / 1 | 5 / 5 | 2 / 2 |
| B Always available, 100%/100% | 162 | 4.3 | 9.5 | 8 | 0 / 0 | 5 / 5 | 2 / 2 |
| C Developer 80% | 130 | 3.5 | 11.1 | 7 | 0 / 0 | 4 / 4 | 2 / 2 |
| C Developer 85% | 138 | 3.5 | 10.1 | 7 | 0 / 1 | 4.25 / 4.25 | 1 / 2 |
| D Tester 75% | 145 | 3.8 | 29.7 | 25 | 0 / 18 | 5 / 5 | 1.5 / 1.5 |
| E Development WIP 1 | 49 | 1.3 | 7.0 | 1 | 1 / 0 | 0.5 / 5 | 0 / 2 |
| E Development WIP 3 | 140 | 3.8 | 7.2 | 4 | 1 / 1 | 5 / 5 | 1 / 2 |
| E Development WIP 5 | 162 | 4.3 | 9.5 | 8 | 0 / 0 | 5 / 5 | 2 / 2 |
| E Development WIP 10 | 162 | 4.3 | 15.5 | 13 | 0 / 0 | 5 / 5 | 2 / 2 |

A: Fixed-rate behavior retained; automated legacy fixture comparison separately verifies exact model 0.2 continuation.

B: Always supply produced no waiting backlog and had no rate constraint. Used/available capacity and utilization appeared correctly in Status.

C–D: Full fractional capacity was visibly consumed (4.25/4.25 and 1.5/1.5, both 100%). WIP and waiting queues remained distinct. Real waiting queues may grow when downstream work accumulates; that is not generated backlog.

E: Every day's active Development, Review and Testing WIP respected its limit. WIP 1 left capacity unused, with 10% developer utilization on the final observed day, consistent with contribution caps and no midday replacement admissions. No extra work was created to force utilization.

F: At Day 100, developer availability changed 100%→80%. All first 100 daily snapshots remained byte-equivalent when serialized. Day 100 retained capacity 5; Day 101 had capacity 4. The actual chart showed the recorded Day 100 marker and the first reduced value at Day 101. Status showed the latest intervention/effective day. Before/After displayed 81–100 and 101–120; its expanded panel and chart were reachable and rendered correctly.

G: Continued F to Day 1000: Done 681, recent throughput 3.5/5d, cycle time 10.4d, current WIP 5, queues Review 1 / Testing 0, developer 4/4 (100%), tester 1/2 (50%), Always available, backlog 0. Day, delivery, capacity, queues, supply and latest intervention fit in one compact status band above the Flow Board. Navigation to Analyze and back preserved the complete session. Detailed chart and Before/After remained reachable.

The host completed with `PASS: A–G actual Avalonia window, bound fields, status, WIP, DayN+1 capacity, markers, Before/After and navigation.`

Final verification: **361 tests passed** (110 Core, 159 Application, 92 UI), including 25 new cases. Release solution build: **0 warnings, 0 errors**. `git diff --check` passed.

Reviewed screenshots: [fractional capacity status](status-fractional.png), [capacity trend and intervention](capacity-trend.png), [Before/After](before-after.png), [Day 1000 overview](long-session.png).
