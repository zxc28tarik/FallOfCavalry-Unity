#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FOC.Domain.Battle;
using FOC.Domain.Common;
using FOC.Domain.Soldiers;
using FOC.Presentation.Visuals;

namespace FOC.Editor.Visuals
{
    public static class BattlePipelineBatch
    {
        public const string BattleScenePath=VisualProofAssetGenerator.Root+"/Scenes/BattleProof.unity";
        public static void Run()
        {
            var exit=0;var report=new StringBuilder();try{VisualProofAssetGenerator.GenerateAll();GenerateProofScene(false);report.AppendLine("BATTLE_CONTENT=PASS");report.AppendLine("BATTLE_SCENE=PASS transient; versioned fixture preserved path="+BattleScenePath);foreach(var count in new[]{100,250,500})report.AppendLine(Measure(count));report.AppendLine("GPU_FPS=unavailable (batchmode -nographics)");report.AppendLine("UNITY="+Application.unityVersion);report.AppendLine("RESULT=PASS");}catch(Exception ex){exit=1;report.AppendLine("RESULT=FAIL");report.AppendLine(ex.ToString());UnityEngine.Debug.LogException(ex);}finally{var directory=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","TestResults"));Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"battle-pipeline-result.txt"),report.ToString());UnityEngine.Debug.Log(report.ToString());AssetDatabase.SaveAssets();EditorApplication.Exit(exit);}
        }
        [MenuItem("FOC/Battle/Generate Proof Scene")]
        public static void GenerateProofScene()=>GenerateProofScene(true);
        public static void GenerateProofScene(bool persist)
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var root=new GameObject("BattleProof");root.AddComponent<BattleProofSceneState>();var assembler=root.AddComponent<VisualSoldier3DAssembler>();assembler.Configure(AssetDatabase.LoadAssetAtPath<VisualCatalogAsset>(VisualProofAssetGenerator.CatalogPath),64,256);var camera=new GameObject("BattleCamera").AddComponent<Camera>();camera.transform.position=new Vector3(0,18,-28);camera.transform.rotation=Quaternion.Euler(28,0,0);var light=new GameObject("BattleLight").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(48,-24,0);var sectors=new[]{("sector-left",new Vector3(-10,0,0)),("sector-middle",Vector3.zero),("sector-right",new Vector3(10,0,0))};foreach(var x in sectors){var anchor=new GameObject("Sector_"+x.Item1);anchor.transform.position=x.Item2;anchor.transform.SetParent(root.transform);anchor.AddComponent<BattleSectorAnchor>().Configure(BattleSectorId.Create(x.Item1));}var kinds=new[]{"deli","humbaraci","bostanci","deli","humbaraci","bostanci"};for(var i=0;i<kinds.Length;i++){var fixture=VisualSmokeRunner.CreateProof(kinds[i]);var host=new GameObject((i<3?"SideA_":"SideB_")+i);host.transform.SetParent(root.transform);host.transform.position=new Vector3(i<3?-8+(i*2):4+((i-3)*2),0,i%2==0?0:2);var view=host.AddComponent<VisualSoldier3D>();host.AddComponent<BattleCombatantViewBinding>().Configure(view,assembler);assembler.Assemble(view,fixture.soldier,fixture.troop,fixture.equipment);}if(persist){EditorSceneManager.SaveScene(scene,BattleScenePath);AssetDatabase.Refresh();}
        }
        private static string Measure(int count)
        {
            var host=new GameObject("BattleBenchmark_"+count);var assembler=host.AddComponent<VisualSoldier3DAssembler>();assembler.Configure(AssetDatabase.LoadAssetAtPath<VisualCatalogAsset>(VisualProofAssetGenerator.CatalogPath),32,count);var fixture=VisualSmokeRunner.CreateProof("deli");var views=new List<VisualSoldier3D>(count);var watch=Stopwatch.StartNew();for(var i=0;i<count;i++){var view=new GameObject("Actor_"+i).AddComponent<VisualSoldier3D>();view.transform.SetParent(host.transform,false);assembler.Assemble(view,fixture.soldier,fixture.troop,fixture.equipment);views.Add(view);}watch.Stop();var bindMs=watch.Elapsed.TotalMilliseconds;var renderers=host.GetComponentsInChildren<Renderer>(true).Length;watch.Restart();foreach(var view in views)view.ReleaseVisual();watch.Stop();var releaseMs=watch.Elapsed.TotalMilliseconds;var line="ACTORS="+count+" active="+assembler.ActiveLeaseCount+" renderers="+renderers+" bind_ms="+bindMs.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" release_ms="+releaseMs.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+" created="+assembler.Metrics.CreatedRepresentations+" reused="+assembler.Metrics.ReusedRepresentations+" pooled="+assembler.PooledInstanceCount;UnityEngine.Object.DestroyImmediate(host);return line;
        }
    }
}
