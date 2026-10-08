using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    /// <summary>Equipped benchmark preparation/cleanup; no timing or visual acceptance.</summary>
    public sealed class MeshyHasanPilotBenchmarkGripTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

        [Test]
        public void RetainedKilicIsOptionalTailParameterForExistingRunCallers()
        {
            var tail = typeof(MeshyHasanPilotBenchmark).GetMethod("Run").GetParameters().Last();
            Assert.That(tail.Name, Is.EqualTo("kilicPrefab"));
            Assert.That(tail.ParameterType, Is.EqualTo(typeof(GameObject)));
            Assert.That(tail.IsOptional, Is.True);
            Assert.That(tail.DefaultValue, Is.Null);
        }

        [Test]
        public void ActualEquippedPathIncludesWeaponGeometryAndClosesAllThreeLods()
        {
            using (var trial = new Trial(true))
            {
                var view = trial.Rent();
                var before = MeshyHasanPilotBenchmark.InspectGeometry(new[] { view });
                var motion = Animate(view, trial);
                try
                {
                    Attach(motion, view, trial.Kilic);
                    var weapon = Field<GameObject>(motion, "weaponInstance");
                    var animator = Field<Animator>(motion, "animator");
                    var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                    Assert.That(weapon.transform.IsChildOf(hand), Is.True);
                    Assert.That(weapon.transform.parent.name, Is.EqualTo("Socket_RightHand"));
                    Assert.That(MeshyKilicGripAttachment.MeasureAnchorErrorMeters(weapon.transform, hand,
                        MeshyKilicGripAttachment.CorrectivePalmInHand), Is.LessThan(1e-5f));
                    AssertGrip(view, true, 100f);
                    var after = MeshyHasanPilotBenchmark.InspectGeometry(new[] { view });
                    Assert.That(after.rendererComponents, Is.EqualTo(before.rendererComponents + trial.Kilic.GetComponentsInChildren<Renderer>(true).Length));
                    Assert.That(after.skinnedRendererComponents, Is.EqualTo(before.skinnedRendererComponents));
                    Assert.That(after.materialSlots, Is.GreaterThan(before.materialSlots));
                    Assert.That(after.uniqueMaterials, Is.GreaterThan(before.uniqueMaterials));
                    Assert.That(after.uniqueMeshes, Is.GreaterThan(before.uniqueMeshes));
                    Assert.That(after.lods[0].triangles, Is.GreaterThan(before.lods[0].triangles));
                    Assert.That(after.unityReportedSharedTextureMemoryBytes, Is.GreaterThanOrEqualTo(before.unityReportedSharedTextureMemoryBytes));
                }
                finally { Stop(motion); }
                AssertGrip(view, false, 0f);
                Assert.That(view.GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(before.rendererComponents));
                trial.Return(view);
            }
        }

        [Test]
        public void CleanupRemovesAccessoryBeforeActualRepresentationAndViewAreReused()
        {
            using (var trial = new Trial(true))
            {
                var view = trial.Rent();
                var root = view.ActiveRepresentationRoot;
                var motion = Animate(view, trial);
                Attach(motion, view, trial.Kilic);
                var weapon = Field<GameObject>(motion, "weaponInstance");
                Stop(motion);
                Assert.That(weapon == null, Is.True, "EditMode cleanup destroys the review accessory immediately.");
                AssertGrip(view, false, 0f);
                trial.Return(view);
                var reused = trial.Rent();
                Assert.That(reused, Is.SameAs(view));
                Assert.That(reused.ActiveRepresentationRoot, Is.SameAs(root));
                AssertGrip(reused, false, 0f);
                Assert.That(reused.GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(3));
                trial.Return(reused);
                Assert.That(trial.Pool.LeasedCount + trial.Assembler.ActiveLeaseCount, Is.Zero);
            }
        }

        [Test]
        public void UncorrectedSourceFailsBeforeWeaponInstantiation()
        {
            using (var trial = new Trial(false))
            {
                var view = trial.Rent();
                var motion = Animate(view, trial);
                try
                {
                    var exception = Assert.Throws<TargetInvocationException>(() => Attach(motion, view, trial.Kilic));
                    Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
                    Assert.That(exception.InnerException.Message, Does.Contain("right-hand grip component"));
                    Assert.That(Field<GameObject>(motion, "weaponInstance"), Is.Null);
                    Assert.That(view.GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(3));
                }
                finally { Stop(motion); }
                trial.Return(view);
            }
        }

        private static void AssertGrip(VisualSoldier3D view, bool equipped, float weight)
        {
            var driver = view.GetComponentInChildren<MeshyRightHandGripVisual>(true);
            Assert.That(driver.KilicEquipped, Is.EqualTo(equipped));
            foreach (var skin in driver.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                Assert.That(skin.GetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex(MeshyRightHandGripVisual.ShapeName)), Is.EqualTo(weight));
        }

        private static object Animate(VisualSoldier3D view, Trial trial) =>
            typeof(MeshyHasanPilotBenchmark).GetMethod("Animate", PrivateStatic).Invoke(null,
                new object[] { view, trial.Prefab, trial.Clip, .25f, trial.Calibrated });
        private static void Attach(object motion, VisualSoldier3D view, GameObject kilic) =>
            typeof(MeshyHasanPilotBenchmark).GetMethod("AttachKilic", PrivateStatic).Invoke(null, new object[] { motion, view, kilic });
        private static void Stop(object motion) => typeof(MeshyHasanPilotBenchmark).GetMethod("StopMotion", PrivateStatic, null,
            new[] { motion.GetType() }, null).Invoke(null, new[] { motion });
        private static T Field<T>(object motion, string name) => (T)motion.GetType().GetField(name).GetValue(motion);

        private sealed class Trial : IDisposable
        {
            public readonly GameObject Prefab, Kilic, Host;
            public readonly Avatar Calibrated;
            public readonly AnimationClip Clip;
            public readonly VisualSoldier3DAssembler Assembler;
            public readonly VisualSoldierPool Pool;
            private readonly VisualCatalogAsset catalog;
            private readonly HashSet<VisualSoldier3D> leases = new HashSet<VisualSoldier3D>();
            private int sequence;

            public Trial(bool corrected)
            {
                Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(corrected ? MeshyKilicGripCorrectivePipeline.PrefabPath : MeshyHasanPilotCatalog.PrefabPath);
                Kilic = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FOC/Presentation/Equipment/Ottoman1648/WPN_Kilic_01.prefab");
                Calibrated = AssetDatabase.LoadAssetAtPath<Avatar>(MeshyHumanoidCalibration.TargetAvatarPath);
                Assert.That(Prefab != null && Kilic != null && Calibrated != null, Is.True, "Actual retained assets are required, not a skipped test.");
                Clip = AssetDatabase.LoadAllAssetsAtPath(MeshyHasanPilotBuild.SourcePath).OfType<AnimationClip>().Single(c => c.name == "Walking");
                Host = new GameObject("Meshy equipped benchmark test");
                Assembler = Host.AddComponent<VisualSoldier3DAssembler>();
                Pool = Host.AddComponent<VisualSoldierPool>();
                var template = new GameObject("Empty benchmark view").AddComponent<VisualSoldier3D>();
                template.transform.SetParent(Host.transform, false);
                template.gameObject.SetActive(false);
                Pool.Configure(template, 2);
                catalog = MeshyHasanPilotCatalog.Create(Prefab);
            }

            public VisualSoldier3D Rent()
            {
                var view = Pool.Rent(); leases.Add(view);
                var index = sequence++;
                var troop = new TroopDefinition(TroopDefinitionId.Create("benchmark-grip-test"), "Benchmark grip fixture",
                    UnitClassId.Create("grip-test-class"), CombatRoleId.Create("grip-test-role"), MountContext.InfantryOnly,
                    VisualProfileId.Create(MeshyHasanPilotCatalog.ProfileId));
                var soldier = new SoldierInstance(SoldierId.Create("benchmark-grip-" + index), UnitGroupId.Create("benchmark-grip-group"), troop.Id,
                    new SoldierRecruitmentProvenance(RecruitmentSourceId.Create("benchmark-grip-source"), RecruitmentRecordId.Create("benchmark-grip-record-" + index)),
                    new SoldierLoadout(Array.Empty<WeaponSlotAssignment>()), troop.DefaultCombatRoleId);
                MeshyHasanPilotCatalog.Assemble(Assembler, catalog, view, soldier, troop, new Dictionary<EquipmentInstanceId, EquipmentInstance>());
                return view;
            }
            public void Return(VisualSoldier3D view) { Pool.Return(view); leases.Remove(view); }
            public void Dispose()
            {
                foreach (var view in leases.ToArray()) Return(view);
                Assembler.DisposeCache(); Object.DestroyImmediate(Host); Object.DestroyImmediate(catalog);
            }
        }
    }
}
