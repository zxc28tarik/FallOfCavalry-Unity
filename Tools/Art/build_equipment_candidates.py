"""Original profile/loft-authored historical equipment, no Unity proof primitives.
All meshes are DRAFT, with historical limitations recorded in source provenance.
"""
import bpy
import math
import json
import hashlib
from pathlib import Path
from mathutils import Vector
REPO=Path(__file__).resolve().parents[2]
OUT=REPO/'UnityProject/Assets/FOC/ArtSource/HistoricalSlice/Equipment'
OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)

def mesh(name,verts,faces,material):
    data=bpy.data.meshes.new(name);data.from_pydata(verts,[],faces);data.update()
    obj=bpy.data.objects.new(name,data);bpy.context.scene.collection.objects.link(obj)
    for p in data.polygons:p.use_smooth=True
    uv=data.uv_layers.new()
    for p in data.polygons:
        for li in p.loop_indices:
            v=data.vertices[data.loops[li].vertex_index].co
            uv.data[li].uv=(v.x*3+v.z,v.y*2+v.z)
    return obj,material

def loft(name,sections,material,segments=24,axis='y'):
    # Sections: coordinate, offset-a, offset-b, cross-section radius-a/radius-b.
    verts=[];faces=[]
    for t,ca,cb,ra,rb in sections:
        for i in range(segments):
            a=2*math.pi*i/segments;x=ca+ra*math.cos(a);z=cb+rb*math.sin(a)
            verts.append((x,t,z) if axis=='y' else (x,z,t))
    for j in range(len(sections)-1):
        for i in range(segments):
            a=j*segments+i;b=j*segments+(i+1)%segments
            faces.append((a,b,b+segments,a+segments))
    faces.append(tuple(reversed(range(segments))));faces.append(tuple((len(sections)-1)*segments+i for i in range(segments)))
    return mesh(name,verts,faces,material)

def extrude(name,profile,thickness,material):
    verts=[(x,y,z) for z in [-thickness/2,thickness/2] for x,y in profile];n=len(profile)
    faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]
    for i in range(n):faces.append((i,(i+1)%n,(i+1)%n+n,i+n))
    return mesh(name,verts,faces,material)

def tube(name,path,radius,material):
    verts=[];faces=[];segments=8
    for j,p in enumerate(path):
        tangent=Vector(path[min(j+1,len(path)-1)])-Vector(path[max(0,j-1)])
        tangent.normalize();side=tangent.cross(Vector((0,1,0)))
        if side.length<.01:side=tangent.cross(Vector((0,0,1)))
        side.normalize();other=tangent.cross(side).normalized()
        for i in range(segments):
            a=2*math.pi*i/segments;v=Vector(p)+radius*(math.cos(a)*side+math.sin(a)*other);verts.append(v)
    for j in range(len(path)-1):
        for i in range(segments):a=j*segments+i;b=j*segments+(i+1)%segments;faces.append((a,b,b+segments,a+segments))
    return mesh(name,verts,faces,material)

def export(name,category,parts):
    lods=[]
    for ratio in [1,.55,.25]:
        out=[]
        for source,material in parts:
            obj=source.copy();obj.data=source.data.copy();bpy.context.scene.collection.objects.link(obj);bpy.context.view_layer.objects.active=obj
            if ratio<1 and len(obj.data.polygons)>80:
                mod=obj.modifiers.new('SilhouetteLOD','DECIMATE');mod.ratio=ratio;mod.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=mod.name)
            data=obj.data;data.calc_loop_triangles();part={'material':material,'positions':[],'normals':[],'uv':[],'triangles':[],'boneIndices':[],'boneWeights':[]}
            # Equipment hard edges keep per-loop normals, no spherical shading on blades.
            for tri in data.loop_triangles:
                indices=[]
                for li in tri.loops:
                    v=data.vertices[data.loops[li].vertex_index];indices.append(len(part['positions'])//3)
                    part['positions'].extend(round(float(x),6) for x in v.co)
                    normal=v.normal if v.normal.dot(tri.normal)>0 else tri.normal
                    part['normals'].extend(round(float(x),6) for x in normal)
                    part['uv'].extend(round(float(x),6) for x in data.uv_layers.active.data[li].uv)
                part['triangles'].extend(indices)
            out.append(part);bpy.data.objects.remove(obj,do_unlink=True)
        lods.append({'parts':out})
    payload={'formatVersion':1,'assetId':name,'category':category,'status':'Draft','generator':'Blender '+bpy.app.version_string+' / Tools/Art/build_equipment_candidates.py','source':'Original FOC profile/loft surface authoring; references in ArtSource/HistoricalSlice/README.md','sourceSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'license':'Project-authored original mesh','date':'2026-09-25','revision':'14C-r1','bones':[],'lods':lods}
    (OUT/(name+'.focmesh.json')).write_text(json.dumps(payload,separators=(',',':')),encoding='utf-8')
    print('FOC_EQUIPMENT_EXPORT',name,[sum(len(p['triangles'])//3 for p in lod['parts']) for lod in lods],flush=True)

# Blade follows a restrained curve with a broadened upper cutting section and point.
blade=loft('KilicBlade',[(0,0,0,.021,.004),(.18,.005,0,.022,.0038),(.40,.03,0,.024,.0035),(.58,.075,0,.03,.003),(.69,.10,0,.028,.0025),(.78,.15,0,.001,.0004)],'Steel',8)
guard=extrude('KilicGuard',[(-.095,-.02),(-.105,.0),(-.07,.016),(-.025,.012),(0,.029),(.025,.012),(.07,.016),(.105,0),(.095,-.02),(.03,-.005),(-.03,-.005)],.016,'Steel')
grip=loft('KilicGrip',[(-.145,-.015,0,.024,.019),(-.125,-.01,0,.024,.018),(-.08,-.006,0,.017,.014),(-.015,0,0,.016,.013)],'Leather',16)
pommel=loft('KilicPommel',[(-.158,-.026,0,.009,.012),(-.145,-.016,0,.025,.021),(-.13,-.012,0,.024,.02)],'Steel',16)
export('WPN_Kilic_01','Weapon',[blade,guard,grip,pommel])
shaft=loft('LanceShaft',[(-1.1,0,0,.012,.012),(-.9,0,0,.015,.015),(0,0,0,.018,.018),(1.5,0,0,.012,.012),(1.6,0,0,.01,.01)],'Wood',16)
point=loft('LancePoint',[(1.5,0,0,.018,.014),(1.57,0,0,.021,.007),(1.68,0,0,.034,.006),(1.92,0,0,.001,.001)],'Steel',8)
binding=loft('LanceGrip',[(-.15,0,0,.02,.02),(.15,0,0,.02,.02)],'Leather',16)
export('WPN_Mizrak_01','Weapon',[shaft,point,binding])
# Matchlock anatomy: shaped stock, octagonal barrel, serpentine and separate ramrod.
stock=extrude('MatchlockStock',[(-.10,-.54),(.045,-.51),(.04,-.25),(.019,-.12),(.024,.64),(-.02,.64),(-.025,-.10),(-.075,-.23),(-.14,-.44)],.05,'Wood')
barrel=loft('OctagonalBarrel',[(-.10,0,.041,.018,.018),(.65,0,.041,.013,.013),(.76,0,.041,.014,.014)],'Steel',8)
ramrod=loft('Ramrod',[(-.05,0,-.03,.005,.005),(.74,0,-.03,.005,.005)],'Wood',10)
lock=tube('MatchSerpentine',[(.03,-.14,.035),(.045,-.07,.04),(.06,-.02,.025),(.054,.04,.014),(.033,.063,.015)],.009,'Steel')
match=tube('SlowMatch',[(.03,.04,.014),(.06,.075,.017),(.08,.09,.017),(.1,.06,.018)],.004,'Linen')
export('WPN_FitilliTufek_01','Weapon',[stock,barrel,ramrod,lock,match])
shield=loft('WovenShield',[(0,0,0,.26,.26),(.035,0,0,.25,.25),(.075,0,0,.19,.19),(.105,0,0,.08,.08),(.11,0,0,.003,.003)],'Wood',64,axis='z')
rim=loft('ShieldRim',[(-.012,0,0,.26,.26),(0,0,0,.271,.271),(.016,0,0,.264,.264)],'Leather',64,axis='z')
boss=loft('ShieldBoss',[(.092,0,0,.08,.08),(.11,0,0,.075,.075),(.17,0,0,.012,.012),(.19,0,0,.001,.001)],'Steel',32,axis='z')
export('SHD_Kalkan_01','Shield',[shield,rim,boss])
for label,top,material in [('Sipahi',.25,'Steel'),('Cebeli',.16,'ClothRed'),('Tufekci',.22,'Linen')]:
    cap=loft(label+'Cap',[(-.045,0,-.025,.102,.115),(0,0,-.022,.108,.118),(.09,0,-.02,.095,.10),(top*.85,0,-.02,.05,.06),(top,0,-.02,.007,.007)],material,48)
    parts=[cap]
    if label!='Tufekci':
        for row in range(5):
            path=[((.12+.002*math.sin(i)) * math.cos(i*math.pi/32),-.025+row*.014+.013*math.sin(i*math.pi/32),-.022+.132*math.sin(i*math.pi/32)) for i in range(65)]
            parts.append(tube('WrappedBand'+str(row),path,.012,'Linen'))
    else:
        flap=extrude('RearFeltFold',[(-.09,-.03),(.09,-.03),(.085,-.20),(.045,-.25),(-.05,-.25),(-.085,-.20)],.012,'Linen')
        for v in flap[0].data.vertices:v.co.z-=.12
        parts.append(flap)
    export('HDG_'+label+'_01','Headgear',parts)
bag=loft('AmmunitionPouch',[(-.1,0,0,.06,.022),(-.09,0,0,.08,.04),(.07,0,0,.077,.038),(.10,0,0,.06,.025)],'Leather',24)
export('AUX_TufekMuhimmat_01','Auxiliary',[bag])
# Draft tack fit is an explicit review requirement; never hard-coded onto proof horse.
blanket=loft('SaddleBlanket',[(-.48,0,1.42,.28,.05),(-.40,0,1.49,.31,.045),(.30,0,1.48,.30,.045),(.39,0,1.42,.25,.04)],'ClothRed',32,axis='z')
saddle=loft('SaddleSeat',[(-.36,0,1.57,.19,.075),(-.25,0,1.54,.20,.06),(.12,0,1.54,.17,.05),(.24,0,1.65,.16,.08)],'Leather',24,axis='z')
parts=[blanket,saddle]
for sign in [-1,1]:
    parts.append(tube('StirrupLeather',[(sign*.16,1.55,0),(sign*.36,1.30,0),(sign*.43,.86,.06)],.012,'Leather'))
    parts.append(tube('StirrupIron',[(sign*.43,.89,.02),(sign*.49,.74,.02),(sign*.49,.72,.17),(sign*.40,.72,.17),(sign*.40,.74,.02),(sign*.43,.89,.02)],.011,'Steel'))
    parts.append(tube('Rein',[(sign*.08,1.61,.10),(sign*.22,1.64,.45),(sign*.17,1.46,.95),(sign*.12,1.28,1.35)],.006,'Leather'))
export('HAR_SipahiHarness_01','Harness',parts)
