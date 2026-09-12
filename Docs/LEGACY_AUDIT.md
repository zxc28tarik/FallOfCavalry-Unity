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

## Implementation 2 addendum

Remote legacy refs `origin/main` and `origin/333` were re-audited for party, companion/network, assignment, household/family, faction/subgroup, political group, loyalty and ownership material. `js/core/state.js`, `js/entities/ai-party.js`, `js/managers/WorldPartyManager.js`, `js/systems/factions.js`, `js/systems/faction-wars.js`, quest/faction constants and settlement owner data contain no reusable typed social-domain contract.

- KEEP: none.
- ADAPT: party affiliation and owner-reference concepts only, behind new typed package authority.
- REWRITE: identity, assignment, affiliation, ownership and persistence when their owning later package is implemented.
- REMOVE: global mutable state, browser coupling, arbitrary string identity/ownership, wall-clock behavior and doctrine bonuses in social contracts.
- REFERENCE_ONLY: faction art/names, world-party AI/movement, battle deployment assignments, quest guild vocabulary and settlement owner maps.

## Implementation 3 addendum

Remote refs `origin/main` and `origin/333` were searched under `js`, `docs` and `tests` for religion, religious, faith, sect, confession, population faith, faction religion, tolerance and religion-driven diplomacy. Exact-word searches found no Religion/Sect domain implementation; broad `sect` hits were battle-sector text.

- KEEP: none.
- ADAPT: none.
- REWRITE: future faction/culture affiliation only through typed content definitions and explicit policy/event context.
- REMOVE: any automatic religious hostility, loyalty/unrest, rebellion/war or combat modifier and arbitrary string identity.
- REFERENCE_ONLY: faction names and geography for later researched content, not gameplay authority.

## Implementation 4 addendum

Remote refs `origin/main` and `origin/333` were audited for city, settlement, town, building, market, housing, castle, infrastructure, owner, population, security, health, production, garrison and city-manager behavior. Relevant sources include global `GameState`, `city-state.js`, `city-building-catalog.js`, CityManager, building-system, UI services, scene manifests and the city-overhaul documents.

- KEEP: visual reference assets only as historical repository material; no runtime contract qualifies unchanged.
- ADAPT: four-level fullness vocabulary and functional-area/content-pool intent.
- REWRITE: identity, area/building/infrastructure state, officials and persistence as typed deterministic C# contracts.
- REMOVE: global mutable state, arbitrary strings, DOM/localStorage coupling, hash/jitter inference, Port area, Istanbul hardcode, automatic bonuses/costs and parcel/slot truth.
- REFERENCE_ONLY: art, scene manifests, city names/geography and old planning documents for later content/art work.
