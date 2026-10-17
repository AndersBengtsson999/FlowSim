# B5.3.1 — Game View KPI dashboard

The Game View has a hideable Team Performance panel. Select up to four of twelve metrics with **KPIs…**, and select a shared 10/20/40-day window. Defaults are released throughput, cycle time, monetary cost per delivery and actual rework capacity. Wide windows place cards beside the workshop; narrow windows put a horizontally scrollable row below it. Existing station controls and the camera remain available.

## Definitions and reuse

`KpiDashboard.Project` reads recorded simulation history and reuses `LivePerformance.Period`, `TeamEconomics` and `LivePerformanceTrend`. `TeamKpiObservation` separates metric definitions, current/previous values, periods and history from presentation, leaving room for future comparison columns without adding experiment branching.

- Throughput: released items per five observed days, matching existing LIVE.
- Cycle time: existing development-start-to-release cohort average.
- Cost per delivery: recorded team cost divided by released items over the period; not the mean of daily ratios.
- Rework: actual consumed developer capacity, summed over the period, not a percentage.
- Total team cost: sum of recorded daily staffing costs, including idle staff.
- Utilization: used capacity divided by available capacity over the period; change is in percentage points.
- WIP, Backlog/development queue, review queue, testing queue and debt: end-of-period snapshots. Backlog includes dependency waiting.

At Day 100 with a 20-day window, current is Days 81–100 and preceding is Days 61–80. Comparisons require two full windows. A partial current window explicitly shows its observed day count. Undefined ratios and absent monetary/debt observations display N/A. All deltas use neutral styling.

Trends use the existing chart with a samples-only option: no interpolation, actual simulated days, a zero-based scale and explicit units/range. The compact view shows up to forty recent daily samples; each rolling sample uses the selected evaluation window.

## Updates and persistence

Updates use the existing LIVE refresh path, with no new timer or simulation mutations. Preferences are stored in the existing LIVE session document. Save/load preserves selection, visibility and window. Old documents default to the four standard metrics. Reset/new runs retain the current UI preferences; restarting the application without loading a session uses defaults. Simulation checkpoints restore observations while retaining the user's current display preferences.

## Verification

Release verification passed: build with zero warnings/errors; **801 tests passed** (Core 218, Application 247, UI 336), zero failures or skips. `build.log` and `tests.log` record the final results. Focused tests cover defaults/limits, all twelve authoritative metrics, windows, partial history, undefined ratios, missing observations, cost ratios, playback/interventions/checkpoints/reset and save/load. UI-only edits are checked against captured simulation state.

`Program.cs` is a separate native macOS verification executable using the real MainWindow, GameView and dashboard. It checks default day labels, the rolling-window binding, keyboard access to the KPI flyout, checkbox bindings and maximum selection, responsive layout, hide/show, zoom/station selection and save/load. RenderTargetBitmap captures show the actual Avalonia controls:

- [Wide dashboard](screenshots/dashboard-wide.png)
- [Narrow dashboard](screenshots/dashboard-narrow.png)
- [Selection interface](screenshots/kpi-selector.png)

Run from the repository root with `dotnet run --project docs/verification/kpi-dashboard-b5-3-1/Verification.csproj -c Release`. No runtime network dependency was added.

## Limits

No scoring, optimization, forecasts, new experiment branches or simulation mechanics were introduced. Cards use independent zero-based scales, so visual slopes across different units are not directly comparable. Narrow windows require horizontal scrolling to reach all four cards. The full existing LIVE history remains the source of calculations; no new incremental metric cache was introduced.
