# Battle Deployment and Sectors

`BattleSector` is a typed tactical node, not a tile, Transform, or pixel coordinate. A `BattleSectorGraph` stores canonical nodes and explicit symmetric adjacency. Self, duplicate, and dangling references are rejected. Terrain values are descriptive hooks only.

Each `BattleDeployment` binds one participating Unit Group to its Side, Army, Sector, formation state, and reserve flag. One Unit Group cannot be deployed twice. Side eligibility and participation are checked before mutation. Deployment does not change campaign Army location.

Typed orders are Hold, Advance, MoveSector, Attack, Withdraw, and ChangeFormation. Side or participant commanders may issue orders only for their side. Movement requires an adjacent target; attacks require a hostile deployment target. UI strings are never Domain commands.

The proof fixture has two sides, two real Armies, four Unit Groups, three connected sectors, four deployments, explicit commanders, melee/ranged examples, and an armored mounted persistent Soldier. The generated Unity scene maps the three Domain sector IDs to presentation anchors but the coordinates have no gameplay authority.
