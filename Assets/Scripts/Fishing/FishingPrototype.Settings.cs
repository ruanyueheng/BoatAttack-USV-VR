using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using ShadowQuality = UnityEngine.ShadowQuality;
using ShadowResolution = UnityEngine.ShadowResolution;

namespace BoatAttack.Fishing
{
    public enum FishingAudioCategory { Music, Ambient, Effects, Voice }

    public sealed partial class FishingPrototype
    {
        private const string SettingsKey="Fishing.V032.";
        internal const float WindMixBase=.025f,WindRainMixBoost=.05f;
        public float MasterVolume { get; private set; }=1;
        public float MusicVolume { get; private set; }=1;
        public float AmbientVolume { get; private set; }=1;
        public float EffectsVolume { get; private set; }=1;
        public float VoiceVolume { get; private set; }=1;
        public float MouseSensitivity { get; private set; }=1;
        public float FieldOfView { get; private set; }=FishingViewRig.DesktopFieldOfView;
        public bool InvertLook { get; private set; }
        public int GraphicsQuality { get; private set; }=2;
        public FishingTitleVoyage TitleVoyage { get; private set; }
        public FishingCoastalPresentation CoastPresentation { get; private set; }
        public bool CoastalDepthReady { get; private set; }
        private bool _coastalWorldRendered;
        private sealed class AudioSetting
        {
            public AudioSource Source;
            public FishingAudioCategory Category;
            public float BaseVolume, OriginalVolume;
            public bool OriginalMute, OriginalIgnoreListener, AllowDuringPause;
        }
        private readonly Dictionary<AudioSource,AudioSetting> _settingAudio=new Dictionary<AudioSource,AudioSetting>();
        private bool _listenerCaptured, _graphicsCaptured;
        private float _originalListenerVolume, _originalLodBias;
        private ShadowQuality _originalShadows;
        private ShadowResolution _originalShadowResolution;
        private UniversalAdditionalCameraData _settingsCameraData;
        private bool _originalPostProcessing;

        public void SetSetting(string name,float value)
        {
            if(float.IsNaN(value) || float.IsInfinity(value))return;
            switch(name)
            {
                case "Master": MasterVolume=Mathf.Clamp01(value);value=MasterVolume;break;
                case "Music": MusicVolume=Mathf.Clamp01(value);value=MusicVolume;break;
                case "Ambient": AmbientVolume=Mathf.Clamp01(value);value=AmbientVolume;break;
                case "Effects": EffectsVolume=Mathf.Clamp01(value);value=EffectsVolume;break;
                case "Voice": VoiceVolume=Mathf.Clamp01(value);value=VoiceVolume;break;
                case "Sensitivity": MouseSensitivity=Mathf.Clamp(value,.25f,2.5f);value=MouseSensitivity;break;
                case "FOV": FieldOfView=Mathf.Clamp(value,55,90);value=FieldOfView;break;
                case "Invert": InvertLook=value>.5f;value=InvertLook?1:0;break;
                case "Quality": GraphicsQuality=Mathf.Clamp(Mathf.RoundToInt(value),0,2);value=GraphicsQuality;ApplyGraphicsQuality();break;
                default:return;
            }
            ApplyAudioSettings();
            if(!HasIsolatedProfile)PlayerPrefs.SetFloat(SettingsKey+name,value);
        }
        public void RegisterAudioSource(AudioSource source,FishingAudioCategory category,float baseVolume,bool allowDuringPause=false)
        {
            if(source==null || source.gameObject.scene!=gameObject.scene || float.IsNaN(baseVolume) || float.IsInfinity(baseVolume))return;
            if(!_settingAudio.TryGetValue(source,out var entry))
            {
                entry=new AudioSetting{Source=source,OriginalVolume=source.volume,OriginalMute=source.mute,OriginalIgnoreListener=source.ignoreListenerVolume};
                _settingAudio.Add(source,entry);
            }
            entry.Category=category;entry.BaseVolume=Mathf.Clamp01(baseVolume);entry.AllowDuringPause=allowDuringPause;
            ApplyAudioSettings();
        }
        /// <summary>Registered source gain before listener/master, quiet and pause gates. Unowned sources return zero.</summary>
        public float GetAudioChannelVolume(AudioSource source)
        {
            if(source==null || !_settingAudio.TryGetValue(source,out var entry))return 0;
            float volume=CategoryVolume(entry.Category);
            return entry.BaseVolume*volume;
        }
        public float CategoryVolume(FishingAudioCategory category)=>category==FishingAudioCategory.Music?MusicVolume
            :category==FishingAudioCategory.Ambient?AmbientVolume:category==FishingAudioCategory.Voice?VoiceVolume:EffectsVolume;
        public void SetAudioBaseVolume(AudioSource source,float baseVolume)
        {
            if(source==null || float.IsNaN(baseVolume) || float.IsInfinity(baseVolume) || !_settingAudio.TryGetValue(source,out var entry))return;
            baseVolume=Mathf.Clamp01(baseVolume);if(entry.BaseVolume==baseVolume)return;
            entry.BaseVolume=baseVolume;ApplyAudioSettings();
        }
        public void ApplyAudioSettings()
        {
            if(!isActiveAndEnabled)return;
            if(!_listenerCaptured){_originalListenerVolume=AudioListener.volume;_listenerCaptured=true;}
            AudioListener.volume=QuietMode?0:MasterVolume;
            foreach(var entry in _settingAudio.Values)
            {
                if(entry.Source==null)continue;
                float volume=CategoryVolume(entry.Category);
                entry.Source.ignoreListenerVolume=false;
                entry.Source.volume=entry.BaseVolume*volume;
                entry.Source.mute=entry.OriginalMute || QuietMode || (Paused && !StartMenuOpen && entry.Category!=FishingAudioCategory.Music && !entry.AllowDuringPause);
            }
        }
        public void PersistDisplaySettings(bool fullscreen,int windowWidth,int windowHeight)
        {
            if(!DesktopDisplay || HasIsolatedProfile)return;
            if(windowWidth>=640 && windowHeight>=480){_windowWidth=windowWidth;_windowHeight=windowHeight;}
            // SetResolution 延迟生效，持久化请求值，而不是同一帧仍旧的 Screen.fullScreen。
            PlayerPrefs.SetInt(SettingsKey+"Fullscreen",fullscreen?1:0);
            PlayerPrefs.SetInt(SettingsKey+"WindowWidth",_windowWidth);
            PlayerPrefs.SetInt(SettingsKey+"WindowHeight",_windowHeight);
            PlayerPrefs.Save();
        }
        public void SaveSettings(){if(!HasIsolatedProfile)PlayerPrefs.Save();}
        public bool QuitGame()
        {
            Save();SaveSettings();
            if(CanContinue && !string.IsNullOrEmpty(SaveWarning))
            {Notice("保存失败，暂未退出。请检查存档空间后重试。",8);return false;}
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
            return true;
        }
        private void InitializeSettings()
        {
            CoastPresentation=gameObject.AddComponent<FishingCoastalPresentation>();CoastPresentation.Build(this);
            if(_feedback!=null)_feedback.mute=false;
            RegisterAudioSource(_feedback,FishingAudioCategory.Effects,.45f);
            // Preserve the authored levels for restoration; apply the quieter mix only inside this fishing scene.
            foreach(var engine in FindObjectsOfType<Engine>(true))
            {
                RegisterEnvironmentLoop(engine.engineSound,.20f,.08f);
                RegisterEnvironmentLoop(engine.waterSound,.18f,.055f);
            }
            // The original island's spatial waves/jetty loops also belong to Ambient, not an uncontrolled second mix.
            foreach(var source in FindObjectsOfType<AudioSource>(true))
                if(source.gameObject.scene==gameObject.scene && !_settingAudio.ContainsKey(source) && source.loop && source.clip!=null
                    && (source.clip.name=="ShoreWaves" || source.clip.name=="ShoreRocks" || source.clip.name=="Jetty" || source.clip.name=="FlowingWater"))
                    RegisterEnvironmentLoop(source,.20f,.055f);
            if(!HasIsolatedProfile && Weather.ReducedEffects!=(PlayerPrefs.GetInt(SettingsKey+"ReducedEffects",0)==1))Weather.ToggleReducedEffects();
            foreach(string key in new[]{"Master","Music","Ambient","Effects","Voice","Sensitivity","FOV","Invert","Quality"})
            {
                float fallback=key=="FOV"?FishingViewRig.DesktopFieldOfView:key=="Quality"?2:key=="Invert"?0:1;
                SetSetting(key,HasIsolatedProfile?fallback:PlayerPrefs.GetFloat(SettingsKey+key,fallback));
            }
            if(!HasIsolatedProfile && DesktopDisplay && !HasArgument("-screen-fullscreen") && !HasArgument("-screen-width") && !HasArgument("-screen-height"))
            {
                _windowWidth=Mathf.Max(640,PlayerPrefs.GetInt(SettingsKey+"WindowWidth",1280));
                _windowHeight=Mathf.Max(480,PlayerPrefs.GetInt(SettingsKey+"WindowHeight",720));
                if(PlayerPrefs.HasKey(SettingsKey+"Fullscreen") && !HasArgument("-screen-fullscreen") && !HasArgument("-screen-width") && !HasArgument("-screen-height"))
                    SetFullscreen(PlayerPrefs.GetInt(SettingsKey+"Fullscreen",0)==1);
            }
            TitleVoyage=gameObject.AddComponent<FishingTitleVoyage>();TitleVoyage.Build(this);
            var water=WaterSystem.Water.Instance;
            if(water!=null)
            {
                var map=Resources.Load<FishingMapDefinition>("FishingMap");
                if(map==null || !map.Bounds.Valid)throw new System.InvalidOperationException("Missing world coverage for shoreline depth capture.");
                var bounds=map.Bounds;
                UnityEngine.Rendering.RenderPipelineManager.endCameraRendering+=CoastalWorldRendered;
                StartCoroutine(CaptureCoastalDepth(water,Rect.MinMaxRect((float)bounds.MinX-32,(float)bounds.MinZ-32,(float)bounds.MaxX+32,(float)bounds.MaxZ+32)));
            }
            FishingPerformanceCapture.AttachIfRequested(this);
            FishingDrawBreakdown.AttachIfRequested(this);
            FishingHitchTour.AttachIfRequested(this);
        }
        private void CoastalWorldRendered(UnityEngine.Rendering.ScriptableRenderContext context,Camera camera)
        {
            if(camera!=viewCamera)return;
            _coastalWorldRendered=true;
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering-=CoastalWorldRendered;
        }
        private System.Collections.IEnumerator CaptureCoastalDepth(WaterSystem.Water water,Rect bounds)
        {
            // The first real world render uploads new shores and establishes the cloned URP renderer.
            while(!_coastalWorldRendered)yield return null;
            if(water!=null && water.isActiveAndEnabled){water.CaptureDepthMap(bounds);CoastalDepthReady=true;}
        }
        private void RegisterEnvironmentLoop(AudioSource source,float scale,float maximum)
        {
            if(source==null || source.gameObject.scene!=gameObject.scene)return;
            float authored=_settingAudio.TryGetValue(source,out var entry)?entry.OriginalVolume:source.volume;
            RegisterAudioSource(source,FishingAudioCategory.Ambient,Mathf.Min(maximum,Mathf.Max(0,authored)*scale));
        }
        private void ApplyGraphicsQuality()
        {
            if(!DesktopDisplay || !isActiveAndEnabled)return;
            if(!_graphicsCaptured)
            {
                _graphicsCaptured=true;_originalShadows=QualitySettings.shadows;_originalShadowResolution=QualitySettings.shadowResolution;_originalLodBias=QualitySettings.lodBias;
                _settingsCameraData=viewCamera!=null?viewCamera.GetComponent<UniversalAdditionalCameraData>():null;
                if(_settingsCameraData!=null)_originalPostProcessing=_settingsCameraData.renderPostProcessing;
            }
            CoastPresentation?.SetQuality(GraphicsQuality);
            QualitySettings.shadows=GraphicsQuality==0?ShadowQuality.Disable:ShadowQuality.All;
            QualitySettings.shadowResolution=GraphicsQuality==2?ShadowResolution.High:ShadowResolution.Medium;
            QualitySettings.lodBias=GraphicsQuality==0?.8f:GraphicsQuality==1?1.2f:1.8f;
            if(_settingsCameraData!=null)_settingsCameraData.renderPostProcessing=GraphicsQuality>0;
        }
        private void OnEnable()
        {
            if(_initialized){ApplyAudioSettings();ApplyGraphicsQuality();}
        }
        public void RestoreAudioSettings()
        {
            if(_listenerCaptured){AudioListener.volume=_originalListenerVolume;_listenerCaptured=false;}
            foreach(var entry in _settingAudio.Values)if(entry.Source!=null)
            {
                entry.Source.volume=entry.OriginalVolume;entry.Source.mute=entry.OriginalMute;entry.Source.ignoreListenerVolume=entry.OriginalIgnoreListener;
            }
        }
        private void OnDisable()
        {
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering-=CoastalWorldRendered;
            RestoreAudioSettings();
            if(!_graphicsCaptured)return;
            QualitySettings.shadows=_originalShadows;QualitySettings.shadowResolution=_originalShadowResolution;QualitySettings.lodBias=_originalLodBias;
            if(_settingsCameraData!=null)_settingsCameraData.renderPostProcessing=_originalPostProcessing;
            _graphicsCaptured=false;
        }
    }
}
