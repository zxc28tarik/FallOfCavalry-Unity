# Implementation 9 — Battle / Deploy / Sectors Scope

## Authority and base

Implementation 9 starts from `codex/impl-8-5-visual-runtime-hardening` at `ee45cc0230fdbaf42580111f6d318502e74a894a`. The Parts 1–11 transfer package, the accepted Implementation 0–8.5 contracts, and the user's Implementation 9 package are binding. The browser/Phaser repository is reference-only.

## Locked scope

- A UnityEngine-free Battle domain with typed Battle, Side, Sector, Deployment Group, Order, and Event identities.
- Controlled Preparing → Deployment → Active → Resolving → Completed lifecycle, with terminal Aborted support.
- Explicit campaign snapshots of real Armies, Unit Groups, persistent active Soldiers, exact persistent equipment IDs, morale, fatigue, discipline, Army supply, campaign timestamp, and deterministic RNG state.
- Aggregate non-instantiated strength remains count-based. It never creates fake Soldier identity.
- Multi-side participants, deterministic sector graphs, deployment, formations as organization state, typed orders, capability-based attack intents, explicit ammunition consumption policy, armor policy boundary, and resolver boundary.
- Typed BattleResult, identity-preserving casualty outcomes, explicit Character injury/death/captivity integration, atomic preflight reconciliation, and duplicate-application protection.
- Save schema v10, v9→v10 migration, active-battle/RNG continuation, canonical ordering, hardcoded v9 compatibility fixture, and no presentation serialization.
- Existing VisualSoldier3D pipeline binding and an automated battle proof/benchmark scene. Visual layout is presentation-only.

## Out of scope

Implementation 10, encounters/contracts, full tactical/strategic AI, production combat balance, invented damage/protection/hit/terrain/formation/morale numbers, final UI/animation/VFX/audio, siege rules, naval battle, loot economy, ransom/exchange, world pathfinding, and final art are excluded.

## Authority audit

### KEEP

- `ArmyState`, typed owner/controller/real Character commander, `UnitGroupState`, finite recruitment records, real TradeGood supply, payroll, and separate morale/fatigue/discipline.
- Persistent `SoldierInstance`, immutable identity/provenance, `SoldierLoadout`, EquipmentInstance identity, WeaponDefinition capabilities, AttackMode/DamageType separation, armor/shield/mount/auxiliary definitions.
- WorldClock, SeededRandomSource/RandomState, Character injury/death/captivity rules, ActorInformation/report boundary, deterministic save/migration conventions, and the Implementation 8.5 visual catalog/assembler/cache/pool.

### ADAPT

- `SoldierCombatSnapshot` becomes a source for a battle-owned immutable snapshot, never mutable campaign truth.
- Unit Group aggregate headcount becomes `CampaignHeadcount` plus explicit `AggregateNonInstantiatedStrength` in battle snapshots.
- Legacy sector/deployment/order vocabulary informs typed contracts only.
- Character casualty services are invoked by reconciliation; no parallel death or captivity system is created.

### REWRITE

- Browser-era battle lifecycle, deployment, sector state, formation/controller, unit/combatant state, combat events, ranged handling, casualty aftermath, and campaign reconciliation are rewritten as deterministic C# Domain/Application contracts.
- Presentation binding is rewritten as a consumer of Battle state and the existing Unity 3D soldier pipeline.

### REMOVE

- Battle-start Soldier generation, battle-start equipment generation/reroll, global or Unity random, direct campaign mutation during battle, infinite ammunition, troop-name combat branching, implicit numeric bonuses, physics/collider gameplay truth, visual ownership of state, automatic Soldier deletion, and omniscient result publication.

### REFERENCE_ONLY

- `legacy-origin/main` browser files under `js/battle`, `js/systems/battle-*`, `js/systems/combat-rules.js`, battle UI/rendering, sprites, numeric tuning, AI heuristics, and legacy tests/assets.

## Critical distinctions

`Campaign Army State != Battle State`; `UnitGroup Campaign State != Battle Unit State`; `SoldierInstance != Battle Combatant State`; `Persistent SoldierLoadout != Battle-generated Loadout`; `BattleSector != UnitGroup/Transform/tile`; `Battle Order != Campaign Command`; `BattleResult != Campaign State`; `Battle Simulation != Unity Animation`; `Casualty Outcome != automatic deletion`.

## Acceptance lock

Implementation 9 is READY only after .NET, Unity import/compile/EditMode, visual pipeline, battle smoke, save v10 migration/roundtrip/determinism, exact final-SHA CI, remote-SHA, and clean-worktree evidence all pass with zero critical skips. Implementation 10 must not begin.
