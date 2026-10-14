# Game View B2.2 — Moving Work Items

B2.2 adds presentation-only package movement to LIVE → Game View → Sprites. **Animate activity** controls both the B2.1 worker effects and packages. No simulation, assignment, release, checkpoint or random-number behavior changes.

## Inspection and limits established before implementation

The existing model exposes real `WorkItem.Id` values and ordered `StateTransition(From, To, Day)` histories. Reading only the current day's suffix preserves multiple transitions for the same item in their actual order, including review/testing queues, rework and release. Aggregate queue differences are never used to invent journeys.

The model has no within-day timestamps or physical positions. Paths, durations, easing and spacing are presentation choices. They are **replays of observed transitions**, not an additional in-flight simulation stage. Queue counters and bounded stationary stacks show the latest authoritative snapshot immediately, even while a replay finishes. Moving packages are not extra WIP. Worker poses remain illustrative aggregate activity, as in B2.1.

## Architecture

- `GameSceneState.From` copies immutable journeys for at most 12 changed items and counts all changed items. It reads each item's last transition first, then the current-day suffix for selected items. A journey longer than 32 stages is omitted in its entirety rather than inventing or truncating its route. Current implementation favors session item order when sampling a busy day.
- A weak-table session identity distinguishes a newly restored session even when the checkpoint has the same day and item IDs. This token belongs only to presentation; it is never persisted in simulation state and does not consume the simulation RNG.
- `PackageMotion` accepts immutable scenes and elapsed presentation time. A consecutive day in the same session replaces the current bounded batch. Each package retains the observed item ID and visits all observed stages in order. There is no pending animation queue.
- One existing `PresentationClock` and one existing dispatcher timer drive workers and packages. No package timer, background scheduler, engine callback or runtime network access was added.
- Fixed aisle waypoints connect station ports, including reverse rework paths. Interpolation uses smoothstep and route distance. Positions convert through the existing 2:1 isometric projection. No pathfinding or worker movement was introduced.
- `SpriteBatch` caches all static draw commands and merges at most 12 moving sprites by layer, ground depth, stack order and ID. Existing occlusion masks and worker effects are preserved. Sprites share the existing decoded bitmap cache; moving frames do not allocate or decode bitmaps.

## Playback and synchronization

| Situation | Package behavior |
| --- | --- |
| 0.5×, 1×, 2× | Replay selected current-day journeys over 85% of the day interval. |
| 5×, 10× | Skip individual package motion; display latest queues immediately. Worker effects remain enabled. |
| Pause / resume | Freeze elapsed motion; continue from that position on resume. |
| Manual step while paused | Synchronize queues without creating a new replay. |
| Speed change | Cancel current movement; new eligible days can animate at the new speed. |
| Animation disabled / vector mode / hidden or offscreen view / detach | Cancel packages; no stale replay when shown again. |
| Checkpoint restore / load / backward day / skipped days / reset | Cancel and synchronize immediately. A same-day restored session is detected too. |
| Repeated notification for the same scene | Does not replay transitions. |

## Queues and label placement

Review, testing, ready-for-release, rework and dependency waiting use bounded grouped packages with exact counters. Dependency waiting has a separate small stack but is subtracted from the ordinary backlog stack; the displayed backlog total still includes both. Stacks have 2–6 representatives regardless of queue size. A subtle gold outline marks waiting queues that grew since the preceding observed day. Large queues also retain the amber accumulation tray treatment.

Backlog/dependency counters were moved clear of the delivery vehicle. Technical-debt and testing titles were moved clear of furniture; review/testing queue captions are shorter. The footer explicitly distinguishes real transition replays from current queue totals. Technical debt remains a side activity, never a mandatory route stage. The delivery vehicle remains parked.

## Artwork

No new artwork or image dependency. Packages, furniture, characters, plant and parked vehicle reuse the original embedded workshop PNGs and existing cache. See the B1.3 and B2.1 provenance documentation. No emoji, external icon set or internet-loaded image was introduced.

## Verification and artifacts

- `PackageMotionTests` covers actual item identity/order, multiple same-day transitions, endpoint placement, real rework/release history, bounded batches, high-speed skipping, replacement/cancellation, pause/resume, same-day restoration identity, backward/skipped days, duplicate snapshots, queue totals and dependency subsets.
- Three seeds (7, 12345, 9876), 100 days each: compare serialized **complete Live captures**, including histories and shared checkpoints, with and without animation projection/sampling. Both runs receive the same Day 41 configuration intervention.
- Existing sprite integration checks now assert backlog = ordinary backlog + dependency subset. The complete existing solution test suite is retained.
- `AnimationPreview` exercises the actual native macOS Avalonia controls: LIVE bindings, speed binding, toggle, vector switch, pause pixel equality, resizing at 1280 and 960, navigation, minimized/hidden/offscreen views, detach/reattach, checkpoint session replacement, large teams and caches.
- The capture host explicitly advances a real fixture between immutable snapshots. Production `GameWorldControl` supplies all rendered motion. The animation itself never advances that fixture. This is a verification host, not a second simulation model.
- `screenshots/scene-2x.png`, `live-960.png`, `moving-packages.png` and `large-team.png` show actual rendered controls. `moving-packages.gif` encodes 48 native captured frames using measured presentation timestamps. The optional Pillow encoder is tooling only, not an application dependency.

**Results:** Release build: 0 warnings, 0 errors. All 655 tests pass (Core 218, Application 216, UI 221); the final UI rerun also passes. Native macOS: 30 renders with 40 static objects and the full 12 moving packages took 425 ms (about 14.2 ms/render). Advancing 100 actual LIVE days took 73 ms in this fixture. The previous isolated sprite prototype also passes its native regression checks. Core/Application/Infrastructure production files are unchanged.

Build, test and native measurements are saved beside this report. These are local software-renderer checks, not a GPU benchmark or an exhaustive cross-platform performance claim.

## Remaining limitations and B2.3

At busy days, only the first eligible 12 item journeys are replayed; the exact counters still cover all work. Intraday timing and physical paths are illustrative. Static queue stacks are representative rather than individually identified inventory. Long multi-stage journeys can be difficult to follow at 2×; use 0.5× for visual inspection. Very large numeric labels beyond the tested thousands may need further formatting. B2.1 bodies remain static poses with workstation effects.

Stop here for visual review. For B2.3, first agree how observed release batches should drive a parked vehicle's departure and return. Use actual release events, the same presentation clock, bounded playback and cancellation rules; keep release scheduling/capacity entirely in the simulation. Do not introduce autonomous logistics or worker travel as part of that visual step.
