#nullable enable
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FOC.Editor.Visuals
{
    public static class VisualPipelineBatch
    {
        public static void Run()
        {
            var exitCode=0;var summary=new StringBuilder();try{VisualProofAssetGenerator.GenerateAll();var validation=VisualProjectValidator.ValidateAll();summary.AppendLine("VALIDATION="+(validation.IsValid?"PASS":"FAIL"));foreach(var warning in validation.Warnings)summary.AppendLine("WARNING="+warning);foreach(var error in validation.Errors)summary.AppendLine("ERROR="+error);if(!validation.IsValid)throw new InvalidOperationException("Visual validation failed with "+validation.Errors.Count+" error(s).");foreach(var line in VisualSmokeRunner.Run())summary.AppendLine("SMOKE="+line);var benchmark=VisualBenchmarkRunner.Run();summary.AppendLine("BENCHMARK=PASS measurements="+benchmark.measurements.Count);summary.AppendLine("UNITY="+Application.unityVersion);summary.AppendLine("RESULT=PASS");}catch(Exception ex){exitCode=1;summary.AppendLine("RESULT=FAIL");summary.AppendLine(ex.ToString());Debug.LogException(ex);}finally{var directory=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","TestResults"));Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"visual-pipeline-result.txt"),summary.ToString());Debug.Log(summary.ToString());AssetDatabase.SaveAssets();EditorApplication.Exit(exitCode);}}
    }
}
