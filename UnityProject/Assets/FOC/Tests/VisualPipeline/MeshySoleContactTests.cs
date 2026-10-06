using System;
using System.Linq;
using FOC.Editor.Visuals;
using FOC.Presentation.Visuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object=UnityEngine.Object;

namespace FOC.Tests.VisualPipeline
{
    public sealed class MeshySoleContactTests
    {
        [TestCase("Walking")]
        [TestCase("Running")]
        public void MeasuredContactKeepsTheRealSkinAboveFloorWithoutMovingActorOrChangingSource(string clipName)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(MeshyHasanPilotPipeline.PrefabPath);
            var before=MeshyLod2Derivation.Hash(MeshyHasanPilotPipeline.ModelPath);
            var instance=Object.Instantiate(prefab);var animator=instance.GetComponent<Animator>();
            var graph=PlayableGraph.Create("Contact regression");var bake=new Mesh();
            try
            {
                animator.enabled=false;animator.runtimeAnimatorController=null;
                MeshyHasanPilotPlayer.RestoreSourceBindPose(prefab,instance);animator.Rebind();animator.enabled=true;
                var clip=AssetDatabase.LoadAllAssetsAtPath(MeshyHasanPilotBuild.SourcePath).OfType<AnimationClip>()
                    .Single(c=>c.name==clipName);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(true);
                AnimationPlayableOutput.Create(graph,"Actual Humanoid skin",animator).SetSourcePlayable(playable);graph.Play();
                var contact=MeshyPilotContactLookup.Create(prefab,animator,clip,graph,playable,0f);
                Assert.That(MeshyPilotContactLookup.Create(prefab,animator,clip,graph,playable,0f).MaximumCorrectionMeters,Is.EqualTo(contact.MaximumCorrectionMeters));
                var skin=instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s=>s.sharedMesh.triangles.Length/3==9586);
                var root=instance.transform.position;
                for(var i=0;i<=480;i++)
                {
                    playable.SetTime(clip.length*i/480f);graph.Evaluate(0);
                    var leftHand=animator.GetBoneTransform(HumanBodyBones.LeftHand).position;
                    var rightHand=animator.GetBoneTransform(HumanBodyBones.RightHand).position;
                    contact.Apply(i/480f);skin.BakeMesh(bake);
                    Assert.That(Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.LeftHand).position,leftHand),Is.LessThan(.000001f));
                    Assert.That(Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.RightHand).position,rightHand),Is.LessThan(.000001f));
                    var minimum=bake.vertices.Min(p=>skin.transform.TransformPoint(p).y);
                    Assert.That(minimum,Is.GreaterThanOrEqualTo(-.002f),"actual baked skin at phase "+i/480f);
                    Assert.That(Vector3.Distance(instance.transform.position,root),Is.LessThan(.00001f));
                }
                Assert.That(contact.MaximumCorrectionMeters,Is.LessThan(.10f));
                Assert.That(MeshyPilotContactLookup.CachedProfileCount,Is.LessThanOrEqualTo(8));
                Assert.That(MeshyLod2Derivation.Hash(MeshyHasanPilotPipeline.ModelPath),Is.EqualTo(before));
                Assert.Throws<ArgumentException>(()=>contact.Apply(float.NaN));
                instance.transform.position+=Vector3.up*.01f;
                Assert.Throws<InvalidOperationException>(()=>contact.Apply(.5f));
                instance.transform.position=root;
                instance.transform.localScale*=1.01f;
                Assert.Throws<InvalidOperationException>(()=>contact.Apply(.5f));
                instance.transform.localScale/=1.01f;
                instance.transform.rotation=Quaternion.Euler(0,5,0);
                Assert.Throws<InvalidOperationException>(()=>contact.Apply(.5f));
                instance.transform.rotation=Quaternion.Euler(5,0,0);
                Assert.Throws<InvalidOperationException>(()=>contact.Apply(.5f));
            }
            finally{graph.Destroy();Object.DestroyImmediate(bake);Object.DestroyImmediate(instance);}
        }
    }
}
