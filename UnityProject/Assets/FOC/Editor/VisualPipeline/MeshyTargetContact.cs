#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object=UnityEngine.Object;

namespace FOC.Editor.Visuals
{
    /// <summary>
    /// Offline target-specific contact candidate. This changes only the NEW
    /// Humanoid clip's normalized body Y; it is not gameplay/root movement,
    /// foot IK, contact acceptance, or a repair for bad source choreography.
    /// </summary>
    public static class MeshyTargetContact
    {
        public const string OutputRoot=MeshyHasanPilotPipeline.OutputRoot+"/TargetContact";
        [Flags] public enum Support { Airborne=0,Left=1,Right=2,Both=3 }
        [Serializable] public sealed class SupportKey { public float phase;public Support support; }
        [Serializable] public sealed class ContactPolicy
        {
            public string id="";public bool referenceAndSourcePoseReviewed;public string sourcePoseGateEvidence="";
            public bool loop;public bool useSourceFootIK;public int authorHz=60;public int validationSubdivisions=2;public float groundY;
            public SupportKey[] supportKeys=Array.Empty<SupportKey>();
            public bool requireHeelAndToeSupport;public float contactTolerance=.002f;public float flatSoleHoverTolerance=.006f;public float maximumBodyCorrection=.12f;
            public bool externalPlanarMotionKnown;public Vector3 expectedPlanarDisplacementPerCycle;
            public string supportProvenance="Explicit reviewed support windows; never infer an airborne Run as always grounded.";
        }
        [Serializable] public sealed class InputHash { public string path="";public string sha256=""; }
        [Serializable] public sealed class FootMeasurement
        {
            public float minimumY;public float maximumY;public float heelMinimumY;public float toeMinimumY;public Vector3 supportPoint;
        }
        [Serializable] public sealed class SkinMeasurement
        {
            public FootMeasurement left=new FootMeasurement();public FootMeasurement right=new FootMeasurement();public float skinMinimumY;
            public Vector3 actorRootPosition;public Quaternion actorRootRotation;
        }
        [Serializable] public sealed class FrameEvidence
        {
            public float phase;public Support support;public float correctionMeters;public SkinMeasurement original=new SkinMeasurement();public SkinMeasurement candidate=new SkinMeasurement();
        }
        [Serializable] public sealed class Manifest
        {
            public int version=1;public string targetPrefab="";public string targetAvatar="";public string sourceClip="";public string candidateClip="";public string candidateHash="";
            public ContactPolicy policy=new ContactPolicy();public int authorIntervals;public float targetHumanScale;public float[] correctionMeters=Array.Empty<float>();public InputHash[] immutableInputs=Array.Empty<InputHash>();
            public string method="Actual chosen target Animator/Avatar/clip -> HumanPose. Body position already normalized by humanScale; add measured world-metre correction / target humanScale once to RootT.y. No other muscles/body channels intentionally changed.";
            public string airbornePolicy="Explicit airborne windows interpolate adjacent support baseline corrections, preserving source flight relative to that baseline; they are not snapped to floor.";
            public string limitation="Vertical body rooting cannot guarantee dual-foot flat contact, correct ankle roll or world-space stance lock. Quality failure is preserved; local IK or a better source may still be required.";
        }
        [Serializable] public sealed class Report
        {
            public string status="NOT_COMPLETED";public string operation="";public string utc="";public string unityVersion="";public string error="";public bool originalAssetsUnchanged;
            public string candidate="";public int validationSamples;public int airborneSamples;public int groundedSamples;public int leftSoleVertices;public int rightSoleVertices;
            public float minimumCandidateFootY;public float minimumCandidateSkinY;public float maximumStanceHover;public float maximumFlatSoleHover;public float maximumAbsoluteCorrection;
            public float maximumAirborneHeightDelta;public float maximumExternalActorTranslation;public float maximumExternalActorRotation;
            public float maximumRelativeStancePointStep;public float maximumPredictedWorldStancePointStep;public float loopCorrectionSeam;public float loopMuscleSeam;public float loopBodySeam;public float loopBodyAngleSeam;
            public string slidingStatus="Relative in-place foot motion is measured, not automatically world sliding. External gameplay movement is neither read nor changed.";
            public string visualAcceptance="NOT_EVALUATED: actual Windows player, contact transitions, sliding, loop, toe/heel and cloth review required.";
            public string[] failedChecks=Array.Empty<string>();public FrameEvidence[] frames=Array.Empty<FrameEvidence>();
        }

        public static string ManifestPath(string candidatePath)=>candidatePath+".contact.json";
        public static string ReportPath(string candidatePath)=>Path.GetFullPath("../TestResults/MeshyContact/"+Path.GetFileNameWithoutExtension(candidatePath)+".json");

        public static Report Generate(GameObject targetPrefab,Avatar targetAvatar,AnimationClip sourceClip,ContactPolicy policy,string outputPath)
        {
            ValidateRequest(targetPrefab,targetAvatar,sourceClip,policy,outputPath);
            var report=NewReport("Generate",outputPath);var inputs=RecordInputs(targetPrefab,targetAvatar,sourceClip);
            try
            {
                var intervals=Mathf.Max(8,Mathf.CeilToInt(sourceClip.length*policy.authorHz));var poses=new HumanPose[intervals+1];var left=new float[intervals+1];var right=new float[intervals+1];var supports=new Support[intervals+1];float humanScale;
                using(var sampler=new TargetSampler(targetPrefab,targetAvatar))
                {
                    humanScale=sampler.HumanScale;sampler.SetClip(sourceClip,policy.useSourceFootIK);
                    for(var i=0;i<=intervals;i++)
                    {
                        var phase=i/(float)intervals;sampler.Evaluate(sourceClip.length*phase);poses[i]=sampler.ReadHumanPose();var skin=sampler.Measure();left[i]=skin.left.minimumY;right[i]=skin.right.minimumY;supports[i]=SupportAt(policy,phase);
                    }
                }
                var correction=SolveCorrections(left,right,supports,policy.loop,policy.groundY,policy.maximumBodyCorrection);
                var clip=Bake(sourceClip,poses,correction,humanScale,policy,Path.GetFileNameWithoutExtension(outputPath));
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);AssetDatabase.Refresh();var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
                    if(existing==null)AssetDatabase.CreateAsset(clip,outputPath);else{EditorUtility.CopySerialized(clip,existing);EditorUtility.SetDirty(existing);}AssetDatabase.SaveAssets();
                }
                finally{if(!AssetDatabase.Contains(clip))Object.DestroyImmediate(clip);}
                var manifest=new Manifest{targetPrefab=AssetDatabase.GetAssetPath(targetPrefab),targetAvatar=AssetDatabase.GetAssetPath(targetAvatar),sourceClip=AssetDatabase.GetAssetPath(sourceClip),candidateClip=outputPath,candidateHash=Hash(outputPath),policy=policy,authorIntervals=intervals,targetHumanScale=humanScale,correctionMeters=correction,immutableInputs=inputs};
                CheckInputs(inputs);File.WriteAllText(ManifestPath(outputPath),JsonUtility.ToJson(manifest,true));AssetDatabase.ImportAsset(ManifestPath(outputPath));AssetDatabase.SaveAssets();
                Audit(manifest,report);CheckInputs(inputs);report.originalAssetsUnchanged=true;return report;
            }
            catch(Exception e){report.error=e.ToString();throw;}
            finally{WriteReport(report);}
        }

        /// <summary>Read-only fresh evidence; no reimport, reauthoring or asset writes.</summary>
        public static Report Verify(string candidatePath)
        {
            var report=NewReport("Verify",candidatePath);
            try
            {
                var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath(candidatePath)))??throw new InvalidOperationException("Missing target-contact provenance.");
                Require(manifest.version==1&&manifest.candidateClip==candidatePath,"Unknown target-contact provenance.");Require(Hash(candidatePath)==manifest.candidateHash,"Candidate differs from its sampled contact provenance.");CheckInputs(manifest.immutableInputs);
                Audit(manifest,report);CheckInputs(manifest.immutableInputs);report.originalAssetsUnchanged=true;return report;
            }
            catch(Exception e){report.error=e.ToString();throw;}
            finally{WriteReport(report);}
        }

        public static void ValidatePolicy(ContactPolicy policy)
        {
            if(policy==null)throw new InvalidOperationException("Contact policy is missing.");Require(!string.IsNullOrWhiteSpace(policy.id),"Contact policy must have an explicit ID.");
            Require(policy.authorHz>=16&&policy.authorHz<=240&&policy.validationSubdivisions>=2&&policy.validationSubdivisions<=8,"Invalid deterministic contact sample rates.");
            Require(Finite(policy.groundY)&&Finite(policy.contactTolerance)&&policy.contactTolerance>0&&policy.contactTolerance<=.01f,"Contact floor/tolerance invalid.");
            Require(Finite(policy.flatSoleHoverTolerance)&&policy.flatSoleHoverTolerance>=policy.contactTolerance&&Finite(policy.maximumBodyCorrection)&&policy.maximumBodyCorrection>0,"Contact correction/tolerance invalid.");
            if(policy.supportKeys==null)throw new InvalidOperationException("Explicit support schedule required.");Require(policy.supportKeys.Length>=2,"Explicit support schedule required; do not auto-pin airborne clips.");
            Require(policy.supportKeys[0].phase==0f&&policy.supportKeys[policy.supportKeys.Length-1].phase==1f,"Support schedule must explicitly include phase0 and phase1.");
            for(var i=0;i<policy.supportKeys.Length;i++)
            {
                var key=policy.supportKeys[i];if(key==null)throw new InvalidOperationException("Missing support key.");Require(Finite(key.phase)&&key.phase>=0&&key.phase<=1&&(int)key.support>=0&&(int)key.support<=3,"Invalid support key.");if(i>0)Require(key.phase>policy.supportKeys[i-1].phase,"Support phases must be strictly ordered.");
            }
            Require(policy.supportKeys.Any(k=>k.support!=Support.Airborne),"All-airborne clip has no ground baseline; explicit different adapter required.");
            if(policy.loop)Require(policy.supportKeys[0].support==policy.supportKeys[policy.supportKeys.Length-1].support,"Loop endpoint support must match.");
            Require(Finite(policy.expectedPlanarDisplacementPerCycle)&&Mathf.Abs(policy.expectedPlanarDisplacementPerCycle.y)<1e-6f,"External comparison baseline must be planar; it is never applied to gameplay/root.");
        }

        public static Support SupportAt(ContactPolicy policy,float phase)
        {
            Require(Finite(phase)&&phase>=0&&phase<=1,"Contact phase out of range.");
            for(var i=policy.supportKeys.Length-1;i>=0;i--)if(phase>=policy.supportKeys[i].phase)return policy.supportKeys[i].support;
            throw new InvalidOperationException("Support schedule omitted phase zero.");
        }

        /// <summary>Pure measured solver. No time queries, random input or global fixed offset.</summary>
        public static float[] SolveCorrections(float[] leftMinimum,float[] rightMinimum,Support[] support,bool loop,float floor,float maximumCorrection)
        {
            if(leftMinimum==null||rightMinimum==null||support==null)throw new InvalidOperationException("Contact samples missing.");Require(leftMinimum.Length==rightMinimum.Length&&leftMinimum.Length==support.Length&&support.Length>=3,"Contact samples mismatch.");
            Require(leftMinimum.All(Finite)&&rightMinimum.All(Finite)&&Finite(floor)&&Finite(maximumCorrection)&&maximumCorrection>0,"Nonfinite/invalid contact input.");
            Require(support.All(s=>(int)s>=0&&(int)s<=3),"Unknown support bits.");if(loop)Require(support[0]==support[support.Length-1],"Loop support endpoint mismatch.");
            var count=loop?support.Length-1:support.Length;var result=new float[support.Length];var anchors=new List<int>();
            for(var i=0;i<count;i++)
            {
                if(support[i]==Support.Airborne){result[i]=float.NaN;continue;}
                // Lowest explicit support establishes baseline. A lower other
                // foot cannot be allowed to penetrate unnoticed: lifting to
                // clear it may cause support hover, which Audit FAILS honestly.
                var minimum=support[i]==Support.Left?leftMinimum[i]:support[i]==Support.Right?rightMinimum[i]:Mathf.Min(leftMinimum[i],rightMinimum[i]);
                result[i]=Mathf.Max(floor-minimum,floor-Mathf.Min(leftMinimum[i],rightMinimum[i]));anchors.Add(i);
            }
            Require(anchors.Count>0,"Sampled clip has no grounded interval.");
            for(var i=0;i<count;i++)if(float.IsNaN(result[i]))
            {
                var previous=anchors.LastOrDefault(a=>a<i);var next=anchors.FirstOrDefault(a=>a>i);var hasPrevious=anchors.Any(a=>a<i);var hasNext=anchors.Any(a=>a>i);
                if(!hasPrevious)previous=loop?anchors[anchors.Count-1]-count:anchors[0];if(!hasNext)next=loop?anchors[0]+count:anchors[anchors.Count-1];
                var p=result[(previous%count+count)%count];var n=result[(next%count+count)%count];var t=next==previous?0f:Mathf.Clamp01((i-previous)/(float)(next-previous));t=t*t*(3f-2f*t);result[i]=Mathf.Lerp(p,n,t);
            }
            if(loop)
            {
                // An airborne endpoint uses the cyclic baseline. A grounded
                // endpoint is independently measured: never force a fake zero
                // seam when the supplied source endpoints actually disagree.
                var last=result.Length-1;result[last]=support[last]==Support.Airborne?result[0]:floor-Mathf.Min(leftMinimum[last],rightMinimum[last]);
            }
            Require(result.All(v=>Finite(v)&&Mathf.Abs(v)<=maximumCorrection),"Measured correction exceeds explicit bound; reject misfit instead of silently lifting character.");return result;
        }

        private static AnimationClip Bake(AnimationClip source,HumanPose[] poses,float[] correction,float humanScale,ContactPolicy policy,string name)
        {
            Require(Finite(humanScale)&&humanScale>0,"Target humanScale invalid.");var clip=new AnimationClip{name=name,frameRate=policy.authorHz,legacy=false};
            try
            {
                var names=HumanTrait.MuscleName.Concat(new[]{"RootT.x","RootT.y","RootT.z","RootQ.x","RootQ.y","RootQ.z","RootQ.w"}).ToArray();var keys=names.Select(_=>new List<Keyframe>()).ToArray();var previous=Quaternion.identity;
                for(var i=0;i<poses.Length;i++)
                {
                    var pose=poses[i];ValidatePose(pose);var rotation=pose.bodyRotation;if(i>0&&Quaternion.Dot(previous,rotation)<0)rotation=new Quaternion(-rotation.x,-rotation.y,-rotation.z,-rotation.w);previous=rotation;
                    // HumanPose body position is already normalized. Apply this
                    // world-metre delta divided by target scale ONCE, Y only.
                    var values=pose.muscles.Concat(new[]{pose.bodyPosition.x,pose.bodyPosition.y+correction[i]/humanScale,pose.bodyPosition.z,rotation.x,rotation.y,rotation.z,rotation.w}).ToArray();var time=source.length*i/(poses.Length-1f);
                    for(var channel=0;channel<values.Length;channel++)keys[channel].Add(new Keyframe(time,values[channel]));
                }
                for(var i=0;i<names.Length;i++)
                {
                    var curve=new AnimationCurve(keys[i].ToArray());for(var k=0;k<curve.length;k++){AnimationUtility.SetKeyLeftTangentMode(curve,k,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,k,AnimationUtility.TangentMode.Linear);}AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),names[i]),curve);
                }
                var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=policy.loop;settings.loopBlend=false;settings.loopBlendPositionY=true;settings.loopBlendPositionXZ=true;settings.loopBlendOrientation=true;settings.keepOriginalPositionY=true;settings.keepOriginalPositionXZ=true;settings.keepOriginalOrientation=true;AnimationUtility.SetAnimationClipSettings(clip,settings);
                Require(clip.humanMotion,"Contact candidate did not produce real Humanoid curves.");return clip;
            }
            catch{Object.DestroyImmediate(clip);throw;}
        }

        private static void Audit(Manifest manifest,Report report)
        {
            var prefab=Load<GameObject>(manifest.targetPrefab);var avatar=Load<Avatar>(manifest.targetAvatar);var original=Load<AnimationClip>(manifest.sourceClip);var candidate=Load<AnimationClip>(manifest.candidateClip);var policy=manifest.policy;
            ValidateRequest(prefab,avatar,original,policy,manifest.candidateClip);Require(candidate.humanMotion,"Contact candidate is not Humanoid.");var count=manifest.authorIntervals*policy.validationSubdivisions;var frames=new List<FrameEvidence>();
            using(var sourceSampler=new TargetSampler(prefab,avatar))using(var candidateSampler=new TargetSampler(prefab,avatar))
            {
                Require(Mathf.Abs(sourceSampler.HumanScale-manifest.targetHumanScale)<1e-5f,"Target Avatar scale changed since contact generation.");sourceSampler.SetClip(original,policy.useSourceFootIK);candidateSampler.SetClip(candidate);report.leftSoleVertices=sourceSampler.LeftCount;report.rightSoleVertices=sourceSampler.RightCount;
                for(var i=0;i<=count;i++)
                {
                    var phase=i/(float)count;sourceSampler.Evaluate(phase*original.length);candidateSampler.Evaluate(phase*candidate.length);var interval=phase*manifest.authorIntervals;var left=Mathf.Min(manifest.authorIntervals,Mathf.FloorToInt(interval));var right=Mathf.Min(manifest.authorIntervals,left+1);
                    frames.Add(new FrameEvidence{phase=phase,support=SupportAt(policy,phase),correctionMeters=Mathf.Lerp(manifest.correctionMeters[left],manifest.correctionMeters[right],interval-left),original=sourceSampler.Measure(),candidate=candidateSampler.Measure()});
                }
            }
            report.frames=frames.ToArray();report.validationSamples=frames.Count;report.airborneSamples=frames.Count(f=>f.support==Support.Airborne);report.groundedSamples=frames.Count-report.airborneSamples;report.maximumAbsoluteCorrection=manifest.correctionMeters.Max(v=>Mathf.Abs(v));
            report.minimumCandidateFootY=frames.Min(f=>Mathf.Min(f.candidate.left.minimumY,f.candidate.right.minimumY));report.minimumCandidateSkinY=frames.Min(f=>f.candidate.skinMinimumY);
            foreach(var frame in frames)
            {
                foreach(var pair in new[]{(Support.Left,frame.candidate.left),(Support.Right,frame.candidate.right)})if((frame.support&pair.Item1)!=0)
                {
                    report.maximumStanceHover=Mathf.Max(report.maximumStanceHover,pair.Item2.minimumY-policy.groundY);report.maximumFlatSoleHover=Mathf.Max(report.maximumFlatSoleHover,Mathf.Max(pair.Item2.heelMinimumY,pair.Item2.toeMinimumY)-policy.groundY);
                }
                if(frame.support==Support.Airborne)report.maximumAirborneHeightDelta=Mathf.Max(report.maximumAirborneHeightDelta,Mathf.Abs(Mathf.Min(frame.candidate.left.minimumY,frame.candidate.right.minimumY)-Mathf.Min(frame.original.left.minimumY,frame.original.right.minimumY)));
                report.maximumExternalActorTranslation=Mathf.Max(report.maximumExternalActorTranslation,frame.candidate.actorRootPosition.magnitude);report.maximumExternalActorRotation=Mathf.Max(report.maximumExternalActorRotation,Quaternion.Angle(frame.candidate.actorRootRotation,Quaternion.identity));
            }
            for(var i=1;i<frames.Count;i++)foreach(var side in new[]{Support.Left,Support.Right})if((frames[i-1].support&side)!=0&&(frames[i].support&side)!=0)
            {
                var a=side==Support.Left?frames[i-1].candidate.left.supportPoint:frames[i-1].candidate.right.supportPoint;var b=side==Support.Left?frames[i].candidate.left.supportPoint:frames[i].candidate.right.supportPoint;report.maximumRelativeStancePointStep=Mathf.Max(report.maximumRelativeStancePointStep,Vector3.ProjectOnPlane(b-a,Vector3.up).magnitude);
                if(policy.externalPlanarMotionKnown)report.maximumPredictedWorldStancePointStep=Mathf.Max(report.maximumPredictedWorldStancePointStep,Vector3.ProjectOnPlane(b-a+(frames[i].phase-frames[i-1].phase)*policy.expectedPlanarDisplacementPerCycle,Vector3.up).magnitude);
            }
            report.slidingStatus=policy.externalPlanarMotionKnown?"Predicted world stance step uses explicit planar comparison baseline only; actor/gameplay not moved. Full rendered stance/transition acceptance pending.":"World sliding NOT_EVALUATED: no external planar-motion comparison baseline supplied. Relative in-place stance-point steps reported without calling them world slip.";
            report.loopCorrectionSeam=Mathf.Abs(manifest.correctionMeters[0]-manifest.correctionMeters[manifest.correctionMeters.Length-1]);MeasureLoop(candidate,manifest.targetHumanScale,report);
            var failures=new List<string>();if(report.minimumCandidateFootY<policy.groundY-policy.contactTolerance)failures.Add("FOOT_PENETRATION_AT_VALIDATION_PHASE");if(report.minimumCandidateSkinY<policy.groundY-policy.contactTolerance)failures.Add("WHOLE_SKIN_PENETRATION_NOT_HIDDEN_BY_FOOT_ROOTING");if(report.maximumStanceHover>policy.contactTolerance)failures.Add("SUPPORTED_FOOT_HOVER_REQUIRES_LOCAL_IK_OR_SOURCE_FIX");if(policy.requireHeelAndToeSupport&&report.maximumFlatSoleHover>policy.flatSoleHoverTolerance)failures.Add("HEEL_TOE_FLAT_CONTACT_REQUIRES_ANKLE_IK_OR_SOURCE_FIX");
            if(report.maximumExternalActorTranslation>1e-5f||report.maximumExternalActorRotation>.01f)failures.Add("EXTERNAL_GAMEPLAY_ACTOR_ROOT_MOVED");if(policy.loop&&(report.loopCorrectionSeam>.001f||report.loopMuscleSeam>.001f||report.loopBodySeam>.001f||report.loopBodyAngleSeam>.1f))failures.Add("LOOP_ENDPOINT_DISCONTINUITY");
            report.failedChecks=failures.ToArray();report.status=failures.Count>0?"CONTACT_CANDIDATE_QUALITY_FAIL":"CONTACT_NUMERIC_CHECKS_PASS_WINDOWS_VISUAL_SLIDING_PENDING";
        }

        private sealed class FootRegion
        {
            public int[] all=Array.Empty<int>();public int[] heel=Array.Empty<int>();public int[] toe=Array.Empty<int>();
        }
        internal sealed class TargetSampler:IDisposable
        {
            private GameObject? actor,stationaryFrame;private Animator animator=null!;private SkinnedMeshRenderer skin=null!;private FootRegion left=null!,right=null!;private PlayableGraph graph;private AnimationClipPlayable playable;private HumanPoseHandler? handler;private Mesh? baked;
            private Transform[] transforms=Array.Empty<Transform>();private Vector3[] positions=Array.Empty<Vector3>(),scales=Array.Empty<Vector3>();private Quaternion[] rotations=Array.Empty<Quaternion>();
            public float HumanScale=>animator.humanScale;public int LeftCount=>left.all.Length;public int RightCount=>right.all.Length;
            public TargetSampler(GameObject prefab,Avatar avatar)
            {
                try
                {
                    stationaryFrame=new GameObject("Stationary contact gameplay-frame proxy");actor=Object.Instantiate(prefab,stationaryFrame.transform);actor.name=prefab.name;actor.SetActive(true);actor.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);Require((actor.transform.localScale-Vector3.one).sqrMagnitude<1e-8f,"Contact actor root must have unit scale.");animator=actor.GetComponent<Animator>();if(animator==null)throw new InvalidOperationException("Target requires an Animator.");animator.enabled=false;animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                    FOC.Presentation.Visuals.MeshyHasanPilotPlayer.RestoreSourceBindPose(prefab,actor);
                    transforms=actor.GetComponentsInChildren<Transform>(true);positions=transforms.Select(t=>t.localPosition).ToArray();rotations=transforms.Select(t=>t.localRotation).ToArray();scales=transforms.Select(t=>t.localScale).ToArray();animator.avatar=avatar;Restore();
                    skin=actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s=>s.sharedMesh!=null).OrderByDescending(s=>s.sharedMesh.triangles.Length).First();Require(skin.sharedMesh.isReadable,"Target skin must be readable for measured contact authoring.");left=Region(true);right=Region(false);baked=new Mesh();handler=new HumanPoseHandler(avatar,actor.transform);
                }
                catch{Dispose();throw;}
            }
            private void Restore(){for(var i=0;i<transforms.Length;i++){transforms[i].localPosition=positions[i];transforms[i].localRotation=rotations[i];transforms[i].localScale=scales[i];}}
            public void SetClip(AnimationClip clip,bool useFootIK=false)
            {
                if(graph.IsValid())graph.Destroy();Restore();animator.enabled=true;animator.Rebind();animator.applyRootMotion=false;graph=PlayableGraph.Create("Measured target-contact sample");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(useFootIK);playable.SetApplyPlayableIK(false);AnimationPlayableOutput.Create(graph,"Original or candidate Humanoid",animator).SetSourcePlayable(playable);graph.Play();graph.Evaluate(0);
            }
            public void Evaluate(float time,float presentationOffset=0){actor!.transform.localPosition=Vector3.zero;playable.SetTime(time);graph.Evaluate(0);actor.transform.localPosition=Vector3.up*presentationOffset;}
            public HumanPose ReadHumanPose(){var pose=new HumanPose{muscles=new float[HumanTrait.MuscleCount]};handler!.GetHumanPose(ref pose);ValidatePose(pose);return pose;}
            public SkinMeasurement Measure()
            {
                var vertices=Vertices();
                return new SkinMeasurement{left=MeasureFoot(left,vertices),right=MeasureFoot(right,vertices),skinMinimumY=vertices.Min(v=>v.y),actorRootPosition=stationaryFrame!.transform.position,actorRootRotation=stationaryFrame.transform.rotation};
            }
            public Vector3[] Vertices(){skin.BakeMesh(baked!);var vertices=baked!.vertices.Select(v=>stationaryFrame!.transform.InverseTransformPoint(skin.transform.TransformPoint(v))).ToArray();Require(vertices.Length>0&&vertices.All(Finite),"Contact sample produced nonfinite/empty skin.");return vertices;}
            private static FootMeasurement MeasureFoot(FootRegion region,Vector3[] vertices)
            {
                var lowest=region.all.OrderBy(i=>vertices[i].y).First();return new FootMeasurement{minimumY=vertices[lowest].y,maximumY=region.all.Max(i=>vertices[i].y),heelMinimumY=region.heel.Min(i=>vertices[i].y),toeMinimumY=region.toe.Min(i=>vertices[i].y),supportPoint=vertices[lowest]};
            }
            private FootRegion Region(bool isLeft)
            {
                Transform Mapped(HumanBodyBones bone){var entry=animator.avatar.humanDescription.human.Single(h=>h.humanName==bone.ToString());return transforms.Single(t=>t.name==entry.boneName);}
                var foot=Mapped(isLeft?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);var toes=Mapped(isLeft?HumanBodyBones.LeftToes:HumanBodyBones.RightToes);var indices=new HashSet<int>();var bones=skin.bones;for(var i=0;i<bones.Length;i++)if(bones[i]==foot||bones[i]==toes)indices.Add(i);
                Require(indices.Count>=1,"Target weighted foot bones absent.");var vertices=skin.sharedMesh.vertices;var weights=skin.sharedMesh.boneWeights;var candidates=new List<int>();var points=new Dictionary<int,Vector3>();
                for(var i=0;i<vertices.Length;i++)
                {
                    var w=weights[i];var influence=(indices.Contains(w.boneIndex0)?w.weight0:0)+(indices.Contains(w.boneIndex1)?w.weight1:0)+(indices.Contains(w.boneIndex2)?w.weight2:0)+(indices.Contains(w.boneIndex3)?w.weight3:0);if(influence<.5f)continue;var point=skin.transform.TransformPoint(vertices[i]);if(point.y>foot.position.y+.02f)continue;candidates.Add(i);points.Add(i,point);
                }
                Require(candidates.Count>=12,"Too few measured foot surface vertices.");var min=candidates.Min(i=>points[i].y);var max=candidates.Max(i=>points[i].y);var sole=candidates.Where(i=>points[i].y<=min+.3f*(max-min)).ToArray();Require(sole.Length>=6,"Insufficient lower-sole topology.");var forward=Vector3.ProjectOnPlane(toes.position-foot.position,Vector3.up).normalized;Require(forward.sqrMagnitude>.99f,"Degenerate physical toe heading.");var projection=sole.ToDictionary(i=>i,i=>Vector3.Dot(points[i]-foot.position,forward));var near=projection.Values.Min();var far=projection.Values.Max();Require(far-near>.03f,"Foot longitudinal span too short.");
                var heel=sole.Where(i=>projection[i]<=near+.3f*(far-near)).ToArray();var toe=sole.Where(i=>projection[i]>=far-.3f*(far-near)).ToArray();Require(heel.Length>0&&toe.Length>0,"Missing measured heel/toe regions.");return new FootRegion{all=sole,heel=heel,toe=toe};
            }
            public void Dispose(){if(graph.IsValid())graph.Destroy();handler?.Dispose();handler=null;if(baked!=null)Object.DestroyImmediate(baked);baked=null;if(stationaryFrame!=null)Object.DestroyImmediate(stationaryFrame);else if(actor!=null)Object.DestroyImmediate(actor);stationaryFrame=null;actor=null;}
        }

        private static void MeasureLoop(AnimationClip clip,float scale,Report report)
        {
            foreach(var name in HumanTrait.MuscleName){var curve=AnimationUtility.GetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),name));if(curve==null)throw new InvalidOperationException("Missing candidate muscle channel.");report.loopMuscleSeam=Mathf.Max(report.loopMuscleSeam,Mathf.Abs(curve.Evaluate(0)-curve.Evaluate(clip.length)));}
            float V(string name,float time)=>AnimationUtility.GetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),name)).Evaluate(time);Vector3 P(float t)=>new Vector3(V("RootT.x",t),V("RootT.y",t),V("RootT.z",t));Quaternion Q(float t)=>new Quaternion(V("RootQ.x",t),V("RootQ.y",t),V("RootQ.z",t),V("RootQ.w",t));report.loopBodySeam=Vector3.Distance(P(0),P(clip.length))*scale;report.loopBodyAngleSeam=Quaternion.Angle(Q(0),Q(clip.length));
        }
        private static void ValidateRequest(GameObject prefab,Avatar avatar,AnimationClip clip,ContactPolicy policy,string output)
        {
            if(prefab==null||avatar==null||clip==null)throw new InvalidOperationException("Contact cleanup target, Avatar or source clip missing.");Require(avatar.isHuman&&avatar.isValid&&clip.humanMotion&&clip.length>0,"Contact cleanup requires real target, valid chosen Humanoid Avatar, and Humanoid source clip.");ValidatePolicy(policy);
            Require(policy.referenceAndSourcePoseReviewed&&!string.IsNullOrWhiteSpace(policy.sourcePoseGateEvidence),"Reference/source-pose acceptance evidence required before authoring contact candidates.");
            Require(output.StartsWith(OutputRoot+"/",StringComparison.Ordinal)&&output.EndsWith(".anim",StringComparison.Ordinal)&&!output.Contains(".."),"Contact output must be a NEW isolated candidate path.");Require(AssetDatabase.GetAssetPath(clip)!=output,"Never overwrite the source animation.");
            Require(AnimationUtility.GetAnimationClipSettings(clip).loopTime==policy.loop,"Loop policy must match reviewed source settings; no hidden looping change.");
        }
        internal static InputHash[] RecordInputs(params Object[] objects)
        {
            var paths=objects.Select(AssetDatabase.GetAssetPath).ToArray();Require(paths.All(p=>p.StartsWith("Assets/",StringComparison.Ordinal)&&File.Exists(p)),"Contact inputs must be saved assets with provenance.");return paths.Concat(AssetDatabase.GetDependencies(paths,true)).Where(p=>p.StartsWith("Assets/",StringComparison.Ordinal)&&File.Exists(p)).Distinct().SelectMany(p=>File.Exists(p+".meta")?new[]{p,p+".meta"}:new[]{p}).OrderBy(p=>p,StringComparer.Ordinal).Select(p=>new InputHash{path=p,sha256=Hash(p)}).ToArray();
        }
        internal static string Hash(string path){using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
        internal static void CheckInputs(IEnumerable<InputHash> inputs){foreach(var input in inputs)Require(Hash(input.path)==input.sha256||MeshyLod2Derivation.AllowsInput(input.path,input.sha256),"Original contact input changed: "+input.path);}
        private static T Load<T>(string path) where T:Object=>AssetDatabase.LoadAssetAtPath<T>(path)??throw new InvalidOperationException("Missing contact input "+path);
        private static void ValidatePose(HumanPose pose)=>Require(pose.muscles!=null&&pose.muscles.Length==HumanTrait.MuscleCount&&pose.muscles.All(Finite)&&Finite(pose.bodyPosition)&&Finite(pose.bodyRotation.x)&&Finite(pose.bodyRotation.y)&&Finite(pose.bodyRotation.z)&&Finite(pose.bodyRotation.w),"Nonfinite Humanoid contact sample.");
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);private static bool Finite(Vector3 value)=>Finite(value.x)&&Finite(value.y)&&Finite(value.z);
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        private static Report NewReport(string operation,string path)=>new Report{operation=operation,candidate=path,utc=DateTime.UtcNow.ToString("O"),unityVersion=Application.unityVersion};
        private static void WriteReport(Report report){var path=ReportPath(report.candidate);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,JsonUtility.ToJson(report,true));}
    }
}
