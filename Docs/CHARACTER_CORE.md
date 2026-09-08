# Character Core

## Identity and state

`CharacterId` is the stable identity of a person. `RegisteredPerson`, `GeneratedPerson`, and `NamedCharacter` are immutable identity definitions; they are not role classes. Mutable campaign facts live in `CharacterState`. Companion, commander, envoy, manager, membership and assignment are deliberately absent from Implementation 1.

Generated-to-Named promotion mutates the identity definition attached to the same `CharacterState`, keeps the same `CharacterId`, assigns initial importance D, and appends a promotion history record. The roster and its relation keys therefore remain valid without cloning or rewiring the person.

## Values and importance

The nine core stats and Character dynamic values use validated 0–100 domain values. Base Loyalty, Current Loyalty, Satisfaction, Base Reputation and Current Standing are separate properties. No final political formula or UI conversion is defined here.

Importance A/B/C/D controls persistence/story significance only. Adjacent D→C→B→A promotion is supported; thresholds are intentionally deferred. Importance never changes stats or supplies a combat modifier.

## Relations, location and lifecycle

Relations are stored once under a canonical ordered pair and exist only after an explicit `SetRelation` command. No all-pairs graph exists.

Every `CharacterState` holds exactly one `CharacterLocation`. The discriminated variants use typed City, Army and Caravan IDs or deterministic integer world coordinates. Captivity is a location variant carrying its captor and typed detention site, so it cannot coexist with a second authoritative place.

Alive/dead state, injury and captivity are orthogonal outcomes. Injury supports Light, Serious, Permanent and UnfitForDuty. Death is terminal, retains identity/history, and requires a stationary non-captive final location. A dead character cannot move, receive a new injury, be captured or return to active state.

## Story guard boundary

`ICharacterDeathPolicy` is the future story-system hook. The baseline `ImportanceStoryGuardDeathPolicy` deterministically rejects arbitrary-random death for A/B characters and returns a typed `InjuryOrCaptivity` preference to the future outcome resolver. It neither makes them universally immortal nor implements story content, probabilities or battle outcomes.

## Persistence

Schema v2 persists all Character Core semantics through dedicated SaveData DTOs. `CampaignSaveMapper` validates DTO invariants before reconstruction. The v1→v2 migration preserves foundation metadata and creates no fictional characters. Stable roster/relation/history ordering makes equivalent state serialize identically.
