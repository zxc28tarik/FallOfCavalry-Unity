#nullable enable
using System;
using System.IO;
using System.Linq;
using FOC.Presentation.Visuals;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FOC.Editor.Visuals
{
    public static class HistoricalArtReviewBuild
    {
        public static void Run()
        {
            var settings="ProjectSettings/ProjectSettings.asset";var snapshot=File.ReadAllBytes(settings);var target=NamedBuildTarget.Standalone;var previous=PlayerSettings.GetScriptingBackend(target);var code=0;
            try
            {
                var folder="Assets/FOC/ArtSource/HistoricalSlice/Review";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
                var scenePath=folder+"/HistoricalArtReview.unity";
                if(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath)==null)
                {
                    var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var root=new GameObject("Historical art draft review");var review=root.AddComponent<HistoricalArtReviewPlayer>();
                    review.candidates=new[]{HistoricalArtCandidatePipeline.MountRoot,HistoricalArtCandidatePipeline.CharacterRoot,HistoricalArtCandidatePipeline.EquipmentRoot}.SelectMany(p=>AssetDatabase.FindAssets("t:Prefab",new[]{p})).Select(g=>AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g))).OrderBy(p=>p.name,StringComparer.Ordinal).ToArray();
                    EditorSceneManager.SaveScene(scene,scenePath);
                }
                else EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Single);
                PlayerSettings.SetScriptingBackend(target,ScriptingImplementation.Mono2x);
                var output=Path.GetFullPath("../Artifacts/ArtReviewPlayer/FallOfCavalry-ArtReview.exe");Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{scenePath},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
                if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new InvalidOperationException("Art review player build failed.");
                Debug.Log("FOC_ART_REVIEW_PLAYER_BUILD_PASS "+output);
            }
            catch(Exception e){Debug.LogException(e);code=1;}
            finally{PlayerSettings.SetScriptingBackend(target,previous);RestoreSettingsSnapshot(settings,snapshot);}
            EditorApplication.Exit(code);
        }
        public static void RestoreSettingsSnapshot(string path,byte[] snapshot)
        {
            if(File.ReadAllBytes(path).SequenceEqual(snapshot))return;
            // Windows can keep ProjectSettings memory-mapped immediately after a
            // build. Truncating that file throws ERROR_USER_MAPPED_FILE (1224).
            var temporary=path+".14c-restore-"+Guid.NewGuid().ToString("N");
            try{File.WriteAllBytes(temporary,snapshot);File.Replace(temporary,path,null);}
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
    }
}
