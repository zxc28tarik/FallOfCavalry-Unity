#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace FOC.Presentation.Visuals
{
    public sealed partial class MeshyHasanPilotPlayer
    {
        [Serializable] public sealed class GripReviewVertex
        {
            public int index;
            public Vector3 handLocalPosition;
            public float handWeight;
        }
        [Serializable] public sealed class GripReviewSample
        {
            public float phase, gripToPalmMeters, gripWeight;
            public Vector3 gripInHandFrame;
            public Quaternion weaponRotationInHandFrame;
            public GripReviewVertex[] actualSkinnedHandVertices=Array.Empty<GripReviewVertex>();
        }
        [Serializable] public sealed class GripReviewEvidence
        {
            public string status="DIAGNOSTIC_ONLY_OPEN_HAND_IS_NOT_GRIP_ACCEPTANCE";
            public string sourceSha="",worktreeState="",platform="",unityVersion="";
            public bool rightHandSocketOwned;
            public bool gripCandidateSelected;
            public int mappedFingerBones, availableBlendShapes;
            public GripReviewSample[] samples=Array.Empty<GripReviewSample>();
            public CaptureEvidence[] captures=Array.Empty<CaptureEvidence>();
        }

        [Serializable] public sealed class GripCameraEvidence
        {
            public string status="WINDOWS_CAPTURE_COMPLETE_CAMERA_AWARE_VISUAL_QA_PENDING";
            public string sourceSha="",worktreeState="",platform="",unityVersion="";
            public string tacticalCameraSource="FOC.Editor.Visuals.BattlePipelineBatch: position (0,18,-28), rotation (28,0,0), default perspective FOV60. Current repository battle camera; not a cinematic close-up.";
            public string policy="User tactical-camera addendum: tactical primary, character inspection secondary, extreme grip diagnostic only; small stable intersections accepted, floating/wrong ownership/orientation/major silhouette failure not accepted.";
            public Vector3 tacticalPosition=new Vector3(0,18,-28);
            public Vector3 tacticalEuler=new Vector3(28,0,0);
            public float tacticalFieldOfView=60;
            public bool rightHandSocketOwned;
            public float maximumAnchorErrorMeters;
            public CaptureEvidence[] captures=Array.Empty<CaptureEvidence>();
        }

        private IEnumerator RunGripCameraAcceptance()
        {
            if(output==null||calibratedAvatar==null||Arg("--meshy-grip-candidate")!="true")
                throw new InvalidOperationException("Camera-aware grip acceptance requires the explicit retained corrective candidate.");
            var report=new GripCameraEvidence{sourceSha=Arg("--meshy-sha")??"NOT_SUPPLIED",
                worktreeState=Arg("--meshy-worktree")??"NOT_SUPPLIED",platform=Application.platform.ToString(),unityVersion=Application.unityVersion,
                rightHandSocketOwned=true};
            void Tactical()
            {
                cameraView.fieldOfView=60;cameraView.transform.position=report.tacticalPosition;
                cameraView.transform.rotation=Quaternion.Euler(report.tacticalEuler);
            }
            void Verify(Actor actor)
            {
                var hand=actor.animator.GetBoneTransform(HumanBodyBones.RightHand);
                var weapon=actor.weaponInstance!.transform;
                if(hand==null||weapon.parent==null||weapon.parent.name!="Socket_RightHand"||!weapon.IsChildOf(hand))
                    throw new InvalidOperationException("Real right-hand ownership failed in the camera-aware review.");
                report.maximumAnchorErrorMeters=Mathf.Max(report.maximumAnchorErrorMeters,
                    MeshyKilicGripAttachment.MeasureAnchorErrorMeters(weapon,hand,MeshyKilicGripAttachment.CorrectivePalmInHand));
                if(report.maximumAnchorErrorMeters>.0001f)throw new InvalidOperationException("Weapon detached from its measured grip during playback.");
                actor.gripVisual!.ApplyCurrentState();
            }
            foreach(var motion in new[]{"Idle_12","Right_Hand_Sword_Slash"})
            {
                ClearActors();var clip=SelectedUserMotion(motion);
                var contact=userMotionContactProfiles.Single(p=>p.sourceClip==clip&&p.targetAvatar==calibratedAvatar);
                var actor=CreateActorCore(clip,Vector3.zero,calibratedAvatar,contact,true);
                actor.calibrationScenario="Grip-CameraAware-Calibrated";actor.sourceClipName=motion;
                var idle=motion=="Idle_12";
                foreach(var phase in idle?new[]{.5f}:new[]{0f,.15f,.25f,.35f,.55f,.75f,.95f})
                {
                    yield return null;SetPhase(actor,phase);Verify(actor);
                    var tracked=SnapshotTrackedJoints(actor);yield return new WaitForEndOfFrame();
                    AssertStablePoseAcrossRenderBoundary(actor,tracked);Verify(actor);
                    Tactical();
                    var suffix=idle?"idle":"attack";
                    var phaseName=phase.ToString("0.00",CultureInfo.InvariantCulture);
                    Capture("normal-tactical-sword-"+suffix+(idle||phase==.35f?"":"-"+phaseName)+".png","NORMAL_TACTICAL_PRIMARY",phase);
                    cameraView.fieldOfView=34;Frame("quarter");
                    Capture("character-view-sword-"+suffix+"-"+phaseName+".png","CHARACTER_INSPECTION_SECONDARY",phase);
                    if(!idle&&phase==.35f)
                    {
                        Capture("character-view-sword-three-quarter.png","CHARACTER_INSPECTION_SECONDARY",phase);
                        Frame("side");Capture("character-view-sword-attack-side.png","CHARACTER_INSPECTION_SECONDARY",phase);
                        var hand=actor.animator.GetBoneTransform(HumanBodyBones.RightHand);
                        var center=hand.TransformPoint(new Vector3(-.012f,.125f,.015f));
                        cameraView.transform.position=center+hand.TransformDirection(Vector3.left)*.36f;
                        cameraView.transform.LookAt(center,hand.TransformDirection(Vector3.up));
                        Capture("diagnostic-grip-closeup.png","DEBUG_ONLY_DIAGNOSTIC",phase);
                        for(var lod=1;lod<=2;lod++)
                        {
                            actor.forcedLod=lod;foreach(var group in actor.view.GetComponentsInChildren<LODGroup>())group.ForceLOD(lod);
                            Frame("quarter");Capture("character-view-sword-three-quarter-lod"+lod+".png","CHARACTER_LOD_DIAGNOSTIC",phase);
                        }
                        actor.forcedLod=0;foreach(var group in actor.view.GetComponentsInChildren<LODGroup>())group.ForceLOD(0);
                    }
                }
            }
            ClearActors();var groupClip=SelectedUserMotion("Right_Hand_Sword_Slash");
            var groupContact=userMotionContactProfiles.Single(p=>p.sourceClip==groupClip&&p.targetAvatar==calibratedAvatar);
            for(var i=0;i<12;i++)
            {
                var actor=CreateActorCore(groupClip,new Vector3((i%4-1.5f)*1.8f,0,(i/4)*2f),calibratedAvatar,groupContact,true);
                actor.calibrationScenario="Grip-Tactical-12";SetPhase(actor,(i%4)*.2f+.15f);Verify(actor);
            }
            yield return new WaitForEndOfFrame();Tactical();Capture("normal-tactical-sword-group-12.png","NORMAL_TACTICAL_PRIMARY",.35f);
            report.captures=captures.ToArray();ClearActors();
            if(assembler.ActiveLeaseCount!=0||pool.LeasedCount!=0)throw new InvalidOperationException("Camera-aware review leaked a runtime lease.");
            File.WriteAllText(Path.Combine(output,"camera-aware-grip-evidence.json"),JsonUtility.ToJson(report,true));
            Debug.Log("FOC_MESHY_GRIP_CAMERA_CAPTURE_COMPLETE captures="+captures.Count);Application.Quit(0);
        }

        private IEnumerator RunGripReview()
        {
            if(output==null||calibratedAvatar==null)throw new InvalidOperationException("Grip review requires output and calibrated Avatar.");
            var clip=SelectedUserMotion("Right_Hand_Sword_Slash");
            var contact=userMotionContactProfiles.Single(p=>p.sourceClip==clip&&p.targetAvatar==calibratedAvatar);
            var actor=CreateActorCore(clip,Vector3.zero,calibratedAvatar,contact,true);
            actor.calibrationScenario="RightHandGripDiagnostic";
            actor.sourceClipName=clip.name;
            var hand=actor.animator.GetBoneTransform(HumanBodyBones.RightHand);
            var weapon=actor.weaponInstance!.transform;
            if(hand==null||weapon.parent==null||weapon.parent.name!="Socket_RightHand"||!weapon.IsChildOf(hand))
                throw new InvalidOperationException("Grip diagnostic requires the actual right-hand socket.");
            var skin=VisibleRenderers(actor).OfType<SkinnedMeshRenderer>().Single();
            var handIndex=Array.IndexOf(skin.bones,hand);
            if(handIndex<0)throw new InvalidOperationException("Actual skin has no right-hand influence.");
            var weights=skin.sharedMesh.boneWeights;
            var samples=new List<GripReviewSample>();
            var useGrip=Arg("--meshy-grip-candidate")=="true";
            var palmAnchor=useGrip?MeshyKilicGripAttachment.CorrectivePalmInHand:MeshyKilicGripAttachment.CandidatePalmInHand;
            var fingers=new[]{HumanBodyBones.RightThumbProximal,HumanBodyBones.RightIndexProximal,
                HumanBodyBones.RightMiddleProximal,HumanBodyBones.RightRingProximal,HumanBodyBones.RightLittleProximal};
            foreach(var phase in new[]{0f,.15f,.25f,.35f,.55f,.75f,.95f})
            {
                yield return null; SetPhase(actor,phase);
                var tracked=SnapshotTrackedJoints(actor);
                yield return new WaitForEndOfFrame(); AssertStablePoseAcrossRenderBoundary(actor,tracked);
                var baked=new Mesh();
                try
                {
                    skin.BakeMesh(baked);var vertices=baked.vertices;
                    var selected=new List<GripReviewVertex>();
                    for(var i=0;i<weights.Length;i++)
                    {
                        var w=weights[i];
                        var influence=(w.boneIndex0==handIndex?w.weight0:0)+(w.boneIndex1==handIndex?w.weight1:0)
                            +(w.boneIndex2==handIndex?w.weight2:0)+(w.boneIndex3==handIndex?w.weight3:0);
                        if(influence>=.5f)selected.Add(new GripReviewVertex{index=i,handWeight=influence,
                            handLocalPosition=hand.InverseTransformPoint(skin.transform.TransformPoint(vertices[i]))});
                    }
                    var grip=MeshyKilicGripAttachment.MeasureGripInHand(weapon,hand);
                    samples.Add(new GripReviewSample{phase=phase,gripInHandFrame=grip,
                        gripToPalmMeters=MeshyKilicGripAttachment.MeasureAnchorErrorMeters(weapon,hand,palmAnchor),
                        gripWeight=useGrip?skin.GetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex(MeshyRightHandGripVisual.ShapeName)):0,
                        weaponRotationInHandFrame=Quaternion.Inverse(hand.rotation)*weapon.rotation,
                        actualSkinnedHandVertices=selected.ToArray()});
                }
                finally {Destroy(baked);}
                foreach(var view in new[]{"palm","back","thumb","little"})
                {
                    var center=hand.TransformPoint(new Vector3(-.012f,.125f,.015f));
                    var direction=view=="palm"?Vector3.left:view=="back"?Vector3.right:view=="thumb"?(Vector3.back+Vector3.left*.7f).normalized:(Vector3.forward+Vector3.left*.35f).normalized;
                    cameraView.transform.position=center+hand.TransformDirection(direction)*.36f;
                    cameraView.transform.LookAt(center,hand.TransformDirection(Vector3.up));
                    Capture("grip-"+view+"-"+phase.ToString("0.00",CultureInfo.InvariantCulture)+".png",view,phase);
                }
            }
            var report=new GripReviewEvidence{sourceSha=Arg("--meshy-sha")??"NOT_SUPPLIED",
                worktreeState=Arg("--meshy-worktree")??"NOT_SUPPLIED",platform=Application.platform.ToString(),unityVersion=Application.unityVersion,
                status=useGrip?"RIGHT_HAND_CORRECTIVE_CAPTURE_COMPLETE_VISUAL_QA_PENDING":"DIAGNOSTIC_ONLY_OPEN_HAND_IS_NOT_GRIP_ACCEPTANCE",
                rightHandSocketOwned=true,gripCandidateSelected=useGrip,mappedFingerBones=fingers.Count(b=>actor.animator.GetBoneTransform(b)!=null),
                availableBlendShapes=skin.sharedMesh.blendShapeCount,
                samples=samples.ToArray(),captures=captures.ToArray()};
            File.WriteAllText(Path.Combine(output,"grip-evidence.json"),JsonUtility.ToJson(report,true));
            ClearActors();
            if(assembler.ActiveLeaseCount!=0||pool.LeasedCount!=0)throw new InvalidOperationException("Grip review leaked leases.");
            Debug.Log("FOC_MESHY_GRIP_DIAGNOSTIC_CAPTURE_COMPLETE captures="+captures.Count);
            Application.Quit(0);
        }
    }
}
