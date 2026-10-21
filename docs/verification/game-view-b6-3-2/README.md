# B6.3.2 investigation before implementation

B6.3's ProductDeliveryLedger.Launch closes the launched version, creates the next active version and leaves its PlannedDay null. Subsequent internal releases correctly target that new active version. Boundary is still called on every simulated day, including unattended automatic execution; it correctly does nothing when no plan exists. UI correctly reflects an unplanned successor. This is one-time scheduling, not a failure in development flow or the automatic execution policy.

Implement explicit Manual, One-time and Recurring modes. Legacy After values remain one-time and legacy blank plans remain manual. Recurrence must be explicitly selected. The existing end-of-day convention remains: process internal releases, then pause/launch. Anchor each successor's recurring interval to actual launch day, so 60 postponed to 80 yields 140. Store rule per version so finished versions cannot be relabeled by subsequent scheduling changes. Postpone changes the current target only, not the recurring interval.

Keep the established sequential V1/V2 IDs and deterministic default Version N.0 names, skipping names already used; this avoids semantic parsing of user names. Explicit existing names remain unchanged. New renames must not duplicate a version name. Economic ownership remains attached to work item IDs and launch membership, never to queue presence or a new expenditure.

## Implementation

LaunchSchedule records Manual, OneTime or Recurring, interval, unit and sprint length. ProductVersion stores the rule in force for that version. Each plan revision also records its rule; cancellation is explicit rather than erasing old targets. Initial settings have a nullable Mode solely for compatibility: an older After value means OneTime, not Recurring. New UI choices always supply an explicit mode.

Each launch validates its successor target before modifying state, closes the current version and creates a unique active successor. Recurring rules are copied to the successor and resolve its target from actual launch day. OneTime and Manual produce an unplanned successor. There is no dependency on UI ticks or animation. Integer overflow is rejected before recording a partial launch.

The existing boundary runs after every fully processed day. Day 60 internal releases belong to the version launched on Day 60; Day 61 begins the successor interval. Interactive LIVE stops at every target even when Run to Day requests 200. Automatic Run resolves each decision and continues using exactly the same ledger. Matching LIVE decisions and Run produce identical full results.

Plan / Postpone changes only the current target, preserving a recurring interval. Apply launch strategy explicitly changes the active rule and schedules from the current day. Manual cancels a pending plan and preserves the cancellation in history. A manual early launch under recurrence anchors the next interval to that early actual day. Changing the active rule never edits completed versions.

## UI and event presentation

The dark compact row shows mode/recurrence, next day, remaining days, internally released items awaiting launch and completed launch count. Details offers mode and interval configuration, existing units/sprint length, launch/postpone controls, history and Launch Economics. The shared unit and sprint length are explicitly described next to the strategy controls. Existing expansion, scrolling and camera controls are retained.

B6.3.1's event queue observes every stable CL-N identity. Second and subsequent launches receive separate notices with actual name/day/release/item counts and optional workshop animation. Unattended results enqueue all launches after completion. Dismissal does not remove history; checkpoint/load behavior does not replay old notifications.

Chronological history includes original, final/current and actual dates, version rule, included counts and estimated allocated cost from the recorded economic scope. Default names retain the established Version 1.0, Version 2.0, Version 3.0 sequence rather than interpreting user names as semantic versions. Existing explicit names remain intact; new duplicate renames are rejected.

## Economics and persistence

The existing item-based LaunchEconomics projection is unchanged. Tests check each launch moves only awaiting costs, retains WIP and total incurred cost, preserves historical classification, and reconciles allocated plus unallocated costs. Day snapshots and old internal metrics are identical across Manual/OneTime/Recurring configurations. No capacity, random number, queue, internal batch release or cost charging code was changed.

Model version is 0.10; 0.2–0.9 remain loadable. Per-version Schedule and revision Schedule/Cancelled fields are persisted with the existing product ledger and checkpoints. Resolved targets remain explicit absolute simulation days. Older absent rules derive only the pre-existing one-time/manual semantics; they never become recurring. There is no invented historical launch or new financial ledger.

## Verification

The full solution suite covers the three modes, repeated boundaries, unchanged internal days, unique identities/names/membership, actual-day anchoring, early manual launch, repeated cancellation and mode changes, overflow atomicity, pending save/load/checkpoint replay, legacy 0.9 import, cost transfer/reconciliation and accelerated UI notifications.

The native/headless acceptance harness runs 200 days with six ten-day sprints per launch: targets 60, 120, 180 and then 240. It renders the second/third pending decisions and celebrations, all-version history, cost chart and a narrow compact view. It also checks persisted next target, unattended queue presentation, inspection/Fit, and postponing 60 to 80 followed by 140.

## Limitations

One active version, one channel, no semantic-version parser. Recurring scheduled decisions may launch an empty version, retaining the existing scheduled-empty-launch behavior; no work or cost is invented. Economics remains the documented blended-capacity estimate, with missing legacy observations disclosed. Windows/Linux and physical trackpad input were not runtime-tested here. No unrelated simulation mechanics or architecture changes.

Final results: Release build succeeded with zero warnings/errors. **883 tests passed** (Core 218, Application 292, UI 373), zero failures/skips. Native macOS and headless/Skia acceptance passed. The native harness waits for Avalonia's deferred layout before discovering the product controls. Compact narrow layout, chronological history and later-launch screenshots were visually inspected.
