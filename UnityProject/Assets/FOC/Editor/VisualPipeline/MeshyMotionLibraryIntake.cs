#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object=UnityEngine.Object;

namespace FOC.Editor.Visuals
{
    /// <summary>Licensed motion-only donor intake. Never substitutes its body for Meshy Hasan.</summary>
    public static class MeshyMotionLibraryIntake
    {
        public const string SourceRoot="Assets/FOC/ArtSource/HistoricalSlice/MotionLibrary";
        public const string CandidateRoot=MeshyHasanPilotPipeline.OutputRoot+"/MotionClosure";
        public static string ReportRoot=>Path.GetFullPath("../TestResults/MeshyMotionClosure");
        [Serializable] public sealed class ClipRecord { public string name="";public float duration;public bool humanoid;public int bindings; }
        [Serializable] public sealed class ModelRecord
        {
            public string path="",sha256="",avatar="",error="";
            public bool valid,human;public float humanScale;
            public string[] mapping=Array.Empty<string>();public ClipRecord[] clips=Array.Empty<ClipRecord>();
        }
        [Serializable] public sealed class Inventory
        {
            public string status="NOT_COMPLETED",unityVersion="",utc="";
            public string scope="Licensed animation donor only; source body is never a runtime character replacement.";
            public ModelRecord[] models=Array.Empty<ModelRecord>();
        }
        [Serializable] public sealed class MotionSample
        {
            public float phase,minimumSkinY;public Vector3 root;public Vector3[] joints=Array.Empty<Vector3>();public Quaternion[] rotations=Array.Empty<Quaternion>();
        }
        [Serializable] public sealed class MotionComparison
        {
            public string clip="",variant="",avatar="";public float duration,humanScale,minimumSkinY,leftWristTravel,rightWristTravel,torsoMaximumRotationDegrees;
            public MotionSample[] samples=Array.Empty<MotionSample>();
        }
        [Serializable] public sealed class ComparisonReport
        {
            public string status="MEASURED_NOT_VISUAL_ACCEPTED",utc="",unityVersion="";
            public string[] bones=Array.Empty<string>();public MotionComparison[] comparisons=Array.Empty<MotionComparison>();
        }
        private static readonly HumanBodyBones[] AuditBones={HumanBodyBones.Hips,HumanBodyBones.Spine,HumanBodyBones.Chest,HumanBodyBones.Head,HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot};
        public static void RunAudit()
        {
            var code=0;try
            {
                var target=AssetDatabase.LoadAssetAtPath<GameObject>(MeshyHasanPilotPipeline.PrefabPath);
                var report=new ComparisonReport{utc=DateTime.UtcNow.ToString("O"),unityVersion=Application.unityVersion,bones=AuditBones.Select(b=>b.ToString()).ToArray()};
                var cases=new List<MotionComparison>();
                foreach(var clip in ReviewClips())
                {
                    var source=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(clip));
                    cases.Add(Measure(source,null,clip,"Source"));
                    cases.Add(Measure(target,null,clip,"Original"));
                    cases.Add(Measure(target,MeshyHumanoidCalibration.LoadTargetAvatar(),clip,"Calibrated"));
                }
                if(cases.Count==0)throw new InvalidOperationException("No library comparisons ran.");
                report.comparisons=cases.ToArray();Directory.CreateDirectory(ReportRoot);File.WriteAllText(Path.Combine(ReportRoot,"motion-comparison.json"),JsonUtility.ToJson(report,true));
            }
            catch(Exception e){Debug.LogException(e);code=1;}
            EditorApplication.Exit(code);
        }
        private static MotionComparison Measure(GameObject prefab,Avatar? avatarOverride,AnimationClip clip,string variant)
        {
            var actor=Object.Instantiate(prefab);PlayableGraph graph=default;var mesh=new Mesh();
            try
            {
                Debug.Log("MOTION_SOURCE_INSTANCE variant="+variant+" active="+actor.activeSelf+" rotation="+actor.transform.rotation+" scale="+actor.transform.localScale+" hierarchy="+string.Join(";",actor.GetComponentsInChildren<Transform>(true).Select(t=>t.name+":"+t.gameObject.activeSelf)));
                actor.SetActive(true);
                actor.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                var animator=actor.GetComponent<Animator>();animator.enabled=false;animator.runtimeAnimatorController=null;
                if(avatarOverride!=null){FOC.Presentation.Visuals.MeshyHasanPilotPlayer.RestoreSourceBindPose(prefab,actor);animator.avatar=avatarOverride;animator.Rebind();}
                if(animator.avatar==null||!animator.avatar.isValid||!animator.avatar.isHuman||!clip.isHumanMotion)throw new InvalidOperationException("Cannot measure non-Humanoid source/target.");
                animator.enabled=true;animator.Rebind();animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                graph=PlayableGraph.Create("Licensed motion measurement");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(false);playable.SetApplyPlayableIK(false);
                AnimationPlayableOutput.Create(graph,"Measured humanoid",animator).SetSourcePlayable(playable);graph.Play();
                graph.Evaluate(0);
                var joints=AuditBones.Select(animator.GetBoneTransform).ToArray();
                if(joints.Any(j=>j==null))throw new InvalidOperationException("Missing motion audit joints: "+variant+" "+string.Join(",",AuditBones.Where((b,i)=>joints[i]==null)));
                var skin=actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(s=>s.sharedMesh.triangles.Length).First();
                skin.updateWhenOffscreen=true;var samples=new List<MotionSample>();
                var steps=Mathf.Max(32,Mathf.CeilToInt(clip.length*60));
                for(var i=0;i<=steps;i++)
                {
                    var phase=i/(float)steps;playable.SetTime(phase*clip.length);graph.Evaluate(0);
                    skin.BakeMesh(mesh);var min=mesh.vertices.Min(v=>skin.transform.TransformPoint(v).y);
                    if(float.IsNaN(min)||float.IsInfinity(min))throw new InvalidOperationException("Nonfinite motion skin.");
                    samples.Add(new MotionSample{phase=phase,minimumSkinY=min,root=actor.transform.position,joints=joints.Select(j=>j.position).ToArray(),rotations=joints.Select(j=>j.rotation).ToArray()});
                }
                var first=samples[0];
                return new MotionComparison{clip=clip.name,variant=variant,avatar=animator.avatar.name,duration=clip.length,humanScale=animator.humanScale,minimumSkinY=samples.Min(s=>s.minimumSkinY),leftWristTravel=samples.Max(s=>Vector3.Distance(first.joints[6],s.joints[6])),rightWristTravel=samples.Max(s=>Vector3.Distance(first.joints[9],s.joints[9])),torsoMaximumRotationDegrees=samples.Max(s=>Enumerable.Range(0,3).Max(j=>Quaternion.Angle(first.rotations[j],s.rotations[j]))),samples=samples.ToArray()};
            }
            finally{if(graph.IsValid())graph.Destroy();Object.DestroyImmediate(mesh);Object.DestroyImmediate(actor);}
        }
        public static void RunImport()
        {
            var report=new Inventory{unityVersion=Application.unityVersion,utc=DateTime.UtcNow.ToString("O")};var records=new List<ModelRecord>();var exit=0;
            try
            {
                if(!File.Exists(SourceRoot+"/PROVENANCE.json"))throw new InvalidOperationException("Record reviewed upstream license and archive hashes before intake.");
                AssetDatabase.Refresh();
                var files=Directory.GetFiles(SourceRoot,"*.fbx",SearchOption.AllDirectories).Select(p=>p.Replace('\\','/')).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
                if(files.Length==0)throw new InvalidOperationException("No licensed motion FBX files present.");
                foreach(var path in files)
                {
                    var record=new ModelRecord{path=path,sha256=Hash(path)};records.Add(record);
                    try
                    {
                        var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                        importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
                        // Upstream Unity_Setup.png explicitly requires axis baking.
                        importer.bakeAxisConversion=true;
                        importer.importAnimation=true;importer.optimizeGameObjects=false;importer.isReadable=true;importer.preserveHierarchy=true;importer.meshCompression=ModelImporterMeshCompression.Off;
                        importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.animationCompression=ModelImporterAnimationCompression.Off;
                        // Axis-baking changes the imported skeleton coordinates.
                        // Discard ONLY this donor's previously auto-generated
                        // description so Unity remaps its actual new reference;
                        // retaining it caused measured 0.59m limb-length errors.
                        importer.humanDescription=new HumanDescription{human=Array.Empty<HumanBone>(),skeleton=Array.Empty<SkeletonBone>(),upperArmTwist=.5f,lowerArmTwist=.5f,upperLegTwist=.5f,lowerLegTwist=.5f,armStretch=.05f,legStretch=.05f,feetSpacing=0};
                        importer.SaveAndReimport();
                        // This non-RM source is Humanoid. Use Mecanim Body
                        // Orientation instead of exporting an Armature-root axis
                        // as target body rotation; actor movement remains external.
                        importer.motionNodeName="";
                        var settings=importer.defaultClipAnimations;
                        foreach(var clip in settings)
                        {
                            clip.loopTime=clip.name.IndexOf("Loop",StringComparison.OrdinalIgnoreCase)>=0;
                            clip.loopPose=false;clip.lockRootHeightY=true;clip.keepOriginalPositionY=true;clip.heightFromFeet=false;
                            clip.lockRootPositionXZ=true;clip.keepOriginalPositionXZ=true;
                            clip.lockRootRotation=false;clip.keepOriginalOrientation=false;
                        }
                        importer.clipAnimations=settings;importer.SaveAndReimport();
                        var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);var animator=model.GetComponent<Animator>();var avatar=animator!=null?animator.avatar:null;
                        record.valid=avatar!=null&&avatar.isValid;record.human=avatar!=null&&avatar.isHuman;record.avatar=avatar!=null?avatar.name:"";
                        record.mapping=avatar!=null&&avatar.isHuman?avatar.humanDescription.human.Select(b=>b.humanName+":"+b.boneName).ToArray():Array.Empty<string>();
                        var clone=Object.Instantiate(model);try{record.humanScale=clone.GetComponent<Animator>().humanScale;}finally{Object.DestroyImmediate(clone);}
                        record.clips=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__",StringComparison.Ordinal)).Select(c=>new ClipRecord{name=c.name,duration=c.length,humanoid=c.isHumanMotion,bindings=AnimationUtility.GetCurveBindings(c).Length}).ToArray();
                        if(!record.valid||!record.human)throw new InvalidOperationException("Motion source Avatar auto-mapping invalid: "+path);
                    }
                    catch(Exception e){record.error=e.ToString();Debug.LogError(e);exit=1;}
                }
                report.status=exit==0?"INTAKE_TECHNICAL_PASS_VISUAL_UNREVIEWED":"INTAKE_INCOMPLETE";
            }
            catch(Exception e){Debug.LogException(e);exit=1;}
            finally{report.models=records.ToArray();Directory.CreateDirectory(ReportRoot);File.WriteAllText(Path.Combine(ReportRoot,"source-inventory.json"),JsonUtility.ToJson(report,true));AssetDatabase.SaveAssets();}
            EditorApplication.Exit(exit);
        }
        public static AnimationClip[] AllClips()=>AssetDatabase.FindAssets("t:AnimationClip",new[]{SourceRoot}).Select(AssetDatabase.GUIDToAssetPath).Distinct().SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__",StringComparison.Ordinal)).ToArray();
        public static AnimationClip Clip(string name)
        {
            var matches=AllClips().Where(c=>c.name==name||c.name.EndsWith("|"+name,StringComparison.Ordinal)).ToArray();
            if(matches.Length!=1)throw new InvalidOperationException("Expected exactly one licensed motion "+name+", found "+matches.Length);return matches[0];
        }
        public static AnimationClip[] ReviewClips()
        {
            if(!Directory.Exists(SourceRoot))return Array.Empty<AnimationClip>();
            var all=AllClips();
            return new[]{"Idle_Loop","Sword_Attack","Crouch_Idle_Loop","Sitting_Idle_Loop","Idle_Torch_Loop","Interact"}.Select(name=>all.SingleOrDefault(c=>c.name==name||c.name.EndsWith("|"+name,StringComparison.Ordinal))).Where(c=>c!=null).ToArray()!;
        }
        private static string Hash(string path){using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
    }
}
