# Implementation 2 — Organization + House + Clique Scope Lock

## Authority and base

- Binding authority: the user's latest Implementation 2 instruction and Parts 1–11.
- Character Core base: `4673d900bcb0b0271beb23d965b74a4de3ef2a4b` (`Implementation 1: READY`).
- Feature branch: `codex/impl-2-organization-house-clique`.
- Parts 1–11 are the binding operational authority. A later standalone Master/P1–P15 discovery requires a conflict audit; the newest explicit user decision wins.

## IN SCOPE

- Typed, stable Organization, Assignment, House and Clique identities.
- Organization branches: Party, Household, Army, Settlements, Estates, Production, Trade and Diplomacy only.
- Memberships separate from assignments, loyalty, location and authority; all seven prescribed membership types and multi-branch membership.
- Typed assignment targets, five prescribed authority levels, remote-capable/physical-presence/deputy foundations and deterministic multi-assignment validation.
- Limited biological/marriage family links; Household distinct from Family and House.
- Persistent House identity, membership, lifecycle, head/succession-pending state, prestige, separate house wealth, typed property references, marriage and asset-category-driven inheritance.
- Timar/Dirlik and state-office non-private-inheritance guards plus future regrant hooks.
- Seven prescribed Clique types, optional leader, multi-membership, at most parent-to-subclique hierarchy, lifecycle and source-driven influence/attitude foundations.
- Clique ownership and combat-bonus prohibition contracts.
- Derived internal-political reaction projection and Power + Cause rebellion-readiness foundations without hidden resources.
- Application orchestration, invariant validation, save schema v3, v2-to-v3 migration, preserved v1-to-v2-to-v3 chain, semantic round-trip, determinism and mutation-risk tests.
- Documentation, full .NET regression, Unity import/compile and EditMode tests, and CI on the final commit.

## OUT OF SCOPE

- Implementation 3 Religion/Sect identity, policy or gameplay semantics. Religious Clique remains only a social network category.
- City simulation, Trade simulation, Diplomacy gameplay, Army/Soldier ownership or combat, Estate/Production gameplay, Battle, Encounter, Contract and AI behavior.
- Full genealogy, automatic marriage alliances, succession content, full inheritance economy, title/office transfer or Timar/Dirlik allocation.
- Full rebellion, revolt thresholds, conspiracy, assassination, sabotage, secret agendas or deep intrigue.
- Communication simulation, manager/deputy autonomous AI, final political UI or balance tuning.
- New top-level SpecialAgents, CityAdministration, Caravan, Retinue or Ocak branches; Ocak is not a Clique.
- Clique-owned goods, soldiers or armies; Clique combat bonuses or invented political resources.

## DEPENDENCIES

- Implementation 1 Character Core and Implementation 0 foundations remain intact.
- `FOC.Domain` remains UnityEngine-free.
- `SaveData != RuntimeState`, `Definition != State`, deterministic RNG/time/order and atomic-save invariants remain mandatory.
- Character lifecycle and authoritative location are the source for assignment feasibility; no parallel person/location model is introduced.

## ACCEPTANCE CRITERIA

- Membership, assignment, loyalty, location and authority remain distinct contracts and persisted values.
- Multiple memberships and assignments are supported; two active physical-presence assignments at different targets are rejected deterministically.
- Dead characters cannot retain active assignments; captive characters cannot perform forbidden physical-presence assignments.
- House, Household, Family, Clique and Faction remain distinct concepts.
- A House head is a living active member or the House is explicitly succession-pending; historical identity is not hard-deleted.
- Personal wealth, House wealth and state treasury are not conflated.
- Succession and inheritance are separate; StateOffice and TimarDirlik never pass through ordinary automatic inheritance.
- Marriage does not create an automatic alliance.
- Clique hierarchy is at most two exposed levels, leader is optional and all referenced members/leaders are valid.
- Clique influence is derived from explicit member/resource sources; no Clique owns goods, soldiers or armies and no combat modifier is exposed.
- Rebellion readiness requires both Power and Cause and does not execute rebellion gameplay.
- Schema v3 stores all persistent Organization/House/Clique state and CharacterId references; old v1 and v2 fixtures load through explicit deterministic migrations without inventing social entities.
- Mandatory invariant, mutation-risk, architecture, migration, round-trip, determinism, integration and prior-implementation regression tests pass with zero critical skips.
- Unity 6000.3.16f1 import/compile and EditMode tests pass.
- GitHub Actions passes on the exact final SHA, remote SHA matches, and the final worktree is clean.

## Legacy audit

Audited remote legacy refs `origin/main` and `origin/333`, including `js/core/state.js`, `js/entities/ai-party.js`, `js/managers/WorldPartyManager.js`, `js/systems/factions.js`, `js/systems/faction-wars.js`, quest/faction constants, settlement owner data and related smoke tests.

- KEEP: none. No legacy Organization, House, Clique, membership, assignment, authority, succession or inheritance contract satisfies the current typed domain architecture.
- ADAPT: only the high-level facts that parties can have affiliation, world entities may refer to an owner, and named factions exist. Any future use must pass through typed IDs and package-specific authority.
- REWRITE: party affiliation, faction relations, ownership references, manager state, save representation and world-party identity as separated typed Definition/State contracts when their later implementation package becomes active.
- REMOVE: global mutable `GameState`, direct public array/object mutation, arbitrary string IDs/owners, browser/event/camera coupling, `Date.now`/performance-derived gameplay behavior, faction doctrine bonuses in social contracts and monolithic save blobs.
- REFERENCE_ONLY: faction names/colors/art, settlement owner map, guild/quest vocabulary, AI party movement, battle deployment assignments and faction-war behavior. These are visual/content or later-package references, not Implementation 2 authority.

