"""Original profile/loft-authored historical equipment, no Unity proof primitives.
All meshes are DRAFT, with historical limitations recorded in source provenance.
"""
import bpy
import math
import json
import hashlib
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
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
stock=loft('FacetedMatchlockStock',[
    (-.50,-.055,0,.056,.040),(-.48,-.055,0,.058,.042),(-.39,-.046,0,.062,.040),
    (-.28,-.025,0,.055,.034),(-.18,-.007,0,.033,.025),(-.10,0,0,.026,.026),
    (.02,0,0,.030,.027),(.14,0,0,.027,.024),(.40,0,0,.024,.022),(.76,0,0,.022,.020),
    (.97,0,0,.020,.018)],'Wood',12)
barrel=loft('OctagonalBarrel',[(-.02,0,.032,.020,.020),(.09,0,.032,.019,.019),(.50,0,.032,.016,.016),(.97,0,.032,.014,.014),(1.06,0,.032,.018,.018)],'Steel',8)
ramrod=loft('IronRamrod',[(.06,0,-.025,.004,.004),(1.02,0,-.025,.004,.004),(1.04,0,-.025,.006,.006)],'Steel',12)
lock=tube('MatchSerpentine',[(.032,-.095,.015),(.047,-.047,.014),(.050,.005,.036),(.039,.035,.06),(.028,.060,.06)],.0055,'Steel')
match=tube('SlowMatch',[(.03,.061,.052),(.042,.075,.063),(.063,.078,.071),(.079,.046,.069),(.081,.018,.060)],.0032,'Linen')
pan=loft('PrimingPan',[(.024,.027,.061,.023,.012),(.031,.027,.061,.020,.014),(.035,.027,.061,.022,.016)],'Steel',16,axis='z')
trigger=tube('TriggerLever',[(.012,-.08,-.025),(.016,-.13,-.043),(.008,-.18,-.038)],.0035,'Steel')
parts=[stock,barrel,ramrod,lock,match,pan,trigger]
for index,y in enumerate([.20,.54,.88]):
    parts.append(loft('BarrelBand'+str(index),[(y-.006,0,.009,.029,.042),(y+.006,0,.009,.029,.042)],'Steel',16))
for y in [-.47,-.30]:parts.append(loft('ButtFerrule'+str(y),[(y-.004,-.050 if y<-.4 else -.029,0,.060,.043),(y+.004,-.050 if y<-.4 else -.029,0,.060,.043)],'Steel',12))
rear_sight=extrude('RearSight',[(-.018,-.008),(-.018,.025),(-.004,.025),(-.004,.016),(.004,.016),(.004,.025),(.018,.025),(.018,-.008)],.005,'Steel')
for v in rear_sight[0].data.vertices:v.co=Vector((v.co.x,v.co.z+.02,v.co.y+.045))
parts.append(rear_sight)
export('WPN_FitilliTufek_01','Weapon',parts)
shield=loft('WovenShield',[(0,0,0,.26,.26),(.035,0,0,.25,.25),(.075,0,0,.19,.19),(.105,0,0,.08,.08),(.11,0,0,.003,.003)],'Wood',64,axis='z')
rim=loft('ShieldRim',[(-.012,0,0,.26,.26),(0,0,0,.271,.271),(.016,0,0,.264,.264)],'Leather',64,axis='z')
boss=loft('ShieldBoss',[(.092,0,0,.08,.08),(.11,0,0,.075,.075),(.17,0,0,.012,.012),(.19,0,0,.001,.001)],'Steel',32,axis='z')
grip=tube('ShieldBackGrip',[(-.09,0,-.012),(-.075,0,-.065),(.075,0,-.065),(.09,0,-.012)],.014,'Leather')
forearm=tube('ShieldForearmStrap',[(-.10,-.14,-.012),(-.09,-.15,-.07),(.09,-.15,-.07),(.10,-.14,-.012)],.012,'Leather')
export('SHD_Kalkan_01','Shield',[shield,rim,boss,grip,forearm])
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
# Fit the blanket to the licensed production horse surface, not the old proof's
# dimensions. The previous blanket and Rider socket were inside the actual back.
horse=json.loads((OUT.parent/'Mounts/MNT_Horse_Anatolian_01.focmesh.json').read_text())
body=next(p for p in horse['lods'][0]['parts'] if p['material']=='HorseCoat')
horse_vertices=[Vector(body['positions'][i:i+3]) for i in range(0,len(body['positions']),3)]
horse_faces=[body['triangles'][i:i+3] for i in range(0,len(body['triangles']),3)]
surface=BVHTree.FromPolygons(horse_vertices,horse_faces,all_triangles=True)
coords=[];quads=[];width=25;depth=23
for j in range(depth):
    z=-.79+j/(depth-1)*.70
    for i in range(width):
        x=-.30+i/(width-1)*.60
        hit,normal,_,_=surface.ray_cast(Vector((x,3,z)),Vector((0,-1,0)),3)
        if hit is None:raise RuntimeError('Blanket outside real horse surface')
        coords.append(hit+normal*.014)
for j in range(depth-1):
    for i in range(width-1):
        a=j*width+i;quads.append((a,a+width,a+width+1,a+1))
blanket=mesh('FittedSaddleBlanket',coords,quads,'ClothRed')
saddle=loft('SaddleSeat',[(-.73,0,1.86,.21,.048),(-.65,0,1.80,.21,.036),(-.44,0,1.765,.18,.032),(-.24,0,1.82,.18,.042),(-.15,0,1.92,.14,.042)],'Leather',32,axis='z')
parts=[blanket,saddle]
girth=[]
for i in range(65):
    angle=2*math.pi*i/64;direction=Vector((math.sin(angle),math.cos(angle),0))
    hit,normal,_,_=surface.ray_cast(Vector((0,1.30,-.43)),direction,1)
    if hit is None:raise RuntimeError('Girth ray missing body')
    girth.append(hit+direction*.015)
parts.append(tube('FittedGirth',girth,.017,'Leather'))
for sign in [-1,1]:
    parts.append(tube('StirrupLeather',[(sign*.16,1.81,-.40),(sign*.38,1.48,-.38),(sign*.46,1.15,-.28)],.012,'Leather'))
    parts.append(tube('StirrupIron',[(sign*.46,1.17,-.30),(sign*.52,1.03,-.30),(sign*.52,1.01,-.14),(sign*.40,1.01,-.14),(sign*.40,1.03,-.30),(sign*.46,1.17,-.30)],.011,'Steel'))
    parts.append(tube('Rein',[(sign*.11,2.02,-.12),(sign*.20,1.90,.45),(sign*.17,1.63,1.15),(sign*.12,1.52,1.55)],.006,'Leather'))
export('HAR_SipahiHarness_01','Harness',parts)
