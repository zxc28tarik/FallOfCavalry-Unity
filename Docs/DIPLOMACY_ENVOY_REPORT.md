# Diplomacy, Envoy, Report and Communication Foundation

## Separate truths

Diplomatic relation, factor, action, agreement, Envoy mission, message, report and actor information are separate contracts. A qualitative relation state is not an action and does not silently aggregate every cause into one universal score. Factors retain explicit source, direction and occurrence time.

## Actors, authority and Envoys

Diplomacy reuses the existing typed `FactionId`; the minimal actor registry does not alias Faction with House, Clique or Organization. An Envoy is a normal Character referenced by an active assignment in the existing `OrganizationBranch.Diplomacy`. A mission has an explicit mandate and bounded authority scope. Deliver-only and negotiation mandates cannot conclude state-level actions beyond their authority; full conclusion requires an existing `FullAuthority` assignment.

## Physical communication

Outbound actions bind a real mission and typed message. Dispatch, travel arrival and delivery are distinct state transitions. Arrival and delivery require elapsed world time and application services accept only the current campaign `WorldClock` timestamp. Messenger and Envoy carriers remain distinct. No pathfinding, interception or travel-speed formula is invented.

## Reports and information

A report contains a real source, controlled subject/observation kinds, explicit precision, quality/detail and separate `ObservedAt`, `DispatchedAt` and `ArrivedAt`. Low quality does not randomize or falsify values. Staleness is exactly current world ticks minus observation ticks. An undelivered report cannot enter `ActorInformationState`; delivered information remains a dated observation and never overwrites City, Caravan, Character, Economy or future Army truth.

## Integration limits

City institution sources must resolve an active building carrying an existing report/diplomacy/record information tag; no numeric bonus is inferred. Religion or Sect difference creates no factor or hostility automatically. Diplomatic agreements expose controlled terms but do not mutate Economy without a future explicit integration rule. War, AI strategy, espionage, encounters, UI and final balancing remain outside Implementation 6.
