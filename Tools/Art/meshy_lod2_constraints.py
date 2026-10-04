"""Deterministic, attribute-aware candidate simplification; no DCC/file side effects.

Coincident FBX split vertices are shared geometrically only. Per-corner UV,
normal and weights remain distinct. UV/material/hard-normal seams, boundaries,
nonmanifold edges, weight discontinuities and declared features are locked.
Ordinary smoothly varying adjacent skin weights are NOT locked.
"""
from __future__ import annotations

import heapq
import math
from collections import Counter, defaultdict


def sub(a, b):
    return tuple(x - y for x, y in zip(a, b))


def dot(a, b):
    return sum(x * y for x, y in zip(a, b))


def cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def normalized(a):
    length = math.sqrt(dot(a, a))
    if length < 1e-12:
        raise ValueError("Zero normal")
    return tuple(x / length for x in a)


def weights(record, vertex):
    result = defaultdict(float)
    for index, weight in zip(record["boneIndices"][vertex * 4:vertex * 4 + 4], record["boneWeights"][vertex * 4:vertex * 4 + 4]):
        if weight > 0:
            result[index] += weight
    return dict(result)


def weight_difference(a, b):
    return sum(abs(a.get(i, 0) - b.get(i, 0)) for i in a.keys() | b.keys())


def seam_reasons(uses, hard_normal_degrees=55.0):
    """Each use is (material, endpoint0 attributes, endpoint1 attributes)."""
    if len(uses) != 2:
        return {"boundary" if len(uses) == 1 else "nonmanifold"}
    first, second = uses
    reasons = set()
    if first[0] != second[0]:
        reasons.add("material")
    threshold = math.cos(math.radians(hard_normal_degrees))
    for a, b in zip(first[1:], second[1:]):
        if max(abs(x - y) for x, y in zip(a["uv"], b["uv"])) > 1e-6:
            reasons.add("uv")
        if dot(normalized(a["normal"]), normalized(b["normal"])) < threshold:
            reasons.add("hard_normal")
        # Compare DIFFERENT sides of the SAME position, not adjacent positions.
        if weight_difference(a["weights"], b["weights"]) > 1e-4:
            reasons.add("weight_discontinuity")
    return reasons


def blend_attributes(a, b, t):
    mixed = {i: (1 - t) * a["weights"].get(i, 0) + t * b["weights"].get(i, 0)
             for i in a["weights"].keys() | b["weights"].keys()}
    ordered = sorted(mixed.items(), key=lambda p: (-p[1], p[0]))
    lost = sum(w for _, w in ordered[4:])
    kept = ordered[:4]
    total = sum(w for _, w in kept)
    if total <= 0:
        raise ValueError("Unweighted collapse")
    return {"uv": tuple((1 - t) * x + t * y for x, y in zip(a["uv"], b["uv"])),
            "normal": normalized(tuple((1 - t) * x + t * y for x, y in zip(a["normal"], b["normal"]))),
            "weights": {i: w / total for i, w in kept if w > 0}}, lost


def simplify(record, target_triangles, feature_test=None, hard_normal_degrees=55.0):
    positions, source_to_topology, lookup = [], [], {}
    for i in range(len(record["positions"]) // 3):
        p = tuple(record["positions"][i * 3:i * 3 + 3])
        key = tuple(round(x / 1e-6) for x in p)
        if key not in lookup:
            lookup[key] = len(positions)
            positions.append(p)
        source_to_topology.append(lookup[key])
    faces, attributes, materials = {}, {}, {}
    vertex_faces = [set() for _ in positions]
    edge_uses = defaultdict(list)
    feature_faces, locked, counts = set(), set(), Counter()
    for face in range(len(record["triangles"]) // 3):
        raw = record["triangles"][face * 3:face * 3 + 3]
        ids = tuple(source_to_topology[i] for i in raw)
        if len(set(ids)) != 3:
            raise ValueError("Coincident source triangle is degenerate; do not silently delete it")
        attrs = [{"uv": tuple(record["uv"][i * 2:i * 2 + 2]),
                  "normal": tuple(record["normals"][i * 3:i * 3 + 3]), "weights": weights(record, i)} for i in raw]
        faces[face], attributes[face], materials[face] = ids, attrs, record["triangleMaterialIndices"][face]
        for v in ids:
            vertex_faces[v].add(face)
        if feature_test and any(feature_test(positions[v], attrs[k]) for k, v in enumerate(ids)):
            feature_faces.add(face)
            locked.update(ids)
        for k in range(3):
            a, b = ids[k], ids[(k + 1) % 3]
            pair = (attrs[k], attrs[(k + 1) % 3])
            if a > b:
                a, b = b, a
                pair = pair[::-1]
            edge_uses[(a, b)].append((materials[face], *pair))
    for edge, uses in edge_uses.items():
        reasons = seam_reasons(uses, hard_normal_degrees)
        if reasons:
            locked.update(edge)
            counts.update(reasons)
    # Keep tiny disconnected source components, including feather/hair details.
    adjacency = [set() for _ in positions]
    for a, b in edge_uses:
        adjacency[a].add(b)
        adjacency[b].add(a)
    visited, protected_components = set(), 0
    for start in range(len(positions)):
        if start in visited:
            continue
        component, pending = set(), [start]
        while pending:
            v = pending.pop()
            if v in component:
                continue
            component.add(v)
            pending.extend(adjacency[v] - component)
        visited.update(component)
        component_faces = set().union(*(vertex_faces[v] for v in component))
        extent = tuple(max(positions[v][i] for v in component) - min(positions[v][i] for v in component) for i in range(3))
        if len(component_faces) <= 64 or dot(extent, extent) < .08 ** 2:
            locked.update(component)
            feature_faces.update(component_faces)
            protected_components += 1
    frozen_positions = {v: positions[v] for v in locked}
    frozen_faces = {f: (faces[f], attributes[f]) for f in feature_faces}
    quadrics = [[0.0] * 16 for _ in positions]
    for ids in faces.values():
        a, b, c = (positions[v] for v in ids)
        n = normalized(cross(sub(b, a), sub(c, a)))
        plane = (*n, -dot(n, a))
        q = [x * y for x in plane for y in plane]
        for v in ids:
            quadrics[v] = [x + y for x, y in zip(quadrics[v], q)]
    versions = [0] * len(positions)
    heap, collapses, max_discarded_weight = [], 0, 0.0

    def neighbors(v):
        return set().union(*(set(faces[f]) for f in vertex_faces[v])) - {v} if vertex_faces[v] else set()

    def offer(a, b):
        if a > b:
            a, b = b, a
        if a in locked or b in locked or not vertex_faces[a] or not vertex_faces[b]:
            return
        common = vertex_faces[a] & vertex_faces[b]
        if len(common) != 2:
            return
        q = [x + y for x, y in zip(quadrics[a], quadrics[b])]
        choices = []
        for t in (0.0, .5, 1.0):
            p = tuple((1 - t) * x + t * y for x, y in zip(positions[a], positions[b]))
            h = (*p, 1.0)
            cost = max(0.0, sum(q[r * 4 + c] * h[r] * h[c] for r in range(4) for c in range(4)))
            # Stable tie-breaking favors short edges, then central collapse.
            choices.append((cost, abs(t - .5), t, p))
        cost, _, t, p = min(choices)
        heapq.heappush(heap, (cost, dot(sub(positions[a], positions[b]), sub(positions[a], positions[b])), a, b,
                              versions[a], versions[b], t, p))

    for a, b in sorted(edge_uses):
        offer(a, b)
    while len(faces) > target_triangles and heap:
        _, _, a, b, va, vb, t, p = heapq.heappop(heap)
        if va != versions[a] or vb != versions[b] or not vertex_faces[a] or not vertex_faces[b]:
            continue
        shared = vertex_faces[a] & vertex_faces[b]
        if len(shared) != 2:
            continue
        opposite = set().union(*(set(faces[f]) for f in shared)) - {a, b}
        if neighbors(a) & neighbors(b) != opposite:
            continue  # Manifold link condition: don't bridge unrelated surfaces.
        affected = vertex_faces[a] | vertex_faces[b]
        safe = True
        for f in affected - shared:
            old = [positions[v] for v in faces[f]]
            new = [p if v in (a, b) else positions[v] for v in faces[f]]
            old_normal = cross(sub(old[1], old[0]), sub(old[2], old[0]))
            new_normal = cross(sub(new[1], new[0]), sub(new[2], new[0]))
            if dot(new_normal, new_normal) <= 4e-28 or dot(normalized(old_normal), normalized(new_normal)) < .5:
                safe = False
                break
        if not safe:
            continue
        exemplar_a = min(vertex_faces[a])
        exemplar_b = min(vertex_faces[b])
        attr, discarded = blend_attributes(attributes[exemplar_a][faces[exemplar_a].index(a)],
                                            attributes[exemplar_b][faces[exemplar_b].index(b)], t)
        max_discarded_weight = max(max_discarded_weight, discarded)
        adjacent = set().union(*(set(faces[f]) for f in affected))
        for f in list(affected):
            old = faces[f]
            for v in old:
                vertex_faces[v].discard(f)
            if f in shared:
                del faces[f], attributes[f], materials[f]
                continue
            faces[f] = tuple(a if v == b else v for v in old)
            attributes[f] = [attr if v in (a, b) else attributes[f][k] for k, v in enumerate(old)]
            for v in faces[f]:
                vertex_faces[v].add(f)
        positions[a] = p
        quadrics[a] = [x + y for x, y in zip(quadrics[a], quadrics[b])]
        collapses += 1
        for v in adjacent:
            versions[v] += 1
        for v in sorted(adjacent):
            for other in sorted(neighbors(v)):
                offer(v, other)
    if any(positions[v] != p for v, p in frozen_positions.items()):
        raise AssertionError("Protected vertex moved")
    if any(f not in faces or (faces[f], attributes[f]) != frozen for f, frozen in frozen_faces.items()):
        raise AssertionError("Protected feature triangle/attribute changed")
    output = {key: [] for key in ("positions", "normals", "uv", "triangles", "boneIndices", "boneWeights", "triangleMaterialIndices")}
    output["materialNames"] = list(record["materialNames"])
    export_lookup = {}
    for f in sorted(faces):
        output["triangleMaterialIndices"].append(materials[f])
        for k, v in enumerate(faces[f]):
            attr = attributes[f][k]
            ordered = sorted(attr["weights"].items(), key=lambda pair: (-pair[1], pair[0]))
            key = (positions[v], attr["normal"], attr["uv"], tuple(ordered))
            if key not in export_lookup:
                export_lookup[key] = len(output["positions"]) // 3
                output["positions"].extend(positions[v])
                output["normals"].extend(attr["normal"])
                output["uv"].extend(attr["uv"])
                while len(ordered) < 4:
                    ordered.append((0, 0.0))
                output["boneIndices"].extend(i for i, _ in ordered)
                output["boneWeights"].extend(w for _, w in ordered)
            output["triangles"].append(export_lookup[key])
    return output, {"algorithm": "Constrained endpoint/midpoint quadric edge collapse; no seam welding across attributes",
                    "sourceTriangles": len(record["triangles"]) // 3, "triangles": len(faces), "collapses": collapses,
                    "targetTriangles": target_triangles, "targetReached": len(faces) <= target_triangles,
                    "protectedVertices": len(locked), "protectedFeatureFaces": len(feature_faces),
                    "protectedSmallComponents": protected_components, "seamEdgesByReason": dict(counts),
                    "protectedCoordinatesAndFeaturesUnchanged": True,
                    "maximumDiscardedWeightMass": max_discarded_weight,
                    "weightPolicy": "Only derived vertices interpolate adjacent weights; strongest4 normalized. Smooth gradients are not seams."}
