# Skills & Specialists v1 — model 0.6

Exactly one specialist capability constrains Development. Specialists are a subset of Developers: 5 Developers and 2 Specialists still provide nominal capacity 5. Both settings default to zero. Specialists must be an integer between zero and Developers; Specialist Work Rate must be finite and between 0% and 100%. Invalid input is rejected, including reducing Developers below Specialists. A simultaneous valid change is accepted atomically.

## Classification and determinism

`SkillSettings(Specialists, SpecialistWorkRate)` belongs to the scenario, request and session configuration. `WorkItem.RequiresSpecialist` is an explicit boolean, copied into captured state, item results and daily observations. No effort/debt/queue/productivity inference is involved.

Initial workload items are classified when the session is constructed, including any initial items with a future CreatedDay. Generated arrivals are classified immediately after their effort is sampled, before insertion into the session. These are the earliest points at which those items join the workload. A changed rate only affects subsequent generated items, not the existing backlog or active items. Entering Development, waiting, inspection, rework, interventions and restore never classify again. A new independent simulation run classifies its own fresh workload.

A dedicated SplitMix64 stream uses `seed XOR 0x5A11C0DE`, persisted as `SkillRandomState`. Rates 0 and 1 consume no draws. Rates strictly between zero and one consume one draw per classification. Existing arrival/shortcut, defect-discovery and rework RNG streams are untouched by classification. With active Skills, flow changes can naturally affect later event timing and thus other stream consumption; this is not an extra classification draw on those streams.

## Capacity and allocation

Total available developer capacity remains `Developers × nominal capacity per developer × availability`. Conceptual specialist-capable availability is the total multiplied by `Specialists / Developers`, or zero with no developers. Thus nominal per-person scaling and fractional availability apply equally to both subsets. General-only availability is the remainder; no extra capacity is introduced.

Review, Rework and Debt Repayment retain their existing global priority, per-item limits, productivity and allocation. These unconstrained activities consume the common pool first. Without modelling people, the **remaining** Development pool retains proportional skill composition: specialist residual = remaining pool × Specialists / Developers. This deliberately makes no claim about which individual performed earlier work. General-only residual is the balance.

Development admissions still happen before work, FIFO with dependency eligibility and **one shared WIP limit**. Admission does not itself consume capacity; an admitted specialist item can receive zero work and remain active. There are no reserved slots, evictions, extra queues or skill-based WIP bypasses. If active General items occupy all WIP slots, specialist backlog waits for a normal slot. Reducing WIP preserves existing active items. Zero specialists plus all specialist work stalls stably until an intervention provides eligible capacity; this is the intended constraint.

When no active Development item requires a specialist, the exact previous allocator runs unchanged, using all residual capacity. Otherwise:

1. Specialist primary contributions are allocated in FIFO order from specialist-capable residual capacity.
2. Specialist collaboration uses the existing smallest-remaining-effort order after primary work, with stable FIFO ties, and requires at least two configured Specialists. This gives eligible specialist work preference before fallback.
3. General primary contributions are allocated in FIFO order, drawing general-only residual first, then specialist residual that specialist work cannot use.
4. General collaboration uses the same finish-first ordering after primary work, drawing those same eligible pools, and requires at least two Developers.

Specialist and General primary limits remain `min(1, nominal developer capacity, remaining effort / Development Productivity, eligible residual)`. Collaboration uses efficiency 0.5 with the same per-contribution capacity limit. General contributions may draw fractions from both conceptual sub-pools but are applied as one contribution. Each item receives at most one primary plus one collaborator per day. General-only capacity can never supply either contribution on a specialist item. One Specialist cannot receive General collaboration on its specialist work.

## Existing mechanics and costs

Development Productivity applies equally to both classifications: eligibility chooses capacity; productivity converts eligible consumed capacity into effective work. Collaboration still converts its raw consumption at 50% efficiency. No skill bonus or penalty exists.

Debt plans remain locked at Development admission; overhead and shortcuts work exactly as before for either classification. Debt creation remains tied to original Development completion. Repayment does not require a specialist, retains its existing allocator and reduces the shared pool before its remaining skill composition is determined. Review, Rework and Testing do not test RequiresSpecialist. The flag remains stored through these stages.

Delivery Cost includes actual raw item consumption with equal capacity weights, including collaboration, and still excludes repayment. System Cost still sums period developer plus tester consumption, already including repayment. Specialist capacity is a subset, not an additional cost category. No weighting, money, optimization or recommendations were introduced.

## Specialist Work Waiting

An exact daily observation counts items which, at day end:

- require Specialist Development;
- remain in active Development with positive remaining effort;
- received **zero raw Development capacity** that day.

This is a deliberately narrow eligibility-wait count. Partially served items do not count. Neither do Done, Review, Rework, Testing, or backlog items waiting for shared WIP admission/dependencies. It is not a count of every unfinished specialist item. Specialist preference means an unserved active specialist item could not receive a primary contribution from the eligible pool after higher-priority system work and preceding specialist allocations. Some general-only capacity can remain unused on the same day.

`DailySnapshot.SpecialistWorkWaiting` is persisted directly and displayed as a daily, non-rolling Performance Trend metric. Changing the visible range does not change historical observations.

## Live UI

Normal Configuration places Specialists and Specialist Work (%) immediately under Developers. Help text says Specialists are a subset. The existing Team & Capacity group in the Change panel adds the two rows in Parameter / Current / Try format; Specialists validates as an integer and rate shows %. Existing labels, Apply, cancellation and next-day timing are unchanged.

The Development Flow Board row adds a compact `Specialist waiting: N` when Skills is relevant. Existing capacity/work captions shorten to Used/Work to retain one row. The tooltip defines the daily count. Expanded items show `Specialist Development`; no permanent Skills card or extra chart was added. Configuration details report the specialist subset and rate. The existing Performance Trend dropdown includes Specialist Work Waiting.

Native Avalonia verification exercised the bound Specialists TextBox and Apply Changes button through automation peers, retained the intervention label, and rendered the real MainWindow. Visual review covered Configuration, Change, expanded items, and the chart at 960/1280px. With debt disabled, chart Y=572 and height=210 with Skills disabled and enabled at both widths. The chart was not moved or shrunk. [Evidence and reproducible host](verification/skills/result.txt); [960px](verification/skills/screenshots/layout-960.png), [1280px](verification/skills/screenshots/layout-1280.png), [Configuration](verification/skills/screenshots/configuration.png), [Change](verification/skills/screenshots/change.png), [items](verification/skills/screenshots/items.png).

## Persistence and compatibility

Model version is 0.6; JSON schema remains 1. Supported model 0.2–0.5 documents continue to load. Missing Skills settings default to 0/0%; missing item flags default to General; missing waiting history defaults to zero in those pre-Skills sessions. Restore never reclassifies existing items. If an old session has no skill RNG state, it initializes the new independent stream from its saved seed for future classifications. Checkpoints retain configuration, flags, waiting history, interventions and the new stream. Save/load uses the existing delta-encoded daily observation infrastructure without changes to its serialization mechanics.

Default-Skills regression compares all three complete archived pre-feature model-validation state fingerprints after removing **only** newly introduced Skills metadata. All pre-existing random streams, allocation observations, items, costs, debt and history match exactly. The older pre-Delivery-Cost executable fixture comparison similarly removes only added observation metadata and normalizes the model-version tag. Existing model 0.2/0.3/0.4 fixtures continue to pass.

## Deterministic demonstration

Seed 12345, Always Available, Developers 5, Testers 2, availability 100%, productivity 1/1/1, Specialist Work Rate 40%, WIP 5/3/3, fixed effort 5/1/2, defects/debt off. Run to Day 200 with one Specialist, change Specialists to two on Day 200 (effective Day 201), continue to Day 400. Compare trailing 50-day windows. The post-change window is 351–400, after a 150-day settling interval; this is a descriptive demonstration, not the automatic immediate Before/After period or a causal claim.

| Metric | 1 Specialist, Day 200 | 2 Specialists, Day 400 |
|---|---:|---:|
| Measurement period | 151–200 | 351–400 |
| Done, cumulative | 102 | 250 |
| Done in period | 28 | 40 |
| Throughput / 5 days | 2.80 | 4.00 |
| Cycle Time, days | 12.89 | 9.50 |
| Average WIP | 6.18 | 6.78 |
| Current WIP | 6 | 7 |
| Developer Utilization | 74.76% | 98.86% |
| Tester Utilization | 56.00% | 80.00% |
| Specialist Work Waiting, current | 0 | 0 |
| Specialist Work Waiting, period average | 1.92 | 0.22 |
| Specialist Work Waiting, period maximum | 4 | 2 |
| Delivery Cost / Done | 8.677679 | 8.249688 |
| System Cost / Done | 8.675000 | 8.178633 |

Current waiting is zero at both sample endpoints, while the daily series shows waiting within both windows. Utilization above is aggregated over each period; the compact Live status displays the latest day's utilization. Differences in costs arise from actual flow, collaboration and period/completion-cohort boundaries, not a specialist premium.

## Files and verification

Core: `Skills.cs`, `SimulationSession.cs`, `WorkItem.cs`, `Models.cs`, `WorkItemResult.cs`, `SimulationResultBuilder.cs`, `ScenarioValidator.cs`, `SimulationEngine.cs`, `SimulationModel.cs`.

Application: `SimulationRequest.cs`, `LiveSimulation.cs`, `ScenarioParameters.cs`, `LivePerformanceTrend.cs`. Infrastructure transport records already serialize these domain properties, so no custom serializer is needed.

UI: `MainWindowViewModel.cs`, `LiveViewModel.cs`, `FlowPresentation.cs`, `LiveView.axaml`. Tests and the verification host accompany these changes.

24 new test cases (18 Core, 4 Application, 2 UI) cover validation, no specialists, exact eligible fractional capacity, collaboration eligibility and two contributions, fallback, one WIP, no eviction, unconstrained Review/Rework/Testing/repayment, debt overhead, equal raw cost accounting, atomic interventions, rate timing, independent random streams, repeated runs, save/load/checkpoints, legacy defaults, historical waiting, UI binding and archived baseline compatibility. Existing trend tests now cover the new daily series; configuration presentation/version assertions were updated.

Full suite: **537 passed (194 Core, 198 Application, 145 UI), zero failed or skipped**. Release build: **0 warnings, 0 errors**. Native verification passed. With Specialists=0 and Specialist Work Rate=0%, existing validated simulation behavior remains unchanged.

```sh
dotnet test --nologo -m:1
dotnet build -c Release --nologo -m:1
dotnet run --project docs/verification/skills/NativeVerification.csproj
```
