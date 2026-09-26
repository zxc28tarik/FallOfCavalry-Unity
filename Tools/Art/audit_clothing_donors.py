"""Headless, source-only inspection of the operator's seven Standard FBX donors."""
import bpy
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'ArtSource/HistoricalSlice/Upstream/ClothingDonors/Quaternius'
OUT = ROOT / 'TestResults/ClothingDonors'
OUT.mkdir(parents=True, exist_ok=True)
reports = []
for path in sorted(SOURCE.glob('*.fbx')):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=False)
    report = {'file': path.name, 'meshes': [], 'armatures': []}
    for obj in bpy.context.scene.objects:
        if obj.type == 'MESH':
            obj.data.calc_loop_triangles()
            points = [obj.matrix_world @ v.co for v in obj.data.vertices]
            report['meshes'].append({
                'name': obj.name, 'vertices': len(points),
                'triangles': len(obj.data.loop_triangles),
                'bounds': [[min(p[i] for p in points) for i in range(3)],
                           [max(p[i] for p in points) for i in range(3)]],
                'uv': bool(obj.data.uv_layers),
                'groups': [g.name for g in obj.vertex_groups],
                'unweighted': sum(not any(g.weight > 0 for g in v.groups) for v in obj.data.vertices)})
        elif obj.type == 'ARMATURE':
            report['armatures'].append({'name': obj.name, 'bones': [
                {'name': b.name, 'parent': b.parent.name if b.parent else '',
                 'head': list(obj.matrix_world @ b.head_local),
                 'tail': list(obj.matrix_world @ b.tail_local)} for b in obj.data.bones]})
    assert report['meshes'] and report['armatures'], path.name
    assert all(m['uv'] and m['unweighted'] == 0 for m in report['meshes']), path.name
    reports.append(report)
    print('DONOR_SOURCE_IMPORT_PASS', path.name, flush=True)
(OUT / 'quaternius-source-audit.json').write_text(json.dumps(reports, indent=2), encoding='utf-8')
