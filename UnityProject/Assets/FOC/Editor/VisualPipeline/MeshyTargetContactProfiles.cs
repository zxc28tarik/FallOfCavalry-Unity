#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FOC.Presentation.Visuals;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
using Support=FOC.Editor.Visuals.MeshyTargetContact.Support;

namespace FOC.Editor.Visuals
{
    /// <summary>
    /// Original Humanoid clip + existing FootIK, with measured presentation-only
    /// translation. No HumanPose extraction/rebake: that separate experimental
    /// roundtrip can lose target deformation/stretch and is not this pipeline.
    /// </summary>
    public static class MeshyTargetContactProfiles
    {
        public const string OutputRoot=MeshyTargetContact.OutputRoot+"/Profiles";
        public static readonly string[] ReviewedNames={"Idle_Loop","Sword_Attack","Crouch_Idle_Loop","Idle_Torch_Loop"};
        public static string ReportRoot=>Path.GetFullPath("../TestResults/MeshyContact");
        [Serializable] public sealed class Manifest
        {
            public int version=1;public string prefab="",profile="",profileHash="",sourceClipName="";
            public MeshyTargetContact.ContactPolicy policy=new MeshyTargetContact.ContactPolicy();
            public MeshyTargetContact.InputHash[] immutableInputs=Array.Empty<MeshyTargetContact.InputHash>();
        }
        [Serializable] public sealed class Frame
        {
            public float phase,correctionMeters,maximumSkinDeviationFromPureTranslation;
            public Support support;
            public MeshyTargetContact.SkinMeasurement original=new MeshyTargetContact.SkinMeasurement(),adapted=new MeshyTargetContact.SkinMeasurement();
        }
        [Serializable] public sealed class Report
        {
            public string status="NOT_COMPLETED",operation="",utc="",unityVersion="",error="",profile="",sourceClip="",targetAvatar="";
            public bool originalAssetsUnchanged,useFootIK,sourceClipPreserved=true,humanoidRebaked=false;
            public int authorIntervals,validationSamples,leftSoleVertices,rightSoleVertices;
            public float minimumSkinY,minimumFootY,maximumStanceHover,maximumHeelToeHover,maximumAbsoluteCorrection,maximumSkinDeviationFromPureTranslation,maximumExternalActorRootTravel,maximumExternalActorRootRotation;
            public float loopCorrectionSeam,loopSkinSeam,maximumRelativeStanceStep;
            public string method="Actual original clip + chosen Avatar + FootIK sampled on fixed actor frame; only Animator presentation child local Y receives measured metre curve after graph evaluation. Reset child baseline before each evaluation. No HumanPose rebake or humanScale division.";
            public string limitations="World/terrain stance locking, rendered heel/toe/contact transitions and cloth acceptance are separate visual gates. Body Y cannot independently plant two differently-heighted feet; support hover remains a hard failure.";
            public string visualAcceptance="PENDING_WINDOWS_PLAYER_REVIEW";
            public string[] failedChecks=Array.Empty<string>();public Frame[] frames=Array.Empty<Frame>();
        }
        [Serializable] public sealed class BatchReport
        {
            public string status="NOT_COMPLETED",utc="",error="";public Report[] reports=Array.Empty<Report>();
        }

        public static string ProfilePath(string clipSuffix)=>OutputRoot+"/CTC_"+clipSuffix+".asset";
        public static MeshyTargetContactProfile[] LoadReviewedLibraryProfiles()=>ReviewedNames.Select(n=>AssetDatabase.LoadAssetAtPath<MeshyTargetContactProfile>(ProfilePath(n))??throw new InvalidOperationException("Missing measured contact profile "+n)).ToArray();
        public static void RunGenerateReviewedLibrary()=>RunBatch(true);
        public static void RunVerifyReviewedLibrary()=>RunBatch(false);

        private static void RunBatch(bool generate)
        {
            var batch=new BatchReport{utc=DateTime.UtcNow.ToString("O")};var reports=new List<Report>();var exit=0;
            try
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(MeshyHasanPilotPipeline.PrefabPath)??throw new InvalidOperationException("Meshy target missing.");var avatar=MeshyHumanoidCalibration.LoadTargetAvatar();
                foreach(var name in ReviewedNames)
                {
                    reports.Add(generate?GenerateProfile(prefab,avatar,MeshyMotionLibraryIntake.Clip(name),ReviewedPolicy(name),ProfilePath(name)):VerifyProfile(ProfilePath(name)));
                }
                batch.status=reports.All(r=>r.failedChecks.Length==0)?"CONTACT_NUMERIC_CHECKS_PASS_WINDOWS_PENDING":"CONTACT_QUALITY_FAIL";
                if(reports.Any(r=>r.failedChecks.Length>0))exit=2;
            }
            catch(Exception e){batch.error=e.ToString();Debug.LogException(e);exit=1;}
            finally{batch.reports=reports.ToArray();Directory.CreateDirectory(ReportRoot);File.WriteAllText(Path.Combine(ReportRoot,"profile-batch.json"),JsonUtility.ToJson(batch,true));}
            EditorApplication.Exit(exit);
        }

        public static MeshyTargetContact.ContactPolicy ReviewedPolicy(string name)
        {
            if(!ReviewedNames.Contains(name))throw new InvalidOperationException("This reviewed four-clip policy does not cover locomotion/airborne clips.");
            var sword=name=="Sword_Attack";
            return new MeshyTargetContact.ContactPolicy
            {
                id="meshy-ual-footik-presentation-contact-"+name,loop=!sword,useSourceFootIK=true,authorHz=60,validationSubdivisions=2,
                groundY=0,maximumBodyCorrection=.12f,contactTolerance=.002f,flatSoleHoverTolerance=.006f,requireHeelAndToeSupport=!sword,
                referenceAndSourcePoseReviewed=true,
                sourcePoseGateEvidence="Root visual review: TestResults/MeshyMotionClosure/Trial3 and Trial4FootIK (calibrated source-retarget, Sword/Crouch .35; original choreography retained). Numeric paired evidence: TestResults/MeshyContact/foot-ik-comparison.json. This permits contact authoring, not final motion acceptance.",
                supportProvenance=sword?"FootIK 60Hz measured support + reviewed Trial4. Both start/end; Left support [.15,.80), while the right foot is intentionally lifted through the downstroke. No mirror/choreography edit. Dual contact outside that interval remains audited.":name=="Crouch_Idle_Loop"?"FootIK 60Hz measured support + reviewed Trial4. Crouch holds the left foot as the explicit planted support; the right foot is a raised/repositioning leg in this source choreography. No mirror/choreography edit; no inference of dual support.":"Explicit Both support for reviewed grounded idle/torch source. Any dual-contact mismatch remains FAIL; no airborne inference.",
                supportKeys=sword?new[]{K(0,Support.Left),K(.25f,Support.Right),K(.8f,Support.Both),K(1,Support.Both)}:name=="Crouch_Idle_Loop"?new[]{K(0,Support.Left),K(1,Support.Left)}:new[]{K(0,Support.Both),K(1,Support.Both)}
            };
        }
        private static MeshyTargetContact.SupportKey K(float p,Support s)=>new MeshyTargetContact.SupportKey{phase=p,support=s};

        public static Report GenerateProfile(GameObject prefab,Avatar avatar,AnimationClip clip,MeshyTargetContact.ContactPolicy policy,string path)
        {
            Validate(prefab,avatar,clip,policy,path);var inputs=MeshyTargetContact.RecordInputs(prefab,avatar,clip);var count=Mathf.Max(8,Mathf.CeilToInt(clip.length*policy.authorHz));var left=new float[count+1];var right=new float[count+1];var supports=new Support[count+1];
            using(var sample=new MeshyTargetContact.TargetSampler(prefab,avatar))
            {
                sample.SetClip(clip,policy.useSourceFootIK);
                for(var i=0;i<=count;i++){var phase=i/(float)count;sample.Evaluate(phase*clip.length);var skin=sample.Measure();left[i]=skin.left.minimumY;right[i]=skin.right.minimumY;supports[i]=MeshyTargetContact.SupportAt(policy,phase);}
            }
            var correction=MeshyTargetContact.SolveCorrections(left,right,supports,policy.loop,policy.groundY,policy.maximumBodyCorrection);
            Directory.CreateDirectory(OutputRoot);AssetDatabase.Refresh();var profile=AssetDatabase.LoadAssetAtPath<MeshyTargetContactProfile>(path);var created=profile==null;
            if(created)profile=ScriptableObject.CreateInstance<MeshyTargetContactProfile>();
            try
            {
                profile!.name=Path.GetFileNameWithoutExtension(path);profile.sourceClip=clip;profile.targetAvatar=avatar;profile.useFootIK=policy.useSourceFootIK;profile.loop=policy.loop;profile.groundY=policy.groundY;profile.maximumBodyCorrection=policy.maximumBodyCorrection;profile.correctionMeters=correction;profile.sourcePoseGateEvidence=policy.sourcePoseGateEvidence;profile.supportProvenance=policy.supportProvenance;
                if(created)AssetDatabase.CreateAsset(profile,path);else EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();
                MeshyTargetContact.CheckInputs(inputs);
                var manifest=new Manifest{prefab=AssetDatabase.GetAssetPath(prefab),profile=path,profileHash=MeshyTargetContact.Hash(path),sourceClipName=clip.name,policy=policy,immutableInputs=inputs};
                File.WriteAllText(path+".contact.json",JsonUtility.ToJson(manifest,true));AssetDatabase.ImportAsset(path+".contact.json");
                return Audit(manifest,"Generate");
            }
            finally{if(profile!=null&&!AssetDatabase.Contains(profile))Object.DestroyImmediate(profile);}
        }

        public static Report VerifyProfile(string path)
        {
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(path+".contact.json"))??throw new InvalidOperationException("Contact profile provenance missing.");
            if(manifest.version!=1||manifest.profile!=path||MeshyTargetContact.Hash(path)!=manifest.profileHash)throw new InvalidOperationException("Contact profile/provenance mismatch.");
            return Audit(manifest,"Verify");
        }

        private static Report Audit(Manifest manifest,string operation)
        {
            var report=new Report{operation=operation,profile=manifest.profile,utc=DateTime.UtcNow.ToString("O"),unityVersion=Application.unityVersion};
            try
            {
                MeshyTargetContact.CheckInputs(manifest.immutableInputs);
                var profile=AssetDatabase.LoadAssetAtPath<MeshyTargetContactProfile>(manifest.profile)??throw new InvalidOperationException("Contact profile missing.");var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(manifest.prefab)??throw new InvalidOperationException("Contact prefab missing.");var policy=manifest.policy;
                Validate(prefab,profile.targetAvatar,profile.sourceClip,policy,manifest.profile);
                if(profile.sourceClip.name!=manifest.sourceClipName||profile.useFootIK!=policy.useSourceFootIK||profile.loop!=policy.loop||profile.groundY!=policy.groundY||profile.maximumBodyCorrection!=policy.maximumBodyCorrection)throw new InvalidOperationException("Contact profile no longer matches sampled policy.");
                var intervals=profile.correctionMeters.Length-1;var count=intervals*policy.validationSubdivisions;report.authorIntervals=intervals;report.sourceClip=profile.sourceClip.name;report.targetAvatar=profile.targetAvatar.name;report.useFootIK=profile.useFootIK;
                var frames=new List<Frame>();Vector3[]? firstVertices=null,lastVertices=null;
                using(var baseline=new MeshyTargetContact.TargetSampler(prefab,profile.targetAvatar))using(var adapted=new MeshyTargetContact.TargetSampler(prefab,profile.targetAvatar))
                {
                    baseline.SetClip(profile.sourceClip,profile.useFootIK);adapted.SetClip(profile.sourceClip,profile.useFootIK);report.leftSoleVertices=baseline.LeftCount;report.rightSoleVertices=baseline.RightCount;
                    for(var i=0;i<=count;i++)
                    {
                        var phase=i/(float)count;var offset=profile.Evaluate(phase);baseline.Evaluate(phase*profile.sourceClip.length);adapted.Evaluate(phase*profile.sourceClip.length,offset);
                        var original=baseline.Measure();var shifted=adapted.Measure();var a=baseline.Vertices();var b=adapted.Vertices();if(a.Length!=b.Length)throw new InvalidOperationException("Contact vertex count changed.");
                        var poseError=Enumerable.Range(0,a.Length).Max(v=>Vector3.Distance(a[v]+Vector3.up*offset,b[v]));
                        if(i==0)firstVertices=b;if(i==count)lastVertices=b;
                        frames.Add(new Frame{phase=phase,correctionMeters=offset,support=MeshyTargetContact.SupportAt(policy,phase),original=original,adapted=shifted,maximumSkinDeviationFromPureTranslation=poseError});
                    }
                }
                report.frames=frames.ToArray();report.validationSamples=frames.Count;report.minimumSkinY=frames.Min(f=>f.adapted.skinMinimumY);report.minimumFootY=frames.Min(f=>Mathf.Min(f.adapted.left.minimumY,f.adapted.right.minimumY));report.maximumAbsoluteCorrection=profile.correctionMeters.Max(v=>Mathf.Abs(v));
                report.maximumSkinDeviationFromPureTranslation=frames.Max(f=>f.maximumSkinDeviationFromPureTranslation);report.maximumExternalActorRootTravel=frames.Max(f=>f.adapted.actorRootPosition.magnitude);report.maximumExternalActorRootRotation=frames.Max(f=>Quaternion.Angle(f.adapted.actorRootRotation,Quaternion.identity));
                foreach(var frame in frames)foreach(var pair in new[]{(Support.Left,frame.adapted.left),(Support.Right,frame.adapted.right)})if((frame.support&pair.Item1)!=0)
                {
                    report.maximumStanceHover=Mathf.Max(report.maximumStanceHover,pair.Item2.minimumY-policy.groundY);report.maximumHeelToeHover=Mathf.Max(report.maximumHeelToeHover,Mathf.Max(pair.Item2.heelMinimumY,pair.Item2.toeMinimumY)-policy.groundY);
                }
                for(var i=1;i<frames.Count;i++)foreach(var side in new[]{Support.Left,Support.Right})if((frames[i-1].support&side)!=0&&(frames[i].support&side)!=0)
                {
                    var a=side==Support.Left?frames[i-1].adapted.left.supportPoint:frames[i-1].adapted.right.supportPoint;var b=side==Support.Left?frames[i].adapted.left.supportPoint:frames[i].adapted.right.supportPoint;report.maximumRelativeStanceStep=Mathf.Max(report.maximumRelativeStanceStep,Vector3.ProjectOnPlane(b-a,Vector3.up).magnitude);
                }
                report.loopCorrectionSeam=Mathf.Abs(profile.Evaluate(0)-profile.Evaluate(1));
                if(firstVertices==null||lastVertices==null)throw new InvalidOperationException("Missing contact endpoint sample.");var first=firstVertices;var last=lastVertices;report.loopSkinSeam=Enumerable.Range(0,first.Length).Max(i=>Vector3.Distance(first[i],last[i]));
                var failures=new List<string>();
                if(report.minimumFootY<policy.groundY-policy.contactTolerance)failures.Add("FOOT_PENETRATION");
                if(report.minimumSkinY<policy.groundY-policy.contactTolerance)failures.Add("WHOLE_SKIN_PENETRATION");
                if(report.maximumStanceHover>policy.contactTolerance)failures.Add("DUAL_OR_SINGLE_SUPPORT_HOVER_NOT_FIXED_BY_BODY_Y");
                if(policy.requireHeelAndToeSupport&&report.maximumHeelToeHover>policy.flatSoleHoverTolerance)failures.Add("HEEL_TOE_SUPPORT_HOVER");
                if(report.maximumSkinDeviationFromPureTranslation>.0001f)failures.Add("SOURCE_FOOTIK_POSE_NOT_PRESERVED_BY_PRESENTATION_TRANSLATION");
                if(report.maximumExternalActorRootTravel>1e-6f||report.maximumExternalActorRootRotation>.001f)failures.Add("GAMEPLAY_ACTOR_FRAME_MOVED");
                if(policy.loop&&(report.loopCorrectionSeam>.001f||report.loopSkinSeam>.003f))failures.Add("LOOP_ENDPOINT_DISCONTINUITY");
                MeshyTargetContact.CheckInputs(manifest.immutableInputs);report.originalAssetsUnchanged=true;report.failedChecks=failures.ToArray();report.status=failures.Count==0?"CONTACT_NUMERIC_CHECKS_PASS_WINDOWS_PENDING":"CONTACT_QUALITY_FAIL";
                Debug.Log("FOC_CONTACT_PROFILE "+report.sourceClip+" status="+report.status+" minY="+report.minimumSkinY+" maxSupportHover="+report.maximumStanceHover+" correction="+report.maximumAbsoluteCorrection);return report;
            }
            catch(Exception e){report.error=e.ToString();throw;}
            finally{Directory.CreateDirectory(ReportRoot);File.WriteAllText(Path.Combine(ReportRoot,Path.GetFileNameWithoutExtension(manifest.profile)+".json"),JsonUtility.ToJson(report,true));}
        }
        private static void Validate(GameObject prefab,Avatar avatar,AnimationClip clip,MeshyTargetContact.ContactPolicy policy,string path)
        {
            MeshyTargetContact.ValidatePolicy(policy);
            if(prefab==null||avatar==null||clip==null||!avatar.isHuman||!avatar.isValid||!clip.isHumanMotion||clip.length<=0)throw new InvalidOperationException("Valid existing Humanoid source/target required.");
            if(!policy.referenceAndSourcePoseReviewed||string.IsNullOrWhiteSpace(policy.sourcePoseGateEvidence))throw new InvalidOperationException("Source/reference pose review must precede contact authoring.");
            if(policy.maximumBodyCorrection>.12f)throw new InvalidOperationException("Do not increase the bounded contact cap to hide reference/source failure.");
            if(!path.StartsWith(OutputRoot+"/",StringComparison.Ordinal)||!path.EndsWith(".asset",StringComparison.Ordinal)||path.Contains(".."))throw new InvalidOperationException("New isolated contact profile path required.");
            if(AnimationUtility.GetAnimationClipSettings(clip).loopTime!=policy.loop)throw new InvalidOperationException("Contact loop policy disagrees with unchanged source clip.");
        }
    }
}
