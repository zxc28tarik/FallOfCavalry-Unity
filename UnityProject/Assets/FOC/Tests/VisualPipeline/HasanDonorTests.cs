using System.IO;
using System.Linq;
using FOC.Editor.Visuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FOC.Tests.VisualPipeline
{
    public sealed class HasanDonorTests
    {
        private const string Root="Assets/FOC/ArtSource/HistoricalSlice/HasanDonor";
        [Test]
        public void HasanDraftUsesUnchangedCanonicalRigAndThreeValidLods()
        {
            var source=Read(Root+"/CHR_HasanAga_DonorDraft.focmesh.json");
            var original=Read("Assets/FOC/ArtSource/HistoricalSlice/Characters/BODY_OttomanMale_Standard.focmesh.json");
            HistoricalArtCandidatePipeline.ValidateSource(source);
            Assert.That(source.status,Is.EqualTo("Draft"));
            Assert.That(JsonUtility.ToJson(source.bones[0]),Is.EqualTo(JsonUtility.ToJson(original.bones[0])));
            Assert.That(source.bones.Length,Is.EqualTo(original.bones.Length));
            for(var i=0;i<source.bones.Length;i++)Assert.That(JsonUtility.ToJson(source.bones[i]),Is.EqualTo(JsonUtility.ToJson(original.bones[i])));
            var counts=source.lods.Select(l=>l.parts.Sum(p=>p.triangles.Length)).ToArray();
            Assert.That(counts[0],Is.GreaterThan(counts[1]));Assert.That(counts[1],Is.GreaterThan(counts[2]));
        }
        [Test]
        public void OnlyHasanExistsInNewDraftFolderAndCatalogRemainsUnpromoted()
        {
            CollectionAssert.AreEqual(new[]{"CHR_HasanAga_DonorDraft.focmesh.json"},Directory.GetFiles(Root,"CHR_*.json").Select(Path.GetFileName).ToArray());
            Assert.That(File.ReadAllText("Assets/FOC/Content/Resources/FOC/Visuals/FOC_VisualCatalog.asset"),Does.Not.Contain("HasanAga_DonorDraft"));
        }
        [Test]
        public void AdaptedGarmentsReferencePinnedMakeHumanSourcesNotProceduralOrQuaternius()
        {
            var files=Directory.GetFiles(Root,"CLTH_*.json");Assert.That(files.Length,Is.EqualTo(4));
            foreach(var file in files)
            {
                var source=Read(file);HistoricalArtCandidatePipeline.ValidateSource(source);
                Assert.That(source.source,Does.Contain("Upstream/ClothingDonors/clothes/"));
                Assert.That(source.source,Does.Not.Contain("Quaternius"));Assert.That(source.generator,Does.Contain("adapt_hasan_donors.py"));
                Assert.That(source.license,Does.Contain("CC0"));Assert.That(source.sourceSha256.Length,Is.EqualTo(64));
            }
        }
        [Test]
        public void HasanPrefabHasOneRendererPerLodAndARealDiagnosticController()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(HistoricalArtCandidatePipeline.CharacterRoot+"/CHR_HasanAga_DonorDraft.prefab");
            var lods=prefab.GetComponent<LODGroup>().GetLODs();Assert.That(lods.Length,Is.EqualTo(3));
            foreach(var lod in lods)Assert.That(lod.renderers.Length,Is.EqualTo(1));
            var animator=prefab.GetComponent<Animator>();Assert.That(animator.avatar.isHuman&&animator.avatar.isValid,Is.True);
            Assert.That(animator.runtimeAnimatorController,Is.Not.Null);
            Assert.That(animator.runtimeAnimatorController.animationClips.Length,Is.EqualTo(8));
        }
        [TestCaseSource(typeof(HasanDonorPipeline),nameof(HasanDonorPipeline.Motions))]
        public void MotionClipAnimatesNonPelvisJointsAndProducesFiniteSkin(string motion)
        {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(HasanDonorPipeline.Folder+"/ANM_HasanDonor_"+motion+".anim");
            Assert.That(clip,Is.Not.Null);
            var moving=AnimationUtility.GetCurveBindings(clip).Where(b=>b.propertyName.Contains("Rotation")&&
                !b.path.EndsWith("/Pelvis")&&AnimationUtility.GetEditorCurve(clip,b).keys.Select(k=>k.value).Distinct().Count()>2).ToArray();
            Assert.That(moving.Length,Is.GreaterThan(0),"Pelvis-only fixture is not a motion test.");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(HistoricalArtCandidatePipeline.CharacterRoot+"/CHR_HasanAga_DonorDraft.prefab");
            var instance=Object.Instantiate(prefab);var mesh=new Mesh();
            try
            {
                var animator=PrepareGenericReview(instance);
                foreach(var phase in new[]{0f,.25f,.5f,.75f})
                {
                    animator.Play(motion,0,phase);animator.Update(0);
                    var skin=instance.GetComponentsInChildren<SkinnedMeshRenderer>().Single(s=>s.name=="LOD0");skin.BakeMesh(mesh);
                    Assert.That(mesh.vertices.All(v=>!float.IsNaN(v.x)&&!float.IsInfinity(v.x)&&!float.IsNaN(v.y)&&!float.IsInfinity(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.z)),Is.True);
                    Assert.That(mesh.bounds.size.magnitude,Is.LessThan(5f),"Exploded mesh; numeric check is not clipping/visual acceptance.");
                }
            }
            finally{Object.DestroyImmediate(mesh);Object.DestroyImmediate(instance);}
        }
        [TestCase("Walk")][TestCase("Run")]
        public void GaitStanceFootIsReachableInsteadOfFloatingFromIkClamp(string motion)
        {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(HasanDonorPipeline.Folder+"/ANM_HasanDonor_"+motion+".anim");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(HistoricalArtCandidatePipeline.CharacterRoot+"/CHR_HasanAga_DonorDraft.prefab");
            var instance=Object.Instantiate(prefab);
            try
            {
                var animator=PrepareGenericReview(instance);
                animator.Play(motion,0,.25f);animator.Update(0);
                var foot=instance.GetComponentsInChildren<Transform>().Single(t=>t.name=="Foot_R");
                Assert.That(foot.position.y,Is.EqualTo(.073f).Within(.005f),"Stance foot target must be within leg reach; this does not accept rendered gait.");
            }
            finally{Object.DestroyImmediate(instance);}
        }
        private static Animator PrepareGenericReview(GameObject instance)
        {
            // Match the real review player. Transform curves are not humanoid
            // muscle clips; SampleAnimation through the human avatar drops them.
            var animator=instance.GetComponent<Animator>();
            animator.avatar=AvatarBuilder.BuildGenericAvatar(instance,"Root");
            animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();animator.Update(0);
            return animator;
        }
        private static HistoricalArtCandidatePipeline.SourceAsset Read(string path)=>JsonUtility.FromJson<HistoricalArtCandidatePipeline.SourceAsset>(File.ReadAllText(path));
    }
}
