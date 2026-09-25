# Implementation 14A Performance

The focused benchmark records one authored graph load, 1/100/1,000 deterministic İstanbul→Edirne/Bursa path queries, 1,000 known-marker projections and 1,000 route projections. It validates output shape but deliberately defines no invented performance threshold. Exact final Release .NET timings are emitted by `HistoricalGeographyTravelTests` and reported with the package evidence. They are acceptance-machine wall-clock measurements, not production frame-time promises.

Final local Release pipeline evidence: graph load 2 ms; 1 path query 0 ms; 100 path queries 14 ms; 1,000 path queries 124 ms; 1,000 known-marker projections 56 ms; 1,000 route projections 33 ms.

The graph is intentionally small (12 vertices, 11 edges), sorted and allocation-bounded for the slice. Route rendering is a UI projection. No per-frame world scan, physics query or hidden RNG is used. Larger-world indexing, hierarchical pathfinding and route-cache policy are deferred until real scale evidence requires them.
