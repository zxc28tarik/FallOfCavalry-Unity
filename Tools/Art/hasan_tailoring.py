"""Extract narrow turned-edge/facing details from the adapted CC0 donor coat.

No new tunic, rings, body-derived clothes, ornaments, textures or source assets.
The existing coat's surface, UVs and interpolated weights are the sole geometry
source. This is a practical tailoring interpretation, not new historical proof.
Import from Blender and call build_tailoring(coat, donor) after final coat fitting.
The caller still owns motion/mounted clipping QA and production acceptance.
"""
import heapq
import json
import math

import bpy
import bmesh


def _boundary_regions(coat, donor):
    mesh = coat.data
    uses = {}
    for polygon in mesh.polygons:
        for edge in polygon.edge_keys:
            uses[tuple(sorted(edge))] = uses.get(tuple(sorted(edge)), 0)+1
    minimum_y = min(vertex.co.y for vertex in mesh.vertices)
    regions = {name: [] for name in ('front', 'collar', 'hem', 'cuff_L', 'cuff_R')}
    for edge in mesh.edges:
        a, b = tuple(edge.vertices)
        if uses.get(tuple(sorted((a, b))), 0) != 1:
            continue
        center = (mesh.vertices[a].co+mesh.vertices[b].co)*.5
        x, y, z = center
        # Only the wrist terminal edge qualifies as a cuff, never an original
        # Viking elbow flap/bulge. The garment was already smoothed by the caller.
        cuff = None
        if abs(x) > .30:
            side = 'L' if x > 0 else 'R'
            elbow, wrist = donor.bp['LowerArm_'+side], donor.bp['Hand_'+side]
            axis = wrist-elbow
            along = (center-elbow).dot(axis)/axis.length_squared
            if along > .65 and (center-wrist).length < .14:
                cuff = 'cuff_'+side
        if cuff:
            regions[cuff].append((a, b))
        elif y > 1.425 and abs(x) < .16:
            regions['collar'].append((a, b))
        elif y < minimum_y+.07 and abs(x) < .36:
            regions['hem'].append((a, b))
        elif z > .025 and abs(x) < .13 and minimum_y < y < 1.51:
            regions['front'].append((a, b))
    missing = [name for name, edges in regions.items() if len(edges) < 2]
    if missing:
        raise ValueError('Cannot derive garment boundary detail: '+', '.join(missing))
    return regions


def _distances(mesh, seeds, adjacency):
    values = [math.inf]*len(mesh.vertices)
    queue = []
    for index in sorted(seeds):
        values[index] = 0.0
        heapq.heappush(queue, (0.0, index))
    while queue:
        distance, index = heapq.heappop(queue)
        if distance != values[index]:
            continue
        for other, length in adjacency[index]:
            trial = distance+length
            if trial < values[other]:
                values[other] = trial
                heapq.heappush(queue, (trial, other))
    return values


def _interpolate(a, b, factor):
    weights = {index: value*(1-factor) for index, value in a['weights'].items()}
    for index, value in b['weights'].items():
        weights[index] = weights.get(index, 0)+value*factor
    return {'co': a['co'].lerp(b['co'], factor),
            'normal': a['normal'].lerp(b['normal'], factor).normalized(),
            'uv': a['uv'].lerp(b['uv'], factor), 'weights': weights,
            'distance': a['distance']*(1-factor)+b['distance']*factor}


def _clip_triangle(triangle):
    """Cut an ORIGINAL triangle at the band iso-distance; never fill its hole."""
    output = []
    previous = triangle[-1]
    for current in triangle:
        previous_inside, current_inside = previous['distance'] <= 1, current['distance'] <= 1
        if previous_inside != current_inside:
            denominator = current['distance']-previous['distance']
            if not math.isfinite(denominator) or abs(denominator) < 1e-12:
                raise ValueError('Disconnected/nonfinite coat distance field')
            factor = (1-previous['distance'])/denominator
            output.append(_interpolate(previous, current, factor))
        if current_inside:
            output.append(current)
        previous = current
    return output


def build_tailoring(coat, donor, material='HasanBinding'):
    """Return [(mesh_object, material_name)] for a single combined facing.

    Call after the final cloth fit and BEFORE donor export. The source mesh is
    untouched. The result uses identical vertex-group names/order; original
    vertex weights are copied and edge cuts interpolate them. No weight transfer
    from anatomy and no independently rigged garment component occurs here.
    """
    mesh = coat.data
    mesh.update()
    mesh.calc_loop_triangles()
    if not mesh.uv_layers.active:
        raise ValueError('Facing extraction requires the donor UV layer')
    if [group.name for group in coat.vertex_groups] != donor.names:
        raise ValueError('Facing must use the same canonical vertex-group order')
    regions = _boundary_regions(coat, donor)
    adjacency = [[] for _ in mesh.vertices]
    for edge in mesh.edges:
        a, b = tuple(edge.vertices)
        length = (mesh.vertices[a].co-mesh.vertices[b].co).length
        adjacency[a].append((b, length))
        adjacency[b].append((a, length))
    widths = {'front': .018, 'collar': .016, 'hem': .012, 'cuff_L': .012, 'cuff_R': .012}
    combined = [math.inf]*len(mesh.vertices)
    for name, edges in regions.items():
        distance = _distances(mesh, {index for edge in edges for index in edge}, adjacency)
        combined = [min(old, value/widths[name]) for old, value in zip(combined, distance)]

    vertices, faces, corner_uvs, weights = [], [], [], []
    source_triangles = 0
    for triangle in mesh.loop_triangles:
        records = []
        for loop_index in triangle.loops:
            index = mesh.loops[loop_index].vertex_index
            vertex = mesh.vertices[index]
            records.append({'co': vertex.co.copy(), 'normal': vertex.normal.copy(),
                            'uv': mesh.uv_layers.active.data[loop_index].uv.copy(),
                            'weights': {group.group: group.weight for group in vertex.groups},
                            'distance': combined[index]})
        polygon = _clip_triangle(records)
        if len(polygon) < 3:
            continue
        area = sum(((polygon[i]['co']-polygon[0]['co']).cross(
                    polygon[i+1]['co']-polygon[0]['co']).length
                    for i in range(1, len(polygon)-1)), 0.0)
        if area < 1e-10:
            continue
        source_triangles += 1
        start = len(vertices)
        faces.append(tuple(range(start, start+len(polygon))))
        for record in polygon:
            # Thin cloth facing, not an ornamental rail or displaced silhouette.
            vertices.append(record['co']+record['normal']*.0018)
            corner_uvs.append(record['uv'])
            total = sum(record['weights'].values())
            if total < .999 or total > 1.001:
                raise ValueError('Source/interpolated facing weights are not normalized')
            weights.append({index: value/total for index, value in record['weights'].items() if value > 0})
    if source_triangles < 30:
        raise ValueError('Facing extraction did not capture usable donor boundaries')

    data = bpy.data.meshes.new('HasanTailoringDonorMesh')
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new('HasanTailoringDonor', data)
    bpy.context.scene.collection.objects.link(obj)
    obj.matrix_world = coat.matrix_world.copy()
    for group in coat.vertex_groups:
        obj.vertex_groups.new(name=group.name)
    for index, influences in enumerate(weights):
        for group, value in influences.items():
            obj.vertex_groups[group].add([index], value, 'REPLACE')
    uv_layer = data.uv_layers.new(name=mesh.uv_layers.active.name)
    for polygon in data.polygons:
        polygon.use_smooth = True
        for loop_index in polygon.loop_indices:
            uv_layer.data[loop_index].uv = corner_uvs[data.loops[loop_index].vertex_index]

    # Adjacent extracted triangles share exactly the same source interpolation;
    # weld only numerical duplicates. UV seams stay per-loop, original weights
    # survive. This does not bridge the crossover/slit's distinct boundaries.
    bm = bmesh.new()
    bm.from_mesh(data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=.0000001)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(data)
    bm.free()
    data.update()
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    thickness = obj.modifiers.new('TurnedDonorEdgeThickness', 'SOLIDIFY')
    thickness.thickness = .001
    thickness.offset = 0
    thickness.use_even_offset = False
    bpy.ops.object.modifier_apply(modifier=thickness.name)
    obj['source_coat'] = coat.name
    obj['authoring_status'] = 'DRAFT_REQUIRES_MOTION_AND_MOUNTED_QA'
    obj['requires_coat_corrective_transfer'] = True
    obj['boundary_provenance'] = json.dumps({
        'method': 'original donor triangle iso-distance extraction',
        'selectedBoundaryEdges': {name: len(edges) for name, edges in regions.items()},
        'bandWidthMetres': widths, 'surfaceOffsetMetres': .0018,
        'thicknessMetres': .001, 'sourceTriangles': source_triangles,
        'weights': 'original values or original-triangle interpolation only',
        'historicalClaim': 'practical facing interpretation; no new historical attribution'})
    return [(obj, material)]
