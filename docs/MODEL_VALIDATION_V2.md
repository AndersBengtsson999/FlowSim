# Model Validation v2 — Model 0.8

**Outcome: VERIFIED WITH LIMITATIONS.** No confirmed model inconsistency was found in the tested scenarios. Mathematical accounting, deterministic replay, persistence and metric populations pass. Remaining limitations include single-seed coverage, intentional model simplifications, differing measurement populations, documentation drift and desktop-height constraints. This does not establish real-world forecasting accuracy.

Validation date: 2026-10-08. This task changed **no production source, allocation, metric definition or UI behavior**. All files under `src` (excluding generated bin/obj) were hashed before and after; hashes are identical. Existing uncommitted feature work was treated as the starting implementation, not rewritten. Additions are this report, reproducible harness/evidence and six focused regression cases.

## 1. Implementation inspection before execution

Authoritative inputs are `SimulationRequest`, `SessionConfiguration` and validated Core settings. `SimulationRequest` defaults to a fixed backlog of 30 items for 100 days, five developers/two testers, full availability, effort 5/1/2 and WIP 5/3/3. Live's Demo starts with zero initial items; work supply is separately selected. Therefore validation explicitly sets **zero initial items, Always Available, 300 days** rather than relying on UI presets.

Actual rules inspected in Core/Application/UI:

| Mechanism | Implemented semantics |
| --- | --- |
| Admission | Once at interval start, FIFO among eligible items. Unresolved dependencies are skipped. WIP admission occurs before resource allocation; admission does not reserve a named developer or require positive available capacity. |
| Developer priority | Code Review → Rework → Debt Repayment → Development. Testing uses a separate pool. |
| Contributions | Review, Rework and Testing consume at most min(1, per-person capacity) per item/day. Development permits one primary and one collaboration contribution; collaboration yields 50% effective work. Productivity scales work, not raw capacity. |
| Specialists | Specialists are a subset of developers. After higher-priority work/repayment, the residual developer pool is split proportionally. Specialist primary work and eligible specialist collaboration precede General work. General work may use remaining specialist capacity. |
| Lifecycle | Backlog → Development → Waiting for Code Review → Code Review → Waiting for Testing → Testing → Ready for Release → Released. Defects route to Waiting for Rework → Rework → Code Review. Legacy Done remains supported. |
| Release | Runs after Testing at boundary day+1. Flow-based releases daily; Scheduled releases on absolute days N, 2N, 3N. FIFO by Ready date, stable materialization order for ties; capacity is discrete items/opportunity, without banking or resource cost. |
| Dependencies | Arrival-time independent seed stream. Rate is assignment probability. Geometric whole-day duration includes zero; mean m has P(K=0)=1/(1+m). Resolution is arrival core boundary + K. Thus 25%/mean 4 yields about 20% positive-duration assignments in expectation, not 25% guaranteed visible waiting. |
| Supply | Always Available replenishes free Development slots after subtracting eligible Backlog, once per day. Blocked Backlog can induce later replacement arrivals. This is not a common fixed arrival schedule across scenarios. |
| Debt | Start-time overhead=max(0, opening debt/scope−tolerance)×impact. Shortcut reduction applies after overhead. Saved effort×creation factor becomes debt at Development completion; scope grows by base Development effort. Repayment consumes the configured fraction of the post-Review/Rework pool, limited by existing debt, and uses Development productivity without collaboration. |
| Item cost | Raw Development + Review + Rework + Testing consumption over an item's complete work lifecycle. Repayment excluded. Live averages the Ready/work-completed cohort in the selected period. |
| System cost | Raw Development + Review + Rework + Testing + Repayment consumed inside the period, including unfinished work, divided by Released in that period. Unspent capacity is not charged. Developer/tester units have equal weight; this is not monetary cost. |
| Cycle time | Development Start → Ready for Development Cycle Time; Development Start → Released for Delivery Cycle Time; Ready → Released for Release Wait. Pre-start dependency waiting is excluded. |
| Trend | Existing OLS with Stable absolute slope <0.05 items/day; fewer than three samples gives unavailable. Queue highlighting is a read-only projection using the existing normalized thresholds. |

Settings not active in A/B still have their normal defaults: shortcut reduction 30%, tolerance 10%, creation/impact factors 1, repayment 0, dependencies 0/0, specialist count/rate 0, defects off, Flow-based Unlimited release. Inactive values do not create debt or waiting.

## 2. Exact reproducible scenarios

All scenarios use seed **12345**, Always Available, zero initial items, five developers, two testers, nominal capacity 1/person/day, availability 100%, WIP Development/Review/Testing 5/3/3, fixed effort 5/1/2, defects off and **300 completed days**. The comparison window is **Days 251–300**, 50 observations, for every scenario.

| Parameter | A | B | C | D |
| --- | --- | --- | --- | --- |
| Development/Review/Testing productivity | 1/1/1 | 2/1/1 | 1.5/1/1 | Same as C |
| Specialists / specialist work rate | 0 / 0% | 0 / 0% | 1 / 30% | 1 → 2 on Day 150 |
| Dependency rate / mean days | 0% / 0 | 0% / 0 | 25% / 4 | Rate 25% → 10% on Day 150; mean stays 4 |
| Shortcut rate / reduction | 0% / 30% (inactive) | Same as A | 30% / 50% | Same as C |
| Debt creation / impact / tolerance | 1 / 1 / 10% (inactive) | Same as A | 2 / 1 / 10% | Same as C |
| Repayment | 0% | 0% | 20% | Same as C |
| Release | Flow, Unlimited | Flow, Unlimited | Scheduled, every 5 days, 5 items | Capacity 5 → 10 on Day 150; calendar unchanged |

D applies all three interventions together, labelled **Recovery assumptions**. They affect processing from Day 151. C and D have exactly identical recorded history through Day 150. Intervention application leaves all existing WorkItems—including assignments, classifications, remaining effort and plans—history and debt unchanged.

Full machine-readable configurations are in [results.json](verification/model-validation-v2/results.json). Executable definitions are in [Validation.cs](verification/model-validation-v2/Validation.cs).

## 3. Quantitative comparison

Released and completed-work counts below are **cumulative to Day 300**; all rates, averages and costs use **Days 251–300**. Debt ratio is at Day 300. Queue cells show **window average / end-of-run count**. Specialist waiting is a derived active-Development indicator, not an extra lifecycle state.

| Metric | A | B | C | D |
| --- | ---: | ---: | ---: | ---: |
| Materialized WorkItems | 253 | 424 | 257 | 288 |
| Completed Development, cumulative | 250 | 421 | 253 | 284 |
| Completed Testing/work, cumulative | 245 | 296 | 250 | 271 |
| Released, cumulative | 245 | 296 | 250 | 271 |
| Released in window | 41 | 50 | 49 | 50 |
| Throughput / 5 days | 4.10 | 5.00 | 4.90 | 5.00 |
| Completion Rate / 5 days | 4.10 | 5.00 | 4.90 | 5.00 |
| Development Cycle Time, days | 9.634 | 85.260 | 8.939 | 15.220 |
| Delivery Cycle Time, days | 9.634 | 85.260 | 10.898 | 17.220 |
| Release Wait Time, days | 0 | 0 | 1.959 | 2.000 |
| Average total WIP | 7.18 | 118.58 | 9.46 | 17.18 |
| Average admitted Development WIP | 5.00 | 5.00 | 4.84 | 4.92 |
| Developer Utilization | 100.00% | 99.575% | 98.371% | 100.00% |
| Tester Utilization | 83.00% | 100.00% | 97.00% | 100.00% |
| Delivery Work Cost / Item | 8.000 | 5.547 | 6.152 | 5.971 |
| System Cost / Released Item | 8.122 | 6.979 | 6.999 | 7.000 |
| Technical Debt Ratio, end | 0% | 0% | 0.727% | 2.908% |
| Waiting for Dependency, avg / end | 0 / 0 | 0 / 0 | 0.64 / 1 | 0.18 / 0 |
| Specialist Work Waiting, avg / end | 0 / 0 | 0 / 0 | 0.62 / 0 | 0 / 0 |
| Ready for Release, avg / end | 0 / 0 | 0 / 0 | 1.92 / 0 | 2.00 / 0 |
| Waiting for Code Review, avg / end | 0.86 / 2 | 1.46 / 2 | 1.00 / 1 | 1.06 / 1 |
| Waiting for Testing, avg / end | 0.86 / 1 | 111.54 / 122 | 1.10 / 2 | 8.26 / 10 |
| Waiting for Rework, avg / end | 0 / 0 | 0 / 0 | 0 / 0 | 0 / 0 |
| Testing queue OLS, items/day | -0.00274 | +0.40485 | +0.00245 | +0.07899 |

Additional debt/work diagnostics:

| Diagnostic | A | B | C | D |
| --- | ---: | ---: | ---: | ---: |
| Final debt amount | 0 | 0 | 9.20 | 41.30 |
| Maximum debt amount during run | 0 | 0 | 25.90 | 45.60 |
| Cumulative debt created | 0 | 0 | 285.0433 | 340.0433 |
| Cumulative debt repaid | 0 | 0 | 275.8433 | 298.7433 |
| Repayment capacity in window | 0 | 0 | 40.20 | 39.40 |
| Maximum recorded start overhead | 0% | 0% | 90% | 90% |
| Effective Development work in window | 207 | 352 | 223.505 | 230.538 |
| Raw Development capacity in window | 207 | 178.938 | 156.727 | 157.600 |
| Collaboration raw capacity in window | 0 | 5.875 | 15.447 | 7.816 |

### D: immediate Before/After versus later measurement

The existing comparison correctly uses **Before 101–150**, **After 151–200**, both complete. The main D column instead describes **251–300**, well after the change; cumulative D Released=271 includes all 300 days.

| Metric | Before 101–150 | After 151–200 |
| --- | ---: | ---: |
| Throughput / 5 days | 4.10 | 5.00 |
| Delivery Cycle Time | 12.049 | 11.480 |
| Developer Utilization | 86.132% | 97.245% |
| Tester Utilization | 82.0% | 100.0% |
| Delivery Work Cost / Item | 6.138 | 6.197 |
| System Cost / Released Item | 7.252 | 6.862 |

All three parameters changed concurrently. Neither this comparison nor the later C/D difference isolates their individual causal effects. See [intervention.json](verification/model-validation-v2/intervention.json) and [native Before/After](verification/model-validation-v2/screenshots/D-before-after.png).

## 4. Verified invariants and checks performed

**1,800 audited scenario-days and 226,061 item-day observations** across A–D plus three supplemental scenarios. Replays, checkpoint continuations and native views are additional checks, not extra independent samples.

- **Conservation:** unique IDs; every materialized item represented exactly once; exhaustive lifecycle partition; Flow Board disclosures also form a disjoint partition. Dependency waiting is a Backlog subset; specialist waiting overlaps active Development only as an indicator. Total WIP excludes Backlog and both derived duplicates.
- **WIP:** admitted Development/Review/Testing/Rework occupancy matches recorded states and respects configured limits in these scenarios. No intervention here lowers a WIP limit; existing non-eviction behavior for such changes is outside these particular assertions.
- **Priority and capacity:** independent reconstruction of Review demand/first allocation, Rework demand/second allocation, tester demand and post-priority repayment. Daily consumption remains nonnegative/finite and within available capacity. Debt repayment enters developer utilization. Waiting Backlog/dependencies and Ready-at-day-start consume zero item capacity.
- **Work/collaboration:** per-item and per-day effective Development work = productivity × (raw Development capacity − 0.5×collaboration capacity). Review/Testing use their own productivity. One active item stage per day; at most two Development contributions, bounded collaboration; specialist work fits its eligible residual pool and specialist collaboration requires at least two specialists.
- **Item costs:** reconcile daily ledger deltas and final per-stage costs against independent CapacityApplied events. Sum of item deltas equals developer+tester consumption minus repayment. Delivery Work Cost uses full lifecycle capacity for the work-completed cohort.
- **System costs:** independently sum all five raw capacity components in every rolling window and divide only by Released in that window. Capacity for unfinished items remains included. Release creates no cost units.
- **Debt:** opening amount − repayment + created amount = closing amount; cumulative scope equals base effort of Development completions; debt creation equals frozen plans at completion; admission-time plans use opening debt, overhead, shortcut reduction and creation factor. Fractional exact payoffs and zero-debt reservation behavior are exercised.
- **Timing:** dependencies use arrival plus sampled duration; starts never precede resolution. Release uses absolute scheduled opportunities, per-opportunity limits, correct FIFO/ties and includes same-boundary Testing completions. Zero-delay default release has zero wait.
- **Cycle definitions:** exported same-item Delivery Cycle Time = Development Cycle Time + Release Wait; exported Development Cycle Time equals Ready−DevelopmentStart. Lead−Cycle equals DevelopmentStart−Created, demonstrating exclusion of pre-start waiting. Rolling cycles independently checked against their correct cohorts.
- **History/projections:** every rolling endpoint and all selectable trend metrics are compared with authoritative history/period definitions. Queue current/average/OLS agree. Queue attention thresholds agree with the existing model. Complete session hash is unchanged after all metric/UI projections.
- **Determinism:** each of A–D runs twice with the same seed/interventions; complete captured-state SHA-256 matches exactly, stronger than numeric tolerance. This includes RNG states, classifications, assignments, costs, debt, transitions, release dates and daily history.
- **Checkpoint/JSON:** C Day 150 →300, restore →300 is exact; JSON save/load at Day 150 →300 is exact. Day-150 queues were dependency=0, specialist=1, release=0, so a separate mixed-queue checkpoint was added: Day 9 has dependency=23, specialist=1, release=2; restore/replay for 100 more days is exact.
- **Run/Live:** A/B/C continuous `SimulationRunner.Run` and incremental Live have identical complete result hashes with matching Always Available supply and zero initial items. D intentionally has interventions and is not compared with a non-intervened static Run.
- **No data:** with release capacity zero, System Cost/Released, Delivery Cycle Time and Release Wait are unavailable, while completed-work cost remains available. With both availabilities zero, consumption is zero and utilization ratios are unavailable rather than NaN/Infinity; throughput/completion rate are zero.

Numeric reconciliation tolerance is **1e-8 × max(1, |expected|)**. Repeated-state and Run/Live comparisons require exact serialized hashes. Normal model residue handling remains untouched.

### Supplemental coverage

Main A–D intentionally keep defects off and availability full. To exercise those interactions, additional 200-day runs use C with:

| Probe | Change | Observed window 151–200 | Result |
| --- | --- | --- | --- |
| Availability | Developer 60%, tester 70% | Throughput 1.8/5d; developer utilization 66.708% of available supply, tester 52% | All-day conservation/accounting pass |
| Rework | Review and Testing defect probabilities 20%; existing fixed rework effort 1 and WIP 3 | Throughput 4.0/5d; 20 defects in window; developer utilization 98.549%, tester 99% | Review/Rework priority, repeated-stage item costs and debt conservation pass |
| No release | Flow release capacity 0 | Zero release throughput, nonzero completed-work population | Null release-denominator metrics and available completed-work cost pass |

Unused capacity in the combined/availability runs is allowed: eligible work, specialist restrictions, per-item contribution caps and daily admission can leave nominally available capacity unconsumed. These probes do not isolate a single reason for every unused unit.

## 5. Interpretation: observations versus assumptions

**A:** bounded oscillating queues with near-zero OLS; no release wait. Cost 8 matches 5+1+2 per finished item. System Cost differs because it counts capacity within the window, including unfinished work, divided by that window's releases.

**B:** effective Development output rises without increasing Review or Testing capacity. Testing is fully utilized and its queue grows about 0.405 items/day to 122. Two tester capacity units with two units/item support about one completed item/day here, consistent with 5 releases/5 days. Delivery cost falls with productivity, while Development/Delivery cycle grows to 85.26 because those cycle definitions include downstream waiting. Developer utilization remains raw-capacity based.

**C:** all mechanisms coexist consistently. Specialist waiting averages 0.62 despite occasional unspent developer capacity. Debt both accumulates and is repaid, including high opening-phase overhead; the final debt ratio being below tolerance does not imply debt never affected starts. Scheduled release adds about two days of waiting with zero direct capacity cost. End-of-run Ready=0 occurs because Day 300 is a scheduled release opportunity; it does not mean release waiting was absent.

**D:** later-window specialist waiting is zero and dependency waiting is lower than C, but Testing becomes fully utilized and its queue rises. More completed Development work and different shortcut history coexist with higher total debt, even though the final ratio is still below tolerance. Release capacity rises but the five-day cadence still produces about two days of wait. Calling this uniform system-wide improvement would be unwarranted.

**Model assumptions:** pooled rather than named workers; fixed collaboration efficiency; proportional skill sub-pools; finite daily admission boundaries; geometric residual dependencies rather than external project graphs; debt formula calibrated by parameters rather than empirical data; equal-weight developer/tester cost units; no direct cost for passive waiting. The seed controls reproducibility, not realism.

## 6. Discrepancies, limitations and potential defects

**No confirmed production model defect was found. No corrective production change is proposed on this evidence.**

Documentation discrepancies found during inspection:

1. `SIMULATION_MODEL.md`, Dependencies section, still says explicit predecessors must be “Done.” Current `IsWorkComplete()` accepts Ready for Release as well as Released/legacy Done. The newer Release documentation explicitly describes that intentional behavior. This is stale broad-document wording, not evidence of a release defect.
2. The broad collaboration paragraph says every active item gets a primary opportunity before any collaboration. With specialists, the actual and specialist-specific documented order allows specialist collaboration before General primary allocation. The universal sentence needs qualification.

Other limitations, not confirmed defects:

- Dependency Rate is assignment probability; zero-day samples can resolve instantly. At rate 100% not every item must appear in the end-of-day queue. This follows the documented distribution.
- Development admission precedes capacity allocation; a specialist item can be active with no eligible capacity. Dependency-blocked items themselves remain Backlog. Changing this would change existing model semantics.
- Same-item cycle identity does **not** imply adding independently averaged UI cycle metrics always reconciles: Development Cycle uses the work-completed cohort, Delivery Cycle/Release Wait use the Released cohort. Likewise item and system costs have different numerators/populations.
- Pre-Development waiting is not a dedicated Live duration metric. WorkItemResult exposes LeadTime and CycleTime, whose difference gives pre-start waiting; BlockedTime records observed dependency blocking and need not equal all Backlog waiting. The dependency queue trend shows counts, not attributable delay.
- D changes three settings together. A single seed and finite windows cannot establish independent causal effects or a distribution of outcomes. Parameter extremes and every possible multi-intervention sequence are not exhaustively tested.
- At 960×850, the existing debt bar, loaded-session status and intervention summary can put the chart's bottom axis below the initial viewport in C/D. Scrolling reveals it; counts and highlights remain readable. This is a pre-existing layout-density limitation observed here, not changed in this task.
- Verification assumes fresh-model accounting for the detailed event ledger audit. Existing legacy-load tests pass, but missing historical cost fields are intentionally treated as unavailable rather than reconstructed as exact observations.

## 7. Regression, UI evidence and reproducibility

Full suite: **597 passed, 0 failed/skipped** = 218 Core + 216 Application + 163 UI. Six added test cases cover A–D all-day combined invariants, no-release population separation and zero-availability no-data behavior. No existing expected numeric result was edited for this task.

Release solution build: **0 warnings, 0 errors**. Native validation host and console harness complete successfully. Native A–D at 1280 and 960 pixels verify actual rendered queue counts/arrows and matching rolling values; Before/After boundaries verified in the real control. Images were visually inspected. Production-tree hashes remain unchanged; diff whitespace check passes.

One final-run attempt was temporarily rejected by the automatic approval service with a credit-related error. Retry succeeded after the user requested continuation. It did not leave any verification blocked.

Reproduce from repository root:

```sh
dotnet test -c Release
dotnet build -c Release
dotnet run --project docs/verification/model-validation-v2/Validation.csproj -c Release
dotnet run --project docs/verification/model-validation-v2/native/Native.csproj -c Release
```

Evidence:

- [Scenario configurations, quantitative results and audits](verification/model-validation-v2/results.json)
- [Intervention and immediate Before/After](verification/model-validation-v2/intervention.json)
- [Console validation log](verification/model-validation-v2/validation.log)
- [Test log](verification/model-validation-v2/tests.log), [build log](verification/model-validation-v2/build.log), [native log](verification/model-validation-v2/native.log)
- [Production hash verification](verification/model-validation-v2/production-integrity.json)
- [A](verification/model-validation-v2/screenshots/A-960.png), [B](verification/model-validation-v2/screenshots/B-960.png), [C](verification/model-validation-v2/screenshots/C-960.png), [D](verification/model-validation-v2/screenshots/D-960.png), [D Before/After](verification/model-validation-v2/screenshots/D-before-after.png)

## 8. Further validation recommended

Run a multi-seed experiment with uncertainty intervals; isolate the three recovery interventions; vary dependency means, specialist shares, availability and release cadence independently; audit dynamic WIP reductions under their documented non-eviction rule; compare long-run/stationary and transient windows; add cross-platform viewport/accessibility checks; reconcile the two stale broad-document statements. These are recommendations only—no automatic model change was made.

**Final classification: VERIFIED WITH LIMITATIONS**—tested internal invariants and execution paths are consistent, with the stated scope, documentation and model limitations. This is not certification of accurate real-world delivery prediction.
