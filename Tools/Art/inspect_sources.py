"""Read-only source inspection. Run with Blender --background --disable-autoexec."""
import bpy
import json
from pathlib import Path
from mathutils import Vector

repo = Path(__file__).resolve().parents[2]
bpy.ops.wm.open_mainfile(filepath=str(repo / 'Artifacts/ArtInputs/riggedHorse.blend'), load_ui=False, use_scripts=False)
for obj in bpy.data.objects:
    if obj.type in {'MESH', 'ARMATURE'}:
        print('FOC_SOURCE', json.dumps({'name': obj.name, 'type': obj.type,
            'dimensions': list(obj.dimensions), 'location': list(obj.location),
            'vertices': len(obj.data.vertices) if obj.type == 'MESH' else 0,
            'modifiers': [(m.name, m.type) for m in obj.modifiers],
            'bones': [(b.name, list(b.head_local), list(b.tail_local), b.parent.name if b.parent else '') for b in obj.data.bones] if obj.type == 'ARMATURE' else [],
            'materials': [m.name if m else '' for m in obj.data.materials] if obj.type == 'MESH' else []}))
print('FOC_IMAGES', [(i.name, i.filepath, list(i.size), bool(i.packed_file)) for i in bpy.data.images])
