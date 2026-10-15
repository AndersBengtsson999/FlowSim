# B3.1 — Visual Bottlenecks

The existing Game View now shows growing, bounded package stacks, static warning shapes and short queue status labels. It reuses the original package sprites, floor, workers, clock and authoritative snapshots. No simulation, capacity allocation or work-item behavior changed.

## Stack mapping and rendering

Waiting queues show `n` boxes for 0–4 items, then `min(12, 4 + ceil(2 × log2(n / 4)))`. Thus two items show two boxes, 35 show eleven and 10,000 show twelve. Exact numerical counters remain visible. `grouped` explicitly marks aggregate samples; a box is not an individual work item when aggregation applies. Active-stage and released-today samples retain their previous small bounds.

Each waiting zone has stable sprite IDs and positions: three columns, two rows, then a second height layer. Existing boxes never repack as counts change. Package sprites remain in the existing storage zones, outside the worker approaches. The full static scene has a conservative limit of 128 objects regardless of queue size; six waiting zones each have at most twelve boxes.

`QueueStackMotion` dissolves only added/removed aggregate slots over 350 ms on the existing presentation clock. It does not create item IDs, routes or fictional transitions. Exact counts update immediately. Current-day B2.2 item journeys remain independent. Pause freezes the dissolve; reduced motion, vector mode, hidden/detached views, high speed, discontinuities and checkpoint restoration cancel obsolete transitions. At high speed the latest stack is shown immediately.

## Explainable signals

`QueuePressure` derives presentation signals from existing daily snapshots and current configuration:

- **Normal:** no causal warning merely because a queue is large.
- **↑ Growing:** the current queue exceeds the preceding daily snapshot.
- **Ⅱ Capacity constrained:** four consecutive observations are nondecreasing with net growth, and capacity was fully used on all three latest days. Review/rework additionally require actual work in that stage. These stages share developers, so the label says **Shared Dev capacity busy**, not that an invented stage-specific allocation is exhausted.
- **! Blocked:** waiting work and zero configured downstream capacity. Zero testers is explicitly labeled **No testers**. The empty Testing workstation is marked **INACTIVE**; its WIP count still reflects actual admitted items, which are not evidence of active processing.
- **B Scheduled batch:** a nonempty Ready for Release queue under scheduled release. Queue size or growth alone never turns scheduled batching into a processing bottleneck. Zero release capacity still takes precedence.
- **D Dependency:** the existing dependency-wait subset, not a new queue or an assertion of missing staff.

History before or on the most recent intervention day is excluded from sustained-constraint evidence. A change on Day N takes effect on N+1. Capacity restoration therefore clears a zero-capacity warning immediately; later stacks shrink only when actual simulation queues shrink. Falling queues clear the sustained-growth condition. Signals come from simulation history rather than animation frames and remain available after loading, while paused and with animation disabled.

Available team capacity and per-stage consumed capacity are reused without new capacity formulas. WIP remains the engine's admission limit; this version does not infer a unique root cause from a WIP limit or queue size. If evidence is insufficient, the queue is shown without a causal assertion. No duplicate queue membership calculation is introduced.

## Native macOS scenarios

All captures use the actual Avalonia renderer and real simulations, not mockups or edited counts.

| Scenario | Observed result | Capture |
|---|---|---|
| Balanced, steady inflow | Review 1, Testing 1; no warnings | [Balanced](screenshots/balanced.png) |
| 5 developers, 0 testers | Exactly 35 waiting for Testing; blocked marker, empty inactive station | [Testing blocked](screenshots/testing-blocked.png) |
| Slow Code Review | Review queue 80; observed shared developer capacity constraint; no Testing warning | [Review constraint](screenshots/review-constrained.png) |
| Scheduled release | 38 ready; batch label, no capacity warning | [Scheduled batch](screenshots/scheduled-batch.png) |
| Large actual queue | 1,997 waiting; twelve waiting-box sprites, 36 static objects total | [Large queue](screenshots/huge-queue.png) |
| Recovery after adding five testers | Actual queue falls 35 → 26 → 0; warning clears | [Progress](screenshots/recovery-progress.png), [Recovered](screenshots/recovered.png) |

The native check also covers paused pixel equality, actual shared-clock fades, high speed, reduced motion, vector mode, checkpoint restoration, compact-window scaling, minimize, hide/detach, cached textures and complete-capture invariance. The large scene required 446 ms for 30 native renders (about 14.9 ms/render) on this machine, with eleven cached decoded textures. This is a local offscreen render benchmark, not a cross-device FPS guarantee. See [native log](native.txt) and the per-scenario JSON facts.

## Build and tests

Release build: **0 warnings, 0 errors**. Full suite: **708 passed** (218 Core, 216 Application, 274 UI), including 17 new B3.1 cases. See [build log](build.txt) and [test log](tests.txt). Focused tests cover nonlinear mapping, bounds, stable placement, aggregation, zero capacity, sustained pressure, scheduled batching, real recovery, floor/viewport bounds, fading/shrinkage, disabled/high-speed rendering and restoration. Three seeds × 100 days with matching interventions compare complete capture (including history/checkpoints) and final results with visualization exercised versus disabled.

Reproduce from repository root:

```sh
dotnet build --no-restore -c Release -m:1 -nr:false
dotnet test --no-build --no-restore -c Release -m:1 -nr:false
dotnet run --project docs/verification/game-view-b3-1/AnimationPreview.csproj -c Release
```

## Remaining limits

Stack growth is intentionally nonlinear and saturates at twelve boxes; exact counters distinguish very large queues. Dissolves represent aggregate changes rather than individual package stacking. The conservative constraint rule can miss a bottleneck when raw shared capacity is not fully used; it deliberately avoids diagnosing an unsupported cause. Scheduled release backlog is identified as batching without attempting to diagnose persistent release undercapacity. No zoom, popups, new gameplay, new simulation rules or major layout redesign were added.
