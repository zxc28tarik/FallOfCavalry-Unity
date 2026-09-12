# Implementation 3 — Religion / Sect Scope Lock

## Authority and base

- Binding authority: the user's latest Implementation 3 instruction and Parts 1–11.
- Social/political base: `0eadcfbbd8388756161833ccc9bfcd9f486a2526` (`Implementation 2: READY`).
- Feature branch: `codex/impl-3-religion-sect`.
- Parts 1–11 and the newest explicit user decision are binding. Superseded design or legacy browser behavior is not implementation authority.

## IN SCOPE

- Typed, stable `ReligionId` and `SectId` identities.
- Immutable, content-driven `ReligionDefinition` and `SectDefinition`; a Sect has its own identity and a typed parent Religion.
- Deterministically ordered Religion/Sect registry with duplicate, missing-parent and compatibility validation.
- Persistent `CharacterReligionState` keyed by real `CharacterId`, carrying Religion and optional Sect independently of loyalty, relations, membership, assignment, authority, location and faction allegiance.
- Controlled Character religion-state application service without conversion gameplay, probabilities or automatic side effects.
- Multi-component `ReligionProfile` with typed City/Faction/Region targets, relative presence rather than invented precise historical percentages, and canonical ordering.
- Religion policy foundation for recognition, tolerance/restriction/privilege, enforcement and local application without numeric gameplay modifiers.
- Typed association from existing `CliqueType.Religious` Cliques to a represented Religion and optional Sect; Religion and Clique identities remain separate and multiple Cliques may represent the same Religion.
- Context-only religion difference assessment proving that difference alone produces no automatic unrest, loyalty/relation penalty, rebellion cause, war cause or combat modifier.
- Save schema v4, explicit v3-to-v4 migration, preserved v1-to-v2-to-v3-to-v4 chain, hardcoded v1/v2/v3 compatibility and semantic round-trip.
- Invariant, mutation-risk, architecture, determinism, integration, regression, Unity and CI evidence.

## OUT OF SCOPE

- Implementation 4 City gameplay or later Economy, Trade, Caravan, Army, Soldier, Equipment, Battle, Diplomacy, AI, Encounter, Contract or UI packages.
- Full historical world population/content database or faction-to-religion hardcoded switches.
- Conversion events, missionaries, persecution simulation, religious wars, casus belli execution or rebellion execution.
- Exact tolerance/unrest/conversion/tax/privilege formulas or arbitrary numeric modifiers.
- Automatic unrest, loyalty loss, relation penalty, faction hostility, rebellion, war or combat bonus/penalty caused only by Religion/Sect difference.
- A second religious-group system, religious ownership, religious Clique goods/soldiers/Army, or weakening any Implementation 2 Clique invariant.
- Religious institutions as City state, deep religious intrigue, assassination, sabotage or scripted historical scenarios.

## DESIGN AUTHORITY INVARIANTS

- Religion != Sect != Religious Clique != Faction != House != Character.
- Religion/Sect != Organization membership, House/Household membership, Assignment, Authority, Location, Loyalty or Relation.
- Sect is optional. When present, its typed parent Religion must equal the holder/profile/policy/association Religion.
- Religion definitions own neither cultures nor factions and contain no country-specific behavior.
- Religious Clique is only a social network; it neither owns Religion nor creates military/economic/combat authority.
- Religion difference is context input only and never sufficient cause for unrest, betrayal, hostility, rebellion, war or combat modification.
- Definition != Runtime State, SaveData != RuntimeState, deterministic RNG/time/order, atomic save and all Implementation 0–2 invariants remain binding.

## PUBLIC CONTRACTS

- `ReligionId`, `SectId`, `FactionId`, `RegionId`
- `ReligionDefinition`, `SectDefinition`, `ReligionRegistry`
- `CharacterReligionState`, `CharacterReligionRegistry`
- `ReligionProfileTarget`, `ReligionProfile`, `ReligionProfileEntry`
- `ReligionPolicy`, `ReligionPolicyRule` and controlled policy enums
- `ReligiousCliqueAssociation`, association registry
- `ReligionDifferenceAssessment`, `ReligionInvariantValidator`
- `ReligionCommandService`

## SAVE IMPACT

- Persistent representation changes from schema v3 to v4.
- v4 stores Religion/Sect definitions by stable ID, Character religion assignments, profiles, policy rules and Religious Clique associations in deterministic order.
- `CampaignSaveV3ToV4Migration` must preserve all Character/Organization/House/Clique data and initialize Religion state empty; it must invent no Religion, Sect, profile, policy or association.
- The continuous v1→v2→v3→v4 chain and hardcoded old fixtures remain required.

## TEST GATES

- Identity/type separation, stable ordering, duplicate IDs and Sect parent compatibility.
- Optional Sect and invalid Character/Profile/Policy/Clique Sect pairing rejection.
- Religion change preserves Character identity, stats, loyalty, relations, Organization and Clique memberships.
- Religious Clique ownership/combat prohibitions and distinct identity regression.
- Religion/Sect difference alone has zero automatic political, diplomatic or combat effect.
- Multi-religion profile, duplicate-entry rejection and deterministic order.
- v1→v2→v3→v4, hardcoded v3 migration, full v4 semantic round-trip and old social-state preservation.
- Implementation 0–2 regression, `FOC.Domain` UnityEngine independence, Release build with zero warnings/errors, Unity 6000.3.16f1 import/compile/EditMode, final-SHA CI, clean worktree and matching remote SHA.

## Legacy audit

Audited `origin/main` and `origin/333` under `js`, `docs` and `tests` for religion, religious, faith, sect, confession, population faith, faction religion, tolerance and diplomacy-religion concepts. Exact-word searches found no Religion/Sect domain material; broad `sect` matches were battle `sector` identifiers.

- KEEP: none.
- ADAPT: none; no relevant behavioral contract exists.
- REWRITE: any future legacy faction/culture affiliation must be expressed through typed content definitions and contextual policy consumers.
- REMOVE: any later-discovered automatic religion hostility/bonus/penalty or arbitrary string identity would be rejected by current authority.
- REFERENCE_ONLY: legacy faction names and geography may inform later content research but provide no Religion/Sect gameplay truth.

