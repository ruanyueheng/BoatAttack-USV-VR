using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.XR.OpenXR.Features.PICOSupport;
namespace BoatAttack.Fishing
{
    /// <summary>PICO/Android runtime copy (also on desktop with -fishing-pico-quality for measurement). Project quality assets and desktop settings are never rewritten.</summary>
    public sealed class FishingMobileQuality : MonoBehaviour
    {
        public const string PreviewFlag="-fishing-pico-quality";
        public const float RenderScale=1f;
        public const float LodBias=.65f;
        public static bool Requested=>Application.platform==RuntimePlatform.Android || System.Array.IndexOf(System.Environment.GetCommandLineArgs(),PreviewFlag)>=0;
        private static bool Has(string flag)=>System.Array.IndexOf(System.Environment.GetCommandLineArgs(),flag)>=0;
        public static float ShadowCasterSize=>Has("-pico-keep-small-shadows")?4:8;
        // Desktop PICO preview drops desktop-only work the headset never does (IMGUI HUD, planar reflection camera), so counters match the headset.
        public static bool HeadsetPreview=>Application.platform!=RuntimePlatform.Android && Requested && !Has("-pico-keep-desktop-extras");
        private RenderPipelineAsset _previous;
        private UniversalRenderPipelineAsset _mobile;
        private float _lodBias;
        private void Awake()
        {
            StartCoroutine(WarmShadersAfterBuild());
            // Portal shader: single-layer loops on PICO, full layers on desktop.
            Shader.SetGlobalFloat("_FishingPortalLowEnd",Requested?1:0);
            if(!Requested)return;
            _previous=QualitySettings.renderPipeline;
            var source=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if(source==null)return;
            _mobile=Instantiate(source);_mobile.name="PICO mobile runtime quality";
            // -pico-keep-* flags exist only to measure each cost separately on desktop.
            _mobile.renderScale=RenderScale;_mobile.msaaSampleCount=4;_mobile.shadowDistance=Has("-pico-keep-shadow40")?40:Has("-pico-no-shadows")?0:25;
            bool sceneTextures=Has("-pico-keep-depth");
            _mobile.supportsCameraDepthTexture=sceneTextures;_mobile.supportsCameraOpaqueTexture=sceneTextures;
            if(sceneTextures)Shader.DisableKeyword("_WATER_NO_SCENE_TEXTURES");else Shader.EnableKeyword("_WATER_NO_SCENE_TEXTURES");_mobile.shadowCascadeCount=1;_mobile.supportsHDR=false;
            _mobile.maxAdditionalLightsCount=2;
            _mobile.useSRPBatcher=true;
            QualitySettings.renderPipeline=_mobile;_lodBias=QualitySettings.lodBias;QualitySettings.lodBias=LodBias;
            // Preserve the headset render target's full resolution; undersampling blurs text and distant detail.
            StartCoroutine(ApplyDetailAfterBuild());
        }
        private IEnumerator WarmShadersAfterBuild()
        {
            // Same moment the PICO detail pass runs: the world is assembled, the title screen still covers the view.
            for(int i=0;i<5;i++)yield return null;
            FishingShaderWarmup.Run(gameObject.scene);
        }
        private IEnumerator ApplyDetailAfterBuild()
        {
            // The world is assembled over the first frames; later-spawned props keep their default visibility.
            for(int i=0;i<5;i++)yield return null;
            if(HeadsetPreview)foreach(var planar in FindObjectsOfType<PlanarReflections>(true))planar.enabled=false;
            var detail=ApplyDetailBudget(gameObject.scene);
            if(PerformanceSettingsFeature.isExtensionEnable)
            {
                var cpu=PerformanceSettingsFeature.XrPerfSettingsSetPerformanceLevelEXT(XrPerfSettingsDomainEXT.XR_PERF_SETTINGS_DOMAIN_CPU_EXT,XrPerfSettingsLevelEXT.XR_PERF_SETTINGS_LEVEL_SUSTAINED_HIGH_EXT);
                var gpu=PerformanceSettingsFeature.XrPerfSettingsSetPerformanceLevelEXT(XrPerfSettingsDomainEXT.XR_PERF_SETTINGS_DOMAIN_GPU_EXT,XrPerfSettingsLevelEXT.XR_PERF_SETTINGS_LEVEL_SUSTAINED_HIGH_EXT);
                Debug.Log("[PICO-PRESENTATION] sustained performance CPU="+cpu+" GPU="+gpu);
            }
            Debug.Log("[PICO-PRESENTATION] version="+FishingPrototype.Version+" renderScale="+_mobile.renderScale+" MSAA="+_mobile.msaaSampleCount
                +" eyeTexture="+UnityEngine.XR.XRSettings.eyeTextureWidth+"x"+UnityEngine.XR.XRSettings.eyeTextureHeight+" turn="+FishingXRComfort.TurnName+" "+detail);
        }
        private static Transform Foliage(Transform t)
        {
            for(;t!=null;t=t.parent){var n=t.name;if(n.StartsWith("MeadowPatch")||n.StartsWith("FlowerPatch")||n.StartsWith("FernClump")||n.StartsWith("DuneGrass"))return t;}
            return null;
        }
        /// <summary>PICO detail budget: half-density grass/flower/fern clumps, no shadows from small props, and screen-size culling (small props early, trees/rocks late).</summary>
        public static string ApplyDetailBudget(UnityEngine.SceneManagement.Scene scene)
        {
            int thinned=0,unshadowed=0,culled=0;
            // Baked grass clusters (scene-art): PICO shows the half-density set.
            foreach(var root in scene.GetRootGameObjects())
                if(root.name=="V044 grass clusters")foreach(Transform set in root.transform){if(set.name=="Full")set.gameObject.SetActive(false);else if(set.name=="Half")set.gameObject.SetActive(true);}
            foreach(var root in scene.GetRootGameObjects())
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
                {
                    var go=renderer.gameObject;
                    var clump=Foliage(go.transform);
                    if(clump!=null)
                    {
                        // Half density, keyed by clump position so every LOD of a clump and every session agree.
                        var p=clump.position;
                        if((((Mathf.FloorToInt(p.x*3.1f)*73856093)^(Mathf.FloorToInt(p.z*3.1f)*19349663))&1)==0){renderer.forceRenderingOff=true;thinned++;continue;}
                    }
                    if(go.GetComponentInParent<LODGroup>()!=null || go.GetComponentInParent<Rigidbody>()!=null)continue;
                    float size=renderer.bounds.size.magnitude;if(size>15)continue;
                    if(size<=ShadowCasterSize && renderer.shadowCastingMode!=ShadowCastingMode.Off){renderer.shadowCastingMode=ShadowCastingMode.Off;unshadowed++;}
                    var group=go.AddComponent<LODGroup>();
                    group.SetLODs(new[]{new LOD(size<=4?.02f:.006f,new Renderer[]{renderer})});group.RecalculateBounds();culled++;
                }
            return "thinned="+thinned+" unshadowed="+unshadowed+" culled="+culled;
        }
        private void OnDestroy()
        {
            if(_mobile==null)return;
            Shader.DisableKeyword("_WATER_NO_SCENE_TEXTURES");
            if(QualitySettings.renderPipeline==_mobile)QualitySettings.renderPipeline=_previous;
            QualitySettings.lodBias=_lodBias;Destroy(_mobile);
        }
    }
}
