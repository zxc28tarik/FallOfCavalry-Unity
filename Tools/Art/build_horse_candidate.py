"""CC0 anatomical horse -> deterministic FOC mesh interchange, never proof geometry.

Run using Blender --background --factory-startup --disable-autoexec --python.
Export retains real surface topology, UVs and source body skin weights; repairs
unweighted eyes/mane/tail and maps the source skeleton to the FOC mount contract.
All output remains Draft until Windows-player visual/deformation QA is accepted.
"""
import bpy
import hashlib
import json
from pathlib import Path
from mathutils import Vector

REPO = Path(__file__).resolve().parents[2]
INPUT = REPO / 'Artifacts/ArtInputs/riggedHorse.blend'
OUT = REPO / 'UnityProject/Assets/FOC/ArtSource/HistoricalSlice/Mounts'
OUT.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(INPUT), load_ui=False, use_scripts=False)
arm = bpy.data.objects['Armature']
body = bpy.data.objects['Plane']
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
# Source is Z-up, forward -Y. Normalize to an approximately 1.5 m wither height.
points = [body.matrix_world @ v.co for v in body.data.vertices]
bottom = min(p.z for p in points)
center_y = (min(p.y for p in points) + max(p.y for p in points)) / 2
SCALE = .215

def convert(p):
    return Vector((p.x * SCALE, (p.z - bottom) * SCALE, -(p.y - center_y) * SCALE))

mapping = {
    'Bone':'MountPelvis', 'Bone.001':'MountNeck', 'Bone.002':'MountHead',
    'Bone.001_L':'Ear_L', 'Bone.001_R':'Ear_R',
    'Bone_L':'FrontLeg_L', 'Bone_L.001':'FrontLowerLeg_L', 'Bone_L.002':'FrontHoof_L',
    'Bone_R':'FrontLeg_R', 'Bone_R.001':'FrontLowerLeg_R', 'Bone_R.002':'FrontHoof_R',
    'Bone_L.003':'BackLeg_R', 'Bone_L.004':'BackLowerLeg_R', 'Bone_L.005':'BackHoof_R',
    'Bone_R.003':'BackLeg_L', 'Bone_R.004':'BackLowerLeg_L', 'Bone_R.005':'BackHoof_L',
    'Bone.003':'Tail_01', 'Bone.004':'Tail_02'
}
positions = {mapping[b.name]: convert(arm.matrix_world @ b.head_local) for b in arm.data.bones}
positions['MountRoot'] = Vector((0,0,0))
positions['MountSpine'] = positions['MountNeck'].lerp(positions['MountPelvis'], .4)
parents = {'MountRoot':'', 'MountPelvis':'MountRoot', 'MountSpine':'MountPelvis'}
for b in arm.data.bones:
    name = mapping[b.name]
    if name == 'MountPelvis': continue
    parents[name] = mapping[b.parent.name] if b.parent else 'MountPelvis'
parents['MountNeck'] = 'MountSpine'
parents['FrontLeg_L'] = parents['FrontLeg_R'] = 'MountSpine'
# Seat location is expressed in mount coordinates, not a hard-coded world offset.
positions['Socket_Rider'] = Vector((0,1.54,-.12))
parents['Socket_Rider'] = 'MountSpine'
bone_names = ['MountRoot','MountPelvis','MountSpine'] + [n for n in positions if n not in {'MountRoot','MountPelvis','MountSpine'}]
bone_indices = {n:i for i,n in enumerate(bone_names)}
rig = [{'name':n,'parent':parents[n], 'position':list(positions[n])} for n in bone_names]

def texture(source, target):
    img = bpy.data.images[source]
    if not img.packed_file or img.size[0] == 0: raise RuntimeError('Missing licensed packed texture: ' + source)
    img.filepath_raw = str(OUT / target); img.file_format = 'PNG'; img.save()

texture('HorseMain4k00.png','HorseCoat_D.png')
texture('HorseMain4k00Norm00.p','HorseCoat_N.png')
texture('Hair12Main2k.png','HorseHair_D.png')
texture('eye_texture.bmp.001','HorseEye_D.png')

def group_weights(obj, vertex, world):
    weights = {}
    for g in vertex.groups:
        source = obj.vertex_groups[g.group].name
        if source in mapping:
            name = mapping[source]; weights[name] = weights.get(name,0) + g.weight
    # Original source left hair and eyes without weights. Explicit anatomical repair.
    if not weights:
        if obj.name.startswith('Sphere'): weights={'MountHead':1.0}
        elif obj.name == 'BezierCurve':
            t=max(0,min(1,(1.5-world.y)/1.1)); weights={'Tail_01':1-t,'Tail_02':t}
        else:
            t=max(0,min(1,(world.z-.4)/.8)); weights={'MountNeck':1-t,'MountHead':t}
    pairs=sorted(weights.items(),key=lambda x:(-x[1],x[0]))[:4]
    total=sum(v for _,v in pairs)
    if total <= 0: raise RuntimeError('Unweighted horse vertex')
    return [(bone_indices[n],w/total) for n,w in pairs]

# Apply all transforms without evaluating the armature pose. Make the repaired
# weights actual vertex groups before decimation, so LOD interpolation retains skin.
parts=[]
for obj in meshes:
    material = 'HorseCoat' if obj == body else 'HorseHair' if obj.name.startswith('Bezier') else 'HorseEye'
    source_coords=[obj.matrix_world @ v.co for v in obj.data.vertices]
    weighted=[group_weights(obj,v,convert(source_coords[v.index])) for v in obj.data.vertices]
    copy=obj.copy();copy.data=obj.data.copy();bpy.context.scene.collection.objects.link(copy)
    copy.parent=None;copy.matrix_world.identity();copy.modifiers.clear();copy.vertex_groups.clear()
    for name in bone_names: copy.vertex_groups.new(name=name)
    for v,p,ws in zip(copy.data.vertices,source_coords,weighted):
        # Keep interchange in Unity coordinates; Blender is used for topology/LOD.
        v.co=convert(p)
        for bi,w in ws: copy.vertex_groups[bi].add([v.index],w,'REPLACE')
    parts.append((copy,material))

def export_part(obj, material, ratio):
    temp=obj.copy();temp.data=obj.data.copy();bpy.context.scene.collection.objects.link(temp)
    bpy.context.view_layer.objects.active=temp;temp.select_set(True)
    if ratio<1:
        mod=temp.modifiers.new('SilhouetteLOD','DECIMATE');mod.ratio=ratio;mod.use_collapse_triangulate=True
        bpy.ops.object.modifier_apply(modifier=mod.name)
    mesh=temp.data
    for p in mesh.polygons: p.use_smooth=True
    mesh.calc_loop_triangles()
    vertices=[];normals=[];uv=[];indices=[];weights=[];triangles=[];lookup={}
    for tri in mesh.loop_triangles:
        face=[]
        for li in tri.loops:
            vi=mesh.loops[li].vertex_index;v=mesh.vertices[vi]
            tex=mesh.uv_layers.active.data[li].uv if mesh.uv_layers.active else Vector((0,0))
            normal=v.normal if v.normal.dot(tri.normal)>0 else tri.normal
            key=(vi,round(tex.x,6),round(tex.y,6),*(round(x,5) for x in normal))
            if key not in lookup:
                lookup[key]=len(vertices)//3
                vertices.extend(round(float(x),6) for x in v.co)
                normals.extend(round(float(x),6) for x in normal)
                uv.extend(round(float(x),6) for x in tex)
                ws=sorted([(g.group,g.weight) for g in v.groups if g.weight>0],key=lambda x:-x[1])[:4]
                total=sum(w for _,w in ws)
                if not total: raise RuntimeError('LOD lost skin weights')
                ws=[(i,w/total) for i,w in ws]+[(0,0)]*(4-len(ws))
                indices.extend(i for i,_ in ws);weights.extend(round(w,7) for _,w in ws)
            face.append(lookup[key])
        triangles.extend(face) # Axis conversion is a rotation, not a reflection.
    result={'material':material,'positions':vertices,'normals':normals,'uv':uv,'triangles':triangles,'boneIndices':indices,'boneWeights':weights}
    bpy.data.objects.remove(temp,do_unlink=True)
    return result

lods=[]
for level,ratio in enumerate((1,.52,.23)):
    parts_at_lod=[export_part(obj,mat,ratio) for obj,mat in parts]
    lods.append({'parts':parts_at_lod})
    print('FOC_HORSE_LOD',level,sum(len(p['triangles'])//3 for p in parts_at_lod),flush=True)

payload={'formatVersion':1,'assetId':'MNT_Horse_Anatolian_01','category':'Mount','status':'Draft',
    'generator':'Blender '+bpy.app.version_string+' / Tools/Art/build_horse_candidate.py',
    'source':'Lyndon Daniels / ChadM CC0 riggedHorse.blend',
    'sourceSha256':hashlib.sha256(INPUT.read_bytes()).hexdigest(),
    'license':'CC0-1.0','date':'2026-09-25','revision':'14C-r1','bones':rig,'lods':lods}
dest=OUT/'MNT_Horse_Anatolian_01.focmesh.json'
dest.write_text(json.dumps(payload,separators=(',',':')),encoding='utf-8')
print('FOC_HORSE_EXPORT',dest,flush=True)
# Preserve original input and its license alongside reproducible tools, outside
# Unity (Unity must not execute Blender automatically to reimport source files).
print('FOC_HORSE_SOURCE_SHA256',payload['sourceSha256'],flush=True)
