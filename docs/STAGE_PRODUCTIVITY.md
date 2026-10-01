# Stage-specific Productivity Multipliers v1

Implemented in Simulation Model **0.4**, verified 2026-10-01. Three independent, finite, strictly positive factors default to **Development 1x / Code Review 1x / Testing 1x**. Values below one are supported. These are generic scenario assumptions; intervention labels have no processing semantics.

## Capacity and work

- **Nominal Capacity:** headcount × capacity per person per simulated day.
- **Availability:** the fraction of nominal capacity available to this workflow.
- **Available Capacity:** nominal capacity × availability.
- **Consumed Capacity:** capacity actually allocated from the available pool.
- **Productivity:** effective stage work produced by one consumed capacity unit, before any Development collaboration efficiency.
- **Effective Work:** stage effort removed from a Work Item.

Productivity is not utilization. Productivity is not capacity. Availability is not productivity. The model does not infer real-world effects of AI or any other tool from the factors.

The shared developer pool still serves **Code Review → Rework → Development**. Testing has its own pool. Neither pool is multiplied by productivity. Admission, WIP, FIFO, no same-day stage chaining, and no mid-day replacement are unchanged.

## Stage formulas and fractional allocation

Let `p` be the relevant stage factor and `c = min(1, per-person capacity, remaining pool)`.

| Contribution | Consumed capacity | Effective work |
| --- | --- | --- |
| Development primary | `min(remaining / pD, c)` | `consumed × pD` |
| Development collaborator | `min(remaining / (0.5 × pD), c)` | `consumed × 0.5 × pD` |
| Code Review | `min(remaining / pR, c)` | `consumed × pR` |
| Testing | `min(remaining / pT, c)` | `consumed × pT` |
| Rework | `min(remaining, c)` | `consumed` |

Effective work is bounded by remaining effort to avoid floating-point overshoot. Existing residue handling is retained. The one-unit contribution limit caps **consumed capacity**, allowing more than one unit of effective work at productivity above one.

Development Collaboration Model v1 is preserved: give all admitted items a primary FIFO opportunity, then visit still-active items in ascending remaining effort with stable FIFO ties. A second contribution requires at least two developers and retains efficiency 0.5. No collaboration is added to Code Review or Testing.

At Development 1.4x, one primary and one full collaborator consume 2 capacity and produce `1.4 + 0.7 = 2.1` work. Remaining primary effort 0.7 needs only 0.5 capacity. Remaining collaborator effort 0.35 needs only `0.35 / (0.5 × 1.4) = 0.5` capacity. Code Review effort 0.65 at 1.3x and Testing effort 0.7 at 1.4x each consume only 0.5 capacity. Leftover capacity can serve other already eligible items that day under the existing allocation rules.

Rework retains its existing effort, allocation and defect behavior. It has no configurable productivity parameter.

## Accounting and architecture

`StageProductivity` is an immutable Core value shared by scenario, session configuration and application request. A single contribution calculation handles primary Development, collaboration, Code Review, Testing and unchanged Rework efficiency. Validation occurs at existing scenario/session boundaries, without JSON/UI dependencies in Core.

Existing daily/item work fields continue to mean **effective work**. `StageCapacity` records actual consumed Development, Code Review and Testing capacity in non-default-productivity observations. `UsedReviewCapacity`, `UsedDevelopmentCapacity`, `UsedDeveloperCapacity` and `UsedTesterCapacity` project consumption. Work Item events retain both effective work and consumed capacity. Rework uses its existing consumption field.

Legacy/default 1x observations retain their compact representation: absent `ConsumedCapacity` means the original work/collaboration ledger identities. This preserves historical records and exact default arithmetic; no saved history is replayed or recalculated with today's settings.

All utilization is consumed / available capacity, including lifetime Review utilization. Period and trend utilization retain ratio-of-sums calculations. For example 5 available, 5 consumed and 7 effective work is 100% utilization. Existing zero-availability handling is preserved: undefined Live period/status/trend utilization; finite zero for legacy non-nullable lifetime/recent fields.

## Live, Analyze and time boundaries

The subsequent [Capacity UX Simplification](CAPACITY_UX_SIMPLIFICATION.md) removes per-person capacity inputs from normal Live setup and Change. Normal people-based capacity uses 1.0 nominal unit per person/day; saved custom scaling is retained and disclosed. These productivity formulas and model version are unchanged.

Expanded Configuration has a compact Productivity section with three independent inputs and `x` units. The same editor is available in Analyze's existing settings. Active Configuration displays all three values. The collapsed summary is deliberately unchanged to preserve the compact layout.

Live Change has three independent fields, using the existing draft/apply validation and optional label. A change recorded on Day N affects processing on Day N+1. It never rescales initial or remaining Work Item effort, historical consumption/work, queues, metrics or random state.

Changes and chart marker descriptions name the changed stage and old/new factor. There are no AI-specific branches. Scenario comparison's existing changed-parameter dictionary recognizes each productivity factor separately; configuration snapshots/CSV provenance inherit them.

Performance Trend reuses its existing metrics and markers. Development Capacity Used and Effective Development Work now expose productivity as well as collaboration. Utilization, queues, throughput, cycle time and WIP retain their definitions. No additional charts or headline metric set were introduced.

Before/After uses the existing calculation. Day 100 with window 20 is **Before 81–100 inclusive / After 101–120 inclusive**. At Day 100 After has zero observations; at Day 101 one; at Day 120 twenty. Other intervention handling and neutral wording remain unchanged.

## Persistence and model version

Model 0.4 is emitted in scenario/experiment documents, Live documents, results and traceability. JSON schema remains 1. All three factors persist in requests, initial/current session configurations, intervention Before/After records and checkpoints.

Model **0.2 and 0.3** documents lacking productivity load as 1x/1x/1x. Historical capacity observations use the legacy fallback described above. Live original-model provenance survives subsequent save/load. Model 0.1 remains rejected because of its earlier Development allocation semantics. No migration framework was introduced.

Before changing production code, actual model 0.3 Release assemblies generated the checked-in `model03-*` fixtures under `tests/Simulation.Application.Tests/Fixtures`: Day 30 Live saves and full Day 60 results for Fixed Rate and Always Available. They include fractional availability, triangular efforts, collaboration and defects. Tests load and continue those sessions and also run fresh explicit 1x scenarios, comparing every result, daily/item observation and event after normalizing only the result version. The existing genuine model 0.2 continuation regression also passes.

## Controlled native Live experiment

The actual Avalonia `MainWindow` was run with the platform-native backend, Fluent theme and real bindings. Interaction was automated through native control automation peers and TextBox bindings; the resulting window captures were visually inspected. This was **not a physical mouse/keyboard test**. The repeatable host is [Program.cs](verification/stage-productivity/Program.cs); execution evidence is [result.txt](verification/stage-productivity/result.txt).

Configuration: seed 12345, Always Available, 5 developers, 5 testers, 100% availability, WIP 5/3/5, fixed effort Development 4 / Code Review 2 / Testing 3, defects off.

1. Start at 1x/1x/1x and advance to Day 100; create a checkpoint.
2. Record Development 1x → 1.5x, label “AI-assisted development”; run to Day 120.
3. Record Code Review 1x → 1.3x; run to Day 140.
4. Record Testing 1x → 1.4x; run to Day 160.
5. Inspect Configuration, Flow Board, capacity versus work, utilization, both waiting queues, throughput/cycle time trends, all intervention markers and Before/After.
6. Save/load; restore Day 100 and repeat all interventions. The complete future session capture is identical.

Observed stage totals over the three 20-day intervals:

| Days | D / R / T factors | Development capacity / work | Review capacity / work | Testing capacity / work |
| --- | --- | --- | --- | --- |
| 101–120 | 1.5 / 1 / 1 | 59 / 88.125 | 41 / 41 | 63 / 63 |
| 121–140 | 1.5 / 1.3 / 1 | 62.846 / 93.255 | 37.154 / 48.3 | 68 / 68 |
| 141–160 | 1.5 / 1.3 / 1.4 | 63.615 / 95.423 | 36.385 / 47.3 | 52.286 / 73.2 |

Each interval had 100 available developer and 100 available tester capacity units. Review work remained equal to its consumption after the Development-only change; it then reflected its own factor. Testing remained 1x until its own change. Development totals include the existing collaboration efficiency. This checks processing mechanics, without requiring a particular throughput or queue outcome.

The native Before/After view for the first intervention displayed 81–100 / 101–120, throughput 4.0 → 5.3 items per five days, cycle time 11.0 → 10.2 days, average review queue 0.8 → 1.2, and developer utilization 100% → 100%. These are observations in this deterministic example, not causal claims or predictions about AI.

The complete collapsed Flow Board fits at 1280×800 and 960×720 with no horizontal overflow. Existing compact hierarchy is preserved.

Selected inspected captures:

- [Configuration](verification/stage-productivity/screenshots/configuration.png)
- [Development change](verification/stage-productivity/screenshots/change-120.png)
- [Code Review change](verification/stage-productivity/screenshots/change-140.png)
- [Testing change](verification/stage-productivity/screenshots/change-160.png)
- [Flow and capacity/work](verification/stage-productivity/screenshots/day160-flow.png)
- [Effective work trend](verification/stage-productivity/screenshots/trend-DevelopmentWork.png)
- [Before/After boundaries and observations](verification/stage-productivity/screenshots/before-after.png)
- [Narrow window](verification/stage-productivity/screenshots/size-960.png)

## Automated verification

**402 passing tests:** 131 Core, 166 Application, 105 UI. **30 new cases**: 21 allocation/validation cases, 7 application integration cases, 2 ViewModel cases. Three existing application test files were updated to target current model-version headers while retaining incompatible-model rejection.

Coverage includes independent stage factors, 1.4x primary/collaboration, no Review/Testing collaboration, Rework isolation, fractional completion/reuse, shared-pool priority, availability including zero, sub-unit productivity, invalid factors, utilization, exact 0.3 regression, historical/random-state integrity, active items in each stage, N/N+1 boundaries, partial/full Before/After, trend projections/markers, scenario differences, save/load/checkpoints and deterministic replay. Existing FIFO/WIP/supply/quality regressions all pass.

Release solution build: **0 warnings, 0 errors**. `git diff --check` passes.

```sh
dotnet test SoftwareDevelopmentSimulation.sln -c Release
dotnet build SoftwareDevelopmentSimulation.sln -c Release --no-restore
dotnet run --project docs/verification/stage-productivity/NativeVerification.csproj -c Release
```

## Semantic and scope confirmation

Developer priority remains Code Review → Rework → Development. Development Collaboration Model v1, Capacity Availability, Fixed Rate / Always Available supply, Rework and utilization semantics are preserved. Existing Work Item effort and past observations are never rewritten by an intervention.

No explicit AI model, AI quality assumptions, new collaboration model, recommendations, individual productivity or Rework multiplier were introduced. **Technical Debt has not been started.**
