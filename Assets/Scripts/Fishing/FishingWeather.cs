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
        public Vector3 SunDirection { get; private set; } = Vector3.up;
        public Vector3 MoonDirection { get; private set; } = Vector3.up;
        /// <summary>0 new, .5 full; one lunar cycle per 28-day season.</summary>
        public float MoonPhase { get; private set; }
        /// <summary>Camera has a roof overhead (home, shop, cabin, any building).</summary>
        public bool Sheltered { get; private set; }
        /// <summary>Clock speed multiplier (e.g. lighthouse night watch fast dusk). Calendar caps each tick at .25s, so above ~15x it saturates.</summary>
        public float TimeScale { get; set; } = 1;
        /// <summary>Sea-event fog target 0..1 (lamp-signal event); eases over ~4s to ~45m visibility.</summary>
        public float EventFog { get; set; }
        private float _eventFog;
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
        private readonly float[] _rainRoof = new float[96];
        private Vector3 _rainRoofOrigin = Vector3.positiveInfinity;
        private Texture2D _skyNoise;
        private float _meteorWait = 30, _meteorAge = -1;
        private Vector3 _meteorHead, _meteorDir;
        private Mesh _cardMesh;
        private Material _cardMaterial;
        private readonly Vector3[] _cardVertices=new Vector3[CardMax*4];
        private readonly Vector2[] _cardSun=new Vector2[CardMax*4];
        private readonly Color[] _cardColors=new Color[CardMax*4];
        public const int CardMax=12;
        /// <summary>Cards sit this far out; the camera far plane must exceed it by 200m (asserted in V044 sky validation).</summary>
        public const float CardDistance=1200;
        private readonly Vector4[] _cardDirs=new Vector4[CardMax];
        /// <summary>Distant watercolor cumulus cards; PICO keeps 6.</summary>
        public int CloudCardCount => ReducedEffects || UnityEngine.XR.XRSettings.isDeviceActive ? 6 : CardMax;
        public Renderer CloudCards { get; private set; }
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
                if(_skyNoise==null){_skyNoise=SkyNoise();_owned.Add(_skyNoise);}_sky.SetTexture("_NoiseTex",_skyNoise);_sky.SetTexture("_CumulusTex",Resources.Load<Texture2D>("FishingSkyCumulus"));
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
            BuildCloudCards();
            _audio=gameObject.AddComponent<AudioSource>(); _audio.playOnAwake=false;_audio.loop=true;_audio.spatialBlend=0;_audio.volume=0;
            var rng=new System.Random(731);var samples=new float[22050*4];float low=0;
            for(int i=0;i<samples.Length;i++){low=Mathf.Lerp(low,(float)rng.NextDouble()*2-1,.065f);samples[i]=low*Mathf.Sin(Mathf.PI*i/(samples.Length-1));}
            var clip=AudioClip.Create("Original soft coastal wind",samples.Length,1,22050,false);clip.SetData(samples,0);_owned.Add(clip);_audio.clip=clip;
            Controller.RegisterAudioSource(_audio,FishingAudioCategory.Ambient,FishingPrototype.WindMixBase);_audio.Play();
            _thunder=gameObject.AddComponent<AudioSource>();_thunder.playOnAwake=false;_thunder.spatialBlend=0;
            var rumble=new float[22050*4];float deep=0,body=0;
            for(int i=0;i<rumble.Length;i++)
            {
                float noise=(float)rng.NextDouble()*2-1;deep=Mathf.Lerp(deep,noise,.025f);body=Mathf.Lerp(body,noise,.11f);
                float seconds=i/22050f;float envelope=(1-Mathf.Exp(-seconds*18))*Mathf.Exp(-seconds*1.25f)*(1-seconds/4);
                rumble[i]=Mathf.Clamp((deep*2.6f+body*.35f)*envelope,-.85f,.85f);
            }
            var thunder=AudioClip.Create("Original distant sea thunder",rumble.Length,1,22050,false);thunder.SetData(rumble,0);_owned.Add(thunder);_thunder.clip=thunder;
            Controller.RegisterAudioSource(_thunder,FishingAudioCategory.Ambient,.12f);
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
            Controller.Career.Calendar.Tick(Time.deltaTime*Mathf.Max(0,TimeScale), frozen || Controller.Automated);
            Climate.Tick(Time.deltaTime,frozen,Controller.Automated?(BayWeather?)null:Controller.Career.Calendar.Weather);
            if(!frozen)_cloudTime+=Time.deltaTime;
            TickMeteor(frozen?0:Time.deltaTime);
            ApplyEnvironment();
            var eye=Controller.viewCamera.transform.position;
            // Only explicit volumes or colliders named Roof/Indoor count: tree crowns and bridges stay outdoors.
            Sheltered=Indoor(eye) || (Physics.Raycast(eye,Vector3.up,out var roof,10,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) && IsRoof(roof.collider));
            bool covered=(Controller.Realms!=null && Controller.Realms.Inside) || (Controller.TackleShop!=null && Controller.TackleShop.Inside);
            TickStorm(Time.deltaTime,frozen || covered);
            if(_audio!=null)Controller.SetAudioBaseVolume(_audio,FishingPrototype.WindMixBase+_rainAmount*FishingPrototype.WindRainMixBoost);
            if(_rain!=null)
            {
                _rain.SetActive(_rainAmount>.03f && !covered);
                if(!frozen){_rainTimer+=Time.deltaTime; if(_rainTimer>=.05f){_rainTimer=0;UpdateRain();}}
            }
        }
        /// <summary>Daily wind bearing; strength comes from the weather (cloudy = strong sea wind).</summary>
        public Vector3 WindDirection => Quaternion.Euler(0,Controller!=null && Controller.Career!=null?Controller.Career.Calendar.Day*73%360:0,0)*Vector3.forward;
        public float WindStrength => Mathf.Lerp(Wind(Climate.Previous),Wind(Climate.Current),Mathf.SmoothStep(0,1,Climate.Blend));
        private static float Wind(BayWeather weather)=>weather==BayWeather.Cloudy?1:weather==BayWeather.Rain?.6f:0;
        /// <summary>Peak extra rain roll in degrees; kept small in VR for comfort.</summary>
        public float RainRollLimit => UnityEngine.XR.XRSettings.isDeviceActive || Controller.ComfortMode ? 3f : 6f;
        private void FixedUpdate()
        {
            if(Controller==null || Controller.Player==null || Controller.Career==null || Controller.Paused || Controller.Ashore || Controller.StoryBlocksGameplay || Controller.StartMenuOpen)return;
            var rb=Controller.Player.engine.RB;var forward=Vector3.ProjectOnPlane(rb.transform.forward,Vector3.up).normalized;
            // Swell: rocking torque that stops pushing past the comfort limit.
            if(StormAmount>.01f)
            {
                float roll=Vector3.SignedAngle(Vector3.up,Vector3.ProjectOnPlane(rb.transform.up,forward),forward);
                float push=Mathf.Sin(Time.time*2*Mathf.PI/5.5f)*StormAmount;
                if(Mathf.Abs(roll)<RainRollLimit || Mathf.Sign(push)!=Mathf.Sign(roll))rb.AddTorque(forward*push*1.6f,ForceMode.Acceleration);
            }
            // Tailwind speeds the boat under way, headwind slows it.
            if(WindStrength>.01f && !Controller.Anchored && Controller.Speed>1)
                rb.AddForce(forward*Vector3.Dot(forward,WindDirection)*1.4f*WindStrength,ForceMode.Acceleration);
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
                _sun.transform.rotation=Quaternion.Euler(elevation,_sunRotation.eulerAngles.y+(hour-12)*15,0);
            }
            {
                var calendar=Controller.Career.Calendar;
                float hour=Controller.StartMenuOpen?17.2f:calendar.Hour;
                MoonPhase=Mathf.Repeat((float)((calendar.Day+calendar.Minute/1440)/FishingCalendar.DaysPerSeason)+.35f,1);
                SunDirection=shrine>=0 && _sun!=null?-_sun.transform.forward:SkyArc(hour,65);
                // The moon trails the sun by its phase: a full moon rises at dusk and transits at midnight.
                MoonDirection=SkyArc(hour-MoonPhase*24,58);
            }
            if(_sky!=null)
            {
                _sky.SetFloat("_Daylight",daylight);_sky.SetFloat("_CoastTime",_cloudTime);
                _sky.SetFloat("_Storm",StormAmount);_sky.SetFloat("_CloudCover",CloudAmount);
                _sky.SetVector("_SunDirection",SunDirection);_sky.SetVector("_MoonDirection",MoonDirection);_sky.SetFloat("_MoonPhase",MoonPhase);
                float sidereal=(float)(Controller.Career.Calendar.TotalMinutes%1440/1440*360);
                _sky.SetMatrix("_StarRotation",Matrix4x4.Rotate(Quaternion.AngleAxis(sidereal,Pole)));
                _sky.SetVector("_Meteor",_meteorAge>=0?new Vector4(_meteorHead.x,_meteorHead.y,_meteorHead.z,_meteorAge/.9f):new Vector4(0,1,0,-1));_sky.SetVector("_MeteorDir",_meteorDir);
                // This island uses _SkyTint, not the cubemap shader's _Tint. Its original
                // exposure=4 blows out the horizon, so only this runtime clone is capped.
                if(_skyTintProperty!=null)_sky.SetColor(_skyTintProperty,(_skyTintProperty=="_SkyTint"?new Color(.5f,.5f,.5f):_skyTint)*sky);
                if(_sky.HasProperty("_Exposure"))_sky.SetFloat("_Exposure",Mathf.Min(_skyExposure,1.4f)*Mathf.Lerp(.72f,1,intensity)*Mathf.Lerp(.20f,1,daylight));
            }
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;
            RenderSettings.fogColor=Color.Lerp(new Color(.018f,.031f,.064f),Color.Lerp(a.fog,b.fog,t),daylight);RenderSettings.fogStartDistance=35;
            RenderSettings.fogEndDistance=Mathf.Lerp(a.distance,b.distance,t);
            _eventFog=Mathf.MoveTowards(_eventFog,Mathf.Clamp01(EventFog),Time.deltaTime/4);
            if(_eventFog>0)
            {
                float f=Mathf.SmoothStep(0,1,_eventFog);
                RenderSettings.fogStartDistance=Mathf.Lerp(35,4,f);RenderSettings.fogEndDistance=Mathf.Lerp(RenderSettings.fogEndDistance,45,f);
                RenderSettings.fogColor=Color.Lerp(RenderSettings.fogColor,Color.Lerp(new Color(.05f,.07f,.09f),new Color(.70f,.76f,.78f),daylight),f*.8f);
            }
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=Color.Lerp(Color.Lerp(new Color(.028f,.043f,.075f),new Color(.065f,.085f,.13f),home),_ambient*Color.Lerp(Color.white,sky,.35f),daylight);
            RenderSettings.ambientEquatorColor=Color.Lerp(Color.Lerp(new Color(.015f,.023f,.040f),new Color(.035f,.05f,.075f),home),new Color(.32f,.38f,.39f),daylight);
            RenderSettings.ambientGroundColor=Color.Lerp(Color.Lerp(new Color(.008f,.012f,.019f),new Color(.018f,.026f,.035f),home),new Color(.15f,.18f,.18f),daylight);
            RenderSettings.ambientSkyColor*=Mathf.Lerp(1,.52f,StormAmount);
            RenderSettings.ambientEquatorColor*=Mathf.Lerp(1,.48f,StormAmount);RenderSettings.ambientGroundColor*=Mathf.Lerp(1,.58f,StormAmount);
            RenderSettings.ambientIntensity=_ambientIntensity*Mathf.Lerp(.82f,1,intensity);
            RenderSettings.reflectionIntensity=_reflection*Mathf.Lerp(.08f,1,daylight)*Mathf.Lerp(1,.34f,StormAmount);
            // Whole scene takes the sky's hue: coral sunset, pink dawn, grey-violet dusk (Little Prince lamplighter cycle).
            if(shrine<0)
            {
                float clear=1-StormAmount,y=SunDirection.y;
                float glow=Mathf.Clamp01(1-Mathf.Abs(y-.03f)/.24f)*clear,dusk=Mathf.Clamp01(1-Mathf.Abs(y+.16f)/.15f)*clear;
                bool dawn=(Controller.StartMenuOpen?17.2f:Controller.Career.Calendar.Hour)<12;
                var warm=Color.Lerp(Color.white,dawn?new Color(1.15f,.80f,1.0f):new Color(1.25f,.80f,.72f),glow*.7f);
                var tint=warm*Color.Lerp(Color.white,new Color(.95f,.80f,1.30f),dusk*.7f);
                RenderSettings.ambientSkyColor*=tint;RenderSettings.ambientEquatorColor*=tint;RenderSettings.ambientGroundColor*=tint;
                RenderSettings.fogColor=Color.Lerp(RenderSettings.fogColor,dawn?new Color(.80f,.50f,.62f):new Color(.86f,.49f,.30f),glow*.45f);
                RenderSettings.fogColor=Color.Lerp(RenderSettings.fogColor,new Color(.20f,.14f,.28f),dusk*.55f);
                if(_sun!=null)_sun.color*=Color.Lerp(Color.white,dawn?new Color(1,.78f,.86f):new Color(1,.72f,.62f),glow*.5f);
            }
            _rainAmount=StormAmount;
            if(StormAmount<.7f || Climate.Current!=BayWeather.Rain || ReducedEffects || Controller.StartMenuOpen)ResetStorm();
            ApplyFlash();
            UpdateCloudCards();
        }
        /// <summary>Celestial pole: 30 degrees up toward the side opposite the noon sun.</summary>
        private Vector3 Pole=>Quaternion.Euler(-30,_sunRotation.eulerAngles.y,0)*Vector3.forward;
        /// <summary>Direction toward a body on a daily arc: rises at 06, peaks at 12, sets at 18 (body-local hour).</summary>
        private Vector3 SkyArc(float hour,float peak)=>Quaternion.Euler(peak*Mathf.Sin((hour-6)/12*Mathf.PI),_sunRotation.eulerAngles.y+(hour-12)*15,0)*Vector3.back;
        /// <summary>Explicit indoor volumes for buildings whose roofs have no collider (cottage, tackle shop, lighthouse tower); realms disable rain separately.</summary>
        public bool Indoor(Vector3 p)
        {
            var home=Controller.Home;
            if(home!=null)
            {
                if(Mathf.Abs(p.x-home.Origin.x)<6.4f && Mathf.Abs(p.z-home.Origin.z)<5.4f && p.y>home.Origin.y-1 && p.y<home.Origin.y+4.5f)return true;
                var lens=home.LighthouseLens;if(new Vector2(p.x-lens.x,p.z-lens.z).sqrMagnitude<2.6f*2.6f && p.y<lens.y && p.y>lens.y-30)return true;
            }
            var shop=Controller.TackleShop;
            if(shop!=null && shop.Available){var q=shop.transform.InverseTransformPoint(p);if(Mathf.Abs(q.x)<6.2f && q.z>-5.4f && q.z<5.3f && q.y>-1 && q.y<4.5f)return true;}
            return Controller.Realms!=null && Controller.Realms.Inside;
        }
        private static bool IsRoof(Collider collider)
        {
            for(var t=collider.transform;t!=null;t=t.parent)if(t.name.IndexOf("Roof",System.StringComparison.OrdinalIgnoreCase)>=0 || t.name.IndexOf("Indoor",System.StringComparison.OrdinalIgnoreCase)>=0)return true;
            return false;
        }
        private void BuildCloudCards()
        {
            var go=new GameObject("V044 distant cumulus cards",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);
            _cardMaterial=new Material(Resources.Load<Shader>("FishingSkyCards")){name="Sky cumulus cards"};_owned.Add(_cardMaterial);
            _cardMaterial.SetTexture("_MainTex",Resources.Load<Texture2D>("FishingSkyCloudCards"));
            _cardMesh=new Mesh{name="Cumulus cards (12 quads)"};_cardMesh.MarkDynamic();_owned.Add(_cardMesh);
            var uv=new Vector2[CardMax*4];var triangles=new int[CardMax*6];
            for(int i=0;i<CardMax;i++)
            {
                // Atlas: 8 cells, 2 columns x 4 rows (image top row = uv v 1); cards past 8 mirror.
                int cell=i%8;float u0=cell%2*.5f,u1=u0+.5f,v1=1-cell/2*.25f,v0=v1-.25f;if(i>=8){var t=u0;u0=u1;u1=t;}
                uv[i*4]=new Vector2(u0,v0);uv[i*4+1]=new Vector2(u1,v0);uv[i*4+2]=new Vector2(u1,v1);uv[i*4+3]=new Vector2(u0,v1);
                int v=i*4,j=i*6;triangles[j]=v;triangles[j+1]=v+2;triangles[j+2]=v+1;triangles[j+3]=v;triangles[j+4]=v+3;triangles[j+5]=v+2;
            }
            _cardMesh.vertices=_cardVertices;_cardMesh.uv=uv;_cardMesh.uv2=_cardSun;_cardMesh.colors=_cardColors;_cardMesh.triangles=triangles;
            go.GetComponent<MeshFilter>().sharedMesh=_cardMesh;var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=_cardMaterial;
            r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
            CloudCards=r;
        }
        private void LateUpdate()=>UpdateCloudCards();
        private void UpdateCloudCards()
        {
            if(_cardMesh==null || Controller==null || Controller.viewCamera==null)return;
            // Vertices are world space: keep the card object at the world origin.
            CloudCards.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);CloudCards.transform.localScale=Vector3.one;
            var cam=Controller.viewCamera.transform.position;
            float radius=Mathf.Min(CardDistance,Controller.viewCamera.farClipPlane*.85f);
            float y=SunDirection.y,clear=(1-StormAmount)*Mathf.Lerp(1,.3f,Mathf.InverseLerp(.18f,.82f,CloudAmount));
            float glow=Mathf.Clamp01(1-Mathf.Abs(y-.03f)/.24f),day=Daylight,night=1-day;
            bool shrine=Controller.Realms!=null && Controller.Realms.Inside;
            var sunFlat=new Vector3(SunDirection.x,0,SunDirection.z).normalized;
            _cardMaterial.SetColor("_Lit",Color.Lerp(Color.Lerp(new Color(.10f,.11f,.16f),new Color(1,.95f,.84f),day),new Color(.99f,.66f,.55f),glow));
            _cardMaterial.SetColor("_Shade",Color.Lerp(Color.Lerp(new Color(.03f,.035f,.06f),new Color(.56f,.63f,.77f),day),new Color(.60f,.34f,.47f),glow));
            _cardMaterial.SetColor("_Rim",new Color(.94f,.73f,.45f));_cardMaterial.SetFloat("_Glow",glow);
            int count=shrine?0:CloudCardCount;
            for(int i=0;i<CardMax;i++)
            {
                float azimuth=i*(360f/CardMax)+i*17%29+_cloudTime*.12f*(1+i%3*.3f);
                float elevation=8+(i*37%28);
                var dir=Quaternion.Euler(-elevation,azimuth,0)*Vector3.forward;
                var centre=cam+dir*radius;
                var right=Vector3.Cross(Vector3.up,new Vector3(dir.x,0,dir.z)).normalized;
                float width=radius*(.55f+(i*13%7)*.05f),height=width*.5f;// atlas cells are 2:1
                var basePoint=centre-Vector3.up*height*.5f;
                _cardVertices[i*4]=basePoint-right*width*.5f;_cardVertices[i*4+1]=basePoint+right*width*.5f;
                _cardVertices[i*4+2]=_cardVertices[i*4+1]+Vector3.up*height;_cardVertices[i*4+3]=_cardVertices[i*4]+Vector3.up*height;
                float side=Vector3.Dot(sunFlat,right)*(i>=8?-1:1);
                // Low cards sit in haze and fade out; hidden at night by darkness, fully in rain or realms.
                float alpha=i<count?clear*Mathf.Lerp(.55f,1,day)*Mathf.SmoothStep(0,1,(elevation-6)/10):0;
                // Skybox thins its own clouds around each visible card so the big cloud reads clearly.
                _cardDirs[i]=new Vector4(dir.x,dir.y,dir.z,alpha>.05f?width*.5f/radius:0);
                for(int k=0;k<4;k++){_cardColors[i*4+k]=new Color(1,1,1,alpha);_cardSun[i*4+k]=new Vector2(side,k<2?0:1);}
            }
            Shader.SetGlobalFloat("_FishingSkyLowEnd",CloudCardCount<CardMax?1:0);
            if(_sky!=null)_sky.SetVectorArray("_CardDirs",_cardDirs);
            _cardMesh.vertices=_cardVertices;_cardMesh.uv2=_cardSun;_cardMesh.colors=_cardColors;_cardMesh.RecalculateBounds();
        }
        private void TickMeteor(float seconds)
        {
            if(_meteorAge>=0){_meteorAge+=seconds;_meteorHead=(_meteorHead+_meteorDir*seconds*.38f).normalized;if(_meteorAge>.9f)_meteorAge=-1;return;}
            bool clearNight=Daylight<.1f && StormAmount<.05f && CloudAmount<.5f && !ReducedEffects;
            if(!clearNight || seconds<=0)return;
            _meteorWait-=seconds;if(_meteorWait>0)return;
            _meteorWait=Random.Range(25f,70f);_meteorAge=0;
            float yaw=Random.Range(0,360f);
            _meteorHead=Quaternion.Euler(-Random.Range(35f,65f),yaw,0)*Vector3.forward;
            _meteorDir=Vector3.ProjectOnPlane(Quaternion.Euler(0,yaw+Random.Range(60f,120f),0)*Vector3.forward+Vector3.down*.6f,_meteorHead).normalized;
        }
        /// <summary>256px tileable value-noise fbm (R broad, G detail); replaces per-pixel noise octaves in the sky shader.</summary>
        private static Texture2D SkyNoise()
        {
            const int size=256;var pixels=new Color32[size*size];var rng=new System.Random(4404);
            int[] freq={4,8,16,32,8,16,32,64};var lattice=new float[8][];
            for(int o=0;o<8;o++){lattice[o]=new float[freq[o]*freq[o]];for(int i=0;i<lattice[o].Length;i++)lattice[o][i]=(float)rng.NextDouble();}
            float Value(int o,float x,float y)
            {
                int n=freq[o];var l=lattice[o];int x0=(int)x,y0=(int)y;float fx=x-x0,fy=y-y0;fx=fx*fx*(3-2*fx);fy=fy*fy*(3-2*fy);
                int x1=(x0+1)%n,y1=(y0+1)%n;x0%=n;y0%=n;
                return Mathf.Lerp(Mathf.Lerp(l[y0*n+x0],l[y0*n+x1],fx),Mathf.Lerp(l[y1*n+x0],l[y1*n+x1],fx),fy);
            }
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float r=0,g=0,amp=.5f;
                for(int o=0;o<4;o++,amp*=.5f){r+=amp*Value(o,x*freq[o]/(float)size,y*freq[o]/(float)size);g+=amp*Value(o+4,x*freq[o+4]/(float)size,y*freq[o+4]/(float)size);}
                pixels[y*size+x]=new Color32((byte)(Mathf.Clamp01(r/.9375f)*255),(byte)(Mathf.Clamp01(g/.9375f)*255),0,255);
            }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,true,true){name="Sky cloud noise (runtime)",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear};
            texture.SetPixels32(pixels);texture.Apply(true,true);return texture;
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
            var origin=Controller.ExplorerPosition;
            _rain.transform.position=origin;
            int count=RainQuadBudget;
            // Each drop column remembers the highest surface below 12m: drops under a roof hide, rain past the windows stays.
            if((origin-_rainRoofOrigin).sqrMagnitude>.25f)
            {
                _rainRoofOrigin=origin;
                for(int i=0;i<96;i++)
                {
                    float a=i*2.399963f,r=4.5f+(i%11)*.65f;
                    var column=origin+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);
                    if(Indoor(column+Vector3.up)){_rainRoof[i]=99;continue;}
                    _rainRoof[i]=Physics.Raycast(origin+new Vector3(Mathf.Cos(a)*r,12,Mathf.Sin(a)*r),Vector3.down,out var hit,12,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)?hit.point.y-origin.y:-1;
                }
            }
            for(int i=0;i<96;i++)
            {
                float angle=i*2.399963f;float radius=4.5f+(i%11)*.65f;
                float y=7-Mathf.Repeat(_cloudTime*4.8f+i*.79f,7);
                var p=new Vector3(Mathf.Cos(angle)*radius,y,Mathf.Sin(angle)*radius);
                var width=Vector3.Cross(Controller.viewCamera.transform.forward,Vector3.down).normalized*.011f;
                float length=i<count?.18f*_rainAmount:0;
                _rainVertices[i*4]=p-width;_rainVertices[i*4+1]=p+width;_rainVertices[i*4+2]=p+width+new Vector3(-.04f,-length,0);_rainVertices[i*4+3]=p-width+new Vector3(-.04f,-length,0);
                var color=new Color(.72f,.82f,.86f);for(int j=0;j<4;j++)_rainColors[i*4+j]=color;
                if(i>=count || y-length<_rainRoof[i])for(int j=0;j<4;j++)_rainVertices[i*4+j]=p;
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
