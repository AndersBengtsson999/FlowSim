# B6.4.1 — LIVE workspace and cost KPIs

## Implementation

LIVE has tighter section spacing, smaller configuration/economics headers and a view selector in the primary toolbar. Both visualization choices and their existing commands, inspection, camera and scrolling code remain intact. Configuration, economics and the compact Product Version details retain expansion state.

The shared Team Performance component now appears in Flow Board as well as Game View. A pinned **Unlaunched Development Cost** card precedes the existing configurable four metrics; choosing that metric does not duplicate its card. Cards wrap into additional rows instead of a horizontal scrolling strip. Current-day balances are explicitly distinguished from rolling evaluation periods.

**Delivered work / 1,000 SEK** uses a presentation-only factor of 1,000 and three localized decimal places. `WorkVolumePerSek` retains its identity and original per-SEK observation values. `KpiDisplay` applies the conversion to cards, history, tooltip values and comparison presentation. The numerator is original Development effort of **internally released** work, not customer-launched work. Percentage change uses the existing preceding, complete equal-length window. Missing data and zero baselines show explicit N/A reasons; zero current value against a positive baseline is −100%, not missing. Higher means more internally released effort per unit of cost, not business value.

## Authoritative cost source

The new metric calls existing `LaunchEconomics.Calculate`. It does not allocate costs independently. It uses `LaunchCostPoint.Unlaunched = WorkInProgress + AwaitingLaunch`, excludes unallocated overhead and retains reconciliation with recorded incurred cost. The LIVE view model passes its existing cached Launch Economics history into KPI projection so the card and launch details share the same source. Decimal allocations remain unchanged; chart plotting converts coordinates to doubles only.

Customer launches move the allocated cost of included work into the customer-launched category, without changing expense or removing WIP. Current-day launch changes invalidate the existing cache through launch count, including consecutive product versions. No Core, simulation engine, release scheduling, cost allocation or persistence format was changed.

## History and Explorer

The metric is available in either Compare KPIs slot, including a new explicit primary-metric picker. The existing LaunchCostChart renders WIP, awaiting launch and total unlaunched, with actual ledger-derived launch markers. Axis labels use SEK/kSEK/MSEK for legibility; hover shows day and values in SEK. Existing pixel-bucket extrema sampling supports long histories without changing stored observations.

Explorer retains its 120-day historical range and independent metric scales. The compact sparkline uses the existing 40-day history. Full recorded history remains in Product Version → Launch Economics. No independent historical series is persisted: save/load, checkpoints and replay derive the same balance from existing recorded days and customer launch membership. Missing cost/capacity observations remain excluded by the existing model; the card identifies partial history and the details show missing-day counts. These are estimates, not recovered expenses for old files.

Compare Scenarios retains its existing seven supported metrics and evaluation semantics; this change does not extend that experiment model.

## Verification

Focused tests cover display-only ratio scaling, three-decimal formatting, history/tooltip units, percentage changes, zero/missing baselines, internal-release numerator stability, pinned balance/Explorer selection, launch reclassification, WIP retention, reconciliation, consecutive versions, historical preservation, legacy missing costs, checkpoint restore, save/load and deterministic continuation. Existing tests cover internal release and Run/LIVE behavior, camera/inspection and scrolling semantics.

The standalone Avalonia harness exercises recurring customer launches at days 60/120/180, a mix of WIP and awaiting work, expense invariance at launch, successor scheduling, narrow and wide card layouts, both visualizations, Explorer, station selection/Fit and save/load. Native macOS captures are in `screenshots/`; headless captures are in `headless/screenshots/`.

Build/test logs and final outcomes are recorded alongside this document.

## Remaining limitations

- Missing legacy cost/capacity observations cannot be reconstructed; balances show observed allocations with an explicit partial-history indication.
- Explorer shows the last 120 days; Launch Economics retains the full recorded history.
- Cost allocation retains the existing blended-rate estimate and overhead treatment.
- Rendered verification does not replace physical macOS trackpad/keyboard testing. Input handlers and simulation mechanics were not changed.
- OS-owned dialogs and Windows/Linux runtime appearance were not exercised.

Stop point: B6.4.1. No new simulation rules, cost allocation model or launch scheduling behavior.

## Visual evidence

- [Compact LIVE Game View](screenshots/compact-game.png)
- [Five KPI cards at desktop width](screenshots/kpi-cards.png)
- [Responsive cards at narrow width](screenshots/kpi-cards-narrow.png)
- [Unlaunched cost and delivered work in KPI Explorer](screenshots/kpi-explorer.png)
- [Flow Board](screenshots/flow-board.png)
- [Full Launch Economics history](screenshots/three-launch-economics.png)

Native captures use macOS Avalonia rendering. The final headless run also asserts that all five cards remain inside the dashboard's horizontal bounds at both tested widths.

## Final results

Release solution build: **0 warnings, 0 errors**. Complete solution test suite: **902 passed, 0 failed** (Core 218, Application 295, UI 389). Native macOS and final headless acceptance runs passed. `git diff --check` passed. Physical trackpad/keyboard acceptance remains a manual check.
