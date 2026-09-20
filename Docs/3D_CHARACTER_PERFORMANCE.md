# 3D Character Performance Architecture

## Budget model

Authoring remains modular, but 10–15 independent skinned renderers per ordinary Soldier is not the production default. The default candidate is a cached/prebuilt consolidated body, clothing and body-armor renderer set with rigid weapons/shields and a separate mount. Visual signatures make variants reusable and the cache is bounded/rebuildable.

Catalog assets require LOD0, LOD1 and LOD2. LOD priorities remove face detail, tiny accessories and minor geometry before silhouette-critical body, headgear, weapon, shield and mount forms. A one-renderer crowd proof exists, but billboard/impostor adoption is deferred until animation and battle-camera artifacts are measured.

Shared materials are mandatory. MaterialPropertyBlock may later provide safe per-instance color/wear variation; normal assembly must not clone materials. Shader variants remain limited to skin, cloth/leather, metal, wood and hair/alpha needs.

`VisualSoldierPool` avoids steady Instantiate/Destroy churn and clears binding on return. Signature caches are bounded and save-independent. Animator culling plus near/medium/far update policy reduces off-screen and distant cost; gameplay never reads Animator state.

## Benchmark interpretation

The automated Editor benchmark measures warmed prefab assembly time, configured renderer count, unique shared materials and observed managed-memory delta. It compares modular, cached-consolidated and far representations plus infantry/cavalry and Narrative subsets. In `-nographics` it cannot measure GPU, render thread, real frame time, batches or visible LOD selection; these must remain explicitly unavailable rather than inferred.

Measured proof results select cached-consolidated as the standard production starting point because it halves configured renderer count against the modular comparison and improved the warmed 100-actor spawn result on the implementation machine. Far representation is much cheaper but is not authorized as an unconditional animated-Soldier replacement.

## Required future benchmark

Before final battle budgets, run a Development Player or instrumented standalone build at representative battle camera/resolution with production meshes/shaders/animations. Record CPU main/render thread, GPU timing, batches/draws, visible renderers, skinning cost, memory, spawn spikes and frame distribution for 1/50/100/250/500 and, if hardware permits, 750/1000 actors.
