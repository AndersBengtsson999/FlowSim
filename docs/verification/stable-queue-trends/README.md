# Stable queue trends

Update: the original two-high-window confirmation described below was corrected after a gradual-growth reproduction. See `../queue-trend-diagnosis/README.md` for the current high-entry/lower-confirmation rule, unchanged numeric thresholds and diagnostics. This document preserves the original investigation and baseline.

## Existing behavior (inspection before implementation)

QueuePressure.Classify computes Growing/Shrinking/Unchanged from the last two queue counts. QueuePressure.From supplies at most four snapshots after the most recent configuration change. A separate Constrained classification requires four nondecreasing counts, net growth and exhausted observed capacity on the last three days. Zero capacity, dependencies and scheduled release accumulation have special classifications. SimulationInsights consumes these classifications for relevance, material state and pressure evidence.

Game View uses the daily labels for queue badges, station details and the workflow summary. Insights repeats these in its source observation/title. Flow Board QueueAttention separately uses the existing LivePerformance OLS slope over the selected KPI window for accumulation highlighting; its normalization and KPI-linked attention rules are outside this change. Daily changes are also recorded in Insights metrics.

An intervention recorded on Day N takes effect on Day N+1; its first zero-based DailySnapshot.Day is N. The latest session change boundary already provides a conservative way to exclude incompatible configuration history.

## New method and thresholds

`QueueTrend` is a pure UI projection shared by GameSceneState and SimulationInsights. It reuses `LivePerformance.Slope` without modifying it. Each completed day is evaluated from recorded observations in the last ten simulated days. At least four observations are needed (the existing queue-evidence minimum). A missing day is not filled or interpolated.

OLS slope times the observed day span is the fitted queue change. A direction becomes a candidate only when the absolute fitted change exceeds both two items and twice the regression residual RMS. A direction needs two consecutive eligible windows to be adopted. An established direction is retained only while fitted change has the same sign and exceeds both one item and one residual RMS. Otherwise it becomes Stable until another direction is confirmed. Fewer than four observations gives Insufficient data and clears confirmation state. Stable means no sufficiently clear direction under this rule, not an exactly constant queue.

The one-item retention floor comes from discrete work-item counts; the two-item entry floor gives a one-item hysteresis band rather than a queue-size percentage that would behave differently on large queues. The RMS term adapts to the simulator's batch-like arrivals/completions. The two-window confirmation rejects a single qualifying window while keeping reversal response bounded in the tested sustained reversal. Factors of one/two and confirmation length are explicit presentation heuristics, not statistical confidence or calibrated capacity thresholds. A full monotonic reversal test changes to Shrinking within ten new days.

Every projection replays recorded post-change history in day order; it does not depend on UI refresh frequency or stored acknowledgement state. Each rolling fit uses at most ten days. Load, restore and repeated reads therefore produce identical results. All configuration interventions conservatively restart evidence: change on Day N excludes Day N and begins at Day N+1. This may reset for a change that does not affect every queue, but never silently combines configurations.

## Pressure is unchanged

`QueuePressure.cs` is unchanged. Existing blocked, constrained, batching and dependency warnings retain priority on workshop badges. For ordinary queue badges the trend determines direction/color; the original pressure record remains in the scene. Station inspection shows stable trend separately from pressure and retains daily direction in detail rows. Insights adds QueueDirection metadata after detection, keeping relevance, material state, thresholds, explanations, metrics and charts unchanged. Its technical section preserves the original daily/pressure observation plus the separate trend period. Four-day pressure charts and ten-day trends are explicitly distinguished.

Flow Board accumulation normalization and Team Performance/KPI slopes remain unchanged, as do their selected-window semantics. They are not the Game View daily indicator replaced here.

## Same-history examples

`examples.csv` was generated from the existing deterministic 60-day continuous-arrival fixture (6 developers, 1/3/6 testers; development/review effort 1, testing effort 5; WIP 20/20/6; 3 arrivals/day). No history was edited to obtain these comparisons.

| Day | Testing queue, one tester | Previous daily direction | New trend |
| --- | ---: | --- | --- |
| 30 | 79 | Growing | Growing |
| 31 | 79 | Unchanged | Growing |
| 32 | 85 | Growing | Growing |
| 33 | 84 | Shrinking | Growing |
| 34 | 90 | Growing | Growing |
| 35 | 90 | Unchanged | Growing |

Between Days 10–60 the one-/three-/six-tester histories have 50/50/40 daily-label changes and zero new trend-label changes, correctly retaining their overall upward direction. These fixtures check real queue granularity and batching; they are not a claim of exhaustive calibration for every possible scenario. Synthetic tests additionally cover flat/noisy queues, alternate rises/falls, true decline and reversal.

Reproduce: `dotnet run --project docs/verification/stable-queue-trends/Verification.csproj -c Release`.

## Modified files

- `ViewModels/QueueTrend.cs`: shared presentation-only trend projection.
- `ViewModels/GameSceneState.cs`: attaches queue trends.
- `ViewModels/StationDetails.cs`: separate trend, pressure and daily detail.
- `Controls/GameWorld/SpriteWorkshopRenderer.cs`: stable ordinary queue badges; pressure warnings retained.
- `Views/GameView.axaml`: trend-method tooltip.
- `Insights/SimulationInsights.cs`: metadata attachment only, no detection edits.
- `Insights/InsightPresentation.cs` and `Insights/InsightsWindow.cs`: consistent labels and technical evidence.
- `tests/Simulation.UI.Tests/QueueTrendTests.cs`: focused coverage.
- This verification directory: inspection notes, method, comparison generator and CSV.

All production paths above are beneath `src/Simulation.UI`. No engine, work-item transitions, capacity, WIP, release or KPI formulas were changed.

## Verification results

Release solution build succeeded with zero warnings/errors. Full suite was run: Core 218 and Application 295 passed; the UI suite initially found a lost Scheduled batch summary warning. The summary was corrected to prioritize existing pressure/batching/dependency warnings, and the entire UI suite was rerun: 468 passed. Final total: 981 passing tests, no unresolved failures or skips.

Eleven new cases cover increasing/decreasing histories, low-amplitude noise, alternating counts, entry confirmation and quantization floor, sustained reversal and flattening, insufficient history (0/1/3 observations), day gaps, intervention reset/recovery, identical Game View/Insights labels, unchanged serialized state and pressure results, restore determinism, and independence from projection frequency. Existing pressure tests were retained unchanged.

Limitations: heuristic bands are not a causal/statistical confidence claim. Slowly changing or highly noisy queues may remain Stable, and a new configuration needs fresh history. Insights relevance and acknowledgements still follow the original pressure classification; only its trend label is stabilized. Native macOS visual acceptance was not performed in this task.
