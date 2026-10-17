using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingPrototype
    {
        private const string SettingsKey="Fishing.V032.";
        public float MasterVolume { get; private set; }=1;
        public float MusicVolume { get; private set; }=1;
        public float AmbientVolume { get; private set; }=1;
        public float EffectsVolume { get; private set; }=1;
        public float MouseSensitivity { get; private set; }=1;
        public float FieldOfView { get; private set; }=FishingViewRig.DesktopFieldOfView;
        public bool InvertLook { get; private set; }
        public int GraphicsQuality { get; private set; }=2;
        public FishingTitleVoyage TitleVoyage { get; private set; }
        public FishingCoastalPresentation CoastPresentation { get; private set; }
        public void SetSetting(string name,float value)
        {
            if(float.IsNaN(value) || float.IsInfinity(value))return;
            switch(name)
            {
                case "Master": MasterVolume=Mathf.Clamp01(value);value=MasterVolume;break;
                case "Music": MusicVolume=Mathf.Clamp01(value);value=MusicVolume;break;
                case "Ambient": AmbientVolume=Mathf.Clamp01(value);value=AmbientVolume;break;
                case "Effects": EffectsVolume=Mathf.Clamp01(value);value=EffectsVolume;break;
                case "Sensitivity": MouseSensitivity=Mathf.Clamp(value,.25f,2.5f);value=MouseSensitivity;break;
                case "FOV": FieldOfView=Mathf.Clamp(value,55,90);value=FieldOfView;break;
                case "Invert": InvertLook=value>.5f;value=InvertLook?1:0;break;
                case "Quality": GraphicsQuality=Mathf.Clamp(Mathf.RoundToInt(value),0,2);value=GraphicsQuality;ApplyGraphicsQuality();break;
                default:return;
            }
            AudioListener.volume=MasterVolume;
            if(_feedback!=null)_feedback.volume=.45f*EffectsVolume;
            if(!HasIsolatedProfile){PlayerPrefs.SetFloat(SettingsKey+name,value);}
        }
        public void SaveSettings(){if(!HasIsolatedProfile)PlayerPrefs.Save();}
        private void InitializeSettings()
        {
            CoastPresentation=gameObject.AddComponent<FishingCoastalPresentation>();CoastPresentation.Build(this);
            if(!HasIsolatedProfile && Weather.ReducedEffects!=(PlayerPrefs.GetInt(SettingsKey+"ReducedEffects",0)==1))Weather.ToggleReducedEffects();
            foreach(string key in new[]{"Master","Music","Ambient","Effects","Sensitivity","FOV","Invert","Quality"})
            {
                float fallback=key=="FOV"?FishingViewRig.DesktopFieldOfView:key=="Quality"?2:key=="Invert"?0:1;
                SetSetting(key,HasIsolatedProfile?fallback:PlayerPrefs.GetFloat(SettingsKey+key,fallback));
            }
            TitleVoyage=gameObject.AddComponent<FishingTitleVoyage>();TitleVoyage.Build(this);
        }
        private void ApplyGraphicsQuality()
        {
            if(!DesktopDisplay)return;
            CoastPresentation?.SetQuality(GraphicsQuality);
            QualitySettings.shadows=GraphicsQuality==0?ShadowQuality.Disable:ShadowQuality.All;
            QualitySettings.shadowResolution=GraphicsQuality==2?ShadowResolution.High:ShadowResolution.Medium;
            QualitySettings.lodBias=GraphicsQuality==0?.8f:GraphicsQuality==1?1.2f:1.8f;
            if(viewCamera!=null)viewCamera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing=GraphicsQuality>0;
        }
    }
}
