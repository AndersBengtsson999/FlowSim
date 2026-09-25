# Step 7 — measured defects and rework results

Measured on macOS ARM64 with .NET 10.0.401, Release configuration. Values below come from engine results, not prescribed targets. Seed 12345; horizon 100 working days; 30 independent items; 5 developers and 2 testers at capacity 1; Development/Review/Testing WIP 5/3/3.

Fixed uses effort 5/1/2. Variable uses triangular effort 2/5/12, 0.5/1/3 and 1/2/5. The defect example preserves that same generated variable workload, enabling review probability 0.15, testing probability 0.10, Rework WIP 3, and triangular rework effort 0.5/1/3 and 1/2/5 by discovery source.

## Single runs

| Metric | Fixed, defects off | Variable, defects off | Variable, defects on |
|---|---:|---:|---:|
| CompletedWorkItems | 30 | 30 | 30 |
| ThroughputPerFiveDays | 1.5 | 1.5 | 1.5 |
| AverageLeadTime | 23.8333 | 29.8333 | 32.6 |
| AverageCycleTime | 9.6667 | 12.5 | 14.9333 |
| AverageWip | 2.6 | 3.45 | 4.18 |
| DeveloperUtilization | 36.00% | 42.14% | 47.82% |
| TesterUtilization | 30.00% | 37.03% | 45.60% |
| TotalDefectsFound | 0 | 0 | 8 |
| CodeReviewDefectsFound | 0 | 0 | 2 |
| TestingDefectsFound | 0 | 0 | 6 |
| WorkItemsWithDefects | 0 | 0 | 6 |
| TotalReworkCount | 0 | 0 | 8 |
| TotalReworkEffort | 0 | 0 | 19.2479 |
| ReworkDeveloperCapacityShare | 0.00% | 0.00% | 8.05% |
| AverageReworkEffortPerCompletedItem | 0 | 0 | 0.6416 |
| AverageCodeReviewAttempts | 1 | 1 | 1.2667 |
| AverageTestingAttempts | 1 | 1 | 1.2 |

All three runs finish 30 items within the horizon. Consequently throughput remains 1.5 items per five days even when defects increase elapsed times and consumed capacity. This finite-workload/horizon effect is reported without changing the model. Fixed versus Variable also changes mean initial effort; only the two Variable columns isolate enabling defects for this generated workload.

## Monte Carlo

500 runs use seeds 12345 through 12844. Two complete executions produced identical serialized aggregates and ordered per-run results. One measured batch after warm-up took **1.633 seconds** (environment-specific, not a performance guarantee).

| Metric | P50 | P75 | P85 | P95 |
|---|---:|---:|---:|---:|
| CompletedWorkItems | 30 | 30 | 30 | 30 |
| ThroughputPerFiveDays | 1.5 | 1.5 | 1.5 | 1.5 |
| AverageLeadTime | 36.25 | 37.8 | 38.7383 | 40.2083 |
| AverageCycleTime | 16.1833 | 16.875 | 17.4383 | 18.5 |
| AverageWip | 4.555 | 4.7625 | 4.9315 | 5.25 |
| DeveloperUtilization | 53.05% | 55.55% | 56.87% | 59.06% |
| TesterUtilization | 44.38% | 46.50% | 47.71% | 51.29% |
| MaximumWaitingForCodeReviewQueue | 3 | 3 | 3 | 4 |
| MaximumWaitingForTestingQueue | 3 | 3 | 3 | 4 |
| TotalDefectsFound | 9 | 11 | 13 | 15 |
| WorkItemsWithDefects | 7 | 8 | 9 | 11 |
| TotalReworkEffort | 16.9342 | 21.6226 | 24.4353 | 30.2849 |
| ReworkDeveloperCapacityShare | 6.44% | 8.01% | 8.93% | 10.59% |

All 500 runs finish all 30 items. Every distribution therefore has 500 samples, including lead/cycle time. Percentiles use the existing linear interpolation definition; they describe per-run aggregates, not individual-item percentiles or confidence intervals.

## Verification and interpretation

- Release build: no warnings or errors. All 133 xUnit tests pass (77 Core, 37 Application, 19 UI).
- Both no-defect presets were compared field-by-field against pre-Step-7 outputs, including all prior metrics, item timestamps, daily state and capacity data: unchanged.
- Tests cover endpoint probabilities, repeated failures, both discovery sources, source-specific effort, shared resource priority, active Rework WIP, FIFO re-entry, dependencies through loops, zero-effort rework, incomplete inspections, immutable/reproducible events and Monte Carlo aggregation.
- Native Avalonia verification covers quality enable/disable, preset inputs, both failure sources, selected-day feedback counts, six snapshot charts, quality metric labels, item history/columns, repeated single runs, reset and a responsive 500-run batch (488 UI timer ticks during execution).
- Simulation.Core retains no project/package dependencies or UI references. No new third-party package was needed.

ReworkCount counts episodes admitted to Rework, not discoveries still queued. TotalReworkEffort is actual consumed correction work; assigned and remaining effort are separate item fields. Repeated review/testing costs are counted in those stages and are excluded from the rework-share numerator. That share divides by used developer capacity, not available capacity. Average inspection attempts includes created incomplete and zero-attempt items; average rework effort per completed item uses Done items only.

A completed failed inspection contributes a completion timestamp and history entry but never Done. First inspection starts remain available, completion summaries show the latest completed attempt, and detailed attempts disambiguate repeats. There is no artificial loop cap; the horizon bounds scenarios, including probability 1. Defects represent configured discovery probabilities, not a separately modeled latent defect population, production failures, technical debt or team skill.

See [SIMULATION_MODEL.md](SIMULATION_MODEL.md#defects) for the authoritative rules.
