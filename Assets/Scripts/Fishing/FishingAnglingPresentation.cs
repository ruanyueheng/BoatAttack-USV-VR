using UnityEngine;
using UnityEngine.UI;
namespace BoatAttack.Fishing
{
    /// <summary>Original procedural fishing feedback. Never owns rewards, the camera, or tracked hand poses.</summary>
    public sealed class FishingAnglingPresentation : MonoBehaviour
    {
        private FishingPrototype _c;
        private Transform _tip,_float;
        private LineRenderer _line;
        private LineRenderer _castGuide;
        private Canvas _marker;
        private Text _label;
        private readonly LineRenderer[] _ripples=new LineRenderer[3];
        private AudioSource _reelAudio;
        private AudioClip _clicks;
        private Material _lineMaterial;
        private CatchPhase _lastPhase;
        private float _clock,_nextRipple,_strikeAt=-100;
        private Vector3 _castForward=Vector3.right,_castRight=Vector3.forward;
        private float _range=9;
        public Vector3 WaterTarget { get; private set; }
        public Vector3 CastRight=>_castRight;
        public float VisualTime=>_clock;
        public bool MarkerVisible=>_marker!=null && _marker.gameObject.activeSelf;
        public bool ReelSoundPlaying=>_reelAudio!=null && _reelAudio.isPlaying;
        public void Build(FishingPrototype c,Transform tip,LineRenderer line,Transform bobber)
        {
            _c=c;_tip=tip;_line=line;_float=bobber;
            _lineMaterial=new Material(Resources.Load<Shader>("FishingAnglingLine")){name="Original translucent angling cues"};
            line.sharedMaterial=_lineMaterial;
            // A slender high-contrast float, rather than a giant floating sphere.
            bobber.GetComponent<Renderer>().enabled=false;bobber.localScale=Vector3.one;
            FishingArt.Shape("Float lower cork",PrimitiveType.Sphere,bobber,new Vector3(0,.015f,0),new Vector3(.095f,.14f,.095f),c.cream);
            FishingArt.Shape("Float bright mast",PrimitiveType.Cylinder,bobber,new Vector3(0,.12f,0),new Vector3(.035f,.11f,.035f),c.orange);
            FishingArt.Shape("Float sight tip",PrimitiveType.Sphere,bobber,new Vector3(0,.24f,0),Vector3.one*.055f,c.cream);
            for(int i=0;i<_ripples.Length;i++)
            {var ring=FishingArt.Line("Angling water ripple "+i,transform,_lineMaterial,.013f);ring.positionCount=41;ring.enabled=false;_ripples[i]=ring;}
            _castGuide=FishingArt.Line("Cast landing preview",transform,_lineMaterial,.008f);_castGuide.positionCount=21;_castGuide.enabled=false;
            _castGuide.startColor=_castGuide.endColor=new Color(.75f,.95f,.85f,.5f);
            var root=new GameObject("Fishing water distance and direction",typeof(RectTransform),typeof(Canvas));root.layer=5;root.transform.SetParent(transform,false);
            _marker=root.GetComponent<Canvas>();_marker.renderMode=RenderMode.WorldSpace;_marker.worldCamera=c.viewCamera;
            var rect=root.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(390,160);rect.localScale=Vector3.one*.0025f;
            var text=new GameObject("Fishing hint",typeof(RectTransform),typeof(Text));text.layer=5;text.transform.SetParent(root.transform,false);
            var rt=text.GetComponent<RectTransform>();rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;
            _label=text.GetComponent<Text>();_label.font=FishingUIFont.Font;_label.fontSize=26;_label.alignment=TextAnchor.MiddleCenter;_label.color=Color.white;_label.raycastTarget=false;
            var shadow=text.AddComponent<Shadow>();shadow.effectColor=new Color(0,.04f,.07f,.95f);shadow.effectDistance=new Vector2(1.4f,-1.4f);
            root.SetActive(false);
            // Synthesized reel teeth and drag clicks; no extracted game audio.
            var samples=new float[4410];
            for(int i=0;i<samples.Length;i++){float p=(i%441)/441f;samples[i]=Mathf.Sin(i*1.71f)*Mathf.Exp(-p*50)*.23f;}
            _clicks=AudioClip.Create("Original angling reel teeth",samples.Length,1,22050,false);_clicks.SetData(samples,0);
            _reelAudio=gameObject.AddComponent<AudioSource>();_reelAudio.clip=_clicks;_reelAudio.loop=true;_reelAudio.playOnAwake=false;_reelAudio.spatialBlend=0;
        }
        public void BeginCast(float power)
        {
            var aim=Vector3.ProjectOnPlane(_c.viewCamera.transform.forward,Vector3.up);
            if(aim.sqrMagnitude<.1f)aim=_c.Player.transform.right;
            _castForward=aim.normalized;_castRight=Vector3.Cross(Vector3.up,_castForward);
            _range=Mathf.Max(5,_c.Session.MaxDistance*Mathf.Lerp(.7f,1,Mathf.Clamp01(power)));
            _nextRipple=0;_lastPhase=CatchPhase.Idle;
        }
        public Vector3 Target(CatchPhase phase)
        {
            var s=_c.Session;
            float distance=phase==CatchPhase.Fighting?Mathf.Lerp(1.7f,_range,s.Distance/Mathf.Max(.01f,s.MaxDistance)):_range;
            float side=phase==CatchPhase.Fighting?s.FishDirection*Mathf.Clamp01(s.FightTime/.6f)*Mathf.Lerp(.35f,2.3f,s.Distance/Mathf.Max(.01f,s.MaxDistance)):0;
            var point=_c.Player.transform.position+_castForward*distance+_castRight*side;
            point.y=.055f;
            if(phase==CatchPhase.Waiting)point.y+=Mathf.Sin(_clock*4)*.025f-s.BitePreview*(.08f+.045f*Mathf.Sin(_clock*17));
            if(phase==CatchPhase.Bite)point.y=-.12f+Mathf.Sin(_clock*20)*.035f;
            if(phase==CatchPhase.Fighting)point.y=-.06f+Mathf.Sin(_clock*7)*.07f;
            WaterTarget=point;return point;
        }
        public void Tick(float dt,bool reeling,bool available)
        {
            var s=_c.Session;bool live=available && _c.Anchored && !_c.HasPendingCatch;
            if(live && dt>0 && !float.IsInfinity(dt))_clock+=Mathf.Min(dt,.1f);
            bool fighting=s.Phase==CatchPhase.Fighting;
            if(fighting && _lastPhase!=s.Phase)_strikeAt=_clock;
            bool wet=live && !_c.CastMotion.Casting && (s.Phase==CatchPhase.Waiting || s.Phase==CatchPhase.Bite || fighting || s.Phase==CatchPhase.Landing);
            bool preview=live && _c.CastMotion.Preparing && !UnityEngine.XR.XRSettings.isDeviceActive;
            _castGuide.enabled=preview;
            _marker.gameObject.SetActive(wet || preview);
            if(preview)
            {
                var aim=Vector3.ProjectOnPlane(_c.viewCamera.transform.forward,Vector3.up);
                if(aim.sqrMagnitude<.1f)aim=_c.Player.transform.right;
                float power=.35f+_c.CastMotion.Charge*.65f;
                // Species is not rolled until release; use this fishing ground's nominal line range.
                float nominal=FishingSpecies.Of(FishingGrounds.Species(_c.Spot)).LineMeters;
                var goal=_c.Player.transform.position+aim.normalized*Mathf.Max(5,nominal*Mathf.Lerp(.7f,1,power));goal.y=.06f;
                for(int k=0;k<21;k++){float u=k/20f;_castGuide.SetPosition(k,Vector3.Lerp(_tip.position,goal,u)+Vector3.up*Mathf.Sin(u*Mathf.PI)*(1+power));}
                _marker.transform.position=goal+Vector3.up*.65f;_marker.transform.rotation=Quaternion.LookRotation(goal-_c.viewCamera.transform.position);
                SizeMarker();
                _label.text="预计落点\n松开左键抛竿";
            }
            if(wet)
            {
                Vector3 target=s.Phase==CatchPhase.Landing?_c.LandingTarget:WaterTarget;
                _marker.transform.position=target+Vector3.up*.65f;
                _marker.transform.rotation=Quaternion.LookRotation(target-_c.viewCamera.transform.position);
                SizeMarker();
                _label.text=s.Phase==CatchPhase.Bite?"咬钩！提竿":s.Phase==CatchPhase.Landing?"鱼已靠近 · 抄网":
                    (Vector3.Distance(_c.Player.transform.position,target)).ToString("0.0")+" m"+(fighting?"\n"+(s.Beat==FishBeat.Leap?"跃水 · 松线":s.FishDirection<-.2f?"← 鱼向左游":s.FishDirection>.2f?"鱼向右游 →":"鱼在转向"):s.BitePreview>0?"\n轻啄 · 等待下沉":"");
                if(fighting && _clock-_strikeAt<1.1f && s.PerfectStrike)_label.text="漂亮提竿！\n"+_label.text;
                if(fighting && UnityEngine.XR.XRSettings.isDeviceActive)_label.text+="\n"+(s.Beat==FishBeat.Leap?"松开收线握把":s.FishDirection<0?"持竿手向右控竿":"持竿手向左控竿");
                if(_clock>=_nextRipple)
                {
                    _nextRipple=_clock+(s.Phase==CatchPhase.Bite?.22f:fighting?.35f:1.1f);
                    if(s.Phase!=CatchPhase.Waiting || s.BitePreview>0 || _lastPhase!=s.Phase)_c.Effects?.Burst(target,new Color(.7f,.88f,.94f));
                }
            }
            for(int i=0;i<_ripples.Length;i++)
            {
                var ring=_ripples[i];ring.enabled=wet;
                if(!wet)continue;
                float phase=Mathf.Repeat(_clock*(fighting?1.6f:.7f)+i/3f,1),radius=Mathf.Lerp(.10f,fighting?.9f:.5f,phase);
                var centre=s.Phase==CatchPhase.Landing?_c.LandingTarget:WaterTarget;centre.y=.065f;
                var color=new Color(.76f,.91f,.95f,(1-phase)*.8f);ring.startColor=ring.endColor=color;
                for(int k=0;k<41;k++){float a=k/40f*Mathf.PI*2;ring.SetPosition(k,centre+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));}
            }
            _lastPhase=s.Phase;
            _reelAudio.mute=_c.QuietMode;
            bool sound=live && fighting && (reeling || s.Beat==FishBeat.Surge || s.Beat==FishBeat.Leap);
            if(sound){_reelAudio.volume=reeling?.1f:.075f;_reelAudio.pitch=reeling?1.25f:1.7f;if(!_reelAudio.isPlaying)_reelAudio.Play();}
            else if(_reelAudio.isPlaying)_reelAudio.Stop();
            _line.startWidth=_line.endWidth=fighting?.012f:.009f;
            _line.startColor=_line.endColor=s.Tension>.86f?new Color(1,.40f,.22f):new Color(.91f,.91f,.70f);
        }
        public void PresentRod(float dt)
        {
            var dynamics=_tip.parent.GetComponent<FishingRodDynamics>();
            bool fighting=_c.Anchored && (_c.Session.Phase==CatchPhase.Fighting || _c.Session.Phase==CatchPhase.Landing);
            dynamics?.SetLoad(_c.Session.Tension,_c.Session.FishDirection,fighting,dt);
            if(_castGuide.enabled)
            {
                var goal=_castGuide.GetPosition(20);float power=.35f+_c.CastMotion.Charge*.65f;
                for(int k=0;k<21;k++){float u=k/20f;_castGuide.SetPosition(k,Vector3.Lerp(_tip.position,goal,u)+Vector3.up*Mathf.Sin(u*Mathf.PI)*(1+power));}
            }
        }
        private void SizeMarker()
        {
            // Keep a modest angular reading size: legible at cast range, not huge beside the net.
            float distance=Vector3.Distance(_marker.transform.position,_c.viewCamera.transform.position);
            _marker.transform.localScale=Vector3.one*Mathf.Clamp(distance*(UnityEngine.XR.XRSettings.isDeviceActive?.0011f:.0008f),.0008f,.025f);
        }
        private void OnDestroy(){if(_reelAudio!=null)_reelAudio.Stop();if(_clicks!=null)Destroy(_clicks);if(_lineMaterial!=null)Destroy(_lineMaterial);}
    }
}
