"""Source assessment, not acceptance evidence (acceptance requires the player)."""
import bpy
from pathlib import Path
from mathutils import Vector

repo = Path(__file__).resolve().parents[2]
bpy.ops.wm.open_mainfile(filepath=str(repo / 'Artifacts/ArtInputs/riggedHorse.blend'), load_ui=False, use_scripts=False)
for obj in list(bpy.data.objects):
    if obj.type not in {'MESH', 'ARMATURE'}:
        bpy.data.objects.remove(obj, do_unlink=True)
for obj in bpy.data.objects:
    if obj.type != 'MESH':
        continue
    obj.hide_render = False
    material = bpy.data.materials.new('Review_' + obj.name)
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (.12, .045, .022, 1)
    bsdf.inputs['Roughness'].default_value = .7
    texture_name = 'HorseMain4k00.png' if obj.name == 'Plane' else 'Hair12Main2k.png' if obj.name.startswith('Bezier') else 'eye_texture.bmp.001'
    image = bpy.data.images.get(texture_name)
    if image and image.size[0]:
        tex = material.node_tree.nodes.new('ShaderNodeTexImage'); tex.image = image
        material.node_tree.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    obj.data.materials.clear(); obj.data.materials.append(material)
scene = bpy.context.scene
scene.render.engine = 'BLENDER_EEVEE_NEXT'
scene.world = bpy.data.worlds.new('ReviewWorld'); scene.world.use_nodes = True
scene.world.node_tree.nodes['Background'].inputs[0].default_value = (.13, .16, .20, 1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value = .6
for position, power, size in [((10,-12,16),2200,10),((-10,-4,8),1600,8),((4,12,10),1800,8)]:
    data=bpy.data.lights.new('ReviewKey','AREA');data.energy=power;data.shape='DISK';data.size=size
    obj=bpy.data.objects.new('ReviewKey',data);scene.collection.objects.link(obj);obj.location=position;obj.rotation_euler=(Vector((0,-2,0))-obj.location).to_track_quat('-Z','Y').to_euler()
data=bpy.data.cameras.new('ReviewCamera');camera=bpy.data.objects.new('ReviewCamera',data);scene.collection.objects.link(camera)
camera.location=(19,-20,10);camera.rotation_euler=(Vector((0,-2,0))-camera.location).to_track_quat('-Z','Y').to_euler();data.type='ORTHO';data.ortho_scale=18;scene.camera=camera
scene.render.resolution_x=1280;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.render.filepath=str(repo/'Artifacts/ArtInputs/horse-source-review.png')
bpy.ops.render.render(write_still=True)
