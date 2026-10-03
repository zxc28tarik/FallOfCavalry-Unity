#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FOC.Presentation.Visuals;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FOC.Editor.Visuals
{
    /// <summary>Isolated Hasan deformation clips, not replacement battle animation.</summary>
    public static class HasanDonorPipeline
    {
        public const string Id="CHR_HasanAga_DonorDraft";
        public const string Folder=HistoricalArtCandidatePipeline.CharacterRoot+"/HasanMotionReview";
        public static readonly string[] Motions={"Idle","Walk","Run","Turn","OneHandedAttack","ArmRaise","Crouch","MountedSeated"};
        public static void Run()
        {
            try
            {
                HistoricalArtCandidatePipeline.GenerateFromDirectory(HistoricalArtCandidatePipeline.SourceRoot+"/HasanDonor");
                AuthorMotion();
                HasanDonorPoseAuthoring.Export();
                Debug.Log("FOC_HASAN_DONOR_IMPORT_AND_MOTION_PASS: technical only, NOT visual acceptance");
                EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        private static void AuthorMotion()
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var path=HistoricalArtCandidatePipeline.CharacterRoot+"/"+Id+".prefab";
            var human=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var bones=human.GetComponentsInChildren<Transform>().Where(t=>t!=human.transform&&!t.name.StartsWith("LOD",StringComparison.Ordinal)).ToArray();
                var positions=bones.Select(t=>t.localPosition).ToArray();var rotations=bones.Select(t=>t.localRotation).ToArray();
                var controllerPath=Folder+"/HasanDonorReview.controller";
                var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath)??AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                var machine=controller.layers[0].stateMachine;
                foreach(var motion in Motions)
                {
                    var duration=motion=="Run"?.8f:motion=="Walk"?1.3f:2f;
                    var curves=new Dictionary<string,AnimationCurve[]>();
                    foreach(var t in bones)curves.Add(AnimationUtility.CalculateTransformPath(t,human.transform),Enumerable.Range(0,7).Select(_=>new AnimationCurve()).ToArray());
                    for(var frame=0;frame<=60;frame++)
                    {
                        for(var i=0;i<bones.Length;i++){bones[i].localPosition=positions[i];bones[i].localRotation=rotations[i];}
                        Pose(human,motion,frame/60f);
                        foreach(var t in bones)
                        {
                            var c=curves[AnimationUtility.CalculateTransformPath(t,human.transform)];var p=t.localPosition;var r=t.localRotation;
                            var v=new[]{p.x,p.y,p.z,r.x,r.y,r.z,r.w};for(var i=0;i<7;i++)c[i].AddKey(frame/60f*duration,v[i]);
                        }
                    }
                    var clip=new AnimationClip{name="ANM_HasanDonor_"+motion,frameRate=30};
                    var props=new[]{"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w"};
                    foreach(var pair in curves)for(var i=0;i<7;i++)
                    {
                        var curve=pair.Value[i];
                        // Keep reset bindings but avoid 61 identical keys on
                        // every static socket/position/quaternion channel.
                        if(curve.keys.All(k=>k.value==curve.keys[0].value))curve=AnimationCurve.Constant(0,duration,curve.keys[0].value);
                        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(pair.Key,typeof(Transform),props[i]),curve);
                    }
                    // Finger poses come from the original MakeHuman hand rig,
                    // baked as local corrective shapes. The canonical skeleton
                    // and shared gameplay animation library remain unchanged.
                    foreach(var renderer in human.GetComponentsInChildren<SkinnedMeshRenderer>())
                    foreach(var side in new[]{"L","R"})
                    {
                        // The source's fully open bind hand is a deformation
                        // extreme, not a natural resting/running finger pose.
                        // Reuse the same authored grip corrective at partial
                        // weight; no finger rig or shared animation is changed.
                        var value=motion=="MountedSeated"?85f:motion=="OneHandedAttack"&&side=="R"?100f:motion=="Run"?40f:25f;
                        var rendererPath=AnimationUtility.CalculateTransformPath(renderer.transform,human.transform);
                        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(rendererPath,typeof(SkinnedMeshRenderer),"blendShape.Grip_"+side),AnimationCurve.Constant(0,duration,value));
                    }
                    clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,settings);
                    var clipPath=Folder+"/"+clip.name+".anim";var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                    if(existing==null)AssetDatabase.CreateAsset(clip,clipPath);else{EditorUtility.CopySerialized(clip,existing);EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(clip);}
                    var state=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name==motion)??machine.AddState(motion);
                    state.motion=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);if(motion=="Idle")machine.defaultState=state;
                }
                for(var i=0;i<bones.Length;i++){bones[i].localPosition=positions[i];bones[i].localRotation=rotations[i];}
                var animator=human.GetComponent<Animator>();animator.runtimeAnimatorController=controller;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.applyRootMotion=false;
                PrefabUtility.SaveAsPrefabAsset(human,path);EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
            }
            finally{PrefabUtility.UnloadPrefabContents(human);}
        }
        private static void Pose(GameObject human,string motion,float phase)
        {
            Transform B(string n)=>HistoricalArtPoseReview.Bone(human,n);
            var wave=Mathf.Sin(phase*Mathf.PI*2);var pulse=(1-Mathf.Cos(phase*Mathf.PI*2))*.5f;
            var pelvis=B("Pelvis");var mounted=motion=="MountedSeated";var crouch=motion=="Crouch";
            var bindPelvis=pelvis.position;
            if(crouch)pelvis.localPosition+=new Vector3(0,-.25f*pulse,-.07f*pulse);
            // Keep the stirrup target at its measured tack position, but settle
            // the pelvis into the saddle instead of stretching a standing pose
            // toward it. This is only the candidate's review pose.
            if(mounted)pelvis.localPosition+=new Vector3(0,-.025f,-.025f);
            // Lower the gait center enough to keep the stance leg reachable;
            // otherwise clamped two-link IK lifts both soles above the floor.
            if(motion=="Walk"||motion=="Run")pelvis.localPosition+=new Vector3(0,(motion=="Run"?-.11f:-.055f)+.015f*Mathf.Cos(phase*Mathf.PI*4),0);
            if(motion=="Idle"||mounted)B("Chest").localRotation=Quaternion.Euler(1.2f*wave,0,0);
            if(crouch)B("Spine").localRotation=Quaternion.Euler(16*pulse,0,0);
            if(motion=="Walk"||motion=="Run")
            {
                // A small counter-rotation belongs to the gait, not a changed
                // rig or root-motion contract. Avoid rigid shoulders over hips.
                B("Spine").localRotation=Quaternion.Euler(motion=="Run"?5f:1.5f,3f*wave,0);
                B("Chest").localRotation=Quaternion.Euler(0,-5f*wave,0);
            }
            foreach(var side in new[]{"L","R"})
            {
                var sign=side=="L"?1f:-1f;var step=wave*sign;
                var foot=new Vector3(sign*.1977f,.073f,.019f);var hand=new Vector3(sign*.27f,.965f,.08f);
                var elbowHint=new Vector3(sign*.25f,1.12f,-.08f);
                if(motion=="Walk"||motion=="Run")
                {
                    var running=motion=="Run";
                    var cycle=Mathf.Repeat(phase+(side=="R"?.5f:0),1);
                    var stride=running?.66f:.45f;
                    if(cycle<.5f)
                    {
                        var swing=cycle*2;
                        // The foot moves rear-to-front while lifted, then has
                        // a full planted stance half-cycle. A sinusoid plus
                        // max(0,sine) was moving the foot through the ground.
                        foot.z+=Mathf.Lerp(-stride*.5f,stride*.5f,Mathf.SmoothStep(0,1,swing));
                        foot.y+=(running?.21f:.105f)*Mathf.Sin(swing*Mathf.PI);
                    }
                    else foot.z+=Mathf.Lerp(stride*.5f,-stride*.5f,(cycle-.5f)*2);
                    // A running elbow swings behind the trunk while the bent
                    // forearm still carries the wrist forward. Driving the
                    // wrist itself behind the shoulder folded the elbow across
                    // the torso and inverted the sleeve in the r5 player shot.
                    hand=running
                        ?new Vector3(sign*.265f,1.11f-.055f*step,.24f-.14f*step)
                        :new Vector3(sign*.265f,.96f,.14f-.15f*step);
                    // An explicit anatomical elbow target keeps the upper arm
                    // alongside the torso. The old lateral pole pushed elbows
                    // out into a shrug/chicken-wing silhouette.
                    elbowHint=new Vector3(sign*.27f,running?1.075f:1.12f,-.075f-(running?.055f:.025f)*step);
                }
                if(motion=="ArmRaise")
                {
                    hand=Vector3.Lerp(hand,new Vector3(sign*.40f,1.87f,.10f),pulse);
                    elbowHint=Vector3.Lerp(elbowHint,new Vector3(sign*.52f,1.58f,-.08f),pulse);
                }
                if(motion=="OneHandedAttack"&&side=="R")
                {
                    // The previous end target was .74m from a .52m arm and
                    // necessarily hit the IK clamp. Keep the intended slash
                    // within anatomical reach instead of silently stretching.
                    hand=Vector3.Lerp(new Vector3(-.36f,1.63f,.13f),new Vector3(.015f,1.18f,.39f),pulse);
                    elbowHint=Vector3.Lerp(new Vector3(-.44f,1.40f,-.09f),new Vector3(-.30f,1.16f,.15f),pulse);
                }
                if(crouch)hand+=new Vector3(0,-.10f*pulse,.22f*pulse);
                if(crouch)elbowHint+=new Vector3(0,-.16f*pulse,.10f*pulse);
                if(mounted)
                {
                    // Measured tack: seat (0,1.81,-.42), stirrup sole y=1.01,
                    // x=+/-.46; ankle is .08m above the iron and .16m forward.
                    foot=bindPelvis+new Vector3(sign*.46f,-.72f,.16f);
                    hand=bindPelvis+new Vector3(sign*.12f,.21f+.008f*wave,.30f);
                    elbowHint=bindPelvis+new Vector3(sign*.265f,.22f,-.015f);
                }
                HistoricalArtPoseReview.Limb(B("UpperLeg_"+side),B("LowerLeg_"+side),B("Foot_"+side),foot,new Vector3(sign*(mounted?.5f:.05f),0,1));
                B("Foot_"+side).rotation=Quaternion.identity;
                var upperArm=B("UpperArm_"+side);
                HistoricalArtPoseReview.Limb(upperArm,B("LowerArm_"+side),B("Hand_"+side),hand,elbowHint-upperArm.position);
            }
            if(motion=="Turn")B("Root").localRotation=Quaternion.Euler(0,45*wave,0);
        }
    }
}
