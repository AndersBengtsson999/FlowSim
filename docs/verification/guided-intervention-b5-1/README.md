# B5.1 — Guided intervention comparison

## Reused components and scope

- `LiveSimulation.CreateCheckpoint` and `SimulationSession.Capture/Restore` preserve authoritative work items, all history, debt state and the existing independent random streams.
- `SimulationSession.AdvanceOneDay` evaluates the isolated baseline. There is no new engine, random source, capacity allocation or simulation rule.
- The existing LIVE Change team / work editor applies the intervention, with ordinary pause/resume/step/advance controls.
- `LivePerformance.Period` provides actually released counts, cohort cycle time and `SystemCost` consumed capacity. Historical Before/After remains unchanged and is not used as a counterfactual.
- The existing Experiments comparison page hosts the detailed view; its existing flow and bar charts render the results. The flow chart now labels the actual first observed day instead of assuming Day 1.

## Workflow

1. Select Testing in Game View and choose **Experiment with test capacity**.
2. LIVE pauses and the existing parameter editor opens. Change **Testers**, select 10/20/40 days (default 20), then Apply.
3. The original checkpoint baseline runs asynchronously on a detached session. LIVE does not advance automatically. Resume, Step or explicitly Advance using the existing controls.
4. Expand the experiment summary above the workshop. Open detailed comparison for the queue history, released count, cycle time, consumed capacity and signed observed differences. Return to LIVE to continue.

The compact panel is collapsed initially and can be collapsed again. It participates in the existing header layout, outside the world camera/hit-test surface. No worker/package animation, zoom/pan, station geometry or simulation mechanism changes.

## Boundaries and interpretation

Checkpoint Day N is the shared queue observation; the intervention takes effect on N+1. A 20-day experiment at Day 100 evaluates Days 101–120 inclusive. At LIVE Day 112, both branches display only Days 101–112, even though the baseline has calculated through 120. Completion requires all relevant LIVE days and a ready baseline.

Released means delivered, not merely Ready for Release. Queue values show checkpoint → current matched day; the chart includes the checkpoint sample followed by each observed end-of-day sample. Cycle time uses the existing released-item cohort and is unavailable without releases. Cost is total consumed system capacity, including unfinished work and debt repayment, not money or allocated staff capacity. Missing observations/history remain unavailable, not zero. Signed deltas are observational and do not rank the intervention as beneficial.

## Lifecycle and limitations

- One tester-count comparison at a time; an unchanged tester count is a valid reproducibility experiment.
- Other ordinary parameter changes are allowed, but do not start a tester-only comparison.
- A further intervention before the evaluation end invalidates attribution and clears results. Changes recorded at or after the evaluation end take effect outside the window and do not invalidate the completed period.
- Pause retains progress. Reset, load or checkpoint restore invalidates results. A new experiment supersedes the old one. Canceled/stale baseline tasks cannot publish results into a replacement experiment.
- Experiment results are in memory only; saved LIVE sessions retain their checkpoints and ordinary interventions, not the comparison panel state. Loading explicitly invalidates any current comparison.
- Baseline computation copies the existing history and runs only 10/20/40 extra days; very large histories still have the existing checkpoint capture/memory cost. Cancellation is checked between simulated days.
- This is one deterministic trajectory, not a statistical or causal analysis. Random stream semantics are exactly those of the existing engine; different trajectories may consume future draws differently.

## Verification

The native verification harness instantiates the actual MainWindow with Avalonia's macOS backend and software rendering. It activates the Testing action with keyboard input, edits the real tester TextBox, checks the proposal binding, and captures the editor, partial/complete comparison, detailed page and invalidation. Screenshots are actual control renders, not mockups.

```sh
dotnet build --no-restore -c Release -m:1 -nr:false
dotnet test --no-build --no-restore -c Release -m:1 -nr:false
dotnet run --project docs/verification/guided-intervention-b5-1/Verification.csproj -c Release
```

Focused automated tests cover Day 100 boundaries for 10/20/40 days, matched partial periods, checkpoint history/randomness, baseline independence, no-op equality, replay with defects/dependencies/skills/debt/variable effort, cost aggregation, scheduled release constraints, pause/resume, extra interventions, reset/load/restore and proposal notifications.

### Captured example

The native harness uses a loaded Testing queue at Day 100, changing 2 → 3 testers. At Day 112 the observed Days 101–112 comparison shows 4 versus 6 released items, queue 81 → 90 versus 81 → 88, cycle time 77.5 versus 76.83 days, and consumed capacity 132 versus 144. These are outputs of that seeded fixture, not promised outcomes for other configurations.

- [Existing editor and proposed change](screenshots/guided-editor.png)
- [Partial results with the workshop](screenshots/partial-comparison.png)
- [Detailed comparison and charts](screenshots/detailed-comparison.png)
- [Completed evaluation](screenshots/complete-comparison.png)
- [Invalidation after restore](screenshots/invalidated-comparison.png)

### Final results — 2026-10-16

- Release solution build: passed, 0 warnings and 0 errors.
- Full suite: **768 passed, 0 failed, 0 skipped** (Core 218, Application 224, UI 326).
- Native macOS harness: passed; actual editor binding, baseline independence, matched days, detailed comparison, completion and restoration verified.
- `git diff --check`: passed.
