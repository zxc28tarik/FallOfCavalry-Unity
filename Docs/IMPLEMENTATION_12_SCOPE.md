# Implementation 12 — Presentation / UX scope

Base: `d794df1f98fe5d63c89a98b3cbb1bddf12c8884f`. Branch: `codex/impl-12-presentation-ux`.

## Locked scope

Implementation 12 supplies a production foundation for the desktop Presentation layer: engine-independent read models, explicit viewer knowledge, one-way view models, typed navigation, command adapters, a UI Toolkit shell, UXML/USS design system, virtualized lists, responsive desktop policy, validation, tests and measured automation.

The top-level shell registers Map, City, Character, Organization, Trade, Army, Diplomacy and Battle. Reports and Ledger are cross-cutting routes; Encounter/Contract, Archive and deeper entity views use the same route/read-model contracts. Every `ScreenPresentationState` carries Current, Trend, Why, Risks, Opportunities, Actions and Alerts. Unsupported information has an explicit availability/reason instead of fabricated values.

## Locked exclusions

No world geography, roads, pathfinding, gameplay permission, pricing input, historical trend, causal factor, reward, combat result or hidden information is invented. Final localization content, ornamental art, tutorial, final accessibility certification, long-run campaign hardening and Implementation 13 are excluded. Save schema remains v12; ephemeral tabs, selection, scroll and navigation do not enter campaign persistence.

## Acceptance

- UI Toolkit is the primary runtime UI.
- `FOC.Presentation.Core` has no Unity dependency; `FOC.Presentation.Unity` depends outward on Core and existing Visuals.
- Domain never references Presentation.
- state is read-only toward UI; mutation returns through Application services.
- foreign truth is projected only from the viewing actor's delivered reports.
- Unity 6000.3.16f1 imports, compiles and passes EditMode tests.
- existing pipelines and the new Presentation pipeline remain green on the final SHA.

