using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
namespace BoatAttack.Fishing
{
    /// <summary>VR comfort options (turn, locomotion, vignette, gusts) plus the vignette/blink overlay and stick teleport.</summary>
    public sealed class FishingXRComfort : MonoBehaviour
    {
        public enum Turn { Snap30, Snap45, Smooth }
        public static Turn TurnMode{get=>(Turn)PlayerPrefs.GetInt("FishingXR.Turn",0);set=>Save("FishingXR.Turn",(int)value);}
        public static bool Teleport{get=>PlayerPrefs.GetInt("FishingXR.Teleport",0)==1;set=>Save("FishingXR.Teleport",value?1:0);}
        /// <summary>0 off … 3 strong; default 2.</summary>
        public static int Vignette{get=>Mathf.Clamp(PlayerPrefs.GetInt("FishingXR.Vignette",2),0,3);set=>Save("FishingXR.Vignette",Mathf.Clamp(value,0,3));}
        /// <summary>0 slow 1.0 / 1 medium 1.3 / 2 normal 1.6 m/s.</summary>
        public static int SpeedLevel{get=>Mathf.Clamp(PlayerPrefs.GetInt("FishingXR.Speed",1),0,2);set=>Save("FishingXR.Speed",Mathf.Clamp(value,0,2));}
        public static bool GustComfort{get=>PlayerPrefs.GetInt("FishingXR.GustComfort",1)==1;set=>Save("FishingXR.GustComfort",value?1:0);}
        public static float WalkSpeed=>SpeedLevel==0?1f:SpeedLevel==1?1.3f:1.6f;
        public const float SmoothTurnSpeed=75,StickRamp=4,TeleportRange=6;
        public static float SnapDegrees=>TurnMode==Turn.Snap45?45:30;
        public static bool AllowsGustPush=>!XRSettings.isDeviceActive || !GustComfort;
        /// <summary>Desktop keeps the authored push; VR without gust comfort uses a slow drift.</summary>
        public static float GustSpeed(float desktop)=>XRSettings.isDeviceActive?.35f:desktop;
        public static string TurnName=>TurnMode==Turn.Smooth?"平滑转向":"分段转向 "+SnapDegrees+"°";
        public static string VignetteName=>new[]{"关闭","轻","中","强"}[Vignette];
        public static string SpeedName=>new[]{"慢 1.0","中 1.3","标准 1.6"}[SpeedLevel]+" m/s";
        private static void Save(string key,int value){PlayerPrefs.SetInt(key,value);PlayerPrefs.Save();}
        /// <summary>Ramped stick (acceleration cap); teleport mode removes stick translation except on ladders/in water.</summary>
        public static Vector2 Ramp(Vector2 previous,Vector2 stick,float dt,bool allowTeleportZero)
        {
            if(Teleport && allowTeleportZero)stick=Vector2.zero;
            return Vector2.MoveTowards(previous,Vector2.ClampMagnitude(stick,1),StickRamp*dt);
        }

        private FishingPrototype _c;private GameObject _overlay;private RawImage _vignette,_black;private int _builtStrength=-1;
        private float _motion,_blink,_lastYaw;private Vector3 _lastOrigin;private bool _hasLast;
        private LineRenderer _arc,_ring;private bool _aiming,_aimValid;private Vector3 _aimPoint;
        public bool Aiming=>_aiming;
        public void Build(FishingPrototype c)
        {
            _c=c;
            var root=new GameObject("XR comfort vignette",typeof(RectTransform),typeof(Canvas));root.transform.SetParent(c.viewCamera.transform,false);
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.sortingOrder=32000;
            var rect=root.GetComponent<RectTransform>();rect.sizeDelta=Vector2.one*1.3f;rect.localPosition=Vector3.forward*.3f;
            _vignette=Layer(root.transform);_black=Layer(root.transform);_black.color=Color.clear;
            _arc=FishingArt.Line("XR teleport arc",transform,c.lineMaterial,.012f);_arc.positionCount=0;
            _ring=FishingArt.Line("XR teleport target",transform,c.lineMaterial,.02f);_ring.positionCount=25;_ring.loop=true;
            _overlay=root;root.SetActive(false);_arc.enabled=_ring.enabled=false;
        }
        private static RawImage Layer(Transform parent)
        {
            var go=new GameObject("Layer",typeof(RectTransform),typeof(RawImage));go.transform.SetParent(parent,false);
            var rt=go.GetComponent<RectTransform>();rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;
            var image=go.GetComponent<RawImage>();image.raycastTarget=false;return image;
        }
        /// <summary>Radial alpha: clear centre shrinking with strength, black toward the lens edge.</summary>
        public static Texture2D VignetteTexture(int strength)
        {
            const int size=128;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,name="XR comfort vignette"};
            float inner=Mathf.Lerp(.78f,.38f,strength/3f),outer=inner+.32f;var pixels=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float r=new Vector2(x-size*.5f+.5f,y-size*.5f+.5f).magnitude/(size*.5f);
                pixels[y*size+x]=new Color32(0,0,0,(byte)(255*Mathf.SmoothStep(0,1,Mathf.InverseLerp(inner,outer,r))));
            }
            texture.SetPixels32(pixels);texture.Apply();return texture;
        }
        private void Update()
        {
            if(_c==null)return;
            bool xr=XRSettings.isDeviceActive && _c.XRSeat!=null && _c.XRSeat.Origin!=null;
            // Graphic.canvas is null while inactive, so keep the root itself.
            if(_overlay!=null)_overlay.SetActive(xr);
            if(!xr){_aiming=false;if(_arc!=null)_arc.enabled=false;if(_ring!=null)_ring.enabled=false;_hasLast=false;return;}
            float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
            TickTeleport();
            // Only artificial motion moves the origin; physical room walking leaves it fixed.
            var origin=_c.XRSeat.Origin.transform;float yaw=origin.eulerAngles.y;float target=0;
            if(_hasLast && dt>0)
            {
                float speed=(_c.Ashore?origin.position-_lastOrigin:Vector3.ProjectOnPlane(origin.position-_lastOrigin,Vector3.up)).magnitude/dt;
                float turn=Mathf.Abs(Mathf.DeltaAngle(_lastYaw,yaw));
                // Snaps and teleports are instantaneous, so they never ramp the vignette.
                if(speed*dt<1.5f)target=Mathf.Max(target,Mathf.InverseLerp(.15f,_c.Ashore?1.2f:9f,speed));
                if(turn<12)target=Mathf.Max(target,Mathf.InverseLerp(4,40,turn/dt));
            }
            _lastOrigin=origin.position;_lastYaw=yaw;_hasLast=true;
            _motion=Mathf.MoveTowards(_motion,target,dt*(target>_motion?5:2));
            if(_builtStrength!=Vignette){_builtStrength=Vignette;if(_vignette.texture!=null)Destroy(_vignette.texture);_vignette.texture=Vignette>0?VignetteTexture(Vignette):null;}
            _vignette.color=new Color(1,1,1,Vignette>0?_motion:0);
            bool sleeping=_c.Home!=null && _c.Home.Sleeping;
            _blink=Mathf.MoveTowards(_blink,sleeping?1:0,dt*(sleeping?2:5));
            _black.color=new Color(0,0,0,_blink);
        }
        private bool TeleportAllowed=>Teleport && _c.Ashore && !_c.Paused && !_c.StoryBlocksGameplay && !_c.WorldActionBlocksGameplay && !_c.InventoryOpen
            && !_c.XRNotebook.BlocksGameplay && !_c.XRSeat.HeadBlocked && !_c.Shore.Climbing && !_c.Shore.Swimming && !_c.Expedition.Aligning && Application.isFocused;
        private void TickTeleport()
        {
            if(!TeleportAllowed || !FishingXRTracking.TryPose(_c.viewCamera,XRNode.LeftHand,out var hand,out var rotation,true)){_aiming=false;_arc.enabled=_ring.enabled=false;return;}
            InputDevices.GetDeviceAtXRNode(XRNode.LeftHand).TryGetFeatureValue(CommonUsages.primary2DAxis,out var stick);
            if(!_aiming && stick.y>.7f)_aiming=true;
            if(_aiming && stick.magnitude<.25f)
            {
                _aiming=false;_arc.enabled=_ring.enabled=false;
                if(_aimValid && _c.Shore.Relocate(_aimPoint,_c.Shore.Actor.rotation)){_blink=.85f;_c.WorldAudio?.Play("UIConfirm",_aimPoint,.15f);}
                return;
            }
            if(!_aiming){_arc.enabled=_ring.enabled=false;return;}
            _aimValid=Aim(hand,rotation*Vector3.forward,out _aimPoint);
            var color=_aimValid?new Color(.55f,.95f,.85f,.9f):new Color(1,.45f,.35f,.7f);
            _arc.startColor=_arc.endColor=_ring.startColor=_ring.endColor=color;_arc.enabled=true;_ring.enabled=_aimValid;
            for(int i=0;i<25;i++){float a=i/25f*Mathf.PI*2;_ring.SetPosition(i,_aimPoint+new Vector3(Mathf.Cos(a)*.3f,.04f,Mathf.Sin(a)*.3f));}
        }
        private static readonly Vector3[] ArcPoints=new Vector3[32];
        private bool Aim(Vector3 start,Vector3 direction,out Vector3 point)
        {
            // Ballistic arc; the first surface hit is the target, so walls block it naturally.
            var velocity=direction.normalized*7f;var p=start;int count=0;point=start;bool hit=false;RaycastHit info=default;
            ArcPoints[count++]=p;
            for(int i=0;i<30 && !hit;i++)
            {
                var next=p+velocity*.05f;velocity+=Physics.gravity*.05f;
                if(Physics.Linecast(p,next,out info,~((1<<4)|(1<<5)),QueryTriggerInteraction.Ignore)){next=info.point;hit=true;}
                ArcPoints[count++]=next;p=next;
            }
            _arc.positionCount=count;_arc.SetPositions(ArcPoints);
            if(!hit || info.normal.y<.7f || info.collider.transform.IsChildOf(_c.Player.transform))return false;
            point=info.point;var feet=_c.Shore.Position;
            if(Vector3.ProjectOnPlane(point-feet,Vector3.up).magnitude>TeleportRange || Mathf.Abs(point.y-feet.y)>1.2f)return false;
            return Continuous(feet,point);
        }
        /// <summary>Ground must exist along the whole hop, so teleport cannot skip gaps, water or puzzle bridges.</summary>
        private static bool Continuous(Vector3 from,Vector3 to)
        {
            float top=Mathf.Max(from.y,to.y)+1.2f,bottom=Mathf.Min(from.y,to.y)-.8f;int steps=Mathf.CeilToInt(Vector3.Distance(from,to)/.4f);
            for(int i=1;i<steps;i++)
            {
                var p=Vector3.Lerp(from,to,i/(float)steps);
                if(!Physics.Raycast(new Vector3(p.x,top,p.z),Vector3.down,out var hit,top-bottom,~((1<<4)|(1<<5)),QueryTriggerInteraction.Ignore) || hit.normal.y<.5f)return false;
            }
            return true;
        }
        private void OnDestroy(){if(_vignette!=null && _vignette.texture!=null)Destroy(_vignette.texture);}
    }
}
