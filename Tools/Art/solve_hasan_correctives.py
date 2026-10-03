"""Author bounded pose-space corrections on the EXISTING Hasan donor surfaces.

Run with Blender --background --disable-autoexec --python this_file -- [options].
This script does not create a garment, rig, collider asset, animation, or runtime
driver. It consumes bind-to-posed skin matrices sampled from the canonical rig.
Default is a dry run: derived JSON goes under TestResults, not into Unity assets.
--apply atomically replaces the validated coat/consolidated JSON sources.

The numerical result is diagnostic, not production or visual acceptance. Open
garment boundaries are retained, and unresolved collisions are explicitly counted.
"""
import argparse
import copy
import hashlib
import json
import math
import os
from pathlib import Path
import re
import struct
import sys
import tempfile

from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'UnityProject/Assets/FOC/ArtSource/HistoricalSlice'
DONOR = SOURCE / 'HasanDonor'
REPORT = ROOT / 'TestResults/HasanDonor/Correctives'
CLEARANCE = .008
MAX_OFFSET = .20
BINDING_MAX_DISTANCE = .01


def vector_rows(values):
    return [Vector(values[i:i+3]) for i in range(0, len(values), 3)]


def smoothstep(a, b, value):
    t = max(0., min(1., (value-a)/(b-a)))
    return t*t*(3.-2.*t)


def finite(value):
    return all(math.isfinite(float(v)) for v in value)


def skin_matrices(part, transforms):
    matrices = []
    for vi in range(len(part['positions'])//3):
        indices = part['boneIndices'][vi*4:vi*4+4]
        weights = part['boneWeights'][vi*4:vi*4+4]
        if abs(sum(weights)-1.) > .0001 or min(weights) < 0:
            raise ValueError('Invalid original skin weights')
        matrix = Matrix(tuple(tuple(sum(transforms[bi][r][c]*w
                    for bi, w in zip(indices, weights)) for c in range(4))
                    for r in range(4)))
        if not finite(v for row in matrix for v in row):
            raise ValueError('Nonfinite skin transform')
        matrices.append(matrix)
    return matrices


def inverse_delta(matrix, delta):
    linear = matrix.to_3x3()
    if abs(linear.determinant()) > 1e-6:
        result = linear.inverted() @ delta
    else:
        # Damped least squares instead of silently substituting identity for a
        # nearly singular blend matrix. Reprojection still must agree to 1 mm.
        transpose = linear.transposed()
        regularized = transpose @ linear
        for i in range(3):
            regularized[i][i] += 1e-6
        result = regularized.inverted() @ (transpose @ delta)
    if not finite(result) or (linear @ result-delta).length > .001:
        raise ValueError('Unstable inverse skin mapping')
    return result


def geometric_topology(part):
    """Weld only for solver adjacency, not for exported positions/UV/topology."""
    positions = vector_rows(part['positions'])
    groups, keys, mapping = [], {}, []
    for vi, pos in enumerate(positions):
        key = tuple(round(float(v), 6) for v in pos)
        if key not in keys:
            keys[key] = len(groups)
            groups.append([])
        gi = keys[key]
        mapping.append(gi)
        groups[gi].append(vi)
    neighbors = [set() for _ in groups]
    edge_counts = {}
    for i in range(0, len(part['triangles']), 3):
        tri = [mapping[v] for v in part['triangles'][i:i+3]]
        for a, b in zip(tri, tri[1:]+tri[:1]):
            if a == b:
                continue
            neighbors[a].add(b)
            neighbors[b].add(a)
            key = tuple(sorted((a, b)))
            edge_counts[key] = edge_counts.get(key, 0)+1
    boundary = {i for edge, count in edge_counts.items() if count == 1 for i in edge}
    return positions, groups, neighbors, boundary


class Collider:
    """Posed body or trousers; separate source-side BVHs avoid crossing vents."""
    def __init__(self, part, transforms, label):
        self.label = label
        self.rest = vector_rows(part['positions'])
        self.skin = skin_matrices(part, transforms)
        self.positions = [m @ p for m, p in zip(self.skin, self.rest)]
        rest_normals = vector_rows(part['normals'])
        normals = [(m.to_3x3() @ n).normalized()
                   for m, n in zip(self.skin, rest_normals)]
        groups = {-1: [], 1: []}
        for i in range(0, len(part['triangles']), 3):
            tri = list(part['triangles'][i:i+3])
            a, b, c = [self.positions[v] for v in tri]
            cross = (b-a).cross(c-a)
            if cross.length_squared < 1e-16:
                continue
            # Source JSON winding and imported coordinate handedness need not
            # agree. Original artist normals determine the outward orientation.
            expected = sum((normals[v] for v in tri), Vector())
            if cross.dot(expected) < 0:
                tri.reverse()
            xs = [self.rest[v].x for v in tri]
            if max(xs) >= -.025:
                groups[1].append(tri)
            if min(xs) <= .025:
                groups[-1].append(tri)
        self.trees = {side: BVHTree.FromPolygons(self.positions, triangles,
                      all_triangles=True, epsilon=1e-7)
                      for side, triangles in groups.items() if triangles}

    def inspect(self, point, side):
        tree = self.trees.get(side)
        if tree is None:
            return None
        location, normal, index, distance = tree.find_nearest(point, MAX_OFFSET+.04)
        if location is None:
            return None
        signed = (point-location).dot(normal)
        # Signed closest-surface normals give a reliable local test even though
        # trousers have open cuffs. Far negative projections could be an open
        # rim or opposite body surface; reject rather than pulling through it.
        ambiguous = signed < -.12 or (distance > .035 and abs(signed) < distance*.35)
        return location+normal*CLEARANCE, signed, ambiguous

    def constraint(self, point, side):
        hit = self.inspect(point, side)
        if hit is None or hit[1] >= CLEARANCE or hit[2]:
            return None
        return hit[:2]


def measure(points, rest, colliders, eligible):
    count, inside, worst, ambiguous_count = 0, 0, 0., 0
    for vi, point in enumerate(points):
        if not eligible[vi]:
            continue
        side = 1 if rest[vi].x >= 0 else -1
        observations = [hit for collider in colliders
                        if (hit := collider.inspect(point, side)) is not None]
        needs = [hit[1] for hit in observations]
        ambiguous_count += any(hit[2] and hit[1] < CLEARANCE-.001 for hit in observations)
        if needs and min(needs) < CLEARANCE-.001:
            count += 1
            inside += min(needs) < -.001
            worst = max(worst, CLEARANCE-min(needs))
    return {'clearanceViolations': count, 'insideVertices': inside,
            'ambiguousViolationsNotProjected': ambiguous_count,
            'worstRequiredMeters': round(worst, 7)}


def normal_deltas(rest, adjusted, triangles, source_normals):
    def accumulated(positions):
        values = [Vector() for _ in positions]
        for i in range(0, len(triangles), 3):
            a, b, c = triangles[i:i+3]
            normal = (positions[b]-positions[a]).cross(positions[c]-positions[a])
            for v in (a, b, c):
                values[v] += normal
        return [n.normalized() if n.length_squared > 1e-18 else Vector((0, 1, 0))
                for n in values]
    before, after = accumulated(rest), accumulated(adjusted)
    deltas = []
    for original, a, b in zip(vector_rows(source_normals), before, after):
        if a.dot(original) < 0:
            a, b = -a, -b
        # Rotate the authored normal by the geometric change; don't overwrite
        # authored smooth normals with unrelated flat triangle normals.
        changed = a.rotation_difference(b) @ original
        deltas += list(changed-original)
    return deltas


def solve_part(part, transforms, colliders, name, iterations):
    rest, groups, neighbors, boundary = geometric_topology(part)
    skins = skin_matrices(part, transforms)
    posed = [m @ p for m, p in zip(skins, rest)]
    adjusted = [p.copy() for p in posed]
    freedom = [1.-smoothstep(.975, 1.04, p.y) for p in rest]
    eligible = [f > .001 and p.y < 1.04 for p, f in zip(rest, freedom)]
    before = measure(posed, rest, colliders, eligible)
    capped = set()

    if name == 'Pose_Neutral':
        # Neutral is the authored basis, not a hidden automatic remodelling pass.
        # Any baseline overlap is reported without sculpting around it.
        return ({'name': name, 'deltaPositions': [0.]*len(part['positions']),
                 'deltaNormals': [0.]*len(part['normals'])},
                {'before': before, 'after': dict(before), 'eligibleVertices': sum(eligible),
                 'pinnedVertices': len(rest)-sum(eligible), 'maxBindOffsetMeters': 0.,
                 'cappedVertices': 0, 'boundariesRetained': True, 'neutralBasisUnchanged': True})

    def project():
        for vi, point in enumerate(adjusted):
            if not eligible[vi]:
                continue
            side = 1 if rest[vi].x >= 0 else -1
            for collider in colliders:
                hit = collider.constraint(point, side)
                if hit is None:
                    continue
                destination, _ = hit
                movement = (destination-point)*freedom[vi]
                proposed = point+movement
                local_delta = inverse_delta(skins[vi], proposed-posed[vi])
                # Preserve source-side of open front/back boundaries in bind
                # space: never snap one panel across its opposite leg.
                if abs(rest[vi].x) > .003 and (rest[vi]+local_delta).x*side < .002:
                    local_delta.x = .002*side-rest[vi].x
                if local_delta.length > MAX_OFFSET:
                    local_delta *= MAX_OFFSET/local_delta.length
                    capped.add(vi)
                adjusted[vi] = skins[vi] @ (rest[vi]+local_delta)
                point = adjusted[vi]

    for _ in range(iterations):
        project()
        displacement = [p-o for p, o in zip(adjusted, posed)]
        group_delta = [sum((displacement[i] for i in group), Vector())/len(group)
                       for group in groups]
        for gi, group in enumerate(groups):
            # Geometric boundaries keep their collision correction but aren't
            # tangentially smoothed across the open seam/cuff/hem.
            if gi in boundary or not neighbors[gi]:
                continue
            average = sum((group_delta[n] for n in neighbors[gi]), Vector())/len(neighbors[gi])
            target = group_delta[gi].lerp(average, .28)
            for vi in group:
                if eligible[vi]:
                    adjusted[vi] = posed[vi]+target*freedom[vi]
        # Export duplicates at UV seams move together. Their original topology
        # is unchanged; this only prevents a corrective from opening cracks.
        for group in groups:
            if len(group) > 1:
                mean = sum((adjusted[i]-posed[i] for i in group), Vector())/len(group)
                for vi in group:
                    adjusted[vi] = posed[vi]+mean if eligible[vi] else posed[vi].copy()
    for _ in range(3):
        project()
    deltas = [inverse_delta(m, p-o) for m, p, o in zip(skins, adjusted, posed)]
    if any(not finite(v) or v.length > MAX_OFFSET+.00002 for v in deltas):
        raise ValueError('Corrective exceeded finite 20 cm bind-space bound')
    if any(v.length > 1e-7 for v, allowed in zip(deltas, eligible) if not allowed):
        raise ValueError('Pinned upper garment moved')
    shape = {'name': name,
             'deltaPositions': [round(float(v), 8) for delta in deltas for v in delta],
             'deltaNormals': [round(float(v), 8) for v in normal_deltas(rest,
                 [p+d for p, d in zip(rest, deltas)], part['triangles'], part['normals'])]}
    audit = {'before': before, 'after': measure(adjusted, rest, colliders, eligible),
             'eligibleVertices': sum(eligible), 'pinnedVertices': len(rest)-sum(eligible),
             'maxBindOffsetMeters': round(max((d.length for d in deltas), default=0), 7),
             'cappedVertices': len(capped), 'boundariesRetained': True}
    return shape, audit


def basis_fingerprint(part):
    keys = ('positions', 'normals', 'triangles', 'uv', 'boneIndices', 'boneWeights')
    return hashlib.sha256(json.dumps({k: part[k] for k in keys},
                           separators=(',', ':')).encode()).hexdigest()


def binding_parts(lod):
    return [(index, part) for index, part in enumerate(lod['parts'])
            if index > 0 and part.get('material') == 'HasanBinding']


def closest_triangle_barycentric(point, a, b, c):
    """Double-precision Voronoi-region projection, including very thin strips.

    mathutils dot products return single-precision values; subtracting their
    products loses all area information for the donor's narrow seam triangles.
    Python component products retain doubles. Project the ORIGINAL target point,
    not an already-rounded BVH closest point that may sit outside a thin face.
    """
    def sub(u, v):
        return tuple(float(x)-float(y) for x, y in zip(u, v))
    def dot(u, v):
        return sum(float(x)*float(y) for x, y in zip(u, v))
    ab, ac, ap = sub(b, a), sub(c, a), sub(point, a)
    d1, d2 = dot(ab, ap), dot(ac, ap)
    if d1 <= 0 and d2 <= 0:
        return (1., 0., 0.)
    bp = sub(point, b)
    d3, d4 = dot(ab, bp), dot(ac, bp)
    if d3 >= 0 and d4 <= d3:
        return (0., 1., 0.)
    vc = d1*d4-d3*d2
    if vc <= 0 and d1 >= 0 and d3 <= 0 and d1-d3 > 1e-30:
        v = d1/(d1-d3)
        return (1.-v, v, 0.)
    cp = sub(point, c)
    d5, d6 = dot(ab, cp), dot(ac, cp)
    if d6 >= 0 and d5 <= d6:
        return (0., 0., 1.)
    vb = d5*d2-d1*d6
    if vb <= 0 and d2 >= 0 and d6 <= 0 and d2-d6 > 1e-30:
        w = d2/(d2-d6)
        return (1.-w, 0., w)
    va = d3*d6-d5*d4
    denominator = d4-d3+d5-d6
    if va <= 0 and d4-d3 >= 0 and d5-d6 >= 0 and denominator > 1e-30:
        w = (d4-d3)/denominator
        return (0., 1.-w, w)
    area = va+vb+vc
    if abs(area) <= 1e-30:
        raise ValueError('Numerically degenerate original donor triangle')
    return (va/area, vb/area, vc/area)


def transfer_correctives(source_part, target_part):
    """Bind an existing donor-derived facing to its original coat triangles."""
    original_fingerprint = basis_fingerprint(target_part)
    source_positions = vector_rows(source_part['positions'])
    target_positions = vector_rows(target_part['positions'])
    if not target_positions:
        raise ValueError('Empty HasanBinding part cannot be bound')
    triangles = []
    for offset in range(0, len(source_part['triangles']), 3):
        tri = source_part['triangles'][offset:offset+3]
        a, b, c = [source_positions[i] for i in tri]
        if (b-a).cross(c-a).length_squared > 1e-18:
            triangles.append(tri)
    if not triangles:
        raise ValueError('No valid original coat triangle for binding transfer')
    tree = BVHTree.FromPolygons(source_positions, triangles, all_triangles=True)
    bindings, distances = [], []
    for vi, position in enumerate(target_positions):
        nearest, _, triangle_index, distance = tree.find_nearest(position)
        if nearest is None or not math.isfinite(distance) or distance > BINDING_MAX_DISTANCE:
            raise ValueError('Unbound HasanBinding vertex %d; nearest original coat gap=%s m; maximum=%s m'
                             % (vi, distance, BINDING_MAX_DISTANCE))
        tri = triangles[triangle_index]
        a, b, c = [source_positions[i] for i in tri]
        try:
            weights = closest_triangle_barycentric(position, a, b, c)
        except ValueError as error:
            raise ValueError('HasanBinding vertex %d triangle %s: %s' % (vi, tri, error)) from error
        if not finite(weights) or min(weights) < -1e-8 or max(weights) > 1.00000001:
            raise ValueError('HasanBinding vertex %d triangle %s yielded invalid barycentric weights %s'
                             % (vi, tri, weights))
        weights = [max(0., min(1., x)) for x in weights]
        total = sum(weights)
        weights = [x/total for x in weights]
        reconstructed = tuple(sum(float(source_positions[i][axis])*weight for i, weight in zip(tri, weights))
                              for axis in range(3))
        distance = math.sqrt(sum((float(position[i])-reconstructed[i])**2 for i in range(3)))
        if not math.isfinite(distance) or distance > BINDING_MAX_DISTANCE:
            raise ValueError('Unbound HasanBinding vertex %d triangle %s; precise gap=%s m; maximum=%s m'
                             % (vi, tri, distance, BINDING_MAX_DISTANCE))
        if abs(position.x) > .015 and all(source_positions[i].x*position.x < 0 for i in tri):
            raise ValueError('Binding would cross to the opposite coat panel')
        bindings.append((tri, weights))
        distances.append(distance)
    original_shapes = [s for s in target_part.get('blendShapes', []) if not s['name'].startswith('Pose_')]
    maximum_delta, pinned = 0., sum(p.y >= 1.04 for p in target_positions)
    source_shapes = [s for s in source_part.get('blendShapes', []) if s['name'].startswith('Pose_')]
    if not source_shapes or not any(s['name'] == 'Pose_Neutral' for s in source_shapes):
        raise ValueError('Original coat has no complete pose corrective source')
    for source_shape in source_shapes:
        if len(source_shape['deltaPositions']) != len(source_part['positions']) or not finite(source_shape['deltaPositions']):
            raise ValueError('Invalid original coat corrective during binding transfer')
        if source_shape['name'] == 'Pose_Neutral':
            if any(abs(v) > 1e-12 for v in source_shape['deltaPositions']+source_shape['deltaNormals']):
                raise ValueError('Original neutral corrective must remain exactly zero')
            shape = {'name': source_shape['name'], 'deltaPositions': [0.]*len(target_part['positions']),
                     'deltaNormals': [0.]*len(target_part['normals'])}
        else:
            source_deltas = vector_rows(source_shape['deltaPositions'])
            deltas = [sum((source_deltas[i]*weight for i, weight in zip(tri, weights)), Vector())
                      if position.y < 1.04 else Vector()
                      for position, (tri, weights) in zip(target_positions, bindings)]
            if any(not finite(v) or v.length > MAX_OFFSET+.00002 for v in deltas):
                raise ValueError('Transferred binding corrective is nonfinite or exceeds 20 cm')
            maximum_delta = max(maximum_delta, max((v.length for v in deltas), default=0))
            shape = {'name': source_shape['name'],
                     'deltaPositions': [round(float(v), 8) for delta in deltas for v in delta],
                     'deltaNormals': [round(float(v), 8) for v in normal_deltas(target_positions,
                         [p+d for p, d in zip(target_positions, deltas)], target_part['triangles'], target_part['normals'])]}
        original_shapes.append(shape)
    target_part['blendShapes'] = original_shapes
    if basis_fingerprint(target_part) != original_fingerprint:
        raise ValueError('Corrective transfer changed HasanBinding topology/basis/weights/UV')
    return {'boundVertices': len(bindings), 'unboundVertices': 0,
            'maxBindingDistanceMeters': round(max(distances), 8),
            'meanBindingDistanceMeters': round(sum(distances)/len(distances), 8),
            'maxAllowedBindingDistanceMeters': BINDING_MAX_DISTANCE,
            'maxTransferredBindOffsetMeters': round(maximum_delta, 7),
            'pinnedUpperVertices': pinned, 'neutralExactlyZero': True,
            'basisSha256': original_fingerprint, 'basisUnchanged': True,
            'poseShapes': len(source_shapes)}


def verify_additional_part(part, consolidated_parts, names, lod_index, part_index):
    fingerprint = basis_fingerprint(part)
    matches = [p for p in consolidated_parts if basis_fingerprint(p) == fingerprint]
    if len(matches) != 1 or part.get('blendShapes', []) != matches[0].get('blendShapes', []):
        raise ValueError('Derived binding consolidated basis/shapes do not match')
    shapes = [s for s in part.get('blendShapes', []) if s['name'].startswith('Pose_')]
    if len(shapes) != len(names) or {s['name'] for s in shapes} != {'Pose_'+n for n in names}:
        raise ValueError('Derived binding corrective names differ from metadata')
    rest, maximum = vector_rows(part['positions']), 0.
    for shape in shapes:
        if len(shape['deltaPositions']) != len(part['positions']) or len(shape['deltaNormals']) != len(part['normals']):
            raise ValueError('Invalid derived binding corrective buffer length')
        if not finite(shape['deltaPositions']) or not finite(shape['deltaNormals']):
            raise ValueError('Nonfinite derived binding corrective')
        if shape['name'] == 'Pose_Neutral' and any(abs(v) > 1e-12 for v in shape['deltaPositions']+shape['deltaNormals']):
            raise ValueError('Derived binding neutral must be exactly zero')
        for pos, delta in zip(rest, vector_rows(shape['deltaPositions'])):
            if delta.length > MAX_OFFSET+.00002 or (pos.y >= 1.04 and delta.length > 1e-7):
                raise ValueError('Derived binding corrective violates bounds or upper pin')
            maximum = max(maximum, delta.length)
    return {'lod': lod_index, 'partIndex': part_index, 'material': part['material'],
            'basisSha256': fingerprint, 'matchedConsolidatedBasisAndShapes': True,
            'neutralExactlyZero': True, 'finiteBoundedDeltas': True, 'upperGarmentPinned': True,
            'maxAuthoredBindOffsetMeters': round(maximum, 7),
            'collisionMeasurement': 'NOT_RUN_FOR_DERIVED_FACING'}


def float32(value):
    return struct.unpack('<f', struct.pack('<f', float(value)))[0]


def runtime_weights(features, samples):
    """Match HasanGarmentPoseCorrectives: serialized-order ties, top3, 1/d²."""
    current = [float32(v) for v in features]
    distances = []
    for index, sample in enumerate(samples):
        distance = sum((a-float32(b))**2 for a, b in zip(current, sample['features']))
        if distance <= 1e-10:
            return [(index, 1., distance)]
        distances.append((distance, index))
    nearest = sorted(distances, key=lambda entry: entry[0])[:3]
    total = sum(1./distance for distance, _ in nearest)
    # C# stores blend weight as float percent; reproduce that rounding too.
    return [(index, float32(100./distance/total)/100., distance)
            for distance, index in nearest]


def verify_runtime(samples_path, report_path):
    """Read-only held-out evaluation; interpolate bind deltas BEFORE skinning."""
    paths = [DONOR/'CLTH_Donor_HasanCoatDonor.focmesh.json',
             DONOR/'CHR_HasanAga_DonorDraft.focmesh.json',
             SOURCE/'Characters/BODY_OttomanMale_Standard.focmesh.json',
             DONOR/'CLTH_Donor_HasanTrousersDonor.focmesh.json']
    original_bytes = {path: path.read_bytes() for path in paths}
    coat, character, body, pants = [json.loads(original_bytes[p]) for p in paths]
    if len(coat['bones']) != 18 or any(s['bones'] != coat['bones'] for s in (character, body, pants)):
        raise ValueError('Runtime verification requires the unchanged canonical18 skeleton')
    metadata = coat.get('poseCorrectives', [])
    if not metadata or metadata != character.get('poseCorrectives'):
        raise ValueError('Coat and character corrective metadata do not agree')
    names = [sample['name'] for sample in metadata]
    if len(names) != len(set(names)) or 'Neutral' not in names:
        raise ValueError('Missing Neutral or duplicate corrective names')
    if any(len(s['features']) != 18 or not finite(s['features']) for s in metadata):
        raise ValueError('Invalid stored corrective features')
    sample_bytes = samples_path.read_bytes()
    poses = json.loads(sample_bytes)['poses']
    audits, checks = [], []
    if not poses:
        raise ValueError('No held-out validation samples')
    for li in range(3):
        part = coat['lods'][li]['parts'][0]
        fingerprint = basis_fingerprint(part)
        matches = [p for p in character['lods'][li]['parts'] if basis_fingerprint(p) == fingerprint]
        if len(matches) != 1:
            raise ValueError('Runtime consolidated coat basis does not match standalone coat')
        standalone_shapes = part.get('blendShapes', [])
        if standalone_shapes != matches[0].get('blendShapes', []):
            raise ValueError('Standalone and consolidated corrective shapes differ')
        shape_map = {s['name']: s for s in standalone_shapes if s['name'].startswith('Pose_')}
        if len(shape_map) != len(names) or set(shape_map) != {'Pose_'+n for n in names}:
            raise ValueError('Corrective shape names do not exactly match metadata')
        rest = vector_rows(part['positions'])
        deltas = []
        max_authored_offset = 0.
        for sample in metadata:
            shape = shape_map['Pose_'+sample['name']]
            if len(shape['deltaPositions']) != len(part['positions']) or len(shape['deltaNormals']) != len(part['normals']):
                raise ValueError('Corrective buffer lengths differ from unchanged basis')
            if not finite(shape['deltaPositions']) or not finite(shape['deltaNormals']):
                raise ValueError('Nonfinite stored corrective')
            vectors = vector_rows(shape['deltaPositions'])
            if sample['name'] == 'Neutral' and any(abs(v) > 1e-12 for v in shape['deltaPositions']+shape['deltaNormals']):
                raise ValueError('Neutral corrective must be exactly zero')
            for p, delta in zip(rest, vectors):
                if delta.length > MAX_OFFSET+.00002:
                    raise ValueError('Stored corrective exceeds 20 cm bound')
                if p.y >= 1.04 and delta.length > 1e-7:
                    raise ValueError('Stored corrective moved pinned upper garment')
                max_authored_offset = max(max_authored_offset, delta.length)
            deltas.append(vectors)
        checks.append({'lod': li, 'basisSha256': fingerprint,
                       'matchedConsolidatedBasisAndShapes': True,
                       'neutralExactlyZero': True, 'finiteBoundedDeltas': True,
                       'upperGarmentPinned': True,
                       'maxAuthoredBindOffsetMeters': round(max_authored_offset, 7)})
        for part_index, additional_part in binding_parts(coat['lods'][li]):
            checks.append(verify_additional_part(additional_part, character['lods'][li]['parts'], names, li, part_index))
        eligible = [p.y < 1.04 and 1.-smoothstep(.975, 1.04, p.y) > .001 for p in rest]
        for pose in poses:
            if len(pose['features']) != 18 or not finite(pose['features']):
                raise ValueError('Validation sample features must be 18 finite floats')
            flats = pose['matrices']
            if len(flats) != 18*16 or not finite(flats):
                raise ValueError('Validation sample must have 18 finite skin matrices')
            matrices = []
            for offset in range(0, len(flats), 16):
                row = flats[offset:offset+16]
                if max(abs(row[i]) for i in (12, 13, 14)) > .0001 or abs(row[15]-1.) > .0001:
                    raise ValueError('Validation matrices must be row-major affine')
                matrices.append(Matrix([row[i:i+4] for i in range(0, 16, 4)]))
            weights = runtime_weights(pose['features'], metadata)
            blended = [sum((deltas[index][vi]*weight for index, weight, _ in weights), Vector())
                       for vi in range(len(rest))]
            skins = skin_matrices(part, matrices)
            before = [m @ p for m, p in zip(skins, rest)]
            after = [m @ (p+d) for m, p, d in zip(skins, rest, blended)]
            colliders = [Collider(collider_part, matrices, label)
                         for label, source in (('body', body), ('pants', pants))
                         for collider_part in source['lods'][li]['parts']]
            audit = {'lod': li, 'pose': pose['name'],
                     'classification': 'exact-authored-pose' if weights[0][2] <= 1e-10 else 'held-out-interpolation',
                     'weights': [{'name': metadata[index]['name'], 'weight': weight,
                                  'distanceSquared': distance} for index, weight, distance in weights],
                     'before': measure(before, rest, colliders, eligible),
                     'after': measure(after, rest, colliders, eligible),
                     'affectedVertices': sum(v.length > 1e-7 for v in blended),
                     'maxBlendedBindOffsetMeters': round(max((v.length for v in blended), default=0), 7),
                     'maxPosedMovementMeters': round(max(((a-b).length for a, b in zip(after, before)), default=0), 7)}
            audits.append(audit)
            print(json.dumps(audit, separators=(',', ':')), flush=True)
    if any(path.read_bytes() != data for path, data in original_bytes.items()):
        raise ValueError('Source changed during read-only runtime verification')
    report = {'status': 'READ_ONLY_NUMERIC_DIAGNOSTIC_NOT_VISUAL_ACCEPTANCE',
              'sampleFile': str(samples_path), 'samplesSha256': hashlib.sha256(sample_bytes).hexdigest(),
              'sourceFilesUnchanged': True,
              'sourceSha256': {str(p.relative_to(ROOT)): hashlib.sha256(b).hexdigest() for p, b in original_bytes.items()},
              'interpolation': 'nearest3 1/distanceSquared; exact<=1e-10; bind blend then skin; float-percent rounding',
              'shapeAndBasisChecks': checks, 'audits': audits,
              'unresolvedInsideVertices': sum(a['after']['insideVertices'] for a in audits),
              'unresolvedClearanceViolations': sum(a['after']['clearanceViolations'] for a in audits),
              'maxAffectedVertices': max(a['affectedVertices'] for a in audits),
              'maxBlendedBindOffsetMeters': max(a['maxBlendedBindOffsetMeters'] for a in audits),
              'worstPoseByClearance': max(audits, key=lambda a: a['after']['worstRequiredMeters']),
              'limits': ['Checks vertices, not complete triangle-to-triangle cloth collision.',
                         'Only existing lower coat correction region; upper garment is pinned.',
                         'Ambiguous closest-surface observations remain counted, not hidden.']}
    destination = report_path/'runtime-collision-audit.json'
    atomic_batch({destination: json.dumps(report, indent=2, allow_nan=False).encode('utf-8')})
    print('HASAN_RUNTIME_CORRECTIVE_VERIFICATION_COMPLETE '+str(destination), flush=True)


def atomic_batch(payloads):
    """Stage all bytes first; restore replaced files if a later replace fails."""
    staged, previous, replaced = {}, {}, []
    try:
        for path, data in payloads.items():
            path.parent.mkdir(parents=True, exist_ok=True)
            previous[path] = path.read_bytes() if path.exists() else None
            handle, temp = tempfile.mkstemp(prefix=path.name+'.staged-', dir=path.parent)
            with os.fdopen(handle, 'wb') as stream:
                stream.write(data)
                stream.flush()
                os.fsync(stream.fileno())
            staged[path] = Path(temp)
        for path, temp in staged.items():
            os.replace(temp, path)
            replaced.append(path)
    except Exception:
        for path in reversed(replaced):
            old = previous[path]
            if old is None:
                path.unlink(missing_ok=True)
            else:
                handle, temp = tempfile.mkstemp(prefix=path.name+'.restore-', dir=path.parent)
                with os.fdopen(handle, 'wb') as stream:
                    stream.write(old)
                os.replace(temp, path)
        raise
    finally:
        for temp in staged.values():
            temp.unlink(missing_ok=True)


def selfcheck():
    # Pure numerical fixture, never an exported garment or production mesh.
    affine = Matrix.Translation((.1, -.2, .3)) @ Matrix.Rotation(.7, 4, 'Y')
    delta = Vector((.02, .03, -.01))
    recovered = inverse_delta(affine, affine.to_3x3() @ delta)
    assert (recovered-delta).length < 1e-6
    part = {'positions': [-.2, .7, 0, .2, .7, 0, 0, 1.1, 0],
            'normals': [0, 0, 1]*3, 'uv': [0, 0]*3, 'triangles': [0, 1, 2],
            'boneIndices': [0, 0, 0, 0]*3, 'boneWeights': [1, 0, 0, 0]*3}
    identity = [Matrix.Identity(4)]
    collider = Collider(part, identity, 'unit-plane')
    hit = collider.constraint(Vector((-.1, .8, -.01)), -1)
    assert hit is not None and abs(hit[0].z-CLEARANCE) < 1e-6
    assert collider.constraint(Vector((-.1, .8, .03)), -1) is None
    shape, audit = solve_part(part, identity, [collider], 'Pose_Selfcheck', 2)
    assert all(abs(x) < 1e-7 for x in shape['deltaPositions'][6:9])
    assert max(abs(x) for x in shape['deltaPositions']) <= MAX_OFFSET
    assert audit['after']['insideVertices'] == 0
    assert len(shape['deltaNormals']) == len(part['positions'])
    fixture = [{'name': 'A', 'features': [0.]*18}, {'name': 'B', 'features': [1.]+[0.]*17},
               {'name': 'C', 'features': [1.5]+[0.]*17}, {'name': 'D', 'features': [7.]+[0.]*17}]
    assert runtime_weights([0.]*18, fixture) == [(0, 1., 0.)]
    interpolated = runtime_weights([.5]+[0.]*17, fixture)
    assert [i for i, _, _ in interpolated] == [0, 1, 2]
    assert abs(interpolated[0][1]-4./9.) < 1e-7
    assert abs(interpolated[2][1]-1./9.) < 1e-7
    transfer_source = copy.deepcopy(part)
    transfer_source['blendShapes'] = [
        {'name': 'Pose_Neutral', 'deltaPositions': [0.]*9, 'deltaNormals': [0.]*9},
        {'name': 'Pose_Test', 'deltaPositions': [0, 0, .01, 0, 0, .02, 0, 0, 0], 'deltaNormals': [0.]*9}]
    target = copy.deepcopy(part)
    target['positions'] = [value+.002 if i % 3 == 2 else value for i, value in enumerate(target['positions'])]
    transfer_basis = basis_fingerprint(target)
    transfer_audit = transfer_correctives(transfer_source, target)
    assert transfer_audit['boundVertices'] == 3 and transfer_audit['unboundVertices'] == 0
    assert abs(transfer_audit['maxBindingDistanceMeters']-.002) < 1e-6
    assert basis_fingerprint(target) == transfer_basis
    transferred = next(s for s in target['blendShapes'] if s['name'] == 'Pose_Test')
    assert abs(transferred['deltaPositions'][2]-.01) < 1e-6
    assert abs(transferred['deltaPositions'][5]-.02) < 1e-6
    assert all(v == 0 for v in transferred['deltaPositions'][6:9])
    far_target = copy.deepcopy(target)
    far_target['positions'][2] = .025
    try:
        transfer_correctives(transfer_source, far_target)
    except ValueError as error:
        assert 'Unbound HasanBinding' in str(error)
    else:
        raise AssertionError('Distant binding must fail loudly')
    print('HASAN_CORRECTIVE_SELFCHECK_PASS', flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--samples', type=Path)
    parser.add_argument('--report', type=Path, default=REPORT)
    parser.add_argument('--iterations', type=int, default=12)
    parser.add_argument('--apply', action='store_true')
    parser.add_argument('--selfcheck', action='store_true')
    parser.add_argument('--verify-runtime', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    if args.selfcheck:
        selfcheck()
        return
    if args.samples is None:
        args.samples = ROOT/'TestResults/HasanDonor'/('pose-validation-samples.json' if args.verify_runtime else 'pose-samples.json')
    if args.verify_runtime:
        if args.apply:
            raise ValueError('Read-only --verify-runtime cannot be combined with --apply')
        verify_runtime(args.samples, args.report)
        return
    if not 1 <= args.iterations <= 40:
        raise ValueError('Iterations must be between 1 and 40')
    samples_bytes = args.samples.read_bytes()
    samples = json.loads(samples_bytes)
    poses = samples['poses']
    if not poses:
        raise ValueError('No pose samples')
    source_paths = [DONOR/'CLTH_Donor_HasanCoatDonor.focmesh.json',
                    DONOR/'CHR_HasanAga_DonorDraft.focmesh.json']
    garments = [json.loads(p.read_text(encoding='utf-8')) for p in source_paths]
    coat, character = garments
    body = json.loads((SOURCE/'Characters/BODY_OttomanMale_Standard.focmesh.json').read_text())
    pants = json.loads((DONOR/'CLTH_Donor_HasanTrousersDonor.focmesh.json').read_text())
    sources = garments+[body, pants]
    if any(source['bones'] != character['bones'] for source in sources):
        raise ValueError('Canonical skeleton differs among existing source assets')
    feature_count = 18
    metadata, unique = [], set()
    for pose in poses:
        name = pose['name']
        if not re.fullmatch(r'[A-Za-z0-9_]+', name):
            raise ValueError('Pose sample names must be plain identifiers')
        if name in unique or len(pose['features']) != feature_count or not finite(pose['features']):
            raise ValueError('Duplicate sample name or inconsistent/nonfinite features')
        unique.add(name)
        flat_matrices = pose['matrices']
        if len(flat_matrices) != len(character['bones'])*16:
            raise ValueError('Pose must provide all canonical bone skin matrices')
        pose['_matrixRows'] = [flat_matrices[i:i+16] for i in range(0, len(flat_matrices), 16)]
        for flat in pose['_matrixRows']:
            if len(flat) != 16 or not finite(flat) or max(abs(flat[i]) for i in (12, 13, 14)) > .0001 or abs(flat[15]-1) > .0001:
                raise ValueError('Expected finite row-major affine skin matrices')
        metadata.append({'name': name, 'features': pose['features']})
    audits, transfers = [], []
    if 'Neutral' not in unique:
        raise ValueError('Explicit Neutral sample is required')
    original_basis = [[basis_fingerprint(lod['parts'][0]) for lod in s['lods']] for s in (coat,)]
    for source in (coat,):
        for lod in source['lods']:
            lod['parts'][0]['blendShapes'] = [s for s in lod['parts'][0].get('blendShapes', []) if not s['name'].startswith('Pose_')]
    for li in range(3):
        targets = [coat['lods'][li]['parts'][0]]
        for pose, meta in zip(poses, metadata):
            matrices = [Matrix([flat[i:i+4] for i in range(0, 16, 4)]) for flat in pose['_matrixRows']]
            colliders = [Collider(part, matrices, label)
                         for label, source in (('body', body), ('pants', pants))
                         for part in source['lods'][li]['parts']]
            for label, part in zip(('coat',), targets):
                shape, audit = solve_part(part, matrices, colliders, 'Pose_'+meta['name'], args.iterations)
                part.setdefault('blendShapes', []).append(shape)
                audits.append({'lod': li, 'part': label, 'pose': pose['name'], **audit})
                print(json.dumps(audits[-1], separators=(',', ':')), flush=True)
    for si, source in enumerate((coat,)):
        for li, lod in enumerate(source['lods']):
            part = lod['parts'][0]
            fingerprint = basis_fingerprint(part)
            if fingerprint != original_basis[si][li]:
                raise ValueError('Solver changed the original basis/topology/UV/weights')
            matches = [p for p in character['lods'][li]['parts'] if basis_fingerprint(p) == fingerprint]
            if len(matches) != 1:
                raise ValueError('Cannot uniquely identify consolidated '+source['assetId']+' LOD '+str(li))
            matches[0]['blendShapes'] = copy.deepcopy(part['blendShapes'])
            for part_index, additional_part in binding_parts(lod):
                transfer = transfer_correctives(part, additional_part)
                additional_fingerprint = basis_fingerprint(additional_part)
                additional_matches = [p for p in character['lods'][li]['parts']
                                      if basis_fingerprint(p) == additional_fingerprint]
                if len(additional_matches) != 1:
                    raise ValueError('Cannot uniquely identify consolidated HasanBinding part')
                additional_matches[0]['blendShapes'] = copy.deepcopy(additional_part['blendShapes'])
                transfers.append({'lod': li, 'partIndex': part_index, **transfer})
    for source in garments:
        source['poseCorrectives'] = metadata
        source['poseCorrectiveAuthoring'] = {'tool': 'Tools/Art/solve_hasan_correctives.py',
            'samplesSha256': hashlib.sha256(samples_bytes).hexdigest(),
            'scope': 'existing donor skirt only; canonical bones/basis/UV/weights unchanged',
            'status': 'DRAFT_REQUIRES_FULL_MOTION_VISUAL_QA'}
    report = {'status': 'DRAFT_NOT_VISUAL_ACCEPTANCE', 'applied': args.apply,
              'samplesSha256': hashlib.sha256(samples_bytes).hexdigest(),
              'clearanceMeters': CLEARANCE, 'maxAllowedBindOffsetMeters': MAX_OFFSET,
              'poses': metadata, 'audits': audits, 'derivedBindingTransfers': transfers,
              'unresolvedInsideVertices': sum(a['after']['insideVertices'] for a in audits),
              'unresolvedClearanceViolations': sum(a['after']['clearanceViolations'] for a in audits),
              'limits': ['Local oriented closest-surface collision, not cloth simulation.',
                         'Upper garment y>=1.04 pinned; no shoulder correction authored.',
                         'Between-sample blending must be tested in actual runtime motion.']}
    outputs = {(path if args.apply else args.report/'Derived'/path.name):
               json.dumps(source, separators=(',', ':'), allow_nan=False).encode('utf-8')
               for path, source in zip(source_paths, garments)}
    outputs[args.report/'collision-audit.json'] = json.dumps(report, indent=2, allow_nan=False).encode('utf-8')
    atomic_batch(outputs)
    print('HASAN_DONOR_CORRECTIVES_AUTHORED_DRAFT '+str(args.report/'collision-audit.json'), flush=True)


if __name__ == '__main__':
    main()
