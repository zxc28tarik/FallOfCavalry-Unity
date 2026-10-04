"""Prepare ONE LOD2-only candidate after explicit on-foot acceptance.

Default execution does not generate. --selfcheck uses synthetic data only and
needs ordinary Python, not Blender. Generation needs Blender plus both
--onfoot-accepted and --onfoot-evidence. Output must be a NEW ignored Artifacts
directory. This tool never adopts assets, refreshes calibration hashes, or
edits original LOD0/current LOD1/current LOD2/prefab/materials/animations.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

from meshy_lod2_constraints import seam_reasons, simplify

REPO = Path(__file__).resolve().parents[2]
UNITY = REPO / "UnityProject"
SOURCE = UNITY / "Assets/FOC/ArtSource/HistoricalSlice/MeshyPilot"
PRESENTATION = UNITY / "Assets/FOC/Presentation/Characters/Ottoman1648/MeshyPilot"
BASELINE = PRESENTATION / "Calibration/CalibrationProvenance.json"
PINNED_ORIGINAL_SHA = "0ee0076119289b4748fa0eeb11fa631d9a03e55b9b1208db6afe19beba410b27"
PINNED_RUNTIME_SHA = "356dd92317ec1787f48aabe4e85157d31dfec2dd029168c856c5ffd6a125fadf"
SHARED_KEYS = ("format", "sourceSha256", "runtimeSourceSha256", "coordinates", "bones",
               "sourceBoundsMin", "sourceBoundsMax", "sourceCheckpoints")
ALLOWED_ADOPTION_PATHS = (
    "Assets/FOC/ArtSource/HistoricalSlice/MeshyPilot/Source/Hasan_Meshy_LOD2.json",
    "Assets/FOC/Presentation/Characters/Ottoman1648/MeshyPilot/MESH_HasanAga_MeshyPilot_LOD2.asset",
)


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def guard_snapshot():
    manifest = read(BASELINE)
    if len(manifest["inputs"]) != 124:
        raise ValueError("Expected immutable 124-input calibration baseline")
    paths = {BASELINE}
    for item in manifest["inputs"] + manifest["outputs"]:
        path = (UNITY / item["path"]).resolve()
        if not path.is_relative_to(UNITY) or sha(path) != item["sha256"]:
            raise ValueError("Calibration input/output differs BEFORE LOD2 preparation: " + item["path"])
        paths.add(path)
    # The historical calibration dependency guard excludes source LOD JSONs.
    # Guard them explicitly, as well as existing optimization provenance.
    for name in ("Source/Hasan_Meshy_LOD1.json", "Source/Hasan_Meshy_LOD2.json",
                 "optimization-manifest.json", "media-strip-audit.json", "provenance.json"):
        path = SOURCE / name
        paths.add(path)
        if Path(str(path) + ".meta").exists():
            paths.add(Path(str(path) + ".meta"))
    return {str(p.relative_to(REPO)).replace("\\", "/"): sha(p) for p in sorted(paths)}


def selfcheck():
    # Smooth weight changes along an edge are valid; a split vertex with
    # different weights at the SAME endpoint is a discontinuity.
    a = {"uv": (0.0, 0.0), "normal": (0.0, 0.0, 1.0), "weights": {0: 1.0}}
    b = {"uv": (1.0, 0.0), "normal": (0.0, 0.0, 1.0), "weights": {1: 1.0}}
    assert not seam_reasons([(0, a, b), (0, a, b)])
    assert "uv" in seam_reasons([(0, a, b), (0, dict(a, uv=(.2, 0.0)), b)])
    assert "material" in seam_reasons([(0, a, b), (1, a, b)])
    assert "hard_normal" in seam_reasons([(0, a, b), (0, dict(a, normal=(0.0, 1.0, 0.0)), b)])
    assert "weight_discontinuity" in seam_reasons([(0, a, b), (0, dict(a, weights={1: 1.0}), b)])
    assert "boundary" in seam_reasons([(0, a, b)])
    data = {key: [] for key in ("positions", "normals", "uv", "boneIndices", "boneWeights", "triangles", "triangleMaterialIndices")}
    data["materialNames"] = ["fixture"]
    n = 12
    for y in range(n + 1):
        for x in range(n + 1):
            u, v = x / n, y / n
            data["positions"].extend((u, v, 0.0))
            data["normals"].extend((0.0, 0.0, 1.0))
            data["uv"].extend((u, v))
            data["boneIndices"].extend((0, 1, 0, 0))
            data["boneWeights"].extend((1 - u, u, 0.0, 0.0))
    for y in range(n):
        for x in range(n):
            i = y * (n + 1) + x
            data["triangles"].extend((i, i + 1, i + n + 2, i, i + n + 2, i + n + 1))
            data["triangleMaterialIndices"].extend((0, 0))
    original = json.dumps(data, sort_keys=True)
    output, stats = simplify(data, 190, lambda p, _: p[0] < .15)
    again, again_stats = simplify(data, 190, lambda p, _: p[0] < .15)
    assert original == json.dumps(data, sort_keys=True), "Input mutated"
    assert output == again and stats == again_stats, "Nondeterministic output"
    assert stats["targetReached"] and stats["collapses"] > 0
    for i in range(0, len(output["boneWeights"]), 4):
        assert abs(sum(output["boneWeights"][i:i + 4]) - 1.0) < 1e-6
    assert set(ALLOWED_ADOPTION_PATHS) == {p for p in ALLOWED_ADOPTION_PATHS if "LOD2" in p and not p.endswith(".meta")}
    print("LOD2_PREPARATION_SELFCHECK_PASS " + json.dumps({"syntheticSourceTriangles": 288,
          "candidateTriangles": stats["triangles"], "collapses": stats["collapses"],
          "seamCases": 6, "sourceImmutable": True, "deterministic": True,
          "actualAssetGeneration": "NOT_RUN"}))


def generate(args):
    if not args.onfoot_accepted or not args.onfoot_evidence:
        raise ValueError("Generation is gated: explicitly accept on-foot review and provide its evidence path first")
    evidence = args.onfoot_evidence.resolve()
    evidence_hash = sha(evidence)
    output_directory = args.output_directory.resolve()
    if not output_directory.is_relative_to(REPO / "Artifacts") or output_directory == REPO / "Artifacts" or output_directory.exists():
        raise ValueError("Output must be a new explicit subdirectory under ignored Artifacts")
    if not .35 <= args.ratio < .60:
        raise ValueError("LOD2 candidate ratio must be below the preserved LOD1 ratio .60")
    before = guard_snapshot()
    baseline_hash = sha(BASELINE)
    lod1 = read(SOURCE / "Source/Hasan_Meshy_LOD1.json")
    old_lod2 = read(SOURCE / "Source/Hasan_Meshy_LOD2.json")
    common = {key: lod1[key] for key in SHARED_KEYS}
    if common != {key: old_lod2[key] for key in SHARED_KEYS}:
        raise ValueError("Prior LOD1/LOD2 metadata mismatch")
    strip = read(SOURCE / "media-strip-audit.json")
    original, runtime = Path(strip["source"]), SOURCE / "Source/Hasan_Meshy.fbx"
    if sha(original) != PINNED_ORIGINAL_SHA or sha(runtime) != PINNED_RUNTIME_SHA:
        raise ValueError("Pinned original/runtime LOD0 changed")

    # Blender is loaded only in this explicitly gated path.
    import bpy
    import audit_and_build_meshy_lods as existing
    from verify_meshy_lod_artifacts import validate
    arm, body = existing.import_source(runtime)
    immutable_before = existing.snapshot(arm, body)
    previous_manifest = read(SOURCE / "optimization-manifest.json")
    if immutable_before != previous_manifest["independent_blender_after"]:
        raise ValueError("Source mesh/UV/weights/rig/animations no longer match original independent Blender audit")
    analysis, source_points, source_triangles, _ = existing.mesh_analysis(body)
    if len(source_triangles) != 9586:
        raise ValueError("Unexpected original LOD0 triangle count")
    original_geometry, _ = existing.export_geometry([body], common["bones"])
    by_name = {b["name"]: b for b in common["bones"]}
    hip_z = by_name["Hips"]["headWorld"][2]
    head_region_z = by_name["Neck"]["headWorld"][2] - .03
    feature_spec = {"faceHeadgearAtOrAboveZ": head_region_z,
                    "beltZRange": [hip_z - .025, hip_z + .15], "beltMaximumAbsX": .31,
                    "policy": "Every triangle touching the measured head/neck or waist band is frozen, including UV/normals/weights; not semantic clothing reconstruction."}

    def feature(point, _):
        return point[2] >= head_region_z or (hip_z - .025 <= point[2] <= hip_z + .15 and abs(point[0]) <= .31)

    geometry, simplification = simplify(original_geometry, round(9586 * args.ratio), feature)
    triangles = len(geometry["triangles"]) // 3
    if triangles >= len(lod1["triangles"]) // 3:
        raise ValueError("Protected regions prevent a genuine LOD2 below LOD1; do not relax safeguards silently")
    candidate = dict(common, lod=2, ratioRequested=args.ratio, **geometry)
    structure = validate(candidate)
    distances = existing.surface_distances(source_points, source_triangles, geometry)
    if distances["source_vertices_to_lod_surface"]["maximum_m"] > .025 or distances["lod_vertices_to_source_surface"]["maximum_m"] > .025:
        raise ValueError("Candidate exceeds declared25mm REST-surface diagnostic bound")
    if immutable_before != existing.snapshot(arm, body):
        raise AssertionError("Source in-memory mesh/UV/weights/rig/animations mutated")
    after = guard_snapshot()
    if before != after or sha(BASELINE) != baseline_hash or sha(original) != PINNED_ORIGINAL_SHA:
        raise AssertionError("Protected original/current assets changed")
    output_directory.mkdir(parents=True, exist_ok=False)
    candidate_path = output_directory / "Hasan_Meshy_LOD2.json"
    existing.write_json(candidate_path, candidate)
    report = {"status": "LOD2_CANDIDATE_ONLY_NOT_ADOPTED_OR_VISUAL_ACCEPTED", "blender": bpy.app.version_string,
              "toolSha256": sha(__file__), "constraintToolSha256": sha(Path(__file__).with_name("meshy_lod2_constraints.py")),
              "baselineCalibrationManifestSha256": baseline_hash, "baseline124InputsRefreshed": False,
              "onFootPrerequisite": {"operatorAccepted": True, "path": str(evidence), "sha256": evidence_hash,
                                    "limit": "Provided prerequisite reference; this authoring tool does not independently judge visual acceptance."},
              "originalSourceSha256": PINNED_ORIGINAL_SHA, "runtimeLod0Sha256": PINNED_RUNTIME_SHA,
              "preservedBefore": before, "preservedAfter": after, "sourceUnchanged": True,
              "oldLod2Sha256": sha(SOURCE / "Source/Hasan_Meshy_LOD2.json"),
              "candidate": {"path": str(candidate_path), "sha256": sha(candidate_path), "structure": structure},
              "features": feature_spec, "simplification": simplification, "surfaceDistances": distances,
              "productionActivated": False, "unityImport": "NOT_RUN", "deformation": "NOT_RUN", "nearFarScreenshotQA": "NOT_RUN",
              "adoptionPlan": {"allowedChangedExistingPaths": ALLOWED_ADOPTION_PATHS,
                               "metasPrefabLod0Lod1MaterialsRigAnimationsMustRemainIdentical": True,
                               "requiredOverlay": "Explicit reviewed before/after hashes for each adopted LOD2 path, preserving original124-input baseline bytes. This report does not authorize guard bypass."}}
    existing.write_json(output_directory / "LOD2-Candidate-Provenance.json", report)
    print("LOD2_CANDIDATE_AUTHORED_NOT_ACCEPTED " + json.dumps({"triangles": triangles, "targetReached": simplification["targetReached"],
          "path": str(candidate_path), "sha256": sha(candidate_path), "baselineUnchanged": True}))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--selfcheck", action="store_true")
    parser.add_argument("--generate", action="store_true")
    parser.add_argument("--onfoot-accepted", action="store_true")
    parser.add_argument("--onfoot-evidence", type=Path)
    parser.add_argument("--ratio", type=float, default=.45)
    parser.add_argument("--output-directory", type=Path, default=REPO / "Artifacts/MeshyPilotLod2Repair/Candidate01")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else None)
    if args.selfcheck:
        selfcheck()
    if args.generate:
        generate(args)
    elif not args.selfcheck:
        parser.print_help()


if __name__ == "__main__":
    main()
