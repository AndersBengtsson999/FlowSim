# Step 4 measured scenarios

Measured on macOS ARM64 with .NET 10.0.401. All scenarios run for 100 working days with 30 items of effort 5/1/2 and no dependencies. Baseline uses 5 developers, 2 testers, unit per-person capacity and WIP 5/3/3. `ComparisonScenarios.Create()` provides A/B/C. Values below are observations, not enforced targets.

| Metric | Baseline | A: 1 tester | B: 8 developers | C: development WIP 3 |
|---|---:|---:|---:|---:|
| SimulationDays | 100 | 100 | 100 | 100 |
| TotalWorkItems | 30 | 30 | 30 | 30 |
| CompletedWorkItems | 30 | 30 | 30 | 30 |
| IncompleteWorkItems | 0 | 0 | 0 | 0 |
| ThroughputPerDay | 0.3 | 0.3 | 0.3 | 0.3 |
| ThroughputPerFiveDays | 1.5 | 1.5 | 1.5 | 1.5 |
| AverageLeadTime | 23.8333 | 37 | 22.4333 | 31.1667 |
| AverageCycleTime | 9.6667 | 22.8333 | 9.9333 | 8.4 |
| AverageActiveTime | 8 | 8 | 8 | 8 |
| AverageWaitingTime | 0.1667 | 10.2 | 1.1333 | 0 |
| AverageBlockedTime | 0 | 0 | 0 | 0 |
| AverageWip | 2.6 | 6.55 | 2.68 | 2.22 |
| DeveloperUtilization | 0.36 | 0.36 | 0.225 | 0.36 |
| TesterUtilization | 0.3 | 0.6 | 0.3 | 0.3 |
| MaximumWaitingForCodeReviewQueue | 5 | 5 | 5 | 3 |
| MaximumWaitingForTestingQueue | 3 | 12 | 3 | 3 |

Utilization is shown as a ratio. Time averages use completed items only. WIP and queue sizes are end-of-day samples. Full definitions are in [SIMULATION_MODEL.md](SIMULATION_MODEL.md).

All four scenarios finish the same fixed workload before the horizon, so their horizon throughput is equal despite different flow times. Eight developers reduce average lead time here but increase average cycle time slightly; admission patterns and unchanged WIP limits matter. No rule was changed to hide this outcome. Idle days after the workload finishes remain in utilization and throughput denominators.

Baseline active time is 8 days and queue waiting averages 0.1667 days, while cycle time averages 9.6667 days. The difference reflects time admitted to an active state without receiving capacity; it is not included in queue waiting. End-of-day queue observations can also be nonzero even when next-boundary admission creates zero elapsed queue wait.

Verification: 73 xUnit tests passed (56 Core, 17 Application). All four runs matched pre-metrics final states, timestamps, remaining efforts, transition histories, admission occupancies and daily work allocations byte-for-byte in a temporary regression probe. Determinism is also covered by repeat-run tests.
