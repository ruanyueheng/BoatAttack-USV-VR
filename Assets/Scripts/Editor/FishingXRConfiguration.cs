using System;
using System.IO;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
namespace BoatAttack.EditorTools
{
    public static class FishingXRConfiguration
    {
        public static void Configure()
        {
            try{ConfigureSettings();Debug.Log("[XR-SETUP] PASS: Android-only OpenXR/PICO 4 Ultra profiles configured; no runtime/device certification.");EditorApplication.Exit(0);}
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void ConfigureSettings()
        {
            const string path="Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
            var settings=AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(path);
            if(settings==null)throw new InvalidOperationException("Existing XR settings asset could not be loaded; refusing to replace it blindly");
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey,settings,true);
            foreach(var target in new[]{BuildTargetGroup.Android,BuildTargetGroup.Standalone})
            {
                if(!settings.HasSettingsForBuildTarget(target))settings.CreateDefaultSettingsForBuildTarget(target);
                if(!settings.HasManagerSettingsForBuildTarget(target))settings.CreateDefaultManagerSettingsForBuildTarget(target);
                var general=settings.SettingsForBuildTarget(target);var manager=general.Manager;
                general.InitManagerOnStart=target==BuildTargetGroup.Android;
                manager.automaticLoading=manager.automaticRunning=target==BuildTargetGroup.Android;
                if(target==BuildTargetGroup.Android)
                {
                    if(!XRPackageMetadataStore.AssignLoader(manager,"UnityEngine.XR.OpenXR.OpenXRLoader",target))throw new Exception("Could not assign Android OpenXR loader");
                    if(manager.activeLoaders.Count!=1)throw new Exception("Refusing mixed Android XR loaders");
                }
                else if(manager.activeLoaders.Count!=0)throw new Exception("Windows must stay hardware-independent; review existing desktop loaders");
                EditorUtility.SetDirty(manager);EditorUtility.SetDirty(general);
            }
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            var xr=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            foreach(var feature in xr.GetFeatures<UnityEngine.XR.OpenXR.Features.OpenXRFeature>())feature.enabled=false;
            foreach(var id in new[]{"com.unity.openxr.feature.pico","com.unity.openxr.pico.features","com.unity.openxr.feature.input.PICO4Ultratouch","com.unity.openxr.feature.input.PICO4touch",FoveationFeature.featureId,Unity.XR.OpenXR.Features.PICOSupport.PerformanceSettingsFeature.featureId})
            {
                var feature=FeatureHelpers.GetFeatureWithIdForBuildTarget(BuildTargetGroup.Android,id);
                if(feature==null)throw new Exception("Missing pinned SDK feature: "+id);feature.enabled=true;EditorUtility.SetDirty(feature);
            }
            xr.renderMode=OpenXRSettings.RenderMode.SinglePassInstanced;EditorUtility.SetDirty(xr);
            // Light peripheral foveation preserves readability when looking around without turning the head.
            var foveation=Unity.XR.OpenXR.Features.PICOSupport.PICOProjectSetting.GetProjectConfig();
            foveation.foveationEnable=true;foveation.foveatedRenderingMode=FoveationFeature.FoveatedRenderingMode.FixedFoveatedRendering;
            foveation.foveatedRenderingLevel=FoveationFeature.FoveatedRenderingLevel.Low;EditorUtility.SetDirty(foveation);
            EditorUserBuildSettings.androidBuildSubtarget=MobileTextureSubtarget.ASTC;
            FishingPicoQuality.Ensure();
            PlayerSettings.bundleVersion=FishingPrototype.Version;
            PlayerSettings.Android.bundleVersionCode=52;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion=AndroidSdkVersions.AndroidApiLevel33;
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android,"com.ruanyueheng.tidefin.vr");
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.Vulkan});
            var platform=Unity.XR.PXR.PXR_PlatformSetting.Instance;
            // Offline development does not use Platform services. Avoid the SDK's null .Trim(), not its entitlement checks.
            if(platform.appID==null)platform.appID=string.Empty;
            platform.useHighlight=false; // No automatic gameplay-highlight recording in this offline prototype.
            if(platform.startTimeEntitlementCheck && string.IsNullOrWhiteSpace(platform.appID))throw new Exception("Configured entitlement check needs the developer's real app ID");
            EditorUtility.SetDirty(platform);EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
        }
        public static void BuildAndroid()
        {
            try
            {
                if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.Android)throw new Exception("Launch Unity with -buildTarget Android");
                ConfigureSettings();FishingScriptBinding.ValidateScene();AddressableAssetSettings.BuildPlayerContent(out var content);
                if(!string.IsNullOrEmpty(content.Error))throw new Exception(content.Error);
                string output=Path.GetFullPath("Builds/FishingPrototype-v"+FishingPrototype.Version+"-Android/FishingPrototype.apk");Directory.CreateDirectory(Path.GetDirectoryName(output));
                // LZ4 blocks support random asset reads without repeatedly inflating loose APK entries.
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{FishingPrototypeEditor.ScenePath},locationPathName=output,target=BuildTarget.Android,options=BuildOptions.CompressWithLz4});
                if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Android build failed: "+report.summary.result);
                Debug.Log("[XR-BUILD] PASS: ARM64 APK built: "+output+". Build validation does not certify headset comfort or controller calibration.");EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
