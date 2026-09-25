# World Geography Architecture

`WorldLocationDefinition` and `CityState` are separate identities. A location can link to a City, while waystations, crossings and ports need not be full City simulations. Stable IDs never depend on localized display names or historical aliases.

The authored equirectangular slice bounds are 26.4–30.1°E and 40.0–41.85°N. Input latitude/longitude uses integer microdegrees. Load converts it once to `MapPoint` millionths with integer arithmetic; pathfinding and UI placement consume `MapPoint`, never floating geographic truth.

Routes are explicit edges with endpoints, mode, integer metres, direction, source IDs, confidence and content status. The graph is sorted by typed IDs. Deterministic Dijkstra minimizes integer route distance; equal totals resolve by the ordinal sequence of route IDs. No implicit Euclidean adjacency is created.

The slice contains 12 locations and 11 routes across İstanbul, Thrace/Marmara and northwest Anatolia. The decorative PNG is not a coordinate source and may be replaced without changing simulation or saves.
