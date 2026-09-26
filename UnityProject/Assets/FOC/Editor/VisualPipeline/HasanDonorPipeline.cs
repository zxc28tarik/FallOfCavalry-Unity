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
            if(crouch)pelvis.localPosition+=new Vector3(0,-.25f*pulse,-.07f*pulse);
            // Lower the gait center enough to keep the stance leg reachable;
            // otherwise clamped two-link IK lifts both soles above the floor.
            if(motion=="Walk"||motion=="Run")pelvis.localPosition+=new Vector3(0,(motion=="Run"?-.11f:-.055f)+.015f*Mathf.Cos(phase*Mathf.PI*4),0);
            if(motion=="Idle"||mounted)B("Chest").localRotation=Quaternion.Euler(1.2f*wave,0,0);
            if(crouch)B("Spine").localRotation=Quaternion.Euler(16*pulse,0,0);
            foreach(var side in new[]{"L","R"})
            {
                var sign=side=="L"?1f:-1f;var step=wave*sign;
                var foot=new Vector3(sign*.1977f,.073f,.019f);var hand=new Vector3(sign*.27f,1.02f,.12f);
                if(motion=="Walk"||motion=="Run")
                {
                    var running=motion=="Run";
                    foot.z+=(running?.38f:.27f)*step;foot.y+=(running?.18f:.10f)*Mathf.Max(0,step);
                    hand=new Vector3(sign*.28f,running?1.17f:1.02f,.16f-(running?.19f:.13f)*step);
                }
                if(motion=="ArmRaise")hand=Vector3.Lerp(hand,new Vector3(sign*.40f,1.87f,.10f),pulse);
                if(motion=="OneHandedAttack"&&side=="R")hand=Vector3.Lerp(new Vector3(-.40f,1.65f,.14f),new Vector3(.20f,1.05f,.54f),pulse);
                if(crouch)hand+=new Vector3(0,-.10f*pulse,.22f*pulse);
                if(mounted){foot=pelvis.position+new Vector3(sign*.46f,-.72f,.16f);hand=pelvis.position+new Vector3(sign*.16f,.21f+.008f*wave,.33f);}
                HistoricalArtPoseReview.Limb(B("UpperLeg_"+side),B("LowerLeg_"+side),B("Foot_"+side),foot,new Vector3(sign*(mounted?.75f:.05f),0,1));
                B("Foot_"+side).rotation=Quaternion.identity;
                HistoricalArtPoseReview.Limb(B("UpperArm_"+side),B("LowerArm_"+side),B("Hand_"+side),hand,new Vector3(sign*.4f,0,-1));
            }
            if(motion=="Turn")B("Root").localRotation=Quaternion.Euler(0,45*wave,0);
        }
    }
}
