using System;
using System.Collections.Generic;
using FOC.Presentation.Visuals;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FOC.Tests.VisualPipeline
{
    public sealed class MeshyRightHandGripVisualTests
    {
        private GameObject owner;
        private MeshyRightHandGripVisual grip;
        private SkinnedMeshRenderer[] skins;
        private readonly List<Mesh> ownedMeshes = new List<Mesh>();

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("Right-hand derivative grip test");
            skins = new SkinnedMeshRenderer[3];
            for (var i = 0; i < skins.Length; i++)
            {
                var lod = new GameObject("LOD" + i);
                lod.transform.SetParent(owner.transform, false);
                skins[i] = lod.AddComponent<SkinnedMeshRenderer>();
                skins[i].sharedMesh = NewMesh(true);
                skins[i].SetBlendShapeWeight(0, 37f + i);
                skins[i].enabled = i == 0;
            }
            grip = owner.AddComponent<MeshyRightHandGripVisual>();
            grip.Configure(skins);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(owner);
            foreach (var mesh in ownedMeshes) Object.DestroyImmediate(mesh);
            ownedMeshes.Clear();
        }

        [Test]
        public void ThreeLodsCloseTogetherAndResetWithoutTouchingUnrelatedShape()
        {
            grip.SetKilicEquipped(true);
            Assert.That(grip.KilicEquipped, Is.True);
            AssertWeights(100f);
            grip.ResetForPool();
            Assert.That(grip.KilicEquipped, Is.False);
            AssertWeights(0f);
            AssertUnrelatedWeights();
        }

        [Test]
        public void UnequippingOpensEveryLodAndKeepsOtherSystemsWeights()
        {
            grip.SetKilicEquipped(true);
            grip.SetKilicEquipped(false);
            AssertWeights(0f);
            AssertUnrelatedWeights();
        }

        [Test]
        public void DisableEnableCannotCarryEquippedFlagIntoAnotherLease()
        {
            // A normal MonoBehaviour does not receive every activation callback
            // in EditMode. Exercise the actual private lifecycle entry points,
            // not ResetForPool directly, while retaining the strict assertions.
            void InvokeLifecycle(string name)
            {
                var method = typeof(MeshyRightHandGripVisual).GetMethod(name,
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.That(method, Is.Not.Null, "Required runtime lifecycle callback " + name);
                method.Invoke(grip, Array.Empty<object>());
            }

            grip.SetKilicEquipped(true);
            owner.SetActive(false);
            InvokeLifecycle("OnDisable");
            Assert.That(grip.KilicEquipped, Is.False);
            AssertWeights(0f);
            // Independently prove OnEnable also clears a stale inactive state;
            // merely testing the flag already cleared by OnDisable would not.
            grip.SetKilicEquipped(true);
            AssertWeights(100f);
            owner.SetActive(true);
            InvokeLifecycle("OnEnable");
            grip.ApplyCurrentState();
            Assert.That(grip.KilicEquipped, Is.False);
            AssertWeights(0f);
            AssertUnrelatedWeights();
        }

        [Test]
        public void ReapplicationRestoresGripAfterAnimationWeightOverwrite()
        {
            grip.SetKilicEquipped(true);
            for (var i = 0; i < skins.Length; i++)
                skins[i].SetBlendShapeWeight(skins[i].sharedMesh.GetBlendShapeIndex(MeshyRightHandGripVisual.ShapeName), 12f);
            grip.ApplyCurrentState();
            AssertWeights(100f);
            AssertUnrelatedWeights();
        }

        [Test]
        public void RepeatedStateChangesKeepSameMeshesRendererCountAndBoneTransforms()
        {
            var originalMeshes = new[] { skins[0].sharedMesh, skins[1].sharedMesh, skins[2].sharedMesh };
            var positions = new[] { skins[0].transform.localPosition, skins[1].transform.localPosition, skins[2].transform.localPosition };
            for (var iteration = 0; iteration < 100; iteration++)
            {
                grip.SetKilicEquipped(iteration % 2 == 0);
                grip.ApplyCurrentState();
                grip.ResetForPool();
            }
            Assert.That(owner.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length, Is.EqualTo(3));
            for (var i = 0; i < skins.Length; i++)
            {
                Assert.That(skins[i].sharedMesh, Is.SameAs(originalMeshes[i]));
                Assert.That(skins[i].transform.localPosition, Is.EqualTo(positions[i]));
            }
            AssertWeights(0f);
            AssertUnrelatedWeights();
        }

        [Test]
        public void ConfigurationCopiesRendererArrayAndDoesNotTrustLaterCallerMutation()
        {
            var input = (SkinnedMeshRenderer[])skins.Clone();
            grip.Configure(input);
            input[0] = null;
            grip.SetKilicEquipped(true);
            AssertWeights(100f);
        }

        [Test]
        public void MissingCandidateShapeFailsBeforePartiallyMutatingLods()
        {
            var original = skins[2].sharedMesh;
            skins[2].sharedMesh = NewMesh(false);
            var clean = new GameObject("Unconfigured grip");
            clean.transform.SetParent(owner.transform, false);
            var unconfigured = clean.AddComponent<MeshyRightHandGripVisual>();
            try
            {
                Assert.Throws<InvalidOperationException>(() => unconfigured.Configure(skins));
                Assert.That(unconfigured.KilicEquipped, Is.False);
                Assert.That(skins[0].GetBlendShapeWeight(1), Is.EqualTo(0f));
                Assert.That(skins[1].GetBlendShapeWeight(1), Is.EqualTo(0f));
            }
            finally { skins[2].sharedMesh = original; }
        }

        [Test]
        public void UnconfiguredExplicitEquipFailsClosedRatherThanChangingSourceMesh()
        {
            var child = new GameObject("Source without candidate shape");
            child.transform.SetParent(owner.transform, false);
            var unconfigured = child.AddComponent<MeshyRightHandGripVisual>();
            Assert.Throws<InvalidOperationException>(() => unconfigured.SetKilicEquipped(true));
            Assert.That(unconfigured.KilicEquipped, Is.False);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(4)]
        public void MissingOrExtraLodsAreRejected(int count)
        {
            Assert.Throws<InvalidOperationException>(() => grip.Configure(new SkinnedMeshRenderer[count]));
            AssertWeights(0f);
        }

        [Test]
        public void DuplicateLodRendererIsRejected()
        {
            Assert.Throws<InvalidOperationException>(() => grip.Configure(new[] { skins[0], skins[1], skins[1] }));
            AssertWeights(0f);
        }

        [Test]
        public void UnexpectedMeshSwapFailsBeforeWritingAnyGripWeight()
        {
            var original = skins[2].sharedMesh;
            skins[2].sharedMesh = NewMesh(true);
            try
            {
                Assert.Throws<InvalidOperationException>(() => grip.SetKilicEquipped(true));
                Assert.That(grip.KilicEquipped, Is.False);
                Assert.That(skins[0].GetBlendShapeWeight(1), Is.EqualTo(0f));
                Assert.That(skins[1].GetBlendShapeWeight(1), Is.EqualTo(0f));
            }
            finally { skins[2].sharedMesh = original; }
        }

        [Test]
        public void NullConfigurationIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => grip.Configure(null));
        }

        private Mesh NewMesh(bool includeGrip)
        {
            var mesh = new Mesh { name = "Synthetic derivative hand mesh" };
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.AddBlendShapeFrame("Pose_Unrelated", 100f, new Vector3[3], new Vector3[3], new Vector3[3]);
            if (includeGrip)
                mesh.AddBlendShapeFrame(MeshyRightHandGripVisual.ShapeName, 100f,
                    new[] { Vector3.zero, Vector3.forward * .01f, Vector3.zero }, new Vector3[3], new Vector3[3]);
            ownedMeshes.Add(mesh);
            return mesh;
        }

        private void AssertWeights(float expected)
        {
            foreach (var skin in skins)
                Assert.That(skin.GetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex(MeshyRightHandGripVisual.ShapeName)), Is.EqualTo(expected));
        }

        private void AssertUnrelatedWeights()
        {
            for (var i = 0; i < skins.Length; i++) Assert.That(skins[i].GetBlendShapeWeight(0), Is.EqualTo(37f + i));
        }

    }
}
