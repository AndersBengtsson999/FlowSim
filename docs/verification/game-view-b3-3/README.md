# Game View B3.3 — Zoom & Pan

## Controls

- Mouse wheel or fractional two-finger trackpad scrolling over the workshop zooms around the pointer, from 75% to 250% of fit. Wheel input elsewhere is untouched.
- The accessible − / + buttons zoom around the viewport center. Fit to View restores centered 100% without clearing the selected station.
- Left-drag empty scene space to pan; middle-drag anywhere. A 5 logical-pixel threshold distinguishes dragging from clicking. Dragging from a station with the left button suppresses a click but does not move the station or camera.
- The detail panel, status summary, day indicator and controls remain outside the camera, at normal UI text size. Existing station keyboard controls remain available; modified key combinations are not intercepted.
- Camera updates follow input directly, without inertia, tweening or new animation timers. Animate activity continues to control reduced-motion presentation.

## Architecture

`WorkshopViewport` is a presentation-only camera with uniform scale and screen-space translation. Zoom is relative to the responsive fitted scale. `ToScreen`, `ToWorld` and the rendering matrix share the same values. Pointer-anchored zoom solves the new offset from the old world position under the pointer; edge limits can override that anchor at the permitted pan boundary.

`GameWorldControl` draws its complete existing renderer inside one `DrawingContext.PushTransform`. Floor, furniture, people, packages, labels, queue effects, selection outlines and release paths therefore retain their world coordinates and depth order. The control and its parent clip the zoomed scene to the viewport. Sprite caches and the existing shared presentation clock are unchanged.

`GameWorldControl.Camera` handles wheel, pointer capture, release and capture loss. Station hit testing explicitly applies `Camera.ToWorld` first. Click selection is deferred until release; drag completion never selects or clears a station. Hover is recomputed after camera or size changes. `WorkshopDrag` measures its threshold in screen DIPs, independent of zoom.

The previous Viewbox is replaced by a clipped viewport containing the same world control. B3.2's responsive height allowance and panel breakpoint remain; default camera fit reproduces the same centered framing. A manually adjusted camera preserves relative zoom and the world point at the viewport center across resizing or panel layout changes, subject to pan limits. Ordinary scene snapshots and checkpoint restoration do not reset it.

Panning continuously clamps each axis to retain at least one quarter of the smaller of the viewport and scaled world extent. Fit is always available outside the moving world. Labels use the same uniform world scale as their stations; there is no separate label layout or redesign. Offscreen labels are deliberately clipped when exploring a zoomed view; current warnings and selected metrics remain available in the fixed summary/panel.

No simulation, configuration, persistence, capacity or lifecycle code changed. No new assets, renderer dependency, network requirement or animation system.

## Verification

```sh
dotnet build --no-restore -c Release -m:1 -nr:false
dotnet test --no-build --no-restore -c Release -m:1 -nr:false
dotnet run --project docs/verification/game-view-b3-3/AnimationPreview.csproj -c Release
# Display-independent follow-up using the same controls and actual Skia rendering:
dotnet run --project docs/verification/game-view-b3-3/AnimationPreview.csproj -c Release -- --headless
```

Build: 0 warnings, 0 errors. Full suite: 749 passed, none failed/skipped (218 Core, 216 Application, 315 UI). The new tests cover fit, forward/inverse/matrix agreement, fractional pointer anchoring, limits, pan bounds, resize behavior, drag versus click and all station/queue targets at 75%, 100%, 200% and 250%. Three seeded runs interleave camera operations with simulation steps and compare complete captured histories/checkpoints and final results against an uninspected run.

The first native macOS run passed routed wheel/pointer/keyboard tests, 200% Testing and Review selection, panel stability, manual resize, fit, restore, cache/clock independence and retained B3.2 scenarios. Native screenshots are in `screenshots/`. Requested window sizes were 960×820, 1440×1060 and 1840×1120; macOS limited tall native windows to the available 973-pixel display height. These are real live-control captures, not mockups.

Subsequent native starts failed before opening the window with Avalonia.Native RenderTimer error −6661. The failure is retained in `native-retry.log`. The extended harness consequently runs with Avalonia Headless plus real Skia drawing, separately recorded in `headless.log` and `headless/screenshots/`. It additionally checks the visible zoom buttons, wheel outside the viewport, actual empty-space pan offsets, and camera changes between observed moving worker/package/release frames. It compares full simulation captures before/after camera input and checks that motion counts, presentation time and asset decode counts are not reset by that input.

The harness validates the real window input hit target before dispatching click events. It uses routed input rather than physical OS mouse/trackpad automation. Native layout readiness is explicitly updated before inspecting the control tree.

## Screenshots

- [Native: 200% Testing](screenshots/zoom-testing.png)
- [Native: panned Code Review](screenshots/zoom-review.png)
- [Native: custom camera after resize](screenshots/zoom-resized.png)
- [Native: initial fit](screenshots/initial-fit.png)
- [Skia: active camera with real simulation transitions](headless/screenshots/active-camera.png)

## Limitations

Two-finger trackpad scroll uses Avalonia's wheel-event route, including fractional deltas. Physical trackpad input was not exercised; native pinch-to-magnify is not added. There is no inertial panning. At high zoom, offscreen world labels/effects are clipped intentionally and can be reached by panning or Fit to View. Pan clamping bounds the world rectangle rather than the isometric floor polygon. Screen-reader behavior was not audited. The extended native rerun remains limited by the recorded macOS render-timer failure; its extra checks were completed with Skia/headless instead.

Stopped at B3.3. No station editing, individual worker scheduling or additional gameplay.
