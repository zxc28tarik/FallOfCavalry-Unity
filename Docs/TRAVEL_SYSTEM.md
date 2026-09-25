# Travel System

`TravelCommandService.Start` validates an actor's authoritative origin, resolves an explicit graph path and then moves Character/Army/Caravan into their existing travelling/in-transit lifecycle. `Advance` advances the one `WorldClock` and all active journeys deterministically. Arrival returns the actor to the destination City when linked, otherwise its fixed map position. Envoy and Messenger use `IExternalTravelActorPort`, retaining their own aggregate authority.

Journey state persists origin, destination, ordered routes, departure/advance times, segment index, integer elapsed segment ticks, lifecycle and arrival time. Segment duration is integer ceiling `(distance metres × 3600) / speed metres-per-hour`; one world tick is one second for this slice. Speeds are explicitly `SLICE_TUNING`: Character 5 km/h, Army 3 km/h, Caravan 3.5 km/h, Envoy 6.5 km/h and Messenger 7.5 km/h.

`ITravelProgressHook` provides immutable segment/location context for Encounter checks and Battle location creation. Hooks do not own time or progress and cannot teleport actors. `AITravelActionProvider` calls the same service and policy used by the player; AI difficulty has no speed input.
