using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    /// <summary>Three real, alternating 7.5 cm strokes; tracking loss requires releasing the grip.</summary>
    public sealed class FishingTreeShake
    {
        private Vector3 _origin,_previous,_axis;
        private float _clock,_lastStroke=-1;
        private int _direction;
        private bool _sample,_release=true;
        public int Strokes {get;private set;}
        public void Suspend(){Clear();_release=true;}
        private void Clear(){_sample=false;_direction=0;_axis=Vector3.zero;Strokes=0;_clock=0;_lastStroke=-1;}
        public bool Sample(Vector3 palm,bool held,bool tracked,float seconds)
        {
            if(!tracked || !Finite(palm.x) || !Finite(palm.y) || !Finite(palm.z) || !Finite(seconds) || seconds<=0 || seconds>.15f){Suspend();return false;}
            if(!held){Clear();_release=false;return false;}if(_release || Strokes>=3)return false;
            _clock+=seconds;
            if(!_sample){_origin=_previous=palm;_sample=true;return false;}
            var movement=Vector3.ProjectOnPlane(palm-_previous,Vector3.up);_previous=palm;
            if(movement.magnitude>.30f){Suspend();return false;}
            var travel=Vector3.ProjectOnPlane(palm-_origin,Vector3.up);
            if(_axis.sqrMagnitude<.5f){if(travel.magnitude<.025f)return false;_axis=travel.normalized;}
            float displacement=Vector3.Dot(travel,_axis);int direction=displacement>=0?1:-1;
            if(direction==_direction){if(Mathf.Abs(displacement)>.075f)_origin=palm;return false;}
            if(Mathf.Abs(displacement)<.075f || movement.magnitude/seconds<.18f || _clock-_lastStroke<.16f)return false;
            _origin=palm;_direction=direction;_lastStroke=_clock;Strokes++;return true;
        }
        private static bool Finite(float n)=>!float.IsNaN(n) && !float.IsInfinity(n);
    }

    public sealed partial class FishingHome
    {
        private sealed class OrchardTree
        {
            public Transform Root,Fruit;public Quaternion Rest;public readonly FishingTreeShake Gesture=new FishingTreeShake();
            public Vector3 Start,Ground,Head;public float Shake=-1,Fall=-1;public bool Bonk,Hit;
        }
        private readonly OrchardTree[] _orchard=new OrchardTree[FishingCareer.HomeFruitTreeCount];
        private FishingCareer _orchardProfile;
        private AudioSource _orchardAudio;
        private AudioClip _orchardOuch;
        private float _orchardHapticAt,_orchardOuchAt=-10;
        private Image _orchardEdge;
        private Sprite _orchardEdgeSprite;
        private Texture2D _orchardEdgeTexture;
        public Transform OrchardTreeRoot(int id)=>id>=0 && id<_orchard.Length?_orchard[id]?.Root:null;
        public Transform OrchardFruit(int id)=>id>=0 && id<_orchard.Length?_orchard[id]?.Fruit:null;
        public bool OrchardFruitLanded(int id)=>id>=0 && id<_orchard.Length && _orchard[id]!=null && _orchard[id].Fall>=1.12f;
        public static bool OrchardHeadBonk(int tree,int day)=>tree>=0 && tree<FishingCareer.HomeFruitTreeCount && day>=0 && (tree*17+day*13)%5==0;
        private void RegisterOrchard(int id,Transform tree)
        {
            var fruit=FishingV019Art.Spawn("apple",transform,Vector3.zero,.18f);fruit.name="V036 fallen island fruit "+id;fruit.gameObject.SetActive(false);
            _orchard[id]=new OrchardTree{Root=tree,Fruit=fruit,Rest=tree.localRotation};_interactables[id+7]=tree;_interactionPoints[id+7]=tree.position+Vector3.up*1.1f;
            if(_orchardAudio!=null)return;
            _orchardProfile=_c.Career;_orchardAudio=new GameObject("Fruit bonk reaction").AddComponent<AudioSource>();_orchardAudio.transform.SetParent(transform,false);_orchardAudio.playOnAwake=false;_orchardAudio.spatialBlend=0;
            _orchardOuch=Resources.Load<AudioClip>("FishingV036/OrchardOuch");_c.RegisterAudioSource(_orchardAudio,FishingAudioCategory.Effects,.58f);
        }
        private bool OrchardAvailable=>_c!=null && _c.Ashore && !_c.Paused && !_c.StartMenuOpen && !_c.StoryBlocksGameplay && !_c.WorldActionBusy
            && !_c.InventoryOpen && !_c.JournalOpen && !_c.ShopBlocksGameplay && !_c.Shore.Swimming && !_c.Shore.Climbing && !_c.HasPendingCatch
            && (_c.Farm==null || !_c.Farm.CarryingCan) && _c.Hands.CanPresentTools && !_c.XRSeat.HeadBlocked
            && (_c.XRNotebook==null || !_c.XRNotebook.BlocksGameplay);
        private bool ReachOrchard(int id)
        {
            var tree=id>=0 && id<_orchard.Length?_orchard[id]:null;if(tree==null || !_c.Ashore || _c.Shore.Swimming || _c.Shore.Climbing)return false;
            var point=_interactionPoints[id+7];if(FishingPrototype.FlatDistance(_c.Shore.Position,point)>2.2f || Mathf.Abs(_c.Shore.Position.y-point.y)>2)return false;
            var from=_c.Shore.Position+Vector3.up*_c.Shore.EyeHeight;var delta=point-from;
            int hits=Physics.RaycastNonAlloc(from,delta.normalized,_interactionHits,delta.magnitude,~(1<<4),QueryTriggerInteraction.Ignore);if(hits==_interactionHits.Length)return false;
            for(int i=0;i<hits;i++){var hit=_interactionHits[i];if(hit.collider!=_c.Shore.Body && !hit.transform.IsChildOf(tree.Root) && !hit.transform.IsChildOf(tree.Fruit) && !hit.transform.IsChildOf(_c.Player.transform) && !FishingShore.NonSolidSurface(hit.collider))return false;}
            return true;
        }
        private string OrchardTitle(int id)
        {
            var tree=_orchard[id];string key=XRSettings.isDeviceActive?"A ":"E ";
            if(tree.Fall>=0)return tree.Fall<1.12f?"果子正在落下 · 稍等片刻":key+"拾取海岛甜果";
            if(tree.Shake>=0)return "枝叶轻摇 · 留意树下的果子";
            if(!_c.Career.CanHarvestHomeFruit(id))return "今日成熟的果子已采下 · 明日再来";
            return XRSettings.isDeviceActive?"握住树干，左右轻摇三次 · 真实手部动作":key+"轻摇果树 · 等果子落地后拾取";
        }
        private bool InteractOrchard(int id)
        {
            if(!OrchardAvailable)return false;var tree=_orchard[id];
            if(tree.Fall>=0)
            {
                if(tree.Fall<1.12f)return false;
                if(XRSettings.isDeviceActive)
                {
                    if(!_c.Hands.TryControllerPalm(false,out var palm,out _) || Vector3.Distance(palm,tree.Fruit.position)>.42f){_c.Notice("将右手伸向落果，靠近后按 A 拾取。",4);return false;}
                    return PickOrchardFruit(id);
                }
                var contact=FishingContactGeometry.ClosestPoint(tree.Fruit,_c.Shore.Character.ShoulderPosition(false));
                return _c.BeginWorldAction(FishingActionKind.Pickup,"orchard:"+id,tree.Fruit,contact,_c.Shore.Actor.rotation,()=>PickOrchardFruit(id));
            }
            if(tree.Shake>=0)return false;
            if(!_c.Career.CanHarvestHomeFruit(id)){_c.Notice("今天这棵树已采过果子，明日再来。",4);return false;}
            if(XRSettings.isDeviceActive){_c.Notice("松开握把，将手伸到树干旁，再握住并左右轻摇三次。",5);return true;}
            return ShakeOrchardTree(id);
        }
        public bool ShakeOrchardTree(int id)
        {
            if(id<0 || id>=_orchard.Length || !OrchardAvailable || !ReachOrchard(id) || !_c.Career.CanHarvestHomeFruit(id))return false;
            var tree=_orchard[id];if(tree.Shake>=0 || tree.Fall>=0)return false;
            var away=Vector3.ProjectOnPlane(_c.Shore.Position-tree.Root.position,Vector3.up).normalized;if(away.sqrMagnitude<.1f)away=Vector3.back;
            var seed=tree.Root.position+away*1.55f;if(!_c.Shore.Ground(seed,out var floor))return false;
            tree.Start=tree.Root.position+Vector3.up*2.9f+away*.45f;tree.Ground=floor+Vector3.up*.025f;tree.Head=_c.Shore.Character.EyePosition+Vector3.up*.27f;
            tree.Bonk=OrchardHeadBonk(id,_c.Career.Calendar.Day);tree.Hit=false;tree.Shake=0;tree.Gesture.Suspend();
            _c.Effects?.Burst(tree.Root.position+Vector3.up*2.5f,new Color(.66f,.83f,.36f));_c.Notice("枝叶沙沙作响……等果子落地后再拾取。",4);return true;
        }
        private bool PickOrchardFruit(int id)
        {
            var tree=_orchard[id];if(tree.Fall<1.12f || !tree.Fruit.gameObject.activeSelf || !ReachOrchard(id) || !_c.Career.TryHarvestHomeFruit(id))
            {_c.Notice("背包暂时装不下，果子会留在地上。",4);return false;}
            tree.Fruit.gameObject.SetActive(false);tree.Fall=-1;_interactables[id+7]=tree.Root;_interactionPoints[id+7]=tree.Root.position+Vector3.up*1.1f;
            _c.Save();_c.Effects?.Burst(tree.Ground,new Color(1,.78f,.35f));_c.Notice("拾取海岛甜果 ×1 · 已收入背包",4);return true;
        }
        private void UpdateOrchard()
        {
            if(_orchardProfile!=_c.Career){ResetOrchardMotion();for(int i=0;i<_orchard.Length;i++){var tree=_orchard[i];if(tree==null)continue;tree.Fruit.gameObject.SetActive(false);tree.Fall=-1;_interactables[i+7]=tree.Root;_interactionPoints[i+7]=tree.Root.position+Vector3.up*1.1f;}_orchardProfile=_c.Career;}
            float dt=_c.Paused?0:Mathf.Min(Time.deltaTime,.05f);bool xr=XRSettings.isDeviceActive;
            for(int id=0;id<_orchard.Length;id++)
            {
                var tree=_orchard[id];if(tree==null)continue;
                if(tree.Shake>=0)
                {
                    tree.Shake+=dt;tree.Root.localRotation=tree.Rest*Quaternion.Euler(0,0,Mathf.Sin(tree.Shake*24)*2.4f*Mathf.Max(0,1-tree.Shake));
                    if(tree.Shake>=.55f && tree.Fall<0){tree.Fall=0;tree.Fruit.position=tree.Start;tree.Fruit.gameObject.SetActive(true);_interactables[id+7]=tree.Fruit;_interactionPoints[id+7]=tree.Ground+Vector3.up*.10f;}
                    if(tree.Shake>=1){tree.Shake=-1;tree.Root.localRotation=tree.Rest;}
                }
                if(tree.Fall>=0 && tree.Fall<1.12f)
                {
                    tree.Fall+=dt;float t=tree.Fall;
                    if(tree.Bonk && t<.48f)tree.Fruit.position=Vector3.Lerp(tree.Start,tree.Head,Mathf.SmoothStep(0,1,t/.48f));
                    else
                    {
                        if(tree.Bonk && !tree.Hit){tree.Hit=true;if(Vector3.Distance(_c.Shore.Character.EyePosition+Vector3.up*.27f,tree.Head)<.6f)OrchardBonk();}
                        float u=Mathf.Clamp01((t-(tree.Bonk?.48f:0))/(tree.Bonk?.64f:1.12f));
                        tree.Fruit.position=Vector3.Lerp(tree.Bonk?tree.Head:tree.Start,tree.Ground,u)+Vector3.up*Mathf.Sin(u*Mathf.PI)*(tree.Bonk?.42f:.30f);
                    }
                    tree.Fruit.Rotate(Vector3.right,dt*150,Space.World);
                    if(tree.Fall>=1.12f){tree.Fruit.position=tree.Ground;tree.Fruit.rotation=Quaternion.identity;}
                }
                if(!xr || _c.Automated || !OrchardAvailable || !Application.isFocused || tree.Shake>=0 || tree.Fall>=0 || !_c.Career.CanHarvestHomeFruit(id) || FishingPrototype.FlatDistance(_c.Shore.Position,tree.Root.position)>2.2f){tree.Gesture.Suspend();continue;}
                var hand=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);bool tracked=_c.Hands.TryControllerPalm(false,out var palm,out _);
                bool held=hand.TryGetFeatureValue(CommonUsages.grip,out float grip)?grip>=.55f:hand.TryGetFeatureValue(CommonUsages.gripButton,out bool pressed) && pressed;
                bool native=hand.TryGetFeatureValue(CommonUsages.devicePosition,out var nativePosition);
                bool near=tracked && native && FishingPrototype.FlatDistance(palm,tree.Root.position)<.70f && palm.y>tree.Root.position.y+.55f && palm.y<tree.Root.position.y+1.9f;
                // Native tracking coordinates exclude stick locomotion and snap turns of the world rig.
                if(tree.Gesture.Sample(nativePosition,held,near,Time.deltaTime)){OrchardHaptic(hand,.18f,.035f);_c.Notice("轻摇果树 · "+tree.Gesture.Strokes+" / 3",2);if(tree.Gesture.Strokes>=3)ShakeOrchardTree(id);}
            }
        }
        private void OrchardHaptic(InputDevice hand,float strength,float duration)
        {
            if(Time.unscaledTime<_orchardHapticAt || !hand.isValid || !hand.TryGetHapticCapabilities(out var cap) || !cap.supportsImpulse)return;
            hand.SendHapticImpulse(0,Mathf.Clamp(strength,0,.28f),Mathf.Clamp(duration,0,.07f));_orchardHapticAt=Time.unscaledTime+.16f;
        }
        private void OrchardBonk()
        {
            _orchardOuchAt=Time.unscaledTime;_c.Notice("哎哟！小果子也想打个招呼……果子已落到脚边，可继续拾取。",5);
            if(_orchardOuch!=null && !_c.QuietMode){_c.ApplyAudioSettings();_orchardAudio.PlayOneShot(_orchardOuch);}
            if(XRSettings.isDeviceActive && Application.isFocused && !_c.XRSeat.HeadBlocked)OrchardHaptic(InputDevices.GetDeviceAtXRNode(XRNode.RightHand),.26f,.06f);
        }
        public void ApplyOrchardPresentation()
        {
            float age=Time.unscaledTime-_orchardOuchAt;bool active=age>=0 && age<.25f && !_c.Paused;
            if(_orchardEdge!=null)_orchardEdge.gameObject.SetActive(active && XRSettings.isDeviceActive);
            if(!active)return;float fade=1-age/.25f;
            if(!XRSettings.isDeviceActive){if(!_c.Weather.ReducedEffects)_c.viewCamera.transform.position+=_c.viewCamera.transform.right*(Mathf.Sin(age*90)*.018f*fade)+Vector3.up*(Mathf.Sin(age*60)*.006f*fade);return;}
            if(_orchardEdge==null)
            {
                _orchardEdgeTexture=new Texture2D(64,64,TextureFormat.RGBA32,false){name="Gentle orchard bonk edge",wrapMode=TextureWrapMode.Clamp};var colors=new Color32[64*64];
                for(int y=0;y<64;y++)for(int x=0;x<64;x++){float r=new Vector2(x/63f*2-1,y/63f*2-1).magnitude;colors[y*64+x]=new Color32(232,193,114,(byte)(Mathf.SmoothStep(0,.14f,Mathf.InverseLerp(.65f,1,r))*255));}
                _orchardEdgeTexture.SetPixels32(colors);_orchardEdgeTexture.Apply(false,true);_orchardEdgeSprite=Sprite.Create(_orchardEdgeTexture,new Rect(0,0,64,64),Vector2.one*.5f);
                var go=new GameObject("Soft orchard surprise edge",typeof(RectTransform),typeof(Canvas),typeof(Image));go.transform.SetParent(transform,false);go.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;go.GetComponent<Canvas>().sortingOrder=260;
                _orchardEdge=go.GetComponent<Image>();_orchardEdge.sprite=_orchardEdgeSprite;_orchardEdge.raycastTarget=false;_orchardEdge.rectTransform.sizeDelta=new Vector2(1024,1024);go.transform.localScale=Vector3.one*.0012f;
            }
            _orchardEdge.gameObject.SetActive(true);_orchardEdge.color=new Color(1,1,1,fade);var camera=_c.viewCamera.transform;_orchardEdge.transform.SetPositionAndRotation(camera.position+camera.forward*.38f,camera.rotation);
        }
        private void ResetOrchardMotion(){foreach(var tree in _orchard)if(tree!=null){tree.Gesture.Suspend();tree.Shake=-1;tree.Root.localRotation=tree.Rest;}_orchardOuchAt=-10;if(_orchardEdge!=null)_orchardEdge.gameObject.SetActive(false);}
        private void DisposeOrchard(){if(_orchardEdgeSprite!=null)Destroy(_orchardEdgeSprite);if(_orchardEdgeTexture!=null)Destroy(_orchardEdgeTexture);}
    }
}
