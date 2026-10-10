using UnityEngine;
using UnityEngine.XR;
namespace BoatAttack.Fishing
{
    /// <summary>One net and one fish. Measured capture; bounded, kinematic handling cannot kick the hull or camera.</summary>
    public sealed class FishingCatchRig : MonoBehaviour
    {
        public enum Place { Net, Hand, Tray }
        private FishingPrototype _c;private readonly FishingNetGate _gate=new FishingNetGate();
        private readonly FishingNetTransferGate _transfer=new FishingNetTransferGate();
        private Vector3 _boxLocal;private Transform _tray;
        private MeshFilter _receivingRim;
        private readonly FishingPressEdge _left=new FishingPressEdge(),_right=new FishingPressEdge();
        private bool _assist,_netHeld,_hadSample;private float _assistTime,_releaseUntil;
        private Vector3 _lastNet;private string _fishId;
        public Place Location { get; private set; }
        public bool NetHeld=>_netHeld;
        public bool ReleaseConfirmation=>Time.unscaledTime<_releaseUntil;
        /// <summary>投影到右侧箱沿的真实上表面；横倾时不能用远端网格角点代替面内可达点。</summary>
        public Vector3 ReceivingPoint(Vector3 from)
        {
            var surface=_receivingRim.transform;var bounds=_receivingRim.sharedMesh.bounds;
            var local=surface.InverseTransformPoint(from);
            local.x=Mathf.Clamp(local.x,bounds.min.x,bounds.max.x);local.y=bounds.max.y;
            local.z=Mathf.Clamp(local.z,bounds.min.z,bounds.max.z);
            return surface.TransformPoint(local);
        }
        public void Build(FishingPrototype c)
        {
            _c=c;Location=Place.Net;_boxLocal=c.FishBox.localPosition;
            foreach(var filter in c.FishBox.GetComponentsInChildren<MeshFilter>())
                if(filter.name=="Top rim" && filter.transform.localPosition.x>0){_receivingRim=filter;break;}
            if(_receivingRim==null)throw new System.InvalidOperationException("捕鱼箱缺少右侧接收上沿");
            _tray=FishingArt.Shape("Safe catch inspection tray",PrimitiveType.Cylinder,c.Player.transform,new Vector3(.65f,1.04f,.45f),new Vector3(.8f,.025f,.8f),c.teal).transform;
            _tray.gameObject.SetActive(false);
            var ring=FishingArt.Line("Fish box receiving rim",c.FishBox,c.teal,.022f,false);ring.positionCount=33;
            for(int i=0;i<33;i++){float a=i*Mathf.PI*2/32;ring.SetPosition(i,new Vector3(Mathf.Cos(a)*.36f,.4f,Mathf.Sin(a)*.36f));}
        }
        public void ConfigureLayout(bool xr)
        {
            // 桌面收鱼箱靠近角色右手可达侧；保留 XR 布局，并等收手后再移回航行位置。
            bool receiving=_c.Anchored || _c.HasPendingCatch || (_c.WorldActionBusy && (_c.Actions.Kind==FishingActionKind.Store || _c.Actions.Kind==FishingActionKind.Release));
            _c.FishBox.localPosition=xr?new Vector3(-.5f,.85f,.8f):receiving?new Vector3(-.5f,.85f,.25f):_boxLocal;
            if(xr && !_netHeld && (!_c.HasPendingCatch || Location!=Place.Net))
                _c.LandingNet.SetPositionAndRotation(_c.BoatPoint(new Vector3(-.32f,1.15f,.25f)),Quaternion.Euler(0,_c.Player.transform.eulerAngles.y,0));
        }
        public bool RequestScoop()
        {
            if(_c.Paused || !_c.Anchored || _c.HasPendingCatch || _c.Session.Phase!=CatchPhase.Landing || XRSettings.isDeviceActive)return false;
            if(!_assist){_c.Session.HoldForNet();_assist=true;_assistTime=0;_gate.Reset();_hadSample=false;}
            return true;
        }
        public void ToggleInspect(){if(_c.HasPendingCatch && !_c.Paused){Location=Location==Place.Hand?Place.Tray:Place.Hand;if(Location==Place.Hand)_c.MeasureCatch();}}
        public void ApplyActionContactPose(Vector3 position,Quaternion frame)
        {
            if(_c.HasPendingCatch)_c.ActiveFish.SetPositionAndRotation(position,frame*Quaternion.Euler(0,-90,0));
        }
        public void PrepareHandAction(){if(_c.HasPendingCatch && !_c.Paused)Location=Place.Hand;}
        public void RequestRelease(bool animate=false)
        {
            if(!_c.HasPendingCatch || _c.Paused)return;
            if(ReleaseConfirmation){_releaseUntil=0;if(animate)_c.RequestCatchResolution(false);else _c.ResolveCatch(false);}
            else{_releaseUntil=Time.unscaledTime+3;_c.Notice("再按 X 确认放生；不出售、不增加普通鱼金币。",3);}
        }
        public void ResetFixture(){if(!_c.HasIsolatedProfile)throw new System.InvalidOperationException("Isolated fixtures only");_fishId=null;Recover();}
        public void Recover()
        {
            _assist=_netHeld=_hadSample=_tipSample=false;_assistTime=_releaseUntil=0;_gate.Reset();_transfer.Reset();_left.Reset();_right.Reset();Location=Place.Tray;
        }
        private void OnApplicationFocus(bool focus){if(!focus)Recover();}
        private void OnApplicationPause(bool pause){if(pause)Recover();}
        public void StepDesktop(float dt)
        {
            if(!_assist || _c.Paused)return;
            if(_c.Session.Phase!=CatchPhase.Landing){_assist=false;return;}
            if(dt<=0 || float.IsNaN(dt) || float.IsInfinity(dt))return;
            _assistTime+=Mathf.Min(dt,.05f);
            var center=_c.LandingTarget+Vector3.up*Mathf.Clamp((_assistTime-.25f)*.8f,0,.32f);
            ApplyNetPose(center,DesktopNetRotation(center),dt);
        }
        public bool CaptureAndStoreForAutomation()
        {
            if(!_c.HasIsolatedProfile)throw new System.InvalidOperationException("Only isolated fixtures may fast-step the assist");
            if(!RequestScoop())return false;
            for(int i=0;i<45 && !_c.HasPendingCatch;i++)StepDesktop(.02f);
            if(!_c.HasPendingCatch)return false;
            if(FishingSpecies.Of(_c.Career.PendingCatch.fish.kind).Legendary){_c.MeasureCatch();_c.ResolveCatch(false);}else _c.ResolveCatch(true);return true;
        }
        public bool ApplyNetPose(Vector3 center,Quaternion rotation,float dt)
        {
            var net=_c.LandingNet;net.SetPositionAndRotation(center-rotation*new Vector3(FishingDetailArt.NetMouthOffset,0,0),rotation);
            var relative=Quaternion.Inverse(rotation)*(_c.LandingTarget-center);
            var boatRotation=Quaternion.Euler(0,_c.Player.transform.eulerAngles.y,0);
            var local=Quaternion.Inverse(boatRotation)*(center-_c.Player.transform.position);
            float speed=_hadSample && dt>0?Vector3.Distance(local,_lastNet)/dt:0;_lastNet=local;_hadSample=true;
            bool caught=_gate.Step(_c.Anchored && !_c.Paused && !_c.HasPendingCatch && _c.Session.Phase==CatchPhase.Landing,
                new Vector2(relative.x,relative.z/.8f).magnitude,relative.y,Vector3.Dot(rotation*Vector3.up,Vector3.up),speed,local.y,dt);
            if(caught)
            {
                _assist=false;Location=Place.Net;bool accepted=_c.BeginNetCatch();
                if(accepted && XRSettings.isDeviceActive){var hand=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);if(hand.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse)hand.SendHapticImpulse(0,.22f,.06f);}
                return accepted;
            }
            return false;
        }
        private Quaternion DesktopNetRotation(Vector3 center)
        {
            var along=(center-_c.Shore.Character.ShoulderPosition(true)).normalized;
            var up=Vector3.ProjectOnPlane(Vector3.up,along);
            if(along.sqrMagnitude<.9f || up.sqrMagnitude<.01f)return Quaternion.Euler(0,_c.Player.transform.eulerAngles.y,0);
            return Quaternion.LookRotation(Vector3.Cross(along,up).normalized,up.normalized);
        }
        private Transform _gripFish;
        private Vector3 _leftFishGrip,_rightFishGrip;
        public Vector3 FishPalmLocal(bool left)
        {
            var fish=_c.ActiveFish;
            if(_gripFish!=fish)
            {
                _gripFish=fish;var filter=fish.GetComponentInChildren<MeshFilter>();
                if(filter==null || filter.sharedMesh==null){_leftFishGrip=_rightFishGrip=Vector3.zero;}
                else
                {
                    var bounds=filter.sharedMesh.bounds;float span=Mathf.Min(bounds.extents.z*.28f,.16f/Mathf.Max(.01f,fish.lossyScale.z));
                    var a=bounds.center+new Vector3(0,-bounds.extents.y*.7f,span);
                    var b=bounds.center+new Vector3(0,-bounds.extents.y*.7f,-span);
                    _leftFishGrip=fish.InverseTransformPoint(FishingContactGeometry.ClosestPoint(fish,filter.transform.TransformPoint(a)));
                    _rightFishGrip=fish.InverseTransformPoint(FishingContactGeometry.ClosestPoint(fish,filter.transform.TransformPoint(b)));
                }
            }
            return left?_leftFishGrip:_rightFishGrip;
        }
        public void Apply(float dt)
        {
            if(_c==null || _c.Player==null)return;
            bool xr=XRSettings.isDeviceActive;ConfigureLayout(xr);
            if(_c.Paused){if(_netHeld || Location==Place.Hand)Recover();}
            else if(xr)ReadTracked(dt);
            else StepDesktop(dt);
            bool landing=_c.Anchored && !_c.HasPendingCatch && _c.Session.Phase==CatchPhase.Landing;
            if(!xr && landing && !_assist)
            {
                var rotation=DesktopNetRotation(_c.LandingTarget);
                _c.LandingNet.SetPositionAndRotation(_c.LandingTarget-rotation*new Vector3(FishingDetailArt.NetMouthOffset,0,0),rotation);
            }
            if(_c.HasPendingCatch && Location==Place.Net && !_netHeld)
            {
                var parked=Quaternion.Euler(0,_c.Player.transform.eulerAngles.y,0);
                _c.LandingNet.SetPositionAndRotation(_c.NetTray+Vector3.up*.13f-parked*new Vector3(FishingDetailArt.NetMouthOffset,0,0),parked);
            }
            else if(!xr && !landing && !_netHeld && !_assist && (!_c.HasPendingCatch || Location!=Place.Net))
            {
                _c.LandingNet.localPosition=_c.StowedNetPosition;_c.LandingNet.localRotation=_c.StowedNetRotation;
            }
            // 网的实际姿态先确定，再供应左掌目标；不能放在无待处理鱼的提前返回之后。
            if(!_c.Paused && (_netHeld || _assist || (!xr && landing)))
                _c.Hands.SetToolTarget(true,_c.LandingNet,new Vector3(.05f,.035f,0),Quaternion.LookRotation(Vector3.right,Vector3.up),.9f);
            _tray.gameObject.SetActive(_c.HasPendingCatch && Location==Place.Tray && !_c.Ashore);
            if(!_c.HasPendingCatch){_fishId=null;return;}
            var pending=_c.Career.PendingCatch;
            if(_fishId!=pending.fish.id){_fishId=pending.fish.id;_c.RestorePendingFishVisual();}
            var fish=_c.ActiveFish;fish.gameObject.SetActive(true);
            Vector3 pos;Quaternion rot;
            if(Location==Place.Hand)
            {
                if(xr && FishingXRTracking.TryPose(_c.viewCamera,XRNode.RightHand,out pos,out rot)){}
                else if(!xr)
                {
                    _c.Hands.GetToolFrame(out var origin,out rot);
                    pos=_c.ActionMovesFish?_c.ActionFishPosition:origin+rot*new Vector3(.04f,-.18f,.28f);
                    if(_c.ActionMovesFish)rot=_c.ActionToolRotation;
                }
                else{Recover();pos=_c.NetTray;rot=_c.Player.transform.rotation;}
            }
            else if(Location==Place.Net){pos=_c.LandingNet.TransformPoint(new Vector3(FishingDetailArt.NetMouthOffset,-.13f,0));rot=_c.LandingNet.rotation;}
            else{pos=_c.NetTray;rot=Quaternion.Euler(0,_c.Player.transform.eulerAngles.y,0);}
            float flop=Location==Place.Hand?0:Mathf.Sin(Time.time*9)*.035f;
            fish.SetPositionAndRotation(pos+Vector3.up*Mathf.Abs(flop),rot*Quaternion.Euler(flop*120,-90,flop*180));
            if(Location==Place.Hand && !_c.Paused)
            {
                var left=FishPalmLocal(true);var right=FishPalmLocal(false);
                if(!xr && !_c.ActionMovesFish)_c.Hands.FitToolPose(fish,left,right,!_netHeld,true);
                var palmRotation=Quaternion.LookRotation(Vector3.right,Vector3.down);
                if(!_netHeld && (xr || _c.Hands.CanReach(true,fish.TransformPoint(left),0)))_c.Hands.SetToolTarget(true,fish,left,palmRotation,.5f);
                else if(!xr)_c.Hands.RestHand(true);
                _c.Hands.SetToolTarget(false,fish,right,palmRotation,.55f);
            }
        }
        private Vector3 _lastTip;private bool _tipSample;
        public bool TryTipIntoBox(Vector3 center,Quaternion rotation,float dt)
        {
            if(dt<=0 || float.IsNaN(dt) || float.IsInfinity(dt))return false;
            var yaw=Quaternion.Euler(0,_c.Player.transform.eulerAngles.y,0);
            var local=Quaternion.Inverse(yaw)*(center-_c.Player.transform.position);
            float speed=_tipSample?Vector3.Distance(local,_lastTip)/dt:0;_lastTip=local;_tipSample=true;
            var delta=center-(_c.FishBox.position+Vector3.up*.4f);
            bool ordinary=_c.HasPendingCatch && !FishingSpecies.Of(_c.Career.PendingCatch.fish.kind).Legendary && Location==Place.Net && !_c.Paused;
            bool tip=_transfer.Step(ordinary,new Vector2(delta.x,delta.z).magnitude,delta.y,Vector3.Dot(rotation*Vector3.up,Vector3.up),speed,dt);
            return tip && _c.ResolveCatch(true);
        }
        private static bool Grip(InputDevice device){if(device.TryGetFeatureValue(CommonUsages.gripButton,out bool held))return held;return device.TryGetFeatureValue(CommonUsages.grip,out float value) && value>.65f;}
        private void ReadTracked(float dt)
        {
            var left=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);var right=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            Vector3 lp=Vector3.zero;Quaternion lq=Quaternion.identity;
            bool valid=Application.isFocused && FishingXRTracking.TryPose(_c.viewCamera,XRNode.LeftHand,out lp,out lq)
                && FishingXRTracking.TryPose(_c.viewCamera,XRNode.RightHand,out _,out _);
            if(!valid || _c.DockConsole.BlocksGameplay){Recover();return;}
            bool lg=Grip(left),rg=Grip(right);
            bool grabNet=_left.Step(true,lg),grabFish=_right.Step(true,rg);
            if(grabNet && _c.Anchored && (_c.Session.Phase==CatchPhase.Landing || _c.HasPendingCatch) && Vector3.Distance(lp,_c.LandingNet.position)<.35f)_netHeld=true;
            if(!lg)_netHeld=false;
            if(_netHeld)
            {
                // The pole's +X axis points along the controller's forward axis.
                var rotation=lq*Quaternion.Euler(0,-90,0);
                var center=lp+rotation*new Vector3(FishingDetailArt.NetMouthOffset,0,0);
                ApplyNetPose(center,rotation,dt);
                TryTipIntoBox(center,rotation,dt);
            }
            FishingXRTracking.TryPose(_c.viewCamera,XRNode.RightHand,out var rp,out var rq);
            if(_c.HasPendingCatch && grabFish && Vector3.Distance(rp,_c.ActiveFish.position)<.35f){Location=Place.Hand;_c.MeasureCatch();}
            if(Location==Place.Hand && !rg)
            {
                if(Vector3.Distance(rp,_c.FishBox.position+Vector3.up*.4f)<.42f){if(!_c.ResolveCatch(true))Location=Place.Tray;}
                else Location=Place.Tray; // Never auto-release a fish because tracking or grip was lost.
            }
        }
    }
    public static class FishingXRTracking
    {
        public static bool TryPose(Camera camera,XRNode node,out Vector3 position,out Quaternion rotation,bool aim=false)
        {
            position=Vector3.zero;rotation=Quaternion.identity;
            if(camera==null)return false;
            var head=InputDevices.GetDeviceAtXRNode(XRNode.Head);var hand=InputDevices.GetDeviceAtXRNode(node);
            if(!Read(head,out var hp,out var hq) || !Read(hand,out var p,out var q))return false;
            if(aim && hand.TryGetFeatureValue(new InputFeatureUsage<Vector3>("PointerPosition"),out var ap) && hand.TryGetFeatureValue(new InputFeatureUsage<Quaternion>("PointerRotation"),out var aq))
            {if(!Finite(ap.x)||!Finite(ap.y)||!Finite(ap.z)||!Finite(aq.x)||!Finite(aq.y)||!Finite(aq.z)||!Finite(aq.w))return false;p=ap;q=aq;}
            var basis=camera.transform.rotation*Quaternion.Inverse(hq);
            position=camera.transform.position+basis*(p-hp);rotation=basis*q;return true;
        }
        private static bool Read(InputDevice d,out Vector3 p,out Quaternion q)
        {
            p=Vector3.zero;q=Quaternion.identity;
            if(!d.isValid || !d.TryGetFeatureValue(CommonUsages.isTracked,out bool tracked) || !tracked || !d.TryGetFeatureValue(CommonUsages.devicePosition,out p) || !d.TryGetFeatureValue(CommonUsages.deviceRotation,out q))return false;
            return Finite(p.x)&&Finite(p.y)&&Finite(p.z)&&Finite(q.x)&&Finite(q.y)&&Finite(q.z)&&Finite(q.w)&&Quaternion.Dot(q,q)>.5f;
        }
        private static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
    }
}
