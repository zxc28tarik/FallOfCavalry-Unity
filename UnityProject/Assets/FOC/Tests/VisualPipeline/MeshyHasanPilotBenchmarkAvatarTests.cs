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
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace FOC.Tests.VisualPipeline
{
    /// <summary>Benchmark preparation/lifecycle only. No timing or animation-quality acceptance.</summary>
    public sealed class MeshyHasanPilotBenchmarkAvatarTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

        [Test]
        public void DefaultMotionKeepsOriginalAvatarAndReturnsOriginalController()
        {
            using (var trial = new Trial())
            {
                var view = trial.Rent();
                var animator = view.GetComponentsInChildren<Animator>(true).Single();
                var motion = Animate(view, trial.Prefab, trial.Walking, null);
                try
                {
                    Assert.That(animator.avatar, Is.SameAs(trial.OriginalAvatar));
                    Assert.That(animator.runtimeAnimatorController, Is.Null);
                    Assert.That(Field<PlayableGraph>(motion, "graph").IsValid(), Is.True);
                }
                finally { Stop(motion); }
                Assert.That(animator.avatar, Is.SameAs(trial.OriginalAvatar));
                Assert.That(animator.runtimeAnimatorController, Is.SameAs(trial.OriginalController));
                trial.Return(view);
                Assert.That(trial.Pool.LeasedCount, Is.Zero);
                Assert.That(trial.Assembler.ActiveLeaseCount, Is.Zero);
            }
        }

        [Test]
        public void CalibratedOverrideIsRestoredBeforeSameRepresentationIsReused()
        {
            using (var trial = new Trial())
            {
                var view = trial.Rent();
                var representation = view.ActiveRepresentationRoot;
                var animator = view.GetComponentsInChildren<Animator>(true).Single();
                var candidate = Candidate();
                Assert.That(candidate, Is.Not.SameAs(trial.OriginalAvatar));
                var motion = Animate(view, trial.Prefab, trial.Walking, candidate);
                var graph = Field<PlayableGraph>(motion, "graph");
                try
                {
                    Assert.That(animator.avatar, Is.SameAs(candidate));
                    Assert.That(animator.runtimeAnimatorController, Is.Null);
                    Assert.That(Field<Avatar>(motion, "previousAvatar"), Is.SameAs(trial.OriginalAvatar));
                    Assert.That(graph.IsValid(), Is.True);
                }
                finally { Stop(motion); }
                Assert.That(graph.IsValid(), Is.False);
                Assert.That(animator.avatar, Is.SameAs(trial.OriginalAvatar));
                Assert.That(animator.runtimeAnimatorController, Is.SameAs(trial.OriginalController));
                trial.Return(view);
                var reused = trial.Rent();
                Assert.That(reused, Is.SameAs(view));
                Assert.That(reused.ActiveRepresentationRoot, Is.SameAs(representation));
                Assert.That(reused.GetComponentsInChildren<Animator>(true).Single().avatar, Is.SameAs(trial.OriginalAvatar));
                Assert.That(trial.Pool.CreatedViewCount, Is.EqualTo(1));
                Assert.That(trial.Assembler.Metrics.CreatedRepresentations, Is.EqualTo(1));
                trial.Return(reused);
                Assert.That(trial.Pool.LeasedCount, Is.Zero);
                Assert.That(trial.Assembler.ActiveLeaseCount, Is.Zero);
                Assert.That(trial.Prefab.GetComponent<Animator>().avatar, Is.SameAs(trial.OriginalAvatar));
            }
        }

        [Test]
        public void NonHumanoidOverrideFailureRestoresStateBeforeCallerReturnsView()
        {
            var genericRoot = new GameObject("BenchmarkNonHumanoidOverrideFixture");
            var invalid = AvatarBuilder.BuildGenericAvatar(genericRoot, "");
            try
            {
                Assert.That(invalid, Is.Not.Null);
                Assert.That(invalid.isHuman, Is.False);
                using (var trial = new Trial())
                {
                    var view = trial.Rent();
                    var animator = view.GetComponentsInChildren<Animator>(true).Single();
                    var error = Assert.Throws<TargetInvocationException>(() => Animate(view, trial.Prefab, trial.Walking, invalid));
                    Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
                    Assert.That(error.InnerException.Message, Does.Contain("candidate Avatar"));
                    AssertRestoredAndReturn(trial, view, animator);
                }
            }
            finally { Object.DestroyImmediate(invalid); Object.DestroyImmediate(genericRoot); }
        }

        [Test]
        public void InvalidClipAfterCandidateWasAppliedRestoresOriginalAvatarOnError()
        {
            var invalidClip = new AnimationClip { name = "NonHumanoidBenchmarkFailureFixture" };
            try
            {
                using (var trial = new Trial())
                {
                    var view = trial.Rent();
                    var animator = view.GetComponentsInChildren<Animator>(true).Single();
                    var error = Assert.Throws<TargetInvocationException>(() => Animate(view, trial.Prefab, invalidClip, Candidate()));
                    Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
                    Assert.That(error.InnerException.Message, Does.Contain("Humanoid motion clip"));
                    AssertRestoredAndReturn(trial, view, animator);
                }
            }
            finally { Object.DestroyImmediate(invalidClip); }
        }

        private static void AssertRestoredAndReturn(Trial trial, VisualSoldier3D view, Animator animator)
        {
            Assert.That(animator.avatar, Is.SameAs(trial.OriginalAvatar));
            Assert.That(animator.runtimeAnimatorController, Is.SameAs(trial.OriginalController));
            trial.Return(view); // Run's finally owns the view lease, Animate owns only motion resources.
            Assert.That(trial.Pool.LeasedCount, Is.Zero);
            Assert.That(trial.Assembler.ActiveLeaseCount, Is.Zero);
            var reused = trial.Rent();
            Assert.That(reused.GetComponentsInChildren<Animator>(true).Single().avatar, Is.SameAs(trial.OriginalAvatar));
            trial.Return(reused);
        }
        private static Avatar Candidate()
        {
            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(MeshyHumanoidCalibration.TargetAvatarPath);
            Assert.That(avatar, Is.Not.Null, "Missing actual candidate is a failure, not a skip.");
            Assert.That(avatar.isHuman && avatar.isValid, Is.True);
            return avatar;
        }
        private static object Animate(VisualSoldier3D view, GameObject source, AnimationClip clip, Avatar candidate)
        {
            var method = typeof(MeshyHasanPilotBenchmark).GetMethod("Animate", PrivateStatic);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(null, new object[] { view, source, clip, .25f, candidate });
        }
        private static void Stop(object motion)
        {
            var method = typeof(MeshyHasanPilotBenchmark).GetMethod("StopMotion", PrivateStatic, null, new[] { motion.GetType() }, null);
            Assert.That(method, Is.Not.Null);
            method.Invoke(null, new[] { motion });
        }
        private static T Field<T>(object motion, string name) => (T)motion.GetType().GetField(name).GetValue(motion);

        private sealed class Trial : IDisposable
        {
            public readonly GameObject Host, Prefab;
            public readonly VisualSoldier3DAssembler Assembler;
            public readonly VisualSoldierPool Pool;
            public readonly Avatar OriginalAvatar;
            public readonly RuntimeAnimatorController OriginalController;
            public readonly AnimationClip Walking;
            private readonly VisualCatalogAsset catalog;
            private readonly HashSet<VisualSoldier3D> leased = new HashSet<VisualSoldier3D>();
            private int sequence;

            public Trial()
            {
                Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MeshyHasanPilotCatalog.PrefabPath);
                Assert.That(Prefab, Is.Not.Null);
                OriginalAvatar = Prefab.GetComponent<Animator>().avatar;
                OriginalController = Prefab.GetComponent<Animator>().runtimeAnimatorController;
                Walking = AssetDatabase.LoadAllAssetsAtPath(MeshyHasanPilotBuild.SourcePath).OfType<AnimationClip>().Single(c => c.name == "Walking");
                Host = new GameObject("MeshyBenchmarkAvatarLifecycleFixture");
                Assembler = Host.AddComponent<VisualSoldier3DAssembler>(); Pool = Host.AddComponent<VisualSoldierPool>();
                var template = new GameObject("BenchmarkEmptyViewTemplate").AddComponent<VisualSoldier3D>();
                template.transform.SetParent(Host.transform, false); template.gameObject.SetActive(false); Pool.Configure(template, 2);
                catalog = MeshyHasanPilotCatalog.Create(Prefab);
            }
            public VisualSoldier3D Rent()
            {
                var view = Pool.Rent(); leased.Add(view);
                var index = sequence++;
                var troop = new TroopDefinition(TroopDefinitionId.Create("meshy-benchmark-avatar-test"), "Benchmark lifecycle fixture",
                    UnitClassId.Create("pilot-test-class"), CombatRoleId.Create("pilot-test-role"), MountContext.InfantryOnly,
                    VisualProfileId.Create(MeshyHasanPilotCatalog.ProfileId));
                var soldier = new SoldierInstance(SoldierId.Create("benchmark-avatar-" + index), UnitGroupId.Create("benchmark-avatar-group"), troop.Id,
                    new SoldierRecruitmentProvenance(RecruitmentSourceId.Create("benchmark-avatar-source"), RecruitmentRecordId.Create("benchmark-avatar-record-" + index)),
                    new SoldierLoadout(Array.Empty<WeaponSlotAssignment>()), troop.DefaultCombatRoleId);
                MeshyHasanPilotCatalog.Assemble(Assembler, catalog, view, soldier, troop, new Dictionary<EquipmentInstanceId, EquipmentInstance>());
                return view;
            }
            public void Return(VisualSoldier3D view) { Pool.Return(view); leased.Remove(view); }
            public void Dispose()
            {
                foreach (var view in leased.ToArray()) Return(view);
                Assembler.DisposeCache(); Object.DestroyImmediate(Host); Object.DestroyImmediate(catalog);
            }
        }
    }
}
