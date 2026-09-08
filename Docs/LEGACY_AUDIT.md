# Legacy FOC Audit — Implementation 0

Audited source: `C:\Users\zxc28\Desktop\FallOfCavalry`, branch `333`, commit `d46e8bab06e73c9dcb9369ed2c313d787251c7a0`. Its pre-existing modified `output/web-game/vite-open.out.log` was not changed.

## Classification

| Legacy material | Classification | Implementation 0 decision |
|---|---|---|
| Pure seeded-generation concepts and same-seed tests | ADAPT | Keep the determinism intent; replace JS helpers with explicit `IRandomSource` and capturable C# RNG state. |
| Save version and roundtrip test intent | REWRITE | Preserve the behavioral requirements; replace localStorage/browser JSON and normalization with explicit DTO, migration, validation, serializer, and atomic store contracts. |
| `js/core/state.js` global `GameState` | REWRITE | Replace the concept with bounded domain runtime state and authoritative services; do not copy the singleton. |
| Browser `localStorage`, DOM, Phaser, global singleton wiring | REMOVE | Incompatible with the locked Unity layered architecture. |
| Gameplay `Math.random`, `Date.now`, and real-time-derived campaign outcomes | REMOVE | Violates explicit RNG and WorldClock authority. |
| Assassination/sabotage/deep-intrigue or other removed/out-of-roadmap features | REMOVE | Must not return through legacy migration. |
| Map data, art, historical content, and feature-specific tests | REFERENCE_ONLY | Potential future inputs for their assigned packages; no production integration in Implementation 0. |
| Runtime code copied without change | KEEP | None. No audited runtime file met the new dependency and authority constraints unchanged. |

## Result

The new repository is not an incremental patch inside the legacy project. No legacy runtime file was copied. Relevant concepts were re-expressed behind new public contracts and mutation-risk tests.

