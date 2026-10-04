"""Remove only FBX Video/Content payload nodes; preserve all other raw properties.

No DCC re-export is involved. Supports binary FBX 7400/7500-style node headers.
Original FBX bytes are never written. Offset headers and footer alignment are
necessarily recomputed; every retained node name/property/ordering is verified.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import io
import json
import struct
from dataclasses import dataclass, field
from pathlib import Path

MAGIC = b"Kaydara FBX Binary  \x00\x1a\x00"
FOOT_MAGIC = b"\xf8\x5a\x8c\x6a\xde\xf5\xd9\x7e\xec\xe9\x0c\xe3\x75\x8f\x29\x0b"
EXPECTED_SHA = "0ee0076119289b4748fa0eeb11fa631d9a03e55b9b1208db6afe19beba410b27"


@dataclass
class Node:
    name: bytes
    count: int
    properties: bytes
    children: list["Node"] = field(default_factory=list)
    sentinel: bool = False


@dataclass
class Document:
    version: int
    nodes: list[Node]
    footer: bytes
    footer_start: int


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def parse(data: bytes) -> Document:
    if data[:23] != MAGIC or len(data) < 40:
        raise ValueError("Expected binary FBX header")
    version = struct.unpack_from("<I", data, 23)[0]
    fmt = "<QQQB" if version >= 7500 else "<IIIB"
    size = struct.calcsize(fmt)

    def read_node(offset: int, parent_end: int):
        if offset + size > parent_end:
            raise ValueError("Truncated node header")
        end, count, length, name_length = struct.unpack_from(fmt, data, offset)
        if end == 0:
            if data[offset:offset + size] != b"\0" * size:
                raise ValueError("Malformed null sentinel")
            return None, offset + size
        cursor = offset + size
        if end > parent_end or end < cursor + name_length + length:
            raise ValueError("Invalid absolute FBX node offset")
        name = data[cursor:cursor + name_length]
        cursor += name_length
        properties = data[cursor:cursor + length]
        cursor += length
        children, sentinel = [], False
        while cursor < end:
            child, cursor = read_node(cursor, end)
            if child is None:
                sentinel = True
                if cursor != end:
                    raise ValueError("Child sentinel does not terminate its node")
            else:
                children.append(child)
        if cursor != end:
            raise ValueError("Node does not end at declared offset")
        return Node(name, count, properties, children, sentinel), end

    offset, nodes = 27, []
    while True:
        node, offset = read_node(offset, len(data))
        if node is None:
            break
        nodes.append(node)
    return Document(version, nodes, data[offset:], offset)


def serialize(doc: Document) -> bytes:
    fmt = "<QQQB" if doc.version >= 7500 else "<IIIB"
    size = struct.calcsize(fmt)
    stream = io.BytesIO()
    stream.write(MAGIC + struct.pack("<I", doc.version))

    def emit(node: Node):
        offset = stream.tell()
        stream.write(b"\0" * size)
        stream.write(node.name)
        stream.write(node.properties)
        for child in node.children:
            emit(child)
        if node.sentinel:
            stream.write(b"\0" * size)
        end = stream.tell()
        stream.seek(offset)
        stream.write(struct.pack(fmt, end, node.count, len(node.properties), len(node.name)))
        stream.seek(end)

    for node in doc.nodes:
        emit(node)
    stream.write(b"\0" * size)
    # Blender's local official encoder documents this footer alignment. Only
    # padding changes; opaque footer ID/version/magic bytes are preserved.
    footer = doc.footer
    tail = struct.pack("<I", doc.version) + b"\0" * 120 + FOOT_MAGIC
    if not footer.endswith(tail) or not 161 <= len(footer) <= 176:
        raise ValueError("Unrecognized FBX footer; refusing to guess its layout")
    prefix = footer[:-len(tail)]
    if len(prefix) < 21 or prefix[16:] != b"\0" * (len(prefix) - 16):
        raise ValueError("Unexpected opaque footer padding")
    pad = 16 - ((stream.tell() + 20) % 16)
    stream.write(prefix[:20] + b"\0" * pad + tail)
    return stream.getvalue()


def walk(nodes: list[Node], path=()):
    for i, node in enumerate(nodes):
        current = path + ((node.name.decode("utf-8", "backslashreplace"), i),)
        yield current, node
        yield from walk(node.children, current)


def remove_content(doc: Document):
    removed = []
    for path, node in list(walk(doc.nodes)):
        if node.name != b"Video":
            continue
        retained = []
        for child in node.children:
            if child.name == b"Content":
                if child.children or child.count != 1 or not child.properties.startswith(b"R"):
                    raise ValueError("Video/Content has unexpected schema")
                length = struct.unpack_from("<I", child.properties, 1)[0]
                if length + 5 != len(child.properties):
                    raise ValueError("Embedded raw-media length mismatch")
                removed.append({"video_path": path, "payload_bytes": length,
                                "payload_sha256": sha(child.properties[5:])})
            else:
                retained.append(child)
        node.children = retained
    return removed


def semantic_digest(nodes: list[Node]) -> str:
    digest = hashlib.sha256()

    def add(node):
        digest.update(struct.pack("<QQQ?", len(node.name), node.count, len(node.properties), node.sentinel))
        digest.update(node.name)
        digest.update(node.properties)
        digest.update(struct.pack("<Q", len(node.children)))
        for child in node.children:
            add(child)

    for node in nodes:
        add(node)
    return digest.hexdigest()


def protected_hashes(doc: Document):
    categories = (b"Geometry", b"Model", b"Deformer", b"Pose", b"AnimationStack",
                  b"AnimationLayer", b"AnimationCurveNode", b"AnimationCurve", b"Connections")
    return {category.decode(): {"count": len(nodes), "sha256": semantic_digest(nodes)}
            for category in categories
            for nodes in [[node for _, node in walk(doc.nodes) if node.name == category]]}


def selfcheck():
    for version in (7400, 7500):
        raw = b"fake embedded PNG bytes"
        media = Node(b"Content", 1, b"R" + struct.pack("<I", len(raw)) + raw)
        protected = Node(b"Geometry", 1, b"S\x04\0\0\0mesh")
        video = Node(b"Video", 0, b"", [media, Node(b"Filename", 1, b"S\x05\0\0\0x.png")], True)
        footer = b"opaque-footer-id" + b"\0" * 5 + struct.pack("<I", version) + b"\0" * 120 + FOOT_MAGIC
        original = Document(version, [Node(b"Objects", 0, b"", [protected, video], True)], footer, 0)
        encoded = serialize(original)
        parsed = parse(encoded)
        assert serialize(parsed) == encoded, "Untouched round-trip changed bytes"
        original_hashes = protected_hashes(parsed)
        removed = remove_content(parsed)
        assert len(removed) == 1 and removed[0]["payload_sha256"] == sha(raw)
        clean = parse(serialize(parsed))
        assert protected_hashes(clean) == original_hashes
        assert semantic_digest(clean.nodes) == semantic_digest(parsed.nodes)
        assert remove_content(clean) == []
    print("FOC_MESHY_BINARY_MEDIA_SELFCHECK_PASS versions=7400,7500")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path)
    parser.add_argument("--output-directory", type=Path, default=Path("Artifacts/MeshyPilotOptimized"))
    parser.add_argument("--selfcheck", action="store_true")
    args = parser.parse_args()
    if args.selfcheck:
        selfcheck()
        if args.source is None:
            return
    if args.source is None:
        parser.error("--source is required unless running only --selfcheck")
    source = args.source.resolve()
    original_bytes = source.read_bytes()
    if sha(original_bytes) != EXPECTED_SHA:
        raise ValueError("Source is not the pinned original Meshy FBX")
    original = parse(original_bytes)
    if serialize(original) != original_bytes:
        raise ValueError("Untouched FBX round-trip is not byte-exact; do not proceed")
    cleaned = copy.deepcopy(original)
    removed = remove_content(cleaned)
    if not removed:
        raise ValueError("Expected source embedded Video/Content is absent")
    cleaned_bytes = serialize(cleaned)
    parsed_cleaned = parse(cleaned_bytes)
    if semantic_digest(cleaned.nodes) != semantic_digest(parsed_cleaned.nodes):
        raise ValueError("Retained full-tree semantics changed")
    if protected_hashes(original) != protected_hashes(parsed_cleaned):
        raise ValueError("Protected geometry/rig/skin/bind/animation data changed")
    if remove_content(copy.deepcopy(parsed_cleaned)):
        raise ValueError("Embedded media remains after strip")
    destination = args.output_directory.resolve()
    output = destination / "Hasan_Meshy_Runtime_Lod0.fbx"
    if source == output:
        raise ValueError("Output must not be original source")
    destination.mkdir(parents=True, exist_ok=True)
    temporary = output.with_suffix(".fbx.tmp")
    temporary.write_bytes(cleaned_bytes)
    temporary.replace(output)
    if sha(source.read_bytes()) != EXPECTED_SHA:
        raise ValueError("Original source changed")
    report = {"status": "SEMANTICALLY_IDENTICAL_EXCEPT_REMOVED_VIDEO_CONTENT",
              "source": str(source), "source_sha256": EXPECTED_SHA, "source_bytes": len(original_bytes),
              "output": str(output), "output_sha256": sha(cleaned_bytes), "output_bytes": len(cleaned_bytes),
              "bytes_saved": len(original_bytes) - len(cleaned_bytes), "removed_content": removed,
              "retained_semantic_sha256": semantic_digest(cleaned.nodes),
              "protected_before": protected_hashes(original), "protected_after": protected_hashes(parsed_cleaned),
              "untouched_roundtrip_byte_identical": True, "source_unchanged": True,
              "geometry_rig_bindpose_weights_animation_raw_property_bytes_identical": True,
              "footer_change": "Offset headers and alignment padding only; opaque footer bytes preserved.",
              "material_note": "External texture filenames remain unchanged; bind intended explicit Unity PBR material. No source image used or rewritten.",
              "unity_validation": "NOT_RUN_BY_THIS_SCRIPT", "license_status": "LOCAL_CANDIDATE_PENDING_PARENT_PROVENANCE_REVIEW"}
    (destination / "fbx-media-strip-audit.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print("FOC_MESHY_BINARY_MEDIA_STRIP_PASS " + json.dumps({key: report[key] for key in
          ("source_bytes", "output_bytes", "bytes_saved", "output_sha256", "source_unchanged")}))


if __name__ == "__main__":
    main()
