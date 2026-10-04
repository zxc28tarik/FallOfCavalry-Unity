"""Read-only checks for derived Meshy LOD JSONs; not Unity/render acceptance."""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
import math
from collections import defaultdict
from pathlib import Path


def require(condition, message):
    if not condition:
        raise ValueError(message)


def hash_file(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def triples(values):
    return [values[i:i + 3] for i in range(0, len(values), 3)]


def area(a, b, c):
    u, v = [b[i] - a[i] for i in range(3)], [c[i] - a[i] for i in range(3)]
    cross = (u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0])
    return .5 * math.sqrt(sum(x * x for x in cross))


def validate(record):
    require(record["format"] == "FOC_MESHY_REST_LOD_MESH_V1", "Unknown format")
    require(record["coordinates"] == "BLENDER_WORLD_METERS_Z_UP", "Unknown coordinate frame")
    bones = record["bones"]
    names = {b["name"] for b in bones}
    require(len(bones) == len(names) == 23, "Expected original23 distinct named bones")
    for bone in bones:
        require(not bone["parent"] or bone["parent"] in names, "Unknown parent bone")
        require(len(bone["restWorldMatrix"]) == 16 and len(bone["headWorld"]) == len(bone["tailWorld"]) == 3,
                "Malformed bone matrix/landmarks")
        require(all(math.isfinite(v) for v in bone["restWorldMatrix"] + bone["headWorld"] + bone["tailWorld"]), "Nonfinite bone")
        require(all(abs(bone["restWorldMatrix"][12 + i] - (1.0 if i == 3 else 0.0)) < 1e-6 for i in range(4)), "Nonaffine row-major matrix")
        require(max(abs(bone["restWorldMatrix"][3 + axis * 4] - bone["headWorld"][axis]) for axis in range(3)) < 1e-5,
                "Rest matrix translation and head landmark disagree")
    count = len(record["positions"]) // 3
    require(count > 0 and len(record["positions"]) == count * 3, "Position length")
    require(len(record["normals"]) == count * 3 and len(record["uv"]) == count * 2, "Normal/UV length")
    require(len(record["boneIndices"]) == len(record["boneWeights"]) == count * 4, "Weight/index length")
    require(len(record["triangles"]) > 0 and len(record["triangles"]) % 3 == 0, "Triangle length")
    for name in ("positions", "normals", "uv", "boneWeights"):
        require(all(math.isfinite(v) for v in record[name]), "Nonfinite " + name)
    positions, normals, triangles = triples(record["positions"]), triples(record["normals"]), triples(record["triangles"])
    require(all(abs(sum(v * v for v in normal) - 1.0) < 1e-4 for normal in normals), "Zero/nonunit normals")
    for vi in range(count):
        weights = record["boneWeights"][vi * 4:vi * 4 + 4]
        indices = record["boneIndices"][vi * 4:vi * 4 + 4]
        require(all(isinstance(i, int) and 0 <= i < 23 for i in indices), "Invalid bone index")
        require(all(w >= 0 for w in weights) and abs(sum(weights) - 1.0) < 1e-6, "Unweighted/unnormalized vertex")
    require(all(isinstance(i, int) and 0 <= i < count for tri in triangles for i in tri), "Invalid triangle index")
    require(len(record["triangleMaterialIndices"]) == len(triangles), "Material triangle count")
    require(all(isinstance(i, int) and 0 <= i < len(record["materialNames"]) for i in record["triangleMaterialIndices"]), "Invalid material index")
    canonical, first = [], {}
    for vi, point in enumerate(positions):
        key = tuple(round(v / .000001) for v in point)
        canonical.append(first.setdefault(key, vi))
    edges, faces = defaultdict(list), defaultdict(list)
    minimum_area = math.inf
    for ti, tri in enumerate(triangles):
        tri_area = area(*(positions[i] for i in tri))
        minimum_area = min(minimum_area, tri_area)
        require(len(set(tri)) == 3 and tri_area > 1e-14, "Degenerate/zero-area triangle")
        welded = [canonical[i] for i in tri]
        faces[tuple(sorted(welded))].append(ti)
        for u, v in zip(welded, welded[1:] + welded[:1]):
            edges[tuple(sorted((u, v)))].append((ti, u, v))
    return {"vertices": count, "triangles": len(triangles), "minimum_triangle_area_m2": minimum_area,
            "zero_area_triangles": 0, "unweighted_vertices": 0, "nonunit_normals": 0,
            "coincident_topology_diagnostic_only": {
                "boundary_edges": sum(len(uses) == 1 for uses in edges.values()),
                "nonmanifold_more_than_two_faces": sum(len(uses) > 2 for uses in edges.values()),
                "same_direction_two_face_edges": sum(len(uses) == 2 and uses[0][1:] == uses[1][1:] for uses in edges.values()),
                "duplicate_position_triangles": sum(max(0, len(uses) - 1) for uses in faces.values())},
            "bounds_min_world_m": [min(v[axis] for v in positions) for axis in range(3)],
            "bounds_max_world_m": [max(v[axis] for v in positions) for axis in range(3)]}


def selfcheck():
    matrix = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]
    record = {"format": "FOC_MESHY_REST_LOD_MESH_V1", "coordinates": "BLENDER_WORLD_METERS_Z_UP",
              "bones": [{"name": "B" + str(i), "parent": "", "restWorldMatrix": matrix,
                         "headWorld": [0, 0, 0], "tailWorld": [0, 0, 1]} for i in range(23)],
              "positions": [0, 0, 0, 1, 0, 0, 0, 1, 0], "normals": [0, 0, 1] * 3,
              "uv": [0, 0, 1, 0, 0, 1], "triangles": [0, 1, 2], "boneIndices": [0, 0, 0, 0] * 3,
              "boneWeights": [1, 0, 0, 0] * 3, "triangleMaterialIndices": [0], "materialNames": ["fixture"]}
    validate(record)
    corruptions = [("positions", 0, float("nan")), ("triangles", 0, 3), ("normals", 2, 0),
                   ("boneWeights", 0, 0), ("boneIndices", 0, 23), ("triangleMaterialIndices", 0, 1)]
    for key, index, value in corruptions:
        broken = copy.deepcopy(record)
        broken[key][index] = value
        try:
            validate(broken)
        except ValueError:
            pass
        else:
            raise AssertionError("Validation accepted corrupt " + key)
    print("FOC_MESHY_LOD_ARTIFACT_SELFCHECK_PASS valid=1 rejected_corruptions=6")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--directory", type=Path, default=Path("Artifacts/MeshyPilotOptimized"))
    parser.add_argument("--selfcheck", action="store_true")
    args = parser.parse_args()
    if args.selfcheck:
        selfcheck()
    directory = args.directory.resolve()
    manifest = json.loads((directory / "dcc-generation-manifest.json").read_text(encoding="utf-8"))
    strip = json.loads((directory / "fbx-media-strip-audit.json").read_text(encoding="utf-8"))
    require(hash_file(Path(strip["source"])) == strip["source_sha256"], "Original source changed")
    require(hash_file(Path(strip["output"])) == strip["output_sha256"], "Runtime LOD0 changed")
    require(manifest["independent_blender_before"] == manifest["independent_blender_after"], "Independent Blender comparison not equal")
    results, previous, baseline = [], manifest["source_geometry_audit"]["triangles"], None
    for output in manifest["outputs"]:
        path = directory / Path(output["path"]).name
        require(hash_file(path) == output["sha256"], "LOD artifact hash changed")
        record = json.loads(path.read_text(encoding="utf-8"))
        measured = validate(record)
        shared = {key: record[key] for key in ("bones", "sourceSha256", "runtimeSourceSha256", "sourceBoundsMin", "sourceBoundsMax", "sourceCheckpoints")}
        if baseline is None:
            baseline = shared
        require(shared == baseline, "LOD shared source metadata changed")
        require(measured["triangles"] == output["triangles"] < previous, "LOD triangle count not decreasing")
        require(abs(measured["triangles"] / 9586 - record["ratioRequested"]) < .002, "LOD ratio differs from declared target")
        previous = measured["triangles"]
        results.append(dict(measured, lod=record["lod"], sha256=hash_file(path)))
    require(len(results) == 2, "Expected both derived LODs")
    report = {"status": "ARTIFACT_STRUCTURE_PASS_NOT_UNITY_OR_VISUAL_ACCEPTANCE", "outputs": results,
              "source_unchanged": True, "runtime_fbx_hash_verified": True, "shared_original23_bone_metadata_equal": True,
              "limitations": "Nonmanifold/coincident edge diagnostics inherited or changed by decimation are not certified manifold. Runtime binding, silhouettes and deformation require Unity validation."}
    (directory / "lod-artifact-validation.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print("FOC_MESHY_LOD_ARTIFACT_PASS " + json.dumps({"lods": len(results), "triangles": [r["triangles"] for r in results], "source_unchanged": True}))


if __name__ == "__main__":
    main()
