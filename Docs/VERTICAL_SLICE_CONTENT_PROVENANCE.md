# Vertical-slice content provenance

Authoritative machine-readable content is `Assets/FOC/Content/Resources/FOC/HistoricalSlice/historical-slice-content.txt`, version `VERTICAL_SLICE_HISTORICAL-1648-09-01-r1`. Records are pipe-delimited and validated during load.

- `META|version|anchorDate`
- `SOURCE|id|description`
- `CHAR|id|name|truth|role|importance|city|source|confidence|statsTruth|nineStats`
- `CITY|id|name|source|truth|buildings|infrastructure|metricsTruth`
- `GOOD|id|name|category|weight|food|military|luxury|value|source|existenceTruth|numericTruth|note`
- `RECIPE|id|name|building|inputs|outputs|source|truth|numericTruth`
- `STOCK|city|good|quantity|demand|numericTruth`

Truth classes:

- `HistoricalAttested`: source directly supports identity or broad historical existence.
- `HistoricalReconstruction`: period-compatible composition inferred from sources; not direct enumeration for the exact day.
- `SliceFiction`: invented actor/content for the playable slice, explicitly disclosed.
- `SliceTuning`: gameplay number, weight, price, quantity, stat or starting-state choice.

`HistoricalSliceContentValidator` rejects missing/duplicate references and non-tuning numeric fields. `HistoricalCampaignValidator` then applies all cross-domain invariants, exact package counts, map/location resolution, story-scope exclusion, save v14 validation and forbidden proof-ID scanning.
