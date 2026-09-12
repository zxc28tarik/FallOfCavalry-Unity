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

## Implementation 5 addendum

Remote legacy refs `origin/main` and `origin/333` were audited for trade goods, production, stock, consumption, demand, prices, market transactions, merchants, caravans, cargo and capacity. Relevant sources include `js/constants/trade-goods.js`, `economy.js`, City market code and browser-era state managers.

- KEEP: none; no runtime contract meets the typed, deterministic and save-safe authority unchanged.
- ADAPT: the high-level real-goods flow and the distinction between City stock, production input/output and transported cargo.
- REWRITE: Trade Good identity/category, recipes, demand sources, pricing inputs, atomic transactions, ownership, Caravan cargo/capacity/accounting and schema v6 persistence.
- REMOVE: global mutable state, browser coupling, arbitrary string ownership, random price drift, hardcoded legacy prices/coefficients and fake inventory transfers.
- REFERENCE_ONLY: legacy numeric balance values, route/world movement, UI and content naming for later authority/content packages.

## Implementation 6 addendum

Remote refs `origin/main` and `origin/333` were audited for diplomacy, Faction relations, Envoys, messengers, reports, intelligence, communication, embassies, treaties, agreements, negotiation and reputation. Relevant material includes `DiplomacyManager.js`, `systems/diplomacy.js`, `systems/factions.js`, city-service UI, encounter/quest messenger data and City-overhaul Reisülküttab documents.

- KEEP: none; legacy runtime is global/browser-coupled and does not satisfy typed delayed-information authority.
- ADAPT: canonical Faction-pair intent, temporary messenger/Envoy concepts and Reisülküttab/record-office institution vocabulary.
- REWRITE: actor identity, relation/factor/action separation, mandate validation, physical mission phases, message/report timelines, actor information and schema v7 persistence.
- REMOVE: instant button diplomacy, single numeric relation truth, arbitrary string actors, purchased truce shortcuts, hidden numeric thresholds, direct global state mutation, omniscient information and random/uncontrolled effects.
- REFERENCE_ONLY: legacy action costs, thresholds, war/encounter logic, quest content, UI and historical presentation for later authorized packages.

## Implementation 7 addendum

Remote refs `origin/main` and `origin/333` were audited for Army, recruitment, troop, soldier, garrison, militia, levy, mercenary, Timar/Cebelü, supply, logistics, food, fodder, ammunition, salary, morale, fatigue and discipline. Relevant material includes browser state/managers, recruit UI/checklists and the Phaser Battle adapter/runtime.

- KEEP: none; no browser/global Army runtime qualifies as typed persistent production truth.
- ADAPT: broad Army/UnitGroup vocabulary, finite recruitment-pool intent and future campaign-to-battle reconciliation boundary.
- REWRITE: typed identities, ownership/control, real-Character command, finite recruitment conservation, goods inventory, atomic supply transfer, payroll/arrears and schema v8 persistence.
- REMOVE: global mutable Army truth, string-generated soldiers, instant/free recruitment, browser/localStorage authority, fake supply, silent infinite ammunition and direct morale hacks.
- REFERENCE_ONLY: Phaser Battle code, numeric recruitment costs/wages/pool refreshes, combat balance, formations, visuals and unit content for later authorized packages.
