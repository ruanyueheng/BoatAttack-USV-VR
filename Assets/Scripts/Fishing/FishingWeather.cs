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
        private AudioSource _audio;
        private Mesh _rainMesh;
        private GameObject _rain;
        private readonly Vector3[] _rainVertices = new Vector3[96 * 4];
        private readonly Color[] _rainColors = new Color[96 * 4];
        private float _rainTimer, _rainAmount, _saveTimer;
        private AmbientMode _ambientMode;
        private Color _equator, _ground;
        private float _reflection;
        private bool _focused = true;
        private readonly List<(Renderer renderer,int index,LightProbeUsage probes)> _bakedRenderers=new List<(Renderer,int,LightProbeUsage)>();
        private readonly List<(Terrain terrain,int index)> _bakedTerrains=new List<(Terrain,int)>();
        public float Daylight => Controller != null && Controller.Career != null ? Controller.Career.Calendar.Daylight : 1;
        private void OnApplicationFocus(bool focus) { _focused = focus; if (!focus && Controller?.Career != null) Controller.Save(); }
        private void OnApplicationPause(bool pause) { _focused = !pause; if (pause && Controller?.Career != null) Controller.Save(); }
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
                case BayWeather.Rain: return new Look(new Color(.76f,.85f,.96f),new Color(.49f,.6f,.72f),new Color(.44f,.57f,.63f),.58f,290,1,36);
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
            var clip=AudioClip.Create("Original soft coastal wind",samples.Length,1,22050,false);clip.SetData(samples,0);_owned.Add(clip);_audio.clip=clip;_audio.Play();
        }
        public void ToggleReducedEffects()
        {
            ReducedEffects=!ReducedEffects;
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
            if(!frozen && !Controller.Automated && (_saveTimer+=Time.deltaTime)>=30){_saveTimer=0;Controller.Save();}
            ApplyEnvironment();
            if(_audio!=null){_audio.mute=Controller.QuietMode || (Controller.Paused && !Controller.StartMenuOpen);_audio.volume=(.07f+_rainAmount*.13f)*Controller.AmbientVolume;}
            if(_rain!=null)
            {
                bool covered=(Controller.Realms!=null && Controller.Realms.Inside) || (Controller.TackleShop!=null && Controller.TackleShop.Inside);
                _rain.SetActive(_rainAmount>.03f && !covered);
                if(!Controller.Paused){_rainTimer+=Time.deltaTime; if(_rainTimer>=.05f){_rainTimer=0;UpdateRain();}}
            }
        }
        public void ApplyEnvironment()
        {
            if(!_captured)return;
            var a=Preset(Climate.Previous);var b=Preset(Climate.Current);float t=Mathf.SmoothStep(0,1,Climate.Blend);
            if(Controller.StartMenuOpen){a=b=Preset(BayWeather.Sunset);t=1;}
            var sky=Color.Lerp(a.sky,b.sky,t);var sun=Color.Lerp(a.sun,b.sun,t);float intensity=Mathf.Lerp(a.light,b.light,t);
            float daylight=Controller.StartMenuOpen?.82f:Daylight;
            if(DeckLight!=null){DeckLight.intensity=3f*(1-daylight);DeckLight.enabled=daylight<.95f;}
            float twilight=4*daylight*(1-daylight);
            var nightSky=new Color(.07f,.12f,.24f);
            sky=Color.Lerp(nightSky,sky,daylight);
            sun=Color.Lerp(new Color(.38f,.52f,.84f),Color.Lerp(sun,new Color(1,.52f,.27f),twilight*.65f),daylight);
            if(_sun!=null)
            {
                _sun.color=_sunColor*sun;_sun.intensity=_sunIntensity*intensity*Mathf.Lerp(.025f,1,daylight);
                float hour=Controller.StartMenuOpen?17.2f:Controller.Career.Calendar.Hour;
                float elevation=daylight>.01f?Mathf.Lerp(8,65,Mathf.Sin(Mathf.Clamp01((hour-6)/12)*Mathf.PI)):32;
                _sun.transform.rotation=Quaternion.Euler(elevation,_sunRotation.eulerAngles.y+(hour-12)*7,0);
            }
            if(_sky!=null)
            {
                _sky.SetFloat("_Daylight",daylight);_sky.SetFloat("_CoastTime",Time.unscaledTime);
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
            RenderSettings.ambientSkyColor=Color.Lerp(new Color(.028f,.043f,.075f),_ambient*Color.Lerp(Color.white,sky,.35f),daylight);
            RenderSettings.ambientEquatorColor=Color.Lerp(new Color(.015f,.023f,.040f),new Color(.32f,.38f,.39f),daylight);
            RenderSettings.ambientGroundColor=Color.Lerp(new Color(.008f,.012f,.019f),new Color(.15f,.18f,.18f),daylight);
            RenderSettings.ambientIntensity=_ambientIntensity*Mathf.Lerp(.82f,1,intensity);
            RenderSettings.reflectionIntensity=_reflection*Mathf.Lerp(.08f,1,daylight);
            _rainAmount=Mathf.Lerp(a.rain,b.rain,t);
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
                float y=7-Mathf.Repeat(Time.time*4.8f+i*.79f,7);
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
