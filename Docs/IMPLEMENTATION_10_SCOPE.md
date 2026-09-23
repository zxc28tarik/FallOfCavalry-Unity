# Implementation 10 Scope — Encounter / Contract

## Locked scope

Implementation 10 adds two distinct production foundations: data-driven Encounter definitions/instances and data-driven Contract definitions/instances. It integrates them with existing Character, City, Economy/Caravan, Diplomacy/Report and Implementation 9 Battle contracts. It does not add Implementation 11 AI, a world-map/pathfinding system, final UI, or production content/balance tables.

## Authority lock

- Encounter families are exactly `Roaming` and `SurpriseEvent`.
- Roaming types are exactly Bandit, Caravan, Noble, Enemy Patrol and Refugee.
- Surprise types are exactly Abandoned Caravan, Epidemic, Road Collapse, Storm and Wounded Soldier.
- Contract categories are exactly Military, Diplomatic, Economic and InternalManagement.
- Agent/Informant, Village Delegation roaming encounter, Deserter, Duel and Night encounter are removed from production scope.
- No trigger rate, join percentage, toll/bribe amount, generic reward, penalty or duration was invented.

## Legacy classification

Browser/Phaser Encounter and quest runtime is `REFERENCE_ONLY`. Typed identity and broad encounter/quest intent are `ADAPT`; lifecycle, policy boundaries, evidence, persistence and validation are `REWRITE`. Global state, hidden randomness, string targets, removed encounter types and automatic numeric effects are `REMOVE`. No legacy runtime file is `KEEP`.

## Acceptance boundary

Schema v11, deterministic round-trips, v10 migration, explicit atomic mutation policies, real-system evidence and all Implementation 0–9 regression gates are in scope. Final encounter/contract content, production probabilities, rewards and success weights remain deferred authority.
