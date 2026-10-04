"""Read-only FBX axis and pose diagnostic; only writes an ignored JSON report.

Blender --background --factory-startup --disable-autoexec --python-exit-code 1
--python Tools/Art/audit_quaternius_motion.py -- --source path.fbx --output report.json
No source geometry, weights, actions, import settings or files are changed.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


def values(v):
    return [float(x) for x in v]


def matrix(m):
    return [values(row) for row in m]


def norm(v):
    return values(v.normalized()) if v.length > 1e-9 else None


def transform(obj):
    return {"name": obj.name, "type": obj.type,
            "parent": obj.parent.name if obj.parent else None,
            "location": values(obj.location), "rotationMode": obj.rotation_mode,
            "rotationEulerDegrees": [math.degrees(x) for x in obj.rotation_euler],
            "localMatrix": matrix(obj.matrix_local), "worldMatrix": matrix(obj.matrix_world),
            "worldQuaternionWxyz": values(obj.matrix_world.to_quaternion()),
            "scale": values(obj.scale)}


def curves(action):
    result = []
    for layer in action.layers:
        for strip in layer.strips:
            if hasattr(strip, "channelbag"):
                for slot in action.slots:
                    bag = strip.channelbag(slot)
                    if bag:
                        result.extend(bag.fcurves)
    return result


def fbx_metadata(path):
    from io_scene_fbx import parse_fbx
    tree, version = parse_fbx.parse(str(path))

    def decode(x):
        return x.decode("utf-8", errors="replace") if isinstance(x, bytes) else x

    setting = next((e for e in tree.elems if e.id == b"GlobalSettings"), None)
    props = next((e for e in setting.elems if e.id == b"Properties70"), None) if setting else None
    return {"version": version, "globalSettings": {decode(p.props[0]): [decode(x) for x in p.props[4:]]
            for p in props.elems if p.id == b"P"} if props else {}}


def capture(arm):
    bpy.context.view_layer.update()
    deps = bpy.context.evaluated_depsgraph_get()
    posed = arm.evaluated_get(deps)
    bones = {}
    for bone in posed.pose.bones:
        world = posed.matrix_world @ bone.matrix
        bones[bone.name] = {"headWorld": values(posed.matrix_world @ bone.head),
                            "tailWorld": values(posed.matrix_world @ bone.tail),
                            "worldQuaternionWxyz": values(world.to_quaternion()),
                            "worldXAxis": norm(world.to_3x3() @ Vector((1, 0, 0))),
                            "worldYAxis": norm(world.to_3x3() @ Vector((0, 1, 0))),
                            "worldZAxis": norm(world.to_3x3() @ Vector((0, 0, 1)))}

    def point(name):
        return Vector(bones[name]["headWorld"])

    direction = {}
    if "Head" in bones and "pelvis" in bones:
        direction["pelvisToHeadUp"] = norm(point("Head") - point("pelvis"))
    for side in ("l", "r"):
        if "ball_" + side in bones and "foot_" + side in bones:
            direction[side + "FootToBallForward"] = norm(point("ball_" + side) - point("foot_" + side))
    if "hand_l" in bones and "hand_r" in bones:
        direction["rightHandToLeftHand"] = norm(point("hand_l") - point("hand_r"))
    if "thigh_l" in bones and "thigh_r" in bones:
        direction["rightThighToLeftThigh"] = norm(point("thigh_l") - point("thigh_r"))
    bound = []
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        evaluated = obj.evaluated_get(deps)
        mesh = evaluated.to_mesh()
        try:
            points = [evaluated.matrix_world @ v.co for v in mesh.vertices]
            if points:
                bound.append({"name": obj.name, "min": [min(p[i] for p in points) for i in range(3)],
                              "max": [max(p[i] for p in points) for i in range(3)]})
        finally:
            evaluated.to_mesh_clear()
    return {"armature": transform(posed), "frame": float(bpy.context.scene.frame_current_final),
            "directions": direction, "bones": bones, "meshWorldBounds": bound}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    source, output = Path(args.source).resolve(), Path(args.output).resolve()
    if source == output or output.suffix != ".json":
        raise ValueError("A distinct JSON diagnostic path is required")
    digest = hashlib.sha256(source.read_bytes()).hexdigest()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(source), use_anim=True, use_image_search=False,
                             automatic_bone_orientation=False, ignore_leaf_bones=False)
    arms = [o for o in bpy.context.scene.objects if o.type == "ARMATURE"]
    if len(arms) != 1:
        raise ValueError("Expected exactly one donor armature")
    arm = arms[0]
    report = {"status": "SOURCE_DIAGNOSTIC_NOT_UNITY_ACCEPTANCE", "source": str(source),
              "sourceSha256": digest, "blender": bpy.app.version_string,
              "coordinateSystem": "Blender world: right-handed Z-up; no Unity-space conversion applied",
              "fbx": fbx_metadata(source),
              "importedObjects": [transform(o) for o in bpy.context.scene.objects],
              "restBones": [{"name": b.name, "parent": b.parent.name if b.parent else None,
                              "headLocal": values(b.head_local), "tailLocal": values(b.tail_local),
                              "matrixLocal": matrix(b.matrix_local)} for b in arm.data.bones],
              "actions": [], "samples": []}
    initial = {o.name: o.matrix_basis.copy() for o in bpy.context.scene.objects}
    arm.animation_data_create()
    arm.animation_data.action = None
    arm.data.pose_position = "REST"
    report["rest"] = capture(arm)
    arm.data.pose_position = "POSE"
    wanted = ("A_TPose", "Sword_Attack", "Idle_Loop", "Crouch_Idle_Loop", "Idle_Torch_Loop")
    for action in sorted(bpy.data.actions, key=lambda x: x.name):
        action_curves = curves(action)
        report["actions"].append({"name": action.name, "frames": values(action.frame_range),
                                  "slots": [{"identifier": s.identifier, "targetIdType": s.target_id_type}
                                            for s in action.slots],
                                  "curveCount": len(action_curves),
                                  "objectCurves": [{"path": c.data_path, "axis": c.array_index,
                                                    "keyRange": [min(k.co.y for k in c.keyframe_points),
                                                                 max(k.co.y for k in c.keyframe_points)]}
                                                   for c in action_curves if not c.data_path.startswith('pose.bones[')]})
        if not any(action.name == name or action.name.endswith("|" + name) for name in wanted):
            continue
        for o in bpy.context.scene.objects:
            o.matrix_basis = initial[o.name].copy()
        for b in arm.pose.bones:
            b.matrix_basis.identity()
        arm.animation_data.action = action
        slots = [s for s in action.slots if s.target_id_type == "OBJECT"]
        if len(slots) != 1:
            raise ValueError("Ambiguous armature action slots: " + action.name)
        arm.animation_data.action_slot = slots[0]
        start, end = action.frame_range
        for phase in (0.0, 0.2, 0.35, 0.5, 0.75, 1.0):
            frame = float(start + phase * (end - start))
            bpy.context.scene.frame_set(math.floor(frame), subframe=frame % 1.0)
            sample = capture(arm)
            sample.update({"action": action.name, "phase": phase,
                           "assignedSlot": arm.animation_data.action_slot.identifier})
            report["samples"].append(sample)
            print("POSE", action.name, phase, json.dumps(sample["directions"]))
    report["sourceSha256After"] = hashlib.sha256(source.read_bytes()).hexdigest()
    if digest != report["sourceSha256After"]:
        raise AssertionError("Read-only audit source digest changed")
    if len(report["samples"]) != len(wanted) * 6:
        raise AssertionError("Not all five required source clips were sampled")
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2, allow_nan=False), encoding="utf-8")
    print("REPORT", str(output))
    print("FBX_METADATA", json.dumps(report["fbx"]))


if __name__ == "__main__":
    main()
