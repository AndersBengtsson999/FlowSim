# B6.3 — Product Versions & Customer Launch

## Architecture and preserved meanings

The existing daily engine advances development, review, testing and release. `ReadyForRelease` is completed work awaiting internal release; `Released` (and legacy `Done`) remains internal delivery. Existing throughput, lead/cycle time, cost per delivered item, comparison cohorts and release capacity retain their definitions. No customer cost allocation is added.

A separate authoritative ProductDeliveryLedger observes internally released items after each completed day. It does not draw random numbers, allocate capacity, reschedule workers or add a work-item stage. Internal releases are nonempty per-day groups of item IDs. Each belongs to the version active when those items are internally released. Work still in progress at a launch is associated with the successor only when internally released; launched items never move.

ProductVersion stores stable ID, name, start boundary, original/current launch targets, actual launch day and planning revisions. InternalRelease and CustomerLaunch record immutable item IDs and days. Result work-item projections expose internal release day, version ID and customer launch day. Launching a version again by its ID returns its existing event.

## Day convention and execution

Decisions occur **after processing the entire named day**, including that day's internal releases. Start boundary 0 means the beginning of Day 1. Six sprints of ten working days resolve to Day 60. A successor starting at boundary 60 begins the next interval on Day 61. There is no calendar or new sprint cadence; sprint length is explicit planning arithmetic only.

Interactive advancement creates a pending decision at the boundary and refuses the next day. LIVE Step, Resume, timer ticks and accelerated Run to Day cannot bypass it. Launch includes only work internally released by the boundary. Postpone uses the current completed day: two ten-day sprints at Day 60 resolve to Day 80. Revisions preserve original and revised targets. Resolving either action does not advance a day. Save/load and checkpoints retain the decision.

Unattended Run uses the same mechanism with an explicit automatic policy: launch at the planned boundary. A manual-only scenario never invents a launch or blocks. Identical LIVE decisions and Run produce identical results. Isolated guided-experiment baseline execution also uses the explicit automatic policy; its existing internal-delivery KPI definitions are unchanged.

A launch creates one unplanned successor immediately, with sequential stable ID and a deterministic default display name that avoids existing default-name collisions. Users may rename and plan it while paused. Manual launch requires internally released work; a scheduled decision can launch an empty version, without inventing customer delivery.

## UI and metrics

Game View has a compact version panel with day/target/remaining days, internal release and waiting-item counts. Pending decisions show Launch Now and Plan / Postpone Launch, with wrapped controls on smaller windows. The expandable section provides naming, separate customer metrics and version/plan history. Flow Board also exposes the decision. Initial settings are available in Release / Deployment; Results distinguish customer data from existing internal metrics.

Customer metrics are cumulative distinct launched items, internal release events awaiting launch, internally released items awaiting launch, mean internal-release-to-launch days and mean development-start-to-launch days. Averages include launched items only, with available authoritative timestamps; absent samples display N/A. Version history shows original plan, revised plan and actual launch. No customer cost KPI is introduced.

## Persistence

Model version is 0.9. Existing supported versions 0.2–0.8 remain loadable. Legacy LIVE saves without a ledger import one explicitly marked version and reconstruct internal releases from existing recorded delivery dates. No historical customer launch or plan is fabricated. Old results without launch data report it unavailable. Restored ledgers validate IDs, dates, membership, plans, launches and pending decisions. Synthetic legacy test fixtures intentionally omit the ledger when replacing their underlying work history.

The old release golden comparison strips only additive product/customer metadata and refuses to normalize actual customer launches. Existing development history, random streams, capacity and costs remain compared.

## Verification

Release build: zero warnings/errors. Full suite: **860 passed** (Core 218, Application 276, UI 366), zero failures/skips. Both the native macOS and headless/Skia acceptance harness passed. See included build, full-test and UI harness logs. Focused tests cover identity, associations, same-day inclusion, unfinished work, successors, working-day/sprint planning, manual and automatic launch, repeated postponement, exact high-speed boundaries, idempotency, deterministic Run/LIVE equivalence, unchanged old metrics/random streams, save/load/checkpoints, legacy import and invalid history rejection.

`Verification.csproj` opens the real MainWindow/GameView and exercises the six-sprint Day 60 scenario, launch, checkpoint restoration, postponement to Day 80, save/load, continuation at Day 81, unattended automatic launch, narrow layout, station selection, Fit and expanded KPI Explorer. Screenshots are actual Avalonia renders; headless renders use Skia. Native harness input is automated, not a physical mouse/trackpad acceptance session.

## Scope and remaining limitations

One active version, one launch per version, no parallel channels/branches, adoption, revenue or customer cost allocation. Successors are deliberately unplanned until configured. Legacy import cannot recover product decisions that were never recorded. Existing KPI Explorer/experiment comparisons remain internal-delivery comparisons; customer metrics are separate summary/history. Windows/Linux runtime acceptance is not established by macOS verification. No unrelated simulation mechanics were added.
