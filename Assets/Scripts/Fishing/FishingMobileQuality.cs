using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace BoatAttack.Fishing
{
    /// <summary>Android-only runtime copy. Project quality assets and desktop settings are never rewritten.</summary>
    public sealed class FishingMobileQuality : MonoBehaviour
    {
        private RenderPipelineAsset _previous;
        private UniversalRenderPipelineAsset _mobile;
        private float _lodBias;
        private void Awake()
        {
            if(Application.platform!=RuntimePlatform.Android)return;
            _previous=QualitySettings.renderPipeline;
            var source=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if(source==null)return;
            _mobile=Instantiate(source);_mobile.name="PICO mobile runtime quality";
            _mobile.renderScale=.85f;_mobile.msaaSampleCount=4;_mobile.shadowDistance=35;
            _mobile.maxAdditionalLightsCount=2;
            _mobile.useSRPBatcher=true;
            QualitySettings.renderPipeline=_mobile;_lodBias=QualitySettings.lodBias;QualitySettings.lodBias=.65f;
            // shortcut: conservative fixed render scale until on-device GPU timings justify dynamic resolution.
        }
        private void OnDestroy()
        {
            if(_mobile==null)return;
            if(QualitySettings.renderPipeline==_mobile)QualitySettings.renderPipeline=_previous;
            QualitySettings.lodBias=_lodBias;Destroy(_mobile);
        }
    }
}
