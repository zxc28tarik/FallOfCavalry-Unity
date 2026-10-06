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
                var contact=new MeshyPilotSoleContact(prefab,animator);
                var skin=instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s=>s.sharedMesh.triangles.Length/3==9586);
                var root=instance.transform.position;
                for(var i=0;i<=120;i++)
                {
                    playable.SetTime(clip.length*i/120f);graph.Evaluate(0);contact.Apply(0f);skin.BakeMesh(bake);
                    var minimum=bake.vertices.Min(p=>skin.transform.TransformPoint(p).y);
                    Assert.That(minimum,Is.GreaterThanOrEqualTo(-.002f),"actual baked skin at phase "+i/120f);
                    Assert.That(Vector3.Distance(instance.transform.position,root),Is.LessThan(.00001f));
                }
                Assert.That(contact.MaximumCorrectionMeters,Is.LessThan(.10f));
                Assert.That(MeshyLod2Derivation.Hash(MeshyHasanPilotPipeline.ModelPath),Is.EqualTo(before));
            }
            finally{graph.Destroy();Object.DestroyImmediate(bake);Object.DestroyImmediate(instance);}
        }
    }
}
