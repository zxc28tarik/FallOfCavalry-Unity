# Implementation 8 Scope — Soldier / Equipment / Weapon / Armor

Authority: the latest explicit Implementation 8 package, Parts 1–11 (especially Part 8 and the Part 10 roadmap), then current repository documentation. Base SHA: `efe237d677fd984c211a1cd72185c889b6f052ed`.

## In scope

- Persistent typed Soldier identities and an explicitly instantiated Soldier roster associated with authoritative Implementation 7 Unit Groups.
- Immutable, typed Troop, Weapon, Armor, Shield and Mount content definitions in deterministic registries.
- Persistent equipment instances with real ownership, acquisition provenance, typed weapon/armor/shield/mount distinctions and deterministic ordering.
- Acquisition-time persistent loadouts supporting Main, Secondary, Sidearm, Throwing, Ammo and Special weapon slots plus a separate Shield slot.
- Typed Cut/Pierce/Blunt damage types and separate Thrust/Slash/Crush attack modes, multi-mode weapons, ammunition-family compatibility and future ranged/firearm hooks without combat numbers.
- Controlled armor layers for Inner, Body, Extra, Head and Limbs; separate Shield and MountArmor equipment; historically meaningful qualitative quality without RPG rarity.
- Mounted-Soldier compatibility and persistent Mount equipment state without breeding, genetics, individual hunger or speed formulas.
- Recruitment provenance linked to real Implementation 7 recruitment records/sources and Unit Groups; source kind creates no automatic combat modifier.
- Atomic City-stock-to-equipment acquisition through explicit Weapon/Armor/Shield/Mount definition-to-TradeGood mappings. Definition identity and economic-good identity remain separate.
- Army ammunition/supply compatibility hooks that do not duplicate individual loadout or consume battle ammunition.
- Explicit equipment mutation service; queries and future Battle snapshot reads cannot generate or reroll loadout.
- Campaign integration, invariants, schema v9, v8→v9 migration, deterministic roundtrip and full regression coverage.

## Soldier identity and Unit Group semantics

- `SoldierInstance` is neither Character, Unit Group nor VisualSoldier and has a stable `SoldierId`.
- A Soldier belongs to exactly one valid Unit Group; its Army is derived through that Unit Group and is not duplicated on Soldier state.
- `UnitGroupState.Headcount` remains the authoritative total manpower count. The persistent Soldier roster is the explicitly instantiated, player-relevant subset, so its count may not exceed Unit Group headcount; migration does not expand aggregate manpower into fabricated individuals.
- Soldier carries a typed Troop definition, immutable recruitment provenance, persistent loadout, qualitative experience/training hooks and lifecycle status. Soldier-to-Character promotion is deferred.

## Loadout, equipment and visual boundary

- Loadout is selected only by an explicit acquisition/issue operation, stored as state and changed only by an explicit equipment mutation.
- Weapon slots are typed and compatibility is definition-driven. Armor cannot enter weapon slots; Shield and MountArmor remain distinct equipment kinds.
- Equipment definitions are immutable content; equipment instances and their ownership/assignment are mutable campaign state. Full definitions are rebuilt from the content package rather than duplicated into save data.
- Visual references are opaque content IDs only. Domain contains no Unity sprite, GameObject, Animator or fixed visual-layer count. Presentation-side VisualSoldier remains deferred.

## Determinism and economy

- Any loadout choice uses injected campaign `IRandomSource`; no global random source, wall clock or query-time draw is allowed.
- Candidate definitions are canonically ordered before a deterministic selection. Once saved, loadout is authoritative and is never regenerated.
- City equipment issue preflights every definition, mapping, stock quantity, Soldier/Unit Group/provenance reference and slot conflict before mutating. On success real City stock decreases exactly as persistent equipment ownership increases.
- Army supply remains aggregate Trade Goods. A weapon definition, equipment instance, Army ammunition stock and Soldier weapon slot are four separate truths.

## Out of scope

- Implementation 9 Deploy, Battle, sectors, formations, attack execution, combat AI, hit/damage/penetration, projectile physics, casualties, rout, retreat, siege and campaign reconciliation.
- Final VisualSoldier, sprites, animation, VFX, UI or presentation-layer equipment composition.
- Exact damage, defense, range, reload, accuracy, durability, weight penalty, cost, XP/training, loadout probability, ammunition-consumption or mount-speed values.
- Soldier-to-Character promotion, cohort expansion, equipment loot/loss, warehouse simulation, horse breeding and a large historical troop database.

## Historical capability fixtures

Generic content fixtures will prove the contracts can represent Deli, Humbaraci and Bostanci, while preserving Deli as unarmored mounted light/shock cavalry, Humbaraci as a humbara/firearm-capable unarmored specialist, and Bostanci as guard/security/imperial service rather than a hardcoded heavy-elite bonus. No troop-name conditional production code or culture combat modifier is permitted.

## Legacy classification

- KEEP: none; browser/Phaser Soldier or equipment state is not production-authoritative.
- ADAPT: broad multi-slot loadout vocabulary, active-versus-inactive weapon distinction, modular visual intent and persistent combat-personnel identity.
- REWRITE: stable identities, definition/state split, ownership, slot compatibility, deterministic acquisition, economic conservation and persistence as typed C# contracts.
- REMOVE: battle-start Soldier/loadout generation, string weapon IDs, mutable global loadout templates, infinite ammunition, visual sprite authority and hardcoded culture combat bonuses.
- REFERENCE_ONLY: legacy battle simulation, sprites/atlases, tactical orders, numeric combat/equipment values, formation logic and presentation code for later authorized implementations.

## Save and acceptance

- Schema v9 stores Soldier identity, Unit Group, Troop definition reference, recruitment provenance, lifecycle/training/experience hooks, equipment instances and exact persistent loadouts. `CampaignSaveV8ToV9Migration` initializes these collections empty without changing earlier domains.
- Implementation 8 closes only after a hardcoded v8 migration fixture, continuous v1→v9 migration, semantic v9 roundtrip, all prior/new .NET tests, Unity 6000.3.16f1 import/compile/EditMode tests, final-SHA GitHub Actions, matching remote SHA and a clean worktree pass with no critical skip.

## Authority TODO

- Exact combat and equipment balance values, equipment durability/condition mutation, individual ammunition consumption, loadout weighting, market conversion ratios beyond explicit one-unit content mappings, promotion, cohort expansion and battle reconciliation require later explicit authority or their owning implementation.
