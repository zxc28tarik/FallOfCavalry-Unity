using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FOC.Presentation.Visuals;
using UnityEditor;
using UnityEngine;

namespace FOC.Editor.Visuals
{
    /// <summary>Donor diagnostics only. Numeric validity does not approve styling or clipping.</summary>
    public static class ClothingDonorValidation
    {
        [Serializable] private sealed class Result
        {
            public string asset="";
            public int lods;
            public int bakedPoses;
            public int sharedClipSamples;
            public string result="";
        }
        [Serializable] private sealed class Report
        {
            public string unity="";
            public string status="DIAGNOSTICS_ONLY_NOT_PRODUCTION_ACCEPTANCE";
            public string limitation="Shared proof clips are pelvis-motion fixtures, not full locomotion. Mounted pose is static diagnostic IK. Visual clipping and production animation acceptance remain pending.";
            public List<Result> donors=new List<Result>();
        }
        public static void Run()
        {
            var code=0;var report=new Report{unity=Application.unityVersion};
            try
            {
                HistoricalArtCandidatePipeline.Generate();
                var files=Directory.GetFiles(HistoricalArtCandidatePipeline.SourceRoot+"/DonorTests","CLTH_Donor_*.focmesh.json").OrderBy(x=>x,StringComparer.Ordinal).ToArray();
                if(files.Length!=11)throw new InvalidOperationException("Expected exactly eleven selected donor sources.");
                var clips=AssetDatabase.FindAssets("t:AnimationClip",new[]{"Assets/FOC/Generated/VisualProof/Animations"}).Select(g=>AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(g))).Where(c=>c!=null).ToArray();
                if(clips.Length==0)throw new InvalidOperationException("Existing shared animation fixtures missing.");
                foreach(var file in files)
                {
                    var source=JsonUtility.FromJson<HistoricalArtCandidatePipeline.SourceAsset>(File.ReadAllText(file));
                    HistoricalArtCandidatePipeline.ValidateSource(source);
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(HistoricalArtCandidatePipeline.CharacterRoot+"/"+source.assetId+".prefab");
                    if(prefab==null)throw new InvalidOperationException("Missing donor prefab "+source.assetId);
                    var result=new Result{asset=source.assetId,lods=3};
                    foreach(var pose in new[]{"bind","standing","mounted"})
                    {
                        var instance=UnityEngine.Object.Instantiate(prefab);var seat=new GameObject("Donor diagnostic seat");
                        try
                        {
                            var animator=instance.GetComponent<Animator>();
                            if(animator==null||!animator.avatar.isValid||!animator.avatar.isHuman)throw new InvalidOperationException("Invalid canonical avatar");
                            if(pose=="standing")HistoricalArtPoseReview.Standing(instance);
                            if(pose=="mounted"){seat.transform.position=new Vector3(0,1.81f,-.42f);HistoricalArtPoseReview.Mounted(instance,seat.transform);}
                            CheckMeshes(instance);result.bakedPoses++;
                            if(pose=="bind")foreach(var clip in clips)foreach(var time in new[]{0f,.25f,.5f,.75f})
                            {clip.SampleAnimation(instance,time*clip.length);CheckMeshes(instance);result.sharedClipSamples++;}
                        }
                        finally{UnityEngine.Object.DestroyImmediate(instance);UnityEngine.Object.DestroyImmediate(seat);}
                    }
                    result.result="NUMERIC_PASS_VISUAL_QA_REQUIRED";report.donors.Add(result);
                    Debug.Log("FOC_DONOR_DIAGNOSTIC_PASS "+source.assetId+" poses="+result.bakedPoses+" clipSamples="+result.sharedClipSamples);
                }
            }
            catch(Exception e){Debug.LogException(e);report.status="FAILED";code=1;}
            var output=Path.GetFullPath("../TestResults/ClothingDonors/unity-donor-diagnostics.json");Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,JsonUtility.ToJson(report,true));
            EditorApplication.Exit(code);
        }
        private static void CheckMeshes(GameObject root)
        {
            var renderers=root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if(renderers.Length!=3)throw new InvalidOperationException("Expected one renderer per LOD");
            foreach(var renderer in renderers)
            {
                var mesh=new Mesh();
                try
                {
                    renderer.BakeMesh(mesh);
                    if(mesh.vertexCount==0)throw new InvalidOperationException("Empty baked donor");
                    foreach(var v in mesh.vertices)if(float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.x)||float.IsInfinity(v.y)||float.IsInfinity(v.z)||v.sqrMagnitude>36)throw new InvalidOperationException("Exploded donor deformation");
                }
                finally{UnityEngine.Object.DestroyImmediate(mesh);}
            }
        }
    }
}
