# Implementation 13 — Save Hardening / Full Integration / Performance scope

Base: `a0098ef6b95d85e360a8d31a60431d44a21a9b53`. Branch: `codex/impl-13-save-integration-performance`.

## Locked scope

Implementation 13 hardens the existing v12 persistence architecture, proves continuous v1→v12 migration, joins every completed foundation system in one deterministic `PROOF_ONLY` campaign, adds replay/continuation/long-run/performance evidence, and produces a real Windows x64 Development player with the existing Presentation shell and a minimal atomic save/load surface.

The package does not replace the save format, introduce v13, add gameplay content, invent balance, expand world simulation, or begin a later vertical slice. UI state remains ephemeral. Production gameplay truth remains in `CampaignRuntimeState`; `CampaignSaveData` remains the persistence representation.

## Acceptance

- current schema remains v12 and all docs/code/migrations agree;
- atomic write has same-directory temp, durable flush, reread/validation, replace and one backup;
- faults at every write boundary leave a valid committed generation;
- corruption, truncation, future versions, traversal, concurrency and recovery are explicit;
- v1→v12 migration is continuous and hardcoded fixtures do not invent newer state;
- one production-located proof campaign crosses Character, social, religion, city, economy, diplomacy, military, soldier/equipment, battle, encounter/contract, AI and Presentation;
- direct execution and save-boundary replay agree;
- short/medium/long deterministic runs survive periodic reloads and remain bounded;
- Unity 6000.3.16f1 imports/compiles and passes all EditMode tests;
- Windows x64 Development build boots, presents the proof campaign, saves, loads and exits cleanly under smoke automation;
- all prior pipelines, final .NET/Unity suites and final-SHA CI remain green.
