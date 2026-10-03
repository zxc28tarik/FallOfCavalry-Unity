using System;
using System.Linq;
using FOC.Editor.Visuals;
using FOC.Presentation.Visuals;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace FOC.Tests.VisualPipeline
{
    /// <summary>Measured diagnostic regressions, never a rendered-art acceptance gate.</summary>
    public sealed class HasanDonorMotionRepairTests
    {
        [TestCase("Walk")]
        [TestCase("Run")]
        public void GaitHasPlantedHalfCycleInsteadOfIkClampedFloatingFeet(string motion)
        {
            WithReview((instance,animator)=>
            {
                foreach(var phase in new[]{0f,.125f,.25f,.375f,.5f,.625f,.75f,.875f})
                {
                    Sample(animator,motion,phase);
                    var side=phase<.5f?"R":"L";
                    var foot=Bone(instance,"Foot_"+side);
                    Assert.That(foot.position.y,Is.EqualTo(.073f).Within(.005f),motion+" stance phase "+phase);
                }
            });
        }

        [TestCase("Walk")]
        [TestCase("Run")]
        public void GaitElbowsStayBesideTorsoRatherThanShruggingAboveShoulders(string motion)
        {
            WithReview((instance,animator)=>
            {
                foreach(var phase in new[]{0f,.125f,.25f,.375f,.5f,.625f,.75f,.875f})
                {
                    Sample(animator,motion,phase);
                    foreach(var side in new[]{"L","R"})
                    {
                        var shoulder=Bone(instance,"UpperArm_"+side).position;
                        var elbow=Bone(instance,"LowerArm_"+side).position;
                        Assert.That(elbow.y,Is.LessThan(shoulder.y+.015f),motion+" elbow raised above shoulder at "+phase);
                        Assert.That(Mathf.Abs(elbow.x-shoulder.x),Is.LessThan(.18f),motion+" lateral chicken-wing pole at "+phase);
                        Assert.That(elbow.x*(side=="L"?1f:-1f),Is.GreaterThan(.12f),motion+" elbow folded inward across the trunk at "+phase);
                    }
                }
            });
        }

        [Test]
        public void MountedPoseBendsKneesWhileRetainingMeasuredStirrupAnkleTargets()
        {
            WithReview((instance,animator)=>
            {
                // The source bind pelvis is unchanged; the clip is permitted
                // to settle it 25mm down/back without moving tack geometry.
                var bindPelvis=new Vector3(0,.93459326f,-.06512925f);
                Sample(animator,"MountedSeated",.25f);
                Assert.That(Vector3.Distance(Bone(instance,"Pelvis").position,bindPelvis+new Vector3(0,-.025f,-.025f)),Is.LessThan(.002f));
                foreach(var side in new[]{"L","R"})
                {
                    var sign=side=="L"?1f:-1f;
                    var hip=Bone(instance,"UpperLeg_"+side).position;
                    var knee=Bone(instance,"LowerLeg_"+side).position;
                    var ankle=Bone(instance,"Foot_"+side).position;
                    var flex=Vector3.Angle(knee-hip,ankle-knee);
                    Assert.That(flex,Is.InRange(45f,70f),"Mounted leg must visibly bend; this does not accept clipping/rider fit.");
                    Assert.That(Vector3.Distance(ankle,bindPelvis+new Vector3(sign*.46f,-.72f,.16f)),Is.LessThan(.003f),"Do not fake knee bend by lifting feet out of stirrups.");
                    var hand=Bone(instance,"Hand_"+side).position;
                    Assert.That(Mathf.Abs(hand.x),Is.EqualTo(.12f).Within(.003f));
                    Assert.That(hand.z,Is.EqualTo(bindPelvis.z+.30f).Within(.003f));
                }
            });
        }

        [Test]
        public void AttackHandReachesAuthoredEndTargetWithoutAnatomicalIkClamp()
        {
            WithReview((instance,animator)=>
            {
                Sample(animator,"OneHandedAttack",.5f);
                Assert.That(Vector3.Distance(Bone(instance,"Hand_R").position,new Vector3(.015f,1.18f,.39f)),Is.LessThan(.003f),"An unreachable target cannot be counted as a valid slash.");
            });
        }

        [TestCaseSource(typeof(HasanDonorPipeline),nameof(HasanDonorPipeline.Motions))]
        public void HandCorrectivesApplyAuthoredGripOrRelaxationAndResetOtherStates(string motion)
        {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(HasanDonorPipeline.Folder+"/ANM_HasanDonor_"+motion+".anim");
            Assert.That(clip,Is.Not.Null);
            var bindings=AnimationUtility.GetCurveBindings(clip).Where(b=>b.type==typeof(SkinnedMeshRenderer)&&b.propertyName.StartsWith("blendShape.Grip_",StringComparison.Ordinal)).ToArray();
            Assert.That(bindings.Length,Is.EqualTo(6),"Both hand correctives need explicit reset curves on all three LODs.");
            foreach(var side in new[]{"L","R"})
            {
                var expected=motion=="MountedSeated"?85f:motion=="OneHandedAttack"&&side=="R"?100f:motion=="Run"?40f:25f;
                var handBindings=bindings.Where(b=>b.propertyName=="blendShape.Grip_"+side).ToArray();
                Assert.That(handBindings.Select(b=>b.path).OrderBy(p=>p),Is.EqualTo(new[]{"LOD0","LOD1","LOD2"}));
                foreach(var binding in handBindings)
                foreach(var time in new[]{0f,clip.length*.5f,clip.length})
                    Assert.That(AnimationUtility.GetEditorCurve(clip,binding).Evaluate(time),Is.EqualTo(expected).Within(.001f));
            }
        }

        [Test]
        public void ActualRunAndIdleStatesRelaxBothHandsAfterFullAttackGrip()
        {
            WithReview((instance,animator)=>
            {
                var skins=instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach(var motion in new[]{"OneHandedAttack","Run","Idle"})
                {
                    Sample(animator,motion,.25f);
                    foreach(var skin in skins)
                    foreach(var side in new[]{"L","R"})
                    {
                        var index=skin.sharedMesh.GetBlendShapeIndex("Grip_"+side);
                        Assert.That(index,Is.GreaterThanOrEqualTo(0),"Relaxed finger pose requires the original hand corrective on every LOD.");
                        var expected=motion=="OneHandedAttack"&&side=="R"?100f:motion=="Run"?40f:25f;
                        Assert.That(skin.GetBlendShapeWeight(index),Is.EqualTo(expected).Within(.01f),motion+" must reset the previous state's grip on "+skin.name);
                    }
                }
            });
        }

        private static Transform Bone(GameObject instance,string name)=>HistoricalArtPoseReview.Bone(instance,name);
        private static void Sample(Animator animator,string motion,float phase){animator.Play(motion,0,phase);animator.Update(0);}
        private static void WithReview(Action<GameObject,Animator> action)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(HistoricalArtCandidatePipeline.CharacterRoot+"/"+HasanDonorPipeline.Id+".prefab");
            var instance=Object.Instantiate(prefab);Avatar avatar=null;
            try
            {
                var animator=instance.GetComponent<Animator>();
                avatar=AvatarBuilder.BuildGenericAvatar(instance,"Root");animator.avatar=avatar;
                animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();animator.Update(0);
                action(instance,animator);
            }
            finally{Object.DestroyImmediate(instance);if(avatar!=null)Object.DestroyImmediate(avatar);}
        }
    }
}
