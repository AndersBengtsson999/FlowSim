# Step 8 — Model validation and sensitivity results

Measured on macOS ARM64 using .NET 10.0.401, Release configuration, base seed 12345. This report records outcomes from the real engine. No Core source file or simulation rule was changed. No analytical shortcut or forced business conclusion was used.

## Setup and definitions

Unless explicitly stated otherwise, sweeps use **Steady Flow Validation**: 250 working days, 1,000 independent day-0 items, 5 developers, 2 testers, capacities 1/1, Development/Review/Testing WIP 10/5/10, Fixed effort 5/1/2, defects disabled. The measurement interval is [50,250); completion boundaries are (50,250]. The full run completed 204 items and ended with 785 still in Backlog, so this preset did not exhaust its input supply.

Each sweep changes one parameter from the same base. Probability sweeps use **Defect Stress Validation**, identical except defects are enabled with probabilities initially 0/0, Rework WIP 3, and triangular rework 1/3/6 (review) and 2/5/10 (testing). Their varying probability leaves the other at zero. All seven sweeps were repeated and returned identical serialized results.

Throughput is items per five working days. Lead/cycle/active/waiting means describe whole lifetimes of items finishing in the measurement window; they exclude incomplete items and are not clipped at day 50. Utilization/saturation below are ratios, so 1 = 100%. Queue means/maxima and WIP are end-of-day measurements. Saturation is after-admission occupancy. All values are rounded for presentation; calculations use original doubles.

## Main findings

- **Developer capacity has a strong response from 1 to 6 developers**: throughput increases from 0.825 to 5. Beyond 6, the tested 8 and 10 developer points retain throughput 5 while testing utilization stays at 100% and the maximum testing queue grows to 78 and 160. Developer capacity continues to be fully used; extra work accumulates downstream.
- **Tester response is strong from 1 to 2**, then nearly flat: throughput 2.5 → 4.175. Cycle time continues to decline modestly. At 10 testers only 16.5% of tester capacity is used. This is evidence over these configurations, not a staffing recommendation.
- **Development WIP matters strongly below 5**: WIP 1/2/3 yields throughput 1/2/3. WIP 5 through 15 all yield 4.175. The limit is reached on 100% of measured days at every point, but only the lower points suppress used developer capacity. At WIP 1, only 1.2 of 5 developer units/day are used. Saturation alone therefore does not identify a binding constraint.
- **Testing WIP has a strong step from 1 to 2**, then a plateau through 15. At WIP 1, throughput is 2.5 and the testing queue reaches 85. At 2 or above, throughput is 4.175 and the queue maximum is 5. WIP 2 and 3 both reach their limit on 67% of days, without a throughput difference.
- **Testing effort becomes strongly influential above 2 units**: effort 3/5/8/13 gives throughput 3.35/2/1.25/0.75. Test utilization is 100%, test WIP saturation 100%, and the maximum waiting queue grows to 159 at effort 13.
- **Both defect probabilities strongly affect this model**. Review discovery 0 → 70% reduces throughput 4.175 → 1.575; testing discovery 0 → 70% reduces it to 1.15. The paired 70%/70% stress test falls to 0.45, with 51.62% of used developer capacity spent directly on Rework during the measurement window.

## One-parameter sweeps

### Developers

| Developers | Throughput /5d | Lead | Cycle | Waiting | WIP | Dev util. | Test util. | Max review Q | Max test Q |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 0.825 | 152 | 62.7576 | 0 | 10.33 | 1 | 0.165 | 1 | 1 |
| 2 | 1.65 | 152 | 33 | 0 | 10.66 | 1 | 0.33 | 2 | 2 |
| 3 | 2.5 | 151.66 | 23.66 | 0 | 11.325 | 1 | 0.5 | 3 | 3 |
| 4 | 3.35 | 151.4925 | 18.9701 | 0 | 11.99 | 1 | 0.67 | 4 | 4 |
| 5 | 4.175 | 151.1976 | 16.6048 | 0 | 12.99 | 1 | 0.835 | 5 | 5 |
| 6 | 5 | 151 | 15.015 | 0 | 13.99 | 1 | 1 | 4 | 4 |
| 8 | 5 | 151 | 48.5 | 30 | 63.32 | 1 | 1 | 3 | 78 |
| 10 | 5 | 151 | 68.6 | 51.6 | 112.65 | 1 | 1 | 5 | 160 |

| Developers | Dev available | Dev used | Test available | Test used | Dev saturation | Review saturation | Test saturation | Avg review Q | Avg test Q |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 1 | 1 | 2 | 0.33 | 1 | 0 | 0 | 0.165 | 0.165 |
| 2 | 2 | 2 | 2 | 0.66 | 1 | 0 | 0 | 0.33 | 0.33 |
| 3 | 3 | 3 | 2 | 1 | 1 | 0 | 0 | 0.495 | 0.495 |
| 4 | 4 | 4 | 2 | 1.34 | 1 | 0 | 0 | 0.66 | 0.66 |
| 5 | 5 | 5 | 2 | 1.67 | 1 | 0.165 | 0 | 0.825 | 0.825 |
| 6 | 6 | 6 | 2 | 2 | 1 | 0 | 0 | 0.995 | 0.99 |
| 8 | 8 | 8 | 2 | 2 | 1 | 0 | 1 | 1.335 | 44.32 |
| 10 | 10 | 10 | 2 | 2 | 1 | 0.33 | 1 | 1.675 | 93.65 |

### Testers

| Testers | Throughput /5d | Lead | Cycle | Waiting | WIP | Dev util. | Test util. | Max review Q | Max test Q |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 2.5 | 151 | 74.6 | 41.6 | 61.325 | 1 | 1 | 5 | 76 |
| 2 | 4.175 | 151.1976 | 16.6048 | 0 | 12.99 | 1 | 0.835 | 5 | 5 |
| 3 | 4.175 | 151.5928 | 15.8144 | 0 | 12.32 | 1 | 0.5567 | 5 | 5 |
| 4 | 4.15 | 151.7952 | 15.4096 | 0 | 11.985 | 1 | 0.415 | 5 | 5 |
| 5 | 4.125 | 152 | 15 | 0 | 11.65 | 1 | 0.33 | 5 | 5 |
| 6 | 4.125 | 152 | 15 | 0 | 11.65 | 1 | 0.275 | 5 | 5 |
| 8 | 4.125 | 152 | 15 | 0 | 11.65 | 1 | 0.2062 | 5 | 5 |
| 10 | 4.125 | 152 | 15 | 0 | 11.65 | 1 | 0.165 | 5 | 5 |

| Testers | Dev available | Dev used | Test available | Test used | Dev saturation | Review saturation | Test saturation | Avg review Q | Avg test Q |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 5 | 5 | 1 | 1 | 1 | 0.165 | 1 | 0.825 | 41.825 |
| 2 | 5 | 5 | 2 | 1.67 | 1 | 0.165 | 0 | 0.825 | 0.825 |
| 3 | 5 | 5 | 3 | 1.67 | 1 | 0.165 | 0 | 0.825 | 0.825 |
| 4 | 5 | 5 | 4 | 1.66 | 1 | 0.165 | 0 | 0.825 | 0.825 |
| 5 | 5 | 5 | 5 | 1.65 | 1 | 0.165 | 0 | 0.825 | 0.825 |
| 6 | 5 | 5 | 6 | 1.65 | 1 | 0.165 | 0 | 0.825 | 0.825 |
| 8 | 5 | 5 | 8 | 1.65 | 1 | 0.165 | 0 | 0.825 | 0.825 |
| 10 | 5 | 5 | 10 | 1.65 | 1 | 0.165 | 0 | 0.825 | 0.825 |

### DevelopmentWipLimit

| DevelopmentWipLimit | Throughput /5d | Lead | Cycle | Waiting | WIP | Dev util. | Test util. | Max review Q | Max test Q |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 1 | 150.5 | 8 | 0 | 1.4 | 0.24 | 0.2 | 1 | 1 |
| 2 | 2 | 150.5 | 8 | 0 | 2.8 | 0.48 | 0.4 | 2 | 2 |
| 3 | 3 | 151.1667 | 8.3333 | 0 | 4.4 | 0.72 | 0.6 | 2 | 2 |
| 5 | 4.175 | 151.1976 | 10.6048 | 0 | 7.99 | 1 | 0.835 | 5 | 5 |
| 8 | 4.175 | 151.1976 | 14.1976 | 0 | 10.99 | 1 | 0.835 | 5 | 5 |
| 10 | 4.175 | 151.1976 | 16.6048 | 0 | 12.99 | 1 | 0.835 | 5 | 5 |
| 15 | 4.175 | 151.1976 | 22.6048 | 0 | 17.99 | 1 | 0.835 | 5 | 5 |

| DevelopmentWipLimit | Dev available | Dev used | Test available | Test used | Dev saturation | Review saturation | Test saturation | Avg review Q | Avg test Q |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 5 | 1.2 | 2 | 0.4 | 1 | 0 | 0 | 0.2 | 0.2 |
| 2 | 5 | 2.4 | 2 | 0.8 | 1 | 0 | 0 | 0.4 | 0.4 |
| 3 | 5 | 3.6 | 2 | 1.2 | 1 | 0 | 0 | 0.6 | 0.6 |
| 5 | 5 | 5 | 2 | 1.67 | 1 | 0.165 | 0 | 0.825 | 0.825 |
| 8 | 5 | 5 | 2 | 1.67 | 1 | 0.165 | 0 | 0.825 | 0.825 |
| 10 | 5 | 5 | 2 | 1.67 | 1 | 0.165 | 0 | 0.825 | 0.825 |
| 15 | 5 | 5 | 2 | 1.67 | 1 | 0.165 | 0 | 0.825 | 0.825 |

### TestingWipLimit

| TestingWipLimit | Throughput /5d | Lead | Cycle | Waiting | WIP | Dev util. | Test util. | Max review Q | Max test Q |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 2.5 | 151 | 74.6 | 59.6 | 61.325 | 1 | 0.5 | 5 | 85 |
| 2 | 4.175 | 151.1976 | 16.6048 | 1.6048 | 12.99 | 1 | 0.835 | 5 | 5 |
| 3 | 4.175 | 151.1976 | 16.6048 | 0.8024 | 12.99 | 1 | 0.835 | 5 | 5 |
| 5 | 4.175 | 151.1976 | 16.6048 | 0 | 12.99 | 1 | 0.835 | 5 | 5 |
| 8 | 4.175 | 151.1976 | 16.6048 | 0 | 12.99 | 1 | 0.835 | 5 | 5 |
| 10 | 4.175 | 151.1976 | 16.6048 | 0 | 12.99 | 1 | 0.835 | 5 | 5 |
| 15 | 4.175 | 151.1976 | 16.6048 | 0 | 12.99 | 1 | 0.835 | 5 | 5 |

| TestingWipLimit | Dev available | Dev used | Test available | Test used | Dev saturation | Review saturation | Test saturation | Avg review Q | Avg test Q |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 5 | 5 | 2 | 1 | 1 | 0.165 | 1 | 0.825 | 50.825 |
| 2 | 5 | 5 | 2 | 1.67 | 1 | 0.165 | 0.67 | 0.825 | 2.155 |
| 3 | 5 | 5 | 2 | 1.67 | 1 | 0.165 | 0.67 | 0.825 | 1.485 |
| 5 | 5 | 5 | 2 | 1.67 | 1 | 0.165 | 0.33 | 0.825 | 0.825 |
| 8 | 5 | 5 | 2 | 1.67 | 1 | 0.165 | 0 | 0.825 | 0.825 |
| 10 | 5 | 5 | 2 | 1.67 | 1 | 0.165 | 0 | 0.825 | 0.825 |
| 15 | 5 | 5 | 2 | 1.67 | 1 | 0.165 | 0 | 0.825 | 0.825 |

### TestingEffort

| TestingEffort | Throughput /5d | Lead | Cycle | Waiting | WIP | Dev util. | Test util. | Max review Q | Max test Q |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0.5 | 4.125 | 151.2 | 14.2 | 0 | 10.99 | 1 | 0.2062 | 5 | 5 |
| 1 | 4.15 | 151.1928 | 14.8072 | 0 | 11.485 | 1 | 0.415 | 5 | 5 |
| 2 | 4.175 | 151.1976 | 16.6048 | 0 | 12.99 | 1 | 0.835 | 5 | 5 |
| 3 | 3.35 | 150 | 46 | 18.0672 | 37.655 | 1 | 1 | 5 | 37 |
| 5 | 2 | 148.5 | 91.3 | 53.3 | 76.325 | 1 | 1 | 5 | 101 |
| 8 | 1.25 | 150 | 118 | 65.16 | 98.075 | 1 | 1 | 5 | 137 |
| 13 | 0.75 | 149 | 133.6667 | 57.4667 | 112.025 | 1 | 1 | 5 | 159 |

| TestingEffort | Dev available | Dev used | Test available | Test used | Dev saturation | Review saturation | Test saturation | Avg review Q | Avg test Q |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0.5 | 5 | 5 | 2 | 0.4125 | 1 | 0.165 | 0 | 0.825 | 0.825 |
| 1 | 5 | 5 | 2 | 0.83 | 1 | 0.165 | 0 | 0.825 | 0.825 |
| 2 | 5 | 5 | 2 | 1.67 | 1 | 0.165 | 0 | 0.825 | 0.825 |
| 3 | 5 | 5 | 2 | 2 | 1 | 0.165 | 1 | 0.825 | 18.325 |
| 5 | 5 | 5 | 2 | 2 | 1 | 0.165 | 1 | 0.825 | 56.725 |
| 8 | 5 | 5 | 2 | 2 | 1 | 0.165 | 1 | 0.825 | 78.325 |
| 13 | 5 | 5 | 2 | 2 | 1 | 0.165 | 1 | 0.825 | 92.175 |

### CodeReviewDefectProbability

| CodeReviewDefectProbability | Throughput /5d | Lead | Cycle | Waiting | WIP | Dev util. | Test util. | Max review Q | Max test Q |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 | 4.175 | 151.1976 | 16.6048 | 0 | 12.99 | 1 | 0.835 | 5 | 5 |
| 0.05 | 4.025 | 151.0373 | 16.1739 | 0 | 12.15 | 1 | 0.805 | 4 | 4 |
| 0.1 | 3.8 | 149.7763 | 16.9474 | 0 | 12.135 | 1 | 0.7575 | 4 | 3 |
| 0.2 | 3.275 | 149.0458 | 19.9389 | 0.0076 | 12.57 | 1 | 0.66 | 3 | 3 |
| 0.3 | 2.975 | 148.5126 | 21.916 | 0.0168 | 12.855 | 1 | 0.5975 | 3 | 3 |
| 0.5 | 2.225 | 150.7079 | 30.7191 | 0.3258 | 13.685 | 1 | 0.445 | 3 | 3 |
| 0.7 | 1.575 | 163.1111 | 51.873 | 5.1429 | 15.78 | 1 | 0.315 | 4 | 2 |

| Probability | Defects | Rework units | Rework share | Max rework Q | Rework saturation |
|---|---:|---:|---:|---:|---:|
| 0 | 0 | 0 | 0 | 0 | 0 |
| 0.05 | 8 | 27.2438 | 0.0272 | 1 | 0 |
| 0.1 | 23 | 66.7703 | 0.0668 | 2 | 0.01 |
| 0.2 | 50 | 157.2879 | 0.1573 | 2 | 0.05 |
| 0.3 | 66 | 211.193 | 0.2112 | 2 | 0.09 |
| 0.5 | 109 | 353.5054 | 0.3535 | 3 | 0.415 |
| 0.7 | 149 | 475.5661 | 0.4756 | 7 | 0.845 |

### TestingDefectProbability

| TestingDefectProbability | Throughput /5d | Lead | Cycle | Waiting | WIP | Dev util. | Test util. | Max review Q | Max test Q |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 | 4.175 | 151.1976 | 16.6048 | 0 | 12.99 | 1 | 0.835 | 5 | 5 |
| 0.05 | 3.925 | 151.2484 | 16.3185 | 0 | 12.095 | 1 | 0.8275 | 2 | 2 |
| 0.1 | 3.625 | 150.8276 | 18.2138 | 0 | 12.44 | 1 | 0.83 | 3 | 3 |
| 0.2 | 3 | 149.0917 | 23.1917 | 0.0583 | 13.415 | 1 | 0.8225 | 3 | 3 |
| 0.3 | 2.7 | 149.2315 | 26.3704 | 0.0926 | 13.73 | 1 | 0.82 | 2 | 2 |
| 0.5 | 1.875 | 149.5867 | 39.4667 | 1.5867 | 15.44 | 1 | 0.8025 | 3 | 3 |
| 0.7 | 1.15 | 158.1522 | 70.087 | 20.0652 | 25.745 | 1 | 0.7925 | 3 | 3 |

| Probability | Defects | Rework units | Rework share | Max rework Q | Rework saturation |
|---|---:|---:|---:|---:|---:|
| 0 | 0 | 0 | 0 | 0 | 0 |
| 0.05 | 8 | 49.7385 | 0.0497 | 1 | 0 |
| 0.1 | 20 | 105.5859 | 0.1056 | 1 | 0.02 |
| 0.2 | 44 | 228.6442 | 0.2286 | 2 | 0.145 |
| 0.3 | 56 | 288.6359 | 0.2886 | 2 | 0.185 |
| 0.5 | 86 | 455.1731 | 0.4552 | 4 | 0.635 |
| 0.7 | 112 | 548.6715 | 0.5487 | 16 | 1 |

## Extreme experiments

These paired stress experiments deliberately differ from one-parameter sweeps: the Testing Constraint pair changes both tester count and testing effort, and Defect/Rework Stress changes both probabilities. The development pair changes only developer count. All three produced distinguishable measurements; none triggered the documented weak-response warning.

### Testing Constraint

A: 5 developers, 1 tester, Fixed effort 5/1/10, WIP 10/5/10, defects disabled. B: identical except 10 testers and testing effort 1.

| Metric | A | B | Absolute Δ | Relative Δ % |
|---|---:|---:|---:|---:|
| CompletedWorkItems | 20 | 165 | 145 | 725 |
| ThroughputPerFiveDays | 0.5 | 4.125 | 3.625 | 725 |
| AverageLeadTime | 151 | 151 | 0 | 0 |
| AverageCycleTime | 143.6 | 14 | -129.6 | -90.2507 |
| AverageActiveTime | 16 | 7 | -9 | -56.25 |
| AverageWaitingTime | 40.3 | 0 | -40.3 | -100 |
| AverageWip | 119.325 | 10.825 | -108.5 | -90.9281 |
| DeveloperUtilization | 1 | 1 | 0 | 0 |
| TesterUtilization | 1 | 0.0825 | -0.9175 | -91.75 |
| MaximumWaitingForCodeReviewQueue | 5 | 5 | 0 | 0 |
| MaximumWaitingForTestingQueue | 172 | 5 | -167 | -97.093 |
| AverageAvailableDeveloperCapacity | 5 | 5 | 0 | 0 |
| AverageUsedDeveloperCapacity | 5 | 5 | 0 | 0 |
| AverageAvailableTesterCapacity | 1 | 10 | 9 | 900 |
| AverageUsedTesterCapacity | 1 | 0.825 | -0.175 | -17.5 |
| DevelopmentWipSaturation | 1 | 1 | 0 | 0 |
| CodeReviewWipSaturation | 0.165 | 0.165 | 0 | 0 |
| TestingWipSaturation | 1 | 0 | -1 | -100 |
| AverageWaitingForCodeReviewQueue | 0.825 | 0.825 | 0 | 0 |
| AverageWaitingForTestingQueue | 99.425 | 0.825 | -98.6 | -99.1702 |
| AverageBacklog | 866.675 | 866.675 | 0 | 0 |

### Development Capacity

A: 1 developer; B: 10 developers. Both have 20 testers, Fixed effort 5/1/1, WIP 10/5/20, defects disabled. Other Steady Flow parameters remain unchanged.

| Metric | A | B | Absolute Δ | Relative Δ % |
|---|---:|---:|---:|---:|
| CompletedWorkItems | 33 | 330 | 297 | 900 |
| ThroughputPerFiveDays | 0.825 | 8.25 | 7.425 | 900 |
| AverageLeadTime | 151 | 151 | 0 | 0 |
| AverageCycleTime | 61.7576 | 8 | -53.7576 | -87.0461 |
| AverageActiveTime | 7 | 7 | 0 | 0 |
| AverageWaitingTime | 0 | 0 | 0 | n/a |
| AverageWip | 10.165 | 11.65 | 1.485 | 14.609 |
| DeveloperUtilization | 1 | 1 | 0 | 0 |
| TesterUtilization | 0.0083 | 0.0825 | 0.0743 | 900 |
| MaximumWaitingForCodeReviewQueue | 1 | 5 | 4 | 400 |
| MaximumWaitingForTestingQueue | 1 | 5 | 4 | 400 |
| AverageAvailableDeveloperCapacity | 1 | 10 | 9 | 900 |
| AverageUsedDeveloperCapacity | 1 | 10 | 9 | 900 |
| AverageAvailableTesterCapacity | 20 | 20 | 0 | 0 |
| AverageUsedTesterCapacity | 0.165 | 1.65 | 1.485 | 900 |
| DevelopmentWipSaturation | 1 | 1 | 0 | 0 |
| CodeReviewWipSaturation | 0 | 0.33 | 0.33 | n/a |
| TestingWipSaturation | 0 | 0 | 0 | n/a |
| AverageWaitingForCodeReviewQueue | 0.165 | 1.675 | 1.51 | 915.1515 |
| AverageWaitingForTestingQueue | 0.165 | 1.65 | 1.485 | 900 |
| AverageBacklog | 965.335 | 743.35 | -221.985 | -22.9956 |

### Defect/Rework Stress

A: defect-enabled Steady Flow with probabilities 0/0. B: probabilities 0.7/0.7. Both use Rework WIP 3, review-origin triangular rework 1/3/6 and testing-origin 2/5/10.

| Metric | A | B | Absolute Δ | Relative Δ % |
|---|---:|---:|---:|---:|
| CompletedWorkItems | 167 | 18 | -149 | -89.2216 |
| ThroughputPerFiveDays | 4.175 | 0.45 | -3.725 | -89.2216 |
| AverageLeadTime | 151.1976 | 158.1111 | 6.9135 | 4.5725 |
| AverageCycleTime | 16.6048 | 98.8889 | 82.2841 | 495.5443 |
| AverageActiveTime | 8 | 24.7778 | 16.7778 | 209.7222 |
| AverageWaitingTime | 0 | 51.3333 | 51.3333 | n/a |
| AverageWip | 12.99 | 45.425 | 32.435 | 249.6921 |
| DeveloperUtilization | 1 | 1 | 0 | 0 |
| TesterUtilization | 0.835 | 0.3025 | -0.5325 | -63.7725 |
| MaximumWaitingForCodeReviewQueue | 5 | 4 | -1 | -20 |
| MaximumWaitingForTestingQueue | 5 | 3 | -2 | -40 |
| AverageAvailableDeveloperCapacity | 5 | 5 | 0 | 0 |
| AverageUsedDeveloperCapacity | 5 | 5 | 0 | 0 |
| AverageAvailableTesterCapacity | 2 | 2 | 0 | 0 |
| AverageUsedTesterCapacity | 1.67 | 0.605 | -1.065 | -63.7725 |
| DevelopmentWipSaturation | 1 | 1 | 0 | 0 |
| CodeReviewWipSaturation | 0.165 | 0 | -0.165 | -100 |
| TestingWipSaturation | 0 | 0 | 0 | n/a |
| AverageWaitingForCodeReviewQueue | 0.825 | 0.97 | 0.145 | 17.5758 |
| AverageWaitingForTestingQueue | 0.825 | 0.31 | -0.515 | -62.4242 |
| AverageBacklog | 866.675 | 944.215 | 77.54 | 8.9468 |
| MaximumWaitingForReworkQueue | 0 | 51 | 51 | n/a |
| AverageWaitingForReworkQueue | 0 | 31.78 | 31.78 | n/a |
| TotalDefectsFound | 0 | 175 | 175 | n/a |
| TotalReworkEffort | 0 | 516.1749 | 516.1749 | n/a |
| ReworkDeveloperCapacityShare | 0 | 0.5162 | 0.5162 | n/a |
| ReworkWipSaturation | 0 | 1 | 1 | n/a |

The testing pair increases throughput by 725%, reduces maximum testing queue 172 → 5, and changes tester utilization by −91.75 percentage points. The development pair increases throughput by 900%, with developer utilization remaining 100% in both. The defect pair reduces throughput by 89.22%, increases cycle time 16.60 → 98.89 days, and accumulates a rework queue of up to 51; Rework WIP is saturated on every measured day. These are model measurements, not classifications of organizations.

## Unexpected or weak responses investigated

### Small apparent throughput decrease with more testers

For 2/3/4/5/10 testers, full-run completions are 204/205/205/205/205. Completions by the excluded boundary 50 are 37/38/39/40/40. Thus included completions are 167/167/166/165/165, explaining the apparent 4.175 → 4.125 decrease. Faster testing moved completions into the excluded warm-up period. Development/review work was unchanged.

Follow-up real-engine runs with warm-up 52 gave **4.1666667 for all five counts**. Extending the horizon to 1,000 days and measuring [100,1000) also gave **4.1666667 for all five**. Backlog remained sufficient. This is fixed-effort batch timing and window placement, not evidence that extra testers reduce sustained capacity. Small reversals in the low testing-effort points have the same boundary-sampling risk; no monotonicity was forced into code.

### Nearly constant lead time despite strong capacity changes

The testing and development extreme pairs both have mean window-completion lead time 151 days in A and B. Every item was created at day 0, and completions are spread across approximately the same [50,250] window. Mean lead time is therefore mean completion day, close to the window midpoint, even though the numbers of completions differ dramatically. This completion cohort also selects different items under different configurations. It is unsuitable as a standalone validation of capacity response in a bulk-arrival experiment. Throughput, cycle time, unfinished backlog and queues show the effects directly.

### Active admission, waiting and saturation semantics

Developer WIP is saturated even in configurations where increasing it no longer changes throughput. Active admission can occur before capacity is available; those stalled intervals increase cycle time but do not increase queue WaitingTime. In the development extreme, A has cycle time 61.76 days, active time 7, and queue waiting 0. This large gap follows documented active-state stalling, not lost elapsed time. The system does not model named developers, strict simultaneous-person assignment or exhaustive time accounting.

With unbounded waiting queues, more development can produce accumulated testing/rework work without more completions. Some extreme windows are not steady state. A warm-up removes startup measurements but cannot stabilize an overloaded model. These assumptions deserve review before using the simulator for calibrated organizational forecasts.

No implementation defect violating an existing documented simulation rule was identified. No engine change was made. The weak-response threshold did not catch the insensitive lead-time metric on its own: it considers several metrics together and is an investigation aid, not a comprehensive correctness validator.

## Monte Carlo sensitivity

Base: existing Defects & Rework Example (100 days, 30 items, variable effort, review/testing discovery 15%/10%, original rework distributions), seed 12345, warm-up 10. Testers varied through 1, 2, 5; each point and the separate base use the same 100 seeds (12345–12444). Two complete analyses matched exactly, including all metrics, diagnostics and deltas. One measured 400-run analysis took 0.734 seconds, environment-specific.

| Testers | Metric | P50 | P85 | P95 | Samples |
|---|---:|---:|---:|---:|---:|
| 1 | ThroughputPerFiveDays | 1.6111 | 1.6667 | 1.6667 | 100 |
| 1 | AverageLeadTime | 52.9322 | 55.8423 | 59.0669 | 100 |
| 1 | AverageCycleTime | 33.2201 | 37.0714 | 39.1828 | 100 |
| 2 | ThroughputPerFiveDays | 1.6111 | 1.6667 | 1.6667 | 100 |
| 2 | AverageLeadTime | 37.3391 | 39.0383 | 40.4692 | 100 |
| 2 | AverageCycleTime | 16.4464 | 17.4345 | 18.8679 | 100 |
| 5 | ThroughputPerFiveDays | 1.6111 | 1.6667 | 1.6667 | 100 |
| 5 | AverageLeadTime | 36.75 | 38.8041 | 40.0117 | 100 |
| 5 | AverageCycleTime | 15.7497 | 16.9071 | 17.6328 | 100 |

The finite 30-item Monte Carlo example can deplete its backlog, so throughput percentiles alone can hide timing differences. This batch verifies seeded variation and percentile presentation; sustained-capacity conclusions above use the 1,000-item preset.

## Verification and implementation scope

- Entire Release solution builds with no warnings/errors; 159 xUnit tests pass: 77 Core, 60 Application, 22 UI ViewModel.
- New tests cover all 11 supported parameter mappings, base immutability, point ordering/duplicates, triangular bounds, no-defect metric equivalence, developer/tester/effort/defect response, warm-up completion/discovery boundaries, capacity/WIP saturation, zero/missing deltas, Monte Carlo seed reuse, validation and cancellation, and UI capture/formatting.
- Native Avalonia on macOS ran the Steady Flow sweeps for Developers, Testers, Development WIP, Testing WIP and Testing Effort, plus a defect-probability sweep and all three extreme tests. Chart/table bindings, Y selection and complete report rendering were checked and screenshots inspected. A 3-point × 100-run Monte Carlo sensitivity run kept the UI responsive (72 timer ticks); cancellation retained the preceding complete result.
- Core source files were compared byte-for-byte with their pre-Step-8 copy: unchanged. The Core project still has no UI/project/package dependencies.
- New Application files: SensitivityParameters.cs, AnalysisMetrics.cs, SensitivityAnalysisRunner.cs, ModelValidationRunner.cs, AnalysisReport.cs. New UI files: SensitivityViewModel.cs, SensitivityChart.cs, SensitivityView.axaml and code-behind. MainWindow and App integrate the new tab and cancellation. New Application/UI test files and documentation complete the change. No solution replacement, new packages, technical debt or additional simulation mechanisms were introduced.

The immutable analysis outputs retain full-run and measurement-window metric distributions; raw daily histories are generated in full by the existing engine, then released between analysis runs to bound memory. The Current Scenario form supports its existing generated workloads; arbitrary heterogeneous dependency graphs remain available through the existing Core API, not this generated-request sensitivity UI. Table sorting/export and automatic stationarity/bottleneck detection are not implemented.

## Recommendation

The existing core is sufficiently responsive and understandable to support another deliberately scoped simulation concept: all extreme tests produced substantial, explainable differences, and the surprising effects were traced to documented rules or measurement cohorts. No engine correction is justified by this evidence. Before interpreting results as organizational predictions, agree on the bulk-arrival/completion-cohort convention, active-state stalls and unbounded queues, and consider refining the validation workload/measurement design. Do not use mean lead time or WIP saturation alone as validation of response, and do not treat this Step 8 result as real-world calibration.
