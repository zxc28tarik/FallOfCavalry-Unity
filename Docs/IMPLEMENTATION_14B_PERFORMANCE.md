# Implementation 14B performance evidence

The focused .NET benchmark test performs 100 manifest parses, 100 complete historical/domain/save validations and 100 full save projections. Initial local Release evidence: manifest parses 45–50 ms total, validations about 467 ms total and save projections about 24 ms total on the validation machine. These are diagnostic measurements, not frame-time guarantees.

The content gate also reuses the existing visual pipeline and its bounded signature/pool benchmarks. Final acceptance records exact Unity logs and the Windows Development artifact; no rendered FPS, GPU time, production hardware target or final art budget is inferred from headless tests.
