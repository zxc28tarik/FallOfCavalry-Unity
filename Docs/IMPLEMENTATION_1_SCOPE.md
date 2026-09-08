# Implementation 1 — Character Core Scope Lock

## Authority and base

- Binding authority: the user's latest Implementation 1 instruction and Parts 1–11.
- Foundation base: `2f980cc7f87839572747435583bab95dd5e15ebb` (`Implementation 0: READY`).
- Feature branch: `codex/impl-1-character-core`.
- Standalone Master/P1–P15 files are not required for this package under the current user authority decision. If found later, they require a conflict audit.

## IN SCOPE

- Typed `CharacterId` and immutable identity definitions for `RegisteredPerson`, `NamedCharacter`, and `GeneratedPerson`.
- Mutable `CharacterState` kept separate from character definition data.
- Importance A/B/C/D without combat modifiers and adjacent promotion constraints without invented thresholds.
- Nine 0–100 core stats.
- Separate persistent Base Loyalty, Current Loyalty, Satisfaction, Base Reputation, and Current Standing values.
- Explicitly-created sparse, canonical character relations with deterministic ordering.
- One discriminated authoritative physical location: City, Army, Caravan, Travelling world position, or Captivity.
- Orthogonal injury, captivity, and terminal death state.
- Generated-to-Named(D) promotion preserving CharacterId and all existing state/history.
- Bounded deterministic character history.
- A/B story-guard policy hook that blocks arbitrary cheap random death without implementing story content.
- Character roster/application orchestration, invariant validation, save schema v2, v1-to-v2 migration, semantic round-trip, determinism and mutation-risk tests.
- Documentation, full .NET regression, Unity import/compile and EditMode tests, and CI on the final commit.

## OUT OF SCOPE

- Organization branches, memberships and assignments beyond no-op future compatibility.
- House, Household, marriage, inheritance, Clique, internal politics.
- Full Religion/Sect behavior.
- City, Trade, Diplomacy, Army, Soldier combat, Battle, Encounter, Contract, AI, final Character UI, and story content.
- Balance thresholds, fast RPG levelling, universal success formulas, or probability tuning.
- Full relation graph, separate Companion class, or separate story-relation score.
- Ransom, exchange, captivity diplomacy, succession, or cross-domain death cascades.

## DEPENDENCIES

- Implementation 0 typed-ID, deterministic RNG/time/order, validation, save/migration, atomic save, test and CI foundations.
- `FOC.Domain` remains UnityEngine-free.
- `SaveData != RuntimeState` and `Definition != State` remain mandatory.

## ACCEPTANCE CRITERIA

- All required Character contracts and semantics in the user instruction are implemented.
- Generated-to-Named(D) keeps the same object identity, stable ID, history, location, relation links, metrics, injury and captivity state.
- Invalid/default IDs, stats outside 0–100, relations outside -100–100, duplicate entities, impossible lifecycle/location combinations and active movement after death are rejected.
- Relations remain sparse and deterministic; no all-pairs creation occurs.
- A/B arbitrary-random death is denied through a policy hook; deterministic non-cheap outcomes remain policy-controlled.
- Schema v2 stores all persistent Character Core state; v1 loads through an explicit deterministic migration; semantic round-trip is complete.
- Mutation-risk, architecture, migration, integration and Implementation 0 regression tests pass with zero critical skips.
- Unity 6000.3.16f1 import/compile and EditMode tests pass.
- GitHub Actions passes on the exact final SHA, remote SHA matches, and the final worktree is clean.

## Legacy audit

Audited remote legacy refs `origin/main` and `origin/333`, especially `js/entities/player.js` and `js/core/state.js`.

- KEEP: none. No legacy Character Core contract is compatible with the current architecture.
- ADAPT: the conceptual single current/target position transition, rewritten as an authoritative discriminated Character location.
- REWRITE: player identity, mutable state, serialization, movement and renown concepts using typed IDs, Definition/State separation, deterministic rules and versioned SaveData.
- REMOVE: browser/DOM/camera coupling, direct public mutation, global `GameState` singleton, `Date.now`/performance gameplay timing, monolithic player/army/inventory state and local JSON-object serialization.
- REFERENCE_ONLY: legacy naming, map coordinates and visual/player-flow behavior; battle-soldier death code is not Character death authority.

