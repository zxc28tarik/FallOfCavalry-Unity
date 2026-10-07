#nullable enable
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace FOC.Presentation.Visuals
{
    public sealed partial class MeshyHasanPilotPlayer
    {
        public AnimationClip[] userMotionClips=Array.Empty<AnimationClip>();
        public MeshyTargetContactProfile[] userMotionContactProfiles=Array.Empty<MeshyTargetContactProfile>();
        [Serializable] public sealed class UserMotionEvidence
        {
            public string status="WINDOWS_CAPTURE_COMPLETE_VISUAL_QA_PENDING",sourceSha="",unityVersion="",platform="",graphicsDevice="";
            public string scope="New user Meshy motion on the retained Hasan via assembler/cache/pool; no model/rig/material/gameplay replacement.";
            public bool actualRightHandSocket,weaponVisible,rootMotionEnabled;
            public string worktreeState="NOT_SUPPLIED";
            public bool measuredContactCandidate;
            public int activeLeases;
            public CaptureEvidence[] results=Array.Empty<CaptureEvidence>();
        }

        public AnimationClip SelectedUserMotion(string name)
        {
            if(name!="Idle_12"&&name!="Right_Hand_Sword_Slash")throw new ArgumentException("Unselected pilot motion: "+name);
            var matches=userMotionClips.Where(c=>c!=null&&c.name==name).ToArray();
            if(matches.Length!=1||!matches[0].isHumanMotion)throw new InvalidOperationException("Exactly one actual Humanoid user motion required: "+name);
            return matches[0];
        }

        private IEnumerator RunUserMotionReview()
        {
            if(output==null)throw new InvalidOperationException("User motion capture requires output.");
            if(calibratedAvatar==null||!calibratedAvatar.isHuman||!calibratedAvatar.isValid)
                throw new InvalidOperationException("Missing calibrated comparison Avatar.");
            var rightHandOwned=true;var weaponVisible=true;
            foreach(var variant in new[]{"Original","Calibrated"})
            foreach(var motion in new[]{"Idle_12","Right_Hand_Sword_Slash"})
            {
                ClearActors();var clip=SelectedUserMotion(motion);var attack=motion=="Right_Hand_Sword_Slash";
                var contact=variant=="Calibrated"&&Arg("--meshy-user-contact")=="true"
                    ?userMotionContactProfiles.Single(p=>p.sourceClip==clip&&p.targetAvatar==calibratedAvatar):null;
                var actor=CreateActorCore(clip,Vector3.zero,variant=="Original"?null:calibratedAvatar,contact,attack);
                actor.calibrationScenario="UserMotion-"+variant;actor.sourceClipName=motion;
                if(attack)
                {
                    var hand=actor.animator.GetBoneTransform(HumanBodyBones.RightHand);
                    var socket=actor.weaponInstance!.transform.parent;
                    rightHandOwned&=socket!=null&&socket.name=="Socket_RightHand"&&socket.IsChildOf(hand);
                    weaponVisible&=actor.weaponInstance.GetComponentsInChildren<Renderer>().Any(r=>r.enabled&&r.sharedMaterial!=null);
                    if(!rightHandOwned||!weaponVisible)throw new InvalidOperationException("Real right-hand weapon attachment failed.");
                }
                var phases=attack?new[]{0f,.15f,.25f,.35f,.45f,.55f,.75f,.95f}:new[]{0f,.5f,.99f};
                foreach(var view in attack?new[]{"side","quarter"}:new[]{"front","quarter"})
                foreach(var phase in phases)
                {
                    yield return null;SetPhase(actor,phase);var sampled=SnapshotTrackedJoints(actor);
                    yield return new WaitForEndOfFrame();AssertStablePoseAcrossRenderBoundary(actor,sampled);
                    Frame(view);
                    Capture(variant+"-"+motion+"-"+view+"-"+phase.ToString("0.00",CultureInfo.InvariantCulture)+".png",view,phase);
                }
            }
            ClearActors();
            if(assembler.ActiveLeaseCount!=0||pool.LeasedCount!=0)throw new InvalidOperationException("User motion review leaked a representation lease.");
            var report=new UserMotionEvidence{sourceSha=Arg("--meshy-sha")??"NOT_SUPPLIED",unityVersion=Application.unityVersion,
                platform=Application.platform.ToString(),graphicsDevice=SystemInfo.graphicsDeviceName,actualRightHandSocket=rightHandOwned,
                weaponVisible=weaponVisible,rootMotionEnabled=false,activeLeases=assembler.ActiveLeaseCount,results=captures.ToArray(),
                worktreeState=Arg("--meshy-worktree")??"NOT_SUPPLIED",measuredContactCandidate=Arg("--meshy-user-contact")=="true"};
            File.WriteAllText(Path.Combine(output,"user-motion-evidence.json"),JsonUtility.ToJson(report,true));
            Debug.Log("FOC_MESHY_USER_MOTION_CAPTURE_COMPLETE count="+captures.Count);Application.Quit(0);
        }
    }
}
