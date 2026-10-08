using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FOC.Domain.Common;
using FOC.Domain.Soldiers;
using FOC.Editor.Visuals;
using FOC.Presentation.Visuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FOC.Tests.VisualPipeline
{
    /// <summary>Right-hand derivative integrity, not certification of a believable grasp.</summary>
    public sealed class MeshyKilicGripCorrectiveTests
    {
        private const string ProtectedSourceHash = "356dd92317ec1787f48aabe4e85157d31dfec2dd029168c856c5ffd6a125fadf";

        [Test]
        public void RequiredCandidateAndProvenanceMustExistRatherThanSkipTheGate()
        {
            Assert.That(Source(), Is.Not.Null);
            var candidate = Candidate();
            Assert.That(candidate.GetComponent<MeshyRightHandGripVisual>(), Is.Not.Null);
            Assert.That(File.Exists(MeshyKilicGripCorrectivePipeline.ProvenancePath), Is.True,
                "Approved derivative provenance is required, not an optional skipped test.");
            Assert.That(Skins(candidate).Length, Is.EqualTo(3));
        }

        [Test]
        public void ProtectedOriginalRuntimeFbxHashRemainsUnchanged()
        {
            Assert.That(File.Exists(MeshyHasanPilotBuild.SourcePath), Is.True);
            using (var hash = SHA256.Create())
                Assert.That(string.Concat(hash.ComputeHash(File.ReadAllBytes(MeshyHasanPilotBuild.SourcePath))
                    .Select(b => b.ToString("x2"))), Is.EqualTo(ProtectedSourceHash));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void DerivativeRetainsExactOriginalMeshBasisTopologyUvAndSkinning(int lod)
        {
            var source = Skins(Source())[lod].sharedMesh;
            var candidate = Skins(Candidate())[lod].sharedMesh;
            Assert.That(candidate, Is.Not.SameAs(source));
            Assert.That(candidate.vertexCount, Is.EqualTo(source.vertexCount));
            CollectionAssert.AreEqual(source.vertices, candidate.vertices, "Unchanged base vertices LOD" + lod);
            CollectionAssert.AreEqual(source.normals, candidate.normals, "Unchanged base normals LOD" + lod);
            CollectionAssert.AreEqual(source.tangents, candidate.tangents);
            CollectionAssert.AreEqual(source.uv, candidate.uv);
            CollectionAssert.AreEqual(source.uv2, candidate.uv2);
            CollectionAssert.AreEqual(source.colors, candidate.colors);
            CollectionAssert.AreEqual(source.bindposes, candidate.bindposes);
            CollectionAssert.AreEqual(source.boneWeights, candidate.boneWeights);
            Assert.That(candidate.indexFormat, Is.EqualTo(source.indexFormat));
            Assert.That(candidate.subMeshCount, Is.EqualTo(source.subMeshCount));
            Assert.That(candidate.bounds, Is.EqualTo(source.bounds));
            for (var submesh = 0; submesh < source.subMeshCount; submesh++)
            {
                Assert.That(candidate.GetTopology(submesh), Is.EqualTo(source.GetTopology(submesh)));
                CollectionAssert.AreEqual(source.GetIndices(submesh), candidate.GetIndices(submesh));
            }
            Assert.That(candidate.blendShapeCount, Is.EqualTo(source.blendShapeCount + 1));
            Assert.That(source.GetBlendShapeIndex(MeshyRightHandGripVisual.ShapeName), Is.EqualTo(-1));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void CorrectiveIsFiniteUnitNormalAndConfinedToWeightedRightHandAboveWrist(int lod)
        {
            var sourceSkin = Skins(Source())[lod];
            var source = sourceSkin.sharedMesh;
            var candidate = Skins(Candidate())[lod].sharedMesh;
            var handIndex = Array.FindIndex(sourceSkin.bones, b => b.name == "RightHand");
            Assert.That(handIndex, Is.GreaterThanOrEqualTo(0));
            var vertices = source.vertices;
            var normals = source.normals;
            var weights = source.boneWeights;
            var deltas = Shape(candidate, out var normalDeltas);
            var changed = 0;
            var maximum = 0f;
            for (var i = 0; i < vertices.Length; i++)
            {
                Assert.That(Finite(deltas[i]) && Finite(normalDeltas[i]), Is.True, "Finite vertex " + i);
                var magnitude = deltas[i].magnitude;
                maximum = Mathf.Max(maximum, magnitude);
                Assert.That(magnitude, Is.LessThan(.15f), "Protected delta envelope " + i);
                if (magnitude > 1e-6f)
                {
                    changed++;
                    var handPoint = source.bindposes[handIndex].MultiplyPoint3x4(vertices[i]);
                    Assert.That(handPoint.y, Is.GreaterThanOrEqualTo(.08f), "No wrist/cuff deformation " + i);
                    Assert.That(Influence(weights[i], handIndex), Is.GreaterThanOrEqualTo(.8f), "Only actual right-hand vertices " + i);
                    Assert.That((normals[i] + normalDeltas[i]).magnitude, Is.EqualTo(1f).Within(.001f), "Posed normal " + i);
                }
                else
                    Assert.That(normalDeltas[i].sqrMagnitude, Is.Zero, "No unrelated normal-only edits " + i);
                if (Influence(weights[i], handIndex) < .8f ||
                    source.bindposes[handIndex].MultiplyPoint3x4(vertices[i]).y < .08f)
                    Assert.That(magnitude, Is.Zero, "Protected non-hand/wrist vertex " + i);
            }
            Assert.That(changed, Is.GreaterThanOrEqualTo(8), "A missing/empty shape cannot pass.");
            Assert.That(maximum, Is.GreaterThan(.02f));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void CoincidentUvSeamVerticesReceiveIdenticalPositionDeltas(int lod)
        {
            var source = Skins(Source())[lod].sharedMesh;
            var candidate = Skins(Candidate())[lod].sharedMesh;
            var vertices = source.vertices;
            var deltas = Shape(candidate, out _);
            var seen = new Dictionary<Vector3, Vector3>();
            var duplicates = 0;
            for (var i = 0; i < vertices.Length; i++)
            {
                if (seen.TryGetValue(vertices[i], out var first))
                {
                    duplicates++;
                    Assert.That(Vector3.Distance(deltas[i], first), Is.LessThan(1e-6f), "Seam vertex " + i);
                }
                else seen.Add(vertices[i], deltas[i]);
            }
            Assert.That(duplicates, Is.GreaterThan(0), "The real source has coincident UV vertices; no substitute mesh.");
        }

        [Test]
        public void CandidatePreservesTwentyThreeBoneRigAvatarControllerAndMaterials()
        {
            var source = Source();
            var candidate = Candidate();
            var sourceAnimator = source.GetComponent<Animator>();
            var candidateAnimator = candidate.GetComponent<Animator>();
            Assert.That(candidateAnimator.avatar, Is.SameAs(sourceAnimator.avatar));
            Assert.That(candidateAnimator.runtimeAnimatorController, Is.SameAs(sourceAnimator.runtimeAnimatorController));
            Assert.That(candidateAnimator.applyRootMotion, Is.EqualTo(sourceAnimator.applyRootMotion));
            var sourceSkins = Skins(source);
            var candidateSkins = Skins(candidate);
            for (var lod = 0; lod < 3; lod++)
            {
                Assert.That(candidateSkins[lod].bones.Length, Is.EqualTo(23));
                CollectionAssert.AreEqual(sourceSkins[lod].sharedMaterials, candidateSkins[lod].sharedMaterials);
                Assert.That(candidateSkins[lod].transform.localPosition, Is.EqualTo(sourceSkins[lod].transform.localPosition));
                Assert.That(candidateSkins[lod].transform.localScale, Is.EqualTo(sourceSkins[lod].transform.localScale));
                for (var bone = 0; bone < 23; bone++)
                {
                    var original = sourceSkins[lod].bones[bone];
                    var derived = candidateSkins[lod].bones[bone];
                    Assert.That(AnimationUtility.CalculateTransformPath(derived, candidate.transform),
                        Is.EqualTo(AnimationUtility.CalculateTransformPath(original, source.transform)));
                    Assert.That(Vector3.Distance(derived.localPosition, original.localPosition), Is.LessThan(1e-6f));
                    Assert.That(Quaternion.Angle(derived.localRotation, original.localRotation), Is.LessThan(.01f));
                    Assert.That(Vector3.Distance(derived.localScale, original.localScale), Is.LessThan(1e-6f));
                }
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void WeightZeroRestoresExactOpenBasisAfterWeightHundredClosesCandidate(int lod)
        {
            var instance = Object.Instantiate(Candidate());
            var open = new Mesh();
            var closed = new Mesh();
            var restored = new Mesh();
            try
            {
                instance.GetComponent<Animator>().enabled = false;
                MeshyHasanPilotPipeline.RestoreSourceBindPose(instance);
                var skin = Skins(instance)[lod];
                var driver = instance.GetComponent<MeshyRightHandGripVisual>();
                driver.SetKilicEquipped(false);
                skin.BakeMesh(open);
                var openVertices = open.vertices;
                driver.SetKilicEquipped(true);
                Assert.That(skin.GetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex(MeshyRightHandGripVisual.ShapeName)), Is.EqualTo(100f));
                skin.BakeMesh(closed);
                Assert.That(closed.vertices.Where((point, i) => Vector3.Distance(point, openVertices[i]) > .001f).Any(), Is.True);
                driver.SetKilicEquipped(false);
                skin.BakeMesh(restored);
                Assert.That(skin.GetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex(MeshyRightHandGripVisual.ShapeName)), Is.Zero);
                CollectionAssert.AreEqual(openVertices, restored.vertices);
                CollectionAssert.AreEqual(open.normals, restored.normals);
            }
            finally
            {
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(open);
                Object.DestroyImmediate(closed);
                Object.DestroyImmediate(restored);
            }
        }

        [Test]
        public void CorrectiveWeaponFrameExitsIndexSideAndAlignsBladePlaneToPalmNormal()
        {
            var rotation = MeshyKilicGripAttachment.CorrectiveWeaponRotationInHand;
            Assert.That(Vector3.Distance(rotation * Vector3.up, Vector3.back), Is.LessThan(1e-6f));
            Assert.That(Vector3.Distance(rotation * Vector3.forward, Vector3.left), Is.LessThan(1e-6f));
            Assert.That(Vector3.Distance(rotation * Vector3.right, Vector3.up), Is.LessThan(1e-6f));
            Assert.That(MeshyKilicGripAttachment.CorrectivePalmInHand.x, Is.LessThan(0f));
        }

        [Test]
        public void OfflinePointCorrectiveCannotTouchWristBelowProtectedEnvelope()
        {
            foreach (var point in new[] { Vector3.zero, new Vector3(-.03f, .079f, -.025f), new Vector3(.02f, .03f, .01f) })
            {
                var posed = MeshyKilicGripCorrectivePipeline.PoseHandPoint(point, out var rotation);
                Assert.That(posed, Is.EqualTo(point));
                Assert.That(rotation, Is.EqualTo(Quaternion.identity));
            }
        }

        [Test]
        public void ActualAssemblerReturnsAndReusesCandidateWithNoStaleGripOrIdentity()
        {
            var host = new GameObject("Kilic actual assembler test");
            var catalog = MeshyHasanPilotCatalog.Create(Candidate());
            var assembler = host.AddComponent<VisualSoldier3DAssembler>();
            var view = new GameObject("Grip pooled view").AddComponent<VisualSoldier3D>();
            view.transform.SetParent(host.transform, false);
            var troop = new TroopDefinition(TroopDefinitionId.Create("grip-pilot-troop"), "Grip pilot",
                UnitClassId.Create("grip-pilot-class"), CombatRoleId.Create("grip-pilot-role"), MountContext.InfantryOnly,
                VisualProfileId.Create("original-profile-must-not-mutate"));
            var equipment = new Dictionary<EquipmentInstanceId, EquipmentInstance>();
            try
            {
                var firstSoldier = Soldier(0, troop);
                MeshyHasanPilotCatalog.Assemble(assembler, catalog, view, firstSoldier, troop, equipment);
                var root = view.ActiveRepresentationRoot;
                Assert.That(root, Is.Not.Null);
                var driver = root.GetComponentInChildren<MeshyRightHandGripVisual>(true);
                Assert.That(driver, Is.Not.Null);
                driver.SetKilicEquipped(true);
                AssertGripWeights(root, 100f);
                view.ReleaseVisual();
                Assert.That(assembler.ActiveLeaseCount, Is.Zero);
                Assert.That(driver.KilicEquipped, Is.False);
                AssertGripWeights(root, 0f);
                var secondSoldier = Soldier(1, troop);
                MeshyHasanPilotCatalog.Assemble(assembler, catalog, view, secondSoldier, troop, equipment);
                Assert.That(view.ActiveRepresentationRoot, Is.SameAs(root));
                Assert.That(view.Binding.SoldierId.Value, Is.EqualTo(secondSoldier.Id));
                Assert.That(driver.KilicEquipped, Is.False);
                AssertGripWeights(root, 0f);
                Assert.That(firstSoldier.Loadout.OrderedWeapons.Count, Is.Zero);
                Assert.That(assembler.Metrics.PoolHits, Is.EqualTo(1));
                view.ReleaseVisual();
                Assert.That(assembler.ActiveLeaseCount, Is.Zero);
            }
            finally
            {
                view.ReleaseVisual();
                assembler.DisposeCache();
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(catalog);
            }
        }

        private static SoldierInstance Soldier(int index, TroopDefinition troop) => new SoldierInstance(
            SoldierId.Create("grip-pilot-soldier-" + index), UnitGroupId.Create("grip-pilot-group"), troop.Id,
            new SoldierRecruitmentProvenance(RecruitmentSourceId.Create("grip-pilot-source"), RecruitmentRecordId.Create("grip-pilot-record-" + index)),
            new SoldierLoadout(Array.Empty<WeaponSlotAssignment>()), troop.DefaultCombatRoleId);

        private static void AssertGripWeights(GameObject root, float expected)
        {
            var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.That(skins.Length, Is.EqualTo(3));
            foreach (var skin in skins)
                Assert.That(skin.GetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex(MeshyRightHandGripVisual.ShapeName)), Is.EqualTo(expected));
        }

        private static GameObject Source()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(MeshyHasanPilotBuild.PrefabPath);
            Assert.That(source, Is.Not.Null, MeshyHasanPilotBuild.PrefabPath);
            return source;
        }

        private static GameObject Candidate()
        {
            var candidate = AssetDatabase.LoadAssetAtPath<GameObject>(MeshyKilicGripCorrectivePipeline.PrefabPath);
            Assert.That(candidate, Is.Not.Null, "Missing approved candidate must fail, not skip: " + MeshyKilicGripCorrectivePipeline.PrefabPath);
            return candidate;
        }

        private static SkinnedMeshRenderer[] Skins(GameObject root)
        {
            var group = root.GetComponent<LODGroup>();
            Assert.That(group, Is.Not.Null);
            var lods = group.GetLODs();
            Assert.That(lods.Length, Is.EqualTo(3));
            return lods.Select(lod => lod.renderers.OfType<SkinnedMeshRenderer>().Single()).ToArray();
        }

        private static Vector3[] Shape(Mesh mesh, out Vector3[] normalDeltas)
        {
            var index = mesh.GetBlendShapeIndex(MeshyRightHandGripVisual.ShapeName);
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            Assert.That(mesh.GetBlendShapeFrameCount(index), Is.EqualTo(1));
            Assert.That(mesh.GetBlendShapeFrameWeight(index, 0), Is.EqualTo(100f));
            var deltas = new Vector3[mesh.vertexCount];
            normalDeltas = new Vector3[mesh.vertexCount];
            mesh.GetBlendShapeFrameVertices(index, 0, deltas, normalDeltas, null);
            return deltas;
        }

        private static float Influence(BoneWeight weight, int bone) =>
            (weight.boneIndex0 == bone ? weight.weight0 : 0f) + (weight.boneIndex1 == bone ? weight.weight1 : 0f) +
            (weight.boneIndex2 == bone ? weight.weight2 : 0f) + (weight.boneIndex3 == bone ? weight.weight3 : 0f);

        private static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z) &&
            !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);
    }
}
