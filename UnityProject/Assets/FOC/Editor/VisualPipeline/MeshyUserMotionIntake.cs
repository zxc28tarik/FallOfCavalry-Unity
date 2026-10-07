#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using FOC.Presentation.Visuals;

namespace FOC.Editor.Visuals
{
    /// <summary>Two user-selected motions only. Local donor meshes never replace the retained character.</summary>
    public static class MeshyUserMotionIntake
    {
        public const string SourceRoot = "Assets/FOC/ArtSource/HistoricalSlice/TestResults/MeshyUserMotion";
        public static readonly string[] Names = { "Idle_12", "Right_Hand_Sword_Slash" };
        private static readonly string[] Hashes = {
            "c02617853820fa38f5e7e6fe00d8389309dd371aa4e4fa7fcba0757a3d730ea4",
            "b3c635a6b1c7534975241a74408e1baae2514396aa623a44e81b2e5fddd4439e" };
        [Serializable] public sealed class Record
        {
            public string motion="",path="",sha256="",avatar="",clip="",error="";
            public bool valid,human,humanoidClip,axisBaked;
            public float duration;
            public string[] mapping=Array.Empty<string>();
        }
        [Serializable] public sealed class Report
        {
            public string status="IMPORT_INCOMPLETE",unityVersion="",utc="";
            public string scope="Local user Meshy animation donor only. Retained model/rig/material untouched. Visual QA pending.";
            public Record[] motions=Array.Empty<Record>();
        }
        public static string PathFor(string motion) => SourceRoot+"/"+motion+".fbx";
        public static void RunImport()
        {
            var code=0;var report=new Report{unityVersion=Application.unityVersion,utc=DateTime.UtcNow.ToString("O")};
            var records=new System.Collections.Generic.List<Record>();
            try
            {
                AssetDatabase.Refresh();
                for(var i=0;i<Names.Length;i++)
                {
                    var path=PathFor(Names[i]);var record=new Record{motion=Names[i],path=path};records.Add(record);
                    try
                    {
                        record.sha256=Hash(path);
                        if(record.sha256!=Hashes[i])throw new InvalidOperationException("Selected source hash mismatch: "+path);
                        var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                        importer.animationType=ModelImporterAnimationType.Human;
                        importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
                        importer.importAnimation=true;importer.optimizeGameObjects=false;importer.preserveHierarchy=true;
                        importer.isReadable=true;importer.meshCompression=ModelImporterMeshCompression.Off;
                        importer.materialImportMode=ModelImporterMaterialImportMode.None;
                        importer.animationCompression=ModelImporterAnimationCompression.Off;
                        // No Quaternius-specific axis baking or target Avatar copying.
                        importer.SaveAndReimport();
                        var settings=importer.defaultClipAnimations;
                        if(settings.Length!=1)throw new InvalidOperationException("Expected one source stack: "+path);
                        settings[0].name=Names[i];settings[0].loopTime=i==0;settings[0].loopPose=false;
                        settings[0].lockRootPositionXZ=true;settings[0].keepOriginalPositionXZ=true;
                        settings[0].lockRootHeightY=true;settings[0].keepOriginalPositionY=true;
                        settings[0].lockRootRotation=true;settings[0].keepOriginalOrientation=false;
                        importer.clipAnimations=settings;importer.SaveAndReimport();
                        var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        var avatar=model.GetComponent<Animator>().avatar;
                        record.avatar=avatar==null?"":avatar.name;record.valid=avatar!=null&&avatar.isValid;
                        record.human=avatar!=null&&avatar.isHuman;record.axisBaked=importer.bakeAxisConversion;
                        record.mapping=record.human?avatar!.humanDescription.human.Select(b=>b.humanName+":"+b.boneName).ToArray():Array.Empty<string>();
                        var clip=Clip(Names[i]);record.clip=clip.name;record.duration=clip.length;record.humanoidClip=clip.isHumanMotion;
                        if(!record.valid||!record.human||!record.humanoidClip)throw new InvalidOperationException("Source Avatar or Humanoid clip invalid: "+Names[i]);
                        if(Hash(path)!=Hashes[i])throw new InvalidOperationException("Raw source changed during import.");
                    }
                    catch(Exception e){record.error=e.ToString();throw;}
                }
                report.status="IMPORT_PASS_RUNTIME_VISUAL_QA_PENDING";
            }
            catch(Exception e){Debug.LogException(e);code=1;}
            finally
            {
                report.motions=records.ToArray();var directory=System.IO.Path.GetFullPath("../TestResults/MeshyUserMotion");
                Directory.CreateDirectory(directory);File.WriteAllText(directory+"/import.json",JsonUtility.ToJson(report,true));
                AssetDatabase.SaveAssets();
            }
            EditorApplication.Exit(code);
        }
        public static AnimationClip Clip(string motion)
        {
            if(!Names.Contains(motion))throw new ArgumentException("Unselected motion: "+motion);
            var clips=AssetDatabase.LoadAllAssetsAtPath(PathFor(motion)).OfType<AnimationClip>()
                .Where(c=>!c.name.StartsWith("__preview__",StringComparison.Ordinal)).ToArray();
            if(clips.Length!=1||clips[0].name!=motion||!clips[0].isHumanMotion)
                throw new InvalidOperationException("Expected one validated Humanoid donor: "+motion);
            return clips[0];
        }
        public static void RunAudit()
        {
            var code=0;
            try
            {
                var report=new MeshyMotionLibraryIntake.ComparisonReport{unityVersion=Application.unityVersion,utc=DateTime.UtcNow.ToString("O")};
                var cases=new System.Collections.Generic.List<MeshyMotionLibraryIntake.MotionComparison>();
                var target=AssetDatabase.LoadAssetAtPath<GameObject>(MeshyHasanPilotBuild.PrefabPath);
                var calibrated=MeshyHumanoidCalibration.LoadTargetAvatar();
                foreach(var name in Names)
                {
                    var clip=Clip(name);var source=AssetDatabase.LoadAssetAtPath<GameObject>(PathFor(name));
                    cases.Add(MeshyMotionLibraryIntake.Measure(source,null,clip,"Source"));
                    cases.Add(MeshyMotionLibraryIntake.Measure(target,null,clip,"Original"));
                    cases.Add(MeshyMotionLibraryIntake.Measure(target,calibrated,clip,"Calibrated"));
                    cases.Add(MeshyMotionLibraryIntake.Measure(target,calibrated,clip,"CalibratedFootIK",true));
                }
                report.comparisons=cases.ToArray();var output=System.IO.Path.GetFullPath("../TestResults/MeshyUserMotion");
                Directory.CreateDirectory(output);File.WriteAllText(output+"/comparison.json",JsonUtility.ToJson(report,true));
            }
            catch(Exception e){Debug.LogException(e);code=1;}
            EditorApplication.Exit(code);
        }
        public static string ContactPath(string motion)=>SourceRoot+"/CONTACT_"+motion+".asset";
        public static void RunContactCandidates()
        {
            var code=0;
            try
            {
                var audit=JsonUtility.FromJson<MeshyMotionLibraryIntake.ComparisonReport>(File.ReadAllText("../TestResults/MeshyUserMotion/comparison.json"));
                foreach(var name in Names)
                {
                    var source=audit.comparisons.Single(c=>c.clip==name&&c.variant=="Source");
                    var target=audit.comparisons.Single(c=>c.clip==name&&c.variant=="Calibrated");
                    if(source.samples.Length!=target.samples.Length||source.samples.Length<3)throw new InvalidOperationException("Mismatched measured contact phases.");
                    var sourceFloor=source.samples.Min(s=>s.minimumSkinY);
                    var ratio=target.humanScale/source.humanScale;
                    var path=ContactPath(name);var profile=AssetDatabase.LoadAssetAtPath<MeshyTargetContactProfile>(path);
                    if(profile==null){profile=ScriptableObject.CreateInstance<MeshyTargetContactProfile>();AssetDatabase.CreateAsset(profile,path);}
                    profile.sourceClip=Clip(name);profile.targetAvatar=MeshyHumanoidCalibration.LoadTargetAvatar();
                    profile.groundY=-.015f;profile.useFootIK=false;profile.loop=name=="Idle_12";profile.maximumBodyCorrection=.12f;
                    profile.correctionMeters=target.samples.Select((sample,i)=>profile.groundY+(source.samples[i].minimumSkinY-sourceFloor)*ratio-sample.minimumSkinY).ToArray();
                    if(profile.correctionMeters.Any(v=>float.IsNaN(v)||float.IsInfinity(v)||Mathf.Abs(v)>.12f))
                        throw new InvalidOperationException("Measured user motion contact candidate exceeds 12cm presentation limit.");
                    profile.sourcePoseGateEvidence="Development Windows A/B captures: calibrated attack owns right hand; final visual acceptance pending.";
                    profile.supportProvenance="Per-clip source minimum skinned-surface envelope baseline, source/target humanScale ratio, sampled actual calibrated skin. Preserves source-relative foot lift; no actor/gameplay root movement, no bone or mesh edits.";
                    EditorUtility.SetDirty(profile);
                    Debug.Log("USER_MOTION_CONTACT_CANDIDATE "+name+" sourceFloor="+sourceFloor+" minCorrection="+profile.correctionMeters.Min()+" maxCorrection="+profile.correctionMeters.Max());
                }
                AssetDatabase.SaveAssets();
            }
            catch(Exception e){Debug.LogException(e);code=1;}
            EditorApplication.Exit(code);
        }
        private static string Hash(string path)
        {
            using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        }
    }
}
