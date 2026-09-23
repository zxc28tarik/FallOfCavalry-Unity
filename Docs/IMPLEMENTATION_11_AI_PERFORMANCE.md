# Implementation 11 AI Performance

Environment: local Release `.NET 10` test run on 2026-09-23. Values below are observed proof-run samples, not FPS claims or budgets. Timing varies by machine/load. Managed allocation is the observed `GC.GetTotalMemory` delta and is indicative rather than an allocation-profiler result.

| Decision owners | Important individual | Low-importance aggregated | Cycle ms | Context builds | Candidate generations | Utility evaluations | Shared materializations | Selected | Managed delta bytes |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 10 | 10 | 0 | 0.289 | 10 | 10 | 10 | 0 | 10 | 65,760 |
| 100 | 0 | 100 | 1.887 | 1 | 1 | 100 | 100 | 100 | 640,832 |
| 500 | 0 | 500 | 6.878 | 5 | 5 | 500 | 500 | 500 | 2,812,400 |
| 1,000 | 0 | 1,000 | 13.512 | 10 | 10 | 1,000 | 1,000 | 1,000 | 5,744,944 |

With proof batch size 100, shared low-importance scheduling reduces context builds and candidate-generation calls by 99% versus all-individual scheduling. Utility remains owner-specific: 1,000 owners still produce 1,000 evaluations and proposals. The equivalence test confirms identical selected actions versus individual planning while preserving every owner identity.

The benchmark creates no persistent controller state for actors without actual AI semantic state; v11 migration produces an empty AI list, avoiding automatic save bloat.
