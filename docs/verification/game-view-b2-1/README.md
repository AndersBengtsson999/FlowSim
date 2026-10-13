# Game View B2.1 — worker and workstation activity

Implemented and verified on macOS, 2026-10-13. In **LIVE → Game View → Sprites**, **Animate activity** controls the independent presentation animation. The option defaults on. Turning it off does not pause, resume or step the simulation. Pausing LIVE freezes the entire activity animation, including ambient effects. The adjacent status distinguishes simulation pause from animation being disabled. Legacy vectors remain available.

## Rendered demonstration

[Living Workers animated capture](living-workers.gif) shows 48 actual Avalonia-rendered frames over approximately 7.2 seconds. The preview consumes an immutable real Day 8 snapshot with observed development, collaboration, review, testing and debt repayment. The verification host enables the presentation clock independently to prove that the underlying paused simulation, history and checkpoint capture remain unchanged. This is a capture of the production GameWorldControl, not a generated animation or scene mockup. In the normal application, the animation-running property is bound to LIVE playback and productive animation freezes on Pause.

- [Actual application at 1280×1000](screenshots/live-1280.png)
- [Application at 960×820, with animation control and pause status](screenshots/live-960.png)
- [Workshop at 2× density](screenshots/scene-2x.png)
- [Large team](screenshots/large-team.png)
- [Preserved vector renderer](screenshots/legacy.png)

The static figures now occupy stable workstation anchors across activity changes. The third displayed developer has a review seat. There is no movement between stations, package travel, queue transfer, vehicle movement or individual employee scheduler.

## Architecture

Three independent layers are used:

1. **Authoritative state:** the existing LIVE snapshot and configuration. `GameSceneState` copies used Development, Review, Rework, Testing and Debt Repayment capacity in addition to its existing stage counts and capacity ceilings. No simulation source was edited.
2. **Visual state:** pure `WorkshopActivity.From` selects a small number of illustrative poses from those observations. `SpriteWorkshop` combines them with fixed scenery slots, independent furniture and the existing cached assets. It does not store person IDs or work-item assignments.
3. **Animation time:** `PresentationClock` receives monotonic timestamps and accumulates elapsed time only while allowed to run. It has no reference to Core/Application, randomness, simulation commands, work items or playback speed. One background-priority Avalonia DispatcherTimer per attached GameWorldControl requests redraws at up to 30 Hz for the whole workshop.

The timer samples current elapsed time rather than queueing frames or replaying missed transitions. LIVE speed never enters the animation clock. A new snapshot replaces the small visual projection; a fast advance does not accumulate animation jobs. Avalonia still shares a UI thread with other UI work, so rendering has a finite cost; low-priority, bounded redraws avoid an animation backlog rather than promising zero UI-thread cost.

The clock stops for: paused simulation, disabled animation, vector mode, no session, invisible ancestors, an offscreen scroll viewport, a minimized window, and detachment. Ancestor handlers are removed on detach and cached bitmaps are disposed. Reattachment restores the latest scene with one timer. The existing Flow Board/LIVE navigation remains unchanged.

`SpriteBatch` now supports direct sampling of optional frame clips, precomputes destination rectangles for every frame, and retains depth-sorted draw commands. `ISpriteDecoration` draws workstation effects immediately after a computer sprite and before the worker's upper-body layer, preserving overlap. It never transforms or warps the character image. `WorkshopEffects` caches screen geometry, pens and coordinates per snapshot; frame updates decode no images and allocate no bitmaps.

## Activity mapping

These are aggregate signals, not verified personal assignments. End-of-day occupancy alone cannot establish that work occurred, and work performed earlier in the day does not imply an empty stage remains active. The mapping deliberately requires both active stage work and positive observed capacity.

| Visual state | Required evidence / capacity limit | Rendering |
| --- | --- | --- |
| Working | Active Development or Rework, positive corresponding used capacity, at least one developer pose equivalent available | Seated pose; slow code-line emphasis and faint key highlights |
| Reviewing | Active Code Review and positive used Review capacity, a remaining developer pose equivalent | Seated pose; lavender screen emphasis with a slower reading cadence |
| Testing | Active Testing and positive used Tester capacity, at least one tester pose equivalent | Seated tester; cyan code/status activity and subtle key highlights; dots do not indicate test outcomes |
| Collaborating | Existing primary Development vignette, positive collaboration capacity, and another developer pose equivalent available | Stable seated/standing pair, warm shared screen emphasis; explicitly illustrative |
| Idle / neutral | No supported activity or insufficient whole pose equivalents | Static standing/seated pose, dim screen and a very low-contrast slow ground halo while animation runs |
| Debt repayment | Positive observed repayment capacity and developer capacity ceiling above zero | Slow maintenance accent plus actual last-day repayment capacity; no extra employee or fictional debt task |

Developer activity shares one combined ceiling. The display uses at most three developer figures and two tester figures. Development is considered first, then one review vignette, then an optional collaboration partner; this is a fixed presentation priority, not an allocation model. Tester activity uses its separate capacity ceiling. Neither work-item count nor utilization percentage determines a busy-worker count. Exact configured role totals and grouped counts remain visible.

The conservative ceilings remain the minimum of observed available capacity, current configured capacity and current available headcount, rounded down. Fractional capacity below one can therefore perform real work without showing a productive worker figure. All Day 0 poses are neutral. Some simultaneous activity may be omitted because of limited visual slots.

For a one- or two-developer team, review-only activity can be illustrated at the primary desk without moving a figure to another station. This is another reason the figures must be read as capacity illustrations, not named assignments. A station's counters remain authoritative regardless of the illustrative pose. Idle figures similarly do not measure a count of idle employees.

## Assets and visual limits

No PNGs were added or edited. All twelve existing sprite PNGs remain byte-for-byte unchanged; [their origins and usage-rights basis](../../../src/Simulation.Sprites/Assets/ORIGIN.md) still apply. New screen/keyboard/ground/maintenance effects are locally authored vector geometry in `WorkshopEffects.cs`, not external artwork or runtime downloads.

The new Reviewing/Testing/Idle seated clips are explicitly **single-frame fallbacks** using the existing seated artwork and the same anchors. No real hand, arm, head or breathing sprite frames exist yet. The live companion uses the same standing image in idle and collaboration states, avoiding rapid swaps between unrelated character art; the original pointing colleague remains in the isolated prototype. Consequently the bodies remain static; B2.1 provides workstation and ambient activity around them. It does not claim full character animation. The optional frame sampler supports Idle, Working, Reviewing, Testing and Collaboration clips once suitable frames are supplied.

Effects use smooth, low-contrast cycles instead of flashing or binary pass/fail signals. There is an explicit keyboard-accessible animation-off checkbox. The application had no existing reduced-motion preference to inherit; no automatic macOS accessibility preference integration is claimed. The toggle is a view-model preference for the current application session, not a persisted setting. Small windows reduce the already subtle effect detail.

## Verification results

- Solution Release build: **0 warnings, 0 errors**.
- Existing Core tests: **218 passing**; Application tests: **216 passing**.
- Final UI suite: **207 passing**. Total: **641 passing**. The full suite ran, then the UI suite was rerun after adding three focused small-team/rework cases.
- Animation mapping covers active/empty stages, observed versus absent capacity, rework, review-only small teams, collaboration, Day 0, zero/fractional capacity, combined developer limits and large-team aggregation.
- Clock tests cover all seven run-policy conditions, freeze/resume/reset, backwards timestamp rejection, and direct sampling after long gaps without catch-up tasks.
- Identical seeded runs (7, 12345, 9876) at 0.5×/2×/10×, with the same intervention on Day 40, compare complete captures every day through Day 100 and final result histories. Animation enabled/disabled, view hiding, mode switching and presentation-clock sampling make no difference. Shared checkpoints and pending intervention drafts are preserved.
- Native macOS verification covers actual checkbox/mode/LIVE bindings, visible pixel changes, exact frozen-pixel equality, pause, reduced motion, vector mode, hidden ancestors, minimization, scrolling out of view, navigation, detach/reattach, reset, cache disposal, large teams, 1×/2× rendering and 960/1280 layouts.
- Native performance smoke check: **30 offscreen renders in 515 ms**, 38 image objects in the large-team fixture, 10 initially decoded textures. Actual LIVE advance of 100 days took **58 ms**. These are single-machine observations, not throughput guarantees. GIF capture includes PNG-writing overhead and runs at a lower capture frame rate than the 30 Hz presentation timer.
- [Integrity report](integrity.json): all 41 production Core/Application/Infrastructure C# files and all twelve PNG assets remain unchanged. No simulation timing, scheduling, capacity allocation, lifecycle, random sequence or history code was modified.

Logs: [build](build.log), [full suite](tests.log), [final UI tests](ui-tests.log), [native verification](native.log), [prototype regression](prototype.log).

## Reproduce

```sh
dotnet build SoftwareDevelopmentSimulation.sln -c Release -m:1 -nr:false
dotnet test SoftwareDevelopmentSimulation.sln -c Release -m:1 -nr:false
dotnet run --project docs/verification/game-view-b2-1/AnimationPreview.csproj -c Release
python3 docs/verification/game-view-b2-1/encode_capture.py
```

The optional GIF encoder requires Pillow and reads only the verification frames in `/tmp/flowsim-b21-frames`; it is not part of the application. It uses the captured timestamps and one fixed GIF palette to keep unchanged pixels stable. No animation assets are generated or edited by this encoding step.

## Proposed B2.2

Review this visual behavior first. The next practical step is a small, consistent set of true seated idle/typing/review/test frames and a neutral/gesturing companion pair with matched feet, desk anchors, scale, lighting and occlusion masks. Validate those frames in the existing isolated prototype, then use the prepared SpriteClip sampler without adding timers or simulation logic. Keep positions fixed until that quality is approved. Any later movement or package transfers should be a separate reviewed phase driven by authoritative events and cancellable presentation interpolation. B2.1 stops at the current worker/workstation effects.
