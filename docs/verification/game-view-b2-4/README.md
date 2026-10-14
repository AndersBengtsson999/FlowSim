# Game View B2.4 — Living Office

Living Office adds bounded, illustrative worker movement to the existing sprite Game View. It does not assign real employees, complete work, consume capacity, or advance the simulation. The existing Animate activity toggle controls it alongside packages and releases.

## Architecture

`WorkerMotion` accepts immutable `GameSceneState` snapshots. It retains at most four developer slots and two tester slots, with stable visual IDs and D/S/T markers. These IDs are presentation slots, not employee records. `WorkshopActivity` remains the authority for the shared pose budget, including maintenance and collaboration. A moving or resting character never adds a productive pose.

Activity selects a destination: development, review, testing, maintenance or the collaboration position. A single character at a time follows fixed, chair-side approach waypoints and the central aisle. Points are stored in world coordinates using the existing 2:1 isometric projection. Distance-based interpolation eases departure and arrival; four facing directions and two walking poses are sampled from one cached RGBA atlas. Moving sprites and seated upper-body masks are depth-sorted with existing furniture and packages. Monitor effects follow the currently pictured occupants.

The host uses its existing presentation clock and dispatcher timer. There is no timer per worker, simulation RNG access, task assignment or work-item mutation. Only the latest destination is retained. A character finishes its current path before adapting to a changed destination; an obsolete destination receives an idle pose, never stale productive activity. Slot-based deterministic intervals introduce brief stand-up/return trips and idle gestures. Collaboration partners approach, gesture and occasionally return to their resting position.

Pause freezes positions and poses. Above 2× speed, snapshots synchronize immediately and unnecessary walking is skipped. Reduced motion, vector mode, hidden/offscreen/minimized/detached views cancel trips. New session identity, backwards time and skipped days resynchronize without replay. Team changes remove/add only the bounded visual slots. Larger teams remain aggregated.

## Artwork and coordinate conventions

`src/Simulation.Sprites/Assets/developer-walking.png` is the only new bitmap, created with the built-in image generator from the project's original developer identity. It is embedded locally: no runtime network access. The PNG bytes and alpha are unmodified. Four asset aliases share one decoded texture.

The atlas is 1774 × 887 pixels. Columns face screen SE, SW, NW and NE. Row one is contact; row two is passing with a raised knee. Source rectangles and calibrated foot anchors are in `SpriteCatalog.Walking`; rendering uses .52 DIP per source pixel before the common .34 scene scale. Frame sampling is 5 Hz; source rectangles remain independent from world position. Existing desk, seated developer, gesture, computer, plant, package, chair, shelf, maintenance and vehicle artwork retain their provenance in `src/Simulation.Sprites/Assets/ORIGIN.md`.

Exact generation prompts, source filename and output hash are in `artwork.json`. These are original generated assets under the same output-use basis as the existing sprite set, not third-party stock assets or an invented stock license.

## Verification

- Full Release solution build: **0 warnings, 0 errors**; see `build.txt`.
- Full test suite: **691 passed** (218 Core, 216 Application, 257 UI), including 15 new worker-motion test cases; see `tests.txt`.
- Native macOS Avalonia verification: see `native.txt`. Uses the actual GameWorldControl and real simulation snapshots, with an isolated read-only preview for observing the clock independently from simulation progress.
- Three seeds, 100 days each, identical interventions: complete capture (including histories/checkpoints) and final results match with Living Office exercised versus disabled.
- Tests cover world interpolation/facing, continuous approach, latest target, collaboration, maintenance, small-team review, headcount/shared capacity, deterministic ambient variation, pause, high speed and restoration. Native checks additionally cover actual UI bindings, reduced motion, mode changes, hiding, minimize, viewport, detach, resizing, 2× capture and large teams with twelve moving packages.
- Performance on this macOS machine: 30 renders with three moving packages took 464 ms; 30 large-team renders with twelve moving packages took 543 ms (about 18.1 ms/render). Four developer identities walked in the capture; decoded assets remain cached. This is a local render benchmark, not a cross-device frame-rate guarantee.
- Native recording: `workshop-animation.gif`; timestamped positions, poses and directions in `capture-timing.json`. Screenshots are actual Avalonia renders, not generated mockups.

Reproduce from repository root:

```sh
dotnet build --no-restore -c Release -m:1 -nr:false
dotnet test --no-restore -c Release -m:1 -nr:false
dotnet run --project docs/verification/game-view-b2-4/AnimationPreview.csproj -c Release
python3 docs/verification/game-view-b2-4/encode_capture.py
```

The encoder requires Pillow and only assembles native captures; it does not edit sprite artwork.

## Limits before further polish

This is a deliberately quiet, fixed-layout office choreography. One walker prevents opposing aisle traffic; activity destinations can therefore lag rapidly changing aggregate data at normal speed. Small teams still reserve unpictured maintenance capacity using the existing maintenance diagram instead of inventing an extra person.

The walk has two contact/passing drawings per facing, not a full four/eight-frame left/right gait. It is visibly more alive, but the step cycle and immediate seated/standing pose change remain stylized. The same original character identity is reused across slots; role badges distinguish developers, specialists and testers, rather than unique facial/clothing variants. Four-direction standing/seated art, a fuller gait and authored sit/stand transitions are the practical next art-polish step. No limb stretching, rigid-body rotation or independent employee scheduling was added.

No engine, allocation, work-item, WIP, release, debt or economic logic was changed. Stop at B2.4 for visual review.
