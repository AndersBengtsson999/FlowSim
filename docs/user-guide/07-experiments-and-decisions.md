# Experiments & Decisions {#experiments}

An experiment tests a stated hypothesis under known assumptions. Establish a baseline, preserve its state, change one variable where practical, and compare both delivery and resource consequences. Treat surprising results as questions to investigate rather than verdicts.

![Day 100 intervention: Before days 81–100 and After days 101–120, effective from Day 101.](assets/images/intervention.png)

*Timing diagram. Queue improvements must be observed; they are not guaranteed.*

## Interventions and before/after timing {#experiments.interventions}

1. Pause and record the problem, relevant KPI window and queue evidence.
2. Create a Checkpoint.
3. Select Change team / work. Edit the supported assumption and optionally label the change.
4. Apply Changes. Correct any validation error before resuming.
5. Advance enough days for the new assumption to affect downstream work.

**A change recorded on Day N takes effect on Day N+1.** For a Day 100 intervention and a 20-day comparison window, Before is **Days 81–100 inclusive** and After is **Days 101–120 inclusive**. At Day 108, only Days 101–108 are available in After; the remaining days are not invented.

Early Before history may also be partial. Other interventions inside the comparison range can confound interpretation. Do not confuse this intervention-anchored comparison with a dashboard’s current rolling window or its displayed historical range.

## Test staffing, productivity and WIP {#experiments.capacity}

| Hypothesis | What to inspect |
| --- | --- |
| Add a tester to reduce testing waits | Waiting count, tester utilization, cycle time, release throughput and daily cost. |
| Add a developer | Review pressure, Development output, downstream testing queue and cost. Shared priorities can change where capacity goes. |
| Increase stage productivity | Effective work, raw utilization, queues and throughput. Productivity is not extra staff. |
| Change WIP limits | Admission, queue displacement, age of WIP and idle capacity. Limits do not create capacity. |
| Change launch frequency | Awaiting-launch balance and launch history. Customer launch alone does not increase internal-release throughput. |

Use the guided staffing comparison when available: it captures a baseline from the same checkpoint and supports a matched evaluation period. It does not prove that all later random events remain paired. For broader changes, retain the baseline, record the exact settings and use the existing scenario comparison tools.

## Reproducibility and checkpoints {#experiments.reproducibility}

A checkpoint captures the simulation state and random-stream state. Restoring it returns to that state, including the timeline needed for replay. Saved LIVE sessions support later continuation under the supported model versions.

Record the seed, initial settings, arrival mode, effort distributions, intervention day and changed values. Keep the evaluation duration fixed when comparing alternatives. A seeded run is reproducible under the same event sequence; policies can diverge after an intervention. Repeat across multiple seeds for robustness.

## Avoid misleading conclusions {#experiments.causality}

A queue shrinking immediately after a staffing change may reflect work already completed upstream. A large release batch can temporarily improve throughput or cost per delivered item without improving the underlying flow. Different work sizes can change item-based KPIs. A customer launch can reduce unlaunched cost without reducing expense.

Use at least one flow measure, one queue/unfinished-work measure and one resource measure. State alternative explanations and note partial periods. Test one change where possible, then replicate across seeds. FLOW explores consequences within a simplified model; it cannot establish real-world causality by itself.
