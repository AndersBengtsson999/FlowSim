# KPI Explorer UX redesign

## Inspection and previous structure

KpiDashboardView is shared by compact Team Performance and its detached modeless Explorer window. LiveViewModel.Kpis projects KpiDashboard observations; LiveViewModel.ScenarioKpis owns the existing guided experiment comparison. Previously, Compare KPIs was the default, both selectors were always displayed, and a long methodology paragraph plus definitions preceded the charts. The other existing mode was Compare Scenarios.

All 24 entries in KpiDashboard.Definitions remain selectable. Period rates, totals, ratios, cohorts and slopes use the selected 10/20/40-day window. WIP, Backlog, Review/Testing queue counts, outstanding debt and WIP age are day-end snapshots. Unlaunched Development Cost is an accumulated allocation balance. The presentation now explicitly distinguishes these from rolling-period measurements. Existing projection, history limits (120 days), recorded samples, launch markers and hover implementations are unchanged.

## New hierarchy

Explore one KPI is the initial mode. Selection is followed by a prominent value/unit, applicable date or evaluation period, short plain-language definition, relevant balance components, historical chart, and collapsed calculation/limitations sections. Financial qualification remains visible for the unlaunched balance: estimated allocations exclude overhead and are neither lost revenue nor cash expenditure. Its exact definition, missing allocation counts and full interpretation remain accessible below the chart.

Compare KPIs reveals the second selector, optional remove action, a concise empty state, separate-scale warning and the second chart. Selectors/charts sit side by side at sufficient width and stack below 780 pixels. Each metric retains its units and dates. Compare Scenarios remains available as the existing third mode, with its experiment controls and mathematics untouched.

Switching modes preserves the primary KPI, evaluation window and comparison choice. The compact dashboard remains unchanged. Return to compact view closes the detached window; Escape remains supported. Charts use existing controls/templates, which the rendering harness verifies are reused on data updates. No new view model or charting subsystem was introduced.

## Files

- src/Simulation.UI/ViewModels/LiveViewModel.Kpis.cs — presentation text, snapshot distinction and existing balance component formatting.
- src/Simulation.UI/ViewModels/LiveViewModel.ScenarioKpis.cs — default Explore mode and visibility properties; existing scenario comparison retained.
- src/Simulation.UI/Views/KpiDashboardView.axaml — hierarchy, mode-specific selectors, summaries and progressive disclosure.
- src/Simulation.UI/Views/KpiDashboardView.axaml.cs — responsive comparison selectors and chart sizing.
- tests/Simulation.UI.Tests/KpiExplorerPresentationTests.cs — ten new regression cases.
- docs/verification/kpi-explorer-redesign/ — reproducible Avalonia harness and rendered screenshots.

## Verification

Full suite: 1,009 passed (218 Core, 295 Application, 496 UI), no failures or skips. Release solution build: zero warnings and zero errors.

New tests cover default mode, comparison empty state, mode/selection/window preservation, all 24 authoritative values and samples, distinct units, all seven snapshot/balance types across rolling windows, breakdown sums/formatting and missing legacy allocations. Existing tests cover rolling metrics, replay, checkpoint restoration, launch allocation boundaries and scenarios.

The actual Avalonia headless/Skia harness checks opening from Game View; default single-KPI visibility; customer-launch history markers and hover text; collapsed disclosures; chart-control identity and scroll offset after updating the window; comparison visibility/units; 620×700, 860×800 and 1200×850 layouts; expandable calculation details; retained scenario mode; Return to compact; and unchanged serialized simulation state throughout inspection. The first harness run selected multiple visible dashboard instances; scoping the query to Game View fixed the harness without a production change.

Run from the repository root:

```sh
dotnet run --project docs/verification/kpi-explorer-redesign/Verification.csproj -c Release -- --headless
dotnet build -c Release
dotnet test -c Release --no-build
```

Screenshots in `headless/screenshots`: explore-balance.png, explore-period.png, compare-kpis.png, explore-narrow.png. These are rendered Avalonia evidence, not native macOS acceptance screenshots.

## Scope and limitations

No Application/Core files, formulas, allocations, history generation, comparison mathematics, trend calculations, insight rules or simulation mechanics were changed. Only presentation code and verification were modified.

At the minimum window size, chart bottoms and optional detail sections require vertical scrolling; there is one Explorer scroll surface and no increased minimum size. Side-by-side charts retain independent vertical scales and may have different vertical positions when one has a breakdown. Automatic qualitative trend descriptions were deliberately omitted: existing data is displayed directly without introducing a new statistical classifier. Native pointer/trackpad acceptance remains manual; the existing hover/chart implementations were reused unchanged.
