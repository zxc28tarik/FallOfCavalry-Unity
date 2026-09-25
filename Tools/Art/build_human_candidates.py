"""MakeHuman CC0 anatomy adapted to the shared FOC rig; DRAFT authoring output.

No legacy/proof mesh input. Blender performs real topology LOD reduction.
The common interchange is imported by HistoricalArtCandidatePipeline in Unity.
"""
import bpy
import bmesh
import json
import math
import hashlib
import random
import shutil
import sys
from pathlib import Path
from collections import defaultdict
from mathutils import Vector

REPO=Path(__file__).resolve().parents[2]
INPUT=REPO/'Artifacts/ArtInputs'
OUT=REPO/'UnityProject/Assets/FOC/ArtSource/HistoricalSlice/Characters'
OUT.mkdir(parents=True,exist_ok=True)
SYSTEM=REPO/'ArtSource/HistoricalSlice/Upstream/SystemAssets'
RESCUE='--hasan-rescue' in sys.argv
if str(Path(__file__).resolve().parent) not in sys.path:
    sys.path.insert(0,str(Path(__file__).resolve().parent))
for source,dest in [
    ('skins/young_caucasian_male/young_lightskinned_male_diffuse.png','Skin_D.png'),
    ('skins/middleage_caucasian_male/middleage_lightskinned_male_diffuse.png','SkinMature_D.png'),
    ('hair/short01/short01_diffuse.png','HairCards01_D.png'),
    ('hair/short02/short02_diffuse.png','HairCards02_D.png')]:
    shutil.copyfile(SYSTEM/source,OUT/dest)
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
    if name.startswith(('wrist','metacarpal','finger')):return 'Hand_'+s
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
    if RESCUE and name!='CHR_HasanAga_01':return
    if not name.startswith(('BODY_','HEAD_','CLTH_','ARM_','CHR_')):raise RuntimeError('Invalid human asset ID: '+name)
    lods=[]
    for level,ratio in enumerate((1,.48,.20)):
        outparts=[]
        for source,material in parts:
            obj=source.copy();obj.data=source.data.copy();bpy.context.scene.collection.objects.link(obj)
            bpy.context.view_layer.objects.active=obj;obj.select_set(True)
            if source.name.startswith('FacialHair') and level>0:
                stride=6 if level==1 else 28
                selected=[i for i in range(0,len(source.data.vertices),3*stride)]
                coords=[source.data.vertices[i+j].co.copy() for i in selected for j in range(3)]
                faces=[f for i in range(0,len(coords),3) for f in [(i,i+1,i+2),(i+2,i+1,i)]]
                data=bpy.data.meshes.new('SparseFiberLOD');data.from_pydata(coords,[],faces);data.update();obj.data=data
                obj.vertex_groups.clear()
                for bone_name in bone_names:obj.vertex_groups.new(name=bone_name)
                obj.vertex_groups[bone_index['Head']].add(list(range(len(coords))),1,'REPLACE')
            elif ratio<1 and len(obj.data.polygons)>100:
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
                    if source.name.startswith('short'):
                        normal=(v.co-Vector((0,1.65,.02))).normalized()
                        if normal.dot(tri.normal)<0:normal=-normal
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
        'source':'MakeHuman hm08 CC0 + system-assets CC0 skin/hair + original FOC garment adaptations'+(' + bodyparts05 RehmanPolanski CC0 beard/moustache' if name=='CHR_HasanAga_01' else ''),'sourceSha256':hashlib.sha256((INPUT/'base.obj').read_bytes()).hexdigest(),
        'sourcePackSha256':'b542127a8e25547c7c29c19f2d1d2adb9a664c80396ecd694095dbc8028a0107',
        'license':'CC0-1.0 anatomy; project-authored adaptations','date':'2026-09-25','revision':'14C-r3-surface-rescue' if name=='CHR_HasanAga_01' else '14C-r2-continuous-garments','bones':bones if rig else [],'lods':lods}
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

def fitted_hair(style,variant,folder=None,material=None):
    folder=folder or SYSTEM/'hair'/style
    mappings=[];factors=[scale]*3;reading=False
    obj_file=style+'.obj'
    for line in (folder/(style+'.mhclo')).read_text().splitlines():
        p=line.split()
        if not p or p[0].startswith('#'):continue
        if p[0]=='obj_file':obj_file=p[1]
        elif p[0] in ['x_scale','y_scale','z_scale']:
            axis='xyz'.index(p[0][0]);factors[axis]=abs(verts[int(p[1])][axis]-verts[int(p[2])][axis])/float(p[3])
        elif p[0]=='verts':reading=True
        elif reading and len(p)==9:
            position=sum((verts[int(p[i])]*float(p[i+3]) for i in range(3)),Vector())
            position+=Vector(tuple(float(p[i+6])*factors[i] for i in range(3)))
            fac=max(0,1-abs(position.y-1.61)/.12);position.x*=1+(variant-2)*.045*fac;position.z+=(variant-2)*.008*fac
            mappings.append(position)
    tex=[];polys=[]
    for line in (folder/obj_file).read_text().splitlines():
        p=line.split()
        if not p:continue
        if p[0]=='vt':tex.append(tuple(map(float,p[1:3])))
        elif p[0]=='f':polys.append([(int(v.split('/')[0])-1,int(v.split('/')[1])-1) for v in p[1:]])
    if max(i for f in polys for i,_ in f)>=len(mappings):raise RuntimeError('Hair fitting map incomplete')
    # Double-sided cards in one material/renderer, not transparent object sorting.
    polys=polys+[list(reversed(f)) for f in polys]
    data=bpy.data.meshes.new(style);data.from_pydata(mappings,[],[[i for i,_ in f] for f in polys]);data.update()
    obj=bpy.data.objects.new(style,data);bpy.context.scene.collection.objects.link(obj)
    for name in bone_names:obj.vertex_groups.new(name=name)
    obj.vertex_groups[bone_index['Head']].add(list(range(len(mappings))),1,'REPLACE')
    uv=data.uv_layers.new()
    for poly,face in zip(data.polygons,polys):
        for li,(_,ti) in zip(poly.loop_indices,face):uv.data[li].uv=tex[ti]
    return obj,material or 'HairCards'+style[-2:]

def beard_strands(variant):
    rng=random.Random(1400+variant);coords=[];polys=[]
    # Short tapered fibers follow the actual jaw/cheek surface. No opaque beard mask
    # and no inflated chunk occupying the mouth. Variant 3 is moustache-only.
    source=make_subset('Follicles',lambda p:1.535<p.y<1.61 and p.z>.041 and (p.y<1.573 or abs(p.x)>.025),head_variant=variant)[0]
    source.data.calc_loop_triangles()
    for triangle in source.data.loop_triangles:
        centroid=sum((source.data.vertices[i].co for i in triangle.vertices),Vector())/3
        if variant==3 and (centroid.y<1.587 or abs(centroid.x)>.028):continue
        if triangle.normal.z<-.2:continue
        expected=triangle.area*100000;samples=int(expected)+(1 if rng.random()<expected%1 else 0)
        for _ in range(samples):
            a=rng.random();b=rng.random()
            if a+b>1:a=1-a;b=1-b
            points=[source.data.vertices[i].co for i in triangle.vertices]
            root=points[0]+(points[1]-points[0])*a+(points[2]-points[0])*b+triangle.normal*.0004
            direction=(Vector((root.x*.7,-.9,.1))+triangle.normal*.3).normalized()
            length=(.004 if variant in [2,3] else .008 if variant==1 else .012)*rng.uniform(.65,1.15)
            axis=direction.cross(triangle.normal)
            if axis.length<.001:axis=Vector((1,0,0))
            axis.normalize();start=len(coords);width=.00065
            coords.extend([root-axis*width,root+axis*width,root+direction*length])
            polys.extend([(start,start+1,start+2),(start+2,start+1,start)])
    data=bpy.data.meshes.new('FacialHair');data.from_pydata(coords,[],polys);data.update();obj=bpy.data.objects.new('FacialHair',data);bpy.context.scene.collection.objects.link(obj)
    for name in bone_names:obj.vertex_groups.new(name=name)
    if coords:obj.vertex_groups[bone_index['Head']].add(list(range(len(coords))),1,'REPLACE')
    return obj,'Hair'

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
    # One continuous anatomical shoulder/armpit/sleeve surface. The old independently
    # lofted sleeves left open armholes; overlapping caps would only hide that defect.
    obj,_=make_subset(name,lambda p:True,.012+extra,material)
    bm=bmesh.new();bm.from_mesh(obj.data)
    deform=bm.verts.layers.deform.verify()
    cuts=[((0,1.01,0),(0,1,0),False),((0,1.52,0),(0,1,0),True)]
    for side in ['L','R']:
        elbow=positions['LowerArm_'+side];wrist=positions['Hand_'+side]
        cuts.append((elbow.lerp(wrist,.92),(wrist-elbow).normalized(),True))
    for co,normal,outer in cuts:
        bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.000001,plane_co=co,plane_no=normal,clear_outer=outer,clear_inner=not outer)
    # Relax anatomical muscle contours into woven-cloth drape; keep openings fixed.
    interior=[v for v in bm.verts if not v.is_boundary]
    for _ in range(6):bmesh.ops.smooth_vert(bm,verts=interior,factor=.45,use_axis_x=True,use_axis_y=True,use_axis_z=True)
    # Extend the actual waist boundary, not a disconnected elliptical skirt.
    waist=[v for v in bm.verts if v.is_boundary and abs(v.co.y-1.01)<.00001 and abs(v.co.x)<.30]
    waist.sort(key=lambda v:math.atan2(v.co.x,v.co.z))
    if len(waist)<12:raise RuntimeError('Continuous garment waist boundary missing')
    previous=waist
    uv=bm.loops.layers.uv.active
    for ring in range(1,7 if skirt else 3):
        t=ring/(6 if skirt else 2);y=1.01-t*(.40 if skirt else .15)
        current=[]
        for i,original in enumerate(waist):
            angle=math.atan2(original.co.x,original.co.z)
            rx=.24+extra+t*.065;rz=.145+extra+t*.045
            target=Vector((rx*math.sin(angle)*(1+.018*math.cos(angle*16)),y,rz*math.cos(angle)))
            v=bm.verts.new(original.co.lerp(target,t));v.co.y=y
            leg='UpperLeg_L' if v.co.x>0 else 'UpperLeg_R'
            # Split skirt follows thighs progressively; not a rigid pelvis cone.
            v[deform][bone_index['Pelvis']]=1-t*.65;v[deform][bone_index[leg]]=t*.65
            current.append(v)
        for i in range(len(waist)):
            j=(i+1)%len(waist)
            # Functional front/back vent below upper thigh, opening toward the hem.
            mid=(current[i].co+current[j].co)*.5
            if skirt and ring>=4 and abs(mid.x)<.016:continue
            face=bm.faces.new((previous[i],previous[j],current[j],current[i]))
            if uv:
                for loop in face.loops:loop[uv].uv=(math.atan2(loop.vert.co.x,loop.vert.co.z)/(2*math.pi)+.5,loop.vert.co.y)
        previous=current
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    for face in bm.faces:
        center=face.calc_center_median()
        if center.y<1.009 and face.normal.dot(Vector((center.x,0,center.z)))<0:face.normal_flip()
    bm.to_mesh(obj.data);bm.free();obj.data.update()
    for p in obj.data.polygons:
        p.use_smooth=True
        # Metric UVs avoid anatomical atlas distortion turning mail links into
        # stretched hoops at the shoulders. Projection seams are confined to folds.
        axis=max(range(3),key=lambda i:abs(p.normal[i]))
        for li in p.loop_indices:
            v=obj.data.vertices[obj.data.loops[li].vertex_index].co
            obj.data.uv_layers.active.data[li].uv=(v.z,v.y) if axis==0 else (v.x,v.z) if axis==1 else (v.x,v.y)
    return [(obj,material)]

def boot_pair():
    result=[]
    for side in ['L','R']:
        foot=positions['Foot_'+side];knee=positions['LowerLeg_'+side]
        sections=[]
        # Broad closed toe box, welt/sole, instep, ankle and calf. No toe anatomy is
        # copied into the final visible leather surface.
        for y,rz,rx,forward in [(.018,.145,.061,.065),(.037,.147,.062,.065),(.065,.135,.058,.062),(.105,.096,.056,.025),(.15,.062,.057,0),(.23,.063,.061,-.008),(.34,.064,.065,-.005),(.355,.065,.067,-.005)]:
            x=foot.x+(knee.x-foot.x)*max(0,(y-.08)/.43)
            sections.append(((x,y,foot.z+forward),(1,0,0),(0,0,1),rx,rz))
        def skin(j,n,s=side):
            blend=max(0,min(1,(sections[j][0][1]-.07)/.16))
            return [('Foot_'+s,1-blend),('LowerLeg_'+s,blend)]
        part=tailored_surface('Boot'+side,sections,'Leather',skin,32)
        obj=part[0];bm=bmesh.new();bm.from_mesh(obj.data)
        bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001)
        # Bottom is closed, shaft opening intentionally remains open.
        edges=[e for e in bm.edges if e.is_boundary and all(v.co.y<.019 for v in e.verts)]
        bmesh.ops.holes_fill(bm,edges=edges,sides=0)
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(obj.data);bm.free()
        result.append(part)
    return result

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
    part=make_subset('Head'+str(variant),lambda p:p.y>=1.53 or (p.y>=1.50 and abs(p.x)<.065),material='SkinMature' if variant==4 else 'Skin',head_variant=variant)
    hair=fitted_hair('short01' if variant==2 else 'short02',variant)
    beard=beard_strands(variant)
    headparts=[part,hair,beard]+eyes
    export('HEAD_OttomanMale_0'+str(variant),'Head',headparts);heads.append(headparts)
# Clothing is a separate weighted shell fitted to the canonical anatomy, not a box.
shirt=garment('InnerGarment','Linen')
trousers=make_subset('Trousers',lambda p:.31<p.y<1.02 and abs(p.x)<.31,.025,'ClothBlue')
for v in trousers[0].data.vertices:
    if v.co.y>.87:
        # Tuck the waistband under the connected coat; the previous equally
        # inflated layers intersected and produced the jagged metallic waist line.
        tuck=1-.24*min(1,(v.co.y-.87)/.11);v.co.x*=tuck;v.co.z*=tuck
    if v.co.y<.47:
        side='L' if v.co.x>0 else 'R';foot=positions['Foot_'+side];knee=positions['LowerLeg_'+side]
        center=Vector((foot.x+(knee.x-foot.x)*max(0,(v.co.y-.08)/.43),v.co.y,foot.z-.005))
        radial=v.co-center;fac=max(0,min(1,(.47-v.co.y)/.14))
        if radial.length>.055:v.co=center+radial.lerp(radial.normalized()*.055,fac)
boots=boot_pair()
coat=garment('Kaftan','ClothRed',.018,True)
mail=garment('MailShirt','Mail',.014,True)
export('CLTH_InnerGarment_01','Clothing',shirt+[trousers]+boots)
export('CLTH_LightSoldier_01','Clothing',coat+[trousers]+boots)
export('CLTH_Infantry_01','Clothing',coat+[trousers]+boots)
export('ARM_OttomanMail_01','BodyArmor',mail)
exposed=[]
for side in ['L','R']:
    sign=1 if side=='L' else -1
    part=make_subset('Hand'+side,lambda p:sign*p.x>.40 and .90<p.y<1.25)
    obj=part[0];bm=bmesh.new();bm.from_mesh(obj.data)
    elbow=positions['LowerArm_'+side];wrist=positions['Hand_'+side]
    bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.000001,plane_co=elbow.lerp(wrist,.89),plane_no=(wrist-elbow).normalized(),clear_inner=True)
    bm.to_mesh(obj.data);bm.free();obj.data.update();exposed.append(part)
for i,label in enumerate(['Sipahi','Cebeli','Tufekci','HasanAga']):
    # Draft assemblies intentionally not activated in the production catalog.
    parts=[(part[0],'SkinMature' if i==3 else 'Skin') for part in exposed]+[trousers]+boots+heads[i]
    if label in ['Sipahi','HasanAga']:parts+=mail
    else:parts+=coat
    if label=='HasanAga':
        from rescue_hasan_surface import build
        parts=build(globals())
    export('CHR_'+label+'_01','ConsolidatedCharacter',parts)
print('FOC_HUMAN_CANDIDATES_DRAFT_ONLY',flush=True)
