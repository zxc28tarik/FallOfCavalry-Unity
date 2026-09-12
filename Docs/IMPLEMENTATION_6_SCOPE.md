# Implementation 6 Scope Lock — Diplomacy / Envoy / Report / Communication

## Authority and base

- Authoritative base: `2eaf46a78ae9e580a4412e9e6cc3cd1a4458c283`.
- Current user instruction and transferred Parts 1–11 are binding operational authority. Part 7 is the detailed diplomacy/report authority.
- Implementation 0–5 semantics remain unchanged unless a minimal persistence integration is required.

## In scope

- Minimal typed Faction actor registry reusing the existing `FactionId`.
- Canonical Faction-pair diplomatic relations with qualitative state and separately stored explicit factors.
- Persistent diplomatic actions with typed kinds, role-scoped mandate, delivery requirement and single-resolution guards.
- Real-Character Envoy missions validated through active `OrganizationBranch.Diplomacy` assignments, with explicit non-instant travel phases and timestamps.
- Typed diplomatic messages with separate messenger/envoy carrier roles, dispatch/delivery chronology and duplicate-delivery guards.
- Persistent typed reports and observations with provenance, quality, uncertainty, `ObservedAt`, `DispatchedAt`, `ArrivedAt`, deterministic staleness and bounded delivered-information stores separate from world truth.
- Typed City institution source hooks without numeric effects.
- Save schema v7, v6→v7 migration, continuous migration tests, deterministic roundtrip, validation, Unity metadata and documentation.

## Out of scope

- Implementation 7, Army/Battle/Recruitment, War simulation, pathfinding, encounters, interception, spies, sabotage, assassination, deep intrigue, AI strategy, UI/art, modern permanent embassies and historical treaty database.
- Fake instant remote diplomacy, random misinformation, omniscient player/AI queries, automatic religious hostility, automatic trade/price effects and invented numeric success/travel/quality formulas.
- Full treaty, crisis, war, prisoner exchange, gift/treasury and bargaining simulation. Contracts remain future-compatible, but systems requiring unimplemented owners are not faked.

## Boundaries and public contracts

- Relation, factor, action, mission, message, report, observation and actor knowledge remain separate.
- Envoy is a normal Character plus an existing Diplomacy assignment and mission; Messenger is a separate carrier role.
- `DiplomaticActorState`, `DiplomaticRelationState`, `DiplomaticFactor`, `DiplomaticActionState`, `DiplomaticMandate`, `EnvoyMissionState`, `DiplomaticMessageState`, `ReportState`, `ReportObservation`, `ActorInformationState`, registries, validators and application services are introduced.
- Faction, House, Clique and Organization identities are not aliased. Religion difference produces no factor automatically.

## Determinism, save and tests

- All registries and child collections use typed IDs/canonical keys and ordinal ordering. World timestamps are explicit; no wall clock or random source is used.
- Schema v7 persists actors, relations/factors, actions, missions, messages, reports and delivered knowledge. Migration preserves v6 state and initializes diplomacy empty.
- Gates: complete .NET regression/new tests, Unity 6000.3.16f1 import/compile/EditMode, final-SHA GitHub CI, exact remote SHA and clean worktree.

## Open authority topics

- Exact relation formulas/thresholds, factor decay, envoy travel speed, audience delay, Character-stat weighting, report quality/confidence computation, archive limits by importance, retry/loss rules, full agreement/crisis/war semantics and AI decision tolerances remain `TODO_BALANCE` or `TODO_DESIGN_AUTHORITY`.
