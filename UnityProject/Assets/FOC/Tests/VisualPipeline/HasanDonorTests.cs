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
        public void NecklineDoesNotFollowArmsAndCoatHemDoesNotFollowShins()
        {
            var source=Read(Root+"/CLTH_Donor_HasanCoatDonor.focmesh.json");
            var part=source.lods[0].parts[0];var measuredCollar=0;var measuredHem=0;
            for(var vi=0;vi<part.positions.Length/3;vi++)
            {
                var x=part.positions[vi*3];var y=part.positions[vi*3+1];
                var collar=Mathf.Abs(x)<.072f&&y>1.445f;
                var hem=y<.72f;
                if(collar)measuredCollar++;if(hem)measuredHem++;
                for(var j=0;j<4;j++)
                {
                    var bi=vi*4+j;var bone=source.bones[part.boneIndices[bi]].name;var weight=part.boneWeights[bi];
                    if(collar&&(bone.StartsWith("UpperArm_")||bone.StartsWith("LowerArm_")||bone=="Head"))
                        Assert.That(weight,Is.LessThan(.00001f),"Collar must not be pulled by raised arm: "+vi);
                    if(hem&&(bone.StartsWith("LowerLeg_")||bone.StartsWith("Foot_")))
                        Assert.That(weight,Is.LessThan(.00001f),"Above-knee coat hem must not follow shin: "+vi);
                }
            }
            Assert.That(measuredCollar,Is.GreaterThan(10));Assert.That(measuredHem,Is.GreaterThan(30));
        }
        [Test]
        public void SourceValidatorRejectsMalformedCorrectives()
        {
            var source=Read(Root+"/CLTH_Donor_HasanCoatDonor.focmesh.json");
            source.lods[0].parts[0].blendShapes=new[]{new HistoricalArtCandidatePipeline.SourceShape{name="Broken",deltaPositions=new[]{float.NaN},deltaNormals=new float[3]}};
            Assert.Throws<System.InvalidOperationException>(()=>HistoricalArtCandidatePipeline.ValidateSource(source));
        }
        [TestCase(0)][TestCase(1)][TestCase(2)]
        public void DerivedFacingFollowsSamePoseCorrectionsWithoutMovingNeutralBasis(int lod)
        {
            var source=Read(Root+"/CLTH_Donor_HasanCoatDonor.focmesh.json");
            var main=source.lods[lod].parts[0];
            var facing=source.lods[lod].parts.Single(p=>p.material=="HasanBinding");
            var character=Read(Root+"/CHR_HasanAga_DonorDraft.focmesh.json");
            var combined=character.lods[lod].parts.Single(p=>p.material=="HasanBinding");
            Assert.That(source.poseCorrectives.Length,Is.EqualTo(20));
            CollectionAssert.AreEquivalent(main.blendShapes.Select(s=>s.name),facing.blendShapes.Select(s=>s.name));
            Assert.That(JsonUtility.ToJson(facing),Is.EqualTo(JsonUtility.ToJson(combined)));
            foreach(var shape in facing.blendShapes)
            {
                Assert.That(shape.deltaPositions.Length,Is.EqualTo(facing.positions.Length));
                Assert.That(shape.deltaNormals.Length,Is.EqualTo(facing.normals.Length));
                if(shape.name=="Pose_Neutral")
                    Assert.That(shape.deltaPositions.Concat(shape.deltaNormals).All(v=>v==0),Is.True);
            }
            Assert.That(facing.blendShapes.Single(s=>s.name=="Pose_CrouchFull").deltaPositions.Any(v=>Mathf.Abs(v)>.001f),Is.True);
        }
        [TestCase(0)][TestCase(1)][TestCase(2)]
        public void SashFrontUsesOutwardNormalsInsteadOfAnInvisibleBackface(int lod)
        {
            var sash=Read(Root+"/CLTH_Donor_HasanCoatDonor.focmesh.json").lods[lod].parts.Single(p=>p.material=="HasanSash");
            var measured=0;var normalZ=0f;
            for(var i=0;i<sash.positions.Length;i+=3)
                if(sash.positions[i+1]>1.015f&&sash.positions[i+1]<1.095f&&Mathf.Abs(sash.positions[i])<.17f&&sash.positions[i+2]>.02f)
                {measured++;normalZ+=sash.normals[i+2];}
            Assert.That(measured,Is.GreaterThan(10));
            Assert.That(normalZ/measured,Is.GreaterThan(.3f),"Open band winding must point outward; material color cannot fix backface culling.");
        }
        [TestCase(0)][TestCase(1)][TestCase(2)]
        public void NeckBridgeEndsAtExistingHeadBoundaryAndFacesOutwards(int lod)
        {
            var bridge=Read(Root+"/CHR_HasanAga_DonorDraft.focmesh.json").lods[lod].parts[10];
            Assert.That(bridge.material,Is.EqualTo("SkinMature"));
            var score=0f;
            for(var i=0;i<bridge.positions.Length;i+=3)
            {
                Assert.That(bridge.positions[i+1],Is.LessThanOrEqualTo(1.49451f),"Do not duplicate the mature head's existing neck surface.");
                score+=bridge.positions[i]*bridge.normals[i]+(bridge.positions[i+2]-.02f)*bridge.normals[i+2];
            }
            Assert.That(bridge.triangles.Length,Is.GreaterThan(30));
            Assert.That(score/(bridge.positions.Length/3),Is.GreaterThan(.01f),"Open clipped neck band must not face inward.");
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
                    var correctives=instance.GetComponent<FOC.Presentation.Visuals.HasanGarmentPoseCorrectives>();
                    Assert.That(correctives,Is.Not.Null,"Real donor skin must exercise the runtime corrective driver, not only its uncorrected basis.");
                    correctives.ApplyNow();
                    foreach(var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        var active=0;var total=0f;
                        for(var shape=0;shape<renderer.sharedMesh.blendShapeCount;shape++)
                        {
                            if(!renderer.sharedMesh.GetBlendShapeName(shape).StartsWith("Pose_",System.StringComparison.Ordinal))continue;
                            var weight=renderer.GetBlendShapeWeight(shape);
                            Assert.That(float.IsNaN(weight)||float.IsInfinity(weight),Is.False);
                            Assert.That(weight,Is.InRange(0f,100f));
                            if(weight>.00001f)active++;
                            total+=weight;
                        }
                        Assert.That(active,Is.InRange(1,3),"Only the nearest three garment poses may be active per LOD.");
                        Assert.That(total,Is.EqualTo(100f).Within(.001f));
                    }
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
