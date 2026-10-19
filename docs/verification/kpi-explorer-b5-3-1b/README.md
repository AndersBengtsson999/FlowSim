# B5.3.1b — KPI Explorer

Select any Team Performance card to open the Explorer in Game View. The four-card dashboard stays available. The Explorer shows the selected metric, definition, current value, period, interpretation notes and up to 120 historical daily observations. Select a second metric or remove it. Close returns to the compact dashboard.

Wide layouts use a 400px sidebar; narrow layouts place the panel below the workshop. The Explorer scrolls independently. Both graphs have identical day coordinates and separate, labelled value axes in original units (teal and purple). There is no normalization or implied causal relationship. Points represent observations, with no invented samples or lines across missing data. Hover reveals original values. Negative ranges and small positive scales are supported.

## Inventory and authoritative data

All 12 original metrics were already available in Game View; all 11 additions below can also be selected as one of its four cards. The catalog uses `KpiDashboard`, `LivePerformance.Period`, `LivePerformanceTrend`, `TeamEconomics`, immutable daily observations and existing work-item dates. Analyze/Compare definitions and simulation mechanics are unchanged.

For a window W ending at day D, use inclusive display days max(1,D−W+1)…D. Core snapshot day is display day minus one. The immediately preceding comparison requires two full W-day windows. Partial current periods explicitly show the available day count. Each historical point applies the same formula ending on its own day, never today's cohort to yesterday's value. Snapshots ignore W except for the comparison interval. Earlier points remain unchanged after future work completes.

| Metric | Definition / aggregation and unit | Source and reconstruction / limitations |
|---|---|---|
| Released throughput | Released count / observed days × 5; items/5 days | DoneDay release cohort; existing LIVE rate. Counts do not measure size/value. |
| Cycle time | Mean DoneDay − DevelopmentStartedDay of releases; days | Existing LIVE released cohort; excludes unfinished work. |
| Cost / delivered item | Period recorded team cost / releases; SEK/item | Daily Economics + release dates. N/A without releases or complete cost history. |
| Rework effort | Sum used raw rework capacity; capacity units | Daily UsedReworkDeveloperCapacity; not a percentage or elapsed time. |
| Total team cost | Sum Economics.Total; SEK | Recorded historic prices, includes idle available staff. Missing observations yield N/A. |
| Developer utilization | 100 × sum used / sum available developer capacity; % | Existing LIVE ratio; zero denominator N/A. High utilization is not a delivery score. |
| Tester utilization | 100 × sum used / sum available tester capacity; % | Existing LIVE ratio; zero denominator N/A. |
| Total WIP | Final day's started unreleased count; items | Daily TotalWip, including queues and Ready for Release. Not average WIP. |
| Development queue | Final Backlog count; items | Daily BacklogCount includes dependency waiting. Not active Development. |
| Code Review queue | Final waiting-for-review count; items | Daily WaitingForCodeReviewCount. |
| Testing queue | Final waiting-for-testing count; items | Daily WaitingForTestingCount. |
| Technical debt | Final recorded Debt.State.Amount; effort units | Missing debt observation N/A; not cumulative creation or monetary liability. |
| **Cycle time P85** | Sort release-cohort durations; element ceil(.85×n), one-based; days | Existing release/start dates. No interpolation. N/A empty cohort; small samples unstable. |
| **Delivery frequency** | 100 × distinct release days / observed days; % of days | Release dates. Zero releases = 0; measures release-day frequency, not batch size. |
| **Delivery variability** | sqrt(mean((daily releases − mean daily releases)²)); items/day | Population standard deviation, includes zero-release days. Single observed day gives 0; batching affects it. |
| **Age of WIP** | Mean D − DevelopmentStartedDay of started, unreleased items at D; days | Membership from the end-of-day snapshot, with existing start dates. This avoids admitting next-day starts into historical WIP. N/A empty WIP. Excludes Backlog; not a prediction. |
| **Review queue growth** | Existing least-squares daily queue slope; items/day | Daily waiting-for-review count and `LivePerformance.Slope`. N/A fewer than 3 observations. Negative means shrinking queue, not automatically improvement. |
| **Rework cost** | Sum Economics.Rework; SEK | Existing recorded allocation. Already included in team cost; missing costs N/A. |
| **Rework proportion** | 100 × sum rework capacity / sum total used developer capacity; % | Same definition as existing LIVE ReworkCapacity. N/A zero denominator; not defect rate. |
| **Technical debt growth** | Sum Debt.Created − Debt.Repaid; effort units | Recorded flows, net period change, can be negative. Any missing debt observation gives N/A. |
| **Debt repayment effort** | Sum Debt.RepaymentCapacity; capacity units | Raw recorded capacity; differs from effort removed when productivity differs. Any missing observation N/A. |
| **Cost / delivered work size** | Sum team cost / sum original DevelopmentEffort of releases; SEK/effort unit | Immutable original effort is a size proxy independent of shortcut savings. N/A zero size or missing cost. Cost and release cohort use the same window. |
| **Delivered work / SEK** | Sum original DevelopmentEffort of releases / sum team cost; effort units/SEK | N/A missing or nonpositive cost; 0 when positive cost and no releases. Not business value or profit. |

No new simulation tracking fields were added. Original cost/debt history absent from legacy saves remains unavailable, not repriced or synthesized. Percent changes use percentage points; all changes are neutral.

## Investigated but not exposed

- **Flow efficiency:** capacity is abstract work, not time. Daily occupancy/work observations cannot establish a consistent within-day active/waiting time split. No capacity-based approximation was introduced.
- **Waiting time by workflow stage:** daily `StateDuringDay` and end states exist, but exact sub-day durations and repeated stage entries are not represented as complete comparable time intervals. A count of days sampled in a state would be a different measure and is not labelled elapsed waiting time here.
- **Blocked work age:** dependencies and daily blocked flags exist, but “blocked age” could mean current continuous blocking, cumulative blocking, or age since item creation, with different dependency mechanisms. No single authoritative age definition exists; no proxy was introduced.

## Architecture, lifecycle and persistence

`TeamKpiObservation` remains a read-only projection with a reusable definition, period, original values and `LiveTrendSeries`. Explorer projections share the dashboard formulas and existing charts; a second scenario can later supply the same structure. No experiment branching or score is added.

The Explorer refreshes from the normal LIVE refresh path only while open. It retains at most 120 samples per displayed metric, with no independent timer or duplicate stored history. Existing optimized trend projections are reused. Added metrics read recorded windows and cohorts; this is bounded to the displayed sample range, but very large histories/item populations are not claimed to have constant-time projection.

LIVE save/load persists Explorer selection and optional comparison alongside existing window/card settings. Old files open with the Explorer closed. Closing removes the primary selection; comparison choice is retained until removed. Checkpoint restoration refreshes data while retaining display preferences. Reset clears data, retaining preferences for the next run. Preferences are not global across application restarts without loading a session.

## Verification

Release build passed with zero warnings/errors. **809 tests passed** (Core 218, Application 253, UI 338), zero failures or skips. Native macOS verification passed.

Focused tests cover all added formulas across 10/20/40 days, actual day alignment, missing/partial data, zero denominators, historical WIP age after future completion, card selection/open/close, comparison removal, pause/resume/step/speed, interventions, checkpoints, reset, save/load and unchanged captured simulation state. Full build/test logs and native macOS verification are stored here.

`Program.cs` opens the real MainWindow and activates a card via keyboard, changes the actual comparison ComboBox, verifies aligned days, advances LIVE, checks zoom/station selection, resizes and restores a saved session. Screenshots are actual rendered Avalonia controls.

### Visual evidence and limits

- [Wide Explorer](screenshots/explorer-wide.png): both graphs and the workshop visible together.
- [Narrow Explorer](screenshots/explorer-narrow.png): panel below the workshop; vertical page scrolling is needed on short/narrow screens, and the four cards scroll horizontally. Wide windows are recommended for simultaneous inspection.
- The native fixture completed 20 LIVE steps with Explorer open in 4.092 seconds while the full test suite was also running. This is an integration smoke measurement, not an isolated benchmark or a guarantee for maximum history sizes.
- Definitions and interpretation notes are available by hovering the definition text; point tooltips retain original precision.
