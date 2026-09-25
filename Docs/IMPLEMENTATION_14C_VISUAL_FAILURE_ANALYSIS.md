# Implementation 14C — pre-edit visual failure analysis

Date: 2026-09-25. Baseline: `241ce2513d34f74e53ce47c19fbe0ebbdcd4655b`.
All nine committed `Docs/Evidence/Implementation14C/Drafts/Continuation/` images were opened and visually inspected BEFORE this rescue modified assets. These are rejected diagnostic renders, not accepted production evidence. A static image cannot establish animation quality, hidden topology, skin weights, or performance.

## Hasan body — hasan-body-draft.png

Anatomy is recognizably human: articulated hands, face, neck, legs. No evidence here warrants replacing the anatomical base. The garment silhouette is a fitted shirt abruptly becoming a cone. Artificial bright crescents at shoulders/armpits and triangular shading require geometry/normal/weight investigation. A jagged grey waist band, blue trouser intrusions and a rectangular centre vent destroy the layered construction. Collar, opening, cuffs and hem are not intentional enough. The head has features but dull eyes; hair reads as a smooth cap, beard as coarse stippled fibers. Armor reads like dark cloth, not mail. Boots are closed but lack sole, ankle and leather construction. No weapons, rider or tack in frame; animation not assessed.

## Hasan deformation — hasan-standing-deformation-draft.png

Hands remain anatomical rather than exploding toward the head, supporting the earlier metacarpal fix. Bent arms expose artificial sleeve/underarm transitions; waist and trouser intersections remain. This is a diagnostic IK pose, not proof of shared animation. Face, hair, mail, boot and garment defects above remain. No weapons/mount/tack. Hidden topology and moving deformation still need tests and actual motion.

## Cebeli — cebeli-body-draft.png

Same generic upper-body/coat silhouette as Hasan, red recolor rather than distinct equipment/role. Jagged waist, blue patches through skirt and centre cutout are obvious. No complete headgear/armor/weapon identity. Anatomical base is usable at this distance, face/hair/boots still draft. Flat material differentiation. No rider/tack/horse; animation not assessed.

## Mounted Sipahi side — sipahi-mounted-side-fit-draft.png

Horse has genuine anatomical barrel, neck, muzzle and articulated limbs, not proof primitives. Rider remains an unequipped generic dark tunic with rough head/hair. Seat/pelvis, coat termination and thigh transition are awkward. Foot is near a stirrup but believable support/contact is not established. Blanket is an overly simple red sheet, girth a strip, bridle/bit/rein-to-hand connection incomplete. No readable mail/headgear/kılıç/lance/shield. Rigid pose; a still is not locomotion evidence. Garment weights and saddle fit must be reviewed together.

## Mounted Sipahi three-quarter — sipahi-mounted-three-quarter-fit-draft.png

Hands cross/open without convincingly gripping reins. Torso/waist joins and boots remain weak; no complete equipment silhouette. Horse mane is a raised strip and tail shredded cards. Saddle and tack lack construction and hand connection. Body/horse proportions are substantially better than primitives, but limb contact/clearance requires animated review. No conclusion about gait can be drawn from this pose.

## Horse side — horse-side.png

Keep this real anatomical base. Chest, barrel, quarters, neck and muzzle are recognizable. Mane ridge and layered tail strips look artificial; hoof/sock transition is lumpy and broad. Coat mottling lacks coherent surface polish. Only one coat is shown. Eyes/hooves need closer checks. No human garments/face/armor/footwear/weapons/tack/rider in this image. It cannot prove rig, hoof contact or gait quality.

## Horse three-quarter — horse-three-quarter.png

Recognizable horse face, ears, chest and limb proportions. Mane forms a raised flap; tail cards and hooves are rough. Rigid leg presentation does not establish deformation. Body does not need rebuilding solely because its grooming/materials fail. Human/equipment/tack categories absent. Animation not assessed.

## Horse trot — horse-trot.png

Mane visibly separates/floats above the neck. Leg folding/crossing and suspension look unnatural; ground contact is questionable. Code inspection separately shows simple sinusoidal joint rotations, not contact-controlled stride trajectories. A single sampled frame cannot quantify foot sliding, loop continuity, or gait correctness. No rider/tack/weapon animation is proven. Base anatomy is not thereby disproved; rig weighting and animation are separate faults.

## Matchlock — matchlock-draft.png

Distinct shaped stock/barrel/ramrod/lock elements are present, so this is not the old box prop. Still too thin/under-detailed, with weak fittings and flat wood/metal response. Isolated upright framing cannot prove scale, hand grip, aiming, or socket fit. Exact stock dating needs care because the cited surviving example has a later refurbished butt. All human/horse/tack/animation categories are absent.

## Root-cause hypotheses and strategy lock

1. Human base: **usable**, provisionally. Existing visible anatomy and corrected hand deformation do not support calling it the root blocker. This is not full animation certification.
2. Garment: construction, layer containment and surface/skin continuity are the immediate blockers. The failed stash's independent ring/armhole garment will NOT be restored.
3. New attempt: preserve body and canonical rig; derive connected shoulder/sleeve topology from the body; reconstruct lower garment with continuous boundary connectivity; use subdivision, thickness, seam/opening construction and collision correction against trousers. Transfer/interpolate source weights. Add visible collar/cuffs/waist construction; test Hasan alone first.
4. Hair: selectively recover only individually verified CC0 source cards and adapt fitting/material handling, not failed generated garments.
5. Horse: retain base, defer gait/tack changes until Hasan is acceptable. Static fit never substitutes for motion inspection.
6. Catalog remains proof-backed until all four profiles pass. No Accepted images, activation or full-runtime production benchmark may be claimed from diagnostic renders.

## Stash audit

`stash@{0}` / `3d0dd1cf40c3a5a5b9a59c38e88b20559105cac2` inspected by file list and ordinary parent-to-stash diff. Preserve the stash. Selective source candidates: RehmanPolanski beard/moustache with CC0 headers, generalized fitting and exact head-neck trimming. Reject wholesale application, generated geometry, independent-ring garment module and cached Python bytecode. No reserved Quaternius versus MPFB/MakeHuman base replacement in this turn.
