#nullable enable
using System;
using System.IO;
using System.Linq;
using FOC.Presentation.Visuals;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FOC.Editor.Visuals
{
    /// <summary>Builds only the isolated source-intake review scene, not the campaign.</summary>
    public static class MeshyHasanPilotBuild
    {
        public const string PrefabPath = "Assets/FOC/Presentation/Characters/Ottoman1648/MeshyPilot/CHR_HasanAga_MeshyPilot.prefab";
        public const string SourcePath = "Assets/FOC/ArtSource/HistoricalSlice/MeshyPilot/Source/Hasan_Meshy.fbx";
        public const string ScenePath = "Assets/FOC/ArtSource/HistoricalSlice/Review/MeshyHasanPilotReview.unity";

        public static void Run()
        {
            const string settingsPath = "ProjectSettings/ProjectSettings.asset";
            var settingsSnapshot = File.ReadAllBytes(settingsPath);
            var target = NamedBuildTarget.Standalone;
            var previousBackend = PlayerSettings.GetScriptingBackend(target);
            var previousRunInBackground = PlayerSettings.runInBackground;
            var previousScreenWidth = PlayerSettings.defaultScreenWidth;
            var previousScreenHeight = PlayerSettings.defaultScreenHeight;
            var previousFullScreenMode = PlayerSettings.fullScreenMode;
            var code = 0;
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)
                    ?? throw new InvalidOperationException("Import Meshy Hasan pilot before building the review player.");
                var animator = prefab.GetComponent<Animator>();
                if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                    throw new InvalidOperationException("Pilot must retain its own valid Humanoid avatar.");
                var imported = AssetDatabase.LoadAllAssetsAtPath(SourcePath).OfType<AnimationClip>()
                    .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
                var required = new[] { "Walking", "Running" }.Select(name =>
                {
                    var matches = imported.Where(clip => clip.name.Equals(name, StringComparison.OrdinalIgnoreCase)
                        || clip.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
                    if (matches.Length != 1) throw new InvalidOperationException("Expected one imported " + name + " humanoid clip; found " + matches.Length);
                    if (!matches[0].isHumanMotion) throw new InvalidOperationException(name + " was not imported as humanoid motion.");
                    return matches[0];
                }).ToArray();

                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
                AssetDatabase.Refresh();
                var existing = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
                var scene = existing == null
                    ? EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)
                    : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var reviews = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MeshyHasanPilotPlayer>(true)).ToArray();
                if (reviews.Length > 1) throw new InvalidOperationException("Review scene contains duplicate player components.");
                var review = reviews.SingleOrDefault();
                var changed = review == null;
                if (review == null) review = new GameObject("Isolated Meshy Hasan player review").AddComponent<MeshyHasanPilotPlayer>();
                if (review.characterPrefab != prefab) { review.characterPrefab = prefab; changed = true; }
                if (!review.clips.SequenceEqual(required)) { review.clips = required; changed = true; }
                var retargets=MeshyHasanPilotMotionAdaptation.LoadClips();
                if(!review.retargetClips.SequenceEqual(retargets)){review.retargetClips=retargets;changed=true;}
                var variants=new[]{4096,2048,1024}.Select(size=>AssetDatabase.LoadAssetAtPath<Material>(MeshyHasanPilotPipeline.OutputRoot+"/MAT_HasanAga_MeshyPilot"+(size==2048?"":"_"+size)+".mat")??throw new InvalidOperationException("Missing texture comparison variant "+size)).ToArray();
                if(!review.textureVariants.SequenceEqual(variants)){review.textureVariants=variants;changed=true;}
                // Optional calibration candidates never replace the original prefab Avatar.
                var calibrated=AssetDatabase.LoadAssetAtPath<Avatar>(MeshyHasanPilotPipeline.OutputRoot+"/Calibration/AVT_MeshyHasan_Calibrated.asset");
                if(review.calibratedAvatar!=calibrated){review.calibratedAvatar=calibrated;changed=true;}
                var dualPaths=MeshyHasanPilotMotionAdaptation.MotionNames.Select(name=>MeshyHasanPilotPipeline.OutputRoot+"/Calibration/ScenarioC/ANM_HumanoidCandidate_"+name+".anim").ToArray();
                var dual=dualPaths.Select(AssetDatabase.LoadAssetAtPath<AnimationClip>).Where(c=>c!=null).ToArray();
                if(dual.Length!=0&&dual.Length!=6)throw new InvalidOperationException("Partial dual-calibration candidate set.");
                if(!review.dualCalibrationClips.SequenceEqual(dual)){review.dualCalibrationClips=dual;changed=true;}
                // No SHA/time fields are serialized: final-SHA evidence is passed
                // via --meshy-sha. Rebuilding an unchanged scene does not churn it.
                if (changed) { EditorUtility.SetDirty(review); EditorSceneManager.SaveScene(scene, ScenePath); }
                PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.Mono2x);
                PlayerSettings.runInBackground = true;
                PlayerSettings.defaultScreenWidth = 1280;
                PlayerSettings.defaultScreenHeight = 900;
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                var output = Path.GetFullPath("../Artifacts/MeshyPilotPlayer/FallOfCavalry-MeshyPilot.exe");
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath }, locationPathName = output,
                    target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("Meshy pilot Windows build failed: " + report.summary.result);
                Debug.Log("FOC_MESHY_PILOT_BUILD_PASS output=" + output + " clips=" + string.Join(",", required.Select(clip => clip.name)));
            }
            catch (Exception exception) { Debug.LogException(exception); code = 1; }
            finally
            {
                PlayerSettings.SetScriptingBackend(target, previousBackend);
                // Restore Unity's in-memory state before restoring bytes: editor
                // shutdown may serialize these settings again after this method.
                PlayerSettings.runInBackground = previousRunInBackground;
                PlayerSettings.defaultScreenWidth = previousScreenWidth;
                PlayerSettings.defaultScreenHeight = previousScreenHeight;
                PlayerSettings.fullScreenMode = previousFullScreenMode;
                HistoricalArtReviewBuild.RestoreSettingsSnapshot(settingsPath, settingsSnapshot);
            }
            EditorApplication.Exit(code);
        }
    }
}
