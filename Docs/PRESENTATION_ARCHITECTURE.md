# Presentation architecture

```text
CampaignRuntimeState
  -> CampaignPresentationQueries + PresentationViewerContext
  -> immutable PresentationReadModel / ScreenPresentationState
  -> PresentationShellViewModel
  -> UI Toolkit UXML/USS/ListView

player click
  -> PresentationActionDescriptor
  -> PresentationCommandBindingRegistry
  -> typed Presentation command adapter
  -> existing Application service
  -> gameplay validation and mutation
  -> action result + explicit refresh
```

`FOC.Presentation.Core` contains typed entity links, availability/precision metadata, screen contracts, formatter/localizer ports, navigation, layout policy, stable filtering, virtualized windows, queries and command adapters. It references Domain/Application for projection/orchestration but no Unity assembly.

`FOC.Presentation.Unity` contains the UI Toolkit shell controller and zero-configuration `PresentationRuntimeHost`. The host loads the shell from `Resources`, creates `UIDocument`, navigator and view model, and requires the composition root to provide only `IPresentationScreenSource` plus optional localization/command dispatcher. Views never hold `CampaignRuntimeState` and never invoke Domain mutators.

## Data binding policy

Binding is one-way from immutable/read-only Presentation objects. Runtime code uses explicit event-driven refresh rather than per-frame world scans. Mutable Domain objects are not binding targets. Application services remain the final authority if the world changes between render and click.

## Command safety

Adapters translate intents to `TradeTransactionService`, `ArmySupplyService`, `ArmyCommandService`, `DiplomacyCommandService`, `EncounterService`, `ContractService` and `BattleOrderCommandService`. `PresentationActionGate` prevents a pending action from being applied twice. Expected validation and stale targets become player-safe rejection keys; unexpected failures retain the technical exception for logging.

## Visual integration

The context/preview boundary reuses `VisualSoldier3DAssembler`, its pool/cache/LOD and `BattleCombatantViewBinding`. No second Soldier or Battle renderer exists. Presentation never rerolls loadouts or mutates battle simulation.

