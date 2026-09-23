# Implementation 12 Presentation performance

Environment: Unity `6000.3.16f1`, Windows batchmode, `-nographics`. Artifact: ignored `TestResults/presentation-pipeline-result.txt`.

The measured pipeline constructs the real UXML shell, controller, view model and fixed-height `ListView` source. Measurements from the first complete gate run:

| Rows | Shell/list source build | Managed allocation delta | Rendered live rows |
|---:|---:|---:|---:|
| 100 | 3.648 ms | 184,320 B | unavailable headless |
| 500 | 3.011 ms | 266,240 B | unavailable headless |
| 1,000 | 2.701 ms | 475,136 B | unavailable headless |
| 5,000 | 5.675 ms | 1,232,896 B | unavailable headless |

The engine-independent virtualization window is bounded to 24 visible plus four rows of overscan on each side: maximum 32 live row models at 5,000 source rows. UI Toolkit `ListView` is configured for fixed-height recycling. Headless construction does not attach a render panel, so live `VisualElement` row creation, scroll responsiveness and FPS are explicitly **not measured**.

Read-model construction measured 100 Character rows at 0.416 ms / 16,384 B, 500 Soldier rows at 0.365 ms / 45,056 B, and 1,000 Report/Contract rows at 0.694 ms / 159,744 B. These are proof Presentation rows, not fabricated gameplay history and not per-frame workloads. The architecture performs no `Update()` world scan and rebuilds only on route/change/command refresh.

Screenshots at 1366×768, 1920×1080 and 2560×1440 are `NOT RUN` because the accepted gate uses `-nographics`. Structural responsive checks at those sizes plus 3440×1440 pass. VisualSoldier preview and Battle presentation reuse the existing pool/cache; this package creates no alternate hierarchy.
