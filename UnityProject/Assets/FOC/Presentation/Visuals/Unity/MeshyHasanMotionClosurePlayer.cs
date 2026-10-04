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
        public AnimationClip[] libraryMotionClips=Array.Empty<AnimationClip>();
        public MeshyTargetContactProfile[] contactProfiles=Array.Empty<MeshyTargetContactProfile>();
        [Serializable] public sealed class MotionClosureEvidence
        {
            public string status="SOURCE_MOTION_COMPARISON_NOT_VISUAL_ACCEPTANCE",sourceSha="",unityVersion="",platform="",graphicsDevice="";
            public string source="Licensed CC0 external animation donor; original Meshy character unchanged";
            public string contact="NOT_CORRECTED",mounted="NOT_RUN",performance="NOT_RUN";
            public bool builtInHumanoidFootIk;
            public int activeLeases;public CaptureEvidence[] results=Array.Empty<CaptureEvidence>();
        }
        private IEnumerator RunMotionClosure()
        {
            if(output==null||libraryMotionClips.Length==0)throw new InvalidOperationException("Motion comparison needs supplied licensed clips and output path.");
            if(Arg("--meshy-contact-review")=="true")
            {
                var review=RunContactReview();while(review.MoveNext())yield return review.Current;yield break;
            }
            foreach(var variant in new[]{"Original","Calibrated"})
            {
                if(variant=="Calibrated"&&(calibratedAvatar==null||!calibratedAvatar.isValid))throw new InvalidOperationException("Missing calibrated comparison Avatar.");
                foreach(var clip in libraryMotionClips)
                {
                    ClearActors();var actor=CreateActor(clip,Vector3.zero,variant=="Original"?null:calibratedAvatar);
                    actor.calibrationScenario="Library-"+variant;actor.sourceClipName=clip.name;
                    foreach(var phase in new[]{.15f,.35f,.55f,.75f})
                    {
                        yield return null;SetPhase(actor,phase);var sampled=SnapshotTrackedJoints(actor);
                        yield return new WaitForEndOfFrame();AssertStablePoseAcrossRenderBoundary(actor,sampled);
                        FrameBounds(new Bounds(new Vector3(0,.9f,0),new Vector3(2.2f,1.8f,.8f)));
                        var safe=clip.name.Replace('|','_').Replace('/','_').Replace('\\','_');
                        Capture(variant+"-"+safe+"-"+phase.ToString("0.00",CultureInfo.InvariantCulture)+".png","library-comparison-quarter",phase);
                    }
                }
            }
            ClearActors();
            if(assembler.ActiveLeaseCount!=0||pool.LeasedCount!=0)throw new InvalidOperationException("Motion comparison leaked leases.");
            var report=new MotionClosureEvidence{sourceSha=Arg("--meshy-sha")??"NOT_SUPPLIED",unityVersion=Application.unityVersion,platform=Application.platform.ToString(),graphicsDevice=SystemInfo.graphicsDeviceName,builtInHumanoidFootIk=Arg("--meshy-foot-ik")=="true",activeLeases=assembler.ActiveLeaseCount,results=captures.ToArray()};
            File.WriteAllText(Path.Combine(output,"motion-closure-player.json"),JsonUtility.ToJson(report,true));
            Debug.Log("FOC_MESHY_MOTION_COMPARISON_COMPLETE captures="+captures.Count);Application.Quit(0);
        }

        private IEnumerator RunContactReview()
        {
            if(contactProfiles.Length<4)throw new InvalidOperationException("No complete reviewed contact candidate set.");
            foreach(var profile in contactProfiles)
            {
                ClearActors();var actor=CreateActor(profile.sourceClip,Vector3.zero,profile.targetAvatar,profile);
                actor.calibrationScenario="MeasuredContact";actor.sourceClipName=profile.sourceClip.name;
                foreach(var phase in new[]{0f,.15f,.35f,.55f,.75f,.99f})
                {
                    yield return null;SetPhase(actor,phase);var sampled=SnapshotTrackedJoints(actor);
                    yield return new WaitForEndOfFrame();AssertStablePoseAcrossRenderBoundary(actor,sampled);
                    FrameBounds(new Bounds(new Vector3(0,.9f,0),new Vector3(2.2f,1.8f,.8f)));
                    Capture(profile.name+"-"+phase.ToString("0.00",CultureInfo.InvariantCulture)+".png","contact-quarter",phase);
                }
            }
            ClearActors();
            var report=new MotionClosureEvidence{sourceSha=Arg("--meshy-sha")??"NOT_SUPPLIED",unityVersion=Application.unityVersion,platform=Application.platform.ToString(),graphicsDevice=SystemInfo.graphicsDeviceName,contact="TARGET_SPECIFIC_MEASURED_CURVE_WINDOWS_QA_PENDING",builtInHumanoidFootIk=true,activeLeases=assembler.ActiveLeaseCount,results=captures.ToArray()};
            File.WriteAllText(Path.Combine(output!,"motion-closure-player.json"),JsonUtility.ToJson(report,true));
            Debug.Log("FOC_MESHY_CONTACT_CAPTURE_COMPLETE captures="+captures.Count);Application.Quit(0);
        }
    }
}
