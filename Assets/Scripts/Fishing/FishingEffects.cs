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
        private Material _sprayMaterial,_foamMaterial,_sparkMaterial;
        private ParticleSystem _spray;
        private ParticleSystem _coastalMotes,_fireflies,_trail,_wet;
        public const int WetBudget=24;
        private float _nextTrail;
        /// <summary>Gold stardust drifting from the explorer toward the navigation target; off in reduced effects.</summary>
        public bool TrailEnabled=true;
        public const int TrailBudget=30;
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
            var main=_sparks.main;main.maxParticles=64;main.startLifetime=.9f;main.gravityModifier=-.05f;main.startSize=.025f;main.startSpeed=0;main.simulationSpace=ParticleSystemSimulationSpace.World;
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
            _sparkMaterial=new Material(_sprayMaterial){name="Gold sparkle stars"};_sparkMaterial.SetFloat("_Round",2);renderer.sharedMaterial=_sparkMaterial;
            var sparkFade=_sparks.colorOverLifetime;sparkFade.enabled=true;var sg=new Gradient();sg.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(.9f,.4f),new GradientAlphaKey(0,1)});sparkFade.color=sg;
            var sparkSize=_sparks.sizeOverLifetime;sparkSize.enabled=true;sparkSize.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.3f),new Keyframe(.15f,1),new Keyframe(1,0)));
            var sparkDrag=_sparks.limitVelocityOverLifetime;sparkDrag.enabled=true;sparkDrag.drag=2.5f;
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
            var flies=new GameObject("V044 night fireflies");flies.transform.SetParent(transform,false);
            _fireflies=flies.AddComponent<ParticleSystem>();_fireflies.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var fm=_fireflies.main;fm.maxParticles=24;fm.startSpeed=0;fm.simulationSpace=ParticleSystemSimulationSpace.World;
            var fe=_fireflies.emission;fe.enabled=false;var fs=_fireflies.shape;fs.enabled=false;
            var wander=_fireflies.noise;wander.enabled=true;wander.strength=.25f;wander.frequency=.35f;wander.scrollSpeed=.2f;wander.quality=ParticleSystemNoiseQuality.Low;
            // Slow breathing glow: three soft pulses over each firefly's life.
            var fc=_fireflies.colorOverLifetime;fc.enabled=true;var fg=new Gradient();
            fg.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.15f),new GradientAlphaKey(.25f,.32f),new GradientAlphaKey(1,.5f),new GradientAlphaKey(.2f,.68f),new GradientAlphaKey(.9f,.84f),new GradientAlphaKey(0,1)});fc.color=fg;
            var trail=new GameObject("V044 target stardust trail");trail.transform.SetParent(transform,false);
            _trail=trail.AddComponent<ParticleSystem>();_trail.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var tm=_trail.main;tm.maxParticles=TrailBudget;tm.startSpeed=0;tm.simulationSpace=ParticleSystemSimulationSpace.World;
            var te=_trail.emission;te.enabled=false;var ts=_trail.shape;ts.enabled=false;
            var tc=_trail.colorOverLifetime;tc.enabled=true;var tg=new Gradient();tg.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.2f),new GradientAlphaKey(0,1)});tc.color=tg;
            trail.GetComponent<ParticleSystemRenderer>().sharedMaterial=_sparkMaterial;trail.GetComponent<ParticleSystemRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            // Dark soil wet spots: ground-flat soft discs that dry over a minute.
            var wet=new GameObject("V044 soil wet spots");wet.transform.SetParent(transform,false);
            _wet=wet.AddComponent<ParticleSystem>();_wet.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var wm=_wet.main;wm.maxParticles=WetBudget;wm.startSpeed=0;wm.simulationSpace=ParticleSystemSimulationSpace.World;wm.startLifetime=60;
            var we=_wet.emission;we.enabled=false;var ws=_wet.shape;ws.enabled=false;
            var wc=_wet.colorOverLifetime;wc.enabled=true;var wg=new Gradient();wg.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(.7f,.4f),new GradientAlphaKey(0,1)});wc.color=wg;
            var wr=wet.GetComponent<ParticleSystemRenderer>();wr.sharedMaterial=_sprayMaterial;wr.renderMode=ParticleSystemRenderMode.HorizontalBillboard;wr.shadowCastingMode=ShadowCastingMode.Off;wr.receiveShadows=false;
            var fr=flies.GetComponent<ParticleSystemRenderer>();fr.sharedMaterial=_sparkMaterial;fr.shadowCastingMode=ShadowCastingMode.Off;fr.receiveShadows=false;
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
            Ripple(at,strength);
        }
        /// <summary>A dark damp patch on soil that fades over ~60 s, plus a couple of tiny droplets.</summary>
        public void Wet(Vector3 at)
        {
            if(_wet==null)return;
            float h=Mathf.Repeat(at.x*7.3f+at.z*3.1f,1);
            _wet.Emit(new ParticleSystem.EmitParams{position=at+Vector3.up*.012f,startSize=.16f+h*.12f,startColor=new Color(.20f,.13f,.07f,.55f),rotation=h*360},1);
            if(!Controller.Weather.ReducedEffects)_spray?.Emit(new ParticleSystem.EmitParams{position=at+Vector3.up*.02f,startSize=.02f,startLifetime=.3f,startColor=new Color(.8f,.93f,1,.8f),velocity=new Vector3(h-.5f,.6f,.5f-h)*.5f},2);
        }
        /// <summary>One expanding foam ring at the given height (well water, puddles); shares the 8-ring pool.</summary>
        public void Ripple(Vector3 at,float strength)
        {
            if(_spray==null)return;
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
            int count=Controller.Weather.ReducedEffects?4:16;
            for(int i=0;i<count;i++)
            {
                float angle=i*2.399963f;
                // Alternate a few large twinkles with fine glitter, lifted toward warm white.
                _sparks.Emit(new ParticleSystem.EmitParams{position=at,startColor=Color.Lerp(color,new Color(1,.95f,.8f),i%3==0?.5f:.15f),startSize=(splash?.09f:.07f)*(i%4==0?1.8f:1),startLifetime=.6f+(i%5)*.12f,
                    velocity=new Vector3(Mathf.Cos(angle)*.6f,.5f+(i%4)*.25f,Mathf.Sin(angle)*.6f)*(splash?2.5f:1f)},1);
            }
        }
        public void Firefly(Vector3 at)
        {
            float a=at.x*1.7f+at.z;
            _fireflies?.Emit(new ParticleSystem.EmitParams{position=at,startSize=.06f,startLifetime=6,startColor=new Color(.85f,1,.45f,.9f),velocity=new Vector3(.03f,.02f,Mathf.Sin(a)*.03f)},1);
        }
        /// <summary>Upgrade / milestone flourish: a gold spiral of sparkles rising around the point, shared with the spark pool.</summary>
        public void Celebrate(Vector3 at)
        {
            if(_sparks==null || Controller.Paused)return;
            int count=Controller.Weather.ReducedEffects?8:28;
            for(int i=0;i<count;i++)
            {
                float a=i*Mathf.PI*2/count*2,ring=.35f+(i%3)*.12f;
                _sparks.Emit(new ParticleSystem.EmitParams{position=at+new Vector3(Mathf.Cos(a)*ring,i*.02f,Mathf.Sin(a)*ring),startColor=i%4==0?new Color(1,.97f,.85f):new Color(1,.8f,.4f),
                    startSize=i%5==0?.13f:.07f,startLifetime=1+(i%4)*.15f,velocity=new Vector3(-Mathf.Sin(a)*.5f,.9f+(i%5)*.15f,Mathf.Cos(a)*.5f)},1);
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
                    if(night)Firefly(point);
                    else _coastalMotes.Emit(new ParticleSystem.EmitParams{position=point,startSize=.018f,startLifetime=4,startColor=new Color(.95f,.89f,.63f,.45f),velocity=new Vector3(.045f,.035f,Mathf.Sin(a)*.035f)},1);
                }
            }
            if(_trail!=null && TrailEnabled && !Controller.Weather.ReducedEffects && !Controller.Paused && !Controller.StoryBlocksGameplay && Time.time>=_nextTrail)
            {
                _nextTrail=Time.time+1.4f;
                var from=Controller.ExplorerPosition+Vector3.up*1.1f;var to=Controller.NavigationPosition-from;
                if(to.sqrMagnitude>36)
                {
                    // Five motes per puff (≈11 alive at once) glide ~3.5 m toward the target, #E8B75A amber.
                    var dir=to.normalized;
                    for(int i=0;i<5;i++)
                        _trail.Emit(new ParticleSystem.EmitParams{position=from+dir*(.6f+i*.18f)+new Vector3(Mathf.Sin(i*2.4f),Mathf.Cos(i*1.7f),Mathf.Cos(i*2.4f))*.08f,
                            velocity=dir*1.6f+Vector3.up*.08f,startSize=i==0?.09f:.05f,startLifetime=2.2f,startColor=new Color(.91f,.72f,.35f,.9f)},1);
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
        private void OnDestroy(){if(_material!=null)Destroy(_material);if(_starMaterial!=null)Destroy(_starMaterial);if(_starMesh!=null)Destroy(_starMesh);if(_sprayMaterial!=null)Destroy(_sprayMaterial);if(_foamMaterial!=null)Destroy(_foamMaterial);if(_sparkMaterial!=null)Destroy(_sparkMaterial);}
    }
}
