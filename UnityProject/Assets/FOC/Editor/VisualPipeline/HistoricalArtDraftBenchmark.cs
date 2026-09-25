using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using Debug=UnityEngine.Debug;

namespace FOC.Editor.Visuals
{
    /// <summary>Asset-level comparison only, not a complete actor/cache or FPS gate.</summary>
    public static class HistoricalArtDraftBenchmark
    {
        [Serializable]private sealed class Report{public string scope="Draft prefab CPU lifecycle, not assembled actors or GPU/frame/skinning timing";public Row[] rows=Array.Empty<Row>();}
        [Serializable]private sealed class Row{public string asset="";public int count;public double coldSpawnMs;public double warmReactivateMs;public long managedDeltaBytes;public int nearRenderersPerInstance;public int materialsPerInstance;public int nearTrianglesPerInstance;public long sharedMeshBytes;}
        public static void Run()
        {
            try
            {
                var rows=new List<Row>();
                foreach(var path in new[]{VisualProofAssetGenerator.Root+"/Prefabs/CHR_Consolidated_Proof.prefab",HistoricalArtCandidatePipeline.CharacterRoot+"/CHR_Sipahi_01.prefab",VisualProofAssetGenerator.Root+"/Prefabs/MNT_Horse_Proof.prefab",HistoricalArtCandidatePipeline.MountRoot+"/MNT_Horse_Anatolian_01.prefab"})
                {
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path)??throw new InvalidOperationException("Missing benchmark prefab: "+path);
                    var renderers=prefab.GetComponent<LODGroup>().GetLODs()[0].renderers;var meshes=renderers.Select(r=>r is SkinnedMeshRenderer skin?skin.sharedMesh:r.GetComponent<MeshFilter>().sharedMesh).Distinct().ToArray();
                    foreach(var count in new[]{100,250,500})
                    {
                        var objects=new List<GameObject>();var before=GC.GetTotalMemory(true);var timer=Stopwatch.StartNew();
                        try
                        {
                            for(var i=0;i<count;i++){var obj=UnityEngine.Object.Instantiate(prefab);obj.GetComponent<LODGroup>().ForceLOD(0);objects.Add(obj);}timer.Stop();var cold=timer.Elapsed.TotalMilliseconds;var memory=GC.GetTotalMemory(false)-before;
                            foreach(var obj in objects)obj.SetActive(false);timer.Restart();foreach(var obj in objects)obj.SetActive(true);timer.Stop();
                            rows.Add(new Row{asset=prefab.name,count=count,coldSpawnMs=cold,warmReactivateMs=timer.Elapsed.TotalMilliseconds,managedDeltaBytes=memory,nearRenderersPerInstance=renderers.Length,materialsPerInstance=renderers.SelectMany(r=>r.sharedMaterials).Distinct().Count(),nearTrianglesPerInstance=meshes.Sum(m=>m.triangles.Length/3),sharedMeshBytes=meshes.Sum(m=>Profiler.GetRuntimeMemorySizeLong(m))});
                        }
                        finally{foreach(var obj in objects)UnityEngine.Object.DestroyImmediate(obj);}
                    }
                }
                var pathOut=Path.GetFullPath("../TestResults/14c-draft-prefab-benchmark.json");Directory.CreateDirectory(Path.GetDirectoryName(pathOut));File.WriteAllText(pathOut,JsonUtility.ToJson(new Report{rows=rows.ToArray()},true));Debug.Log("FOC_14C_DRAFT_PREFAB_BENCHMARK_COMPLETE "+pathOut);EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
