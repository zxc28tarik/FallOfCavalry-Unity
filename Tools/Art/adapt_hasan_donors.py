"""Hasan-only CC0 donor reshaping. No new garment topology generator.

Original shoulder/armhole vertices and weights survive; edits operate on the
selected tunic, trousers and boots. Draft output cannot activate the catalog.
"""
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
import test_clothing_donors as donor
import bpy
import bmesh
import json
import hashlib
from mathutils import Vector

ROOT = donor.ROOT
OUT = ROOT / 'UnityProject/Assets/FOC/ArtSource/HistoricalSlice/HasanDonor'
REPORT = ROOT / 'TestResults/HasanDonor'
OUT.mkdir(parents=True, exist_ok=True)
REPORT.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)

def edit_mesh(obj, action):
    bm = bmesh.new(); bm.from_mesh(obj.data)
    action(bm)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data); bm.free(); obj.data.update()

def component_keep(bm):
    pending = set(bm.verts); groups = []
    while pending:
        v = pending.pop(); group = {v}; stack = [v]
        while stack:
            cur=stack.pop()
            for edge in cur.link_edges:
                other = edge.other_vert(cur)
                if other in pending: pending.remove(other); group.add(other); stack.append(other)
        groups.append(group)
    keep = max(groups, key=len)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v not in keep], context='VERTS')

def cut_opening(bm, front, top):
    # Bisect existing front/back quads then split the new seam. The original
    # UV and deform layers are interpolated by BMesh, not re-generated.
    selected = [f for f in bm.faces if f.calc_center_median().z * front > .015
                and f.calc_center_median().y < top]
    edges = {e for f in selected for e in f.edges}
    verts = {v for f in selected for v in f.verts}
    bmesh.ops.bisect_plane(bm, geom=list(verts)+list(edges)+selected,
                          plane_co=(0, 0, 0), plane_no=(1, 0, 0), dist=.00001)
    seam = [e for e in bm.edges if all(abs(v.co.x) < .0001 and v.co.z*front > .015
                                      and v.co.y < top for v in e.verts)]
    if seam: bmesh.ops.split_edges(bm, edges=seam)
    for v in bm.verts:
        if abs(v.co.x) < .0001 and v.co.z*front > .015 and v.co.y < top:
            side = 1 if sum(f.calc_center_median().x for f in v.link_faces) >= 0 else -1
            spread = .012 + max(0, .99-v.co.y)*.11
            v.co.x += side*spread
            v.co.z += .004*front

paths = {name: next((donor.SOURCE/'clothes'/name).glob('*.mhclo')) for name in
         ('rehmanpolanski_viking_tunic', 'toigo_harem_pants', 'culturalibre_male_boots')}
coat = donor.makehuman(paths['rehmanpolanski_viking_tunic'])
edit_mesh(coat, component_keep)  # Delete Viking belt, pendant and buckle only.

# Inner garment is a cropped copy of the SAME licensed donor, not a new tunic.
inner = coat.copy(); inner.data = coat.data.copy(); bpy.context.scene.collection.objects.link(inner)
edit_mesh(inner, lambda bm: bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.y < 1.015 or abs(v.co.x)>.17], context='VERTS'))
for v in inner.data.vertices:
    v.co.x *= .99; v.co.z = (v.co.z-.025)*.99+.025
    if v.co.y>1.465 and abs(v.co.x)<.085:
        t=min(1,(v.co.y-1.465)/.045)
        v.co.y+=.024*t
        v.co.x*=1-.12*t
        v.co.z=(v.co.z-.025)*(1-.10*t)+.025
edit_mesh(inner, lambda bm: bmesh.ops.delete(bm,geom=[v for v in bm.verts if v.co.y>1.475 and abs(v.co.x)>.085],context='VERTS'))
inner.name = 'HasanInnerDonor'

def reshape_coat(bm):
    layer = bm.verts.layers.deform.active
    for v in bm.verts:
        x, y, z = v.co
        if y < 1.015:
            t = min(1, (1.015-y)/.283)
            v.co.y = 1.015-(1.015-y)*1.65
            v.co.x *= 1+.30*t
            v.co.z = (z-.02)*(1+.28*t)+.02
            # Existing fit weights are retained above the hip. Split skirts
            # follow their same-side thigh below it, not the opposite thigh.
            # Refit ONLY edited skirt weights at their new anatomical position.
            # A pelvis/thigh blend guessed from old hem height penetrated trousers
            # during crouch; use the same fitted body correspondence as the donor.
            values = {}
            side='L' if x>=0 else 'R'
            for bi,w in donor.transfer_weights(v.co):
                name=donor.names[bi]
                if name.startswith(('UpperLeg_','LowerLeg_')):bi=donor.names.index(name[:-1]+side)
                values[bi]=values.get(bi,0)+w
            v[layer].clear()
            for i,w in donor.normalize(values): v[layer][i] = w
        # Add shell clearance without modifying shoulders/armhole construction.
        if abs(x)<.24 and y>1.015:
            v.co.x *= 1.035; v.co.z=(z-.025)*1.055+.025
    cut_opening(bm, 1, 1.60)  # Full front opening exposes inner garment.
    cut_opening(bm, -1, .965) # Riding vent below waist.
    for v in bm.verts:
        if .011<v.co.x<.014 and v.co.z>.02 and 1.015<v.co.y<1.49:
            v.co.x-=.040;v.co.z+=.014 # Left front crosses over right, not a zip stripe.
edit_mesh(coat, reshape_coat)
coat.name='HasanCoatDonor'

pants = donor.makehuman(paths['toigo_harem_pants'])
for v in pants.data.vertices:
    # Keep roomy upper legs, taper calf into the selected boot shafts.
    sign=1 if v.co.x>=0 else -1
    if v.co.y<.53:
        t=max(0,min(1,(.53-v.co.y)/.13))
        center=sign*(.1977+(.1536-.1977)*max(0,min(1,(v.co.y-.073)/.438)))
        t=t*t*(3-2*t)
        v.co.x=center+(v.co.x-center)*(1-.50*t)
        v.co.z=.026+(v.co.z-.026)*(1-.50*t)
# Occluded lower legs are removed under tall boots, not visible collision masking.
edit_mesh(pants, lambda bm:bmesh.ops.delete(bm,geom=[v for v in bm.verts if v.co.y<.36],context='VERTS'))
pants.name='HasanTrousersDonor'
boots = donor.makehuman(paths['culturalibre_male_boots']); boots.name='HasanBootsDonor'
for v in boots.data.vertices:
    v.co.y += .011 # sole rests on floor; preserve upstream foot topology.
bpy.context.view_layer.objects.active=boots
mod=boots.modifiers.new('BootTopologyReduction','DECIMATE'); mod.ratio=.32
bpy.ops.object.modifier_apply(modifier=mod.name)

# Preserve canonical human base and licensed mature head/hair. Use source hand
# islands only: clothing correctly occludes the body, no hidden second torso.
existing=json.loads((ROOT/'UnityProject/Assets/FOC/ArtSource/HistoricalSlice/Characters/CHR_HasanAga_01.focmesh.json').read_text())
retained=(0,1,5,6,7,8,9,10,11,12)
def retained_neck(lod):
    # Clothing occludes the torso, not the neck. Preserve the ORIGINAL base
    # neck/upper-chest bridge and its original weights instead of inventing a
    # collar to hide the missing skin. This is a body visibility mask, not a base swap.
    p=donor.canonical['lods'][lod]['parts'][0]
    positions=p['positions']; triangles=[]
    for i in range(0,len(p['triangles']),3):
        tri=p['triangles'][i:i+3]
        if all(1.38<positions[v*3+1]<1.57 and abs(positions[v*3])<.115 for v in tri):triangles+=tri
    used=sorted(set(triangles)); lookup={v:i for i,v in enumerate(used)}
    part={'material':'SkinMature','triangles':[lookup[v] for v in triangles]}
    for key,stride in [('positions',3),('normals',3),('uv',2),('boneIndices',4),('boneWeights',4)]:
        part[key]=[n for v in used for n in p[key][v*stride:(v+1)*stride]]
    assert len(triangles)>9, 'Neck bridge must survive each LOD'
    return part
donor.OUT=OUT
sources=[]
for obj,material,path in ((coat,'ClothRed',paths['rehmanpolanski_viking_tunic']),
                          (inner,'Linen',paths['rehmanpolanski_viking_tunic']),
                          (pants,'ClothBlue',paths['toigo_harem_pants']),
                          (boots,'Leather',paths['culturalibre_male_boots'])):
    donor.export(obj,path)
    file=OUT/('CLTH_Donor_'+obj.name+'.focmesh.json')
    src=json.loads(file.read_text())
    for lod in src['lods']:
        for part in lod['parts']: part['material']=material
    src.update(revision='hasan-donor-r4-historical-reshape-NOT-ACCEPTED',
               generator='Blender '+bpy.app.version_string+' / Tools/Art/adapt_hasan_donors.py',
               source=src['source']+'; Docs/IMPLEMENTATION_14C_HASAN_DONOR_TRANSFORMATION.md')
    file.write_text(json.dumps(src,separators=(',',':')),encoding='utf-8'); sources.append(src)

payload=dict(sources[0]);payload['assetId']='CHR_HasanAga_DonorDraft';payload['category']='ConsolidatedCharacter'
payload['source']='Pinned MakeHuman garments + unchanged mature Hasan head/hands/hair; see transformation ledger'
payload['sourceSha256']=hashlib.sha256(''.join(s['sourceSha256'] for s in sources).encode()).hexdigest()
payload['lods']=[{'parts':[existing['lods'][i]['parts'][j] for j in retained]+[retained_neck(i)]+[p for s in sources for p in s['lods'][i]['parts']]} for i in range(3)]
(OUT/(payload['assetId']+'.focmesh.json')).write_text(json.dumps(payload,separators=(',',':')),encoding='utf-8')
result={'assetId':payload['assetId'],'status':'DRAFT_NOT_VISUALLY_ACCEPTED','revision':payload['revision'],
        'donors':list(paths),'unchangedCanonicalBones':payload['bones']==donor.bones,
        'lodTriangles':[sum(len(p['triangles'])//3 for p in lod['parts']) for lod in payload['lods']],
        'scope':'Hasan only; catalog unchanged; Quaternius not imported'}
(REPORT/'adaptation.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(result),flush=True)
