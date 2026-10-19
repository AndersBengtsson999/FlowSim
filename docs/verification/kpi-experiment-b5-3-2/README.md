# B5.3.2 — KPI experiment comparison

## Reused architecture

B5.1's `GuidedInterventionExperiment` owns an immutable checkpoint, the existing active LIVE session and one isolated baseline restored with `SimulationSession.Restore`. Its cancellation, baseline preparation, deterministic engine, reference/extra-intervention validation and matched elapsed-day handling are reused. No new engine, stored metric history, experiment branching system or simulation tracking fields were added.

B5.1 previously allowed tester-count changes only. Its validation now additionally accepts a developer-count-only change, with every other configuration field unchanged. Simultaneously changing both counts, costs, WIP or other settings does not create an experiment. Ordinary LIVE edits still apply through the existing explicit Apply action. No-op experiments remain valid for deterministic checks.

KPI definitions and `KpiDashboard.PeriodValue` reuse the same authoritative `LivePerformance.Period`/`TeamEconomics` calculations as the dashboard and Explorer. The existing trend chart gained an optional comparison series, a shared scale and two-series tooltips. Existing single-series charts retain their original operation. `ScenarioKpiComparison` is a transient projection of the existing branch histories, not another history store.

## How to use

1. In Game View, select a Team Performance card to open Explorer.
2. Select **Compare Scenarios**. Set **New evaluation days** to 10, 20 or 40.
3. Select **Start staffing experiment…**. Change only Developers or only Testers in the existing intervention editor, then Apply.
4. Resume or step LIVE. Baseline calculation does not advance or switch LIVE.
5. Select one of seven scenario KPIs for the detailed chart. Scroll within the panel to inspect the summary and economic consequences.

**Compare KPIs** keeps the previous two different metrics from the same LIVE scenario, with separate scales. **Compare Scenarios** shows one KPI definition for both branches, on the same day/value scale. Switching modes preserves the dashboard's four selected cards, rolling window and within-scenario comparison choice. The experiment period is separate from the rolling dashboard window; changing the "New evaluation days" selection never changes an already created experiment.

## Period and chart semantics

A checkpoint and intervention on display Day N have evaluation days **N+1 through N+W inclusive**. The unchanged baseline and intervention use identical inclusive boundaries. During partial progress of k observed LIVE days, all summaries use **N+1 through N+k**. The precomputed baseline's future outcomes are never exposed early. After completion, the comparison freezes at N+W even if LIVE continues.

Each graph sample is an **evaluation-to-date prefix** N+1…sample day, not a rolling window that leaks pre-intervention observations. Thus the final point equals the summary value. Actual simulation-day numbers appear on the axis. Teal circles identify baseline and purple squares identify intervention; coincident values may overlap, but hover lists both and their difference. One shared numeric scale spans both histories, including negative values when present. No independent normalization, interpolated missing observations or per-branch axis scaling is used.

Tooltips show day, baseline value, intervention value, intervention-minus-baseline difference and unit at original numeric precision. Missing or undefined inputs produce N/A. At zero observed days there are no summary values or graph points.

## Summary and economics

All seven summary rows use the same matched period:

- Released throughput: released items / observed days × 5.
- Cycle time: mean development-start-to-release duration of items released in that period; N/A with no releases.
- Cost per delivered item: recorded total team cost / actual released count, not an average of daily ratios.
- Total team cost: sum of recorded daily monetary costs for the matched period, not lifetime cost.
- Developer and tester utilization: sums of used / available capacity × 100; zero denominators N/A. Deltas in these rows are percentage points.
- Rework cost: sum of recorded allocated rework cost; already included in team cost.

The table provides baseline, intervention and difference for team cost and cost per delivery. The economic explanation additionally gives both released counts and their difference. Incremental cost per additional delivery is Δ team cost / Δ released only when Δ released > 0 and monetary observations exist. It is N/A when deliveries do not increase. A negative result means lower cost with more deliveries. No revenue, profit, customer-value assumptions or overall ranking are introduced.

## Lifecycle and limits

Preparation, pause/resume, step, accelerated playback and completion use existing B5.1 controls. Checkpoint restoration, reset, load or another intervention effective within the evaluation invalidate the comparison and clear its rows/series. An intervention recorded at the evaluation end takes effect afterward and does not invalidate the completed period.

B5.1 experiments are in-memory and are **not serialized** with LIVE sessions. Loading a session never reconstructs or silently reuses an experiment: the previous comparison becomes unavailable and a new experiment must be created. The Explorer still restores ordinary saved card/KPI preferences. Mode selection itself is transient and a fresh application defaults to Compare KPIs.

The baseline starts from the exact checkpoint RNG state. Branches are individually reproducible, but interventions can change draw counts/order, so identical random events between different branches are not guaranteed. This is one controlled simulation comparison, not a statistical confidence or causal claim about real teams.

The existing workshop, camera and stations remain present. The comparison panel is closeable and internally scrollable; narrow windows place it below the workshop and need page scrolling, as in B5.3.1b. Scenario comparison is limited to the seven established KPIs and one staffing variable at a time. Arbitrary historical scenario selection and experiment persistence are not introduced.

## Verification

The focused tests verify 10/20/40-day boundaries, matched partial prefixes, all summary metrics, cost ratios, baseline isolation, completion freezing, no-op/repeated determinism (existing B5.1 tests), unsupported changes, zero denominators, missing chart values, shared scale and hover details, mode switching, retained preferences, pause/resume, restoration and save/load/reset invalidation. Existing KPI tests cover missing monetary history without repricing.

The native macOS harness uses the real MainWindow and ComboBoxes, applies a developer intervention through the existing editor, waits for the isolated baseline, advances partial and complete evaluation, checks chart tooltips, zoom/stations, returns to Compare KPIs and verifies load invalidation. Its rendered screenshots and logs are retained here.

### Results

Release build: zero warnings/errors. **817 tests passed** (Core 218, Application 258, UI 341), no failures or skips. Native macOS verification passed. The native check exposed a ComboBox selection error caused by recreating its options during selection; the options now remain stable and redundant selection notifications are ignored, with a regression test.

- [Partial matched evaluation](screenshots/partial-scenario-comparison.png)
- [Completed cost comparison](screenshots/complete-scenario-comparison.png)
- [Economic summary](screenshots/economic-summary.png)
- [Unavailable after load](screenshots/invalidated-comparison.png)
