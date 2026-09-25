# Historical slice source provenance — 14C

Authoring status: **Draft, NOT visually accepted** until the explicit 14C review passes.

## Anatomical source inputs

- Human: MakeHuman Community `hm08` base mesh and adult male morph targets, pinned upstream commit `a8bc2d54ff0ac92e78ff71431b1023eda42bf482`. Assets are CC0, **not** the separate AGPL application code. Source: https://github.com/makehumancommunity/makehuman/tree/a8bc2d54ff0ac92e78ff71431b1023eda42bf482/makehuman/data . License: https://static.makehumancommunity.org/about/license.html . These are a generic anatomical foundation, not a claim of Ottoman ethnicity or likeness.
- Horse: Lyndon Daniels, rigged by ChadM, https://opengameart.org/content/rigged-horse and https://opengameart.org/content/realtime-ranchers-3d-model-pack . The source page specifies CC0. Download: https://opengameart.org/sites/default/files/riggedHorse.blend . Geometry and packed textures come from this source; no commercial game source is used. Scripts embedded in downloaded blends are disabled during import. Old rig requires repair/rebinding; source rig is not accepted merely because it is present.
- Tools: Blender 4.5.9 LTS portable, official blender.org distribution; ZIP SHA256 `41da973b9bf95bb312cbeff4d1982feb13259b43c821686b9bafea4dfe5477cf`. Unity 6000.3.16f1. Local source audit date: 2026-09-25.

## Historical design references (reference only, not texture/model extraction)

- Clothing: Smithsonian National Museum of Asian Art, *Style and Status: Imperial Costumes from Ottoman Turkey*, https://asia.si.edu/whats-on/exhibitions/style-and-status-imperial-costumes-from-ottoman-turkey/ . Sixteenth/seventeenth-century kaftan silhouette reference. Imperial luxury decoration is not evidence that ordinary troops wore imperial robes.
- Mail: Met *Islamic Arms and Armor*, mail-and-plate shirt 36.25.362, https://resources.metmuseum.org/resources/metpublications/pdf/Islamic_Arms_and_Armor_in_The_Metropolitan_Museum_of_Art.pdf . Earlier surviving objects inform construction; not proof of an exact 1648 uniform.
- Steel headgear: Met 22012, https://www.metmuseum.org/art/collection/search/22012 . Earlier Turkish/Turkman-style construction reference, not a precise 1648 issue designation.
- Ceremonial helmet exclusion: Met 26563, https://www.metmuseum.org/art/collection/search/26563 . Gilt copper/tombak is explicitly ceremonial; do not reinterpret its material as combat protection.
- Shield: Met 27336, https://www.metmuseum.org/art/collection/search/27336 . Wound/wrapped cane body with steel boss, 16th–17th century.
- Saddle: Museum of Applied Arts Budapest, mid-17th-century ceremonial saddle, https://collections.imm.hu/gyujtemeny/disznyereg/1722?npn=1 . Construction/silhouette reference only; use restrained working leather and cloth, not unearned jeweled ceremonial decoration.
- Matchlock: Cleveland Museum of Art 1916.828, https://www.clevelandart.org/art/1916.828 . **Later (c.1750)** Ottoman survivor: mechanism/material reference only, not sufficient on its own to assert a 1648 stock/lock pattern. Period-specific confirmation remains an art-research gate.
- Sabre: Met 23991, https://www.metmuseum.org/art/collection/search/23991 . Turkish mounts/scabbard with a European blade, dated late seventeenth century in the curatorial discussion. Useful material/construction context, **not evidence for a standardized 1648 kilic profile**. The original restrained blade draft is an interpretation requiring period-specific review.

No JS/Phaser/web soldier or horse asset is an authoring input. No Bannerlord mesh, animation or texture is an input. There is no external AI 3D service/account dependency. DCC source assessment renders are not Windows-player acceptance screenshots.

## Continuation research and repairs — 2026-09-25

- CC0 skin and hair-card sources: see `Upstream/SystemAssets/PROVENANCE.md` for the official package URL, exact archive hash, selected assets and license. The previous flat skin and inflated beard shell are not the finished target. New short facial fibers are original deterministic surface authoring, not extracted photographs.
- Additional matchlock reference: Christie's direct object catalogue, 17th-century Turkish matchlock, https://www.christies.com/en/lot/lot-4711733 . Its documented 108 cm barrel, full wooden stock, retaining bands, pan and iron ramrod inform the new construction study. The catalogue explicitly says the butt was refurbished in the eighteenth/nineteenth century; that replacement and lavish decoration are **not** evidence for an ordinary 1648 issue weapon. The model is an original restrained interpretation, not a scan or exact replica. Historical acceptance remains pending.
- Additional sabre construction reference: Met 36.25.1297, https://www.metmuseum.org/art/collection/search/24320 , Turkish blade dated 1522–66, blade length 78.1 cm. The curator identifies the grip as a later replacement. This establishes an earlier curved-blade precedent, not a definitive 1648 grip or standardized troop weapon. No museum image was extracted as a texture.
- Repairs: continuous anatomical shoulder/armpit mesh, waist-connected vented skirt, tailored closed-toe boots, metric mail UVs, original tiling surface normals; separate firearm components and shield rear straps. All remain **Draft** pending assembled, animated Windows-player review.
