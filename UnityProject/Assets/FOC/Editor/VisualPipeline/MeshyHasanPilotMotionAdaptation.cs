#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object=UnityEngine.Object;

namespace FOC.Editor.Visuals
{
    /// <summary>
    /// Bounded conversion of EXISTING FOC diagnostic transform animation using
    /// its existing human Avatar. No new model/rig, shared-library replacement,
    /// source animation edit or production animation claim is made here.
    /// </summary>
    public static class MeshyHasanPilotMotionAdaptation
    {
        public const string DonorPrefabPath="Assets/FOC/Presentation/Characters/Ottoman1648/CHR_HasanAga_DonorDraft.prefab";
        public const string DonorMotionRoot="Assets/FOC/Presentation/Characters/Ottoman1648/HasanMotionReview";
        public const string OutputRoot=MeshyHasanPilotPipeline.OutputRoot+"/Retargeted";
        public const string ProvenancePath=OutputRoot+"/FOCDiagnosticMotionProvenance.json";
        public static readonly string[] MotionNames={"Idle","Turn","OneHandedAttack","ArmRaise","Crouch","MountedSeated"};
        public static string ReportPath=>Path.GetFullPath("../TestResults/MeshyPilot/motion-adaptation.json");
        public const int SamplesPerSecond=60;

        [Serializable] public sealed class InputRecord
        {
            public string path="";public string sha256="";
        }
        [Serializable] public sealed class MotionProvenance
        {
            public string motion="";public string sourcePath="";public string sourceSha256="";public string outputPath="";
            public bool sourceWasHumanoid;public string status="EXISTING_FOC_DIAGNOSTIC_CONVERTED_NOT_PRODUCTION_ACCEPTED";
            public int transformBindings;public int ignoredBlendShapeBindings;public int sampleCount;
            public float duration;public float maximumAbsoluteMuscle;public string curveSignature="";
            public float maximumSourceJointTravel;public float maximumSourceJointRotation;
        }
        [Serializable] public sealed class Provenance
        {
            public int version=1;public int samplesPerSecond=SamplesPerSecond;
            public string method="Sample unchanged FOC diagnostic Transform clips on a source-prefab clone with temporary Generic sampling Avatar (same unchanged bones); existing saved Human Avatar HumanPoseHandler.GetHumanPose -> new Animator muscle / RootT / RootQ curves. Temporary sampling Avatar is never saved.";
            public string bodySpace="Unity6000.3 GetHumanPose returns world center of mass divided by source Avatar humanScale. Sampling root is identity at world origin; write this already-normalized value once to RootT, without a second scale factor.";
            public string origin="Original FOC procedural diagnostic motion, not Meshy-supplied motion, mocap, or an existing shared Humanoid animation library.";
            public string limitations="Converting format does not improve choreography, contact, grip or garment deformation. No finger/grip blendshapes transferred. MountedSeated is only a pose donor, not mounted integration acceptance.";
            public InputRecord[] inputs=Array.Empty<InputRecord>();public MotionProvenance[] motions=Array.Empty<MotionProvenance>();
        }
        [Serializable] public sealed class TargetSample
        {
            public float phase;public Vector3 minimum;public Vector3 maximum;public Vector3[] trackedJointPositions=Array.Empty<Vector3>();
        }
        [Serializable] public sealed class MotionAudit
        {
            public string motion="";public bool humanMotion;public int muscleCurves;public int transformCurves;
            public string sampledSkinMesh="";public int sampledSkinTriangles;
            public float maximumJointTravel;public float maximumBodyTranslation;public float maximumBodyRotation;public float maximumMuscleDelta;
            public string curveSignature="";public TargetSample[] targetSamples=Array.Empty<TargetSample>();
        }
        [Serializable] public sealed class AdaptationReport
        {
            public string status="NOT_READY";public string operation="";public string unityVersion="";public string utc="";public string error="";
            public string visualAcceptance="NOT EVALUATED: real player inspection of shoulders/elbows/wrists/hips/knees/ankles/coat/trousers/boots/neck required.";
            public string sharedLibraryFinding="VisualProof shared action names are pelvis-only generic translation; they are NOT the retarget source or existing Humanoid clips.";
            public string mountAcceptance="NOT EVALUATED; MountedSeated donor must not enable mounted catalog use.";
            public string targetPrefab=MeshyHasanPilotPipeline.PrefabPath;public string provenance=ProvenancePath;
            public bool sourceAvatarHuman;public bool sourceAvatarValid;public bool targetAvatarHuman;public bool targetAvatarValid;
            public MotionAudit[] motions=Array.Empty<MotionAudit>();
        }

        public static string SourceClipPath(string motion)=>DonorMotionRoot+"/ANM_HasanDonor_"+motion+".anim";
        public static string OutputClipPath(string motion)=>OutputRoot+"/ANM_FOCDiagnostic_"+motion+".anim";
        public static AnimationClip[] LoadClips()=>MotionNames.Select(m=>AssetDatabase.LoadAssetAtPath<AnimationClip>(OutputClipPath(m))??throw new InvalidOperationException("Missing converted diagnostic clip: "+m)).ToArray();
        public static void Run()=>Batch(true);
        public static void RunVerify()=>Batch(false);
        private static void Batch(bool generate)
        {
            try{if(generate)Generate();else Verify();Debug.Log("FOC_MESHY_DIAGNOSTIC_MOTION_TECHNICAL_PASS_NOT_VISUAL_ACCEPTANCE");EditorApplication.Exit(0);}
            catch(Exception exception){Debug.LogException(exception);EditorApplication.Exit(1);}
        }

        [MenuItem("FOC/Visuals/Convert Existing FOC Diagnostic Motions For Meshy Pilot")]
        public static AdaptationReport Generate()
        {
            var report=NewReport("Generate");
            try
            {
                var sourcePrefab=LoadPrefab(DonorPrefabPath);var sourceAnimator=sourcePrefab.GetComponent<Animator>();
                RequireHuman(sourceAnimator,"source");
                var avatarPath=AssetDatabase.GetAssetPath(sourceAnimator.avatar);
                var inputs=new[]{DonorPrefabPath,avatarPath}.Concat(MotionNames.Select(SourceClipPath)).Select(RecordInput).ToArray();
                Directory.CreateDirectory(OutputRoot);AssetDatabase.Refresh();
                var provenance=new Provenance{inputs=inputs};
                var records=new List<MotionProvenance>();
                foreach(var motion in MotionNames)
                {
                    var sourceClip=AssetDatabase.LoadAssetAtPath<AnimationClip>(SourceClipPath(motion))??throw new InvalidOperationException("Existing diagnostic source missing: "+motion);
                    ValidateGenericDiagnostic(sourceClip);
                    var record=new MotionProvenance{motion=motion,sourcePath=SourceClipPath(motion),sourceSha256=HashFile(SourceClipPath(motion)),outputPath=OutputClipPath(motion),sourceWasHumanoid=sourceClip.humanMotion,duration=sourceClip.length};
                    var clip=Convert(sourcePrefab,sourceClip,motion,record);
                    try{StoreClip(clip,record.outputPath);}finally{if(!AssetDatabase.Contains(clip))Object.DestroyImmediate(clip);}
                    records.Add(record);
                }
                provenance.motions=records.ToArray();
                foreach(var input in inputs)Require(HashFile(input.path)==input.sha256,"Source changed during conversion: "+input.path);
                File.WriteAllText(ProvenancePath,JsonUtility.ToJson(provenance,true));AssetDatabase.ImportAsset(ProvenancePath);
                AssetDatabase.SaveAssets();
                Audit(report,provenance);report.status="TECHNICAL_CONVERSION_PASS_VISUAL_ACCEPTANCE_PENDING";return report;
            }
            catch(Exception e){report.error=e.ToString();throw;}
            finally{WriteReport(report);}
        }

        /// <summary>Read-only asset verification; only the external evidence report is written.</summary>
        public static AdaptationReport Verify()
        {
            var report=NewReport("Verify");
            try
            {
                Require(File.Exists(ProvenancePath),"Converted motion provenance missing; conversion was not performed.");
                var provenance=JsonUtility.FromJson<Provenance>(File.ReadAllText(ProvenancePath));
                Audit(report,provenance);report.status="TECHNICAL_CONVERSION_PASS_VISUAL_ACCEPTANCE_PENDING";return report;
            }
            catch(Exception e){report.error=e.ToString();throw;}
            finally{WriteReport(report);}
        }

        public static void ValidateGenericDiagnostic(AnimationClip source)
        {
            if(source==null)throw new ArgumentNullException(nameof(source));
            Require(!source.humanMotion,"Expected existing generic diagnostic source, not an unrecorded Humanoid replacement.");
            Require(source.length>0f&&Finite(source.length),"Empty diagnostic animation.");
            var rotations=AnimationUtility.GetCurveBindings(source).Where(b=>b.type==typeof(Transform)&&b.propertyName.StartsWith("m_LocalRotation.",StringComparison.Ordinal)).Select(b=>b.path).Distinct().ToArray();
            foreach(var joint in new[]{"UpperArm_L","LowerArm_L","UpperArm_R","LowerArm_R","UpperLeg_L","LowerLeg_L","UpperLeg_R","LowerLeg_R"})
                Require(rotations.Any(path=>path.EndsWith("/"+joint,StringComparison.Ordinal)),"Diagnostic lacks articulated source rotation: "+joint+". A named pelvis-only proof clip is not an action donor.");
        }

        private static AnimationClip Convert(GameObject sourcePrefab,AnimationClip sourceClip,string motion,MotionProvenance record)
        {
            var instance=Object.Instantiate(sourcePrefab);instance.name=sourcePrefab.name;
            Avatar? samplingAvatar=null;
            try
            {
                // GetHumanPose bodyPosition is world-space center of mass / the
                // Avatar's humanScale (Unity6000.3 API). Use an identity sampling
                // root, then write that already-normalized body value directly;
                // multiplying/dividing by humanScale again would corrupt height.
                // https://docs.unity3d.com/6000.3/Documentation/ScriptReference/HumanPoseHandler.GetHumanPose.html
                Require((instance.transform.localScale-Vector3.one).sqrMagnitude<1e-8f,"Source sampling root must use its unit Avatar scale.");
                instance.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                var animator=instance.GetComponent<Animator>();var sourceHumanAvatar=animator.avatar;animator.enabled=false;animator.runtimeAnimatorController=null;
                // The diagnostic clip is Generic. A Humanoid Animator silently
                // discards its Transform curves even during SampleAnimation.
                // Match the existing donor review's generic sampling context,
                // but use the unchanged saved Human Avatar for pose extraction.
                samplingAvatar=AvatarBuilder.BuildGenericAvatar(instance,"Root");
                Require(samplingAvatar!=null&&samplingAvatar.isValid,"Cannot sample the existing source transform clip.");
                animator.avatar=samplingAvatar;animator.Rebind();
                var transforms=instance.GetComponentsInChildren<Transform>(true);var positions=transforms.Select(t=>t.localPosition).ToArray();var rotations=transforms.Select(t=>t.localRotation).ToArray();var scales=transforms.Select(t=>t.localScale).ToArray();
                var bindings=AnimationUtility.GetCurveBindings(sourceClip);
                record.transformBindings=bindings.Count(b=>b.type==typeof(Transform));record.ignoredBlendShapeBindings=bindings.Count(b=>b.propertyName.StartsWith("blendShape.",StringComparison.Ordinal));
                var frames=Mathf.CeilToInt(sourceClip.length*SamplesPerSecond);record.sampleCount=frames+1;
                var names=HumanTrait.MuscleName.Concat(new[]{"RootT.x","RootT.y","RootT.z","RootQ.x","RootQ.y","RootQ.z","RootQ.w"}).ToArray();
                var keys=names.Select(_=>new List<Keyframe>(frames+1)).ToArray();var pose=new HumanPose{muscles=new float[HumanTrait.MuscleCount]};var priorRotation=Quaternion.identity;
                Vector3[]? firstSourcePositions=null;Quaternion[]? firstSourceRotations=null;
                using(var handler=new HumanPoseHandler(sourceHumanAvatar,instance.transform))
                {
                    for(var frame=0;frame<=frames;frame++)
                    {
                        for(var i=0;i<transforms.Length;i++){transforms[i].localPosition=positions[i];transforms[i].localRotation=rotations[i];transforms[i].localScale=scales[i];}
                        var time=sourceClip.length*frame/frames;
                        sourceClip.SampleAnimation(instance,time);
                        var currentPositions=transforms.Select(t=>t.position).ToArray();var currentRotations=transforms.Select(t=>t.rotation).ToArray();
                        if(firstSourcePositions==null){firstSourcePositions=currentPositions;firstSourceRotations=currentRotations;}
                        else for(var i=0;i<transforms.Length;i++)
                        {
                            record.maximumSourceJointTravel=Mathf.Max(record.maximumSourceJointTravel,Vector3.Distance(firstSourcePositions[i],currentPositions[i]));
                            record.maximumSourceJointRotation=Mathf.Max(record.maximumSourceJointRotation,Quaternion.Angle(firstSourceRotations![i],currentRotations[i]));
                        }
                        handler.GetHumanPose(ref pose);
                        ValidatePose(pose);
                        if(frame>0&&Quaternion.Dot(priorRotation,pose.bodyRotation)<0f)pose.bodyRotation=new Quaternion(-pose.bodyRotation.x,-pose.bodyRotation.y,-pose.bodyRotation.z,-pose.bodyRotation.w);
                        priorRotation=pose.bodyRotation;
                        for(var i=0;i<pose.muscles.Length;i++){keys[i].Add(new Keyframe(time,pose.muscles[i]));record.maximumAbsoluteMuscle=Mathf.Max(record.maximumAbsoluteMuscle,Mathf.Abs(pose.muscles[i]));}
                        var body=new[]{pose.bodyPosition.x,pose.bodyPosition.y,pose.bodyPosition.z,pose.bodyRotation.x,pose.bodyRotation.y,pose.bodyRotation.z,pose.bodyRotation.w};
                        for(var i=0;i<body.Length;i++)keys[HumanTrait.MuscleCount+i].Add(new Keyframe(time,body[i]));
                    }
                }
                Require(record.maximumSourceJointTravel>.0001f||record.maximumSourceJointRotation>.01f,"The source generic clip did not actually move its original rig: "+motion);
                var clip=new AnimationClip{name="ANM_FOCDiagnostic_"+motion,frameRate=SamplesPerSecond,legacy=false};
                for(var i=0;i<names.Length;i++)
                {
                    var curve=new AnimationCurve(keys[i].ToArray());
                    // Linear sampling is deliberate: no cubic overshoot may
                    // invent a more extreme pose between measured frames.
                    for(var k=0;k<curve.length;k++){AnimationUtility.SetKeyLeftTangentMode(curve,k,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,k,AnimationUtility.TangentMode.Linear);}
                    AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),names[i]),curve);
                }
                var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;settings.loopBlend=false;
                // These stationary diagnostic actions retain body movement and
                // turning inside the pose. They do not drive gameplay travel.
                settings.loopBlendPositionY=true;settings.loopBlendPositionXZ=true;settings.loopBlendOrientation=true;
                settings.keepOriginalPositionY=true;settings.keepOriginalPositionXZ=true;settings.keepOriginalOrientation=true;
                AnimationUtility.SetAnimationClipSettings(clip,settings);
                Require(clip.humanMotion,"Unity did not recognize baked muscle curves as Humanoid animation.");
                record.curveSignature=CurveSignature(clip);return clip;
            }
            finally{Object.DestroyImmediate(instance);if(samplingAvatar!=null)Object.DestroyImmediate(samplingAvatar);}
        }

        private static void Audit(AdaptationReport report,Provenance provenance)
        {
            Require(provenance!=null&&provenance.version==1&&provenance.samplesPerSecond==SamplesPerSecond,"Unknown motion-conversion provenance.");
            foreach(var input in provenance.inputs)Require(HashFile(input.path)==input.sha256,"Donor source differs from conversion provenance: "+input.path);
            Require(provenance.motions.Length==MotionNames.Length&&MotionNames.All(m=>provenance.motions.Count(p=>p.motion==m)==1),"Missing/duplicate converted action.");
            var source=LoadPrefab(DonorPrefabPath).GetComponent<Animator>();var target=LoadPrefab(MeshyHasanPilotPipeline.PrefabPath).GetComponent<Animator>();
            RequireHuman(source,"source");RequireHuman(target,"target");report.sourceAvatarHuman=source.avatar.isHuman;report.sourceAvatarValid=source.avatar.isValid;report.targetAvatarHuman=target.avatar.isHuman;report.targetAvatarValid=target.avatar.isValid;
            var audits=new List<MotionAudit>();
            foreach(var motion in MotionNames)
            {
                var record=provenance.motions.Single(m=>m.motion==motion);
                Require(!record.sourceWasHumanoid&&record.sourcePath==SourceClipPath(motion)&&record.outputPath==OutputClipPath(motion),"Source-format/path claim is inaccurate.");
                ValidateGenericDiagnostic(AssetDatabase.LoadAssetAtPath<AnimationClip>(record.sourcePath));
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(record.outputPath)??throw new InvalidOperationException("Converted clip missing: "+motion);
                var audit=AuditClip(clip,motion);
                Require(audit.curveSignature==record.curveSignature,"Converted clip no longer matches its recorded sampled curves: "+motion);
                AuditTarget(clip,audit);audits.Add(audit);
            }
            Require(audits.Select(a=>a.curveSignature).Distinct(StringComparer.Ordinal).Count()==MotionNames.Length,"Different action names resolved to duplicate motion data.");
            report.motions=audits.ToArray();
        }

        public static MotionAudit AuditClip(AnimationClip clip,string motion)
        {
            Require(clip!=null&&clip.humanMotion&&clip.length>0f,"Converted action is not real nonempty Humanoid motion: "+motion);
            var bindings=AnimationUtility.GetCurveBindings(clip);var muscles=new HashSet<string>(HumanTrait.MuscleName,StringComparer.Ordinal);
            var audit=new MotionAudit{motion=motion,humanMotion=clip.humanMotion,muscleCurves=bindings.Count(b=>b.type==typeof(Animator)&&muscles.Contains(b.propertyName)),transformCurves=bindings.Count(b=>b.type==typeof(Transform)),curveSignature=CurveSignature(clip)};
            Require(audit.muscleCurves==HumanTrait.MuscleCount&&audit.transformCurves==0,"Converted action contains missing muscles or hidden foreign-bone transform curves.");
            foreach(var binding in bindings)
            {
                Require(binding.type==typeof(Animator)&&binding.path.Length==0,"Only true root Animator muscle/body bindings are permitted.");
                var curve=AnimationUtility.GetEditorCurve(clip,binding);Require(curve!=null&&curve.length>=2,"Empty converted channel.");
                Require(curve.keys.All(k=>Finite(k.time)&&Finite(k.value)&&Finite(k.inTangent)&&Finite(k.outTangent)),"Nonfinite baked motion channel.");
                if(muscles.Contains(binding.propertyName))audit.maximumMuscleDelta=Mathf.Max(audit.maximumMuscleDelta,curve.keys.Max(k=>k.value)-curve.keys.Min(k=>k.value));
            }
            var first=ReadBody(clip,0f);
            for(var sample=0;sample<=8;sample++)
            {
                var body=ReadBody(clip,clip.length*sample/8f);
                audit.maximumBodyTranslation=Mathf.Max(audit.maximumBodyTranslation,Vector3.Distance(first.position,body.position));
                audit.maximumBodyRotation=Mathf.Max(audit.maximumBodyRotation,Quaternion.Angle(first.rotation,body.rotation));
            }
            Require(audit.maximumMuscleDelta>.0001f||audit.maximumBodyTranslation>.0001f||audit.maximumBodyRotation>.1f,"Action is a static renamed pose, not its supplied diagnostic motion.");
            return audit;
        }

        private static (Vector3 position,Quaternion rotation) ReadBody(AnimationClip clip,float time)
        {
            float V(string name)
            {
                var curve=AnimationUtility.GetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),name));
                Require(curve!=null,"Missing Humanoid body channel: "+name);var value=curve.Evaluate(time);Require(Finite(value),"Nonfinite body channel.");return value;
            }
            var rotation=new Quaternion(V("RootQ.x"),V("RootQ.y"),V("RootQ.z"),V("RootQ.w"));
            Require(Mathf.Abs(Quaternion.Dot(rotation,rotation)-1f)<.01f,"Invalid sampled body quaternion.");
            return(new Vector3(V("RootT.x"),V("RootT.y"),V("RootT.z")),rotation);
        }
        private static void AuditTarget(AnimationClip clip,MotionAudit audit)
        {
            var target=Object.Instantiate(LoadPrefab(MeshyHasanPilotPipeline.PrefabPath));target.name="EphemeralRetargetAudit";
            var graph=default(PlayableGraph);
            try
            {
                var animator=target.GetComponent<Animator>();animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.enabled=true;
                var lod0=target.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.sharedMesh!=null).OrderByDescending(r=>r.sharedMesh.triangles.Length).FirstOrDefault();
                Require(lod0!=null,"Retarget target has no LOD0 skin.");audit.sampledSkinMesh=lod0!.sharedMesh.name;audit.sampledSkinTriangles=lod0.sharedMesh.triangles.Length/3;
                var tracked=new[]{HumanBodyBones.Head,HumanBodyBones.Chest,HumanBodyBones.LeftHand,HumanBodyBones.RightHand,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot}.Select(animator.GetBoneTransform).ToArray();
                Require(tracked.All(t=>t!=null),"Retarget target lacks required tracked bones.");
                graph=PlayableGraph.Create("Ephemeral FOC diagnostic retarget audit");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(false);playable.SetApplyPlayableIK(false);
                AnimationPlayableOutput.Create(graph,"Source Avatar converted muscle motion",animator).SetSourcePlayable(playable);graph.Play();
                var samples=new List<TargetSample>();Vector3[]? first=null;
                for(var index=0;index<8;index++)
                {
                    var phase=index/8f;playable.SetTime(phase*clip.length);graph.Evaluate(0);
                    var joints=tracked.Select(t=>target.transform.InverseTransformPoint(t.position)).ToArray();Require(joints.All(Finite),"Retarget yielded nonfinite joint positions.");
                    if(first==null)first=joints;else for(var i=0;i<joints.Length;i++)audit.maximumJointTravel=Mathf.Max(audit.maximumJointTravel,Vector3.Distance(first[i],joints[i]));
                    var minimum=new Vector3(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity);var maximum=new Vector3(float.NegativeInfinity,float.NegativeInfinity,float.NegativeInfinity);
                    // Inspect the real highest-resolution skin, not a union of
                    // alternate LOD meshes whose decimation can alter bounds.
                    foreach(var renderer in new[]{lod0})
                    {
                        var mesh=new Mesh();
                        try
                        {
                            renderer.BakeMesh(mesh);Require(mesh.vertexCount>0,"Retarget produced empty skin.");
                            foreach(var vertex in mesh.vertices)
                            {
                                var p=target.transform.InverseTransformPoint(renderer.transform.TransformPoint(vertex));Require(Finite(p),"Retarget produced nonfinite skin.");minimum=Vector3.Min(minimum,p);maximum=Vector3.Max(maximum,p);
                            }
                        }
                        finally{Object.DestroyImmediate(mesh);}
                    }
                    Require(Finite(minimum)&&Finite(maximum)&&(maximum-minimum).magnitude<5f,"Retarget skin is missing or exploded; visual quality is a separate gate.");
                    samples.Add(new TargetSample{phase=phase,minimum=minimum,maximum=maximum,trackedJointPositions=joints});
                }
                Require(audit.maximumJointTravel>.0005f,"Converted clip did not move target-avatar joints.");audit.targetSamples=samples.ToArray();
            }
            finally{if(graph.IsValid())graph.Destroy();Object.DestroyImmediate(target);}
        }

        public static string CurveSignature(AnimationClip clip)
        {
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
            {
                foreach(var binding in AnimationUtility.GetCurveBindings(clip).OrderBy(b=>b.path,StringComparer.Ordinal).ThenBy(b=>b.propertyName,StringComparer.Ordinal))
                {
                    writer.Write(binding.path);writer.Write(binding.type.FullName??"");writer.Write(binding.propertyName);
                    var curve=AnimationUtility.GetEditorCurve(clip,binding);writer.Write(curve.length);
                    foreach(var key in curve.keys){writer.Write(key.time);writer.Write(key.value);writer.Write(key.inTangent);writer.Write(key.outTangent);}
                }
                writer.Flush();using(var sha=SHA256.Create())return Hex(sha.ComputeHash(stream.ToArray()));
            }
        }
        private static void StoreClip(AnimationClip generated,string path)
        {
            var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(existing==null)AssetDatabase.CreateAsset(generated,path);else{EditorUtility.CopySerialized(generated,existing);EditorUtility.SetDirty(existing);}
        }
        private static void ValidatePose(HumanPose pose)
        {
            Require(pose.muscles!=null&&pose.muscles.Length==HumanTrait.MuscleCount&&pose.muscles.All(Finite),"Source HumanPose muscles are invalid.");
            Require(Finite(pose.bodyPosition)&&Finite(pose.bodyRotation.x)&&Finite(pose.bodyRotation.y)&&Finite(pose.bodyRotation.z)&&Finite(pose.bodyRotation.w)&&Mathf.Abs(Quaternion.Dot(pose.bodyRotation,pose.bodyRotation)-1f)<.001f,"Source HumanPose body transform invalid.");
        }
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        private static bool Finite(Vector3 value)=>Finite(value.x)&&Finite(value.y)&&Finite(value.z);
        private static void RequireHuman(Animator animator,string role)=>Require(animator!=null&&animator.avatar!=null&&animator.avatar.isHuman&&animator.avatar.isValid,"Missing/invalid EXISTING "+role+" Humanoid Avatar; do not invent a new rig.");
        private static GameObject LoadPrefab(string path)=>AssetDatabase.LoadAssetAtPath<GameObject>(path)??throw new InvalidOperationException("Required existing prefab missing: "+path);
        private static InputRecord RecordInput(string path)=>new InputRecord{path=path,sha256=HashFile(path)};
        private static string HashFile(string path){using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))return Hex(sha.ComputeHash(stream));}
        private static string Hex(byte[] value)=>BitConverter.ToString(value).Replace("-","").ToLowerInvariant();
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        private static AdaptationReport NewReport(string operation)=>new AdaptationReport{operation=operation,unityVersion=Application.unityVersion,utc=DateTime.UtcNow.ToString("O")};
        private static void WriteReport(AdaptationReport report){Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)!);File.WriteAllText(ReportPath,JsonUtility.ToJson(report,true));}
    }
}
