# FOC 3D Art Bible

## Direction

The visual identity is grounded historical semi-realism for approximately 1648–1650. Assets must be original or carry explicit commercially usable provenance. Proprietary meshes, textures or animations from Bannerlord or any other commercial game are forbidden.

Priorities are historically plausible silhouettes, realistic human proportions, readable equipment, coherent metal/leather/cloth/wood materials, controlled wear, strong distance readability and cultural distinction without caricature. Avoid fantasy-MMO shapes, heroic exaggeration, cartoon/chibi proportions, neon accents, modern tactical styling and micro-detail that disappears at the tactical camera scale.

## Tactical-camera hierarchy

At common battle distance, spend budget in this order:

1. body and mount silhouette;
2. headgear and shield outline;
3. main weapon family/readability;
4. armor/clothing mass and material family;
5. cultural color/pattern grouping;
6. face, minor accessories and surface wear.

Narrative/Hero assets may use higher face and texture detail. Standard Soldiers use the battle-standard profile. Crowd/Far assets remove face detail, small accessories, minor cloth geometry, costly normal detail and unnecessary bones first. Major silhouette, headgear, main weapon, shield and mount remain readable.

## Materials and textures

Use a controlled Built-in-compatible family for skin, cloth/leather, metal, wood and hair/alpha when required. Reuse shared materials and atlases; do not create a runtime material per Soldier. Standard Soldiers should normally target compact 1K-class texture sets or atlases; Narrative assets may justify higher tiers after camera testing. Automatic 4K generation for every item is prohibited.

Quality describes plausible production/workmanship condition, not Common/Rare/Epic rarity. Visual wear is presentation metadata and cannot silently change combat values.

## Body, face and modular fit

All ordinary humans must fit the canonical Humanoid skeleton and `standard-human` body family unless a new validated family is introduced. Controlled height/build variation must remain skeleton-compatible and does not change hitboxes or gameplay stats. Heads, hair, beard and age-detail options are composable; ordinary Soldiers do not carry facial animation rigs by default.

Visual-fit review focuses on helmet/head, body/armor, cape/back weapon, weapon/hand, shield/arm, rider/saddle, leg/horse and polearm grip intersections. Automated validation catches missing/mechanically invalid configurations; final aesthetic clipping approval remains an art-review task.

## Naming

Machine names use semantic prefixes such as `CHR_`, `BODY_`, `HEAD_`, `CLTH_`, `ARM_`, `HDG_`, `WPN_`, `SHD_`, `AUX_`, `MNT_`, `HAR_`, `MARM_` and `ANM_`. IDs contain only ASCII letters, digits and underscores. IDs are stable and unique; filenames are not gameplay identities.

## Asset lifecycle

Every asset is Draft, Validated, ProductionCandidate or Approved. Generated/source metadata records tool, source, license, generation date, specification ID/revision and authoring status. Technical validation does not imply final human art approval.
