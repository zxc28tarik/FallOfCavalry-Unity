# Implementation 11 AI Performance

Environment: local Release `.NET 10` test run on 2026-09-23. Values below are observed proof-run samples, not FPS claims or budgets. Timing varies by machine/load. Managed allocation is the observed `GC.GetTotalMemory` delta and is indicative rather than an allocation-profiler result.

| Decision owners | Mode | Cycle ms | Context builds | Candidate generations | Utility evaluations | Shared materializations | Selected | Managed delta bytes |
|---:|---|---:|---:|---:|---:|---:|---:|---:|
| 10 | individual | 58.735 | 10 | 10 | 10 | 0 | 10 | 82,240 |
| 100 | individual | 2.006 | 100 | 100 | 100 | 0 | 100 | 682,528 |
| 100 | aggregated | 6.912 | 1 | 1 | 100 | 100 | 100 | 719,680 |
| 500 | individual | 9.728 | 500 | 500 | 500 | 0 | 500 | 3,569,336 |
| 500 | aggregated | 11.545 | 5 | 5 | 500 | 500 | 500 | 3,724,832 |
| 1,000 | individual | 34.742 | 1,000 | 1,000 | 1,000 | 0 | 1,000 | 5,353,896 |
| 1,000 | aggregated | 21.642 | 10 | 10 | 1,000 | 1,000 | 1,000 | 4,039,880 |

With proof batch size 100, shared low-importance scheduling reduces context builds and candidate-generation calls by 99% versus all-individual scheduling. Utility remains owner-specific: 1,000 owners still produce 1,000 evaluations and proposals. The equivalence test confirms identical selected actions versus individual planning while preserving every owner identity. These short microbenchmarks include warm-up/GC noise: structural work reduction is demonstrated at every scale, while elapsed-time improvement is observed at 1,000 owners and is not claimed as a universal speedup.

The benchmark creates no persistent controller state for actors without actual AI semantic state; v11 migration produces an empty AI list, avoiding automatic save bloat.
