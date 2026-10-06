"""Read-only, conservative preflight for additional animations on the SAME Hasan.

No extraction, import, re-export, Avatar change or runtime activation occurs.
Exact decoded geometry/rig/weights/bind-pose equality is a continuity check,
NOT animation, Humanoid, licensing or visual acceptance. Re-export differences
are flagged for manual review rather than silently replacing the character.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import struct
import uuid
import zipfile
import zlib
from pathlib import Path

from strip_meshy_fbx_media import Node, parse

PROTECTED = {b"Geometry", b"Model", b"Deformer", b"Pose"}
AXES = {"UpAxis", "UpAxisSign", "FrontAxis", "FrontAxisSign", "CoordAxis",
        "CoordAxisSign", "OriginalUpAxis", "OriginalUpAxisSign",
        "UnitScaleFactor", "OriginalUnitScaleFactor"}
TICKS_PER_SECOND = 46186158000
MAX_FILE_BYTES = 256 * 1024 * 1024
MAX_ARRAY_BYTES = 128 * 1024 * 1024
REPO = Path(__file__).resolve().parents[2]
DEFAULT_BASELINE = REPO / "UnityProject/Assets/FOC/ArtSource/HistoricalSlice/MeshyPilot/Source/Hasan_Meshy.fbx"


def digest(value):
    encoded = json.dumps(value, sort_keys=True, separators=(",", ":"), allow_nan=False).encode()
    return hashlib.sha256(encoded).hexdigest()


def properties(node):
    """Decode values, ignoring only array compression representation."""
    data, offset, result = node.properties, 0, []

    def take(count):
        nonlocal offset
        if count < 0 or offset + count > len(data):
            raise ValueError("Truncated FBX property")
        value = data[offset:offset + count]
        offset += count
        return value

    scalars = {b"Y": "h", b"C": "?", b"I": "i", b"F": "f", b"D": "d", b"L": "q"}
    arrays = {b"f": "f", b"d": "d", b"l": "q", b"i": "i", b"b": "?", b"c": "b"}
    for _ in range(node.count):
        kind = take(1)
        if kind in scalars:
            fmt = "<" + scalars[kind]
            value = struct.unpack(fmt, take(struct.calcsize(fmt)))[0]
            if isinstance(value, float) and not math.isfinite(value):
                raise ValueError("Nonfinite FBX scalar")
        elif kind in (b"S", b"R"):
            length = struct.unpack("<I", take(4))[0]
            raw = take(length)
            value = raw.decode("utf-8", errors="strict") if kind == b"S" else {"bytes": length, "sha256": hashlib.sha256(raw).hexdigest()}
        elif kind in arrays:
            count, encoding, length = struct.unpack("<III", take(12))
            size = struct.calcsize("<" + arrays[kind]) * count
            if size > MAX_ARRAY_BYTES or encoding not in (0, 1):
                raise ValueError("Unsupported or oversized FBX array")
            raw = take(length)
            if encoding == 1:
                decoder = zlib.decompressobj()
                raw = decoder.decompress(raw, size + 1)
                if not decoder.eof or decoder.unconsumed_tail or decoder.unused_data:
                    raise ValueError("Malformed or oversized compressed FBX array")
            if len(raw) != size:
                raise ValueError("FBX array length mismatch")
            value = list(struct.unpack("<" + arrays[kind] * count, raw))
            if any(isinstance(v, float) and not math.isfinite(v) for v in value):
                raise ValueError("Nonfinite FBX array")
        else:
            raise ValueError("Unsupported FBX property type: " + repr(kind))
        result.append(value)
    if offset != len(data):
        raise ValueError("Trailing undeclared FBX property bytes")
    return result


def child(node, name):
    matches = [c for c in node.children if c.name == name]
    if len(matches) != 1:
        raise ValueError("Expected one " + name.decode() + " node")
    return matches[0]


def inventory(data):
    doc = parse(data)
    root = Node(b"Root", 0, b"", doc.nodes)
    objects = child(root, b"Objects").children
    # Blender omits stack properties equal to the in-file FbxAnimStack template.
    # Read those defaults; never guess a missing duration from an action label.
    stack_defaults = {}
    for definitions in (n for n in root.children if n.name == b"Definitions"):
        for object_type in definitions.children:
            if object_type.name != b"ObjectType" or properties(object_type) != ["AnimationStack"]:
                continue
            for template in object_type.children:
                if template.name != b"PropertyTemplate" or properties(template) != ["FbxAnimStack"]:
                    continue
                for p in child(template, b"Properties70").children:
                    entry = properties(p)
                    if p.name == b"P" and entry and entry[0] in ("LocalStart", "LocalStop"):
                        if entry[0] in stack_defaults:
                            raise ValueError("Ambiguous animation stack defaults")
                        stack_defaults[entry[0]] = entry[-1]
    identified, labels = {}, set()
    for obj in objects:
        values = properties(obj)
        if len(values) < 3 or not isinstance(values[0], int):
            raise ValueError("Malformed FBX object identity")
        label = json.dumps([obj.name.decode(), values[1], values[2]], ensure_ascii=True)
        if values[0] in identified or (obj.name in PROTECTED and label in labels):
            raise ValueError("Ambiguous duplicate object identity; explicit DCC review required")
        identified[values[0]] = (label, obj)
        if obj.name in PROTECTED:
            labels.add(label)

    def reference(object_id):
        if object_id == 0:
            return "FBX_SCENE_ROOT"
        if object_id not in identified:
            raise ValueError("Unresolved protected FBX object reference")
        return identified[object_id][0]

    def tree(node, object_root=False):
        values = properties(node)
        if object_root:
            values[0] = reference(values[0])
        elif node.name == b"Node":  # PoseNode/Node refers to an object ID.
            if len(values) != 1:
                raise ValueError("Malformed bind-pose object reference")
            values[0] = reference(values[0])
        return [node.name.decode(), values, [tree(c) for c in node.children]]

    protected = {label: digest(tree(obj, True)) for label, obj in identified.values() if obj.name in PROTECTED}
    # Rest transforms can inherit omitted values from FBX property templates.
    # Comparing only explicit object properties would miss changed defaults.
    protected_templates = {}
    for definitions in (n for n in root.children if n.name == b"Definitions"):
        for object_type in definitions.children:
            category = properties(object_type) if object_type.name == b"ObjectType" else []
            if len(category) != 1 or category[0].encode() not in PROTECTED:
                continue
            for template in object_type.children:
                if template.name != b"PropertyTemplate":
                    continue
                key = json.dumps([category[0], properties(template)])
                if key in protected_templates:
                    raise ValueError("Ambiguous protected property template")
                protected_templates[key] = digest(tree(template))
    if not all(any(obj.name == category for _, obj in identified.values()) for category in PROTECTED):
        raise ValueError("Expected rigged complete-character Geometry/Model/Deformer/Pose")
    connections = []
    for connection in child(root, b"Connections").children:
        values = properties(connection)
        if connection.name != b"C" or len(values) < 3 or values[0] not in ("OO", "OP"):
            raise ValueError("Unsupported FBX connection")
        source, target = values[1:3]
        if source not in identified or (target != 0 and target not in identified):
            raise ValueError("Unresolved FBX connection")
        if identified[source][1].name in PROTECTED and (target == 0 or identified[target][1].name in PROTECTED):
            connections.append([values[0], reference(source), reference(target)] + values[3:])
    connections.sort(key=lambda value: json.dumps(value, sort_keys=True))
    axes = {}
    for prop in child(child(root, b"GlobalSettings"), b"Properties70").children:
        values = properties(prop)
        if prop.name == b"P" and values and values[0] in AXES:
            axes[values[0]] = values[4:]
    if not {"UpAxis", "FrontAxis", "CoordAxis", "UnitScaleFactor"}.issubset(axes):
        raise ValueError("Missing FBX coordinate/scale settings")

    clips, meshes, bones = [], [], []
    for label, obj in identified.values():
        values = properties(obj)
        if obj.name == b"AnimationStack":
            times = dict(stack_defaults)
            for prop in child(obj, b"Properties70").children:
                entry = properties(prop)
                if prop.name == b"P" and entry and entry[0] in ("LocalStart", "LocalStop"):
                    times[entry[0]] = entry[-1]
            if set(times) != {"LocalStart", "LocalStop"} or times["LocalStop"] <= times["LocalStart"]:
                raise ValueError("Animation stack has no positive declared duration")
            clips.append({"name": values[1].split("\x00")[0],
                          "duration_seconds": (times["LocalStop"] - times["LocalStart"]) / TICKS_PER_SECOND})
        elif obj.name == b"Geometry" and values[2] == "Mesh":
            vertices = properties(child(obj, b"Vertices"))[0]
            indices = properties(child(obj, b"PolygonVertexIndex"))[0]
            if len(vertices) == 0 or len(vertices) % 3:
                raise ValueError("Malformed geometry position array")
            size, triangles, polygons = 0, 0, 0
            for index in indices:
                vertex = index if index >= 0 else -index - 1
                if vertex >= len(vertices) // 3:
                    raise ValueError("Polygon references missing vertex")
                size += 1
                if index < 0:
                    if size < 3:
                        raise ValueError("Degenerate polygon index sequence")
                    triangles += size - 2
                    polygons += 1
                    size = 0
            if size or not polygons:
                raise ValueError("Unterminated or empty polygon index sequence")
            meshes.append({"name": values[1].split("\x00")[0], "control_vertices": len(vertices) // 3,
                           "polygons": polygons, "fan_triangle_count": triangles})
        elif obj.name == b"Model" and values[2] == "LimbNode":
            bones.append(values[1].split("\x00")[0])
    if not clips or not meshes or not bones:
        raise ValueError("Expected mesh, bone models and at least one animation stack")
    return {"fbx_version": doc.version, "sha256": hashlib.sha256(data).hexdigest(), "bytes": len(data),
            "protected_objects": protected, "protected_connections_sha256": digest(connections),
            "protected_property_templates": protected_templates,
            "coordinate_settings": axes, "meshes": meshes, "bone_names": sorted(bones),
            "clips": sorted(clips, key=lambda c: c["name"])}


def read_input(path, member=None):
    path = path.resolve(strict=True)
    source_hash = hash_file(path)
    if path.suffix.lower() == ".zip":
        with zipfile.ZipFile(path) as archive:
            candidates = [i for i in archive.infolist() if not i.is_dir() and i.filename.lower().endswith(".fbx")]
            selected = [i for i in candidates if i.filename == member] if member else candidates
            if len(selected) != 1:
                raise ValueError("Select exactly one FBX with --fbx-member when ZIP contains multiple exports")
            info = selected[0]
            if info.file_size > MAX_FILE_BYTES:
                raise ValueError("FBX member exceeds local preflight size limit")
            with archive.open(info) as stream:
                data = stream.read(MAX_FILE_BYTES + 1)
            if len(data) != info.file_size:
                raise ValueError("ZIP FBX size mismatch")
        metadata = {"path": str(path), "member": info.filename, "zip_sha256": source_hash}
    else:
        if member or path.suffix.lower() != ".fbx" or path.stat().st_size > MAX_FILE_BYTES:
            raise ValueError("Expected bounded FBX or ZIP input")
        data, metadata = path.read_bytes(), {"path": str(path), "member": None}
    if hash_file(path) != source_hash:
        raise ValueError("Input changed while reading")
    metadata["file_sha256"] = source_hash
    return data, metadata


def hash_file(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def compare(baseline, candidate):
    names = set(baseline["protected_objects"]) | set(candidate["protected_objects"])
    changed = sorted(n for n in names if baseline["protected_objects"].get(n) != candidate["protected_objects"].get(n))
    same_connections = baseline["protected_connections_sha256"] == candidate["protected_connections_sha256"]
    same_axes = baseline["coordinate_settings"] == candidate["coordinate_settings"]
    same_defaults = baseline["protected_property_templates"] == candidate["protected_property_templates"]
    same = not changed and same_connections and same_axes and same_defaults
    original_clips = {c["name"] for c in baseline["clips"]}
    return {"status": "CONTINUITY_MATCH_NOT_ACCEPTANCE" if same else "CONTINUITY_REVIEW_REQUIRED",
            "same_protected_character_data": same, "changed_protected_objects": changed,
            "protected_connections_equal": same_connections, "coordinate_settings_equal": same_axes,
            "protected_property_templates_equal": same_defaults,
            "additional_clip_names": [c["name"] for c in candidate["clips"] if c["name"] not in original_clips],
            "unity_avatar": "NOT_RUN", "runtime_visual_qa": "NOT_RUN", "animation_quality": "NOT_DECIDED",
            "license": "NEW_MOTION_PROVENANCE_REVIEW_REQUIRED_BEFORE_PUBLIC_DISTRIBUTION",
            "runtime_activated": False, "save_schema": "v14_UNCHANGED"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--candidate", type=Path, required=True)
    parser.add_argument("--baseline", type=Path, default=DEFAULT_BASELINE)
    parser.add_argument("--fbx-member")
    args = parser.parse_args()
    baseline_data, baseline_source = read_input(args.baseline)
    candidate_data, candidate_source = read_input(args.candidate, args.fbx_member)
    source_hashes = {args.baseline.resolve(): baseline_source["file_sha256"],
                     args.candidate.resolve(): candidate_source["file_sha256"]}
    baseline, candidate = inventory(baseline_data), inventory(candidate_data)
    report = compare(baseline, candidate)
    report.update({"scope": "Read-only additional-animation preflight; no asset copying or activation",
                   "baseline_source": baseline_source, "candidate_source": candidate_source,
                   "baseline": baseline, "candidate": candidate})
    if any(hash_file(p) != expected for p, expected in source_hashes.items()):
        raise ValueError("Input changed during preflight")
    report["source_files_unchanged"] = True
    output = REPO / "TestResults/MeshyAnimationIntake" / (uuid.uuid4().hex + ".json")
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2, allow_nan=False), encoding="utf-8")
    print(json.dumps({"status": report["status"], "report": str(output), "clips": candidate["clips"],
                      "additional_clip_names": report["additional_clip_names"], "source_files_unchanged": True}))
    return 0 if report["same_protected_character_data"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
