# Reference & Glossary {#reference}

Use this chapter to resolve terminology and common questions. Search matches titles, body text, abbreviations and topic keywords. Learn more links open the same guide at a stable section; they do not change simulation state.

## Glossary and abbreviations {#reference.glossary}

| Term | Meaning |
| --- | --- |
| Backlog | Work that has not started Development, including dependency waiting. |
| Capacity unit | Abstract raw resource consumption; not an hour. |
| Productivity | Effective work per consumed raw capacity unit. |
| Availability | Fraction of nominal role capacity available; also used in modeled staffing cost. |
| Specialist | A subset of developers eligible for specialist Development work. |
| WIP | Work in progress; distinguish active-stage limits from total started unreleased work. |
| FIFO | First in, first out; queue-entry order used for admission/work selection. |
| KPI | Key performance indicator; a measure with a defined unit and observation basis. |
| P85 | Nearest-rank 85th percentile. |
| Throughput | Internal releases per observed period, displayed per five days. |
| Cycle time | Development start to internal release, for the released cohort. |
| Lead time | Broader elapsed time can include Backlog or customer delay; do not substitute it for this cycle-time KPI. |
| Utilization | Consumed divided by available raw capacity. |
| Rework | Repeated corrective work after a discovered defect. |
| Technical debt | Modeled effort debt and its effect on future Development plans. |
| Internal release | Ready work moved to Released under the release policy. |
| Customer launch | Active version’s internally released work recorded as customer delivered. |
| SEK / MSEK | Swedish kronor / millions of Swedish kronor. |
| Allocation | Estimated attribution of incurred staffing cost to work items. |
| Overhead | Incurred cost left outside item-level allocations. |
| N/A | Required data, cohort or denominator is unavailable; not zero. |
| Seed | Starting value for deterministic pseudo-random streams. |
| Checkpoint | Restorable model state, including random states and history. |
| Intervention | A recorded configuration change taking effect the next simulated day. |
| Sprint | A configured number of simulated working days for launch planning. |

[Workflow](#how-flow-works), [KPIs](#kpis-performance) and [Economics](#economics) explain the terms in context.

## Frequently asked questions {#reference.faq}

**Why is utilization high but throughput low?** Work can queue at another stage, need Rework, or wait for an internal release opportunity. Inspect the whole path.

**Why does cost per delivered item show N/A?** No internal releases, missing monetary history or an unavailable denominator can make a ratio undefined.

**Why did Unlaunched Development Cost fall after launch?** Included awaiting allocations moved to Customer-Launched Cost. No staffing expense was reversed.

**Why is the development queue different from Development WIP?** The KPI means Backlog; WIP is already admitted work.

**Why does a specialist wait while another worker looks busy?** Sprites illustrate aggregate capacity, not scheduled employees. Specialist eligibility and higher-priority work matter.

**Why are there fewer boxes than items?** Package stacks are bounded/grouped; numerical counts are exact.

**Does changing speed change the result?** Playback speed changes presentation timing, not per-day model rules.

**Can I keep help open during LIVE?** Yes. Help is non-modal and does not pause, advance or modify the simulation.

## Troubleshooting {#reference.troubleshooting}

- **Apply Changes does not complete:** check the validation text and numeric fields for invalid characters or out-of-range values. A validation failure does not apply a partial intervention.
- **Resume is unavailable at a launch target:** open Product Version Details and choose Launch Now or Plan / Postpone Launch.
- **The workshop is off-center:** use Fit to View; confirm the selected rendering mode and available window size.
- **KPIs are not visible:** enable Team Performance, inspect the selected cards under KPIs…, and scroll if using a small window or expanded details.
- **A queue is empty but its chart is nonzero:** inspect the historical date and rolling period; a snapshot and a historical point describe different times.
- **Imported costs are partial:** old history may not contain monetary or raw-capacity observations. Missing allocations are not reconstructed using current rates.
- **A help search finds nothing:** try a shorter word or abbreviation, such as review, WIP, launch or SEK. Clear the search to return to reading.

Save a LIVE session explicitly if you need to resume later. Closing the application does not automatically save it.

## Model and guide limitations {#reference.limitations}

FLOW is a simplified, daily-step learning model. It has aggregate resource pools, bounded per-item contributions, simplified defect discovery and effort-based technical debt. It does not model real employee calendars, hiring delays, learning curves, business value, revenue, production incidents or every dependency in a real organization.

The guide describes the implemented behavior audited for B6.5.3. Screenshot values are captured examples and may differ from your settings. Historical imports can have less detail than new sessions. Illustrations explain relationships; they are not additional calculations or predictive analytics.

Use the verification report in the repository for implementation references and audit findings. No inaccessible editorial drafts were reviewed.

## Insight terminology {#reference.insight-terms}

| Term | Meaning |
| --- | --- |
| Observation | A measured condition or change, with its actual period and evidence. |
| Possible explanation | An explicitly uncertain hypothesis; not a proven cause. |
| Evidence strength | A description of supporting observations, not a confidence percentage. |
| Material insight change | A change in station-pressure classification, or recurrence after resolution. Dates and raw count changes alone do not qualify. |
| Reviewed insight | Evidence acknowledged by selecting it; review does not change the simulation. |
| Suggested experiment | An optional supported comparison intended to test a hypothesis, never an automatic intervention. |

See [Simulation Insights](#experiments.simulation-insights) for windows, limitations and acknowledgement lifetime.

## Evidence charts and prepared baselines {#reference.prepared-comparison}

| Term | Meaning |
| --- | --- |
| Recorded sample | A value at an actual completed simulated day; no invented intermediate value. |
| Endpoint growth | Last observed queue size minus first; not necessarily continuous or sustained growth. |
| Frozen baseline | A captured simulation state, including current random-stream state, retained independently while LIVE continues. |
| Prepared comparison | A temporary editable proposal that requires explicit confirmation to run isolated branches through the existing guided comparison. |

See [visual evidence and prepared comparisons](#experiments.insight-evidence).
