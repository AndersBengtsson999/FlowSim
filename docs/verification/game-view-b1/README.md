# Game View Stage B1 — Connected Game World

Stage B1 replaces Stage A's separate islands with one tiled isometric workshop, connected walkways and directional floor markings. Backlog shelves, developer desks, review desks, testing equipment, staging shelves and a parked delivery van form the process. A maintenance bench connects to Development with a non-directional branch: technical debt is not a seventh process stage.

The existing LIVE view selector, playback, intervention editor, checkpoints and Flow Board remain in use. No animation or engine change is included.

## Architecture

All scene code is native Avalonia vector drawing; no bitmap background or external game engine is used.

| Component | Responsibility |
| --- | --- |
| `GameSceneState` | Existing authoritative snapshot/configuration projection; adds only bounded active/waiting package allocation. |
| `GameWorldControl` | Styled property binding, render invalidation and renderer lifetime. No simulation reference, timer or commands. |
| `GameWorldLayout` | Shared floor, tile geometry, walkways, direction markers and stable workstation anchors. |
| `GameWorkstations` | Reusable racks, desks, equipment, maintenance bench and parked vehicle. |
| `GameCharacters` | Reusable worker figures with capacity-slot keys and developer/specialist/tester appearances. These are illustrative slots, not employee identities. |
| `GamePackages` | Separate mint active and amber waiting groups, exact counters, bounded glyphs and grouped remainder. |
| `GameDrawing` | Shared vector primitives, brushes and text cache cleared when the snapshot changes. |
| `GameWorldRenderer` | Layer composition and labels; delegates all world/object drawing. |
| `GameView` | Existing uniform vector scaling, with a readable height floor and outer LIVE scrolling in short windows. |

Station anchors and capacity slots provide reusable positioning for B2. They deliberately do not claim correspondence to individual simulation workers or work-item IDs. Future item movement must use authoritative item IDs/transitions.

## Metrics and representation

- Backlog includes dependency-blocked items once. Its dependency count remains visible below the world.
- Development active packages include development and active rework. Its waiting packages represent waiting rework. The footer preserves the regular development/WIP-limit and rework breakdown.
- Code Review and Testing show separate active and waiting counters and package groups.
- Ready for Release shows its actual waiting count.
- The dock shows cumulative delivered items and actual deliveries on the current day. Cumulative delivered items are **not** drawn as a physical queue. The van remains parked even on days without releases.
- Technical Debt displays the existing effort and ratio ledger, not invented work packages.
- At most eight packages per non-release area. Both categories retain glyphs when both are nonempty; counters and `+N grouped` labels account for every item. Larger queues brighten the staging surface.
- At most six developer figures and four tester figures. Yellow caps distinguish specialist capacity. Review does not add fictitious workers to the shared developer pool. Exact current team sizes are shown below the world.
- Empty shelves, desk equipment and the toolbox are scenery. Only mint/amber taped packages encode work items.

## Viewport

The world uses a 1080×680 logical coordinate system, uniform vector scaling and device-independent coordinates. At 1280×1000 and 960×820 the complete view fits. At the application's 960×680 minimum, the scene retains a readable scale and the existing LIVE scroll viewer exposes the lower captions/legend; content is not discarded. Double-resolution render targets check vector output at 192 DPI. This does not claim testing on physical monitors with every DPI setting.

## Verification

- Full solution: **611 tests passed**, zero failures/skips (218 Core, 216 Application, 177 UI).
- Solution and preview builds: zero warnings/errors.
- Existing Stage A invariance tests still pass: view switching, pause/run speeds, day-zero/load/reset/checkpoint refresh, release-day counters, snapshot partitions and large team/queue aggregation.
- New tests cover active/waiting package conservation at zero, asymmetric and large counts, and applying changed developer/tester counts without advancing a day.
- Avalonia harness exercises the actual MainWindow, LIVE view, selector binding and existing Flow Board; checks capture hashes, preservation of pending edits, two fitting window sizes, short-window scrolling, run-to-day refresh, large queues/teams and rendering without model mutation.
- `git diff` for Core, Application and Infrastructure is empty. Simulation calculations, allocation, WIP, release scheduling and debt logic are unchanged.

The native macOS preview attempt failed before opening a window because Avalonia's display RenderTimer returned error −6661. The successful verification used Avalonia's headless platform with **real Skia drawing** (`UseHeadlessDrawing=false`), actual controls, bindings and layout. Images below are actual UI renders, not concept mockups. Native interactive display verification remains a limitation of this session. The native failure and successful logs are retained separately.

## Screenshots

- [Full app, 1280×1000](screenshots/game-1280.png)
- [Full app, 960×820](screenshots/game-960.png)
- [World detail](screenshots/scene-1280.png)
- [Double-resolution world](screenshots/scene-2x.png)
- [Minimum window, top](screenshots/game-960-short-top.png)
- [Minimum window, scrolled](screenshots/game-960-short-scrolled.png)
- [200 developers, 100 testers, 2000 backlog items](screenshots/large-queues.png)
- [Testing accumulation](screenshots/testing-accumulation.png)
- [Existing Flow Board](screenshots/flow-board-960.png)

## Remaining visual limitations

The world is static. Staff figures show aggregate capacity, not observed individual activity. Shelves and workstations remain fixed in number while figures/packages aggregate. Tiny package positions are illustrative rather than individual item locations. Short windows require vertical scrolling and secondary captions become smaller when scaled. Area inspection, zoom/pan and selection are not implemented in this stage.

## Recommended B2, not implemented

1. Create a bounded presentation transition buffer from existing work-item IDs, stage transitions and actual release events.
2. Animate reusable worker/package objects relative to the existing station anchors; staff activity remains illustrative of aggregate capacity.
3. Use one presentation-only animation clock, with pause/reduced-motion behavior and coalescing at fast simulation speeds. Never delay or advance the engine from rendering.
4. Reset visual transitions on load/reset/checkpoint restore. Trigger van departure only from actual newly observed releases, not view selection or scheduled opportunities without released items.
5. Verify unchanged captures/results, no stale event replay and bounded visual work under large queues.

## Reproduction

Run from the repository root:

```sh
dotnet test SoftwareDevelopmentSimulation.sln -c Release -m:1 -nr:false
dotnet build SoftwareDevelopmentSimulation.sln -c Release -m:1 -nr:false
dotnet build docs/verification/game-view-b1/GamePreview.csproj -c Release -m:1 -nr:false
dotnet docs/verification/game-view-b1/bin/Release/net10.0/GamePreview.dll --headless
```

Omit `--headless` to run the same verification through the native desktop backend when the display is available. The harness opens and closes its own window/session; it does not alter a user's running simulation.
