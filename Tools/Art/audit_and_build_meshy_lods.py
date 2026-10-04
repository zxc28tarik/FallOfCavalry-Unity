"""Blender read-only source audit and derived REST-world LOD JSON authoring.

Use Blender --background --factory-startup --disable-autoexec --python-exit-code 1
--python this_script.py -- --source original.fbx --runtime-fbx stripped.fbx.
Only ignored output artifacts are written. Original LOD0 is never re-exported,
decimated, welded, edited or overwritten. Small source components are retained.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
from collections import Counter, defaultdict
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

EXPECTED_SOURCE_SHA = "0ee0076119289b4748fa0eeb11fa631d9a03e55b9b1208db6afe19beba410b27"
COINCIDENT_METERS = 0.000001
ZERO_AREA_METERS_SQUARED = 1e-14


def sha_file(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(",", ":"), allow_nan=False).encode()).hexdigest()


def matrix_flat(matrix):
    return [float(value) for row in matrix for value in row]


def finite(values):
    return all(math.isfinite(float(value)) for value in values)


def action_curves(action):
    result = []
    for layer in action.layers:
        for strip in layer.strips:
            if not hasattr(strip, "channelbag"):
                continue
            for slot in action.slots:
                bag = strip.channelbag(slot)
                if bag:
                    result.extend(bag.fcurves)
    return result


def import_source(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=True, use_image_search=False,
                             automatic_bone_orientation=False, ignore_leaf_bones=False)
    arms = [o for o in bpy.context.scene.objects if o.type == "ARMATURE"]
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if len(arms) != 1 or len(meshes) != 1 or len(arms[0].data.bones) != 23:
        raise ValueError("Expected exactly one mesh and the preserved 23-bone source armature")
    arms[0].data.pose_position = "REST"
    bpy.context.view_layer.update()
    return arms[0], meshes[0]


def snapshot(arm, obj):
    mesh = obj.data
    mesh.calc_loop_triangles()
    uv = mesh.uv_layers.active
    result = {
        "mesh": {"vertices": [list(v.co) for v in mesh.vertices],
                 "polygons": [list(p.vertices) for p in mesh.polygons],
                 "loops": [loop.vertex_index for loop in mesh.loops],
                 "corner_normals": [list(n.vector) for n in mesh.corner_normals],
                 "uv": [list(x.uv) for x in uv.data] if uv else [],
                 "polygon_material_indices": [p.material_index for p in mesh.polygons],
                 "object_matrix_world": matrix_flat(obj.matrix_world)},
        "weights": [[(obj.vertex_groups[g.group].name, float(g.weight)) for g in v.groups] for v in mesh.vertices],
        "rig": {"object_matrix_world": matrix_flat(arm.matrix_world),
                "bones": [{"name": b.name, "parent": b.parent.name if b.parent else "",
                           "matrix_local": matrix_flat(b.matrix_local), "head": list(b.head_local),
                           "tail": list(b.tail_local), "deform": bool(b.use_deform)} for b in arm.data.bones]},
        "animations": [],
    }
    for action in sorted(bpy.data.actions, key=lambda a: a.name):
        curves = []
        for curve in sorted(action_curves(action), key=lambda c: (c.data_path, c.array_index)):
            curves.append({"path": curve.data_path, "index": curve.array_index,
                           "keys": [{"co": list(k.co), "left": list(k.handle_left), "right": list(k.handle_right),
                                     "interpolation": k.interpolation} for k in curve.keyframe_points]})
        result["animations"].append({"name": action.name, "range": list(action.frame_range), "curves": curves})
    return {key: {"sha256": digest(value)} for key, value in result.items()}


def union_components(count, pairs):
    parents = list(range(count))

    def find(a):
        while parents[a] != a:
            parents[a] = parents[parents[a]]
            a = parents[a]
        return a

    for a, b in pairs:
        a, b = find(a), find(b)
        if a != b:
            parents[b] = a
    groups = defaultdict(list)
    for i in range(count):
        groups[find(i)].append(i)
    return sorted(groups.values(), key=lambda group: (-len(group), group[0]))


def mesh_analysis(obj):
    mesh = obj.data
    mesh.calc_loop_triangles()
    points = [obj.matrix_world @ v.co for v in mesh.vertices]
    if not points or not all(finite(point) for point in points):
        raise ValueError("Nonfinite/empty source geometry")
    triangles = [tuple(t.vertices) for t in mesh.loop_triangles]
    raw_components = union_components(len(points), [tuple(e.vertices) for e in mesh.edges])
    canonical, first = [], {}
    for vi, point in enumerate(points):
        key = tuple(round(float(value) / COINCIDENT_METERS) for value in point)
        canonical.append(first.setdefault(key, vi))
    pairs = [tuple(edge.vertices) for edge in mesh.edges] + list(enumerate(canonical))
    components = union_components(len(points), pairs)
    component_of = {vi: ci for ci, component in enumerate(components) for vi in component}
    tri_count = Counter(component_of[t[0]] for t in triangles)
    area_by_component = Counter()
    raw_edges, welded_edges = defaultdict(list), defaultdict(list)
    faces_by_positions = defaultdict(list)
    zero_area, duplicate_faces, opposed_duplicate_pairs = [], 0, 0
    normals = []
    for ti, triangle in enumerate(triangles):
        a, b, c = (points[i] for i in triangle)
        cross = (b - a).cross(c - a)
        area = cross.length * .5
        if area <= ZERO_AREA_METERS_SQUARED:
            zero_area.append(ti)
        normals.append(cross.normalized() if cross.length > 0 else Vector((0, 0, 0)))
        area_by_component[component_of[triangle[0]]] += area
        welded = [canonical[vi] for vi in triangle]
        faces_by_positions[tuple(sorted(welded))].append(ti)
        for values, edges in ((triangle, raw_edges), (welded, welded_edges)):
            for u, v in zip(values, values[1:] + values[:1]):
                edges[tuple(sorted((u, v)))].append((ti, u, v))
    for group in faces_by_positions.values():
        if len(group) > 1:
            duplicate_faces += len(group) - 1
            for i, a in enumerate(group):
                for b in group[i + 1:]:
                    opposed_duplicate_pairs += int(normals[a].dot(normals[b]) < -.99)

    def edge_metrics(edges):
        return {"edges": len(edges), "boundary_edges": sum(len(uses) == 1 for uses in edges.values()),
                "nonmanifold_more_than_two_faces": sum(len(uses) > 2 for uses in edges.values()),
                "same_direction_two_face_edges": sum(len(uses) == 2 and uses[0][1:] == uses[1][1:] for uses in edges.values())}

    components_report = []
    for ci, component in enumerate(components):
        lo = [min(points[i][axis] for i in component) for axis in range(3)]
        hi = [max(points[i][axis] for i in component) for axis in range(3)]
        diagonal = (Vector(hi) - Vector(lo)).length
        influence = Counter()
        for vi in component:
            for group in mesh.vertices[vi].groups:
                influence[obj.vertex_groups[group.group].name] += group.weight
        dominant = influence.most_common(3)
        if lo[2] >= 1.35:
            hint = "head_region_candidate_face_hair_or_headgear_not_semantically_certified"
        elif hi[2] < .55:
            hint = "lower_leg_or_boot_region_candidate_not_semantically_certified"
        elif dominant and "Hand" in dominant[0][0]:
            hint = "hand_region_detail_candidate_not_semantically_certified"
        else:
            hint = "body_or_garment_region_unclassified"
        small = tri_count[ci] <= 64 or diagonal < .08
        components_report.append({"index": ci, "raw_vertices": len(component), "triangles": tri_count[ci],
                                  "bounds_min_world_m": lo, "bounds_max_world_m": hi, "diagonal_m": diagonal,
                                  "surface_area_m2": area_by_component[ci], "dominant_bones_by_weight_sum": dominant,
                                  "classification": hint, "small_component_protected_from_decimation": small,
                                  "decision": "KEEP_NO_CONFIRMED_JUNK_DELETION"})
    report = {"mesh": obj.name, "vertices": len(points), "triangles": len(triangles),
              "raw_connected_components": len(raw_components), "raw_component_vertex_sizes": [len(c) for c in raw_components],
              "coincident_connected_components": len(components), "coincident_tolerance_m": COINCIDENT_METERS,
              "raw_topology": edge_metrics(raw_edges), "coincident_topology_diagnostic_only": edge_metrics(welded_edges),
              "zero_area_triangles": len(zero_area), "zero_area_triangle_indices": zero_area,
              "duplicate_position_triangle_count": duplicate_faces, "opposed_duplicate_triangle_pairs": opposed_duplicate_pairs,
              "bounds_min_world_m": [min(p[axis] for p in points) for axis in range(3)],
              "bounds_max_world_m": [max(p[axis] for p in points) for axis in range(3)],
              "components": components_report,
              "interpretation": "Raw UV/normal seams split FBX vertices; coincident connectivity is diagnostic, not proof of holes or junk. No source components deleted."}
    return report, points, triangles, components


def subset_clone(source, allowed_vertices, name):
    clone = source.copy()
    clone.data = source.data.copy()
    clone.name = name
    bpy.context.collection.objects.link(clone)
    for modifier in list(clone.modifiers):
        clone.modifiers.remove(modifier)
    bm = bmesh.new()
    try:
        bm.from_mesh(clone.data)
        bm.verts.ensure_lookup_table()
        removed = [v for v in bm.verts if v.index not in allowed_vertices]
        bmesh.ops.delete(bm, geom=removed, context="VERTS")
        bm.to_mesh(clone.data)
        clone.data.update()
    finally:
        bm.free()
    return clone


def weld_derived_only(obj):
    scale = obj.matrix_world.to_scale()
    if min(abs(x) for x in scale) <= 0 or max(scale) - min(scale) > 1e-5:
        raise ValueError("Only uniform source scale supported for derived seam weld")
    before = len(obj.data.vertices)
    bm = bmesh.new()
    try:
        bm.from_mesh(obj.data)
        bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=COINCIDENT_METERS / abs(scale[0]))
        bm.to_mesh(obj.data)
        obj.data.update()
    finally:
        bm.free()
    return {"vertices_before": before, "vertices_after": len(obj.data.vertices), "world_tolerance_m": COINCIDENT_METERS,
            "scope": "DERIVED_LOD_ONLY; source LOD0 and protected small components not welded"}


def derived_weight(groups, obj, bone_index, metrics):
    values = [(bone_index[obj.vertex_groups[g.group].name], float(g.weight)) for g in groups if g.weight > 0]
    if not values or any(not math.isfinite(weight) or weight < 0 for _, weight in values):
        raise ValueError("Derived vertex is unweighted or nonfinite")
    values.sort(key=lambda pair: (-pair[1], pair[0]))
    metrics["maximum_influences_before_limit"] = max(metrics["maximum_influences_before_limit"], len(values))
    metrics["maximum_discarded_weight_mass"] = max(metrics["maximum_discarded_weight_mass"], sum(w for _, w in values[4:]))
    if len(values) > 4:
        metrics["vertices_over_four_influences_before_limit"] += 1
    values = values[:4]
    total = sum(weight for _, weight in values)
    values = [(index, weight / total) for index, weight in values]
    while len(values) < 4:
        values.append((0, 0.0))
    return [index for index, _ in values], [weight for _, weight in values]


def export_geometry(objects, bones):
    result = {"positions": [], "normals": [], "uv": [], "triangles": [], "boneIndices": [],
              "boneWeights": [], "triangleMaterialIndices": [], "materialNames": []}
    metrics = {"maximum_influences_before_limit": 0, "vertices_over_four_influences_before_limit": 0,
               "maximum_discarded_weight_mass": 0.0, "unweighted_vertices": 0,
               "weight_policy": "Derived copy only: deterministic strongest4 then normalize; no exact-source-weight claim."}
    bone_index = {bone["name"]: i for i, bone in enumerate(bones)}
    for obj in objects:
        mesh = obj.data
        if not mesh.polygons:
            continue
        mesh.calc_loop_triangles()
        uv = mesh.uv_layers.active
        if uv is None:
            raise ValueError("Derived LOD lost source UV")
        normal_matrix = obj.matrix_world.to_3x3().inverted().transposed()
        source_weights = {v.index: derived_weight(v.groups, obj, bone_index, metrics) for v in mesh.vertices}
        lookup = {}
        for tri in mesh.loop_triangles:
            material = obj.data.materials[tri.material_index]
            material_name = material.name if material else "MISSING"
            if material_name == "MISSING":
                raise ValueError("Missing source material slot")
            if material_name not in result["materialNames"]:
                result["materialNames"].append(material_name)
            result["triangleMaterialIndices"].append(result["materialNames"].index(material_name))
            for li in tri.loops:
                vi = mesh.loops[li].vertex_index
                normal = (normal_matrix @ mesh.corner_normals[li].vector).normalized()
                uv_value = uv.data[li].uv
                key = (vi, tuple(normal), tuple(uv_value))
                if key not in lookup:
                    position = obj.matrix_world @ mesh.vertices[vi].co
                    if not finite(position) or not finite(normal) or not finite(uv_value):
                        raise ValueError("Derived geometry contains nonfinite values")
                    lookup[key] = len(result["positions"]) // 3
                    result["positions"].extend(position)
                    result["normals"].extend(normal)
                    result["uv"].extend(uv_value)
                    indices, weights = source_weights[vi]
                    result["boneIndices"].extend(indices)
                    result["boneWeights"].extend(weights)
                result["triangles"].append(lookup[key])
    count = len(result["positions"]) // 3
    if not count or len(result["triangles"]) % 3 or len(result["boneWeights"]) != count * 4:
        raise ValueError("Derived export topology/weight lengths invalid")
    for i in range(count):
        weights = result["boneWeights"][i * 4:i * 4 + 4]
        if abs(sum(weights) - 1.0) > 1e-6:
            raise ValueError("Derived weights not normalized")
    if any(index < 0 or index >= count for index in result["triangles"]):
        raise ValueError("Derived triangle index invalid")
    return result, metrics


def surface_distances(source_points, source_triangles, output):
    points = [Vector(output["positions"][i:i + 3]) for i in range(0, len(output["positions"]), 3)]
    triangles = [output["triangles"][i:i + 3] for i in range(0, len(output["triangles"]), 3)]
    source_bvh = BVHTree.FromPolygons(source_points, source_triangles, all_triangles=True)
    lod_bvh = BVHTree.FromPolygons(points, triangles, all_triangles=True)

    def stats(vertices, target):
        values = sorted(float(target.find_nearest(point)[3]) for point in vertices)
        return {"samples": len(values), "maximum_m": max(values), "p95_m": values[int((len(values) - 1) * .95)],
                "mean_m": sum(values) / len(values)}

    return {"source_vertices_to_lod_surface": stats(source_points, lod_bvh),
            "lod_vertices_to_source_surface": stats(points, source_bvh),
            "limit": "Rest-surface distance diagnostic, not a rendered silhouette or animated-deformation acceptance."}


def write_json(path, value):
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(value, indent=2, allow_nan=False), encoding="utf-8")
    temporary.replace(path)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--runtime-fbx", type=Path, required=True)
    parser.add_argument("--output-directory", type=Path, default=Path("Artifacts/MeshyPilotOptimized"))
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    source, runtime, out = args.source.resolve(), args.runtime_fbx.resolve(), args.output_directory.resolve()
    if sha_file(source) != EXPECTED_SOURCE_SHA or source == runtime:
        raise ValueError("Expected separate pinned original and stripped runtime FBX")
    runtime_hash = sha_file(runtime)
    out.mkdir(parents=True, exist_ok=True)
    arm, body = import_source(source)
    original_snapshot = snapshot(arm, body)
    original_analysis, _, _, _ = mesh_analysis(body)
    arm, body = import_source(runtime)
    runtime_snapshot = snapshot(arm, body)
    if original_snapshot != runtime_snapshot:
        raise ValueError("Independent Blender geometry/weights/rig/animation snapshots differ after media strip")
    source_analysis, source_points, source_triangles, components = mesh_analysis(body)
    if original_analysis != source_analysis:
        raise ValueError("Independent source/runtime topology audits differ")
    if len(source_triangles) != 9586:
        raise ValueError("Unexpected source triangle count")
    bones = [{"name": bone.name, "parent": bone.parent.name if bone.parent else "",
              "restWorldMatrix": matrix_flat(arm.matrix_world @ bone.matrix_local),
              "headWorld": list(arm.matrix_world @ bone.head_local),
              "tailWorld": list(arm.matrix_world @ bone.tail_local)} for bone in arm.data.bones]
    checkpoint_ids = set([0, len(source_points) // 4, len(source_points) // 2, len(source_points) - 1])
    for axis in range(3):
        checkpoint_ids.add(min(range(len(source_points)), key=lambda i: source_points[i][axis]))
        checkpoint_ids.add(max(range(len(source_points)), key=lambda i: source_points[i][axis]))
    checkpoints = [{"sourceVertex": i, "position": list(source_points[i])} for i in sorted(checkpoint_ids)]
    common = {"format": "FOC_MESHY_REST_LOD_MESH_V1", "sourceSha256": EXPECTED_SOURCE_SHA,
              "runtimeSourceSha256": runtime_hash, "coordinates": "BLENDER_WORLD_METERS_Z_UP",
              "bones": bones, "sourceBoundsMin": source_analysis["bounds_min_world_m"],
              "sourceBoundsMax": source_analysis["bounds_max_world_m"], "sourceCheckpoints": checkpoints}
    manifest = {"status": "DCC_DERIVED_CANDIDATE_NOT_UNITY_OR_VISUAL_ACCEPTANCE", "blender": bpy.app.version_string,
                "source_sha256": EXPECTED_SOURCE_SHA, "runtime_fbx_sha256": runtime_hash,
                "independent_blender_before": original_snapshot, "independent_blender_after": runtime_snapshot,
                "media_strip_semantics_independently_identical": True, "source_geometry_audit": source_analysis,
                "lod0_policy": "Original geometry, rig, weights, UV, bindposes and animations retained; only Video/Content stripped.",
                "lod_policy": "REST-world geometry only; original23 bone names/matrices retained in JSON. Small/unclassified components protected. No new body or garments.",
                "animation_policy": "Use original clips from stripped LOD0 FBX. Derived JSON contains no replacement animation or skeleton.",
                "outputs": [], "source_unchanged": False, "production_changes": False, "unity_validation": "NOT_RUN"}
    protected = set()
    for item, vertices in zip(source_analysis["components"], components):
        if item["small_component_protected_from_decimation"]:
            protected.update(vertices)
    large = set(range(len(body.data.vertices))) - protected
    protected_triangles = sum(1 for tri in source_triangles if tri[0] in protected)
    if not large:
        raise ValueError("All source geometry is protected; cannot meet requested LOD ratios safely")
    for level, ratio in ((1, .60), (2, .25)):
        main_copy = subset_clone(body, large, "Derived_LOD%d_Main" % level)
        detail_copy = subset_clone(body, protected, "Derived_LOD%d_ProtectedDetails" % level)
        try:
            weld = weld_derived_only(main_copy)
            main_copy.data.calc_loop_triangles()
            target = round(len(source_triangles) * ratio)
            main_ratio = (target - protected_triangles) / len(main_copy.data.loop_triangles)
            if not 0 < main_ratio < 1:
                raise ValueError("Protected parts prevent requested ratio; do not delete them")
            bpy.ops.object.select_all(action="DESELECT")
            main_copy.select_set(True)
            bpy.context.view_layer.objects.active = main_copy
            modifier = main_copy.modifiers.new("DerivedOnlyRestDecimation", "DECIMATE")
            modifier.decimate_type = "COLLAPSE"
            modifier.ratio = main_ratio
            modifier.use_collapse_triangulate = True
            bpy.ops.object.modifier_apply(modifier=modifier.name)
            geometry, weight_metrics = export_geometry([main_copy, detail_copy], bones)
            output = dict(common, lod=level, ratioRequested=ratio, **geometry)
            triangles = len(geometry["triangles"]) // 3
            if triangles >= len(source_triangles):
                raise ValueError("Derived LOD did not reduce geometry")
            path = out / ("Hasan_Meshy_LOD%d.json" % level)
            write_json(path, output)
            metrics = {"lod": level, "path": str(path), "sha256": sha_file(path),
                       "ratio_requested": ratio, "triangles": triangles, "vertices": len(geometry["positions"]) // 3,
                       "ratio_actual": triangles / len(source_triangles), "material_count": len(geometry["materialNames"]),
                       "protected_component_count": sum(c["small_component_protected_from_decimation"] for c in source_analysis["components"]),
                       "protected_source_triangles": protected_triangles, "derived_weld": weld,
                       "weights": weight_metrics, "surface_distance": surface_distances(source_points, source_triangles, geometry),
                       "source_parts_deleted": False, "rendered_silhouette": "NOT_RUN", "animated_deformation": "NOT_RUN"}
            manifest["outputs"].append(metrics)
            print("FOC_MESHY_LOD_AUTHORED " + json.dumps({k: metrics[k] for k in ("lod", "triangles", "vertices", "ratio_actual", "sha256")}), flush=True)
        finally:
            for obj in (main_copy, detail_copy):
                mesh = obj.data
                bpy.data.objects.remove(obj, do_unlink=True)
                bpy.data.meshes.remove(mesh)
    if sha_file(source) != EXPECTED_SOURCE_SHA or sha_file(runtime) != runtime_hash:
        raise ValueError("Source or runtime LOD0 changed during derived authoring")
    manifest["source_unchanged"] = True
    write_json(out / "dcc-generation-manifest.json", manifest)
    print("FOC_MESHY_DCC_AUDIT_LODS_PASS " + json.dumps({"manifest": str(out / "dcc-generation-manifest.json"),
          "source_unchanged": True, "source_components": source_analysis["coincident_connected_components"],
          "zero_area_triangles": source_analysis["zero_area_triangles"]}), flush=True)


if __name__ == "__main__":
    main()
