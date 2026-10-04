using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FOC.Editor.Visuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace FOC.Tests.VisualPipeline
{
    /// <summary>Foreign motion import integrity only; not retarget, contact or art acceptance.</summary>
    public sealed class MeshyMotionLibraryIntakeTests
    {
        private const string SourcePath = "Assets/FOC/ArtSource/HistoricalSlice/MotionLibrary/UAL1_Standard.fbx";
        private const string SourceHash = "21b32d912da3cb93426d974fb945e86f5b2e86970acd2ce89905e0fbf9f1dcc2";
        private const string MeshyHash = "356dd92317ec1787f48aabe4e85157d31dfec2dd029168c856c5ffd6a125fadf";
        private static readonly string[] ClipNames =
        {
            "A_TPose", "Crouch_Fwd_Loop", "Crouch_Idle_Loop", "Dance_Loop", "Death01", "Driving_Loop",
            "Fixing_Kneeling", "Hit_Chest", "Hit_Head", "Idle_Loop", "Idle_Talking_Loop", "Idle_Torch_Loop",
            "Interact", "Jog_Fwd_Loop", "Jump_Land", "Jump_Loop", "Jump_Start", "PickUp_Table",
            "Pistol_Aim_Down", "Pistol_Aim_Neutral", "Pistol_Aim_Up", "Pistol_Idle_Loop", "Pistol_Reload",
            "Pistol_Shoot", "Punch_Cross", "Punch_Jab", "Push_Loop", "Roll", "Sitting_Enter", "Sitting_Exit",
            "Sitting_Idle_Loop", "Sitting_Talking_Loop", "Spell_Simple_Enter", "Spell_Simple_Exit",
            "Spell_Simple_Idle_Loop", "Spell_Simple_Shoot", "Sprint_Loop", "Swim_Fwd_Loop", "Swim_Idle_Loop",
            "Sword_Attack", "Sword_Idle", "Walk_Formal_Loop", "Walk_Loop"
        };

        [Test]
        public void SelectedFreeStandardMotionExportMatchesPinnedBytesAndLicenseLedger()
        {
            Assert.That(File.Exists(SourcePath), Is.True, "Missing licensed source must fail, never skip.");
            Assert.That(Hash(SourcePath), Is.EqualTo(SourceHash));
            Assert.That(new FileInfo(SourcePath).Length, Is.EqualTo(23754684L));
            var ledgerPath = Path.GetDirectoryName(SourcePath) + "/PROVENANCE.json";
            Assert.That(File.Exists(ledgerPath), Is.True);
            var ledger = JsonUtility.FromJson<Provenance>(File.ReadAllText(ledgerPath));
            Assert.That(ledger, Is.Not.Null);
            Assert.That(ledger.license, Is.EqualTo("CC0-1.0"));
            Assert.That(ledger.selectedLocalFile, Is.EqualTo(SourcePath));
            Assert.That(ledger.selectedFileSha256, Is.EqualTo(SourceHash));
            Assert.That(ledger.selectedArchiveFile, Is.EqualTo("Universal Animation Library[Standard]/Unity/UAL1_Standard.fbx"));
        }

        [Test]
        public void ForeignSourceKeepsReadableUnoptimizedAxisBakedUncompressedImport()
        {
            var importer = AssetImporter.GetAtPath(SourcePath) as ModelImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Human));
            Assert.That(importer.avatarSetup, Is.EqualTo(ModelImporterAvatarSetup.CreateFromThisModel));
            Assert.That(importer.importAnimation, Is.True);
            Assert.That(importer.optimizeGameObjects, Is.False,
                "Canonical FOC optimization stripped this foreign rig's live mapped bones in the original failure.");
            Assert.That(importer.isReadable, Is.True, "Source skin measurements need actual readable geometry.");
            Assert.That(importer.preserveHierarchy, Is.True);
            Assert.That(importer.animationCompression, Is.EqualTo(ModelImporterAnimationCompression.Off));
            Assert.That(importer.meshCompression, Is.EqualTo(ModelImporterMeshCompression.Off));
            Assert.That(importer.bakeAxisConversion, Is.True, "The selected upstream Unity export requires baked axes.");
        }

        [Test]
        public void MotionDonorOwnsItsValidHumanAvatarInsteadOfCopyingTargetAvatar()
        {
            var model = Source();
            var animator = model.GetComponent<Animator>();
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.avatar, Is.Not.Null);
            Assert.That(animator.avatar.isValid, Is.True);
            Assert.That(animator.avatar.isHuman, Is.True);
            Assert.That(AssetDatabase.GetAssetPath(animator.avatar), Is.EqualTo(SourcePath));
            Assert.That(animator.avatar, Is.Not.SameAs(MeshyPrefab().GetComponent<Animator>().avatar));
        }

        [Test]
        public void ActualFbxContainsExactlyTheFortyThreeExpectedHumanoidClips()
        {
            var clips = Clips(SourcePath);
            Assert.That(clips.Length, Is.EqualTo(43), "Check actual imported FBX clips, not upstream marketing or GLB counts.");
            CollectionAssert.AreEquivalent(ClipNames, clips.Select(c => c.name.Substring(c.name.LastIndexOf('|') + 1)).ToArray());
            foreach (var clip in clips)
            {
                Assert.That(clip.isHumanMotion, Is.True, clip.name);
                Assert.That(clip.legacy, Is.False, clip.name);
                Assert.That(float.IsNaN(clip.length) || float.IsInfinity(clip.length), Is.False, clip.name);
                Assert.That(clip.length, Is.GreaterThan(0f), clip.name);
            }
        }

        [Test]
        public void EveryMappedHumanoidBoneRetainsAnAccessibleLiveTransform()
        {
            var instance = Object.Instantiate(Source());
            try
            {
                instance.SetActive(true);
                var animator = instance.GetComponent<Animator>();
                Assert.That(animator, Is.Not.Null);
                Assert.That(animator.avatar, Is.Not.Null);
                Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True);
                animator.runtimeAnimatorController = null;
                animator.enabled = true;
                animator.Rebind();
                animator.Update(0f);
                var hierarchy = instance.GetComponentsInChildren<Transform>(true);
                var mapping = animator.avatar.humanDescription.human;
                Assert.That(mapping.Length, Is.GreaterThanOrEqualTo(15));
                foreach (var entry in mapping)
                {
                    var index = Array.IndexOf(HumanTrait.BoneName, entry.humanName);
                    Assert.That(index, Is.GreaterThanOrEqualTo(0), entry.humanName);
                    var matches = hierarchy.Where(t => t.name == entry.boneName).ToArray();
                    Assert.That(matches.Length, Is.EqualTo(1), entry.humanName + " -> " + entry.boneName);
                    var bone = animator.GetBoneTransform((HumanBodyBones)index);
                    Assert.That(bone, Is.Not.Null, "Optimizer must not erase " + entry.humanName);
                    Assert.That(bone, Is.SameAs(matches[0]), entry.humanName);
                    Assert.That(bone.IsChildOf(instance.transform), Is.True, entry.humanName);
                }
                foreach (var required in new[] { HumanBodyBones.Hips, HumanBodyBones.Head,
                    HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
                    HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
                    HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
                    HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot })
                    Assert.That(animator.GetBoneTransform(required), Is.Not.Null, required.ToString());
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void AxisBakedSourceActuallyPlaysUprightWithoutStaleReferencePose()
        {
            var instance = Object.Instantiate(Source());
            PlayableGraph graph = default;
            var baked = new Mesh();
            try
            {
                instance.SetActive(true);
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var animator = instance.GetComponent<Animator>();
                animator.runtimeAnimatorController = null;
                animator.enabled = true;
                animator.applyRootMotion = false;
                animator.Rebind();
                var skin = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .OrderByDescending(s => s.sharedMesh.triangles.Length).First();
                foreach (var name in new[] { "Idle_Loop", "Sword_Attack" })
                {
                    var clip = MeshyMotionLibraryIntake.Clip(name);
                    graph = PlayableGraph.Create("Import reference regression");
                    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    var playable = AnimationClipPlayable.Create(graph, clip);
                    playable.SetApplyFootIK(false);
                    AnimationPlayableOutput.Create(graph, "Source reference", animator).SetSourcePlayable(playable);
                    graph.Play();
                    for (var i = 0; i <= 12; i++)
                    {
                        playable.SetTime(clip.length * i / 12d);
                        graph.Evaluate(0);
                        var hips = animator.GetBoneTransform(HumanBodyBones.Hips).position;
                        var head = animator.GetBoneTransform(HumanBodyBones.Head).position;
                        Assert.That(head.y - hips.y, Is.GreaterThan(.2f), name + " inverted reference pose");
                        skin.BakeMesh(baked);
                        var minimum = baked.vertices.Min(v => skin.transform.TransformPoint(v).y);
                        Assert.That(minimum, Is.InRange(-.03f, .10f), name + " stale axis/reference contact");
                        Assert.That(instance.transform.position.sqrMagnitude, Is.LessThan(1e-8f));
                    }
                    graph.Destroy();
                }
            }
            finally
            {
                if (graph.IsValid()) graph.Destroy();
                Object.DestroyImmediate(baked);
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void MotionIntakeDoesNotReplaceProtectedMeshySourceAvatarClipsOrSkinRig()
        {
            var path = MeshyHasanPilotBuild.SourcePath;
            Assert.That(Hash(path), Is.EqualTo(MeshyHash), "The licensed donor must not overwrite Meshy geometry or original motion.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(source, Is.Not.Null);
            var original = source.GetComponent<Animator>().avatar;
            Assert.That(original.isValid && original.isHuman, Is.True);
            Assert.That(AssetDatabase.GetAssetPath(original), Is.EqualTo(path));
            var prefab = MeshyPrefab();
            Assert.That(prefab.GetComponent<Animator>().avatar, Is.SameAs(original), "Candidate Avatars belong on isolated review clones.");
            CollectionAssert.AreEquivalent(new[] { "Walking", "Running" }, Clips(path).Select(c => c.name).ToArray());
            Assert.That(Clips(path).All(c => c.isHumanMotion), Is.True);
            var skins = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.That(skins.Length, Is.EqualTo(3));
            foreach (var skin in skins)
            {
                Assert.That(skin.bones.Length, Is.EqualTo(23));
                Assert.That(skin.sharedMesh.bindposes.Length, Is.EqualTo(23));
            }
            Assert.That(skins.SelectMany(s => s.bones).Distinct().Count(), Is.EqualTo(23));
        }

        private static GameObject Source()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            Assert.That(model, Is.Not.Null, "The real licensed FBX must be imported before these tests run.");
            return model;
        }

        private static GameObject MeshyPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MeshyHasanPilotPipeline.PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            return prefab;
        }

        private static AnimationClip[] Clips(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();

        private static string Hash(string path)
        {
            using (var algorithm = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        [Serializable]
        private sealed class Provenance
        {
            public string license = "";
            public string selectedLocalFile = "";
            public string selectedFileSha256 = "";
            public string selectedArchiveFile = "";
        }
    }
}
