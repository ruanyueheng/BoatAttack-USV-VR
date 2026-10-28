using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    /// <summary>Development Windows player (profiler markers readable) for hitch attribution; output Builds/PerfDev, never shipped.</summary>
    public static class FishingPerfDevBuild
    {
        public static void Build()
        {
            try
            {
                FishingScriptBinding.ValidateScene();
                AddressableAssetSettings.BuildPlayerContent(out var content);
                if(!string.IsNullOrEmpty(content.Error))throw new Exception("Addressables: "+content.Error);
                string output=Path.GetFullPath("Builds/PerfDev/FishingPrototype.exe");Directory.CreateDirectory(Path.GetDirectoryName(output));
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{FishingPrototypeEditor.ScenePath},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
                if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Dev build failed: "+report.summary.result);
                Debug.Log("[PERF-DEV] PASS: "+output);EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
