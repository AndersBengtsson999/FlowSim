# Capacity UX Simplification

Verified 2026-10-01. This is a presentation and terminology change; Simulation Model **0.4** and JSON schema **1** are unchanged.

## Normal mental model

```text
People × Availability → Available Capacity
Available Capacity → Consumed Capacity
Consumed Capacity × Stage Productivity → Effective Work
```

Normal nominal capacity is **1.0 unit per person per day**. Capacity describes the amount of resource available; productivity describes the effective stage work produced by consumed capacity. Development collaboration additionally applies its existing 0.5 efficiency to the second contribution.

Five developers at 80% availability supply **4.0** shared developer capacity units/day. Development productivity 1.5x can turn four primary capacity units into six effective work units; it does not turn the available pool into six. Two testers at 75% availability supply **1.5** tester capacity units/day.

The developer pool is shared by Code Review → Rework → Development. Testing keeps its separate pool.

## Live configuration and interventions

Normal setup contains:

- Developers and Testers under TEAM, with concise pool explanations.
- Developer Availability (%) and Tester Availability (%) under a small AVAILABILITY heading.
- Three independent Development, Code Review and Testing productivity inputs, with `x` units and stage-specific help, in the existing compact Productivity expander.
- Existing WIP, supply, effort and quality settings.

Developer Capacity and Tester Capacity inputs were removed from Live More settings. The two normal Live Change fields were also removed. Change retains people counts, availability, all three productivity fields, WIP, supply and existing quality options. Tooltips distinguish these concepts.

The collapsed Configuration summary still prioritizes people, WIP, supply and availability; no additional collapsed height was added. Expanded Configuration no longer repeats ordinary per-person capacity values. Normal Analyze changes likewise prioritize availability; custom nominal scaling remains under its Advanced settings.

## Internal properties, Advanced and compatibility

`DeveloperCapacityPerDay` and `TesterCapacityPerDay` are **preserved**, with existing default 1.0. No Core, Application or Infrastructure behavior was changed for this UX task.

Existing Advanced scenario editors remain editable with precise labels:

- Developer Capacity per Person / Day
- Tester Capacity per Person / Day

Their help states: “Advanced scaling of nominal capacity per person. Normal simulations use 1.0.” These are scenario editors, not new controls for modifying an already-running Live timeline.

Loading a saved configuration/session retains non-1.0 values exactly. Beginning a normal Live Change copies the complete current configuration into the draft, including hidden nominal scaling; applying edits therefore preserves it. Saving, comparison, checkpoints and continuing simulation retain the values. Nothing is silently reset or interpreted as productivity.

For loaded custom scaling, expanded Configuration shows a read-only “Advanced nominal scaling retained” notice with both actual values and explains that available capacity includes them. The general formula for these configurations remains:

```text
People × stored nominal capacity per person × Availability
```

Normal new Live presets all use 1.0. No migration or model-version bump is needed because the allocation and persistence semantics are unchanged. Historical capacity interventions can still be displayed using explicit per-person labels.

## Runtime observability

Live Status retains actual used / available capacity and utilization. Development Capacity Used, Available Developer Capacity, Effective Development Work, tester metrics, Performance Trend and Before/After remain intact. No metric/chart was added or removed.

Utilization remains consumed / available capacity. Productivity and collaboration determine work after allocation; neither changes the available pool. Saved advanced scaling is acknowledged in the capacity tooltip and custom-configuration notice.

## Automated validation

Six new cases in `CapacityUxTests` cover:

1. Normal 5 developers / 2 testers / 100% availability produces pools 5 / 2.
2. 80% developer and 75% tester availability produces pools 4 / 1.5.
3. 1.5x Development productivity leaves the developer pool at 4 while producing 6 effective work from 4 consumed capacity; utilization remains 100%.
4. Every normal Live preset retains internal per-person capacity 1.0.
5. Normal Live Change exposes the seven people/availability/productivity inputs with help and no nominal-capacity fields; normal Analyze changes do not expose them either.
6. An old model-0.3 scenario with explicit 1.7 / 0.4 per-person capacities and no productivity field retains those values through configuration editing, scenario save/load, comparison description, Live save/load, normal interventions and checkpoint restore. Historical observations are unchanged. At five developers and 80% availability, the custom developer pool remains 6.8.

**Complete suite: 408 passing tests** — 131 Core, 166 Application, 111 UI; zero failures/skips. The previous 402 cases pass unchanged. Release build: **0 warnings, 0 errors**. `git diff --check` passes.

## Actual Avalonia verification

The [native verification host](verification/capacity-ux/Program.cs) ran the real Avalonia MainWindow with the platform backend. It exercised native control automation peers and TextBox bindings, then saved window captures for visual inspection. This was automated native interaction plus visual image review, **not physical mouse/keyboard operation**.

Verified setup and Change contain all seven normal inputs and no per-person capacity editors, including after expanding More settings. Checked the Advanced labels and help. Inspected the custom-capacity notice and verified custom scaling survives a normal intervention.

The requested normal scenario was entered through actual bound inputs:

- 5 developers, 2 testers;
- developer availability 80%, tester availability 100%;
- Development productivity 1.5x, Review and Testing 1x;
- Always Available supply, default WIP/effort.

After one simulated day the real window showed **Dev 4 / 4 · 100%**, **Test 0 / 2 · 0%**, and Development **Capacity used 4 / Effective work 6**. The available developer pool was 4, not 6. The complete collapsed Flow Board fits at both 1280×800 and 960×720, without horizontal overflow.

Evidence:

- [Execution result](verification/capacity-ux/result.txt)
- [Normal configuration](verification/capacity-ux/screenshots/configuration.png)
- [Normal Live Change](verification/capacity-ux/screenshots/change.png)
- [Runtime 4 capacity / 6 work](verification/capacity-ux/screenshots/runtime-1280.png)
- [Narrow runtime](verification/capacity-ux/screenshots/runtime-960.png)
- [Loaded custom values](verification/capacity-ux/screenshots/custom-loaded.png)
- [Advanced controls](verification/capacity-ux/screenshots/advanced.png)

```sh
dotnet test SoftwareDevelopmentSimulation.sln -c Release
dotnet build SoftwareDevelopmentSimulation.sln -c Release --no-restore
dotnet run --project docs/verification/capacity-ux/NativeVerification.csproj -c Release
```

## Semantic and scope confirmation

Developer priority remains **Code Review → Rework → Development**. Capacity Availability, stage productivity, consumed/available utilization and Development Collaboration Model v1 are unchanged. WIP, Work Supply, arrivals, defects, Rework, intervention timing, checkpoints, Before/After and Performance Trend calculations are unchanged.

No Technical Debt, Rework Productivity, AI-specific behavior, new metrics/charts, collaboration rules, individual productivity, skills, multiple teams or recommendations were introduced. **Technical Debt has not been started.**
