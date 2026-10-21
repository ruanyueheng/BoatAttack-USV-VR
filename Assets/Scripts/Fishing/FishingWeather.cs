using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BoatAttack.Fishing
{
    /// <summary>Owns only fishing-scene climate changes; restores every borrowed environment value on disable.</summary>
    [DefaultExecutionOrder(180)]
    public sealed class FishingWeather : MonoBehaviour
    {
        public FishingPrototype Controller;
        public BayClimate Climate { get; } = new BayClimate();
        public bool ReducedEffects { get; private set; }
        public float StormAmount { get; private set; }
        public float CloudAmount { get; private set; }
        public int RainQuadBudget => ReducedEffects || UnityEngine.XR.XRSettings.isDeviceActive ? 32 : 96;
        private Light _sun;
        public Light DeckLight { get; private set; }
        private Material _sky, _originalSky;
        private Color _sunColor, _fogColor, _ambient, _skyTint;
        private string _skyTintProperty;
        private float _skyExposure;
        private float _sunIntensity, _fogStart, _fogEnd, _fogDensity, _ambientIntensity;
        private bool _fog, _captured;
        private FogMode _fogMode;
        private Quaternion _sunRotation;
        private readonly List<Behaviour> _suspended = new List<Behaviour>();
        private readonly List<Object> _owned = new List<Object>();
        private AudioSource _audio,_thunder;
        private float _cloudTime,_stormWait=8,_flashAge=1,_thunderDelay=-1,_lightning,_sunBaseIntensity;
        private int _strike;
        private Vector3 _lightningDirection=new Vector3(0,.28f,1).normalized;
        private Mesh _rainMesh;
        private GameObject _rain;
        private readonly Vector3[] _rainVertices = new Vector3[96 * 4];
        private readonly Color[] _rainColors = new Color[96 * 4];
        private float _rainTimer, _rainAmount;
        private AmbientMode _ambientMode;
        private Color _equator, _ground;
        private float _reflection;
        private bool _focused = true;
        private readonly List<(Renderer renderer,int index,LightProbeUsage probes)> _bakedRenderers=new List<(Renderer,int,LightProbeUsage)>();
        private readonly List<(Terrain terrain,int index)> _bakedTerrains=new List<(Terrain,int)>();
        public float Daylight => Controller != null && Controller.Career != null ? Controller.Career.Calendar.Daylight : 1;
        private void OnApplicationFocus(bool focus) { _focused=focus;if(!focus)ResetStorm();if(!focus && Controller?.Career!=null)Controller.Save(); }
        private void OnApplicationPause(bool pause) { _focused=!pause;if(pause)ResetStorm();if(pause && Controller?.Career!=null)Controller.Save(); }
        private void OnApplicationQuit() { if (Controller?.Career != null) Controller.Save(); }

        private struct Look
        {
            public Color sun, sky, fog;
            public float light, distance, rain, elevation;
            public Look(Color s, Color k, Color f, float l, float d, float r, float e) { sun=s; sky=k; fog=f; light=l; distance=d; rain=r; elevation=e; }
        }
        private static Look Preset(BayWeather weather)
        {
            switch (weather)
            {
                case BayWeather.Cloudy: return new Look(new Color(.87f,.92f,1),new Color(.72f,.80f,.9f),new Color(.57f,.67f,.73f),.73f,410,0,38);
                case BayWeather.Rain: return new Look(new Color(.64f,.72f,.85f),new Color(.30f,.37f,.46f),new Color(.22f,.28f,.34f),.31f,230,1,36);
                case BayWeather.Mist: return new Look(new Color(.98f,.91f,.79f),new Color(.86f,.88f,.82f),new Color(.66f,.76f,.75f),.74f,185,0,28);
                case BayWeather.Sunset: return new Look(new Color(1,.68f,.39f),new Color(1,.65f,.52f),new Color(.74f,.59f,.53f),.8f,480,0,18);
                default: return new Look(Color.white,Color.white,new Color(.68f,.81f,.83f),1,650,0,48);
            }
        }
        private void OnEnable()
        {
            CaptureEnvironment();
            if(_audio!=null)_audio.Play();
        }
        private void CaptureEnvironment()
        {
            if(_captured)return; _captured=true;
            _originalSky=RenderSettings.skybox; _sun=RenderSettings.sun;
            // Baked midday light cannot darken with the clock. Borrow only scene lighting assignments.
            foreach(var renderer in FindObjectsOfType<Renderer>())
            {
                _bakedRenderers.Add((renderer,renderer.lightmapIndex,renderer.lightProbeUsage));
                renderer.lightmapIndex=-1;renderer.lightProbeUsage=LightProbeUsage.Off;
            }
            foreach(var terrain in Terrain.activeTerrains){_bakedTerrains.Add((terrain,terrain.lightmapIndex));terrain.lightmapIndex=-1;}
            _fog=RenderSettings.fog; _fogMode=RenderSettings.fogMode; _fogColor=RenderSettings.fogColor;
            _fogStart=RenderSettings.fogStartDistance; _fogEnd=RenderSettings.fogEndDistance; _fogDensity=RenderSettings.fogDensity;
            _ambientMode=RenderSettings.ambientMode;_equator=RenderSettings.ambientEquatorColor;_ground=RenderSettings.ambientGroundColor;_reflection=RenderSettings.reflectionIntensity;
            _ambient=RenderSettings.ambientSkyColor; _ambientIntensity=RenderSettings.ambientIntensity;
            if(_sun!=null){_sunColor=_sun.color;_sunIntensity=_sun.intensity;_sunRotation=_sun.transform.rotation;}
            // The original race has independent environment writers; suspend only active instances.
            foreach(var day in FindObjectsOfType<DayNightController>())if(day.enabled){_suspended.Add(day);day.enabled=false;}
            foreach(var fog in FindObjectsOfType<FogOverride>())if(fog.enabled){_suspended.Add(fog);fog.enabled=false;}
            if(_originalSky!=null)
            {
                _sky=new Material(Resources.Load<Shader>("FishingCoastalSky")){name="Fishing watercolor sky (runtime)"};_owned.Add(_sky);
                _skyTintProperty=_sky.HasProperty("_SkyTint")?"_SkyTint":_sky.HasProperty("_Tint")?"_Tint":null;
                _skyTint=_skyTintProperty!=null?_sky.GetColor(_skyTintProperty):Color.white;
                _skyExposure=_sky.HasProperty("_Exposure")?_sky.GetFloat("_Exposure"):1f;
                RenderSettings.skybox=_sky;
            }
        }
        private void Start()
        {
            DeckLight=Controller.Player.GetComponentInChildren<Light>(true);
            foreach(var renderer in GetComponentsInChildren<Renderer>())
            {
                bool known=false;foreach(var entry in _bakedRenderers)if(entry.renderer==renderer){known=true;break;}
                if(known)continue;_bakedRenderers.Add((renderer,renderer.lightmapIndex,renderer.lightProbeUsage));renderer.lightmapIndex=-1;renderer.lightProbeUsage=LightProbeUsage.Off;
            }
            BuildRain();
            _audio=gameObject.AddComponent<AudioSource>(); _audio.playOnAwake=false;_audio.loop=true;_audio.spatialBlend=0;_audio.volume=0;
            var rng=new System.Random(731);var samples=new float[22050*4];float low=0;
            for(int i=0;i<samples.Length;i++){low=Mathf.Lerp(low,(float)rng.NextDouble()*2-1,.065f);samples[i]=low*Mathf.Sin(Mathf.PI*i/(samples.Length-1));}
            var clip=AudioClip.Create("Original soft coastal wind",samples.Length,1,22050,false);clip.SetData(samples,0);_owned.Add(clip);_audio.clip=clip;
            Controller.RegisterAudioSource(_audio,FishingAudioCategory.Ambient,.07f);_audio.Play();
            _thunder=gameObject.AddComponent<AudioSource>();_thunder.playOnAwake=false;_thunder.spatialBlend=0;
            var rumble=new float[22050*4];float deep=0,body=0;
            for(int i=0;i<rumble.Length;i++)
            {
                float noise=(float)rng.NextDouble()*2-1;deep=Mathf.Lerp(deep,noise,.025f);body=Mathf.Lerp(body,noise,.11f);
                float seconds=i/22050f;float envelope=(1-Mathf.Exp(-seconds*18))*Mathf.Exp(-seconds*1.25f)*(1-seconds/4);
                rumble[i]=Mathf.Clamp((deep*2.6f+body*.35f)*envelope,-.85f,.85f);
            }
            var thunder=AudioClip.Create("Original distant sea thunder",rumble.Length,1,22050,false);thunder.SetData(rumble,0);_owned.Add(thunder);_thunder.clip=thunder;
            Controller.RegisterAudioSource(_thunder,FishingAudioCategory.Effects,.42f);
        }
        public void ToggleReducedEffects()
        {
            ReducedEffects=!ReducedEffects;if(ReducedEffects)ResetStorm();
            if(Controller!=null && !Controller.HasIsolatedProfile){PlayerPrefs.SetInt("Fishing.V032.ReducedEffects",ReducedEffects?1:0);PlayerPrefs.Save();}
        }
        public void CyclePreview()
        {
            if(!Climate.Select(Climate.Forecast))Controller.Notice("天气正在缓慢过渡，请等待过渡结束。",3);
        }
        private void Update()
        {
            if(Controller==null || !_captured)return;
            bool frozen=Controller.Paused || Controller.StoryBlocksGameplay || (Controller.XRNotebook!=null && Controller.XRNotebook.IsOpen) || (!_focused && !Controller.Automated);
            Controller.Career.Calendar.Tick(Time.deltaTime, frozen || Controller.Automated);
            Climate.Tick(Time.deltaTime,frozen,Controller.Automated?(BayWeather?)null:Controller.Career.Calendar.Weather);
            if(!frozen)_cloudTime+=Time.deltaTime;
            ApplyEnvironment();
            bool covered=(Controller.Realms!=null && Controller.Realms.Inside) || (Controller.TackleShop!=null && Controller.TackleShop.Inside);
            TickStorm(Time.deltaTime,frozen || covered);
            if(_audio!=null)Controller.SetAudioBaseVolume(_audio,.07f+_rainAmount*.13f);
            if(_rain!=null)
            {
                _rain.SetActive(_rainAmount>.03f && !covered);
                if(!frozen){_rainTimer+=Time.deltaTime; if(_rainTimer>=.05f){_rainTimer=0;UpdateRain();}}
            }
        }
        public void ApplyEnvironment()
        {
            if(!_captured)return;
            var a=Preset(Climate.Previous);var b=Preset(Climate.Current);float t=Mathf.SmoothStep(0,1,Climate.Blend);
            if(Controller.StartMenuOpen){a=b=Preset(BayWeather.Sunset);t=1;}
            var sky=Color.Lerp(a.sky,b.sky,t);var sun=Color.Lerp(a.sun,b.sun,t);float intensity=Mathf.Lerp(a.light,b.light,t);
            float daylight=Controller.StartMenuOpen?.82f:Daylight;
            int shrine=Controller.Realms!=null && Controller.Realms.Inside && !Controller.StartMenuOpen?Controller.Realms.Island:-1;
            if(shrine>=0)
            {
                daylight=shrine==3?.14f:shrine==2?.48f:shrine==1?.75f:.82f;
                intensity=shrine==3?.70f:.72f;
                sky=shrine==3?new Color(.17f,.22f,.45f):shrine==1?new Color(.62f,.84f,.70f):new Color(.91f,.81f,.67f);
            }
            StormAmount=shrine>=0?0:Mathf.Lerp(a.rain,b.rain,t);
            CloudAmount=Mathf.Lerp(CloudCover(Climate.Previous),CloudCover(Climate.Current),t);
            if(Controller.StartMenuOpen || shrine>=0)CloudAmount=.18f;
            if(DeckLight!=null){DeckLight.intensity=3f*(1-daylight)+StormAmount*.75f;DeckLight.enabled=daylight<.95f || StormAmount>.3f;}
            float twilight=4*daylight*(1-daylight);
            var nightSky=new Color(.07f,.12f,.24f);
            sky=Color.Lerp(nightSky,sky,daylight);
            sun=Color.Lerp(new Color(.38f,.52f,.84f),Color.Lerp(sun,new Color(1,.52f,.27f),twilight*.65f),daylight);
            if(shrine>=0)sun=shrine==3?new Color(.48f,.63f,1):shrine==1?new Color(.81f,.94f,.74f):new Color(1,.88f,.69f);
            float home=Controller.Home!=null && shrine<0 && !Controller.StartMenuOpen?Mathf.SmoothStep(0,1,Mathf.InverseLerp(90,45,Vector3.Distance(Controller.viewCamera.transform.position,Controller.Home.Origin))):0;
            if(_sun!=null)
            {
                _sun.color=_sunColor*sun;_sunBaseIntensity=_sunIntensity*intensity*Mathf.Lerp(Mathf.Lerp(.025f,.11f,home),1,daylight);_sun.intensity=_sunBaseIntensity;
                float hour=Controller.StartMenuOpen?17.2f:Controller.Career.Calendar.Hour;
                float elevation=shrine>=0?42:daylight>.01f?Mathf.Lerp(8,65,Mathf.Sin(Mathf.Clamp01((hour-6)/12)*Mathf.PI)):32;
                _sun.transform.rotation=Quaternion.Euler(elevation,_sunRotation.eulerAngles.y+(hour-12)*7,0);
            }
            if(_sky!=null)
            {
                _sky.SetFloat("_Daylight",daylight);_sky.SetFloat("_CoastTime",_cloudTime);
                _sky.SetFloat("_Storm",StormAmount);_sky.SetFloat("_CloudCover",CloudAmount);
                if(_sun!=null)_sky.SetVector("_SunDirection",-_sun.transform.forward);
                // This island uses _SkyTint, not the cubemap shader's _Tint. Its original
                // exposure=4 blows out the horizon, so only this runtime clone is capped.
                if(_skyTintProperty!=null)_sky.SetColor(_skyTintProperty,(_skyTintProperty=="_SkyTint"?new Color(.5f,.5f,.5f):_skyTint)*sky);
                if(_sky.HasProperty("_Exposure"))_sky.SetFloat("_Exposure",Mathf.Min(_skyExposure,1.4f)*Mathf.Lerp(.72f,1,intensity)*Mathf.Lerp(.20f,1,daylight));
            }
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;
            RenderSettings.fogColor=Color.Lerp(new Color(.018f,.031f,.064f),Color.Lerp(a.fog,b.fog,t),daylight);RenderSettings.fogStartDistance=35;
            RenderSettings.fogEndDistance=Mathf.Lerp(a.distance,b.distance,t);
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=Color.Lerp(Color.Lerp(new Color(.028f,.043f,.075f),new Color(.065f,.085f,.13f),home),_ambient*Color.Lerp(Color.white,sky,.35f),daylight);
            RenderSettings.ambientEquatorColor=Color.Lerp(Color.Lerp(new Color(.015f,.023f,.040f),new Color(.035f,.05f,.075f),home),new Color(.32f,.38f,.39f),daylight);
            RenderSettings.ambientGroundColor=Color.Lerp(Color.Lerp(new Color(.008f,.012f,.019f),new Color(.018f,.026f,.035f),home),new Color(.15f,.18f,.18f),daylight);
            RenderSettings.ambientSkyColor*=Mathf.Lerp(1,.52f,StormAmount);
            RenderSettings.ambientEquatorColor*=Mathf.Lerp(1,.48f,StormAmount);RenderSettings.ambientGroundColor*=Mathf.Lerp(1,.58f,StormAmount);
            RenderSettings.ambientIntensity=_ambientIntensity*Mathf.Lerp(.82f,1,intensity);
            RenderSettings.reflectionIntensity=_reflection*Mathf.Lerp(.08f,1,daylight)*Mathf.Lerp(1,.34f,StormAmount);
            _rainAmount=StormAmount;
            if(StormAmount<.7f || Climate.Current!=BayWeather.Rain || ReducedEffects || Controller.StartMenuOpen)ResetStorm();
            ApplyFlash();
        }
        private static float CloudCover(BayWeather weather)
        {
            switch(weather){case BayWeather.Rain:return 1;case BayWeather.Cloudy:return .82f;case BayWeather.Mist:return .68f;default:return .18f;}
        }
        private void TickStorm(float seconds,bool frozen)
        {
            if(frozen || !_focused || ReducedEffects || StormAmount<.7f || Climate.Current!=BayWeather.Rain || Controller.StartMenuOpen){ResetStorm();return;}
            seconds=Mathf.Clamp(seconds,0,.25f);
            if(_thunderDelay>=0)
            {
                _thunderDelay-=seconds;
                if(_thunderDelay<=0){_thunderDelay=-1;if(_thunder!=null && !Controller.QuietMode)_thunder.Play();}
            }
            _flashAge+=seconds;_stormWait-=seconds;
            if(_stormWait<=0)
            {
                _strike++;_flashAge=0;_stormWait=13+(_strike*7%11);_thunderDelay=.9f+(_strike%3)*.35f;
                float bearing=(_strike*137.508f+31)*Mathf.Deg2Rad;
                _lightningDirection=new Vector3(Mathf.Sin(bearing),.25f+(_strike%3)*.04f,Mathf.Cos(bearing)).normalized;
            }
            // A single soft attack/decay avoids repeated full-screen strobing.
            _lightning=_flashAge<.38f?Mathf.Min(1,_flashAge/.035f)*Mathf.Exp(-_flashAge*9)*.65f:0;
            ApplyFlash();
        }
        private void ApplyFlash()
        {
            if(_sky!=null){_sky.SetFloat("_Lightning",_lightning);_sky.SetVector("_LightningDirection",_lightningDirection);}
            if(_sun!=null)_sun.intensity=_sunBaseIntensity+_lightning*StormAmount*.32f;
        }
        private void ResetStorm()
        {
            _stormWait=8;_flashAge=1;_thunderDelay=-1;_lightning=0;
            if(_thunder!=null)_thunder.Stop();ApplyFlash();
        }
        public void RefreshForCapture()
        {
            if(Controller==null || !Controller.Automated)return;
            ApplyEnvironment();if(_rain!=null){_rain.SetActive(_rainAmount>.03f);UpdateRain();}
        }
        private void BuildRain()
        {
            _rain=new GameObject("Bounded coastal rain",typeof(MeshFilter),typeof(MeshRenderer));_rain.transform.SetParent(transform,false);
            var mat=new Material(Controller.PaletteShader){name="Rain palette"};_owned.Add(mat);mat.SetFloat("_Unlit",1);
            _rainMesh=new Mesh{name="Rain quads (96 maximum)"};_rainMesh.MarkDynamic();_owned.Add(_rainMesh);
            var triangles=new int[96*12];
            for(int i=0;i<96;i++){int v=i*4,j=i*12;int[] f={v,v+1,v+2,v,v+2,v+3,v+2,v+1,v,v+3,v+2,v};for(int k=0;k<12;k++)triangles[j+k]=f[k];}
            _rainMesh.vertices=_rainVertices;_rainMesh.triangles=triangles;_rainMesh.colors=_rainColors;
            _rain.GetComponent<MeshFilter>().sharedMesh=_rainMesh;var renderer=_rain.GetComponent<MeshRenderer>();renderer.sharedMaterial=mat;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            UpdateRain();
        }
        private void UpdateRain()
        {
            if(Controller.Player==null)return;
            _rain.transform.position=Controller.ExplorerPosition;
            int count=RainQuadBudget;
            for(int i=0;i<96;i++)
            {
                float angle=i*2.399963f;float radius=4.5f+(i%11)*.65f;
                float y=7-Mathf.Repeat(_cloudTime*4.8f+i*.79f,7);
                var p=new Vector3(Mathf.Cos(angle)*radius,y,Mathf.Sin(angle)*radius);
                var width=Vector3.Cross(Controller.viewCamera.transform.forward,Vector3.down).normalized*.011f;
                float length=i<count?.18f*_rainAmount:0;
                _rainVertices[i*4]=p-width;_rainVertices[i*4+1]=p+width;_rainVertices[i*4+2]=p+width+new Vector3(-.04f,-length,0);_rainVertices[i*4+3]=p-width+new Vector3(-.04f,-length,0);
                var color=new Color(.72f,.82f,.86f);for(int j=0;j<4;j++)_rainColors[i*4+j]=color;
                if(i>=count)for(int j=0;j<4;j++)_rainVertices[i*4+j]=p;
            }
            _rainMesh.vertices=_rainVertices;_rainMesh.colors=_rainColors;_rainMesh.RecalculateNormals();_rainMesh.RecalculateBounds();
        }
        private void OnDisable()
        {
            ResetStorm();
            if(!_captured)return;_captured=false;
            foreach(var entry in _bakedRenderers)if(entry.renderer!=null){entry.renderer.lightmapIndex=entry.index;entry.renderer.lightProbeUsage=entry.probes;}
            foreach(var entry in _bakedTerrains)if(entry.terrain!=null)entry.terrain.lightmapIndex=entry.index;
            _bakedRenderers.Clear();_bakedTerrains.Clear();
            RenderSettings.skybox=_originalSky;RenderSettings.fog=_fog;RenderSettings.fogMode=_fogMode;RenderSettings.fogColor=_fogColor;
            RenderSettings.fogStartDistance=_fogStart;RenderSettings.fogEndDistance=_fogEnd;RenderSettings.fogDensity=_fogDensity;
            RenderSettings.ambientMode=_ambientMode;RenderSettings.ambientEquatorColor=_equator;RenderSettings.ambientGroundColor=_ground;RenderSettings.reflectionIntensity=_reflection;
            RenderSettings.ambientSkyColor=_ambient;RenderSettings.ambientIntensity=_ambientIntensity;
            if(_sun!=null){_sun.color=_sunColor;_sun.intensity=_sunIntensity;_sun.transform.rotation=_sunRotation;}
            foreach(var writer in _suspended)if(writer!=null)writer.enabled=true;_suspended.Clear();
            if(_rain!=null)_rain.SetActive(false);if(_audio!=null)_audio.Stop();
            if(_sky!=null){_owned.Remove(_sky);Destroy(_sky);_sky=null;}
        }
        private void OnDestroy(){foreach(var item in _owned)if(item!=null)Destroy(item);}
    }
}
