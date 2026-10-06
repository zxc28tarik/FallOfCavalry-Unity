#nullable enable
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FOC.Editor.Visuals
{
    /// <summary>Review a candidate in a real player, restoring the existing LOD2 bytes after the build.</summary>
    public static class MeshyLod2CandidateReview
    {
        public const string MeshPath=MeshyHasanPilotPipeline.OutputRoot+"/MESH_HasanAga_MeshyPilot_LOD2.asset";
        public static void RunBuildCandidate()
        {
            var code=0;
            var snapshot=File.ReadAllBytes(MeshPath);
            const string settingsPath="ProjectSettings/ProjectSettings.asset";
            var settingsSnapshot=File.ReadAllBytes(settingsPath);
            var backend=PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            var clone=(GameObject?)null;
            try
            {
                var args=Environment.GetCommandLineArgs();var index=Array.IndexOf(args,"-focLod2Candidate");
                if(index<0||index+1>=args.Length)throw new InvalidOperationException("Explicit candidate JSON required.");
                var json=File.ReadAllText(args[index+1]);
                var data=JsonUtility.FromJson<MeshyHasanPilotLodImport.LodData>(json);
                if(data.triangles.Length/3>=5752)throw new InvalidOperationException("Candidate must remain below preserved LOD1.");
                clone=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(MeshyHasanPilotPipeline.PrefabPath));
                MeshyHasanPilotPipeline.RestoreSourceBindPose(clone);
                var generated=MeshyHasanPilotLodImport.CreateDerivedMesh(clone,data,2);
                var existing=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
                EditorUtility.CopySerialized(generated,existing);UnityEngine.Object.DestroyImmediate(generated);EditorUtility.SetDirty(existing);AssetDatabase.SaveAssets();
                var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../Artifacts/MeshyLod2CandidatePlayer/FallOfCavalry-MeshyPilot.exe"));
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{MeshyHasanPilotBuild.ScenePath},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
                if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Candidate player build failed: "+report.summary.result);
                Debug.Log("FOC_LOD2_CANDIDATE_PLAYER_BUILT triangles="+data.triangles.Length/3+"; adoption and visual acceptance pending.");
            }
            catch(Exception e){Debug.LogException(e);code=1;}
            finally
            {
                if(clone!=null)UnityEngine.Object.DestroyImmediate(clone);
                File.WriteAllBytes(MeshPath,snapshot);AssetDatabase.ImportAsset(MeshPath,ImportAssetOptions.ForceUpdate);
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone,backend);
                HistoricalArtReviewBuild.RestoreSettingsSnapshot(settingsPath,settingsSnapshot);
                EditorApplication.Exit(code);
            }
        }

        public static void RunAdoptReviewedCandidate()
        {
            var clone=(GameObject?)null;var code=0;
            try
            {
                var data=JsonUtility.FromJson<MeshyHasanPilotLodImport.LodData>(File.ReadAllText(MeshyHasanPilotPipeline.SourceRoot+"/Hasan_Meshy_LOD2.json"));
                if(data.triangles.Length/3>=5752)throw new InvalidOperationException("Reviewed LOD2 must remain below LOD1.");
                clone=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(MeshyHasanPilotPipeline.PrefabPath));
                MeshyHasanPilotPipeline.RestoreSourceBindPose(clone);
                var mesh=MeshyHasanPilotLodImport.CreateDerivedMesh(clone,data,2);
                var existing=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
                EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);AssetDatabase.SaveAssets();
                Debug.Log("FOC_REVIEWED_LOD2_IMPORTED triangles="+data.triangles.Length/3+"; separate derivation ledger required.");
            }
            catch(Exception e){Debug.LogException(e);code=1;}
            finally{if(clone!=null)UnityEngine.Object.DestroyImmediate(clone);EditorApplication.Exit(code);}
        }
    }
}
