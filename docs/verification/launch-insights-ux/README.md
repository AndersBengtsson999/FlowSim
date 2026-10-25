# Insights selection and Customer Launch UX

## Changes

- Insights defaults follow existing active category/id ordering, then current information, then the historical finding with the latest observation end day. A selected ID is retained through updates, including a transition to no-longer-relevant; disappearance selects the next appropriate entry. Opening the reader does not acknowledge evidence automatically.
- The existing ProductLaunchView has a clearly labelled Customer Launch summary with version, launch mode, items awaiting customer launch and planned day. Decision-required status remains visible in the compact summary.
- Expanded details expose Launch now and Schedule launch. Controls remain visible while running and explain that LIVE must be paused; other unavailable states have explanations as well. Bindings use the existing LaunchProductCommand, PlanLaunchCommand, ConfigureLaunchCommand and their guards without changes.
- Scheduling, delivery, economics and history use tabs to avoid showing everything simultaneously. History uses the page scroll instead of its own nested scroll.
- New/loaded sessions start compact. Expansion survives simulated days, launch decisions and switching views during a session. Reset also retains its existing compact behavior.

## Verification

- Full Release build: 0 warnings, 0 errors.
- Full suite: 966 passed (218 Core, 295 Application, 453 UI), no failures or skips.
- Six new focused tests cover default selection/fallback, manual selection, unavailable launch explanations, running-state guards, existing scheduling/launch commands, internal-release/customer-launch distinction and expansion state.
- Existing display assertions were updated for the new wording and removal of launch totals from the compact header; launch totals/history are still tested. An initial legacy assertion expecting “1 launches” failed during development and was updated. No unresolved failures.
- Headless Avalonia/Skia checks verify the actual bound commands, keyboard activation of scheduling and launch buttons, disabled-but-visible running controls, workshop height, default selection and accessible delivery/economics/history tabs.
- Wide viewport: 1440 × 1000. Narrow viewport: 980 × 1000. Compact workshop is taller than the expanded one; narrow workshop remains at least 180 pixels high.
- No engine, release/customer-launch rules, capacity, work-item transitions, insight detection/classification, evidence, KPI definitions, persistence or comparison changes. No zoom/camera/rendering changes. Only presentation code and UI state were changed.

## Images and reproduction

Actual Avalonia renders on macOS using headless Skia; not native desktop screenshots. Before/after use the same seed and Day 35 fixture. Narrow after screenshot also demonstrates the customer-launch result.

| View | Before | After |
| --- | --- | --- |
| Insights | `before/headless/screenshots/insights-wide.png` | `after/headless/screenshots/insights-wide.png` |
| Game compact | `before/headless/screenshots/game-compact.png` | `after/headless/screenshots/game-compact.png` |
| Game expanded | `before/headless/screenshots/game-expanded.png` | `after/headless/screenshots/game-expanded.png` |

Additional after images show running/disabled controls and the narrow layout. The normal active Insights fixture looks the same because the change fixes selection fallback rather than its visual design.

Run from repository root:

```sh
dotnet run --project docs/verification/launch-insights-ux/Verification.csproj -- --headless
```

## Modified files

- `src/Simulation.UI/Insights/InsightPresentation.cs`
- `src/Simulation.UI/Insights/InsightsWindow.cs`
- `src/Simulation.UI/ViewModels/LiveViewModel.ProductDelivery.cs`
- `src/Simulation.UI/ViewModels/LiveViewModel.cs`
- `src/Simulation.UI/Views/ProductLaunchView.axaml`
- `tests/Simulation.UI.Tests/LaunchInsightsUsabilityTests.cs`
- `tests/Simulation.UI.Tests/CustomerLaunchPresentationTests.cs`
- `tests/Simulation.UI.Tests/RecurringLaunchViewModelTests.cs`
- This verification directory, harness and before/after images.

## Limitations

Native macOS window-manager acceptance remains unverified. Expanded scheduling or long launch history can still require normal page scrolling at small sizes. Collapse the panel to restore workshop space. Existing B6.6.2 experiment acceptance work is outside this change.
