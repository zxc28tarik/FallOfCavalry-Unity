# Implementation 8.5 Performance Results

## Environment

- Unity: 6000.3.16f1
- OS: Windows 11 (10.0.26200)
- CPU: Intel Core i5-10210U @ 1.60 GHz, 8 logical processors
- System memory reported by Unity: 8025 MB
- Render pipeline: Built-in
- Mode: Editor batchmode, `-nographics`
- Graphics device/API: Null Device / Null
- Resolution: unavailable (`-nographics`)
- Warmup: 10 consolidated-prefab instantiations

GPU timing, render-thread timing, real frame timing, batches and playable-scene FPS are unavailable in this headless run and were not fabricated.

## Actor-count sweep — cached consolidated body plus rigid weapon

| Actors | Spawn/assembly ms | Configured renderers | Unique shared materials | Managed-memory delta |
|---:|---:|---:|---:|---:|
| 1 | 1.607 | 6 | 2 | 0 B |
| 50 | 29.471 | 300 | 2 | 0 B |
| 100 | 56.252 | 600 | 2 | 0 B |
| 250 | 176.802 | 1,500 | 2 | 0 B |
| 500 | 394.163 | 3,000 | 2 | 36,864 B |

Configured renderer counts include the three renderers registered in each LODGroup; Unity selects the visible LOD at render time. This headless measurement does not claim that every configured renderer is drawn simultaneously.

## Scenario results at 100 actors

| Scenario | Strategy | Spawn/assembly ms | Configured renderers | Shared materials |
|---|---|---:|---:|---:|
| Varied infantry | modular | 58.623 | 1,200 | 3 |
| Infantry + cavalry | cached consolidated | 66.687 | 900 | 3 |
| Named subset | modular high detail | 57.317 | 1,500 | 2 |
| Far/crowd | LOD2 representation | 5.628 | 100 | 1 |

## Architecture comparison at 100 actors

| Strategy | Spawn/assembly ms | Configured renderers | Shared materials |
|---|---:|---:|---:|
| Modular | 56.303 | 1,200 | 3 |
| Cached consolidated | 44.700 | 600 | 2 |
| Far/crowd representation | 5.104 | 100 | 1 |

## Decision

The production starting default is cached/prebuilt consolidated body/clothing/armor plus separate rigid equipment and mount. The proof comparison reduced configured renderers by 50% versus modular assembly and was faster in the warmed 100-actor comparison. Modular assembly remains the authoring model and Narrative exception. The far representation is retained as an experimental crowd tier pending a real GPU/animation benchmark.
