using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BoatAttack.EditorTools
{
    /// <summary>Idempotently (re)applies the PICO quality level + URP asset before Android builds, so a parallel editor saving ProjectSettings cannot silently drop them.</summary>
    public static class FishingPicoQuality
    {
        public const string Level="PICO",AssetPath="Assets/Data/UniversalRP/PipelineAsset_PICO.asset";
        public static void Ensure()
        {
            var asset=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetPath);
            if(asset==null)
            {
                if(!AssetDatabase.CopyAsset("Assets/Data/UniversalRP/PipelineAsset_Low.asset",AssetPath))throw new InvalidOperationException("Cannot create "+AssetPath);
                asset=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetPath);
            }
            // URP keeps these setters internal, so write the serialized fields.
            var urp=new SerializedObject(asset);
            Set(urp.FindProperty("m_SupportsHDR"),0);Set(urp.FindProperty("m_MSAA"),4);Set(urp.FindProperty("m_RenderScale"),Fishing.FishingMobileQuality.RenderScale);Set(urp.FindProperty("m_ShadowDistance"),25);Set(urp.FindProperty("m_RequireDepthTexture"),0);Set(urp.FindProperty("m_RequireOpaqueTexture"),0);
            Set(urp.FindProperty("m_ShadowCascadeCount"),1);Set(urp.FindProperty("m_MainLightShadowmapResolution"),1024);
            Set(urp.FindProperty("m_AdditionalLightShadowsSupported"),0);Set(urp.FindProperty("m_SoftShadowsSupported"),0);Set(urp.FindProperty("m_AdditionalLightsPerObjectLimit"),2);
            urp.ApplyModifiedPropertiesWithoutUndo();

            var quality=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels=quality.FindProperty("m_QualitySettings");int index=-1;
            for(int i=0;i<levels.arraySize;i++)if(levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue==Level)index=i;
            if(index<0){levels.InsertArrayElementAtIndex(levels.arraySize-1);index=levels.arraySize-1;}
            var level=levels.GetArrayElementAtIndex(index);
            level.FindPropertyRelative("name").stringValue=Level;
            foreach(var (name,value) in new[]{("pixelLightCount",1f),("shadowDistance",25f),("shadowCascades",1f),("antiAliasing",0f),("softParticles",0f),("realtimeReflectionProbes",0f),("vSyncCount",0f),("lodBias",1f),("anisotropicTextures",2f),("globalTextureMipmapLimit",0f),("streamingMipmapsMemoryBudget",512f),("streamingMipmapsMaxLevelReduction",2f)})
                Set(level.FindPropertyRelative(name),value);
            level.FindPropertyRelative("customRenderPipeline").objectReferenceValue=asset;
            var excluded=level.FindPropertyRelative("excludedTargetPlatforms");var platforms=new[]{"Standalone","iPhone","WebGL"};
            excluded.arraySize=platforms.Length;for(int i=0;i<platforms.Length;i++)excluded.GetArrayElementAtIndex(i).stringValue=platforms[i];
            var defaults=quality.FindProperty("m_PerPlatformDefaultQuality");bool android=false;
            for(int i=0;i<defaults.arraySize;i++){var pair=defaults.GetArrayElementAtIndex(i);if(pair.FindPropertyRelative("first").stringValue=="Android"){pair.FindPropertyRelative("second").intValue=index;android=true;}}
            if(!android)throw new InvalidOperationException("QualitySettings has no Android default entry");
            quality.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssets();
            Debug.Log("[PICO-QUALITY] Level "+Level+" (index "+index+") with "+AssetPath+" is the Android default.");
        }
        private static void Set(SerializedProperty p,float value)
        {
            if(p==null)throw new InvalidOperationException("Missing serialized quality field");
            switch(p.propertyType)
            {
                case SerializedPropertyType.Boolean:p.boolValue=value!=0;break;
                case SerializedPropertyType.Float:p.floatValue=value;break;
                default:p.intValue=(int)value;break;
            }
        }
    }
}
