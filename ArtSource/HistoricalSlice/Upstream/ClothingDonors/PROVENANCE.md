# Operator-selected clothing donors — 2026-09-26

Status: licensed source intake and initial eleven-donor tests complete / DRAFT
experiments only. Historical transformation is incomplete; NOT production acceptance.
See `Docs/IMPLEMENTATION_14C_CLOTHING_DONOR_TRIAL.md` for exact test results and failures.
Operator decision supersedes the prior procedural garment rescue strategy: test ALL
eleven selected parts before making any new procedural tunic. No alternative asset
shopping, paid Source editions, new body A/B, or Implementation 15.

## Archive ledger

Archives are stored locally under `Artifacts/ArtInputs` (ignored). Selected original
files are retained here without edits. SHA256 values identify the downloaded bytes,
not a claim that upstream supplied a checksum.

| Archive | Exact upstream URL / download identity | SHA256 |
|---|---|---|
| quaternius_outfits_standard.zip | https://quaternius.itch.io/modular-character-outfits-fantasy — free **Modular Character Outfits - Fantasy[Standard].zip**, upload 16289385; public file endpoint https://quaternius.itch.io/modular-character-outfits-fantasy/file/16289385 | c3468b18871cc8c8f05ab14df7712baf22cb9f389cbd870babf130e595187f70 |
| pants01_cc0.zip | https://files.makehumancommunity.org/asset_packs/pants01/pants01_cc0.zip | e4e0ec60db34f279be291a83cfd7b342a7c5cf09bb7676682a5f39f4f6ac4ad9 |
| shoes01_cc0.zip | https://files.makehumancommunity.org/asset_packs/shoes01/shoes01_cc0.zip | ded3f70428505eabbf1f6d7b5f61196a7366ef20757103d276ad0ed336c35ada |
| suits02_cc0.zip | https://files.makehumancommunity.org/asset_packs/suits02/suits02_cc0.zip | 437f4d7ab92b698c1fb1047e7d62b22c11f195b2473e936118661dc8e6b7eb7a |

## Selected files and license evidence

Quaternius primary: https://quaternius.com/packs/modularcharacteroutfitsfantasy.html
Embedded `License_Standard.txt` explicitly states CC0 1.0 Universal. Only these
seven FBX files from `Modular Character Outfits - Fantasy[Standard]/Exports/FBX (Unity)/Modular Parts/`
are selected, stored in `Quaternius/`:

- Male_Peasant_Body.fbx
- Male_Peasant_Arms.fbx
- Male_Peasant_Legs.fbx
- Male_Ranger_Body.fbx
- Male_Ranger_Arms.fbx
- Male_Ranger_Legs.fbx
- Male_Ranger_Feet_Boots.fbx

MakeHuman official inventories:
https://static.makehumancommunity.org/assets/assetpacks/pants01.html
https://static.makehumancommunity.org/assets/assetpacks/shoes01.html
https://static.makehumancommunity.org/assets/assetpacks/suits02.html

Each selected `.mhclo` independently declares CC0 (boots spells it CC-0).
Original OBJ, mhclo, mhmat, thumbnail and referenced textures remain together:

| Folder under clothes/ | Selected geometry and binding file | Author evidence |
|---|---|---|
| toigo_harem_pants | pants_harem.obj; toigo_harem_pants.mhclo | MargaretToigo / embedded MRT |
| culturalibre_male_boots | male_boots.obj; culturalibre_male_boots.mhclo | culturalibre; header also credits original by Roachburn |
| donitz_monk_robe | Monks_Robe.obj; donitz_monk_robe.mhclo | Donitz |
| rehmanpolanski_viking_tunic | tunicviking.obj; rehmanpolanski_viking_tunic.mhclo | Rehman Polanski |

CC0 allows modification and redistribution; credit is retained for traceability.
These assets establish **geometry/deformation provenance only**. Fantasy, Viking,
monk styling and source textures do NOT establish Ottoman authenticity. No source
hood, fantasy pauldron, paid Source pack, or unrelated outfit is selected.

## Required transformation and gate

Fit existing FOC hm08 male body and canonical rig; retain useful donor topology,
UVs and weights; remove incompatible styling; reshape to the researched historical
direction in `Docs/IMPLEMENTATION_14C_ZERO_BUDGET_SPEC.md`; author appropriate
materials; test standing/animation/mounted deformation and real Unity runtime.
Source import, numerical validity, and diagnostic poses alone do not close that gate.

Original trial mappings included Ranger/Peasant parts. They are superseded by
`Docs/IMPLEMENTATION_14C_MAKEHUMAN_DECISION.md`: use only the four pinned MakeHuman
garments for production adaptation; Quaternius is comparison/reference only.
No production catalog activation until visual and technical acceptance.
