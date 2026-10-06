"""Synthetic regressions for read-only FBX continuity preflight, not art QA."""
import copy
import struct
import tempfile
import unittest
import zipfile
import zlib
from pathlib import Path

import audit_meshy_animation_intake as audit
from strip_meshy_fbx_media import Document, FOOT_MAGIC, Node, serialize


def prop(value):
    if isinstance(value, str):
        data = value.encode()
        return b"S" + struct.pack("<I", len(data)) + data
    if isinstance(value, int):
        return b"L" + struct.pack("<q", value)
    if isinstance(value, float):
        return b"D" + struct.pack("<d", value)
    if isinstance(value, list):
        kind, fmt = (b"i", "i") if all(isinstance(v, int) for v in value) else (b"d", "d")
        raw = struct.pack("<" + fmt * len(value), *value)
        return kind + struct.pack("<III", len(value), 0, len(raw)) + raw
    raise TypeError(value)


def node(name, *values, children=()):
    return Node(name.encode(), len(values), b"".join(prop(v) for v in values), list(children), bool(children))


def fixture(offset=0):
    ident = lambda i: i + offset
    objects = [
        node("Model", ident(1), "target_character", "Null", children=[node("Properties70", children=[node("P", "Lcl Rotation", "Vector3D", "Vector", "", 0., 0., 0.)])]),
        node("Model", ident(2), "Hips", "LimbNode"),
        node("Geometry", ident(3), "char1", "Mesh", children=[node("Vertices", [0., 0., 0., 1., 0., 0., 0., 1., 0.]), node("PolygonVertexIndex", [0, 1, -3]), node("UV", [0., 0., 1., 0., 0., 1.])]),
        node("Deformer", ident(4), "Skin", "Skin"),
        node("Deformer", ident(5), "HipsCluster", "Cluster", children=[node("Indexes", [0, 1, 2]), node("Weights", [1., 1., 1.])]),
        node("Pose", ident(6), "BindPose", "BindPose", children=[node("PoseNode", children=[node("Node", ident(2)), node("Matrix", [1., 0., 0., 0., 0., 1., 0., 0., 0., 0., 1., 0., 0., 0., 0., 1.])])]),
        node("AnimationStack", ident(7), "Walking", "", children=[node("Properties70", children=[node("P", "LocalStart", "KTime", "Time", "", 0), node("P", "LocalStop", "KTime", "Time", "", audit.TICKS_PER_SECOND)])]),
        node("AnimationCurve", ident(8), "", "", children=[node("KeyValueFloat", [0., 1.])]),
        # Animation curves routinely share names; protected identities must not.
        node("AnimationCurve", ident(9), "", "", children=[node("KeyValueFloat", [0., 1.])]),
    ]
    links = [(1, 0), (2, 1), (3, 1), (4, 3), (5, 4), (2, 5), (8, 7), (9, 7)]
    connections = [node("C", "OO", ident(a), ident(b) if b else 0) for a, b in links]
    settings = [node("P", name, "int", "Integer", "", value) for name, value in [("UpAxis", 1), ("FrontAxis", 2), ("CoordAxis", 0), ("UnitScaleFactor", 1.)]]
    footer = b"opaque-footer-id" + b"\0" * 5 + struct.pack("<I", 7400) + b"\0" * 120 + FOOT_MAGIC
    return Document(7400, [node("GlobalSettings", children=[node("Properties70", children=settings)]), node("Objects", children=objects), node("Connections", children=connections)], footer, 0)


def record(doc):
    return audit.inventory(serialize(doc))


def objects(doc):
    return next(n for n in doc.nodes if n.name == b"Objects").children


class AnimationIntakeTests(unittest.TestCase):
    def setUp(self):
        self.doc = fixture()
        self.baseline = record(self.doc)

    def assert_match(self, candidate):
        result = audit.compare(self.baseline, record(candidate))
        self.assertTrue(result["same_protected_character_data"])
        self.assertEqual("CONTINUITY_MATCH_NOT_ACCEPTANCE", result["status"])
        self.assertEqual("NOT_RUN", result["unity_avatar"])
        self.assertFalse(result["runtime_activated"])

    def assert_review(self, candidate):
        result = audit.compare(self.baseline, record(candidate))
        self.assertFalse(result["same_protected_character_data"])
        self.assertEqual("CONTINUITY_REVIEW_REQUIRED", result["status"])

    def test_identical_character_is_only_continuity_not_acceptance(self):
        self.assert_match(self.doc)

    def test_exporter_object_id_renumbering(self):
        self.assert_match(fixture(100))

    def test_object_order_is_not_character_change(self):
        objects(self.doc).reverse()
        self.assert_match(self.doc)

    def test_connection_order_is_not_character_change(self):
        self.doc.nodes[-1].children.reverse()
        self.assert_match(self.doc)

    def test_array_compression_is_not_character_change(self):
        vertices = objects(self.doc)[2].children[0]
        count, _, length = struct.unpack_from("<III", vertices.properties, 1)
        payload = zlib.compress(vertices.properties[13:13 + length])
        vertices.properties = b"d" + struct.pack("<III", count, 1, len(payload)) + payload
        self.assert_match(self.doc)

    def test_motion_key_changes_do_not_replace_character(self):
        objects(self.doc)[7].children[0] = node("KeyValueFloat", [2., 3.])
        self.assert_match(self.doc)

    def test_new_idle_is_inventory_not_quality_acceptance(self):
        idle = copy.deepcopy(objects(self.doc)[6])
        idle.properties = node("AnimationStack", 10, "Idle", "").properties
        objects(self.doc).append(idle)
        result = audit.compare(self.baseline, record(self.doc))
        self.assertEqual(["Idle"], result["additional_clip_names"])
        self.assertEqual("NOT_DECIDED", result["animation_quality"])
        self.assert_match(self.doc)

    def test_geometry_change_requires_review(self):
        objects(self.doc)[2].children[0] = node("Vertices", [0., 0., 0., 2., 0., 0., 0., 1., 0.])
        self.assert_review(self.doc)

    def test_uv_change_requires_review(self):
        objects(self.doc)[2].children[2] = node("UV", [0., 0., .5, 0., 0., 1.])
        self.assert_review(self.doc)

    def test_weights_change_requires_review(self):
        objects(self.doc)[4].children[1] = node("Weights", [.5, 1., 1.])
        self.assert_review(self.doc)

    def test_rest_rotation_change_requires_review(self):
        objects(self.doc)[0].children[0].children[0] = node("P", "Lcl Rotation", "Vector3D", "Vector", "", 0., 180., 0.)
        self.assert_review(self.doc)

    def test_bind_pose_change_requires_review(self):
        objects(self.doc)[5].children[0].children[1] = node("Matrix", [2.] * 16)
        self.assert_review(self.doc)

    def test_bone_parent_change_requires_review(self):
        self.doc.nodes[-1].children[1] = node("C", "OO", 2, 0)
        self.assert_review(self.doc)

    def test_axis_or_scale_change_requires_review(self):
        self.doc.nodes[0].children[0].children[-1] = node("P", "UnitScaleFactor", "double", "Number", "", 100.)
        self.assert_review(self.doc)

    def test_duplicate_protected_identity_fails_closed(self):
        objects(self.doc).append(node("Model", 20, "Hips", "LimbNode"))
        with self.assertRaises(ValueError):
            record(self.doc)

    def test_invalid_polygon_vertex_rejected(self):
        objects(self.doc)[2].children[1] = node("PolygonVertexIndex", [0, 1, -5])
        with self.assertRaises(ValueError):
            record(self.doc)

    def test_unterminated_polygon_rejected(self):
        objects(self.doc)[2].children[1] = node("PolygonVertexIndex", [0, 1, 2])
        with self.assertRaises(ValueError):
            record(self.doc)

    def test_nonfinite_geometry_rejected(self):
        objects(self.doc)[2].children[0] = node("Vertices", [float("nan")] * 9)
        with self.assertRaises(ValueError):
            record(self.doc)

    def test_truncated_property_rejected(self):
        with self.assertRaises(ValueError):
            audit.properties(Node(b"Fixture", 1, b"L\0"))

    def test_unsupported_compression_rejected(self):
        with self.assertRaises(ValueError):
            audit.properties(Node(b"Fixture", 1, b"d" + struct.pack("<III", 1, 2, 0)))

    def test_compressed_array_bomb_rejected(self):
        payload = zlib.compress(b"x" * 1000)
        with self.assertRaises(ValueError):
            audit.properties(Node(b"Fixture", 1, b"d" + struct.pack("<III", 1, 1, len(payload)) + payload))

    def test_zero_length_motion_rejected(self):
        objects(self.doc)[6].children[0].children[-1] = node("P", "LocalStop", "KTime", "Time", "", 0)
        with self.assertRaises(ValueError):
            record(self.doc)

    def test_duration_and_triangle_inventory(self):
        self.assertEqual(1., self.baseline["clips"][0]["duration_seconds"])
        self.assertEqual(1, self.baseline["meshes"][0]["fan_triangle_count"])

    def test_missing_start_uses_declared_file_template(self):
        template = node("PropertyTemplate", "FbxAnimStack", children=[node("Properties70", children=[node("P", "LocalStart", "KTime", "Time", "", 0), node("P", "LocalStop", "KTime", "Time", "", 0)])])
        self.doc.nodes.append(node("Definitions", children=[node("ObjectType", "AnimationStack", children=[template])]))
        objects(self.doc)[6].children[0].children.pop(0)
        self.assertEqual(1., record(self.doc)["clips"][0]["duration_seconds"])
        self.assert_match(self.doc)

    def test_missing_start_without_template_is_not_guessed(self):
        objects(self.doc)[6].children[0].children.pop(0)
        with self.assertRaises(ValueError):
            record(self.doc)

    def test_changed_inherited_rest_default_requires_review(self):
        template = node("PropertyTemplate", "FbxNode", children=[node("Properties70", children=[node("P", "Lcl Rotation", "Vector3D", "Vector", "", 0., 180., 0.)])])
        self.doc.nodes.append(node("Definitions", children=[node("ObjectType", "Model", children=[template])]))
        self.assert_review(self.doc)

    def test_animation_template_is_not_character_template(self):
        template = node("PropertyTemplate", "FbxAnimStack", children=[node("Properties70", children=[node("P", "LocalStart", "KTime", "Time", "", 0), node("P", "LocalStop", "KTime", "Time", "", 0)])])
        self.doc.nodes.append(node("Definitions", children=[node("ObjectType", "AnimationStack", children=[template])]))
        self.assert_match(self.doc)

    def test_unresolved_bind_reference_rejected(self):
        objects(self.doc)[5].children[0].children[0] = node("Node", 999)
        with self.assertRaises(ValueError):
            record(self.doc)

    def test_zip_multiple_models_require_explicit_member(self):
        with tempfile.TemporaryDirectory(prefix="foc-motion-test-") as directory:
            path = Path(directory) / "motions.zip"
            with zipfile.ZipFile(path, "w") as archive:
                archive.writestr("a.fbx", serialize(self.doc))
                archive.writestr("b.fbx", serialize(self.doc))
            with self.assertRaises(ValueError):
                audit.read_input(path)

    def test_zip_member_read_never_extracts_even_traversal_name(self):
        with tempfile.TemporaryDirectory(prefix="foc-motion-test-") as directory:
            path = Path(directory) / "motions.zip"
            original = serialize(self.doc)
            with zipfile.ZipFile(path, "w") as archive:
                archive.writestr("../../outside.fbx", original)
            before = path.read_bytes()
            data, metadata = audit.read_input(path, "../../outside.fbx")
            self.assertEqual(original, data)
            self.assertEqual(before, path.read_bytes())
            self.assertEqual([path], list(Path(directory).iterdir()))
            self.assertIn("zip_sha256", metadata)


if __name__ == "__main__":
    unittest.main(verbosity=2)
