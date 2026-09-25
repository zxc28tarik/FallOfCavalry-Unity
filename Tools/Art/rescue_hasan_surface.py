"""Isolated Hasan DRAFT: connected anatomical patch -> constrained subdivision
-> solid shell -> separate construction details. No alternative body/rig input.
Never changes the other profiles or production catalog. Run with --hasan-rescue.
"""
import bpy
import bmesh
import math
import shutil
from mathutils import Vector


def apply(obj, modifier):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def cut(bm,co,normal,outer=True):
    bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),
        dist=1e-6,plane_co=co,plane_no=normal,clear_outer=outer,clear_inner=not outer)


def uv_and_normals(obj):
    bm=bmesh.new();bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(obj.data);bm.free();obj.data.update()
    layer=obj.data.uv_layers.active or obj.data.uv_layers.new()
    for p in obj.data.polygons:
        p.use_smooth=True
        axis=max(range(3),key=lambda i:abs(p.normal[i]))
        for li in p.loop_indices:
            v=obj.data.vertices[obj.data.loops[li].vertex_index].co
            layer.data[li].uv=(v.z,v.y) if axis==0 else (v.x,v.z) if axis==1 else (v.x,v.y)


def shell(obj,thickness=.003):
    uv_and_normals(obj)
    mod=obj.modifiers.new('RealClothThickness','SOLIDIFY')
    # Even-offset miter compensation can explode at acute clipped seam corners.
    # Uniform normal thickness is bounded and still gives real rim surfaces.
    mod.thickness=thickness;mod.offset=0;mod.use_even_offset=False
    apply(obj,mod)
    uv_and_normals(obj)


def patch(source,name,predicate,offset=.006,planes=()):
    obj=source.copy();obj.data=source.data.copy();obj.name=name
    bpy.context.scene.collection.objects.link(obj)
    bm=bmesh.new();bm.from_mesh(obj.data)
    for co,normal,outer in planes:cut(bm,co,normal,outer)
    remove=[f for f in bm.faces if not predicate(f.calc_center_median())]
    bmesh.ops.delete(bm,geom=remove,context='FACES')
    bm.to_mesh(obj.data);bm.free();obj.data.update()
    for v in obj.data.vertices:v.co+=v.normal*offset
    return obj


def build(api):
    make=api['make_subset'];positions=api['positions'];ix=api['bone_index']
    # Start from a single connected anatomical surface including shoulders and
    # armpits. No independent sleeve rings or reconstructed armhole bridges.
    obj,_=make('RescueConnectedCoat',lambda p:True,.025,'Mail')
    bm=bmesh.new();bm.from_mesh(obj.data)
    cut(bm,(0,1.01,0),(0,1,0),False)
    # Segment the head locally. A whole-body horizontal clip also cuts the
    # raised shoulder caps, which is the observed false-collar defect.
    bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),
        dist=1e-6,plane_co=(0,1.525,0),plane_no=(0,1,0))
    head_faces=[f for f in bm.faces if f.calc_center_median().y>1.525 and abs(f.calc_center_median().x)<.13]
    bmesh.ops.delete(bm,geom=head_faces,context='FACES')
    for side in ['L','R']:
        elbow=positions['LowerArm_'+side];wrist=positions['Hand_'+side]
        cut(bm,elbow.lerp(wrist,.91),(wrist-elbow).normalized())
    # Relax muscle shapes without shrinking through the source envelope.
    initial={v:v.co.copy() for v in bm.verts}
    interior=[v for v in bm.verts if not v.is_boundary]
    for _ in range(8):
        bmesh.ops.smooth_vert(bm,verts=interior,factor=.25,use_axis_x=True,use_axis_y=True,use_axis_z=True)
    for v in bm.verts:
        v.co=v.co.lerp(initial[v],.25)
    # Topological boundary traversal, NOT angular sort (which can cross source
    # edges at asymmetric contours). Extrusion keeps original edge adjacency.
    edges=[e for e in bm.edges if e.is_boundary and all(abs(v.co.y-1.01)<1e-5 for v in e.verts)]
    if len(edges)<12:raise RuntimeError('Source waist boundary not found')
    adjacency={}
    for e in edges:
        for v in e.verts:adjacency.setdefault(v,[]).append(e.other_vert(v))
    if any(len(ns)!=2 for ns in adjacency.values()):raise RuntimeError('Waist is not one manifold loop')
    first=min(adjacency,key=lambda v:(v.co.x,v.co.z));loop=[first];previous=None;current=first
    while True:
        nxt=next(v for v in adjacency[current] if v!=previous)
        if nxt==first:break
        loop.append(nxt);previous,current=current,nxt
        if len(loop)>len(adjacency):raise RuntimeError('Invalid boundary traversal')
    if len(loop)!=len(adjacency):raise RuntimeError('Disconnected waist boundaries')
    deform=bm.verts.layers.deform.verify();base=list(loop)
    # Drape a continuous coat with hip clearance and shallow longitudinal folds.
    # Original shoulder topology remains; lower edge is physically connected.
    for j in range(1,9):
        t=j/8;y=1.01-.34*t;new=[]
        for src in base:
            angle=math.atan2(src.co.x,src.co.z+.035)
            radius_x=.252+.035*t;radius_z=.17+.018*t
            pleat=.0045*math.sin(angle*14)*math.sin(math.pi*t*.8)
            target=Vector(((radius_x+pleat)*math.sin(angle),y,-.035+(radius_z+pleat)*math.cos(angle)))
            co=src.co.lerp(target,min(1,t*3));co.y=y
            v=bm.verts.new(co);new.append(v)
            v[deform][ix['Pelvis']]=1-t*.45
            v[deform][ix['UpperLeg_L' if co.x>0 else 'UpperLeg_R']]=t*.45
        for i in range(len(base)):
            k=(i+1)%len(base);bm.faces.new((loop[i],loop[k],new[k],new[i]))
        loop=new
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(obj.data);bm.free();obj.data.update()
    # Catmull-Clark gives deformation-friendly continuity and rounds the hard
    # source transitions. Weights interpolate with the same modifier.
    subdiv=obj.modifiers.new('ConnectedClothSubdivision','SUBSURF')
    subdiv.levels=1;subdiv.subdivision_type='CATMULL_CLARK';apply(obj,subdiv)
    # Build a conservative horizontal cage from the actual trouser surface.
    # This fixes the front-thigh intrusion missed by an axis-only radius check.
    trouser_points=[v.co.copy() for v in api['trousers'][0].data.vertices]
    envelopes=[]
    for j in range(48):
        y=.65+j*.01
        samples=[p for p in trouser_points if abs(p.y-y)<.025]
        q=max([math.sqrt((p.x/.265)**2+((p.z+.035)/.18)**2) for p in samples] or [1])
        envelopes.append(max(1,q)+.10)
    # Correct layer clearance after subdivision, not by hiding trousers/faults.
    for v in obj.data.vertices:
        if .67<v.co.y<1.08:
            a=math.atan2(v.co.x,v.co.z+.035)
            j=max(0,min(47,round((v.co.y-.65)/.01)))
            envelope=max(envelopes[max(0,j-2):min(48,j+3)])
            rx=.265*envelope;rz=.18*envelope
            q=math.sqrt((v.co.x/rx)**2+((v.co.z+.035)/rz)**2)
            if q<1:
                v.co.x=rx*math.sin(a);v.co.z=-.035+rz*math.cos(a)
    # Cut a deliberate front opening and lower riding vent on subdivided faces.
    bm=bmesh.new();bm.from_mesh(obj.data)
    for x in [-.013,.013]:
        bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=1e-6,plane_co=(x,0,0),plane_no=(1,0,0))
    remove=[]
    for f in bm.faces:
        p=f.calc_center_median()
        if p.z>0 and abs(p.x)<.0129 and (p.y>1.25 or p.y<.79):remove.append(f)
    bmesh.ops.delete(bm,geom=remove,context='FACES')
    bm.to_mesh(obj.data);bm.free();obj.data.update();uv_and_normals(obj)
    belt=patch(obj,'RescueWaistSash',lambda p:True,.008,
        [((0,1.01,0),(0,1,0),False),((0,1.067,0),(0,1,0),True)])
    # A separate narrow sewn collar band, not a cropped shoulder patch. This
    # rigid-ish construction detail is not the failed independent sleeve system.
    coords=[];polys=[];segments=48
    for y in [1.516,1.522,1.549,1.552]:
        for k in range(segments+1):
            a=.15+(2*math.pi-.30)*k/segments
            coords.append((.087*math.sin(a),y,.015+.077*math.cos(a)))
    for j in range(3):
        for k in range(segments):
            a=j*(segments+1)+k;polys.append((a,a+segments+1,a+segments+2,a+1))
    data=bpy.data.meshes.new('SewnNeckBand');data.from_pydata(coords,[],polys);data.update()
    collar=bpy.data.objects.new('RescueCollar',data);bpy.context.scene.collection.objects.link(collar)
    for name in api['bone_names']:collar.vertex_groups.new(name=name)
    collar.vertex_groups[ix['Neck']].add(list(range(len(coords))),1,'REPLACE')
    cuffs=[]
    for side in ['L','R']:
        elbow=positions['LowerArm_'+side];wrist=positions['Hand_'+side]
        axis=(wrist-elbow).normalized();origin=elbow.lerp(wrist,.82)
        sign=1 if side=='L' else -1
        cuffs.append(patch(obj,'RescueCuff'+side,lambda p,s=sign:s*p.x>.32,.004,[(origin,axis,False)]))
    shell(obj);shell(belt,.005);shell(collar)
    for cuff in cuffs:shell(cuff)
    # Audited source cards replace random facial fibers. No generated geometry
    # from the rejected stash is reused.
    folder=api['REPO']/'ArtSource/HistoricalSlice/Upstream/FacialHair'
    facial=[]
    for style,texture,material in [
        ('rehmanpolanski_beard_viking','BeardViking.png','HairCardsBeard'),
        ('rehmanpolanski_moustache_viking','MoustacheViking.png','HairCardsMoustache')]:
        source=folder/style
        shutil.copyfile(source/texture,api['OUT']/(material+'_D.png'))
        part=api['fitted_hair'](style,4,source,material)
        if 'beard' in style:
            for v in part[0].data.vertices:
                if v.co.y<1.573:v.co.y=1.573+(v.co.y-1.573)*.85
        facial.append(part)
    head=api['heads'][3][0]
    bm=bmesh.new();bm.from_mesh(head[0].data)
    cut(bm,(0,1.494,0),(0,1,0),False)
    bm.to_mesh(head[0].data);bm.free();head[0].data.update()
    parts=[(p[0],'SkinMature') for p in api['exposed']]
    parts += [api['trousers']]+api['boots']+[head,api['heads'][3][1]]+facial+api['eyes']
    for boot,_ in api['boots']:
        sole=patch(boot,'RescueSole'+boot.name,lambda p:True,.0025,
            [((0,.038,0),(0,1,0),True)])
        shell(sole,.002);parts.append((sole,'LeatherSole'))
    parts += [(obj,'Mail'),(belt,'ClothRed'),(collar,'Linen')]+[(c,'Leather') for c in cuffs]
    print('FOC_HASAN_SURFACE_RESCUE_DRAFT connected-subdivision-solidify; no catalog promotion',flush=True)
    return parts
