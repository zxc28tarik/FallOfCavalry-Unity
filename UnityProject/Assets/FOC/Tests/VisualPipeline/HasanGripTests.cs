using System;
using System.IO;
using System.Linq;
using FOC.Editor.Visuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FOC.Tests.VisualPipeline
{
    /// <summary>Numeric corrective integrity, never a substitute for rendered grip acceptance.</summary>
    public sealed class HasanGripTests
    {
        private const string SourcePath = "Assets/FOC/ArtSource/HistoricalSlice/HasanDonor/CHR_HasanAga_DonorDraft.focmesh.json";
        private const string BasisPath = "Assets/FOC/ArtSource/HistoricalSlice/Characters/CHR_HasanAga_01.focmesh.json";
        private const string PrefabPath = HistoricalArtCandidatePipeline.CharacterRoot + "/CHR_HasanAga_DonorDraft.prefab";

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void GripCorrectivePreservesBothOriginalHandBasesAndWeights(int lod)
        {
            var source = Read(SourcePath);
            var basis = Read(BasisPath);
            foreach (var hand in new[] { 0, 1 })
            {
                var actual = source.lods[lod].parts[hand];
                var original = basis.lods[lod].parts[hand];
                Assert.That(actual.material, Is.EqualTo(original.material));
                CollectionAssert.AreEqual(original.positions, actual.positions, "Open hand basis changed.");
                CollectionAssert.AreEqual(original.normals, actual.normals);
                CollectionAssert.AreEqual(original.uv, actual.uv);
                CollectionAssert.AreEqual(original.triangles, actual.triangles);
                CollectionAssert.AreEqual(original.boneIndices, actual.boneIndices);
                CollectionAssert.AreEqual(original.boneWeights, actual.boneWeights);
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void BothGripShapesHaveFiniteBoundedDeltasAndLeaveForearmUnmoved(int lod)
        {
            var source = Read(SourcePath);
            var gripNames = source.lods[lod].parts.SelectMany(p => p.blendShapes)
                .Where(s => s.name.StartsWith("Grip_", StringComparison.Ordinal)).Select(s => s.name).ToArray();
            CollectionAssert.AreEquivalent(new[] { "Grip_L", "Grip_R" }, gripNames);
            for (var hand = 0; hand < 2; hand++)
            {
                var part = source.lods[lod].parts[hand];
                var side = hand == 0 ? "L" : "R";
                var shape = part.blendShapes.Single(s => s.name == "Grip_" + side);
                Assert.That(shape.deltaPositions.Length, Is.EqualTo(part.positions.Length));
                Assert.That(shape.deltaNormals.Length, Is.EqualTo(part.normals.Length));
                Assert.That(shape.deltaPositions.All(Finite), Is.True);
                Assert.That(shape.deltaNormals.All(Finite), Is.True);
                var maximum = 0f;
                var protectedForearm = 0;
                for (var vertex = 0; vertex < part.positions.Length / 3; vertex++)
                {
                    var delta = V(shape.deltaPositions, vertex * 3);
                    maximum = Mathf.Max(maximum, delta.magnitude);
                    var normal = V(part.normals, vertex * 3) + V(shape.deltaNormals, vertex * 3);
                    Assert.That(normal.magnitude, Is.EqualTo(1f).Within(.0001f), "Corrected normal is not unit length.");
                    var handWeight = 0f;
                    for (var influence = 0; influence < 4; influence++)
                    {
                        var offset = vertex * 4 + influence;
                        if (source.bones[part.boneIndices[offset]].name == "Hand_" + side)
                            handWeight += part.boneWeights[offset];
                    }
                    if (handWeight < .5f)
                    {
                        protectedForearm++;
                        Assert.That(delta.magnitude, Is.LessThan(.0001f), "Finger corrective must not move the sleeve/wrist cut.");
                    }
                }
                Assert.That(maximum, Is.GreaterThan(.02f).And.LessThan(.18f), "Empty or exploded grip corrective.");
                Assert.That(protectedForearm, Is.GreaterThan(0), "No cut-forearm samples were actually checked.");
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void ImportedGripFramesExactlyMatchSourceAndDoNotMoveOtherParts(int lod)
        {
            var source = Read(SourcePath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var skin = prefab.GetComponent<LODGroup>().GetLODs()[lod].renderers.Single() as SkinnedMeshRenderer;
            Assert.That(skin, Is.Not.Null);
            var mesh = skin.sharedMesh;
            foreach (var name in new[] { "Grip_L", "Grip_R" })
            {
                var index = mesh.GetBlendShapeIndex(name);
                Assert.That(index, Is.GreaterThanOrEqualTo(0), "Missing imported " + name);
                Assert.That(mesh.GetBlendShapeFrameCount(index), Is.EqualTo(1));
                Assert.That(mesh.GetBlendShapeFrameWeight(index, 0), Is.EqualTo(100f));
                var positions = new Vector3[mesh.vertexCount];
                var normals = new Vector3[mesh.vertexCount];
                mesh.GetBlendShapeFrameVertices(index, 0, positions, normals, null);
                var offset = 0;
                foreach (var part in source.lods[lod].parts)
                {
                    var shape = part.blendShapes.SingleOrDefault(s => s.name == name);
                    for (var vertex = 0; vertex < part.positions.Length / 3; vertex++)
                    {
                        var expectedPosition = shape == null ? Vector3.zero : V(shape.deltaPositions, vertex * 3);
                        var expectedNormal = shape == null ? Vector3.zero : V(shape.deltaNormals, vertex * 3);
                        Assert.That(Vector3.Distance(positions[offset + vertex], expectedPosition), Is.LessThan(.000001f));
                        Assert.That(Vector3.Distance(normals[offset + vertex], expectedNormal), Is.LessThan(.000001f));
                    }
                    offset += part.positions.Length / 3;
                }
                Assert.That(offset, Is.EqualTo(mesh.vertexCount));
            }
        }

        [Test]
        public void OfflineFingerPoseDoesNotIntroduceRuntimeFingerBones()
        {
            var source = Read(SourcePath);
            var basis = Read(BasisPath);
            Assert.That(source.bones.Length, Is.EqualTo(18));
            CollectionAssert.AreEqual(basis.bones.Select(b => JsonUtility.ToJson(b)).ToArray(), source.bones.Select(b => JsonUtility.ToJson(b)).ToArray());
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            foreach (var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                CollectionAssert.AreEqual(source.bones.Select(b => b.name).ToArray(), skin.bones.Select(b => b.name).ToArray());
                Assert.That(skin.bones.Any(b => b.name.StartsWith("finger", StringComparison.OrdinalIgnoreCase)
                    || b.name.StartsWith("metacarpal", StringComparison.OrdinalIgnoreCase)), Is.False);
            }
        }

        [Test]
        public void ReviewSocketPlacesKilicHandleMidpointAtAuthoredGripCenter()
        {
            var record = JsonUtility.FromJson<GripEnvelope>(File.ReadAllText(SourcePath)).gripAttachment;
            Assert.That(record, Is.Not.Null);
            Assert.That(record.status, Is.EqualTo("DRAFT_REQUIRES_RENDERED_QA"));
            Assert.That(record.side, Is.EqualTo("R"));
            CollectionAssert.AreEqual(new[] { -.006f, -.080f, 0f }, record.weaponGripCenter);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var socket = prefab.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Socket_ReviewKilic");
            Assert.That(socket.parent.name, Is.EqualTo("Hand_R"));
            Assert.That(Vector3.Distance(socket.localPosition, V(record.localPosition, 0)), Is.LessThan(.000001f));
            var r = record.localRotation;
            var rotation = new Quaternion(r[0], r[1], r[2], r[3]);
            Assert.That(Quaternion.Angle(socket.localRotation, rotation), Is.LessThan(.01f));
            var actualCenter = socket.localPosition + socket.localRotation * V(record.weaponGripCenter, 0);
            Assert.That(Vector3.Distance(actualCenter, V(record.gripCenterLocal, 0)), Is.LessThan(.00001f),
                "Weapon origin is the guard, not the handle midpoint.");
            Assert.That(Vector3.Dot(socket.localRotation * Vector3.up, V(record.gripAxisLocal, 0)), Is.GreaterThan(.9999f));
            Assert.That(Vector3.Dot(socket.localRotation * Vector3.forward, V(record.palmNormalLocal, 0)), Is.GreaterThan(.9999f));
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static Vector3 V(float[] values, int offset) => new Vector3(values[offset], values[offset + 1], values[offset + 2]);
        private static HistoricalArtCandidatePipeline.SourceAsset Read(string path) =>
            JsonUtility.FromJson<HistoricalArtCandidatePipeline.SourceAsset>(File.ReadAllText(path));

        [Serializable]
        private sealed class GripEnvelope
        {
            public GripRecord gripAttachment = new GripRecord();
        }

        [Serializable]
        private sealed class GripRecord
        {
            public string side = "";
            public string status = "";
            public float[] localPosition = Array.Empty<float>();
            public float[] localRotation = Array.Empty<float>();
            public float[] gripCenterLocal = Array.Empty<float>();
            public float[] gripAxisLocal = Array.Empty<float>();
            public float[] palmNormalLocal = Array.Empty<float>();
            public float[] weaponGripCenter = Array.Empty<float>();
        }
    }
}
