# Dependencies v1 — Simulation Model 0.8

## Model and timing

Residual dependencies are independent of the pre-existing explicit dependency IDs. No graph editor, new graph types or new physical WorkItem state is introduced.

`DependencySettings` contains only Rate (0–1, UI 0–100%) and MeanWaitingDays (finite, nonnegative). Defaults are 0/0. At each arrival into Backlog, assignment uses an independent SplitMix64 stream seeded by `seed XOR 0xD3EED123`. Rate 0 consumes no draws. Rate 1 skips the probability draw. Intermediate rates assign when U < Rate. A second draw is used for duration only when assigned and mean > 0. Arriving fixed/future items and generated Live items use the same assignment rule. Assignments never repeat, including after restore or interventions.

The whole-day duration K is geometric on **0, 1, 2, …**, with mean m:

- For m=0, K=0 without a duration draw.
- For m>0, K=floor(log(1-U) / -log(1+1/m)). This is inverse sampling with P(K=k)=(1/(1+m))·(m/(1+m))^k.
- Minimum duration is **0**. There is no fractional-day rounding after sampling. Consequently a dependency can resolve immediately even with a positive mean. Rate is assignment probability; it is not a guarantee of observable blocking. At 100%, every arrival gets assignment metadata, including zero-day assignments.
- Durations saturate at int.MaxValue for extreme samples/means; resolution uses a long integer. This avoids overflow; the requested mean is no longer statistically exact near/beyond this implementation bound. No NaN/Infinity is stored.

Core arrival c is zero-based; display arrival is Day A=c+1. Store `ResidualDependency(WaitingDays=K, ResolutionDay=c+K)` on the WorkItem. It blocks initial admission while core day < ResolutionDay. For display arrival Day 10 and K=5, it prevents starts on Days 10–14 and is eligible for normal selection on Day 15. End-of-Day-14 observation considers the dependency resolved, although the next admission occurs on Day 15. K=0 allows normal admission on the arrival day.

Waiting starts at arrival, not an attempted start. A dependency resolving during other Backlog waiting adds no further timer when a slot later becomes available. Already-started work is never reconsidered by this mechanism.

## Selection, capacity, specialists and Always Available

The existing FIFO selector skips unresolved residual dependencies and continues to later eligible items, while retaining existing explicit dependency checks. Blocked items remain Backlog: no Development WIP, developer reservation, specialist reservation, capacity consumption, work event or development plan is created for them.

After resolution, the **existing admission/allocation distinction is preserved**: admission uses WIP limits and ordinary eligibility; available developer/specialist capacity determines work allocated after admission. Existing active specialist items can therefore wait within Development when specialist capacity is absent, just as before. Dependencies v1 does not change that pre-existing start rule or move all capacity-starved work into Backlog. This interpretation preserves the requested existing specialist/collaboration semantics and default behavior.

Priority remains Review → Rework → Debt Repayment / Development; allocation, collaboration efficiency, availability, productivity, debt, rework and release internals are unchanged.

Always Available still makes one bounded replenishment decision per day. It subtracts active Development and eligible Backlog from the Development WIP limit, then creates that many arrivals. Dependency-blocked Backlog is not eligible, so it can induce replacement arrivals on later days. Newly generated blocked items are not replaced in an unbounded same-day loop. Arrival counts may consequently differ between scenarios. This is a pull supply model, not a fixed common arrival schedule.

## Flow Board and measurements

Waiting for Dependency is an **end-of-day derived subset of Backlog**, computed from arrived Backlog items whose persisted resolution boundary is later than the completed day. It covers residual dependencies; pre-existing explicit-ID dependency behavior remains unchanged.

Core BacklogCount retains all Backlog. Live displays two disjoint sets:

- Backlog = total Backlog minus Waiting for Dependency.
- Waiting for Dependency = residual dependency subset, with exactly those items in its disclosure.

No item is duplicated. Neither subset enters total active system WIP. Development/Review/Testing/Rework and other waiting-state WIP definitions are unchanged. Expansion state uses the row name so the two Backlog subsets can expand independently.

The row appears when dependencies are configured or assignment history exists, and remains available after the rate is turned off. Default-disabled runs retain the original row set. Shared `QueueAttention` uses Development WIP limit as its scale, the existing OLS trend with Stable |slope|<0.05 and at least three observations, and the existing Neutral/Attention/Strong thresholds. Zero normalization uses the shared minimum of one. The same colors, number/arrow, accessible descriptions, tooltip and focus treatment apply. No separate warning UI or chart.

Rows use compact margins/heights when the extra queue is present. At 1280×850 the chart Y is **555 versus 559**, height **210 unchanged**; the 960px layout was visually inspected. Opening Work Items or the existing intervention summary can still expand layout normally. Light-theme behavior is preserved; no independent dependency palette is added.

## Live and persistence

Configuration has a collapsed two-field Dependencies expander; Change reuses existing Current/Try fields. History shows percentage and days, retains optional labels, and marks Day N → N+1. Existing items retain their assignment and resolution day; only arrivals in subsequent intervals use the new settings.

Performance Trend adds a daily Waiting for Dependency series. Team Performance adds queue facts when relevant. Before/After adds average Waiting for Dependency when the period used dependencies; existing inclusive windows and partial-period semantics remain unchanged.

Configuration, item assignments, resolution days, arrival days, independent random state, history and interventions persist through the existing JSON/checkpoint infrastructure. Validation rejects invalid settings atomically and inconsistent stored durations/resolution/start boundaries. Model 0.8 loads versions 0.2–0.7; missing dependency settings mean disabled, missing item assignments remain absent, and a missing dependency RNG initializes from the saved seed for future arrivals. No retroactive assignment to legacy items.

## Costs and compatibility

Dependency waiting consumes zero modeled capacity. Development Cycle Time still begins at Development Start and ends at work completion; Delivery Cycle Time still ends at Released. Pre-development waiting is deliberately excluded from both. Lead/blocked-time observations may reflect the new Backlog wait. Release scheduling and cost cohorts are unchanged.

Indirect changes in eligible work can alter collaboration, downstream occupancy, output cohorts, utilization and costs. They do not constitute direct capacity cost for dependency waiting. A paired run with 100% assignments and zero duration verifies that all pre-existing observations, configuration history (apart from new settings), debt, costs, release and all random states are identical with active Skills, productivity, defects and repayment. Disabled-feature archived compatibility fixtures also pass, removing only guarded inactive new metadata and the explicit version tag. No old numerical expected result was changed.

## Comparable scenarios

Seed 12345; Always Available; 5 developers, 2 testers; availability/productivity 100%/1×; WIP 5/3/3; effort 5/1/2; defects, debt and specialist work disabled; Flow-based Unlimited release. Run 500 days. Released is cumulative through Day 500; other metrics use Days **401–500**. Development WIP is mean occupancy sampled after admission.

| Metric | A: 0%, mean 0 | B: 20%, mean 3 | C: 50%, mean 5 |
| --- | ---: | ---: | ---: |
| Released, cumulative | 412 | 412 | 408 |
| Throughput / 5 days | 4.15 | 4.15 | 4.20 |
| Development Cycle Time | 9.6024 | 9.2651 | 8.9048 |
| Delivery Cycle Time | 9.6024 | 9.2651 | 8.9048 |
| Waiting for Dependency, average | 0 | 0.30 | 1.17 |
| Waiting for Dependency, maximum | 0 | 3 | 5 |
| Development WIP, average | 5.00 | 4.88 | 4.76 |
| Developer Utilization | 100% | 100% | 100% |
| Tester Utilization | 83.5% | 83.0% | 83.5% |
| Delivery Work Cost / Item | 8.0000 | 8.0000 | 8.0203 |
| System Cost / Released Item | 8.0361 | 8.0241 | 7.9405 |
| Distinct observed dependency-blocked items, all 500 days | 0 | 66 | 173 |

The final row counts items actually observed as residual-dependency-blocked at an admission boundary. **It is not a counterfactual count of items that would otherwise have received Development capacity that day.** Establishing that count reliably would require additional allocation replay/instrumentation; v1 does not claim it.

More assigned waiting did not monotonically reduce trailing-window throughput in these runs. C released fewer items cumulatively but slightly more in the final window. Cycle times exclude pre-start waiting and compare different completed cohorts. C used 2.3520 collaboration capacity units in the final window; its mean Development item cost was 5.0203 instead of 5, while Review/Testing remained 1/2. The small cost increase is consistent with the unchanged 50%-efficient collaboration consuming extra raw capacity when eligible work changes. B's approximately 7e-13 cost difference is floating-point noise. These seeded scenario observations are not a causal effect estimate or universal throughput ranking.

## Verification

**591 tests pass:** 218 Core, 210 Application, 163 UI; 22 new dependency cases (14 Core, 5 Application, 3 UI). Full Release solution build: **0 warnings, 0 errors**. `git diff --check` passes.

Coverage: validation/atomicity, zero rate and zero duration, geometric mean and skipped probability draw at 100%, arrival and resolution boundaries, absorbed waiting, blocked capacity/WIP, bypass and deterministic order, future-only interventions, checkpoints/JSON/legacy defaults, joint specialist/dependency assignments, collaboration/debt/release/cost equivalence, independent streams, queue/trend/Before-After projection, Backlog partition, expansion identity, safe normalization and finite metrics.

Native Avalonia verification succeeded with software rendering. Automated real TextBox bindings were exercised for Configuration and Change, including a labelled change on Day 500 to 100%/20 days. At Day 550 the shared queue highlighting showed 27 ↑, item disclosure contained the derived subset, and Before/After correctly showed Before 401–500 and partial After 501–600 with 50 days available. Images were visually inspected at 1280 and 960 widths. This is automated native interaction plus visual review, not an unobserved claim of a manual test.

[Results](verification/dependencies/result.txt) · [Configuration](verification/dependencies/screenshots/configuration.png) · [Change](verification/dependencies/screenshots/change.png) · [Flow/Trend at 960px](verification/dependencies/screenshots/dependency-trend-960.png) · [Intervention](verification/dependencies/screenshots/intervention.png) · [Items](verification/dependencies/screenshots/dependency-items.png) · [Before/After](verification/dependencies/screenshots/before-after.png).

Reproduce with `dotnet run --project docs/verification/dependencies/NativeVerification.csproj -c Release`; `-- --headless` is available if the native render timer is unavailable.

## Principal files

Core: `ResidualDependencies.cs`, `SimulationSession.cs`, `WorkItem.cs`, `Models.cs`, `SimulationEngine.cs` (eligibility predicate and snapshot metadata only), `ScenarioValidator.cs`, `SimulationModel.cs`, `WorkItemResult.cs`, `SimulationResultBuilder.cs`.

Application: `SimulationRequest.cs`, `LiveSimulation.cs`, `ScenarioParameters.cs`, `LivePerformance.cs`, `LivePerformanceTrend.cs`.

UI: `MainWindowViewModel.cs`, `LiveViewModel.cs`, `FlowPresentation.cs`, `QueueAttention.cs`, `LivePerformancePresentation.cs`, `LiveView.axaml`.

Tests: `DependencyTests.cs`, `DependencyIntegrationTests.cs`, `DependencyPresentationTests.cs`; existing model-version, trend-switch and Change-group assertions updated for the added feature. The guarded legacy comparison helper strips inactive dependency metadata only.
