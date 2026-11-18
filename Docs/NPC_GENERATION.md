# NPC generation — first Unity implementation slice

**Date:** 2026-11-18
**Scope:** `zxc28tarik/FallOfCavalry-Unity`, NOT web/JS.
**Status:** Engine-independent NPC creation core implemented on a feature PR; CI and Unity Editor acceptance must be reported separately.
**Decision provenance:** User asked for game-world character/NPC creation (explicit correction: *not player character*). The role weighting and 0–100 initialization values below are **ASSISTANT_FILLED — PROVISIONAL**, not approved permanent balance or demographic rules.

## Existing authority reused

- `CharacterId` stable identity; `GeneratedPerson` identity + `CharacterState` live facts.
- `CharacterStats` holds nine 0–100 stats; Loyalty, Satisfaction, Reputation and Standing remain distinct.
- Exactly one `CharacterLocation`; no free-floating secondary coordinates.
- `CharacterRoster` holds authoritative individuals and **explicit, sparse** relations. No pairwise graph is created.
- `GeneratedPerson` can later become `NamedCharacter(D)` **without cloning**, preserving the ID, history, location, stats and relation links.
- Existing save schema v9 already serializes the generated persons, their full stats, typed location and the Created history record. This task makes **no schema change** and therefore cannot require a fake migration.

## Added implementation

- `NpcNamePool`: immutable, data-driven culture-specific given/family name sets. Name sources belong to historical content review; the core does **not** assert that a particular person's culture implies religion, loyalty or faction.
- `NpcArchetype`: weighted role-context initialization with nine bounded stat biases. Archetype is **not** ownership, occupation appointment, military class or a permanent bonus.
- `NpcGenerationRequest`: collision-safe `npc-<batch>-<serial>` keys, unique role codes, strict limits (1–2048 NPCs per batch), explicit typed location and variance.
- `NpcGenerationService`: injected `IWorldClock` and `IRandomSource`, preflight ID collisions **before consuming any random draws**, deterministic weighted sampling of names/archetypes/stats, Created history, registered `GeneratedPerson` entries. It does not touch player setup, unit rosters, equipment, organization, house, cliques or diplomacy.
- `CharacterRoster.Contains`: stable ID existence query used to reject accidental repeat/replay duplicate creation.
- Tests: deterministic byte-for-byte saves and RNG, generated-versus-soldier separation, collision preflight, distinct ranges, profile biases, 0–100 invariant, save/load roundtrip, Created history and Generated-to-Named(D) identity continuity.

## Usage (Application layer)

```csharp
var names = new NpcNamePool(
    "ottoman",
    new[] { "Mehmed", "Ayşe" },
    new[] { "Çelebi", "Hatun" });
var roles = new[] {
    new NpcArchetype("artisan", 6, new int[9]),
    new NpcArchetype("trader", 4, new[] { 0, 4, 6, 0, 0, 14, 0, 0, 3 }),
};
var city = CharacterLocation.InCity(CityId.Create("city-example"));
var batch = new NpcGenerationRequest("city-example-wave-1", 1, 24, names, roles, city);
var result = new NpcGenerationService(campaign.Characters, campaign.Clock, campaign.Random)
    .Generate(batch);
```

Names above are examples only; population composition and historical name coverage require sourced content validation. The caller owns when and how many people to spawn. The generator does **not** invent entire cities' population or redefine demographic counts.

## Explicitly not yet implemented

- No player character designer. Wrong web PR #67 was **closed unmerged**.
- No NPC 2D/3D portraits, soldier sprites or visual asset pipeline changes.
- No automatic whole-world population scheduling or city demography rules.
- No permanent NPC birthday, gender, age, culture/role/trait/profile data fields in save v9. The archetype and culture appear in the Created-history audit summary, but this **is not an authoritative mutable profession or culture field**. Adding those facts requires an explicit versioned schema + migration and content design approval.
- No automatic family kinship, marriages, random loyalty networks, religious conversion, clique membership, military recruitment or official assignment. Those must use existing bounded domain command services and real entities, never hidden made-up owners.
- No role effects on combat/diplomacy/trade as permanent buffs. No conversion of importance to power.
- No NPC life-course/ageing, economic occupation simulation or low-importance aggregation yet.

## Acceptance

- [x] Implementation source and tests committed on dedicated Unity branch.
- [ ] CI on exact final commit.
- [ ] Unity 6000.3.16f1 import/compile/EditMode test result.
- [ ] Future: versioned persistent demographic profile / historical content catalog with explicit authority and migration.
- [ ] Future: bounded generation policy tied to real city and migration events, NPC lifecycle, trait/profession interactions.
