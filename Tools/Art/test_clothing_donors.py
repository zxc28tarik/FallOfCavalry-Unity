"""Test every operator-selected donor on FOC's existing body/rig.

No garment synthesis, production activation, or historical-authenticity claim.
Original sources remain untouched. Run using Blender --background --disable-autoexec.
"""
import bpy
import json
import math
import hashlib
from pathlib import Path
from collections import defaultdict
from mathutils import Vector
from mathutils.kdtree import KDTree

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'ArtSource/HistoricalSlice/Upstream/ClothingDonors'
INPUT = ROOT / 'Artifacts/ArtInputs'
OUT = ROOT / 'UnityProject/Assets/FOC/ArtSource/HistoricalSlice/DonorTests'
REPORT = ROOT / 'TestResults/ClothingDonors'
OUT.mkdir(parents=True, exist_ok=True)
REPORT.mkdir(parents=True, exist_ok=True)
canonical = json.loads((ROOT / 'UnityProject/Assets/FOC/ArtSource/HistoricalSlice/Characters/BODY_OttomanMale_Standard.focmesh.json').read_text())
bones = canonical['bones']
names = [b['name'] for b in bones]
bp = {b['name']: Vector(b['position']) for b in bones}
points, body_faces, tex = [], [], []
group = ''
for line in (INPUT / 'base.obj').read_text().splitlines():
    p = line.split()
    if not p: continue
    if p[0] == 'v': points.append(Vector(tuple(map(float, p[1:4]))))
    elif p[0] == 'vt': tex.append(tuple(map(float, p[1:3])))
    elif p[0] == 'g': group = p[1]
    elif p[0] == 'f' and group == 'body':
        body_faces.append([(int(v.split('/')[0])-1, int(v.split('/')[1])-1) for v in p[1:]])
for file in ('male-young.target', 'male-build.target'):
    for line in (INPUT / file).read_text().splitlines():
        p = line.split()
        if p and not p[0].startswith('#'): points[int(p[0])] += Vector(tuple(map(float, p[1:4])))
body_ids = sorted({i for face in body_faces for i, _ in face})
bottom = min(points[i].y for i in body_ids)
scale = 1.76 / (max(points[i].y for i in body_ids)-bottom)
points = [Vector((v.x*scale, (v.y-bottom)*scale, v.z*scale)) for v in points]

def mh_bone(name):
    side = 'L' if name.endswith('.L') else 'R'
    for prefixes, target in [
        (('upperarm','shoulder'), 'UpperArm_'), (('lowerarm',), 'LowerArm_'),
        (('wrist','metacarpal','finger'), 'Hand_'), (('upperleg',), 'UpperLeg_'),
        (('lowerleg',), 'LowerLeg_'), (('foot','toe'), 'Foot_')]:
        if name.startswith(prefixes): return target+side
    if name.startswith(('pelvis','root','spine05')): return 'Pelvis'
    if name.startswith(('spine04','spine03')): return 'Spine'
    if name.startswith(('spine','clavicle','breast')): return 'Chest'
    return 'Neck' if name.startswith('neck') else 'Head'

body_weights = defaultdict(lambda: defaultdict(float))
for name, entries in json.loads((INPUT/'default_weights.mhw').read_text())['weights'].items():
    for vi, w in entries: body_weights[vi][names.index(mh_bone(name))] += w
kd = KDTree(len(body_ids))
for i in body_ids: kd.insert(points[i], i)
kd.balance()

def normalize(ws):
    valid = sorted(((i,w) for i,w in ws.items() if w > 1e-7), key=lambda p: (-p[1],p[0]))[:4]
    total = sum(w for _,w in valid)
    if total <= 0: raise ValueError('Missing skin weights')
    return [(i,w/total) for i,w in valid]

def transfer_weights(position):
    ws = defaultdict(float)
    for _, index, distance in kd.find_n(position, 4):
        for bi,w in body_weights[index].items(): ws[bi] += w/max(.002,distance)**2
    return normalize(ws)

def mesh_object(name, coords, faces, uvs, weights):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(coords, [], [[v for v,_ in f] for f in faces]); mesh.update()
    obj = bpy.data.objects.new(name, mesh); bpy.context.scene.collection.objects.link(obj)
    layer = mesh.uv_layers.new()
    for poly, face in zip(mesh.polygons, faces):
        poly.use_smooth = True
        for li,(_,ti) in zip(poly.loop_indices,face): layer.data[li].uv = uvs[ti]
    for name in names: obj.vertex_groups.new(name=name)
    for vi,ws in enumerate(weights):
        for bi,w in ws: obj.vertex_groups[bi].add([vi],w,'REPLACE')
    return obj

def makehuman(path):
    factors = [scale]*3; mappings = []; reading = False; obj_file = None
    for line in path.read_text().splitlines():
        p = line.split()
        if not p or p[0].startswith('#'): continue
        if p[0] == 'obj_file': obj_file = p[1]
        elif p[0] in ('x_scale','y_scale','z_scale'):
            axis = 'xyz'.index(p[0][0]); factors[axis] = abs(points[int(p[1])][axis]-points[int(p[2])][axis])/float(p[3])
        elif p[0] == 'verts': reading = True
        elif reading:
            if len(p) == 9:
                value = sum((points[int(p[i])]*float(p[i+3]) for i in range(3)),Vector())
                value += Vector(tuple(float(p[i+6])*factors[i] for i in range(3)))
                mappings.append(value)
            elif len(p) == 1 and p[0].isdigit(): mappings.append(points[int(p[0])].copy())
            else: reading = False
    uv, faces, count = [], [], 0
    for line in (path.parent/obj_file).read_text().splitlines():
        p = line.split()
        if not p: continue
        if p[0] == 'v': count += 1
        elif p[0] == 'vt': uv.append(tuple(map(float,p[1:3])))
        elif p[0] == 'f': faces.append([(int(v.split('/')[0])-1,int(v.split('/')[1])-1) for v in p[1:]])
    assert count == len(mappings), (path.name,count,len(mappings))
    return mesh_object(path.stem,mappings,faces,uv,[transfer_weights(v) for v in mappings])

def convert(v):
    # Quaternius FBX imported by Blender: Z-up, +Y forward, left negative X.
    # FOC canonical: Y-up, +Z forward, left positive X. Proper rotation, no reflection.
    return Vector((-v.x,v.z,v.y))

def qbone(name):
    side = 'L' if name.endswith('_l') else 'R'
    for prefix,target in [('upperarm','UpperArm_'),('lowerarm','LowerArm_'),('hand','Hand_'),
                          ('index','Hand_'),('middle','Hand_'),('pinky','Hand_'),('ring','Hand_'),
                          ('thumb','Hand_'),('thigh','UpperLeg_'),('calf','LowerLeg_'),
                          ('foot','Foot_'),('ball','Foot_')]:
        if name.startswith(prefix): return target+side
    if name.startswith('clavicle'): return 'Chest'
    return {'root':'Root','pelvis':'Pelvis','spine_01':'Spine','spine_02':'Chest',
            'spine_03':'Chest','neck_01':'Neck','Head':'Head'}[name]

def quaternius(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False)
    imported = set(bpy.data.objects)-before
    arm = next(o for o in imported if o.type == 'ARMATURE')
    # Ranger FBXs also contain fantasy bracers / duplicate belt options. Select
    # the explicitly requested garment by exact name, never unordered first mesh.
    sources = [o for o in imported if o.type == 'MESH' and o.name == path.stem]
    assert len(sources) == 1, (path.name, [o.name for o in imported])
    source = sources[0]
    sb = {b.name:convert(arm.matrix_world@b.head_local) for b in arm.data.bones}
    # Piecewise torso height correspondence avoids collapsing two source chest bones.
    levels = [(0,0),(sb['pelvis'].y,bp['Pelvis'].y),(sb['spine_01'].y,bp['Spine'].y),
              (sb['spine_02'].y,bp['Chest'].y),(sb['neck_01'].y,bp['Neck'].y),(1.8,1.8)]
    def torso(v):
        result = v.copy()
        for (a,c),(b,d) in zip(levels,levels[1:]):
            if a <= v.y <= b: result.y=c+(v.y-a)*(d-c)/(b-a); break
        result.z -= .03
        return result
    def transform(v,name):
        target=qbone(name)
        if target.startswith(('UpperArm','LowerArm','Hand','UpperLeg','LowerLeg','Foot')):
            side='l' if target.endswith('_L') else 'r'
            start, end, dest_end = {
                'UpperArm':('upperarm','lowerarm','LowerArm'),
                'LowerArm':('lowerarm','hand','Hand'),
                'Hand':('hand','middle_01','Hand'),
                'UpperLeg':('thigh','calf','LowerLeg'),
                'LowerLeg':('calf','foot','Foot'),
                'Foot':('foot','ball','Foot')}[target.split('_')[0]]
            a=sb[start+'_'+side]; delta=sb[end+'_'+side]-a
            dest=bp[target]
            if start=='hand': dd=(bp['Hand_'+side.upper()]-bp['LowerArm_'+side.upper()]).normalized()*.10
            elif start=='foot': dd=Vector((0,-.05,.14))
            else: dd=bp[dest_end+'_'+side.upper()]-dest
            rotation=delta.rotation_difference(dd)
            local=v-a; along=delta.normalized()*local.dot(delta.normalized())
            return dest+rotation@(local-along+along*(dd.length/delta.length))
        return torso(v)
    coords=[]; weights=[]
    for v in source.data.vertices:
        world=convert(source.matrix_world@v.co)
        values=[(source.vertex_groups[g.group].name,g.weight) for g in v.groups if g.weight>1e-7]
        total=sum(w for _,w in values)
        coords.append(sum((transform(world,n)*(w/total) for n,w in values),Vector()))
        ws=defaultdict(float)
        for n,w in values: ws[names.index(qbone(n))]+=w
        weights.append(normalize(ws))
    uv=[]; faces=[]
    for p in source.data.polygons:
        f=[]
        for li in p.loop_indices:
            f.append((source.data.loops[li].vertex_index,len(uv)))
            uv.append(tuple(source.data.uv_layers.active.data[li].uv))
        faces.append(f)
    obj=mesh_object(path.stem,coords,faces,uv,weights)
    for old in imported: bpy.data.objects.remove(old,do_unlink=True)
    obj.name=path.stem
    return obj

def export(obj, source_path):
    lods=[]
    for level,ratio in enumerate((1,.5,.22)):
        copy=obj.copy(); copy.data=obj.data.copy(); bpy.context.scene.collection.objects.link(copy)
        bpy.context.view_layer.objects.active=copy
        if level:
            mod=copy.modifiers.new('DonorLOD','DECIMATE'); mod.ratio=ratio
            bpy.ops.object.modifier_apply(modifier=mod.name)
        mesh=copy.data; mesh.calc_loop_triangles()
        part={k:[] for k in ('positions','normals','uv','triangles','boneIndices','boneWeights')}
        part['material']='Leather' if 'boot' in obj.name.lower() else 'ClothBlue'
        lookup={}
        for tri in mesh.loop_triangles:
            for li in tri.loops:
                vi=mesh.loops[li].vertex_index; v=mesh.vertices[vi]
                uv=mesh.uv_layers.active.data[li].uv
                normal=v.normal if v.normal.dot(tri.normal)>0 else tri.normal
                key=(vi,tuple(round(x,6) for x in uv),tuple(round(x,5) for x in normal))
                if key not in lookup:
                    lookup[key]=len(part['positions'])//3
                    part['positions']+=list(v.co); part['normals']+=list(normal); part['uv']+=list(uv)
                    ws=normalize({g.group:g.weight for g in v.groups})
                    ws += [(0,0)]*(4-len(ws))
                    part['boneIndices'] += [i for i,_ in ws]; part['boneWeights'] += [w for _,w in ws]
                part['triangles'].append(lookup[key])
        lods.append({'parts':[part]}); bpy.data.objects.remove(copy,do_unlink=True)
    payload={'formatVersion':1,'assetId':'CLTH_Donor_'+obj.name,'category':'Clothing','status':'Draft',
             'generator':'Blender '+bpy.app.version_string+' / Tools/Art/test_clothing_donors.py',
             'source':str(source_path.relative_to(ROOT)).replace('\\','/'),
             'sourceSha256':hashlib.sha256(source_path.read_bytes()).hexdigest(),
             'license':'CC0-1.0 donor; FOC fitting experiment','date':'2026-09-26',
             'revision':'donor-fit-test-r1-not-historical-art','bones':bones,'lods':lods}
    (OUT/(payload['assetId']+'.focmesh.json')).write_text(json.dumps(payload,separators=(',',':')),encoding='utf-8')
    return [len(l['parts'][0]['triangles'])//3 for l in lods]

bpy.ops.wm.read_factory_settings(use_empty=True)
reports=[]
paths=sorted((SOURCE/'Quaternius').glob('*.fbx'))+sorted((SOURCE/'clothes').glob('*/*.mhclo'))
assert len(paths)==11, 'Must test the exact eleven selected donors'
for path in paths:
    obj=quaternius(path) if path.suffix=='.fbx' else makehuman(path)
    assert all(math.isfinite(x) for v in obj.data.vertices for x in v.co)
    assert all(any(g.weight>0 for g in v.groups) for v in obj.data.vertices)
    assert all(-.2<v.co.y<1.9 and abs(v.co.x)<1.1 and abs(v.co.z)<.8 for v in obj.data.vertices), path.name
    lods=export(obj,path)
    adjacency=[set() for _ in obj.data.vertices]
    for edge in obj.data.edges:
        a,b=edge.vertices;adjacency[a].add(b);adjacency[b].add(a)
    pending=set(range(len(adjacency)));components=[]
    while pending:
        seed=min(pending);pending.remove(seed);component=[seed];queue=[seed]
        while queue:
            vi=queue.pop()
            for other in adjacency[vi]:
                if other in pending: pending.remove(other);component.append(other);queue.append(other)
        vv=[obj.data.vertices[i].co for i in component]
        components.append({'vertices':len(vv),'min':[min(v[i] for v in vv) for i in range(3)],'max':[max(v[i] for v in vv) for i in range(3)]})
    reports.append({'donor':path.stem,'status':'FIT_NUMERIC_PASS_NOT_VISUAL_ACCEPTANCE',
                    'vertices':len(obj.data.vertices),'lodTriangles':lods,
                    'uvPreserved':True,'canonicalBones':len(bones),'components':sorted(components,key=lambda c:-c['vertices']),
                    'weights':'source remapped' if path.suffix=='.fbx' else 'transferred from fitted hm08 body; no upstream rig weights supplied',
                    'unityRuntime':'NOT_RUN','mountedAnimation':'NOT_RUN','historicalStyling':'NOT_ADAPTED'})
    print('DONOR_FIT_NUMERIC_PASS',path.stem,lods,flush=True)
(REPORT/'donor-fit-results.json').write_text(json.dumps(reports,indent=2),encoding='utf-8')
print('ALL_11_DONORS_FITTED_DRAFT_ONLY',flush=True)

# Raw-donor comparison assemblies, only AFTER every selected garment was tested.
# Use the existing anatomical body/head, not the old procedural garment rescue.
# Visible body intersections are intentionally not masked in this fit diagnostic.
assemblies={
    'HasanRobe':['donitz_monk_robe','toigo_harem_pants','Male_Ranger_Feet_Boots'],
    'TunicAlternative':['rehmanpolanski_viking_tunic','toigo_harem_pants','culturalibre_male_boots'],
    'SipahiRanger':['Male_Ranger_Body','Male_Ranger_Arms','toigo_harem_pants','Male_Ranger_Feet_Boots'],
    'CebeliPeasant':['Male_Peasant_Body','Male_Peasant_Arms','toigo_harem_pants','Male_Ranger_Feet_Boots'],
    'PeasantLegs':['Male_Peasant_Body','Male_Peasant_Arms','Male_Peasant_Legs','culturalibre_male_boots'],
    'RangerLegs':['Male_Ranger_Body','Male_Ranger_Arms','Male_Ranger_Legs','Male_Ranger_Feet_Boots']}
head=json.loads((ROOT/'UnityProject/Assets/FOC/ArtSource/HistoricalSlice/Characters/HEAD_OttomanMale_01.focmesh.json').read_text())
for label,donors in assemblies.items():
    sources=[json.loads((OUT/('CLTH_Donor_'+name+'.focmesh.json')).read_text()) for name in donors]
    payload=dict(sources[0]);payload['assetId']='CHR_Donor_'+label
    payload['category']='ConsolidatedCharacter';payload['source']='RAW donor comparison: '+', '.join(donors)
    payload['sourceSha256']=hashlib.sha256(''.join(s['sourceSha256'] for s in sources).encode()).hexdigest()
    payload['lods']=[{'parts':canonical['lods'][i]['parts']+head['lods'][i]['parts']+
                     [p for s in sources for p in s['lods'][i]['parts']]} for i in range(3)]
    (OUT/(payload['assetId']+'.focmesh.json')).write_text(json.dumps(payload,separators=(',',':')),encoding='utf-8')
print('RAW_DONOR_COMPARISONS_NOT_HISTORICAL_ADAPTATIONS',flush=True)
