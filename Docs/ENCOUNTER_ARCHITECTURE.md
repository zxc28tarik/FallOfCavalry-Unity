# Encounter Architecture

`EncounterDefinition` is immutable content; `EncounterState` is persistent campaign state. Typed IDs distinguish definitions, instances, choices, outcomes and policies. A definition declares an authoritative family/type, ordered choices, capabilities and a content key. It does not contain a hardcoded world entity or numeric probability.

The lifecycle is `Available → Engaged → Resolved`, with explicit cancellation before resolution. `EncounterService` validates typed context references and an injected eligibility policy. Resolution reconstructs the encounter's captured deterministic RNG, validates the selected choice/policy and continuation state, then invokes an atomic preflight/apply boundary exactly once.

`EncounterContext` separates source from ordered participants and supports existing Character, Army, Caravan, City, Faction, Region and Battle identities. `IEncounterPresenceProvider` is the future world-map boundary; Implementation 10 does not invent movement/pathfinding.

Bandit proof content exposes recruitment, Battle and Contract capabilities without a join percentage. Caravan proof content exposes toll/bribe-compatible choices without a money formula. Battle creation is optional and delegates to the existing `BattleCreationService`; Encounter owns no parallel combat model.
