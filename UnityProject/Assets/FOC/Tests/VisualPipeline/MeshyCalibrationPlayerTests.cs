using System;
using System.Linq;
using System.Reflection;
using FOC.Editor.Visuals;
using FOC.Presentation.Visuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace FOC.Tests.VisualPipeline
{
    /// <summary>Candidate-Avatar lease lifecycle only; not visual or contact acceptance.</summary>
    public sealed class MeshyCalibrationPlayerTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void InvalidAvatarOverrideReturnsBothLeasesAndRestoresCachedOriginalAvatar()
        {
            var genericRoot = new GameObject("InvalidHumanoidCandidateFixture");
            var invalid = AvatarBuilder.BuildGenericAvatar(genericRoot, "");
            try
            {
                Assert.That(invalid, Is.Not.Null);
                Assert.That(invalid.isHuman, Is.False, "A Generic Avatar must not pass the candidate Humanoid gate.");
                using (var trial = new Trial())
                {
                    var error = Assert.Throws<TargetInvocationException>(() => trial.Create(invalid));
                    Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
                    Assert.That(error.InnerException.Message, Does.Contain("Invalid calibration Avatar candidate"));
                    Assert.That(trial.Pool.LeasedCount, Is.Zero);
                    Assert.That(trial.Assembler.ActiveLeaseCount, Is.Zero);
                    Assert.That(trial.Pool.AvailableCount, Is.EqualTo(1));
                    Assert.That(trial.Assembler.PooledInstanceCount, Is.EqualTo(1));
                    var cachedAnimator = trial.Host.GetComponentsInChildren<Animator>(true).Single();
                    Assert.That(cachedAnimator.avatar, Is.SameAs(trial.OriginalAvatar));
                    Assert.That(cachedAnimator.runtimeAnimatorController, Is.SameAs(trial.OriginalController));

                    // A rejected candidate cannot poison the next ordinary pool lease.
                    var recovered = trial.Create(null);
                    Assert.That(ActorField<Animator>(recovered, "animator").avatar, Is.SameAs(trial.OriginalAvatar));
                    Assert.That(trial.Pool.CreatedViewCount, Is.EqualTo(1));
                    trial.Clear();
                    Assert.That(trial.Pool.LeasedCount, Is.Zero);
                    Assert.That(trial.Assembler.ActiveLeaseCount, Is.Zero);
                }
            }
            finally { Object.DestroyImmediate(invalid); Object.DestroyImmediate(genericRoot); }
        }

        [Test]
        public void ClearingCalibratedActorRestoresOriginalAvatarAndControllerBeforePoolReuse()
        {
            var calibrated = AssetDatabase.LoadAssetAtPath<Avatar>(MeshyHumanoidCalibration.TargetAvatarPath);
            Assert.That(calibrated, Is.Not.Null, "A missing real calibration candidate must fail, not skip.");
            Assert.That(calibrated.isHuman && calibrated.isValid, Is.True);
            using (var trial = new Trial())
            {
                Assert.That(calibrated, Is.Not.SameAs(trial.OriginalAvatar));
                var actor = trial.Create(calibrated);
                var animator = ActorField<Animator>(actor, "animator");
                var view = ActorField<VisualSoldier3D>(actor, "view");
                var representation = view.ActiveRepresentationRoot;
                var graph = ActorField<PlayableGraph>(actor, "graph");
                Assert.That(animator.avatar, Is.SameAs(calibrated));
                Assert.That(animator.runtimeAnimatorController, Is.Null);
                Assert.That(graph.IsValid(), Is.True);
                Assert.That(trial.Pool.LeasedCount, Is.EqualTo(1));
                Assert.That(trial.Assembler.ActiveLeaseCount, Is.EqualTo(1));

                trial.Clear();
                Assert.That(graph.IsValid(), Is.False, "ClearActors must release the manual animation graph.");
                Assert.That(animator.avatar, Is.SameAs(trial.OriginalAvatar));
                Assert.That(animator.runtimeAnimatorController, Is.SameAs(trial.OriginalController));
                Assert.That(trial.Pool.LeasedCount, Is.Zero);
                Assert.That(trial.Assembler.ActiveLeaseCount, Is.Zero);
                Assert.That(trial.Assembler.PooledInstanceCount, Is.EqualTo(1));

                var original = trial.Create(null);
                var originalView = ActorField<VisualSoldier3D>(original, "view");
                Assert.That(originalView, Is.SameAs(view));
                Assert.That(originalView.ActiveRepresentationRoot, Is.SameAs(representation));
                Assert.That(ActorField<Animator>(original, "animator").avatar, Is.SameAs(trial.OriginalAvatar));
                Assert.That(trial.Pool.CreatedViewCount, Is.EqualTo(1));
                Assert.That(trial.Assembler.Metrics.CreatedRepresentations, Is.EqualTo(1));
                trial.Clear();
                Assert.That(trial.Pool.LeasedCount, Is.Zero);
                Assert.That(trial.Assembler.ActiveLeaseCount, Is.Zero);
                Assert.That(trial.Prefab.GetComponent<Animator>().avatar, Is.SameAs(trial.OriginalAvatar));
            }
        }

        private static T ActorField<T>(object actor, string name)
        {
            var field = actor.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(actor);
        }

        private sealed class Trial : IDisposable
        {
            public readonly GameObject Host;
            public readonly GameObject Prefab;
            public readonly VisualSoldier3DAssembler Assembler;
            public readonly VisualSoldierPool Pool;
            public readonly Avatar OriginalAvatar;
            public readonly RuntimeAnimatorController OriginalController;
            private readonly MeshyHasanPilotPlayer player;
            private readonly VisualCatalogAsset catalog;
            private readonly AnimationClip walking;

            public Trial()
            {
                Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MeshyHasanPilotCatalog.PrefabPath);
                Assert.That(Prefab, Is.Not.Null);
                OriginalAvatar = Prefab.GetComponent<Animator>().avatar;
                OriginalController = Prefab.GetComponent<Animator>().runtimeAnimatorController;
                walking = AssetDatabase.LoadAllAssetsAtPath(MeshyHasanPilotBuild.SourcePath).OfType<AnimationClip>()
                    .Single(clip => clip.name == "Walking");
                Assert.That(walking.isHumanMotion, Is.True);
                Host = new GameObject("MeshyCalibrationPlayerLifecycleFixture");
                Assembler = Host.AddComponent<VisualSoldier3DAssembler>();
                Pool = Host.AddComponent<VisualSoldierPool>();
                var template = new GameObject("CalibrationEmptyViewTemplate").AddComponent<VisualSoldier3D>();
                template.transform.SetParent(Host.transform, false);
                template.gameObject.SetActive(false);
                Pool.Configure(template, 2);
                catalog = MeshyHasanPilotCatalog.Create(Prefab);
                player = Host.AddComponent<MeshyHasanPilotPlayer>();
                player.enabled = false; // Never start the review/capture coroutine in EditMode.
                player.characterPrefab = Prefab;
                SetField("assembler", Assembler); SetField("pool", Pool); SetField("pilotCatalog", catalog);
            }

            public object Create(Avatar candidate) => Method("CreateActor").Invoke(player, new object[] { walking, Vector3.zero, candidate });
            public void Clear() => Method("ClearActors").Invoke(player, null);
            private static MethodInfo Method(string name)
            {
                var method = typeof(MeshyHasanPilotPlayer).GetMethod(name, PrivateInstance);
                Assert.That(method, Is.Not.Null, name);
                return method;
            }
            private void SetField(string name, object value)
            {
                var field = typeof(MeshyHasanPilotPlayer).GetField(name, PrivateInstance);
                Assert.That(field, Is.Not.Null, name);
                field.SetValue(player, value);
            }

            public void Dispose()
            {
                try { Clear(); }
                finally
                {
                    // Also clean failed pre-registration leases so a failed
                    // regression does not contaminate subsequent EditMode tests.
                    foreach (var view in Host.GetComponentsInChildren<VisualSoldier3D>(true)) view.ReleaseVisual();
                    Assembler.DisposeCache();
                    // Player.OnDestroy uses runtime Destroy for its catalog;
                    // this fixture owns and destroys it immediately in EditMode.
                    SetField("assembler", null); SetField("pilotCatalog", null);
                    Object.DestroyImmediate(Host);
                    Object.DestroyImmediate(catalog);
                }
            }
        }
    }
}
