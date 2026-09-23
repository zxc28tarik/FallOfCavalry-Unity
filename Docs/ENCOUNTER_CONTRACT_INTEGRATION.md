# Encounter / Contract Integration

## Cross-system flow

```text
Encounter choice → deterministic resolution → atomic effect policy
                                      ├─> existing BattleCreationService
                                      └─> Contract offer with typed target bindings

BattleResult / resolved Encounter / real Trade / delivered Report / City or Character state
                                      └─> typed ContractEvidence → objective progress
```

An Encounter may offer one linked Contract only after its one-time resolution is applied. A Contract may bind a linked Encounter target and accept `EncounterResolved` evidence. Duplicate IDs, duplicate evidence and repeated terminal effect application fail explicitly, preventing recursive or double-application behavior.

Battle evidence reads a completed Implementation 9 result and never mutates it. Diplomatic evidence requires delivered `ActorInformation`, so world truth is not automatically actor knowledge. Economic evidence requires actual Caravan accounting or arrival state. City/Character evidence reads the existing aggregates; no parallel state is introduced. Clique remains only a typed target/issuer boundary and gains no Army, Soldier or goods ownership.
