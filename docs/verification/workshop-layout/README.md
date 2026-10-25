# Game View workshop layout

## Changes

The old layout reserved the full KPI strip height and could shrink the workshop to 180 pixels when supporting content expanded. The layout now protects a 400-pixel workshop container (340 in windows shorter than 850), bounded by the available width and scene aspect ratio. It grows further when space permits. Expanded content uses the existing LIVE page scroll instead of reducing the workshop below this floor.

At Game View widths of at least 1000 pixels, Team Performance uses a compact 220-pixel column beside the workshop. Narrow layouts put the cards below the workshop. Cards have reduced padding and value-row height; the narrow column puts its heading above Analyze/KPIs controls to avoid overlap. Existing KPI selection and Explorer remain available.

Customer Launch keeps the existing collapsed session default and preserved disclosure choice. Running LIVE displays “Pause the simulation to enable customer launch.” even while the panel is collapsed. Commands retain their existing pause requirements.

No renderer, camera, animation, simulation, KPI, launch rule, insight analysis, scenario or experiment changes. Layout resizing delegates zoom/pan preservation and Fit to the existing camera. Earlier uncommitted launch/insights UX work was retained.

## Verification

- Full Release build: successful, zero warnings/errors.
- Full test suite: 970 passed (218 Core, 295 Application, 457 UI), no failures/skips.
- Four additional layout/camera test cases verify readable bounds, aspect cap, manual zoom preservation and fitted scene centering.
- Actual Avalonia headless/Skia UI verification at 1440×900, 1280×800 and 980×800 verifies default collapse, disclosure resizing, manual zoom preservation across window resizing, Fit centering, KPI availability, unchanged serialized simulation capture and disabled launch/scheduling while running.
- Captures use the same deterministic Day 35 fixture. Before images were generated before this layout change. They include the preceding Customer Launch UX work.

Run from repository root:

```sh
dotnet run --project docs/verification/workshop-layout/Verification.csproj -- --headless
```

## Modified files for this task

- `src/Simulation.UI/Views/GameView.axaml.cs`: responsive KPI placement and container sizing.
- `src/Simulation.UI/Views/WorkshopLayout.cs`: presentation-only sizing helper.
- `src/Simulation.UI/Views/KpiDashboardView.axaml` and `.axaml.cs`: compact cards and responsive heading.
- `src/Simulation.UI/Views/ProductLaunchView.axaml`: collapsed running explanation.
- `src/Simulation.UI/ViewModels/LiveViewModel.ProductDelivery.cs`: requested running message.
- `tests/Simulation.UI.Tests/WorkshopLayoutTests.cs`: new layout cases.
- `tests/Simulation.UI.Tests/LaunchInsightsUsabilityTests.cs`: updated exact message assertion.
- This directory: harness and before/after evidence.

## Screenshots

For each size, compare `before/headless/screenshots/game-WIDTH-HEIGHT.png` with `after/headless/screenshots/game-WIDTH-HEIGHT.png`. Additional images show expanded launch details and the collapsed running message.

## Remaining limitations

These are actual Avalonia renders on macOS using headless Skia, not native desktop screenshots. Native window-manager acceptance remains unverified. Small windows and expanded launch details require ordinary page scrolling; at 1280×800 the last KPI may require scrolling, and at 980×800 the cards are below the workshop. No extra nested scroll region was added. Manually zoomed scenes may intentionally extend outside the viewport; Fit restores full-scene visibility.
