"""Hasan-only CC0 donor reshaping. No new garment topology generator.

Original shoulder/armhole vertices and weights survive; edits operate on the
selected tunic, trousers and boots. Draft output cannot activate the catalog.
"""
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
import test_clothing_donors as donor
import hasan_grip
import hasan_tailoring
import bpy
import bmesh
import json
import hashlib
import math
from collections import defaultdict
from mathutils import Vector
from mathutils.bvhtree import BVHTree

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

def smoothstep(a,b,v):
    t=max(0.,min(1.,(v-a)/(b-a)))
    return t*t*(3.-2.*t)

def orient_open_band(obj,center_z):
    bm=bmesh.new();bm.from_mesh(obj.data);bm.normal_update()
    score=sum(f.normal.dot(Vector((f.calc_center_median().x,0,f.calc_center_median().z-center_z)))*f.calc_area() for f in bm.faces)
    if score<0:bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
    bm.to_mesh(obj.data);bm.free();obj.data.update()
    return score

def anatomical_weights(bm):
    """Constrain the existing donor correspondence, not a new skeleton."""
    layer=bm.verts.layers.deform.active
    for v in bm.verts:
        x,y,z=v.co
        if y<1.36:continue
        values=dict(v[layer]); side='L' if x>=0 else 'R'
        neck=donor.names.index('Neck'); chest=donor.names.index('Chest')
        # The collar is sewn to the torso, not to an adjacent raised arm.
        collar=(1-smoothstep(.075,.16,abs(x)))*smoothstep(1.39,1.445,y)
        for name in ('UpperArm_L','UpperArm_R','LowerArm_L','LowerArm_R','Head'):
            bi=donor.names.index(name); amount=values.get(bi,0)*collar
            values[bi]=values.get(bi,0)-amount;values[chest]=values.get(chest,0)+amount
        extra=max(0,values.get(neck,0)-.20)*collar
        values[neck]=values.get(neck,0)-extra;values[chest]=values.get(chest,0)+extra
        # Conversely the shoulder cap must not track the neck/head.
        shoulder=smoothstep(.08,.16,abs(x))
        for name in ('Neck','Head'):
            bi=donor.names.index(name); amount=values.get(bi,0)*shoulder
            values[bi]=values.get(bi,0)-amount;values[chest]=values.get(chest,0)+amount
        arm=donor.names.index('UpperArm_'+side)
        cap=smoothstep(.095,.215,abs(x))
        if y>1.39 and values.get(arm,0)>cap:
            values[chest]=values.get(chest,0)+values[arm]-cap;values[arm]=cap
        v[layer].clear()
        for bi,w in donor.normalize(values):v[layer][bi]=w

def sleeve_refine(bm):
    for v in bm.verts:
        if abs(v.co.x)<.25:continue
        side='L' if v.co.x>=0 else 'R'
        elbow=donor.bp['LowerArm_'+side]; wrist=donor.bp['Hand_'+side]
        axis=wrist-elbow;t=(v.co-elbow).dot(axis)/axis.length_squared
        if -.22<t<.24:
            center=elbow+axis*t;radial=v.co-center
            # Smooth the original donor's bulbous elbow cuff into a continuous
            # sleeve. Retain the original quads/UVs and a generous cloth radius.
            influence=smoothstep(-.22,-.07,t)*(1-smoothstep(.08,.24,t))
            if radial.length>.066:
                v.co=center+radial.lerp(radial.normalized()*.066,influence)

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
            spread = .008 + max(0, .95-v.co.y)*.065
            v.co.x += side*spread
            v.co.z += .004*front

paths = {name: next((donor.SOURCE/'clothes'/name).glob('*.mhclo')) for name in
         ('rehmanpolanski_viking_tunic', 'toigo_harem_pants', 'culturalibre_male_boots')}
pants = donor.makehuman(paths['toigo_harem_pants'],barycentric_weights=True)
for v in pants.data.vertices:
    sign=1 if v.co.x>=0 else -1
    if v.co.y<.53:
        t=smoothstep(0,1,(.53-v.co.y)/.13)
        center=sign*(.1977+(.1536-.1977)*max(0,min(1,(v.co.y-.073)/.438)))
        v.co.x=center+(v.co.x-center)*(1-.50*t)
        v.co.z=.026+(v.co.z-.026)*(1-.50*t)
edit_mesh(pants, lambda bm:bmesh.ops.delete(bm,geom=[v for v in bm.verts if v.co.y<.36],context='VERTS'))
pants.name='HasanTrousersDonor'
coat = donor.makehuman(paths['rehmanpolanski_viking_tunic'],barycentric_weights=True)
edit_mesh(coat, component_keep)  # Delete Viking belt, pendant and buckle only.
edit_mesh(coat, sleeve_refine)
edit_mesh(coat, anatomical_weights)

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
            for bi,w in dict(v[layer]).items():
                name=donor.names[bi]
                # Knee-length coat must never follow a shin/foot bone.
                if name.startswith(('UpperLeg_','LowerLeg_','Foot_')):bi=donor.names.index('UpperLeg_'+side)
                values[bi]=values.get(bi,0)+w
            pelvis=donor.names.index('Pelvis');thigh=donor.names.index('UpperLeg_'+side)
            thigh_weight=min(.88,.45+.43*smoothstep(.0,.95,t))
            # Preserve the original seam's Spine/Pelvis correspondence. Jumping
            # straight to two leg influences at y=1.015 creates a waist crease.
            blend=smoothstep(.04,.60,t)
            values={bi:w*(1-blend) for bi,w in values.items()}
            values[pelvis]=values.get(pelvis,0)+(1-thigh_weight)*blend
            values[thigh]=values.get(thigh,0)+thigh_weight*blend
            v[layer].clear()
            for i,w in donor.normalize(values): v[layer][i] = w
        # Add shell clearance without modifying shoulders/armhole construction.
        if abs(x)<.24 and y>1.015:
            v.co.x *= 1.035; v.co.z=(z-.025)*1.055+.025
    cut_opening(bm, 1, 1.60)  # Full front opening exposes inner garment.
    cut_opening(bm, -1, .965) # Riding vent below waist.
    for v in bm.verts:
        if .006<v.co.x<.011 and v.co.z>.02 and 1.015<v.co.y<1.49:
            v.co.x-=.042;v.co.z+=.012 # Left front crosses over right, not a zip stripe.
edit_mesh(coat, reshape_coat)
coat.name='HasanCoatDonor'

# Fit the existing waist/skirt against the actual trousers in bind pose. Both
# the rear waist and a donor thigh ring at y=.643 were inside before animation.
# This is a bounded refit of original vertices, never trouser deletion or a
# nonzero neutral corrective. Preserve every UV, face and deformation binding.
def fit_waist(bm):
    mesh=pants.data;mesh.calc_loop_triangles()
    tree=BVHTree.FromPolygons([v.co for v in mesh.vertices],
                            [tuple(t.vertices) for t in mesh.loop_triangles],all_triangles=True)
    rest={v:v.co.copy() for v in bm.verts}
    selected=[v for v in bm.verts if .56<v.co.y<1.21 and abs(v.co.x)<.36]
    def project():
        for v in selected:
            location,normal,_,distance=tree.find_nearest(v.co,.07)
            if location is None:continue
            leg_center=(.17 if v.co.x>=0 else -.17)*(1-smoothstep(.78,.95,v.co.y))
            radial=Vector((v.co.x-leg_center,0,v.co.z-.02)).normalized()
            if normal.dot(radial)<.25:continue # do not close open waistband rims
            signed=(v.co-location).dot(normal)
            if signed<.012:
                amount=min(.012-signed,.04)*smoothstep(.56,.60,v.co.y)*(1-smoothstep(1.17,1.21,v.co.y))
                v.co+=normal*amount
                delta=v.co-rest[v]
                if delta.length>.055:v.co=rest[v]+delta.normalized()*.055
    for _ in range(8):
        project()
        offsets={v:v.co-rest[v] for v in bm.verts}
        for v in selected:
            neighbors=[e.other_vert(v) for e in v.link_edges]
            if neighbors:
                mean=sum((offsets[n] for n in neighbors),Vector())/len(neighbors)
                v.co=rest[v]+offsets[v].lerp(mean,.22)
    project()
    fit_waist.audit={'region':'original donor waist and thigh skirt','changedVertices':sum((v.co-rest[v]).length>1e-6 for v in selected),
                     'maxOffsetMeters':max(((v.co-rest[v]).length for v in selected),default=0),
                     'targetClearanceMeters':.012,'topologyUvWeightsUnchanged':True}
edit_mesh(coat,fit_waist)
# The actual Run250 audit found 5–9mm penetration at y1.061, immediately above
# the deliberately pinned corrective region. Give the ORIGINAL front waist
# controlled tailoring ease rather than silently widening the runtime solver.
waist_ease=[]
for v in coat.data.vertices:
    x,y,z=v.co
    amount=.028*smoothstep(.97,1.025,y)*(1-smoothstep(1.085,1.16,y))
    amount*=smoothstep(.04,.10,z)*(1-smoothstep(.15,.23,abs(x)))
    if amount>1e-7:v.co.z+=amount;waist_ease.append(amount)
coat.data.update()
tailoring=hasan_tailoring.build_tailoring(coat,donor)

# Sash: cut the already licensed coat surface at two waist planes. This is a
# fitted copy of existing donor topology, not a procedural replacement tunic.
sash=coat.copy();sash.data=coat.data.copy();bpy.context.scene.collection.objects.link(sash)
def cut_sash(bm):
    for y,outer in ((1.005,False),(1.10,True)):
        bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),
                              plane_co=(0,y,0),plane_no=(0,1,0),dist=.000001,
                              clear_outer=outer,clear_inner=not outer)
    component_keep(bm) # Horizontal cuts also intersected sleeve cuffs; not sash.
    layer=bm.verts.layers.deform.active
    for v in bm.verts:
        radial=Vector((v.co.x,0,v.co.z-.015)).normalized()
        v.co+=radial*(.013+.0015*math.sin(v.co.y*380))
        # Retain the underlying donor band's exact weights; another guessed
        # pelvis/spine blend makes the sash slide through the garment.
edit_mesh(sash,cut_sash);sash.name='HasanSashDonor'
# Recalculating normals on an open cut band can choose the inward winding.
# Check its whole connected component against the torso radial direction and
# reverse consistently; never use a two-sided shader to conceal the defect.
orientation=orient_open_band(sash,.015)

boots = donor.makehuman(paths['culturalibre_male_boots'],barycentric_weights=True); boots.name='HasanBootsDonor'
for v in boots.data.vertices:
    v.co.y += .011 # sole rests on floor; preserve upstream foot topology.
bpy.context.view_layer.objects.active=boots
mod=boots.modifiers.new('BootTopologyReduction','DECIMATE'); mod.ratio=.32
bpy.ops.object.modifier_apply(modifier=mod.name)

# Preserve canonical human base and licensed mature head/hair. Use source hand
# islands only: clothing correctly occludes the body, no hidden second torso.
existing=json.loads((ROOT/'UnityProject/Assets/FOC/ArtSource/HistoricalSlice/Characters/CHR_HasanAga_01.focmesh.json').read_text())
grip_reports=[hasan_grip.add_grip_shapes(lod['parts'][i],donor)
              for lod in existing['lods'] for i in (0,1)]
retained=(0,1,5,6,7,8,9,10,11,12)
def retained_neck(lod):
    # Clothing occludes the torso, not the neck. Preserve the ORIGINAL base
    # neck/upper-chest bridge and its original weights instead of inventing a
    # collar to hide the missing skin. This is a body visibility mask, not a base swap.
    p=donor.canonical['lods'][lod]['parts'][0]
    positions=p['positions']; triangles=[]
    for i in range(0,len(p['triangles']),3):
        tri=p['triangles'][i:i+3]
        if all(1.455<positions[v*3+1]<1.57 and abs(positions[v*3])<.078
               and abs(positions[v*3+2]-.025)<.092 for v in tri):triangles+=tri
    used=sorted(set(triangles)); lookup={v:i for i,v in enumerate(used)}
    part={'material':'SkinMature','triangles':[lookup[v] for v in triangles]}
    for key,stride in [('positions',3),('normals',3),('uv',2),('boneIndices',4),('boneWeights',4)]:
        part[key]=[n for v in used for n in p[key][v*stride:(v+1)*stride]]
    assert len(triangles)>9, 'Neck bridge must survive each LOD'
    # The mature head already includes skin from y=1.494 upwards. The former
    # bridge duplicated that neck surface with the wider original body, causing
    # visible wedges/z-fighting. Clip the existing bridge at the true interface;
    # 0.5mm overlap protects the seam. Interpolate UVs/weights, never discard whole
    # crossing triangles or replace the canonical head/body.
    obj=donor.mesh_object('HasanNeckVisibility_'+str(lod),
        [part['positions'][i:i+3] for i in range(0,len(part['positions']),3)],
        [[(v,v) for v in part['triangles'][i:i+3]] for i in range(0,len(part['triangles']),3)],
        [part['uv'][i:i+2] for i in range(0,len(part['uv']),2)],
        [[(bi,w) for bi,w in zip(part['boneIndices'][i:i+4],part['boneWeights'][i:i+4]) if w>0]
         for i in range(0,len(part['boneIndices']),4)])
    edit_mesh(obj,lambda bm:bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),
              plane_co=(0,1.4945,0),plane_no=(0,1,0),dist=.000001,clear_outer=True))
    orient_open_band(obj,.02)
    result={k:[] for k in ('positions','normals','uv','triangles','boneIndices','boneWeights')}
    result['material']='SkinMature';mesh=obj.data;mesh.calc_loop_triangles()
    for tri in mesh.loop_triangles:
        for li in tri.loops:
            vertex=mesh.vertices[mesh.loops[li].vertex_index]
            result['triangles'].append(len(result['positions'])//3)
            result['positions']+=list(vertex.co);result['normals']+=list(vertex.normal)
            result['uv']+=list(mesh.uv_layers.active.data[li].uv)
            weights=donor.normalize({g.group:g.weight for g in vertex.groups});weights += [(0,0)]*(4-len(weights))
            result['boneIndices']+=[bi for bi,w in weights];result['boneWeights']+=[w for bi,w in weights]
    bpy.data.objects.remove(obj,do_unlink=True)
    assert len(result['triangles'])>9,'Clipped neck bridge must survive each LOD'
    return result
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
    if obj==coat:
        # Export derived sash into the SAME coat asset, so authoring remains
        # modular without adding an extra active renderer or a new source pack.
        donor.export(sash,path)
        sash_file=OUT/('CLTH_Donor_'+sash.name+'.focmesh.json')
        sash_src=json.loads(sash_file.read_text())
        for a,b in zip(src['lods'],sash_src['lods']):
            for p in b['parts']:p['material']='HasanSash'
            a['parts']+=b['parts']
        sash_file.unlink() # generated intermediary only; never an upstream file
        for detail,detail_material in tailoring:
            donor.export(detail,path)
            detail_file=OUT/('CLTH_Donor_'+detail.name+'.focmesh.json')
            detail_src=json.loads(detail_file.read_text())
            for a,b in zip(src['lods'],detail_src['lods']):
                for p in b['parts']:p['material']=detail_material
                a['parts']+=b['parts']
            detail_file.unlink()
    src.update(revision='hasan-donor-r8-final-draft-NOT-ACCEPTED',date='2026-10-03',
               generator='Blender '+bpy.app.version_string+' / Tools/Art/adapt_hasan_donors.py',
               source=src['source']+'; Docs/IMPLEMENTATION_14C_HASAN_DONOR_TRANSFORMATION.md')
    file.write_text(json.dumps(src,separators=(',',':')),encoding='utf-8'); sources.append(src)

payload=dict(sources[0]);payload['assetId']='CHR_HasanAga_DonorDraft';payload['category']='ConsolidatedCharacter'
payload['gripAttachment']=hasan_grip.grip_attachment(donor,'R')
payload['source']='Pinned MakeHuman garments + unchanged mature Hasan head/hands/hair; see transformation ledger'
payload['sourceSha256']=hashlib.sha256(''.join(s['sourceSha256'] for s in sources).encode()).hexdigest()
payload['lods']=[{'parts':[existing['lods'][i]['parts'][j] for j in retained]+[retained_neck(i)]+[p for s in sources for p in s['lods'][i]['parts']]} for i in range(3)]
(OUT/(payload['assetId']+'.focmesh.json')).write_text(json.dumps(payload,separators=(',',':')),encoding='utf-8')
result={'assetId':payload['assetId'],'status':'DRAFT_NOT_VISUALLY_ACCEPTED','revision':payload['revision'],
        'donors':list(paths),'unchangedCanonicalBones':payload['bones']==donor.bones,
        'lodTriangles':[sum(len(p['triangles'])//3 for p in lod['parts']) for lod in payload['lods']],
        'scope':'Hasan only; catalog unchanged; Quaternius not imported'}
result['gripCorrectives']=grip_reports
result['gripAttachment']=payload['gripAttachment']
result['waistFit']=fit_waist.audit
result['frontWaistEase']={'vertices':len(waist_ease),'maximumMeters':max(waist_ease,default=0),'reason':'Run250 measured front waist penetration above pinned corrective region'}
result['sashWinding']={'radialAreaScoreBefore':orientation,'reversed':orientation<0,'doubleSidedShader':False}
result['tailoring']=[json.loads(obj['boundary_provenance']) for obj,_ in tailoring]
(REPORT/'adaptation.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(result),flush=True)
