# B6.4 — Unified Dark Theme

## Implementation

The previous shell forced the Light theme, while local light surface overrides and a separately configured KPI Explorer window prevented a consistent appearance. The application now requests Avalonia Dark regardless of the OS preference.

`FlowPalette` owns semantic color values shared by XAML resources and drawn charts. `FlowColors.axaml` exposes those values as brushes; `FlowControls.axaml` provides reusable window, input, button, focus, destructive-action, card, section and tooltip styles. Existing Product/Live style selectors and resource names remain reusable and reference the palette. Muted text is slightly lighter than the suggested palette to meet the tested AA contrast pairs.

Updated surfaces: main shell, LIVE, Game View surroundings, Scenarios, Compare, Results, Explore, Experiments, Settings, station inspection, guided comparison, Team Performance, detached KPI Explorer, Product Version details, launch history/economics and customer launch notices. Chart labels, grids and series use the shared palette; chart data, axes and calculations are unchanged.

The sidebar uses compact blue-gray navigation, selected and hover states, and WORKSPACE / ANALYSIS / SYSTEM grouping. Destinations are unchanged. Primary actions use blue, destructive actions red, and success/warning/error states retain their meanings and text labels. Workshop artwork and its yellow details are untouched.

No Core, Application, Infrastructure or sprite files were changed. No simulation, persistence, replay, scheduling, KPI, zoom/pan or scrolling logic was changed.

## Verification

- Release solution build succeeds.
- Full solution suite: **892 passed, 0 failed** (Core 218, Application 292, UI 382).
- Nine new checks cover eight semantic contrast pairs and navigation selection.
- The macOS native and headless harnesses load the production palette/control styles and actual views, assert Dark theme/resource availability, and render the views listed below.
- Recurring customer launches stop at days 60/120/180, generate separate notices, and schedule the next target at 240. Continuous 200-day execution, history/economics reconciliation, save/load, automatic Run, station/Fit and postponement 60 → 80 → 140 pass.
- Narrow layout, detached KPI Explorer, menu and dropdown popup rendering were exercised. The rendered images were visually reviewed for contrast and unwanted light surfaces.
- The final build and UI test rerun include the last destructive-button styling adjustments; the full suite log precedes those presentation-only refinements.

The standalone verification app intentionally supplies the same production resources/styles rather than starting `App` and its normal launch lifecycle. Production `App.axaml` is also compiled by the solution build.

## Screenshots

Native macOS Avalonia renders are in `screenshots/`; headless captures are in `headless/`.

- [LIVE setup](screenshots/live-setup.png)
- [Game View and launch economics](screenshots/three-launch-economics.png)
- [KPI Explorer](screenshots/kpi-explorer.png)
- [Scenarios](screenshots/scenarios.png), [Compare](screenshots/compare.png), [Results](screenshots/results.png)
- [Explore](screenshots/explore.png), [Experiments](screenshots/experiments.png), [Settings](screenshots/settings.png)
- [Customer launch notice](screenshots/launch-day-180.png), [Launch history](screenshots/three-launch-history.png)
- [Station inspection](screenshots/inspection.png), [Narrow layout](screenshots/recurring-narrow.png)
- [Exit menu](screenshots/menu-popup.png), [Speed dropdown](screenshots/dropdown-popup.png)

## Limits / remaining verification

- Native Avalonia Fluent popup controls retain their standard charcoal surfaces and blue selection treatment; they are dark, but not every template brush is replaced with a FLOW brush.
- OS-owned file pickers/title bars are controlled by macOS and are outside the app-content captures.
- Physical two-finger trackpad scrolling, Cmd-scroll, pointer panning, keyboard traversal and switching the OS appearance should receive a hands-on macOS pass. Existing interaction code and regression tests are preserved; rendered screenshots do not prove physical input behavior.
- Windows/Linux runtime appearance was not exercised.
- No new confirmation dialogs or state-detection logic were introduced.

Stop point: B6.4 only. No new simulation functionality or light-theme option.
