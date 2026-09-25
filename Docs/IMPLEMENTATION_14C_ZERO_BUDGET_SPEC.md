# 14C zero-budget rescue — authoring specification

2026-09-25. This is a text-only reference board. No museum, auction, game or marketplace image is redistributed. References describe construction, not an invented standardized 1648 uniform. Later/ceremonial examples are explicitly limited.

## Historical direction

| Component/profile | Construction target and limitation | Research source |
|---|---|---|
| Hasan Ağa | Restrained layered coat, visible front closure/collar/cuffs, trousers, leather boots; mature distinct face. Project-authored character, not a historical likeness. Muted red-brown cloth, linen edges, iron mail from loadout. | [Smithsonian Ottoman dress exhibition](https://asia.si.edu/whats-on/exhibitions/style-and-status-imperial-costumes-from-ottoman-turkey/): elite court dress establishes layering/cut vocabulary, NOT ordinary military issue. |
| Sipahi | Mounted, armored silhouette with mail over garment, boots and appropriate helmet/head covering; no fantasy oversized shoulders. | Same clothing source; [Met Islamic arms catalogue](https://resources.metmuseum.org/resources/metpublications/pdf/Islamic_Arms_and_Armor_in_The_Metropolitan_Museum_of_Art.pdf), mail and plate construction. |
| Cebeli | Less elaborate retainer silhouette, different garment/headgear/equipment coverage, not recolored Sipahi. Role-specific equipment comes from persistent data. | Same source vocabulary; proposed differentiation is art direction, not a documented universal uniform. |
| Tüfekçi | Complete practical infantry clothing, cap/wrap, long firearm and ammunition gear, anatomically credible grip. | Clothing sources above; firearm source below. No unsupported modern-musket silhouette. |
| Mail | Fine repeated linked surface, slight relief, restrained iron response, weighted garment drape; no individually modeled rings. | Met catalogue above; historical provenance does not validate this draft's rendered quality. |
| Headgear | Distinct mounted/light/infantry silhouettes, plausible wrap/cap and metal helmet construction. Avoid ceremonial gold armor as field equipment. | [Met Turkish helmet](https://www.metmuseum.org/art/collection/search/22012); earlier object is construction evidence, not exact 1648 issue. |
| Kılıç | Curved blade, defined guard/grip/pommel, credible length and metal/organic separation. | [Met Turkish sword](https://www.metmuseum.org/art/collection/search/24320): sixteenth-century blade; later grip must not be mistaken for original evidence. |
| Mızrak | Long wooden shaft, separate socketed iron point, human hand scale and mounted clearance. | Original conservative reconstruction; no claim of recovered exact 1648 design. |
| Firearm | Shaped wooden stock, long iron barrel, bands, match mechanism, ramrod and guard. | [Christie's seventeenth-century Turkish matchlock](https://www.christies.com/en/lot/lot-4711733). Refurbished eighteenth/nineteenth-century butt is NOT evidence of exact 1648 stock form. |
| Kalkan | Convex cane/organic shield with iron boss, rim and rear straps. | [Met shield boss](https://www.metmuseum.org/art/collection/search/27336): this specific record is a boss, not a complete shield pattern. |
| Saddle/tack | Raised saddle structure, blanket, girth, complete bridle/reins/stirrups; seated pelvis and supported feet. | [Budapest mid-seventeenth-century saddle](https://collections.imm.hu/gyujtemeny/disznyereg/1722?npn=1): ceremonial artifact; simplified working tack is an explicitly inferred reconstruction. |

## Practical tool audit

- Blender 4.5.9 LTS portable is installed and is the primary authoring tool. Existing canonical rigs are retained.
- Rigify is bundled under Blender `4.5/scripts/addons_core/rigify`; not run. Regenerating the canonical skeleton would not solve garment cut/fit and is not this rescue's scope.
- Microsoft Rocketbox: repository metadata inspected, but no models or animations downloaded/imported. No claim of use or license clearance of individual assets. An alternate body is reserved, not authorized here.
- Local GPU: NVIDIA GeForce MX130, 2048 MiB; system RAM 8,415,539,200 bytes. Host Python lacks torch/bpy/trimesh/open3d. Blender has its own Python.
- [TRELLIS](https://github.com/microsoft/TRELLIS#-installation) requires at least 16 GB NVIDIA memory; [TRELLIS.2](https://github.com/microsoft/TRELLIS.2#-installation) requires at least 24 GB. This 2 GB machine is not a practical local pipeline. Neither installed or run; no paid remote compute.
- Mixamo/Meshy: no authenticated zero-cost service access established in available tools. Neither used; no account/authorization wait and no asset rights assumed.
- No image generation, bitmap retouching, paid API or purchased asset. Existing Windows player renders are the evidence source.

## Public source license decision

| Input | License evidence | Commercial / modification / derivatives | Attribution | Public source/GitHub redistribution |
|---|---|---|---|---|
| Existing hm08 anatomy, system skin/hair, horse | Versioned upstream provenance/licenses; preserve existing pinned files | CC0 assets: permitted | Not required by CC0; provenance retained | Permitted under CC0 |
| RehmanPolanski beard and moustache ONLY | Official bodyparts05 CC0 pack AND each `.mhclo` header `license CC0` | Permitted | Author recorded voluntarily | Permitted; preserve original files and provenance |
| WDG scruffy beard | Conflicting AGPL3 header | Not cleared for this workflow | N/A | Excluded, not imported or committed |
| Museum/auction references | Research only | No copied asset | URL and limitation recorded | No reference images copied |
| New Blender geometry/scripts | Original project adaptation of the above | Project output | Provenance in source metadata | No closed-source asset dependency |

Facial-hair archive SHA256: `262bba42246f85b2a91f493dd920296b258a3b4544eb495c91c4d08e57c528fd`; [official pack inventory](https://static.makehumancommunity.org/assets/assetpacks/bodyparts05.html). Recover only the two audited source folders and provenance from the stash. The source naming does not establish Ottoman historical authenticity.

## Method and acceptance order

Hasan only: source-derived connected shoulder/sleeve patch, actual boundary connectivity, constrained smoothing/subdivision, cloth layer clearance, solidified seam/opening/collar/cuff surfaces and preserved interpolated canonical skin weights. Inspect front, side and three-quarter in real Windows player before propagating. Failed ring/armhole module from stash is excluded. Hair cards replace stippled triangle beard only if visibly better.

Then, ONLY if Hasan passes: mounted Sipahi, Cebeli/Tüfekçi, tack and combined motion, all-four production activation, full assembled/mixed runtime benchmarks and Accepted images. Draft prefab measurements cannot substitute for runtime acceptance. Save v14 and production gameplay remain unchanged. No reserved base-character A/B this turn; no Implementation 15.
