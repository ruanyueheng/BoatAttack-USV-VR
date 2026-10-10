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
        private float _gesture,_cast;
        private int _presentationFrame=-1;
        private FishBeat _lastCue;
        private readonly FishingHandInput _input=new FishingHandInput();
        private GameObject _pickup;
        private float _pickupTime;
        private Vector3 _gestureTarget;
        // Grip-space mount calibration, independently mirrored. These are NOT camera poses.
        // Source wrist axes are calibrated separately from actual knuckles by FishingCharacter.
        [SerializeField] private Vector3 _leftControllerPalmOffset=new Vector3(-.025f,0,-.02f);
        [SerializeField] private Vector3 _rightControllerPalmOffset=new Vector3(.025f,0,-.02f);
        [SerializeField] private Vector3 _leftControllerPalmEuler=new Vector3(-90,90,0);
        [SerializeField] private Vector3 _rightControllerPalmEuler=new Vector3(-90,-90,0);
        private static readonly Vector3 RodPalm=new Vector3(.058f,.012f,-.045f);
        private static readonly Quaternion RodPalmRotation=Quaternion.LookRotation(Vector3.up,Vector3.right);
        public bool HoldingRod=>_input.Holding;
        public bool XRReeling=>_input.Reeling;
        public FishingHandTarget Left=>_left;
        public FishingHandTarget Right=>_right;
        public Transform Rod=>_rod;
        public Transform Knob=>_knob;
        public Transform Net=>_net;
        public bool HasGesture=>_gesture>0;
        public bool PresentingPickup=>_pickup!=null && _pickup.activeSelf;
        public bool TrackedPresentation { get; private set; }
        // An explicitly isolated desktop batch fixture has no focused Game view. Real
        // desktop/XR sessions still release every goal on focus loss; XR never bypasses it.
        private bool CanPresent=>_c!=null && !_c.Paused && (Application.isFocused
            || (Application.isBatchMode && _c.HasIsolatedProfile && _c.Automated && !XRSettings.isDeviceActive));
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
            // The item owns its pose, never follows an IK output wrist.
            if(_pickup==null)_pickup=FishingProps.Spawn("RepairCoil",transform,Vector3.zero);
            _pickup.transform.localScale=Vector3.one*.55f;_pickup.SetActive(true);_pickupTime=1.1f;
        }
        public void ResetInteraction()
        {
            _input.Update(false,0,false,false,0,false,0);
            if(_left!=null)_left.Disable();if(_right!=null)_right.Disable();
            _gesture=_cast=_pickupTime=0;if(_pickup!=null)_pickup.SetActive(false);
        }
        public void ReadXRTools()
        {
            bool available=XRSettings.isDeviceActive && _c.Anchored && !_c.Paused && !_c.HasPendingCatch
                && _c.Session.Phase!=CatchPhase.Landing && Application.isFocused;
            if(!available){_input.Update(false,0,false,false,0,false,0);return;}
            // Read fresh controller poses, not previous-frame palm targets or solved bones.
            bool rightTracked=FishingXRTracking.TryPose(_c.viewCamera,XRNode.RightHand,out var rp,out _);
            bool leftTracked=FishingXRTracking.TryPose(_c.viewCamera,XRNode.LeftHand,out var lp,out _);
            available=rightTracked && leftTracked;
            if(!available){ResetInteraction();return;}
            var right=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);var left=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            right.TryGetFeatureValue(CommonUsages.grip,out float grip);right.TryGetFeatureValue(CommonUsages.triggerButton,out bool trigger);
            left.TryGetFeatureValue(CommonUsages.grip,out float leftGrip);
            var local=_rod.InverseTransformPoint(lp)-_crank.localPosition;
            float angle=Mathf.Atan2(local.z,-local.y)*Mathf.Rad2Deg;bool wasHolding=HoldingRod;
            _input.Update(available,grip,trigger,Vector3.Distance(rp,_rod.position)<.24f,leftGrip,Vector3.Distance(lp,_knob.position)<.16f,angle);
            if(HoldingRod && !wasHolding)Haptic(right,.22f);
            if(HoldingRod && _c.Session.Phase==CatchPhase.Fighting && _c.Session.Beat!=_lastCue)
            {_lastCue=_c.Session.Beat;Haptic(right,_lastCue==FishBeat.Leap?.3f:.12f);}
            if(_input.Activated)
            {
                bool action=_c.Session.Phase==CatchPhase.Bite?_c.Hook():_c.Session.Phase==CatchPhase.Landing?_c.Scoop():_c.Cast();
                if(action)Haptic(right,.3f);
            }
        }
        private static void Haptic(InputDevice device,float strength)
        {if(device.TryGetHapticCapabilities(out var capabilities) && capabilities.supportsImpulse)device.SendHapticImpulse(0,strength,.04f);}
        public void CastGesture(){_cast=.45f;}
        public void ReachGesture(Vector3 target){_gesture=.55f;_gestureTarget=target;}
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
            GetToolFrame(out _,out var frame);var character=_c.Shore.Character;
            float drop=Mathf.Min(.28f,character.ArmReach(left)*.7f);
            hand.SetPose(character.ShoulderPosition(left)+frame*new Vector3(character.HandSide(left)*.035f,-drop,.07f),frame,.15f);
        }
        public bool CanPresentTools=>CanPresent;
        public bool CanReach(bool left,Vector3 point,float reserve=.02f)
        {
            var character=_c.Shore.Character;
            return character!=null && character.HandRigReady && Vector3.Distance(character.ShoulderPosition(left),point)<=Mathf.Max(.01f,character.ArmReach(left)-reserve);
        }
        /// <summary>只平移独立物体来满足双手可达域，不读取解算后的腕骨反推工具。</summary>
        public bool FitToolPose(Transform tool,Vector3 leftPalm,Vector3 rightPalm,bool useLeft=true,bool useRight=true)
        {
            if(XRSettings.isDeviceActive)return true;
            var character=_c.Shore.Character;if(character==null || !character.HandRigReady)return false;
            for(int pass=0;pass<8;pass++)
            {
                if(useLeft)FitOne(true,leftPalm);if(useRight)FitOne(false,rightPalm);
            }
            return (!useLeft || CanReach(true,tool.TransformPoint(leftPalm),.005f)) && (!useRight || CanReach(false,tool.TransformPoint(rightPalm),.005f));
            void FitOne(bool left,Vector3 local)
            {
                var shoulder=character.ShoulderPosition(left);var delta=tool.TransformPoint(local)-shoulder;
                tool.position+=Vector3.ClampMagnitude(delta,Mathf.Max(.01f,character.ArmReach(left)-.025f))-delta;
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
            _rod.localScale=Vector3.one*(xr?1f:.75f);
            float step=_presentationFrame==Time.frameCount?0:Time.deltaTime;_presentationFrame=Time.frameCount;
            _pickupTime=Mathf.Max(0,_pickupTime-step);
            if(_pickup!=null)_pickup.SetActive(_pickupTime>0 && _c.Ashore && CanPresent && !xr);
            if(!CanPresent){ResetInteraction();StowRod();return;}
            _gesture=Mathf.Max(0,_gesture-step);_cast=Mathf.Max(0,_cast-step);
            if(xr)
            {
                bool leftTracked=Track(_left,XRNode.LeftHand,out _,out _);
                bool rightTracked=Track(_right,XRNode.RightHand,out var rp,out var rq);
                if(!leftTracked || !rightTracked)_input.Update(false,0,false,false,0,false,0);
                StowRod();
                if(HoldingRod && rightTracked)
                {
                    var rotation=rq*Quaternion.Euler(0,90,0);
                    _rod.SetPositionAndRotation(rp-rotation*RodPalm,rotation);
                    _right.SetFromGrip(_rod,RodPalm,RodPalmRotation,.86f);
                }
                return;
            }
            GetToolFrame(out var origin,out var frame);
            bool fishing=_c.Anchored && !_c.Ashore && !_c.HasPendingCatch && _c.Session.Phase!=CatchPhase.Landing;
            if(fishing)
            {
                _rod.gameObject.SetActive(true);
                // 同源双手保持在可见的胸前操作区；俯视只改变竿的瞄准，不把整个握持位置压出画面。
                var holdFrame=Quaternion.Euler(0,frame.eulerAngles.y,0);
                _rod.SetPositionAndRotation(origin+holdFrame*new Vector3(.10f,-.105f,.41f),holdFrame*Quaternion.Euler(-32f-Mathf.Sin(_cast/.45f*Mathf.PI)*24f,-12f,0));
                var reelPalm=_rod.InverseTransformPoint(_knob.position+_rod.TransformVector(new Vector3(-.024f,.012f,-.035f)));
                FitToolPose(_rod,reelPalm,RodPalm);
                _right.SetFromGrip(_rod,RodPalm,RodPalmRotation,.86f);
                _left.SetFromGrip(_rod,reelPalm,Quaternion.identity,.85f);
            }
            else
            {
                StowRod();
                // TP locomotion keeps the source arm swing unless an actual tool/gesture owns it.
                if(!_c.Ashore || (_c.FirstPerson && _c.Shore.WalkSpeed<.1f))
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
        private void StowRod()
        {
            _rod.gameObject.SetActive(!_c.Ashore);
            _rod.localPosition=XRSettings.isDeviceActive && _c.Anchored?new Vector3(.48f,1.45f,.1f):new Vector3(.75f,.95f,.9f);
            _rod.localRotation=Quaternion.LookRotation(new Vector3(1.9f,1.7f,1.35f));
        }
        private bool Track(FishingHandTarget hand,XRNode node,out Vector3 position,out Quaternion rotation)
        {
            position=Vector3.zero;rotation=Quaternion.identity;
            if(!FishingXRTracking.TryPose(_c.viewCamera,node,out position,out rotation)){hand.Disable();return false;}
            var device=InputDevices.GetDeviceAtXRNode(node);device.TryGetFeatureValue(CommonUsages.grip,out float grip);
            bool left=node==XRNode.LeftHand;
            hand.SetPose(position+rotation*(left?_leftControllerPalmOffset:_rightControllerPalmOffset),
                rotation*Quaternion.Euler(left?_leftControllerPalmEuler:_rightControllerPalmEuler),grip);
            return hand.Visible;
        }
        private void OnApplicationFocus(bool focus){if(!focus)ResetInteraction();}
        private void OnApplicationPause(bool pause){if(pause)ResetInteraction();}
        private void OnDisable()=>ResetInteraction();
    }
}
