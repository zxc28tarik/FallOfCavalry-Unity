# 14C Hasan-only MakeHuman transformation

Date: 2026-09-26. Status: DRAFT, NOT ACCEPTED. Scope remains Hasan-first.
Checkpoint: `3889797a4af1b2a01dcf17da1c6a3e1792b239fc`.

## Sources and decision

The exact CC0 archives, SHA-256 hashes, authors and original filenames are in
`ArtSource/HistoricalSlice/Upstream/ClothingDonors/PROVENANCE.md`. Originals remain
unchanged. No additional pack, paid source, procedural tunic or different body.

Use `rehmanpolanski_viking_tunic`'s continuous torso/shoulder/sleeve component,
`toigo_harem_pants` and `culturalibre_male_boots`. The tunic trial retained the
best shoulder fit. `donitz_monk_robe` was tested but is not used in Hasan r1: its
separate cape, rope and floor-length skirt would require more extensive repair
than the tunic's continuous construction. This is a selection within the frozen
MakeHuman family, not a replacement pipeline. Quaternius remains rejected.

The donor names are provenance, not historical attribution. The MakeHuman
clothes provide barycentric body bindings, UVs and topology, not ready FOC bone
weights. Trial weights transferred from the unchanged fitted hm08 body are
retained above the hip. Same-side thigh corrective weights are used for the
lengthened split coat. Their success requires rendered movement review.

## Historical research and limits

- [Topkapi kaftan 13/37, Museum With No Frontiers](https://islamicart.museumwnf.org/database_item.php?id=object%3Bisl%3Btr%3Bmus01_a%3B32%3Ben):
  second half of the **sixteenth century**, uncertain attribution to Prince
  Bayezid. Round collar, crossover front, shaped waist and widening skirt inform
  construction vocabulary. Its luxury brocade and decorative long sleeves are
  **not** evidence for a 1648 ordinary soldier's uniform.
- [Met kaftan, 2003.416a–e](https://www.metmuseum.org/art/collection/search/454043):
  second half of the seventeenth to early eighteenth century, Turkey, silk and
  metal-wrapped thread. Used for chronology and layered garment context, **not**
  copied court decoration or an exact 1648 reconstruction.

The practical knee-length split riding coat is an explicitly inferred art
adaptation between these references, not a museum-verified surviving uniform.
No museum image/texture has been embedded or traced into the mesh.

## Actual mesh edits (r1–r4)

`Tools/Art/adapt_hasan_donors.py`, Blender 4.5.9, works only on pinned donor meshes:

- Delete the tunic's disconnected belt and buckle/pendant components.
- Preserve shoulder/armhole/elbow donor construction and its transferred fit.
- Extend and flare the existing lower garment vertices; no synthesized rings.
- Bisect and split existing front topology for an opening and back lower
  topology for a riding vent; interpolate the existing UV/deformation layers.
- Reuse a cropped copy of that donor as a visible inner layer.
- Taper the original trousers into boots; remove only occluded lower trouser
  geometry inside boot shafts. No body redesign or hidden garment-collision fix.
- Keep original boot shape; reduce excessive topology, preserve sole/upper UVs.
- Reuse the mature Hasan head, hands and already licensed hair/beard/eyes.
- Export three LODs and one consolidated skinned renderer per active LOD.

Materials remain the existing diagnostic surfaces until geometry passes. No
production quality claim is made from these colors or numeric validation.

Revision 2 removes duplicate inner sleeves after real runtime frames exposed
z-fighting/interpenetration, tapers the trouser cuff, and tests a smoother
pelvis/thigh coat blend. That blend fails visibly in running/crouching: trousers
pass through the coat. Revision 3 therefore refits **only** the edited skirt's
weights against the same fitted body at its new length, constrains lower-limb
weights to the corresponding side, raises the donor neckline and creates a
crossing upper-front edge. Shoulder/armhole weights remain unchanged. No body
or rig replacement is involved.

Revision 4 removes the unwanted upper-neckline shoulder tabs and moves the
trouser occlusion boundary fully inside the boot cuff. The consolidated body
also restores the unchanged base neck/upper-chest patch: hiding the entire body
except hands/head had left a visible neck gap during spine motion. Its original
positions, UVs and weights are retained; this is not a new human base. Gait
diagnostic pelvis height is lowered to keep the stance foot within IK reach.
Review framing is fixed across phases so full-body evidence cannot crop a
deformation out of the image.

## Motion evidence scope

`HasanDonorPipeline` authors eight isolated canonical-rig review clips with
actual arm/leg/spine/turn movement. The player runs the Animator for two seconds,
requires measured joint movement, then captures a repeatable phase. The mounted
clip runs on the existing seat/tack/horse. Captures are real Windows/D3D output.

These are functional deformation diagnostics, **not** replacement production
battle animation and not persistent-loadout acceptance. The old shared clips
were mostly pelvis-motion fixtures; they cannot prove credible locomotion.

The first runtime attempt correctly failed with zero measured motion because a
Humanoid avatar cannot consume these transform curves as muscle animation. The
review player now creates a Generic avatar **only on its temporary instance**;
the versioned production-compatible prefab retains its canonical Humanoid
avatar. This fixes the diagnostic, not the missing production animation gate.

Review frames belong under `Drafts/HasanDonor`, never `Accepted`. Catalog and
ProductionArt gate remain unchanged until all four characters pass.
