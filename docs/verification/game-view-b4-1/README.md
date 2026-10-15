# Game View B4.1 — Workflow & Station Identity

## Result

The existing workshop now identifies the six normal stations in sequence: 1 Backlog, 2 Development, 3 Code Review, 4 Testing, 5 Ready for Release, 6 Release. Technical Debt remains an unnumbered maintenance bench with no through-route. No status or stage was added to the simulation.

The main floor aisle and observed package journeys share `WorkflowGeometry.Aisle`. Five restrained yellow arrows show the forward direction, with thin admission connections to the existing queue/active ports. Open-corner pads identify waiting work; solid mint pads identify active work; gold staging identifies completed work awaiting release. Counts and bounded B3.1 stack calculations are unchanged. Review and Testing queue labels name their destinations. Ready for Release uses “staged” to distinguish completed waiting work from active processing. The existing vehicle and dock connect to that staging area.

A dashed mauve return path appears when rework is present or a return was observed. Its direction points back toward Development/Rework. Actual returning packages use that same secondary route only for observed Code Review/Testing → WaitingForRework transitions. The route never creates transitions or implies every item requires rework. Detailed explanations remain in station panels.

Station identities reuse the existing isometric sprite system:

- Backlog: independent planning board beside the incoming shelf.
- Development: second monitor at the existing workstation; collaboration is unchanged.
- Code Review: independent comparison/checklist display beside its workstation.
- Testing: diagnostic console replaces the generic computer sprite on the existing desk.
- Ready for Release: outgoing shelf and gold staging boundary.
- Release: existing yellow vehicle, loading dock and release effects.
- Technical Debt: existing tool cabinet/maintenance bench and debt activity, separate from delivery.

Development WIP and the rework caption were positioned away from the main direction arrow and the Review worker respectively. Secondary figures remain in the contextual panel. Board cards/checklist art identify station purpose; they are not live item counts or approval results.

## Architecture and compatibility

`WorkflowGeometry` owns the shared aisle, status-to-aisle indices, queue destination associations and return routing. `PackageMotion.Position` consumes these routes; all event detection, timing, package bounds and high-speed behavior remain unchanged. `WorkflowMarkings` draws floor cues underneath the existing sprites. The legacy vector view retains its earlier decoration.

Furniture, queue ports and worker homes retain their world positions. The new accessories are individual cached SpriteObjects with ordinary depth sorting. The static object bound increases from 128 to 132 to cover the small fixed addition; queue/team bounds remain unchanged. No new timer, rendering framework, runtime network access or new dependency.

Backlog and Review hit polygons extend to include their new board faces. The polygon set remains non-overlapping. All markings, labels, sprites and highlights use the existing B3.3 camera transform and inverse hit testing. Panels and controls remain outside that transform. Zoom, pan, Fit to View, keyboard selection, animation toggle, accelerated LIVE and checkpoint restoration are preserved.

No engine, capacity, metrics, persistence, lifecycle or scheduling code changed.

## Artwork

Three original assets were generated using the built-in `image_gen` tool and imagegen skill, with the existing project-owned desk as the sole style reference. PNGs are copied unchanged into the embedded resources, preserving RGBA transparency and provenance metadata. Each is 1254×1254. The diagnostic instrument uses the complementary isometric face, consistent with the two floor axes.

- [planning-board.png](../../../src/Simulation.Sprites/Assets/planning-board.png)
- [review-board.png](../../../src/Simulation.Sprites/Assets/review-board.png)
- [test-console.png](../../../src/Simulation.Sprites/Assets/test-console.png)

[Exact generation prompts](prompts.json), [source filenames, rights basis and anchors](../../../src/Simulation.Sprites/Assets/ORIGIN.md), [dimensions/alpha/hash inspection](assets-inspection.json). No third-party stock/Creative Commons license is claimed; these use the same generated-output rights basis as the existing artwork.

## Verification

```sh
dotnet build --no-restore -c Release -m:1 -nr:false
dotnet test --no-build --no-restore -c Release -m:1 -nr:false
dotnet run --project docs/verification/game-view-b4-1/AnimationPreview.csproj -c Release
```

The native macOS harness renders the actual MainWindow, checks actual visual input targets, and dispatches routed input. It verifies balanced flow, Testing queue 35 with zero testers, Review shared-capacity constraint, scheduled release staging, an authoritative observed rework return, and positive debt repayment capacity. It retains B3.3 camera/resize/restore and active worker/package/vehicle checks, including unchanged captured simulation state, shared clock and decode cache under camera input. New tests click both board faces. Screenshots are live-control RenderTargetBitmap captures, not concepts or composites.

Requested sizes: 960×820, 1440×1060, 1840×1120. macOS clamps taller windows to available display height (973 logical pixels during verification). Native layout/input readiness is awaited rather than assuming a fixed frame delay.

Focused tests cover workflow stages, shared floor/motion anchors, queue destinations, return-route endpoints, independently positioned accessory bounds, transformed board hits, restoration and the existing non-overlapping polygon checks. Existing seeded replay/history/checkpoint/metric tests remain in the full suite. Final result: build succeeded with 0 warnings and 0 errors; all 757 tests passed (218 Core, 216 Application, 323 UI), none skipped. Native verification passed. Final logs: `build.log`, `tests.log`, `native.log`.

## Screenshots

- [Normal workflow](screenshots/clean-balanced.png)
- [Testing bottleneck](screenshots/testing-selected.png)
- [Review bottleneck](screenshots/review-selected.png)
- [Observed rework](screenshots/observed-rework.png)
- [Scheduled release](screenshots/release-selected.png)
- [Technical debt maintenance](screenshots/technical-debt.png)
- [200% zoom and selection](screenshots/zoom-review.png)

## Limitations

At compact fit sizes the small decorative details are easier to inspect with zoom; numerical information remains readable in the fixed panel. Waiting/active pads and connecting lines can be partly occluded by people or packages, as they are painted on the floor. Return markings indicate possible observed rework routes, not a count or per-source defect rate. Static generated board/console graphics are illustrative. Native checks use routed events, not physical trackpad automation; B3.3's scroll-to-zoom behavior and lack of pinch magnification remain unchanged. No screen-reader audit was performed. Legacy vector artwork is preserved rather than redesigned.

Stopped at B4.1; no new mechanics, worker management, metrics or navigation.
