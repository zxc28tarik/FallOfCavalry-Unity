#nullable enable
using System;
using System.IO;
using FOC.Application.Geography;
using FOC.Application.Save;
using FOC.Bootstrap.Unity;
using FOC.Infrastructure.Save;
using FOC.Domain.Validation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FOC.Editor.Integration
{
    public static class DevelopmentBuildBatch
    {
        private const string SceneDirectory = "Assets/FOC/Generated/Development";
        private const string ScenePath = SceneDirectory + "/VerticalSlice.unity";

        public static void BuildWindowsDevelopment()
        {
            var projectRoot = Path.GetDirectoryName(UnityEngine.Application.dataPath) ?? throw new InvalidOperationException("Unity project root is unavailable.");
            var projectSettingsPath = Path.Combine(projectRoot, "ProjectSettings", "ProjectSettings.asset");
            var projectSettingsSnapshot = File.ReadAllBytes(projectSettingsPath);
            var standaloneTarget = NamedBuildTarget.Standalone;
            var previousBackend = PlayerSettings.GetScriptingBackend(standaloneTarget);
            var exitCode = 0;
            try
            {
                ValidateProductionBootstrapData();
                EnsureScene();
                var repositoryRoot = Directory.GetParent(projectRoot)?.FullName ?? throw new InvalidOperationException("Repository root is unavailable.");
                var outputDirectory = Path.Combine(repositoryRoot, "Artifacts", "WindowsDevelopment");
                Directory.CreateDirectory(outputDirectory);
                // The acceptance artifact is a local Development player, not a release distribution.
                // Select the installed Mono backend explicitly so this proof build does not silently
                // depend on an optional IL2CPP module being present on the validation machine.
                PlayerSettings.SetScriptingBackend(standaloneTarget, ScriptingImplementation.Mono2x);
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = Path.Combine(outputDirectory, "FallOfCavalry.exe"),
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development | BuildOptions.AllowDebugging
                };
                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new InvalidOperationException("Windows Development build failed: " + report.summary.result);
                Debug.Log("FOC_WINDOWS_DEVELOPMENT_BUILD_PASS path=" + options.locationPathName + " size=" + report.summary.totalSize);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                exitCode = 1;
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(standaloneTarget, previousBackend);
                // Unity 6 expands the intentionally minimal project settings file whenever a
                // PlayerSettings property is touched. Restore the exact source-controlled bytes
                // after the temporary Development-build override so validation does not dirty it.
                File.WriteAllBytes(projectSettingsPath, projectSettingsSnapshot);
            }
            EditorApplication.Exit(exitCode);
        }

        public static void ValidateIntegratedCampaign()
        {
            try
            {
                ValidateProductionBootstrapData();
                Debug.Log("FOC_INTEGRATION_PIPELINE_PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void ValidateProductionBootstrapData()
        {
            var locations=Resources.Load<TextAsset>("FOC/Geography/vertical-slice-locations") ?? throw new InvalidOperationException("Vertical-slice locations were not imported.");
            var routes=Resources.Load<TextAsset>("FOC/Geography/vertical-slice-routes") ?? throw new InvalidOperationException("Vertical-slice routes were not imported.");
            if(Resources.Load<Texture2D>("FOC/Geography/MarmaraStrategyMap")==null)throw new InvalidOperationException("Production-candidate map art was not imported.");
            var campaign = VerticalSliceCampaignFactory.Create(locations.text,routes.text);
            var validator = new CampaignSaveValidator();
            var serializer = new CampaignSaveTextSerializer();
            var data = CampaignSaveMapper.ToSaveData(campaign);
            var validation = validator.Validate(data);
            if (!validation.IsValid) throw new InvalidOperationException("Vertical-slice campaign violates save invariants.");
            if(!new GeographyInvariantValidator().Validate(campaign).IsValid)throw new InvalidOperationException("Vertical-slice geography violates runtime invariants.");
            var read = serializer.Deserialize(serializer.Serialize(data));
            if (!read.Success || read.Data == null) throw new InvalidOperationException(read.Error ?? "Vertical-slice round-trip failed.");
            CampaignSaveMapper.ToRuntimeState(read.Data);
        }

        private static void EnsureScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
                return;
            }
            if (!AssetDatabase.IsValidFolder("Assets/FOC/Generated")) AssetDatabase.CreateFolder("Assets/FOC", "Generated");
            if (!AssetDatabase.IsValidFolder(SceneDirectory)) AssetDatabase.CreateFolder("Assets/FOC/Generated", "Development");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("FOC Vertical Slice Development Bootstrap");
            root.AddComponent<DevelopmentCampaignBootstrap>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }
    }
}
