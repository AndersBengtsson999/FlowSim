# Game View B2.3 — Releases, collaboration and maintenance

B2.3 completes the requested presentation layer on top of B2.1/B2.2. Use LIVE → Game View → Sprites → **Animate activity**. Simulation production code remains unchanged; no person scheduler, new lifecycle stage, scoring or economy was added.

## Observations and data boundaries

The existing model exposes ordered item transitions, cumulative delivered counts, daily release counts, aggregate collaboration capacity and actual used debt-repayment capacity. It does not expose named worker assignments, detailed collaboration events or intra-day delivery timestamps.

Package journeys retain real item identities. The vehicle and loading samples illustrate an actual reported delivery batch. Collaboration and maintenance are capacity vignettes, not assignments to named people or particular work items. Animation never advances the model, consumes its RNG, modifies queues, triggers release or edits history/checkpoints.

## Release sequence

`PackageMotion.Release.cs` is another part of the existing B2.2 `PackageMotion` controller, not a new animation engine. It consumes the same immutable `GameSceneState`, continuity token and `PresentationClock`. A delivery starts only when consecutive snapshots agree that the positive cumulative Released delta equals `ReleasedToday`. A scheduled opportunity with no actual released items dispatches nothing.

At 0.5×–2×, the bounded package replay reaches the loading bay. Up to three representative loading packages enter the existing van; the bay glows briefly. The van drives nose-first down a short 2:1 loading ramp, pauses, and reverses back into its parking position. A `+N Released · Day D` notice identifies the actual release day. The cumulative counter always shows current model state.

The package travel portion takes 85% of the day interval. Loading then takes 0.35 seconds, departure 0.6, away 0.3, return 0.6. These are presentation timings, not physical timestamps from the model. The parked van artwork remains one independent sprite; its doors/wheels are not articulated.

Frequent flow-based events coalesce into the currently active trip without restarting it or queuing another van. The notice then states the first and last included simulation days and exact summed release count. Further released item journeys during that trip are suppressed, so packages do not chase a departing vehicle. Other lifecycle movements continue. Representative loading cubes are scenery, not fictional item IDs, inventory or additional WIP.

At 5×/10×, the van stays parked. Actual events are combined into an expiring 0.8-second feedback window with a count and observed day range. There is no unbounded event queue and the model never waits for a trip.

## Collaboration and technical debt

The standing collaborator now uses a consistent neutral/pointing sprite strip, keeping the same character and calibrated foot anchor in both poses. A periodic two-pose gesture and the existing shared-screen effect show attention toward the seated developer's workstation. Positive observed collaboration capacity is required; this does not imply twice the productivity.

Actual used debt-repayment capacity activates the maintenance diagram and, where representable, a standing developer at the existing tool board. Debt balance alone does not activate maintenance. There are no debt work items or debt releases.

The visible developer budget now supports four fixed vignettes, with no more developers than configured headcount or the conservative completed-day/current-team capacity ceiling. Observed maintenance reserves a slot first; primary development/rework, review and an optional collaboration partner use the remainder. The maintenance figure is part of that budget, not an added person. Its position stays fixed; it never walks or teleports between workstations.

For teams with fewer than four developers, the animated maintenance diagram serves as a worker-equivalent and reserves the same slot instead of creating another figure or moving an existing seat. Thus three people cannot be shown simultaneously as a collaboration pair, reviewer and maintenance worker. `BusyDevelopers` includes this unpictured maintenance equivalent. Testers remain in their separate capacity pool.

## Reused rendering and lifecycle

The single existing dispatcher timer drives gestures, workstation effects, packages, loading and the van. `SpriteBatch` caches static image commands and merges bounded moving objects by the existing depth order. Hidden parked/loading objects are replaced during a trip; sprites share the decoded bitmap cache. No per-package timer or bitmap decoding/allocation occurs in the render loop.

Pause freezes the shared elapsed time, including a van partway along its route. Resume continues it without duplicate delivery. A manually advanced paused day synchronizes counts without dispatch. Animation off, vector mode, hidden/offscreen views, detach, speed changes, skipped/backward days, reset, session loading and checkpoint restoration cancel stale motion. Same-day restoration is distinguished by the presentation session identity. Subsequent eligible observed events can animate normally.

Counts and labels were placed clear of the vehicle's route. The short loading ramp gives the vehicle a visible surface beyond the connected workshop floor. The application shell/navigation is unchanged.

## Artwork and provenance

One added asset: [`developer-gestures.png`](../../../src/Simulation.Sprites/Assets/developer-gestures.png), produced with the built-in OpenAI image generator using the existing original `developer.png` as its reference. No new outside artwork or runtime service is required. The generated PNG was copied unchanged with RGBA transparency and provenance metadata. [Exact prompt, origin, alpha range and SHA-256](artwork.json); [asset inventory and output-use basis](../../../src/Simulation.Sprites/Assets/ORIGIN.md).

The 1774 × 887 strip contains two 887 × 887 cells. Foot anchors: neutral (490, 820), pointing (414, 820) relative to each cell. PixelsToDip 0.28, normal scene scale 0.34. Idle uses neutral only; collaboration/maintenance play neutral ×3, pointing ×2, neutral at 1.5 frames/second. This is an intentional limited-pose gesture, not skeletal animation.

## Verification

- Full Release build and solution tests; final UI rerun after presentation copy/layout refinements.
- Focused tests for scheduled and flow-based events, exact batch counts, no release on an empty scheduled day, within-trip aggregation, high-speed feedback, cancellation, same-day restoration, pause/resume, gesture frames and shared capacity including small teams.
- Three seeds, 100 days each, identical Day 40 release intervention and shared checkpoint: serialized complete simulation captures/history and final result metrics match with and without animation projection/sampling. B2.1/B2.2 invariance tests remain in the suite.
- Native macOS Avalonia verification checks actual LIVE bindings, alpha assets, gesture sampling, all release phases, frozen vehicle pixels, cancellation, resizing at 1280 and 960, view navigation, offscreen/minimized windows, detach/reattach, large teams/queues and the full 12-package batch.
- `workshop-animation.gif` is 48 actual native-rendered frames. It begins with a real collaboration/repayment fixture, then advances the model explicitly in the verification host until an actual release is reported, and records the complete trip. Animation itself never advances that model. Timestamps/day/phase are in `capture-timing.json`.
- Screenshots include the full UI, small window, active maintenance/collaboration and each release phase. Build/test/native logs and integrity evidence are stored here.

**Results:** Release build: 0 warnings, 0 errors. All **676 tests pass** (218 Core, 216 Application, 242 UI), with the final UI rerun also passing. Native capture covers Loading, Departing, Away and Returning. Thirty software-rendered frames with 41 static objects plus 12 moving packages took 423 ms (~14.1 ms/frame); 100 actual LIVE days advanced in 67 ms for this fixture. The older isolated sprite prototype also passes its native regression checks. Core/Application/Infrastructure production files are unchanged.

## Limitations

Character gestures use two poses. Loading, route timing and capacity placements are illustrative. A coalesced trip represents multiple observed deliveries rather than one literal real-world vehicle inventory. Intraday exact ordering is preserved for selected item journeys, but physical times do not exist in the simulation. The renderer keeps representative queues and a bounded selection of package movements; it does not draw every item. Fractional available capacity below one has no busy character under the pre-existing conservative pose ceiling, though actual metrics remain visible. Platform measurements here are local macOS software-renderer checks, not a universal GPU benchmark.

## Final visual review checklist

1. At 0.5×/1×, inspect the shared-screen gesture and maintenance diagram; confirm feet, hands, furniture and labels overlap naturally.
2. Inspect a real scheduled delivery: packages, loading, departure, dated notice and return. Check cumulative Released against the Flow Board.
3. Check a scheduled day with no actual release and a period with zero repayment: neither should invent activity.
4. Try frequent flow-based deliveries and 5×/10×; counts must stay current, trips must not accumulate.
5. Pause during departure, resume, change rendering mode, and restore an earlier/same-day checkpoint; verify no stale trip replays.
6. Inspect one-, two- and three-developer teams: maintenance must not create extra people or leave all shared capacity simultaneously busy elsewhere.
7. Resize to a small window and inspect large queues; labels/counters must remain distinct.

Stop after this B2.3 implementation and review. No further gameplay, worker movement or engine expansion is included.
