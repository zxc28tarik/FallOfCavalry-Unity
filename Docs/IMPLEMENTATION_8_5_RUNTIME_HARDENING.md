# Implementation 8.5 Runtime Visual Hardening

Authority base: `e957a5347600b3678b16313efe009aba052d5be7`.

## Confirmed pre-hardening lifecycle

`VisualSoldierPool.Rent` reused only the outer `VisualSoldier3D`. `VisualSoldier3DAssembler.Assemble` called `Instantiate` for every resolved module on every bind. The declared `BoundedSignatureCache<GameObject>` was not consumed by assembly. `VisualSoldierPool.Return -> ClearVisual` destroyed every child module. The benchmark's cached-consolidated choice was therefore not the normal runtime lifecycle.

## Hardened lifecycle

```text
outer view Rent
  -> deterministic plan/signature
  -> quality-tier runtime resolution
  -> signature variant cache lookup
  -> lease an idle complete hierarchy, or create on cold miss
  -> bind Soldier identity only on the outer view
  -> gameplay use
  -> reset transient Animator state and binding
  -> return complete hierarchy to bounded variant pool
  -> return outer view to its pool
```

Standard uses consolidated body/clothing/body armor plus rigid equipment and optional mount/harness. Narrative uses modular authoring parts. Crowd uses the one-renderer far proof. Mount, harness and rider remain separate hierarchy nodes.

## Ownership and safety

- One assembled hierarchy has at most one active lease; no active GameObject is shared between Soldiers.
- `SoldierId` exists in `VisualSoldierBindingState`, never in the reusable hierarchy.
- Cache keys describe visual configuration, not gameplay identity.
- Variant count and total idle-instance count are bounded.
- Deterministic FIFO eviction destroys idle instances. Active instances from an evicted entry are retired and destroyed only when returned.
- Explicit invalidation destroys idle instances and retires active ones. Catalog revision mismatch invalidates before the next assembly.
- Return resets Animator speed, root motion, triggers and parameters before pooling.
- Cache, metrics and pools are Presentation state and are not serialized.

## Scope boundary

No battle, deployment, combat, tactics, final art, city visuals or save-schema work is included. Save schema remains v9 and Domain remains UnityEngine-free.
