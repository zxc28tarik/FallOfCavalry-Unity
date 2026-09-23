# Screen navigation

Top level: Map, City, Character, Organization, Trade, Army, Diplomacy and Battle. Reports and Ledger are persistent utility routes. Encounter/Contract and Archive are registered contextual routes.

`PresentationRoute` pairs a screen with an optional typed `PresentationEntityRef`. `PresentationNavigator` owns deterministic back/forward history and ignores duplicate current routes. Links such as City → Character, Army → Commander, Report → subject and Battle → Unit Group carry typed identity instead of arbitrary UI strings.

`PresentationShellViewModel` owns one navigator subscription and unsubscribes on dispose. Reopening a screen cannot multiply command handlers. Escape invokes Back when history exists. The composition root supplies screen read models and command bindings; it does not manually wire every button or UIDocument.

Stale or removed entity identity resolves to an explicit unavailable read model. Application validation remains final when state changes between navigation and action.

