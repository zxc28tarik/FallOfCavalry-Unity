# Contract Architecture

`ContractDefinition` is immutable content; `ContractState` is persistent campaign state. Definitions declare one of exactly four categories, typed semantic target slots, objective/evidence requirements, content keys and an optional resolution-profile reference. Instances bind those slots to actual typed IDs and separately retain issuer, optional responsible Character, deadline and cross-system links.

The lifecycle is `Offered → Active → Completed|Failed`, with cancellation as a distinct terminal result. A deadline exists only when content explicitly supplies it; there is no global expiry rule. Completion requires all objectives, an atomic outcome-policy preflight and a one-time applied marker. Rewards and failure penalties require explicit policies; none are implicit.

Progress is event/evidence driven rather than a per-frame omniscient scan. Supported evidence is verified against real Battle, Encounter, Trade/Caravan, delivered Diplomatic information, City and Character state. `ContractResolutionProfile` carries explicit per-content stat weights. No universal task-success formula or production weight is defined.
