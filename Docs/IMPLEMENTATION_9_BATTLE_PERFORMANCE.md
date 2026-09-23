# Implementation 9 Battle Performance Evidence

Environment: Unity `6000.3.16f1`, Windows Editor batchmode with `-nographics`. Proof assets use the existing Implementation 8.5 Standard consolidated path and signature pool/cache. Measurements are CPU wall time for assembly/binding and release in the Editor batch process; they are not production frame-time claims.

| Actors | Active after release | Renderers observed | Bind/assemble ms | Release ms | Created | Reused | Pooled |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 100 | 0 | 1,800 | 276.207 | 19.681 | 100 | 0 | 100 |
| 250 | 0 | 4,500 | 1,104.470 | 27.671 | 250 | 0 | 250 |
| 500 | 0 | 9,000 | 1,759.125 | 111.844 | 500 | 0 | 500 |

Source artifact: `TestResults/battle-pipeline-result.txt` from `Tools/Test-BattlePipeline.ps1`.

GPU timing, rendered FPS, draw calls, and target-hardware frame pacing are unavailable because this run used `-nographics`. No FPS value is inferred. These proof assets are deliberately modular test geometry; renderer counts are measurements of the proof path, not production budgets. A rendered Development build with production assets and a representative battle camera remains required before shipping performance targets can be set.
