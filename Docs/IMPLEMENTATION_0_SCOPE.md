# Implementation 0 Scope Lock

Locked on 2026-09-08 before implementation work.

## Authority order used

1. User's current instruction: implement only Implementation 0 and report honestly.
2. FOC Master Design Authority: no standalone file was found in the transfer archive, current repository, or audited legacy repository.
3. Final P1–P15 documents and current Decision Ledger: no standalone copies were found in the inspected locations.
4. Transfer Package Parts 1–11, preserved in the desktop transfer archive, are the available implementation authority.
5. Legacy JS/Phaser code is reference only and never overrides the transfer package.

Absence of a standalone Master file does not authorize invention of gameplay semantics. It is recorded as `TODO_DESIGN_AUTHORITY` and is non-blocking for the explicitly defined foundation package.

## In scope

- New Unity 6.3 LTS PC project baseline, separate from the legacy prototype.
- Domain, Application, Infrastructure, Presentation, Tests, Data, and Editor directories and assembly definitions.
- Pure C# Domain dependency guard.
- Typed stable ID pattern with invalid default/empty values and ordinal ordering.
- Explicit deterministic RNG with capturable/restorable state.
- Authoritative WorldClock and time value objects.
- Definition versus mutable state contracts and deterministic definition registry.
- Minimal immutable domain message queue.
- Versioned CampaignSaveData skeleton, runtime mapping, migration pipeline, serializer, atomic store, backup recovery.
- Invariant validation contracts.
- Automated .NET/Unity EditMode-compatible test baseline.
- CI definition and architecture/determinism/save/testing documentation.
- Git feature branch and committed closing state.

## Out of scope

- Character, organization, house, clique, religion, city, economy, trade, diplomacy, army, soldier, equipment, battle, encounter, contract, AI, story, historical content, final UI, balance, Steam integration, and release features.
- Legacy browser/Phaser runtime migration.
- Final production save hardening and full campaign migrations (Implementation 13).
- Implementation 1 or any later gameplay package.

## Dependencies

None. This is the first technical package.

## Acceptance criteria

- Domain has no UnityEngine reference.
- Typed IDs, deterministic RNG, and deterministic WorldClock work under tests.
- Definition and runtime state are distinct from static content and SaveData.
- Versioned save skeleton, migration pipeline, atomic write/validate/replace/backup contract, and invariant framework exist and are tested.
- All available test suites pass, CI passes, no critical test is skipped, docs are current, final commit exists, and closing tree is clean.

