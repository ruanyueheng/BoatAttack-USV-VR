using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace BoatAttack.Fishing
{
    /// <summary>
    /// v044 resident behaviour: role work loops with idle fidgets, head/neck-only look-at inside a
    /// clamped cone, body turns only while talking (smooth, delayed), then returns to the home facing and resumes work.
    /// Occasional voiced barks with a world-space bubble. Far or unseen residents are not evaluated.
    /// </summary>
    public sealed class FishingResidentBrain : MonoBehaviour
    {
        public const float LookRange=5.5f,LookCone=110,MaxYaw=70,MaxPitchUp=20,MaxPitchDown=25,BarkRange=9;
        private struct Act{public string Clip;public float Min,Max;public Act(string c,float min,float max){Clip=c;Min=min;Max=max;}}
        private static AnimationClip[] _library;
        private static float _lastBarkAt=-100;
        private FishingPrototype _c;
        private int _id;
        private Animator _animator;
        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private int _active;
        private float _fade=1,_fadeTime=.35f,_until,_nextBark,_engagedAt=-1,_releasedAt=-100,_lookWeight,_lookDelay,_yaw,_pitch,_yawVel,_pitchVel,_turnVel;
        private Act[] _work,_fidget;
        private int _workIndex;
        private Quaternion _home;
        private Transform _head,_neck;
        private Renderer[] _renderers;
        private AudioSource _voice;
        private CanvasGroup _bubble;
        private Text _bubbleText;
        private float _bubbleUntil;
        private bool _evaluated,_wasEngaged;
        public string Current { get; private set; } = "";
        public bool Engaged=>_c!=null && _c.Residents!=null && _c.Residents.ActiveId==_id;
        /// <summary>Applied head yaw relative to the body, degrees (validation).</summary>
        public float HeadYaw=>_yaw*_lookWeight;
        public float LookWeight=>_lookWeight;
        public Quaternion Home=>_home;
        public double ClipTime=>_mixer.IsValid() && _mixer.GetInput(_active).IsValid()?_mixer.GetInput(_active).GetTime():-1;
        public AudioSource Voice=>_voice;
        public bool BubbleVisible=>_bubble!=null && _bubble.alpha>.01f;

        public void Build(FishingPrototype c,int id,AudioSource voice)
        {
            _c=c;_id=id;_voice=voice;_home=transform.rotation;
            _animator=GetComponentInChildren<Animator>();
            if(_animator==null || !_animator.isHuman)throw new System.InvalidOperationException("Resident "+id+" needs a humanoid Animator");
            _animator.applyRootMotion=false;_animator.runtimeAnimatorController=null;
            _animator.cullingMode=AnimatorCullingMode.AlwaysAnimate; // culled below by skipping Evaluate; CullCompletely froze clip time near the player
            _head=_animator.GetBoneTransform(HumanBodyBones.Head);_neck=_animator.GetBoneTransform(HumanBodyBones.Neck);
            _renderers=GetComponentsInChildren<Renderer>();
            if(_library==null)_library=Resources.LoadAll<AnimationClip>("FishingV044/Residents/ResidentClips");
            _graph=PlayableGraph.Create("Resident "+id);_graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _mixer=AnimationMixerPlayable.Create(_graph,2);
            AnimationPlayableOutput.Create(_graph,"Resident",_animator).SetSourcePlayable(_mixer);
            // Each resident's role work, then a pool of short idle variants played between work bouts.
            _work=id==0?new[]{new Act("Fix",12,18),new Act("Interact",0,0),new Act("Idle",4,7)}
                :id==1?new[]{new Act("Water",9,14),new Act("Plant",0,0),new Act("Harvest",0,0),new Act("Idle",4,6)}
                :id==2?new[]{new Act("Fix",10,14),new Act("PickUp",0,0),new Act("Chest",0,0),new Act("Idle",3,5)}
                :id==3?new[]{new Act("Lantern",10,14),new Act("Idle",4,6),new Act("FoldArms",6,9)}
                :id==6?new[]{new Act("Idle",6,10),new Act("Talk",3,5),new Act("FoldArms",5,8)}
                :new[]{new Act("FoldArms",10,14),new Act("Idle",5,8)};
            _homeWork=_work;
            _fidget=new[]{new Act("Idle",3,5),new Act("FoldArms",4,7),new Act("Drink",0,0),new Act("Nod",0,0)};
            _workIndex=Mathf.Abs(id*7)%_work.Length;
            _nextBark=Time.time+Random.Range(8f,20f);
            BuildBubble();
            _graph.Play(); // manual Evaluate(dt) only advances clip time on a playing graph
            Play("Idle",0,9999); // orientation is measured on a neutral pose, then work starts
            _graph.Evaluate(0);
        }
        private Act[] _homeWork;
        private Vector3 _pendingFeet;private Quaternion _pendingFacing;private Act[] _pendingWork;private bool _pending;
        public string Station { get; private set; } = "home";
        public Vector3 ScheduledFeet => _pending ? _pendingFeet : transform.position;
        /// <summary>
        /// Schedule move. Applied only while the player cannot see it happen (off-screen and >12 m away, or instantly when
        /// <paramref name="now"/>), so residents never slide or path through props; spots are pre-validated by FishingResidents.
        /// </summary>
        public void MoveTo(string station,Vector3 feet,Quaternion facing,string[] work,bool now=false)
        {
            if(station==Station && !_pending)return;
            Station=station;_pendingFeet=feet;_pendingFacing=facing;_pending=true;
            _pendingWork=work==null?_homeWork:System.Array.ConvertAll(work,w=>new Act(w,8,14));
            if(now)ApplyMove();
        }
        private bool _walking;
        public bool Walking=>_walking;
        // Visible moves walk a straight, obstacle-free, grounded line; otherwise they wait for the off-screen teleport.
        private bool CanWalk()
        {
            var to=_pendingFeet-transform.position;to.y=0;float len=to.magnitude;if(len<.3f || len>25)return len<.3f;
            if(Physics.SphereCast(transform.position+Vector3.up*.9f,.3f,to/len,out var hit,len,~(1<<4),QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(transform))return false;
            for(float d=.5f;d<len;d+=.5f){var p=transform.position+to/len*d;if(!_c.Shore.Ground(p+Vector3.up*.5f,out var g) || Mathf.Abs(g.y-p.y)>.45f)return false;}
            Play("Walk",.3f,9999);return true;
        }
        private void WalkStep(float dt)
        {
            var to=_pendingFeet-transform.position;to.y=0;
            if(to.magnitude<.08f){_walking=false;ApplyMove();return;}
            transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(to),240*dt);
            var p=transform.position+to.normalized*Mathf.Min(to.magnitude,1.1f*dt);
            if(_c.Shore.Ground(p+Vector3.up*.5f,out var g))p.y=g.y;
            transform.position=p;
        }
        private void ApplyMove()
        {
            _pending=false;_walking=false;transform.SetPositionAndRotation(_pendingFeet,_pendingFacing);_home=_pendingFacing;
            _work=_pendingWork;_workIndex=0;Play(_work[0],0);
        }
        /// <summary>Face another resident for a chat (only at shared stations).</summary>
        public void FacePartner(Vector3 at){var d=Flat(at-transform.position,transform.forward);_home=Quaternion.LookRotation(d);}
        private void OnDestroy(){if(_graph.IsValid())_graph.Destroy();}
        private AnimationClip Clip(string name)
        {
            foreach(var clip in _library)if(clip.name==name)return clip;
            // A missing optional work clip must not stop the whole world from starting; fall back to Idle.
            foreach(var clip in _library)if(clip.name=="Idle"){Debug.LogWarning("[NPC] Missing resident clip "+name+", using Idle");return clip;}
            throw new System.InvalidOperationException("Missing resident clip "+name+" and no Idle fallback");
        }
        private void Play(Act act,float fade)=>Play(act.Clip,fade,act.Max<=0?-1:Random.Range(act.Min,act.Max));
        /// <summary>Cross-fade to a clip. Loops hold for <paramref name="hold"/> seconds; one-shots (hold &lt; 0) last one clip length.</summary>
        public void Play(string name,float fade=.35f,float hold=-1)
        {
            var clip=Clip(name);Current=name;
            int to=1-_active;
            if(_mixer.GetInput(to).IsValid()){var old=_mixer.GetInput(to);_graph.Disconnect(_mixer,to);old.Destroy();}
            var playable=AnimationClipPlayable.Create(_graph,clip);playable.SetApplyFootIK(true);
            _graph.Connect(playable,0,_mixer,to);_active=to;_fade=fade<=0?1:0;_fadeTime=Mathf.Max(.01f,fade);
            _until=Time.time+(hold>=0?hold:clip.length*.98f);
        }
        private void NextActivity()
        {
            // Two work bouts, then occasionally a fidget so loops never read as a fixed cycle.
            if(Random.value<.3f){Play(_fidget[Random.Range(0,_fidget.Length)],.4f);return;}
            _workIndex=(_workIndex+1)%_work.Length;Play(_work[_workIndex],.4f);
        }
        private void Update()
        {
            _evaluated=false;
            if(_c==null || !_graph.IsValid())return;
            float dt=Time.deltaTime;bool engaged=Engaged;var player=_c.ExplorerPosition;
            float distance=Vector3.Distance(transform.position,player);
            if(engaged && !_wasEngaged){_walking=false;_engagedAt=Time.time;Play(_voice!=null && _voice.isPlaying?"Talk":"Idle",.3f,9999);}
            if(!engaged && _wasEngaged){_releasedAt=Time.time;Play("Idle",.3f,1.6f);}
            _wasEngaged=engaged;
            if(engaged)
            {
                string want=_voice!=null && _voice.isPlaying?"Talk":"Idle";
                if(want!=Current && Current!="Nod" && Current!="Shake")Play(want,.3f,9999);
                else if((Current=="Nod" || Current=="Shake") && Time.time>_until)Play(want,.3f,9999);
                // Turn to face the speaker after a short beat; the head has already turned.
                if(Time.time-_engagedAt>.25f)TurnTowards(Quaternion.LookRotation(Flat(player-transform.position,transform.forward)),dt);
            }
            else if(_walking || Time.time-_releasedAt<1.6f){}
            else
            {
                if(Quaternion.Angle(transform.rotation,_home)>.5f)TurnTowards(_home,dt);
                if(Time.time>_until)NextActivity();
                Bark(distance);
            }
            if(_fade<1){_fade=Mathf.Min(1,_fade+dt/_fadeTime);}
            _mixer.SetInputWeight(_active,_fade);_mixer.SetInputWeight(1-_active,1-_fade);
            // Throttle: unseen residents skip evaluation; distant visible ones update at ~1/3 rate.
            bool visible=false;foreach(var r in _renderers)if(r!=null && r.isVisible){visible=true;break;}
            if(_pending && !engaged)
            {
                if(!visible && distance>12)ApplyMove();
                else if(!_walking && CanWalk())_walking=true;
            }
            if(_walking && !engaged)WalkStep(dt);
            _skipped+=dt;
            // Residents next to the player always update (talk turn, look-at, VR peripheral view).
            if(_oriented && ((!visible && distance>LookRange+1) || (distance>25 && Time.frameCount%3!=_id%3)))return;
            _graph.Evaluate(_skipped);_skipped=0;_evaluated=true;
            if(!_oriented && Current=="Idle")
            {
                // Some auto humanoid avatars (male Peasant builds) play the shared clips facing backwards; measured on the first
                // real pose, the model (not the logical body) is turned once.
                _oriented=true;
                var shoulders=_animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position-_animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
                if(Vector3.Dot(Vector3.Cross(shoulders,Vector3.up),transform.forward)<0){_animator.transform.localRotation*=Quaternion.Euler(0,180,0);_graph.Evaluate(0);}
                if(!Engaged)NextActivity();
            }
        }
        private float _skipped;
        private bool _oriented;
        public bool Oriented=>_oriented;
        private static Vector3 Flat(Vector3 v,Vector3 fallback){v.y=0;return v.sqrMagnitude>.01f?v:fallback;}
        private void TurnTowards(Quaternion target,float dt)
        {
            float angle=Quaternion.Angle(transform.rotation,target);if(angle<.1f){_turnVel=0;return;}
            float next=Mathf.SmoothDamp(angle,0,ref _turnVel,.35f,200,dt);
            transform.rotation=Quaternion.RotateTowards(transform.rotation,target,angle-next);
        }
        private void LateUpdate()
        {
            UpdateBubble();
            if(!_evaluated || _head==null)return;
            var eye=_head.position;var target=_c.ExplorerPosition+Vector3.up*1.55f;
            var local=Quaternion.Inverse(transform.rotation)*(target-eye);
            float yaw=Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg;
            float pitch=-Mathf.Atan2(local.y,new Vector2(local.x,local.z).magnitude)*Mathf.Rad2Deg;
            bool wants=_c.Ashore && local.magnitude<LookRange && Mathf.Abs(yaw)<LookCone && (Engaged || !_c.StoryBlocksGameplay);
            // Brief delay before noticing, quick release; kneeling/handling work only half-turns the head.
            _lookDelay=wants?_lookDelay+Time.deltaTime:0;
            float busy=Engaged?1:Current=="Fix" || Current=="Water" || Current=="Plant" || Current=="Harvest" || Current=="PickUp" || Current=="Chest"?.55f:1;
            float goal=wants && (_lookDelay>.4f || Engaged)?busy:0;
            _lookWeight=Mathf.MoveTowards(_lookWeight,goal,Time.deltaTime*(goal>_lookWeight?1.6f:2.4f));
            float t=Time.deltaTime;
            _yaw=Mathf.SmoothDamp(_yaw,wants?Mathf.Clamp(yaw,-MaxYaw,MaxYaw):0,ref _yawVel,.22f,400,t);
            _pitch=Mathf.SmoothDamp(_pitch,wants?Mathf.Clamp(pitch,-MaxPitchUp,MaxPitchDown):0,ref _pitchVel,.22f,400,t);
            if(_lookWeight<=.001f)return;
            var up=transform.up;var right=transform.right;
            if(_neck!=null)_neck.rotation=Quaternion.AngleAxis(_yaw*.35f*_lookWeight,up)*Quaternion.AngleAxis(_pitch*.35f*_lookWeight,right)*_neck.rotation;
            _head.rotation=Quaternion.AngleAxis(_yaw*.65f*_lookWeight,up)*Quaternion.AngleAxis(_pitch*.65f*_lookWeight,right)*_head.rotation;
        }
        private void Bark(float distance)
        {
            if(Time.time<_nextBark || distance>BarkRange || !_c.Ashore || _c.DialogueOpen || _c.StoryBlocksGameplay || Time.time-_lastBarkAt<6)return;
            _nextBark=Time.time+Random.Range(25f,45f);
            int stage=FishingResidentLines.Stage(_c.Career.Lore);
            var key="bark_"+(stage>=3?Random.Range(1,5):stage>=1?Random.Range(1,4):Random.Range(1,3));
            if(!FishingResidentLines.Has(_id,key))return;
            ShowBark(key);
        }
        /// <summary>Speaks a short voiced bark with a bubble over the head (also used by validation).</summary>
        public void ShowBark(string key)
        {
            _lastBarkAt=Time.time;var clip=FishingResidentLines.Clip(_id+"/"+key);
            _bubbleText.text=FishingResidentLines.Text(_id,key);
            _bubbleUntil=Time.time+(clip!=null?clip.length:2)+1.4f;
            if(clip!=null && _voice!=null){_voice.Stop();_voice.PlayOneShot(clip);}
        }
        private void BuildBubble()
        {
            var root=new GameObject("Bark bubble",typeof(RectTransform),typeof(Canvas),typeof(CanvasGroup));root.layer=5;
            root.transform.SetParent(transform,false);root.transform.localPosition=Vector3.up*2.15f;
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=_c.viewCamera;
            var rect=root.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(520,92);rect.localScale=Vector3.one*.0022f;
            _bubble=root.GetComponent<CanvasGroup>();_bubble.alpha=0;_bubble.interactable=false;_bubble.blocksRaycasts=false;
            var panel=root.AddComponent<Image>();panel.sprite=Veil;panel.type=Image.Type.Sliced;panel.material=FishingUITheme.WorldMaterial;panel.raycastTarget=false;panel.color=Color.white;
            var go=new GameObject("Bark text",typeof(RectTransform),typeof(Text));go.layer=5;go.transform.SetParent(root.transform,false);
            var rt=go.GetComponent<RectTransform>();rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=new Vector2(30,12);rt.offsetMax=new Vector2(-30,-12);
            _bubbleText=go.GetComponent<Text>();_bubbleText.font=FishingUIFont.Font;_bubbleText.fontSize=30;_bubbleText.alignment=TextAnchor.MiddleCenter;
            _bubbleText.color=new Color(.969f,.922f,.816f);_bubbleText.material=FishingUITheme.WorldMaterial;_bubbleText.raycastTarget=false;
            _bubbleText.resizeTextForBestFit=true;_bubbleText.resizeTextMinSize=20;_bubbleText.resizeTextMaxSize=30;
            root.SetActive(false);
        }
        private static Sprite _veil;
        private static Sprite Veil
        {
            get
            {
                if(_veil!=null)return _veil;var t=FishingUITheme.PanelVeil;if(t==null)return null;
                return _veil=Sprite.Create(t,new Rect(0,0,t.width,t.height),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(40,40,40,40));
            }
        }
        private void UpdateBubble()
        {
            if(_bubble==null)return;
            bool show=Time.time<_bubbleUntil && !Engaged;
            float alpha=Mathf.MoveTowards(_bubble.alpha,show?1:0,Time.deltaTime/.3f);
            _bubble.alpha=alpha;_bubble.gameObject.SetActive(alpha>0);
            if(alpha>0 && _c.viewCamera!=null)
            {
                var b=_bubble.transform;b.position=(_head!=null?_head.position:transform.position+Vector3.up*1.6f)+Vector3.up*.42f;
                b.rotation=Quaternion.LookRotation(b.position-_c.viewCamera.transform.position);
            }
        }
        /// <summary>One-shot reaction during dialogue (Nod / Shake), then back to talking.</summary>
        public void React(string clip){if(Engaged)Play(clip,.25f);}
    }
}
