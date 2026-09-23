# Implementation 13 production-foundation performance

The benchmark measures actual v12 mapping, invariant validation, canonical serialization, deserialization and runtime reconstruction. It does not substitute synthetic DTO-only work for production code paths.

| Tier | Characters | Payload bytes | Map | Validate | Serialize | Deserialize | Reconstruct |
|---|---:|---:|---:|---:|---:|---:|---:|
| Small | 10 | 2,829 | 1.042 ms | 0.081 ms | 0.052 ms | 0.067 ms | 0.655 ms |
| Medium | 100 | 23,170 | 0.270 ms | 0.168 ms | 0.190 ms | 0.236 ms | 0.368 ms |
| Large | 500 | 113,970 | 0.396 ms | 0.618 ms | 0.930 ms | 2.914 ms | 1.722 ms |

Validation machine details match `IMPLEMENTATION_13_LONG_RUN_RESULTS.md`. Figures are observations from one Release run, not guaranteed thresholds. Payload size and work scale with real collection cardinality; histories and caches used by the proof stay bounded. Run `Tools/Benchmark-ProductionFoundation.ps1` for raw current-machine evidence under ignored `TestResults/`.
