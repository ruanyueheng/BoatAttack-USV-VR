using UnityEngine;
using UnityEngine.Rendering;
namespace BoatAttack.Fishing
{
    /// <summary>A bounded shared emitter and a single star mesh; no per-spark lights or physics.</summary>
    public sealed class FishingEffects : MonoBehaviour
    {
        public FishingPrototype Controller;
        private ParticleSystem _sparks;
        private Transform _stars;
        private Mesh _starMesh;
        private Material _material,_starMaterial;
        private Material _sprayMaterial,_foamMaterial;
        private ParticleSystem _spray;
        private ParticleSystem _coastalMotes;
        private float _nextMote,_nextFootDust;
        private int _moteSeed;
        private readonly LineRenderer[] _ripples=new LineRenderer[8];
        private readonly float[] _rippleAge=new float[8],_ripplePower=new float[8];
        private int _ring;
        private float _nextSplash,_nextWake;
        private CatchPhase _surfacePhase;
        public int SplashCount { get; private set; }
        public int SprayBudget=>192;
        private void Start()
        {
            _material=new Material(Controller.PaletteShader){name="Coastal soft motes"};_material.SetFloat("_Unlit",1);
            var root=new GameObject("Shared interaction spark pool");root.transform.SetParent(transform,false);
            _sparks=root.AddComponent<ParticleSystem>();_sparks.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=_sparks.main;main.maxParticles=48;main.startLifetime=.55f;main.startSize=.025f;main.startSpeed=0;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=_sparks.emission;emission.enabled=false;var shape=_sparks.shape;shape.enabled=false;
            var renderer=root.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=_material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            var b=new FishingMeshBuilder();
            for(int i=0;i<64;i++)
            {
                float yaw=i*2.399963f,height=.25f+(i%17)*.06f;
                var point=new Vector3(Mathf.Cos(yaw),height,Mathf.Sin(yaw)).normalized*90;
                var right=Vector3.Cross(point.normalized,Vector3.up).normalized*(.07f+(i%3)*.03f);var up=Vector3.Cross(right.normalized,point.normalized)*right.magnitude;
                var color=i%4==0?new Color(1,.84f,.57f):new Color(.75f,.88f,1);
                b.Fin(point-up,point-right,point+up,color);b.Fin(point-up,point+up,point+right,color);
            }
            _starMesh=b.Finish("64 coastal stars / 256 triangles");
            var stars=new GameObject("Coastal night sky",typeof(MeshFilter),typeof(MeshRenderer));_stars=stars.transform;_stars.SetParent(transform,false);
            _starMaterial=new Material(_material){name="Night star palette"};stars.GetComponent<MeshFilter>().sharedMesh=_starMesh;
            var sr=stars.GetComponent<MeshRenderer>();sr.sharedMaterial=_starMaterial;sr.shadowCastingMode=ShadowCastingMode.Off;sr.receiveShadows=false;
            _sprayMaterial=new Material(Resources.Load<Shader>("FishingSpray")){name="Soft water droplets"};
            _foamMaterial=new Material(_sprayMaterial){name="Sea foam rings"};_foamMaterial.SetFloat("_Round",0);
            renderer.sharedMaterial=_sprayMaterial;
            var spray=new GameObject("V032 water spray pool");spray.transform.SetParent(transform,false);
            _spray=spray.AddComponent<ParticleSystem>();_spray.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var water=_spray.main;water.maxParticles=SprayBudget;water.startLifetime=.8f;water.gravityModifier=.65f;water.startSpeed=0;water.simulationSpace=ParticleSystemSimulationSpace.World;
            var emit=_spray.emission;emit.enabled=false;var source=_spray.shape;source.enabled=false;
            var fade=_spray.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(new Color(.83f,.96f,1),0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(.85f,0),new GradientAlphaKey(0,1)});fade.color=gradient;
            var size=_spray.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,.2f));
            var sprayRenderer=spray.GetComponent<ParticleSystemRenderer>();sprayRenderer.sharedMaterial=_sprayMaterial;sprayRenderer.shadowCastingMode=ShadowCastingMode.Off;sprayRenderer.receiveShadows=false;
            for(int i=0;i<_ripples.Length;i++)
            {
                _ripples[i]=FishingArt.Line("Expanding water ripple "+i,transform,_foamMaterial,.035f);_ripples[i].loop=true;_ripples[i].positionCount=40;
                _ripples[i].enabled=false;_rippleAge[i]=2;
            }
            var motes=new GameObject("Coastal pollen, fireflies and foot dust");motes.transform.SetParent(transform,false);
            _coastalMotes=motes.AddComponent<ParticleSystem>();_coastalMotes.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var m=_coastalMotes.main;m.maxParticles=48;m.startSpeed=0;m.simulationSpace=ParticleSystemSimulationSpace.World;
            var me=_coastalMotes.emission;me.enabled=false;var ms=_coastalMotes.shape;ms.enabled=false;
            var mf=_coastalMotes.colorOverLifetime;mf.enabled=true;var mg=new Gradient();
            mg.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.7f,.15f),new GradientAlphaKey(0,1)});mf.color=mg;
            var mr=motes.GetComponent<ParticleSystemRenderer>();mr.sharedMaterial=_sprayMaterial;mr.shadowCastingMode=ShadowCastingMode.Off;mr.receiveShadows=false;
        }
        public void Splash(Vector3 at,float strength=1)
        {
            if(_spray==null || (Controller.Paused && !Controller.StartMenuOpen))return;
            strength=Mathf.Clamp(strength,.25f,2);at.y=.12f;SplashCount++;
            int count=Controller.Weather.ReducedEffects?8:28;
            for(int i=0;i<count;i++)
            {
                float a=i*2.399963f;float speed=(.6f+(i%7)*.16f)*strength;
                _spray.Emit(new ParticleSystem.EmitParams{position=at,startColor=new Color(.82f,.96f,1,.9f),startSize=(.04f+(i%3)*.022f)*strength,startLifetime=.45f+(i%5)*.1f,
                    velocity=new Vector3(Mathf.Cos(a)*speed,(.9f+(i%4)*.55f)*strength,Mathf.Sin(a)*speed)},1);
            }
            _ring=(_ring+1)%_ripples.Length;_rippleAge[_ring]=0;_ripplePower[_ring]=strength;_ripples[_ring].transform.position=at;
        }
        public void FishSurface(Vector3 at,CatchPhase phase,float distance,FishBeat beat)
        {
            bool landing=phase==CatchPhase.Landing,leap=phase==CatchPhase.Fighting && beat==FishBeat.Leap;
            if((landing || leap || (phase==CatchPhase.Fighting && distance<5)) && (phase!=_surfacePhase || Time.time>=_nextSplash))
            {Splash(at,leap?1.4f:landing?1.05f:.7f);_nextSplash=Time.time+(landing?.45f:.7f);}
            _surfacePhase=phase;
        }
        public void Burst(Vector3 at,Color color,bool splash=false)
        {
            if(_sparks==null || Controller.Paused)return;
            int count=Controller.Weather.ReducedEffects?4:12;
            for(int i=0;i<count;i++)
            {
                float angle=i*2.399963f;
                _sparks.Emit(new ParticleSystem.EmitParams{position=at,startColor=color,startSize=splash?.07f:.025f,
                    velocity=new Vector3(Mathf.Cos(angle)*.25f,.15f+(i%4)*.08f,Mathf.Sin(angle)*.25f)*(splash?4f:1f)},1);
            }
        }
        private void LateUpdate()
        {
            if(Controller==null)return;
            if(_coastalMotes!=null && !Controller.Paused && Controller.Ashore && !Controller.StoryBlocksGameplay && !Controller.Shore.Swimming)
            {
                var feet=Controller.Shore.Position;bool reduced=Controller.Weather.ReducedEffects;
                if(!Controller.Home.Inside && Controller.Shore.Grounded && Controller.Shore.WalkSpeed>1 && Time.time>=_nextFootDust)
                {
                    _nextFootDust=Time.time+(reduced?.55f:.28f);
                    _coastalMotes.Emit(new ParticleSystem.EmitParams{position=feet+Vector3.up*.045f,startSize=.09f,startLifetime=.65f,startColor=new Color(.83f,.75f,.57f,.35f),velocity=Vector3.up*.12f},reduced?1:3);
                }
                bool garden=Controller.Home!=null && !Controller.Home.Inside && (feet-Controller.Home.Origin).sqrMagnitude<38*38;
                bool grove=Controller.Realms.Inside && Controller.Realms.Island==1;
                if((garden || grove) && Time.time>=_nextMote && Controller.Weather.Climate.Current!=BayWeather.Rain)
                {
                    _nextMote=Time.time+(reduced?1.2f:.45f);float a=_moteSeed++*2.399963f;
                    bool night=Controller.Weather.Daylight<.25f;
                    var point=feet+new Vector3(Mathf.Cos(a)*(2+_moteSeed%4),.7f+(_moteSeed%9)*.25f,Mathf.Sin(a)*(2+_moteSeed%4));
                    _coastalMotes.Emit(new ParticleSystem.EmitParams{position=point,startSize=night?.027f:.018f,startLifetime=4,startColor=night?new Color(1,.83f,.37f,.7f):new Color(.95f,.89f,.63f,.45f),velocity=new Vector3(.045f,.035f,Mathf.Sin(a)*.035f)},1);
                }
            }
            if(_spray!=null)
            {
                var main=_spray.main;main.useUnscaledTime=Controller.StartMenuOpen;
                float dt=Controller.StartMenuOpen?Time.unscaledDeltaTime:Time.deltaTime;
                for(int i=0;i<_ripples.Length;i++)
                {
                    _rippleAge[i]+=dt;float u=_rippleAge[i]/1.25f;var line=_ripples[i];line.enabled=u<1;
                    if(u>=1)continue;float radius=(.18f+u*1.8f)*_ripplePower[i];
                    var tint=new Color(.86f,.98f,1,(1-u)*.65f);line.startColor=line.endColor=tint;line.widthMultiplier=.035f*(1-u*.7f);
                    for(int j=0;j<40;j++){float a=j*Mathf.PI*2/40;line.SetPosition(j,line.transform.position+new Vector3(Mathf.Cos(a)*radius,.01f,Mathf.Sin(a)*radius));}
                }
                bool title=Controller.StartMenuOpen && Controller.TitleVoyage.Sailing;
                if((title || (!Controller.Paused && !Controller.Ashore && Controller.Speed>3)) && Time.unscaledTime>=_nextWake)
                {var boat=title?Controller.TitleVoyage.Boat:Controller.Player.transform;Splash(boat.position-boat.forward*2,.35f);_nextWake=Time.unscaledTime+.24f;}
            }
            if(_stars==null || Controller.Player==null)return;
            float light=1-Controller.Weather.Daylight;
            bool clear=Controller.Weather.Climate.Current!=BayWeather.Rain && Controller.Weather.Climate.Current!=BayWeather.Mist;
            _stars.gameObject.SetActive(clear && light>.08f);_stars.position=Controller.viewCamera.transform.position;
            _starMaterial.SetColor("_BaseColor",Color.white*light);
        }
        private void OnDestroy(){if(_material!=null)Destroy(_material);if(_starMaterial!=null)Destroy(_starMaterial);if(_starMesh!=null)Destroy(_starMesh);if(_sprayMaterial!=null)Destroy(_sprayMaterial);if(_foamMaterial!=null)Destroy(_foamMaterial);}
    }
}
