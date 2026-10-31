using UnityEngine;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    /// <summary>Input/goal bridge only. BeachCharacter is the sole arm/hand mesh source.</summary>
    public sealed class FishingHands : MonoBehaviour
    {
        private FishingPrototype _c;
        private Transform _rod,_crank,_net,_knob;
        private FishingHandTarget _left,_right;
        private float _gesture,_hook;
        private readonly FishingCastGesture _castGesture=new FishingCastGesture();
        private readonly FishingPressEdge _stow=new FishingPressEdge();
        private Vector3 _lastCastPosition;private bool _castSample;
        private int _presentationFrame=-1;
        private FishBeat _lastCue;
        private float _nextTensionPulse;
        private readonly FishingHandInput _input=new FishingHandInput();
        private GameObject _pickup;
        private float _pickupTime;
        private Vector3 _gestureTarget;
        private float _xrSteer,_xrNeutralX;
        private bool _xrFight;
        // Grip-space mount calibration, independently mirrored. These are NOT camera poses.
        // Source wrist axes are calibrated separately from actual knuckles by FishingCharacter.
        [SerializeField] private Vector3 _leftControllerPalmOffset=new Vector3(-.025f,0,-.02f);
        [SerializeField] private Vector3 _rightControllerPalmOffset=new Vector3(.025f,0,-.02f);
        [SerializeField] private Vector3 _leftControllerPalmEuler=new Vector3(0,0,90);
        [SerializeField] private Vector3 _rightControllerPalmEuler=new Vector3(0,0,-90);
        private static readonly Vector3 RodPalm=new Vector3(.058f,.012f,-.045f);
        private static readonly Quaternion RodPalmRotation=Quaternion.LookRotation(Vector3.down,Vector3.right);
        public static readonly Vector3 XRRestRodPosition=new Vector3(.32f,1.1f,.4f);
        private bool _buttonRod;
        public bool HoldingRod=>_buttonRod || _input.Holding;
        public bool HoldRodForButtons()
        {
            if(_c==null || !_c.Anchored || _c.HasPendingCatch || _c.Paused || _c.StoryBlocksGameplay || _c.XRNotebook.IsOpen)return false;
            bool wasHolding=HoldingRod;
            if(!_input.Holding)_buttonRod=true;
            if(!wasHolding)RodHelp();return true;
        }
        public bool XRReeling=>_input.Reeling || _stickReel>0 || _triggerReel;
        private bool _triggerReel;
        private float _stickAngle=float.NaN,_stickReel;
        /// <summary>Right-stick circling reels like the crank: degrees swept this frame, ignoring centre jitter.</summary>
        public static float StickSweep(Vector2 stick,ref float lastAngle)
        {
            if(stick.magnitude<.6f){lastAngle=float.NaN;return 0;}
            float angle=Mathf.Atan2(stick.y,stick.x)*Mathf.Rad2Deg;float sweep=float.IsNaN(lastAngle)?0:Mathf.Abs(Mathf.DeltaAngle(lastAngle,angle));
            lastAngle=angle;return sweep;
        }
        public float XRSteer=>_xrSteer;
        public FishingHandTarget Left=>_left;
        public FishingHandTarget Right=>_right;
        public Transform Rod=>_rod;
        public Transform Knob=>_knob;
        public Transform Crank=>_crank;
        public Transform Net=>_net;
        public bool HasGesture=>_gesture>0;
        public bool PresentingPickup=>_pickup!=null && _pickup.activeSelf;
        public bool TrackedPresentation { get; private set; }
        // An explicitly isolated desktop capture fixture may have no focused Game view. Real
        // desktop/XR sessions still release every goal on focus loss; XR never bypasses it.
        private bool CanPresent=>_c!=null && !_c.Paused && (Application.isFocused
            || (_c.HasIsolatedProfile && _c.Automated && !XRSettings.isDeviceActive));
        public void Build(FishingPrototype c,Transform tip,Transform crank,Transform net)
        {
            _c=c;_rod=tip.parent;_crank=crank;_net=net;
            _left=Create("Left independent palm goal",true);_right=Create("Right independent palm goal",false);
            _knob=new GameObject("Left hand reel attachment").transform;_knob.SetParent(_crank,false);_knob.localPosition=new Vector3(-.085f,-.06f,0);
        }
        private FishingHandTarget Create(string label,bool left)
        {
            var go=new GameObject(label);go.transform.SetParent(transform,false);
            var goal=go.AddComponent<FishingHandTarget>();goal.Build(left);return goal;
        }
        public void PresentPickup()
        {
            _c.Shore.Character.PlayInteraction();
            // The item owns its pose, never follows an IK output wrist.
            if(_pickup==null)_pickup=FishingProps.Spawn("RepairCoil",transform,Vector3.zero);
            _pickup.transform.localScale=Vector3.one*.55f;_pickup.SetActive(true);_pickupTime=1.1f;
        }
        public void ResetInteraction()
        {
            _buttonRod=false;
            _input.Update(false,0,false,false,0,false,0);_triggerReel=false;
            if(_left!=null)_left.Disable();if(_right!=null)_right.Disable();
            _castGesture.Reset();_castSample=false;_xrSteer=0;_xrFight=false;
            _stow.Reset();_stickReel=0;_stickAngle=float.NaN;_biteKnock=-1;
            _c?.CastMotion.CancelPreparation();
            _gesture=_hook=_pickupTime=0;if(_pickup!=null)_pickup.SetActive(false);
        }
        public void ReadXRTools()
        {
            bool available=XRSettings.isDeviceActive && _c.Anchored && !_c.Paused && !_c.HasPendingCatch
                && !_c.XRSeat.HeadBlocked && !_c.XRNotebook.BlocksGameplay && !_c.StoryBlocksGameplay
                && Application.isFocused;
            if(!available)
            {
                _buttonRod=false;_input.Update(false,0,false,false,0,false,0);_stickReel=0;_triggerReel=false;_stickAngle=float.NaN;
                _castGesture.Reset();_castSample=false;_xrSteer=0;_xrFight=false;_stow.Reset();_biteKnock=-1;return;
            }
            // Read fresh controller poses, not previous-frame palm targets or solved bones.
            bool rightTracked=FishingXRTracking.TryPose(_c.viewCamera,XRNode.RightHand,out var rp,out var rq);
            bool leftTracked=FishingXRTracking.TryPose(_c.viewCamera,XRNode.LeftHand,out var lp,out _);
            available=rightTracked && leftTracked;
            if(!available){ResetInteraction();return;}
            var right=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);var left=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if(_stow.Step(true,FishingXRInput.StickClick(right))){_c.LeaveStation();return;}
            if(_c.Session.Phase==CatchPhase.Landing){_input.Update(false,0,false,false,0,false,0);_castGesture.Reset();_castSample=false;_triggerReel=false;_stickReel=0;return;}
            if(!right.TryGetFeatureValue(CommonUsages.grip,out float grip))grip=FishingXRInput.Grip(right)?1:0;
            if(!left.TryGetFeatureValue(CommonUsages.grip,out float leftGrip))leftGrip=FishingXRInput.Grip(left)?1:0;
            bool trigger=FishingXRInput.Trigger(right);
            var local=_rod.InverseTransformPoint(lp)-_crank.localPosition;
            float angle=Mathf.Atan2(local.z,-local.y)*Mathf.Rad2Deg;bool wasHolding=HoldingRod;
            _input.Update(available,grip,trigger,Vector3.Distance(rp,_rod.position)<.24f,leftGrip,Vector3.Distance(lp,_knob.position)<.16f,angle,_buttonRod);
            if(_input.Holding)_buttonRod=false; // A physical grip takes ownership again, so releasing it still stows the rod.
            if(_input.Reeling)_crank.localRotation=Quaternion.AngleAxis(-angle,Vector3.right);
            right.TryGetFeatureValue(CommonUsages.primary2DAxis,out var stick);
            // A circling stick keeps reeling ~0.25 s past each sweep so a steady rotation reads as continuous winding.
            float sweep=StickSweep(stick,ref _stickAngle);_stickReel=sweep>2?.25f:Mathf.Max(0,_stickReel-Time.unscaledDeltaTime);
            if(_stickReel>0)_crank.localRotation*=Quaternion.AngleAxis(-sweep,Vector3.right);
            // Simplest reel: hold the rod-hand trigger while fighting (crank and stick circles still work).
            _triggerReel=trigger && HoldingRod && _c.Session.Phase==CatchPhase.Fighting;
            if(_triggerReel)_crank.localRotation*=Quaternion.AngleAxis(-360*Time.deltaTime,Vector3.right);
            if(HoldingRod && !wasHolding){Haptic(right,.22f);RodHelp();}
            if(HoldingRod && _c.Session.Phase==CatchPhase.Fighting && (_c.Session.Beat!=_lastCue || Time.unscaledTime>=_nextTensionPulse))
            {_lastCue=_c.Session.Beat;_nextTensionPulse=Time.unscaledTime+.18f;Haptic(right,.06f+.4f*_c.Session.Tension);}
            var localPosition=_c.XRSeat.Origin.transform.InverseTransformPoint(rp);
            bool fight=HoldingRod && _c.Session.Phase==CatchPhase.Fighting;
            var controlRight=_c.XRSeat.Origin.transform.InverseTransformDirection(_c.Angling.CastRight);
            float lateral=Vector3.Dot(localPosition,controlRight);
            if(fight && !_xrFight)_xrNeutralX=lateral;
            _xrSteer=fight?Mathf.Clamp((lateral-_xrNeutralX)/.18f,-1,1):0;
            if(Mathf.Abs(_xrSteer)<.18f)_xrSteer=0;
            _xrFight=fight;
            if(_biteKnock>0 && Time.unscaledTime>=_biteKnock){_biteKnock=-1;Haptic(right,.55f);}
            float dt=Time.unscaledDeltaTime;
            var forward=_c.XRSeat.Origin.transform.InverseTransformDirection(_c.viewCamera.transform.forward);
            // A real cast is a wrist flick: track a point 0.6 m up the rod, which moves fast even when the hand barely does.
            var tip=_c.XRSeat.Origin.transform.InverseTransformPoint(rp+rq*Vector3.forward*.6f);
            float speed=_castSample && dt>.001f?Vector3.Dot((tip-_lastCastPosition)/dt,forward):0;
            bool cast=_castGesture.Step(HoldingRod && _castSample && _c.CanChangeBait,trigger,speed,dt);
            _lastCastPosition=tip;_castSample=true;
            bool action=cast?_c.Cast(_castGesture.Power):_input.Activated && _c.Session.Phase==CatchPhase.Bite && _c.Hook();
            if(action)Haptic(right,.3f);
        }
        private void RodHelp()=>_c.Notice("鱼竿已拿起 · 按住右扳机蓄力，松开抛竿；按下右摇杆收竿起航。",6);
        /// <summary>Two short knocks on the rod hand when a fish bites.</summary>
        public void BiteHaptic(){var right=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);Haptic(right,.55f);_biteKnock=Time.unscaledTime+.12f;}
        private float _biteKnock=-1;
        private static void Haptic(InputDevice device,float strength)
        {if(device.TryGetHapticCapabilities(out var capabilities) && capabilities.supportsImpulse)FishingXRInput.Pulse(device,strength,.04f);}
        public void ReachGesture(Vector3 target){_gesture=.55f;_gestureTarget=target;}
        public void HookGesture(){_hook=.32f;}
        /// <summary>Shoulder-centre origin in BOTH desktop views; TP camera position never enters tool placement.</summary>
        public void GetToolFrame(out Vector3 origin,out Quaternion rotation)
        {
            var character=_c.Shore.Character;
            var reference=character!=null?character.transform:_c.Shore.Actor;
            origin=character!=null?character.HandReferencePosition:reference.position+Vector3.up*1.3f;
            rotation=Quaternion.Euler(0,reference.eulerAngles.y,0);
            if(_c.FirstPerson && !XRSettings.isDeviceActive)
            {
                // Look may steer a desktop grip, but cannot pull arms behind the shoulders.
                var local=Quaternion.Inverse(rotation)*_c.viewCamera.transform.forward;
                float yaw=Mathf.Clamp(Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg,-65,65);
                float pitch=Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(local.y,-1,1))*Mathf.Rad2Deg,-50,65);
                rotation*=Quaternion.Euler(pitch,yaw,0);
            }
        }
        public void RestHand(bool left)
        {
            var hand=left?_left:_right;if(!CanPresent){hand.Disable();return;}
            var character=_c.Shore.Character;
            var restFrame=Quaternion.Euler(0,character.transform.eulerAngles.y,0);
            float drop=Mathf.Min(.28f,character.ArmReach(left)*.7f);
            hand.SetPose(character.ShoulderPosition(left)+restFrame*new Vector3(character.HandSide(left)*.035f,-drop,.07f),restFrame,.15f);
        }
        public bool CanPresentTools=>CanPresent;
        public bool CanReach(bool left,Vector3 point,float reserve=.02f)
        {
            var character=_c.Shore.Character;
            return character!=null && character.HandRigReady && Vector3.Distance(character.ShoulderPosition(left),point)<=Mathf.Max(.01f,character.ArmReach(left)-reserve);
        }
        /// <summary>只平移独立物体来满足双手可达域，不读取解算后的腕骨反推工具。</summary>
        public bool FitToolPose(Transform tool,Vector3 leftPalm,Vector3 rightPalm,Quaternion leftRotation,Quaternion rightRotation,bool useLeft=true,bool useRight=true)
        {
            if(XRSettings.isDeviceActive)return true;
            var character=_c.Shore.Character;if(character==null || !character.HandRigReady)return false;
            for(int pass=0;pass<8;pass++)
            {
                if(useLeft)FitOne(true,leftPalm);if(useRight)FitOne(false,rightPalm);
            }
            return (!useLeft || WristReach(true,leftPalm,leftRotation)) && (!useRight || WristReach(false,rightPalm,rightRotation));
            bool WristReach(bool left,Vector3 local,Quaternion rotation)=>Vector3.Distance(character.ShoulderPosition(left),character.WristForPalm(left,tool.TransformPoint(local),tool.rotation*rotation))<=character.ArmReach(left)-.005f
                && Vector3.Distance(character.ShoulderPosition(left),tool.TransformPoint(local))<=character.ArmReach(left)-.005f;
            void FitOne(bool left,Vector3 local)
            {
                var shoulder=character.ShoulderPosition(left);var rotation=left?leftRotation:rightRotation;
                var delta=character.WristForPalm(left,tool.TransformPoint(local),tool.rotation*rotation)-shoulder;
                tool.position+=Vector3.ClampMagnitude(delta,Mathf.Max(.01f,character.ArmReach(left)-.025f))-delta;
                var palmDelta=tool.TransformPoint(local)-shoulder;
                tool.position+=Vector3.ClampMagnitude(palmDelta,Mathf.Max(.01f,character.ArmReach(left)-.025f))-palmDelta;
            }
        }
        /// <summary>Call AFTER Apply and AFTER positioning the tool, BEFORE final character IK.</summary>
        public void SetToolTarget(bool left,Transform tool,Vector3 localPalmPosition,Quaternion localPalmRotation,float curl,bool pointing=false,FishingFingerPose? fingers=null)
        {
            var target=left?_left:_right;
            if(!CanPresent){target.Disable();return;}
            target.SetFromGrip(tool,localPalmPosition,localPalmRotation,curl,pointing);
            if(fingers.HasValue)target.SetFingers(fingers.Value);
        }
        public void Apply()
        {
            if(_c==null)return;
            // Clear every owner every frame. Field/Catch must explicitly resupply overrides.
            _left.BeginFrame();_right.BeginFrame();
            bool xr=XRSettings.isDeviceActive;TrackedPresentation=xr;
            // 桌面采用紧凑钓具比例，第一/第三人称共用；XR 保持原设备挂载尺寸。
            _rod.localScale=Vector3.one*(xr?1f:.68f);
            float step=_presentationFrame==Time.frameCount?0:Time.deltaTime;_presentationFrame=Time.frameCount;
            _pickupTime=Mathf.Max(0,_pickupTime-step);
            if(_pickup!=null)_pickup.SetActive(_pickupTime>0 && _c.Ashore && CanPresent && !xr);
            if(!CanPresent){ResetInteraction();StowRod();return;}
            _gesture=Mathf.Max(0,_gesture-step);_hook=Mathf.Max(0,_hook-step);
            if(xr)
            {
                bool leftTracked=Track(_left,XRNode.LeftHand);
                bool rightTracked=Track(_right,XRNode.RightHand);
                if(!leftTracked || !rightTracked)_input.Update(false,0,false,false,0,false,0);
                StowRod();
                if((HoldingRod || _c.Session.Phase==CatchPhase.Landing) && rightTracked && _c.Anchored && !_c.HasPendingCatch)
                {
                    _rod.gameObject.SetActive(true);
                    // The rod's +Z follows the tracked grip's +Z; the desktop knuckle frame would pitch it back by 90 degrees.
                    var palmRotation=Quaternion.Euler(_rightControllerPalmEuler);
                    var rotation=_right.Palm.rotation*Quaternion.Inverse(palmRotation);
                    _rod.SetPositionAndRotation(_right.Palm.position-rotation*RodPalm,rotation);
                    _right.SetFromGrip(_rod,RodPalm,palmRotation,.86f);
                    RodGripSurface();
                }
                return;
            }
            // Sleep owns the desktop resting arms; tracked controllers above retain their physical poses.
            if(_c.Home.Sleeping || _c.Home.Lounging){StowRod();return;}
            GetToolFrame(out var origin,out var frame);
            bool inspection=_c.CatchRig!=null && _c.CatchRig.LeftHandInspecting && !_c.CatchRig.PlacingInNet;
            bool fishing=_c.Anchored && !_c.Ashore && (!_c.HasPendingCatch || inspection) && !_c.InspectingItem;

            if(fishing)
            {
                _rod.gameObject.SetActive(true);
                // 同源双手保持在可见的胸前操作区；俯视只改变竿的瞄准，不把整个握持位置压出画面。
                var holdFrame=Quaternion.Euler(0,frame.eulerAngles.y,0);
                bool fighting=_c.Session.Phase==CatchPhase.Fighting;
                float pull=fighting?Mathf.Lerp(3,12,_c.Session.Tension):0;
                _rod.SetPositionAndRotation(origin+holdFrame*new Vector3(.17f,-.20f,.45f),holdFrame*Quaternion.Euler(_c.CastMotion.RodPitch+16-pull-Mathf.Sin(_hook/.32f*Mathf.PI)*18,5f+_c.RodControl*13,0));
                // The knob spins freely on its axle: fingers wrap across it, without rolling the wrist with the crank.
                var reelPalm=_rod.InverseTransformPoint(_knob.position)+new Vector3(-.012f,.022f,-.018f);
                bool landing=_c.Session.Phase==CatchPhase.Landing;
                FitToolPose(_rod,reelPalm,RodPalm,Quaternion.LookRotation(Vector3.right,Vector3.up),RodPalmRotation,!inspection,true);
                _right.SetFromGrip(_rod,RodPalm,RodPalmRotation,.86f);
                RodGripSurface();
                if(!landing && !inspection)
                {
                    _left.SetFromGrip(_rod,reelPalm,Quaternion.LookRotation(Vector3.right,Vector3.up),.6f);
                    _left.SetFingers(new FishingFingerPose(.55f,.64f,.66f,.62f,.72f));
                    _left.SetGripSurface(_rod.TransformPoint(reelPalm+new Vector3(-.025f,-.022f,.018f)),_rod.TransformPoint(reelPalm+new Vector3(.025f,-.022f,.018f)),.017f*_rod.lossyScale.x);
                }
            }
            else
            {
                StowRod();
                // TP locomotion keeps the source arm swing unless an actual tool/gesture owns it.
                if(!_c.Ashore || (_c.FirstPerson && _c.Shore.WalkSpeed<.1f && !_c.Shore.Swimming && !_c.Shore.Climbing))
                {
                    _left.SetPose(origin+frame*new Vector3(-.22f,-.32f,.22f),frame*Quaternion.Euler(10,-12,-12),.15f);
                    _right.SetPose(origin+frame*new Vector3(.22f,-.32f,.22f),frame*Quaternion.Euler(10,12,12),.15f);
                }
                if(_gesture>0)
                {
                    float reach=Mathf.Sin((1-_gesture/.55f)*Mathf.PI);
                    var shoulder=_c.Shore.Character.ShoulderPosition(false);
                    var desired=Vector3.Lerp(origin+frame*new Vector3(.22f,-.25f,.25f),_gestureTarget,reach);
                    desired=shoulder+Vector3.ClampMagnitude(desired-shoulder,_c.Shore.Character.ArmReach(false));
                    var direction=_gestureTarget-shoulder;
                    var rotation=direction.sqrMagnitude>.0001f?Quaternion.LookRotation(direction.normalized,frame*Vector3.up):frame;
                    _right.SetPose(desired,rotation,.3f,true);
                }
            }
            if(_pickup!=null && _pickup.activeSelf)
            {
                _pickup.transform.SetPositionAndRotation(origin+frame*new Vector3(.18f,-.2f,.31f),frame);
                _right.SetFromGrip(_pickup.transform,new Vector3(.02f,-.045f,-.045f),Quaternion.identity,.65f);
            }
        }
        private void RodGripSurface()
        {
            _right.SetGripSurface(_rod.TransformPoint(new Vector3(0,0,-.10f)),_rod.TransformPoint(new Vector3(0,0,.04f)),.025f*_rod.lossyScale.x);
            _right.SetForearmRoll(.65f);
        }
        private void StowRod()
        {
            bool xr=XRSettings.isDeviceActive;
            _rod.gameObject.SetActive(xr && _c.Anchored && !_c.Ashore && !_c.HasPendingCatch);
            if(xr && _c.XRSeat.Origin!=null)
            {
                var origin=_c.XRSeat.Origin.transform;
                _rod.SetPositionAndRotation(origin.TransformPoint(XRRestRodPosition),origin.rotation*Quaternion.LookRotation(new Vector3(.25f,1,.2f)));return;
            }
            _rod.localPosition=new Vector3(.8f,.72f,.25f);
            _rod.localRotation=Quaternion.LookRotation(new Vector3(.25f,1,.2f));
        }
        public bool TryControllerPalm(bool left,out Vector3 position,out Quaternion rotation)
        {
            position=Vector3.zero;rotation=Quaternion.identity;
            if(_c==null)return false;
            if(!FishingXRTracking.TryPose(_c.viewCamera,left?XRNode.LeftHand:XRNode.RightHand,out position,out rotation))return false;
            position+=rotation*(left?_leftControllerPalmOffset:_rightControllerPalmOffset);
            var bodyFrame=Quaternion.Euler(0,(_c.Ashore?_c.Shore.Actor:_c.Player.transform).eulerAngles.y,0);
            rotation=AlignControllerRotation(left,rotation,bodyFrame)*Quaternion.Euler(left?_leftControllerPalmEuler:_rightControllerPalmEuler);return true;
        }
        // Side tilt in the character's XY plane, independent of the controller pointing direction.
        public static float ControllerRoll(bool left)
        {
            float value=PlayerPrefs.GetFloat(left?"FishingXR.LeftPalmXY":"FishingXR.RightPalmXY",30f);
            return float.IsNaN(value)||float.IsInfinity(value)?30f:Mathf.Clamp(value,-90,90);
        }
        public static void SetControllerRoll(bool left,float degrees)
        {
            if(float.IsNaN(degrees)||float.IsInfinity(degrees))return;
            PlayerPrefs.SetFloat(left?"FishingXR.LeftPalmXY":"FishingXR.RightPalmXY",Mathf.Clamp(degrees,-90,90));PlayerPrefs.Save();
        }
        public static Quaternion AlignControllerRotation(bool left,Quaternion tracked,Quaternion characterFrame)
            =>Quaternion.AngleAxis(ControllerRoll(left),characterFrame*Vector3.forward)*tracked;
        public Quaternion ControllerGripRotation(Quaternion palmRotation)=>palmRotation*Quaternion.Inverse(Quaternion.Euler(_rightControllerPalmEuler));
        private bool Track(FishingHandTarget hand,XRNode node)
        {
            if(!TryControllerPalm(node==XRNode.LeftHand,out var palm,out var palmFrame)){hand.Disable();return false;}
            var device=InputDevices.GetDeviceAtXRNode(node);device.TryGetFeatureValue(CommonUsages.grip,out float grip);
            hand.SetPose(palm,palmFrame,grip);
            return hand.Visible;
        }
        private void OnApplicationFocus(bool focus){if(!focus)ResetInteraction();}
        private void OnApplicationPause(bool pause){if(pause)ResetInteraction();}
        private void OnDisable()=>ResetInteraction();
    }
}
