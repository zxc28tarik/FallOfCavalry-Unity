"""MakeHuman CC0 anatomy adapted to the shared FOC rig; DRAFT authoring output.

No legacy/proof mesh input. Blender performs real topology LOD reduction.
The common interchange is imported by HistoricalArtCandidatePipeline in Unity.
"""
import bpy
import json
import math
import hashlib
from pathlib import Path
from collections import defaultdict
from mathutils import Vector

REPO=Path(__file__).resolve().parents[2]
INPUT=REPO/'Artifacts/ArtInputs'
OUT=REPO/'UnityProject/Assets/FOC/ArtSource/HistoricalSlice/Characters'
OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
verts=[];uvs=[];faces=[];group=''
for line in (INPUT/'base.obj').read_text().splitlines():
    p=line.split()
    if not p: continue
    if p[0]=='v': verts.append(Vector(tuple(map(float,p[1:4]))))
    elif p[0]=='vt': uvs.append(tuple(map(float,p[1:3])))
    elif p[0]=='g': group=p[1]
    elif p[0]=='f' and group=='body': faces.append([(int(a.split('/')[0])-1,int(a.split('/')[1])-1) for a in p[1:]])
for filename in ['male-young.target','male-build.target']:
    for line in (INPUT/filename).read_text().splitlines():
        p=line.split()
        if not p or p[0].startswith('#'):continue
        verts[int(p[0])]+=Vector(tuple(map(float,p[1:4])))
body_ids={i for face in faces for i,_ in face}
bottom=min(verts[i].y for i in body_ids);height=max(verts[i].y for i in body_ids)-bottom
scale=1.76/height
verts=[Vector((v.x*scale,(v.y-bottom)*scale,v.z*scale)) for v in verts]
rigsrc=json.loads((INPUT/'default.mhskel').read_text())
def joint(source,end='head'):
    inds=rigsrc['joints'][rigsrc['bones'][source][end]]
    return sum((verts[i] for i in inds),Vector())/len(inds)
definitions=[('Root','',None),('Pelvis','Root','root'),('Spine','Pelvis','spine04'),('Chest','Spine','spine02'),('Neck','Chest','neck01'),('Head','Neck','head')]
for s in ['L','R']:
    for n,p,source in [('UpperArm','Chest','upperarm01'),('LowerArm','UpperArm_'+s,'lowerarm01'),('Hand','LowerArm_'+s,'wrist'),('UpperLeg','Pelvis','upperleg01'),('LowerLeg','UpperLeg_'+s,'lowerleg01'),('Foot','LowerLeg_'+s,'foot')]:
        definitions.append((n+'_'+s,p,source+'.'+s))
bones=[{'name':n,'parent':p,'position':list(joint(src) if src else Vector())} for n,p,src in definitions]
bone_names=[b['name'] for b in bones];bone_index={n:i for i,n in enumerate(bone_names)}
positions={b['name']:Vector(b['position']) for b in bones}
weights=defaultdict(lambda:defaultdict(float))
def remap(name):
    s='L' if name.endswith('.L') else 'R'
    if name.startswith(('upperarm','shoulder')):return 'UpperArm_'+s
    if name.startswith('lowerarm'):return 'LowerArm_'+s
    if name.startswith(('wrist','finger')):return 'Hand_'+s
    if name.startswith('upperleg'):return 'UpperLeg_'+s
    if name.startswith('lowerleg'):return 'LowerLeg_'+s
    if name.startswith(('foot','toe')):return 'Foot_'+s
    if name.startswith(('pelvis','root','spine05')):return 'Pelvis'
    if name.startswith(('spine04','spine03')):return 'Spine'
    if name.startswith(('spine','clavicle','breast')):return 'Chest'
    if name.startswith('neck'):return 'Neck'
    return 'Head'
for name,entries in json.loads((INPUT/'default_weights.mhw').read_text())['weights'].items():
    for vi,w in entries: weights[vi][bone_index[remap(name)]]+=w
print('FOC_HUMAN_ANATOMY',json.dumps(bones),flush=True)

def make_subset(name,predicate,offset=0,material='Skin',build=1,head_variant=0):
    selected=[f for f in faces if predicate(sum((verts[i] for i,_ in f),Vector())/len(f))]
    used=sorted({i for f in selected for i,_ in f});remapverts={v:i for i,v in enumerate(used)}
    coords=[]
    for i in used:
        v=verts[i].copy()
        if head_variant and v.y>1.48:
            fac=max(0,1-abs(v.y-1.61)/.12)
            v.x*=1+(head_variant-2)*.045*fac
            v.z+=(head_variant-2)*.008*fac
        elif v.y<1.48:
            v.x*=build;v.z*=build
        coords.append(v)
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(coords,[],[[remapverts[i] for i,_ in f] for f in selected]);mesh.update()
    obj=bpy.data.objects.new(name,mesh);bpy.context.scene.collection.objects.link(obj)
    for n in bone_names:obj.vertex_groups.new(name=n)
    for index,source in enumerate(used):
        ws=sorted(weights[source].items(),key=lambda p:-p[1])[:4];total=sum(w for _,w in ws)
        if total<.0001:ws=[(bone_index['Pelvis'],1)];total=1
        for bi,w in ws:obj.vertex_groups[bi].add([index],w/total,'REPLACE')
    layer=mesh.uv_layers.new()
    for poly,face in zip(mesh.polygons,selected):
        poly.use_smooth=True
        for li,(_,ti) in zip(poly.loop_indices,face):layer.data[li].uv=uvs[ti]
    if offset:
        cached_normals=[v.normal.copy() for v in mesh.vertices]
        for v,normal in zip(mesh.vertices,cached_normals):
            # Anatomical garment surface with restrained folds, not cylindrical limbs.
            fold=.0025*math.sin(v.co.y*110+v.co.x*23)*math.sin(v.co.z*47)
            v.co+=normal*(offset+fold)
    return obj,material

def export(name,category,parts,rig=True):
    lods=[]
    for level,ratio in enumerate((.60,.30,.12)):
        outparts=[]
        for source,material in parts:
            obj=source.copy();obj.data=source.data.copy();bpy.context.scene.collection.objects.link(obj)
            bpy.context.view_layer.objects.active=obj;obj.select_set(True)
            if ratio<1 and len(obj.data.polygons)>100:
                mod=obj.modifiers.new('SilhouetteLOD','DECIMATE');mod.ratio=ratio;mod.use_collapse_triangulate=True
                bpy.ops.object.modifier_apply(modifier=mod.name)
            mesh=obj.data;mesh.calc_loop_triangles()
            data={'material':material,'positions':[],'normals':[],'uv':[],'triangles':[],'boneIndices':[],'boneWeights':[]};lookup={}
            for tri in mesh.loop_triangles:
                face=[]
                for li in tri.loops:
                    vi=mesh.loops[li].vertex_index;v=mesh.vertices[vi]
                    tex=mesh.uv_layers.active.data[li].uv if mesh.uv_layers.active else Vector((v.co.x,v.co.y))
                    normal=v.normal if v.normal.dot(tri.normal)>0 else tri.normal
                    key=(vi,round(tex.x,6),round(tex.y,6),*(round(x,5) for x in normal))
                    if key not in lookup:
                        lookup[key]=len(data['positions'])//3
                        data['positions'].extend(round(float(x),6) for x in v.co)
                        data['normals'].extend(round(float(x),6) for x in normal)
                        data['uv'].extend(round(float(x),6) for x in tex)
                        ws=sorted([(g.group,g.weight) for g in v.groups if g.weight>0],key=lambda x:-x[1])[:4]
                        if not ws:ws=[(bone_index['Chest'],1)]
                        total=sum(w for _,w in ws);ws=[(i,w/total) for i,w in ws]+[(0,0)]*(4-len(ws))
                        data['boneIndices'].extend(i for i,_ in ws);data['boneWeights'].extend(round(w,7) for _,w in ws)
                    face.append(lookup[key])
                data['triangles'].extend(face)
            outparts.append(data);bpy.data.objects.remove(obj,do_unlink=True)
        lods.append({'parts':outparts})
    payload={'formatVersion':1,'assetId':name,'category':category,'status':'Draft','generator':'Blender '+bpy.app.version_string+' / Tools/Art/build_human_candidates.py',
        'source':'MakeHuman hm08 CC0 + original FOC garment adaptations','sourceSha256':hashlib.sha256((INPUT/'base.obj').read_bytes()).hexdigest(),
        'license':'CC0-1.0 anatomy; project-authored adaptations','date':'2026-09-25','revision':'14C-r1','bones':bones if rig else [],'lods':lods}
    (OUT/(name+'.focmesh.json')).write_text(json.dumps(payload,separators=(',',':')),encoding='utf-8')
    print('FOC_HUMAN_EXPORT',name,[sum(len(p['triangles'])//3 for p in l['parts']) for l in lods],flush=True)

def eye_part(name,center,radii,material):
    # Eyeballs are incidental anatomical detail, never the primary head silhouette.
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20,ring_count=12,location=(0,0,0))
    obj=bpy.context.object;obj.name=name
    for v in obj.data.vertices:v.co=Vector((v.co.x*radii[0],v.co.y*radii[1],v.co.z*radii[2]))+center
    for n in bone_names:obj.vertex_groups.new(name=n)
    obj.vertex_groups[bone_index['Head']].add(list(range(len(obj.data.vertices))),1,'REPLACE')
    for p in obj.data.polygons:p.use_smooth=True
    return obj,material

def tailored_surface(name,sections,material,segment_weights,segments=48,opening=0):
    coords=[];polys=[];uvs=[]
    for j,(center,axis_a,axis_b,ra,rb) in enumerate(sections):
        for i in range(segments+1):
            a=opening+(2*math.pi-2*opening)*i/segments
            fold=1+.025*math.cos(a*16+j*.4)
            coords.append(Vector(center)+Vector(axis_a)*ra*math.sin(a)*fold+Vector(axis_b)*rb*math.cos(a)*fold)
            uvs.append((i/segments,j/(len(sections)-1)))
    for j in range(len(sections)-1):
        for i in range(segments):
            a=j*(segments+1)+i;face=(a,a+segments+1,a+segments+2,a+1)
            center=(Vector(sections[j][0])+Vector(sections[j+1][0]))*.5
            radial=sum((coords[k] for k in face),Vector())*.25-center
            normal=(coords[face[1]]-coords[face[0]]).cross(coords[face[2]]-coords[face[0]])
            polys.append(tuple(reversed(face)) if normal.dot(radial)<0 else face)
    data=bpy.data.meshes.new(name);data.from_pydata(coords,[],polys);data.update();obj=bpy.data.objects.new(name,data);bpy.context.scene.collection.objects.link(obj)
    for n in bone_names:obj.vertex_groups.new(name=n)
    for j in range(len(sections)):
        for bone,weight in segment_weights(j,len(sections)):
            obj.vertex_groups[bone_index[bone]].add(list(range(j*(segments+1),(j+1)*(segments+1))),weight,'REPLACE')
    layer=data.uv_layers.new()
    for p in data.polygons:
        p.use_smooth=True
        for li in p.loop_indices:layer.data[li].uv=uvs[data.loops[li].vertex_index]
    return obj,material

def garment(name,material,extra=0,skirt=False):
    rings=[(.89,.205,.145),(1.0,.208,.149),(1.10,.206,.144),(1.20,.23,.154),(1.31,.248,.153),(1.40,.222,.135),(1.445,.17,.108),(1.49,.075,.076)]
    sections=[((0,y,.005),(1,0,0),(0,0,1),rx+extra,rz+extra) for y,rx,rz in rings]
    def torso(j,n):
        t=j/(n-1)
        return [('Pelvis',1-t*2),('Spine',t*2)] if t<.5 else [('Spine',2-2*t),('Chest',2*t-1)]
    parts=[tailored_surface(name+'Torso',sections,material,torso)]
    for side in ['L','R']:
        shoulder=positions['UpperArm_'+side];elbow=positions['LowerArm_'+side];wrist=positions['Hand_'+side]
        sections=[]
        for j in range(12):
            t=j/11;center=shoulder.lerp(elbow,t*2) if t<.5 else elbow.lerp(wrist,(t-.5)*2)
            direction=(elbow-shoulder if t<.5 else wrist-elbow).normalized();a=direction.cross(Vector((0,0,1))).normalized();b=direction.cross(a).normalized()
            r=(.078*(1-t)+.039*t)+extra
            sections.append((center,a,b,r*(1+.05*math.sin(t*math.pi*8)),r))
        def sleeve(j,n,s=side):
            t=j/(n-1);v=max(0,min(1,(t-.35)/.30));return [('UpperArm_'+s,1-v),('LowerArm_'+s,v)]
        parts.append(tailored_surface(name+'Sleeve'+side,sections,material,sleeve,32))
    if skirt:
        sections=[((0,y,0),(1,0,0),(0,0,1),rx+extra,rz+extra) for y,rx,rz in [(.60,.32,.20),(.70,.30,.19),(.82,.27,.18),(.93,.22,.155),(1.0,.21,.15)]]
        parts.append(tailored_surface(name+'Skirt',sections,material,lambda j,n:[('Pelvis',1)],48,.14))
    return parts

for label,build in [('Standard',1),('Lean',.94),('Stocky',1.07)]:
    part=make_subset('Body'+label,lambda p:p.y<1.51,build=build)
    export('BODY_OttomanMale_'+label,'Body',[part])
heads=[]
eyes=[]
for side in ['L','R']:
    center=joint('eye.'+side)
    eyes.append(eye_part('Eye'+side,center,(.013,.012,.012),'EyeWhite'))
    eyes.append(eye_part('Iris'+side,center+Vector((0,0,.011)),(.005,.005,.002),'Hair'))
for variant in range(1,5):
    part=make_subset('Head'+str(variant),lambda p:p.y>=1.50,head_variant=variant)
    hair=make_subset('Hair'+str(variant),lambda p:p.y>1.69 or (p.y>1.64 and p.z<.02),.004,'Hair',head_variant=variant)
    beard=make_subset('Beard'+str(variant),lambda p:1.535<p.y<1.60 and p.z>.035,.004+variant*.002,'Hair',head_variant=variant)
    headparts=[part,hair,beard]+eyes
    export('HEAD_OttomanMale_0'+str(variant),'Head',headparts);heads.append(headparts)
# Clothing is a separate weighted shell fitted to the canonical anatomy, not a box.
shirt=garment('InnerGarment','Linen')
trousers=make_subset('Trousers',lambda p:.17<p.y<1.02 and abs(p.x)<.31,.025,'ClothBlue')
boots=make_subset('Boots',lambda p:p.y<.32,.008,'Leather')
coat=garment('Kaftan','ClothRed',.018,True)
mail=garment('MailShirt','Mail',.014,True)
export('CLTH_InnerGarment_01','Clothing',shirt+[trousers,boots])
export('CLTH_LightSoldier_01','Clothing',coat+[trousers,boots])
export('CLTH_Infantry_01','Clothing',coat+[trousers,boots])
export('ARM_OttomanMail_01','BodyArmor',mail)
exposed=make_subset('ExposedHandsNeck',lambda p:(abs(p.x)>.48 and p.y<1.15) or 1.46<p.y<1.51)
for i,label in enumerate(['Sipahi','Cebeli','Tufekci','HasanAga']):
    # Draft assemblies intentionally not activated in the production catalog.
    parts=[exposed,trousers,boots]+heads[i]
    if label in ['Sipahi','HasanAga']:parts+=mail
    else:parts+=coat
    export('CHR_'+label+'_01','ConsolidatedCharacter',parts)
print('FOC_HUMAN_CANDIDATES_DRAFT_ONLY',flush=True)
