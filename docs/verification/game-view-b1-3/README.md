# Game View B1.3 — static LIVE sprite integration

Verified on macOS, 2026-10-13. Select **LIVE → View: Game View → Sprites**. The same selector offers **Legacy vectors**. Sprites are the default Game View renderer; Flow Board remains available and keeps its existing default navigation.

The sprite world consumes the existing LIVE session. It has no simulation commands, clock, item transitions or employee scheduler. Static furniture, characters and packages replace the vector artwork while preserving the connected B1 floor, process arrangement, walkways and the separate debt branch.

## Actual rendered results

These are RenderTargetBitmap captures of the running native macOS Avalonia controls, using software rendering and the application's MainWindow/LiveViewModel. They are not concept images or composited mockups.

- [Whole application, 1280×1000](screenshots/game-1280.png)
- [Sprite workshop, Day 150, two specialists, scheduled release](screenshots/scene-1280.png)
- [Same scene at 2× pixel density](screenshots/scene-2x.png)
- [One developer and one tester](screenshots/small-team.png)
- [Five developers and two testers](screenshots/five-developers-two-testers.png)
- [200 developers, 100 testers, 40 specialists, 2,000 backlog items](screenshots/large-queues.png)
- [77-item Testing queue and nonzero Technical Debt](screenshots/testing-accumulation.png)
- [Legacy vector fallback](screenshots/legacy-vector.png)
- [960×820 application](screenshots/game-960.png)
- [960×680 application, scrolled](screenshots/game-960-short-scrolled.png)

## Shared architecture

`src/Simulation.Sprites` is an Avalonia-only library, with no reference to Core, Application or Infrastructure. The original prototype and the main application both use it:

- `SpriteScene`: image definitions, optional frame clips, world positions, pose selection, anchors, stable depth order and per-instance scale.
- `SpriteAssets`: one decoded bitmap per embedded resource URI; seated full-body/upper-body layers share the same bitmap. Frame bounds are validated. Images are disposed when their owning view detaches or preview window closes.
- `SpriteBatch`: precomputed, depth-sorted image commands. Repainting performs DrawImage calls without decoding, sorting or recreating scene objects.
- `WorkshopFurniture` and `WorkerVisual`: reusable desk/computer/chair assemblies and separately positionable character layers. No complete workstation is baked into an image.
- `SpriteCatalog`: twelve original PNG resources and curated source rectangles, anchors and nominal scale. The LIVE scene uses eleven textures; the floor PNG is used only by the isolated prototype.

`GameSceneState.From` remains the single read-only projection of current LIVE data. `SpriteWorkshop` creates a bounded presentation from that state. `GameWorldControl` chooses the sprite or preserved vector renderer; `LiveViewModel.GameRenderingMode` changes presentation properties only. Snapshot changes rebuild the small sprite batch once, not once per frame. Legacy labels and sprite labels have bounded caches for the current scene.

World coordinates are `(X,Y,Elevation)`, with a 96×48 DIP isometric grid. The LIVE origin is `(540,17)`; elevation moves an object up by 48 DIP per unit and does not affect ground depth. Catalog anchors are pixels relative to the source rectangle. Destinations multiply pixels by catalog PixelsToDip and instance scale. `SortAnchor` keeps a computer and upper-body overlay attached to their desk's draw order. Stable ordering is layer, ground X+Y, stack order, then object ID. B1's existing continuous floor uses its established approximate isometric perspective; the floor is not replaced by a background illustration.

The logical world remains 1080×680 DIP. The existing Viewbox scales it uniformly. Short windows retain a minimum scene height and use LIVE's existing scroll viewer. Labels outside the world wrap normally. High-quality bitmap interpolation and high-resolution embedded sources support 1×/2× rendering.

## Data semantics and visual limits

All stage counters come from the authoritative end-of-day item state, not admission-time WIP occupancy. Development, active Rework and waiting Rework are separately counted. Dependency waiting is explicitly an included subset of Backlog, displayed in the bottom ledger. Review and Testing each have separate active and waiting trays. Empty queues show no packages.

Each tray shows at most 2–6 sampled packages, with an exact counter. The scene is capped at five logical figures (three developers, two testers), with exact configured totals and grouped counts. There are fewer than 70 image commands even for very large teams and queues. Additional rendering layers are not additional people.

D marks developers, yellow S marks specialists, and cyan T marks testers. Specialists are included in the developer count. Figures are **illustrative capacity**, not employee identities or verified assignments. A working pose illustrates the presence of developer/tester work; a two-person collaboration vignette is permitted only when collaboration capacity is positive and the conservative pose ceiling permits two. It never derives a person count from utilization, assigns a person to an item, or adds reviewers to the developer pool. Remaining standing figures are illustrative, not measured idle employees. Role totals and the previous completed day's used/available capacity remain authoritative.

Pose ceilings never exceed the minimum of the previous day's available capacity, current configured capacity, and current available headcount (rounded down). Day zero and fractional capacity below one show no working figures. This is deliberately conservative and does not claim person-level scheduling. The collaboration caption explicitly identifies the pair as illustrative.

Release shows actual cumulative releases plus the current-day delta. Packages at the loading bay are a bounded sample of **today's deliveries**, not a second WIP queue or items still waiting to release. The vehicle never moves. Technical Debt shows the existing amount/ratio at a maintenance bench on a side branch, without invented tasks or repayment assignments.

## Assets and provenance

Reused unchanged: developer, seated developer, collaborating colleague, desk, computer, chair, package, plant, and the prototype's floor. Added: empty shelf, stationary delivery van, maintenance cabinet/tool board. Each is independently positionable; the furniture has no baked-in people or simulated work packages.

All twelve assets are original AI-generated PNGs. The three additions used the built-in image tool and the existing desk as a style reference. See [asset inventory and usage-rights basis](../../../src/Simulation.Sprites/Assets/ORIGIN.md), [exact prompts](prompts.json), and [alpha/dimensions/hashes](assets-inspection.json). Runtime images are embedded `avares` resources: no generation API, URL fetch, key or network access is used. Old PNG hashes are preserved.

## Verification

- Release solution build: **0 warnings, 0 errors**.
- Full suite: **619 passing** (218 Core, 216 Application, 185 UI).
- New focused tests: complete capture/history/checkpoint/draft preservation across render modes; exact stage/subset/release counts; 1/1, 5/2, specialist and 200/100 teams; bounded geometry; fractional availability; conservative illustrative collaboration; stable draw order and frame placement. Existing playback equivalence now also alternates sprite/vector modes at 0.5× and 10×.
- Native UI harness: actual selector bindings, sprite/vector switching without capture changes, Flow Board visibility, pending intervention draft, fast advance, reset, 1280/960/short-window layouts, 1×/2× capture and embedded resource loading.
- Native performance smoke check: 30 scene renders in **479 ms**; 11 decoded textures, 39 image objects for the Day 150 fixture. Fast advance Day 150→250 took **278 ms**. These are single-machine smoke timings, not a benchmark guarantee. Repeated draws did not decode additional images.
- The B1.1/B1.2 prototype also passed its native regression: independent objects, poses, layer overlap, cache, resizing, single-desk mode and 1×/2× rendering after extraction to the shared library.
- [Source integrity](integrity.json) verifies production Core/Application/Infrastructure source files against their pre-B1.3 hashes. No engine, capacity allocation, history, checkpoint, comparison or release logic was edited.

Logs: [build](build.log), [tests](tests.log), [native UI](native.log), [prototype regression](prototype.log).

Reproduce from repository root:

```sh
dotnet build SoftwareDevelopmentSimulation.sln -c Release -m:1 -nr:false
dotnet test SoftwareDevelopmentSimulation.sln -c Release --no-build -m:1 -nr:false
dotnet run --project docs/verification/game-view-b1-3/GamePreview.csproj -c Release
SPRITE_VERIFICATION_OUTPUT=docs/verification/game-view-b1-3/prototype-regression dotnet run --project tools/SpritePrototype -c Release -- --verify
```

## Limits and proposed B2

This is a static integration suitable for visual review. Small windows reduce character detail; role markers and counters remain the source of meaning. Tester roles currently reuse the character artwork with cyan badges, rather than a separate costume. The seated upper-body crop is specific to this camera angle. The floor's projection and generated artwork are close but not an exact tile-production asset system. Large teams are represented by grouped figures, not complete office populations. Shelves remain empty fixtures, with real packages in their adjacent trays.

B2 should introduce an optional presentation-only transition layer between immutable snapshots and SpriteBatch. Stable item IDs and authoritative StateTransition events may drive bounded package travel, with completion snapping to the latest snapshot during fast playback, restore, reset or skipped days. Reuse a single UI animation clock, independent of the simulation clock. Pause/reduced-motion should freeze or bypass visual interpolation without delaying simulation commands. Named-person movement must wait for authoritative assignment data; aggregate capacity must not become a new scheduler. The existing pose/frame API can accept curated frame sets later, with shared texture caching or an atlas if profiling justifies it. First obtain walking/directional/seated animation art and review occlusion masks. None of this animation is implemented in B1.3.
