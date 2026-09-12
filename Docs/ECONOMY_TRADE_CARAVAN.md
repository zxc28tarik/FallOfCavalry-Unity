# Economy, Trade and Caravan Foundation

## Authoritative flow

Implementation 5 establishes `Production → City Stock → Consumption → Demand/Shortage → Price Quote → Transfer`. Goods are never implied: production consumes explicit recipe inputs and creates explicit outputs, consumption removes actual stock, and trade moves the same quantities between City stock and Caravan cargo.

## Goods and stock

Every good has a typed ID, category, positive integral unit weight and optional content-provided reference unit value. Quantities are non-negative integral values. City stock and Caravan cargo reject underflow, duplicate authority records and overflow. Exact content yields, production cadence, volume, spoilage and balance values remain design/content decisions.

## Demand and price

Demand is a collection of explicit source-tagged quantities. Availability is a deterministic comparison of total demand with authoritative stock. A price quote starts from a content reference unit value and applies explicitly supplied, source-tagged signed unit adjustments in canonical order. The foundation contains no hidden coefficient, random drift or automatic historical claim.

## Transactions

Purchase and sale operations preflight City identity, Caravan stage, stock/cargo, weight capacity, operational cash and accounting overflow. Only after every check passes do they transfer goods and integral money exactly once. Caravan operational cash is not silently aliased to Personal Wealth, House Wealth or State Treasury.

## Caravan boundaries

A Caravan is persistent and has identity, owner, real Character manager, origin/destination, weight capacity, cargo, operational cash, lifecycle/location stage, accounting and optional route/risk hooks. Owner may be Character, House or Organization. An optional representative must be a separate active Organization assignment in the existing Trade branch. Clique and Trade Network membership cannot own goods. This package does not simulate pathfinding, world travel, encounter resolution, tariffs, Army supply consumption, AI or presentation.
