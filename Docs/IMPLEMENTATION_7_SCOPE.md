# Implementation 7 Scope — Army / Recruitment / Logistics

Authority: the latest user package for Implementation 7, Parts 1–11 (especially Part 8 and the Part 10 roadmap), then current repository documentation. Base SHA: `8266a65da1a0f2543e8f3d8d1d4899f7d0e78ebe`.

## In scope

- Persistent, typed `ArmyState` and `UnitGroupState` aggregates with deterministic registries and canonical ordering.
- Typed ownership/control, a real Character commander, non-circular command relationships, physical location and pre-battle lifecycle.
- Explicit recruitment-source records for Retinue, Timar/Service-Based, Central Salaried, Garrison, Local Levy, Mercenary and Irregular sources.
- Recruitment requests that consume finite source availability and create or reinforce real Unit Groups without hidden troops.
- City Military-area/institution and Organization Army-assignment validation hooks where a source requires them.
- Persistent Army goods inventory using Implementation 5 `TradeGoodId` and real atomic transfers from City stock or Caravan cargo.
- Separate food, fodder and ammunition requirement records; no invented consumption rates or supply thresholds.
- Persistent payroll obligations, payments and arrears, with real typed funding-source references and no automatic morale/loyalty effects.
- Separate qualitative morale, fatigue and discipline state, plus a source-preserving readiness projection.
- Campaign integration, invariants, schema v8, v7→v8 migration, deterministic roundtrip and full regression coverage.

## Out of scope

- Implementation 8 SoldierInstance, weapons, armor, horses, individual loadouts, equipment rolls or visual soldiers.
- Battle, deployment, formations, sectors, damage, casualties, rout, siege, encounter and campaign battle reconciliation.
- World-map movement, coordinates, pathfinding, supply lines, convoy escort, ambush, AI strategy, final UI/art and historical army content.
- Invented recruitment/manpower/salary/consumption/capacity/morale/fatigue/discipline/desertion/movement formulas.

## Locked boundaries

- `OrganizationBranch.Army` is an organizational people-network boundary, not an Army entity.
- Recruitment source is not troop type. Army is not Unit Group. Owner is not commander.
- Clique is never a valid Army owner. Military and Religious Cliques create no troops, supplies, morale or combat modifiers.
- Timar/Dirlik is a service obligation, not ordinary private property or automatic House inheritance.
- Commander is a live, non-captive Character with an explicit Army command relationship; no duplicate commander subtype or stat.
- Supplies are real Trade Goods. Source stock decreases exactly when Army stock increases; failure is atomic.
- Salary, arrears, morale, loyalty, fatigue and discipline remain distinct truths.
- Foreign Army state is authoritative campaign state, not automatically available actor information; reports remain the information boundary.
- SaveData remains separate from RuntimeState; Domain remains UnityEngine-free and deterministic.

## Legacy classification

- KEEP: none; browser/global legacy Army truth is not production-authoritative.
- ADAPT: the broad Army/UnitGroup vocabulary, finite recruitment-pool intent and campaign-to-battle reconciliation intent.
- REWRITE: identities, ownership, command, recruitment conservation, goods inventory, payroll, logistics, lifecycle and persistence as typed deterministic C# domain state.
- REMOVE: global mutable Army truth, string-generated soldier IDs, instant/free recruitment, browser/localStorage authority, fake supply, silent infinite ammunition and direct morale hacks.
- REFERENCE_ONLY: Phaser battle/runtime code, legacy numeric costs/wages/pool refreshes, unit balance, formations, visuals and combat behavior for later authorized implementations.

## Acceptance

Implementation 7 closes only after schema v8 migration/roundtrip, all prior and new .NET tests, Unity 6000.3.16f1 import/compile/EditMode tests, final-SHA GitHub Actions, clean worktree and matching local/remote SHA all pass with no critical skip.
