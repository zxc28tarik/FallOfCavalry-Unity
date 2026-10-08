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
    /// <summary>Real equipped review lease lifecycle; not visual/motion acceptance.</summary>
    public sealed class MeshyHasanGripReviewLifecycleTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void ClearDetachesAndDisablesWeaponBeforeDestroyAndImmediateCalibratedReuse()
        {
            using (var trial = new Trial())
            {
                var actor = trial.Create(trial.Calibrated);
                var view = ActorField<VisualSoldier3D>(actor, "view");
                var representation = view.ActiveRepresentationRoot;
                var weapon = ActorField<GameObject>(actor, "weaponInstance");
                var animator = ActorField<Animator>(actor, "animator");
                var graph = ActorField<PlayableGraph>(actor, "graph");
                Assert.That(weapon.transform.IsChildOf(animator.GetBoneTransform(HumanBodyBones.RightHand)), Is.True);
                Assert.That(WeaponCount(representation), Is.EqualTo(1));
                var observer = weapon.AddComponent<MeshyGripDestroyObserver>();

                // No frame/yield between clearing and re-renting. The observer
                // catches missing detachment even though EditMode destroys
                // immediately instead of the player's end-of-frame Destroy.
                trial.Clear();
                Assert.That(observer.DestroyObserved, Is.True, "The actual weapon destruction callback must run.");
                Assert.That(observer.ParentAtDestroy, Is.Null, "Detach BEFORE Destroy; deferred player destruction must not retain duplicate rig paths.");
                Assert.That(observer.ActiveAtDestroy, Is.False, "Disable BEFORE Destroy; a pending accessory cannot render after release.");
                Assert.That(graph.IsValid(), Is.False);
                Assert.That(trial.Pool.LeasedCount + trial.Assembler.ActiveLeaseCount, Is.Zero);
                Assert.That(WeaponCount(representation), Is.Zero);

                var next = trial.Create(trial.Calibrated);
                var nextView = ActorField<VisualSoldier3D>(next, "view");
                Assert.That(nextView, Is.SameAs(view));
                Assert.That(nextView.ActiveRepresentationRoot, Is.SameAs(representation));
                Assert.That(ActorField<Animator>(next, "animator").avatar, Is.SameAs(trial.Calibrated));
                Assert.That(WeaponCount(representation), Is.EqualTo(1), "Immediate reuse must have exactly its new weapon, not a previous queued weapon.");
                Assert.That(trial.Pool.CreatedViewCount, Is.EqualTo(1));
                Assert.That(trial.Assembler.Metrics.CreatedRepresentations, Is.EqualTo(1));
                trial.Clear();
                Assert.That(trial.Pool.LeasedCount + trial.Assembler.ActiveLeaseCount, Is.Zero);
            }
        }

        [Test]
        public void FailedAvatarAfterWeaponAttachmentDoesNotPoisonImmediateCalibratedReuse()
        {
            var genericRoot = new GameObject("Invalid equipped-review avatar fixture");
            var invalid = AvatarBuilder.BuildGenericAvatar(genericRoot, "");
            try
            {
                Assert.That(invalid != null && !invalid.isHuman, Is.True);
                using (var trial = new Trial())
                {
                    var error = Assert.Throws<TargetInvocationException>(() => trial.Create(invalid));
                    Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
                    Assert.That(error.InnerException.Message, Does.Contain("Invalid calibration Avatar candidate"));
                    Assert.That(trial.Pool.LeasedCount + trial.Assembler.ActiveLeaseCount, Is.Zero);
                    Assert.That(WeaponCount(trial.Host), Is.Zero);
                    var recovered = trial.Create(trial.Calibrated);
                    var view = ActorField<VisualSoldier3D>(recovered, "view");
                    Assert.That(WeaponCount(view.ActiveRepresentationRoot), Is.EqualTo(1));
                    Assert.That(ActorField<Animator>(recovered, "animator").avatar, Is.SameAs(trial.Calibrated));
                    Assert.That(trial.Pool.CreatedViewCount, Is.EqualTo(1));
                    trial.Clear();
                    Assert.That(trial.Pool.LeasedCount + trial.Assembler.ActiveLeaseCount, Is.Zero);
                }
            }
            finally { Object.DestroyImmediate(invalid); Object.DestroyImmediate(genericRoot); }
        }

        private static int WeaponCount(GameObject root) => root.GetComponentsInChildren<Transform>(true)
            .Count(t => t.name == "WPN_Kilic_01_REVIEW_RightHand");

        private static T ActorField<T>(object actor, string name)
        {
            var field = actor.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(actor);
        }

        private sealed class Trial : IDisposable
        {
            public readonly GameObject Host;
            public readonly VisualSoldier3DAssembler Assembler;
            public readonly VisualSoldierPool Pool;
            public readonly Avatar Calibrated;
            private readonly MeshyHasanPilotPlayer player;
            private readonly VisualCatalogAsset catalog;
            private readonly AnimationClip clip;

            public Trial()
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(MeshyHasanPilotCatalog.PrefabPath);
                var candidate = AssetDatabase.LoadAssetAtPath<GameObject>(MeshyKilicGripCorrectivePipeline.PrefabPath);
                var kilic = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FOC/Presentation/Equipment/Ottoman1648/WPN_Kilic_01.prefab");
                Calibrated = AssetDatabase.LoadAssetAtPath<Avatar>(MeshyHumanoidCalibration.TargetAvatarPath);
                Assert.That(source != null && candidate != null && kilic != null && Calibrated != null, Is.True,
                    "Actual retained/corrected assets are required; no skipped gate.");
                clip = AssetDatabase.LoadAllAssetsAtPath(MeshyHasanPilotBuild.SourcePath).OfType<AnimationClip>()
                    .Single(c => c.name == "Walking");
                Host = new GameObject("Equipped Meshy review lifecycle fixture");
                Assembler = Host.AddComponent<VisualSoldier3DAssembler>();
                Pool = Host.AddComponent<VisualSoldierPool>();
                var template = new GameObject("Empty equipped-review view template").AddComponent<VisualSoldier3D>();
                template.transform.SetParent(Host.transform, false);
                template.gameObject.SetActive(false);
                Pool.Configure(template, 2);
                catalog = MeshyHasanPilotCatalog.Create(candidate);
                player = Host.AddComponent<MeshyHasanPilotPlayer>();
                player.enabled = false;
                player.characterPrefab = source;
                player.gripCandidatePrefab = candidate;
                player.kilicPrefab = kilic;
                SetField("assembler", Assembler);
                SetField("pool", Pool);
                SetField("pilotCatalog", catalog);
            }

            public object Create(Avatar avatar) => Method("CreateActorCore").Invoke(player,
                new object[] { clip, Vector3.zero, avatar, null, true });
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
                    foreach (var view in Host.GetComponentsInChildren<VisualSoldier3D>(true)) view.ReleaseVisual();
                    Assembler.DisposeCache();
                    SetField("assembler", null);
                    SetField("pilotCatalog", null);
                    Object.DestroyImmediate(Host);
                    Object.DestroyImmediate(catalog);
                }
            }
        }
    }

    // ExecuteAlways is deliberate: normal MonoBehaviour callbacks are not a
    // reliable EditMode test signal. This observer never enters a saved prefab.
    [ExecuteAlways]
    public sealed class MeshyGripDestroyObserver : MonoBehaviour
    {
        public bool DestroyObserved { get; private set; }
        public Transform ParentAtDestroy { get; private set; }
        public bool ActiveAtDestroy { get; private set; }

        private void OnDestroy()
        {
            ParentAtDestroy = transform.parent;
            ActiveAtDestroy = gameObject.activeSelf;
            DestroyObserved = true;
        }
    }
}
