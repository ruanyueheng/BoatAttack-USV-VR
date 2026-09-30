using UnityEngine;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    /// <summary>Desktop tool attachment poses; XR hands use tracking, never a camera-relative fake pose.</summary>
    public sealed class FishingHands : MonoBehaviour
    {
        private FishingPrototype _c;
        private Transform _rod,_crank,_net,_knob;
        private FishingGlove _left,_right;
        private float _gesture,_cast;
        private readonly FishingHandInput _input=new FishingHandInput();
        public bool HoldingRod => _input.Holding;
        public bool XRReeling => _input.Reeling;
        private GameObject _pickup;
        private float _pickupTime;
        public void PresentPickup()
        {
            if(_pickup==null)_pickup=FishingProps.Spawn("RepairCoil",_right.transform,new Vector3(0,-.045f,.075f));
            _pickup.transform.localScale=Vector3.one*.55f;_pickup.SetActive(true);_pickupTime=1.1f;
        }
        public void ReadXRTools()
        {
            bool available=XRSettings.isDeviceActive && _c.Anchored && !_c.Paused && Application.isFocused;
            if(!available){_input.Update(false,0,false,false,0,false,0);return;}
            var right=InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            var left=InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            available=available && Tracked(right) && Tracked(left);
            right.TryGetFeatureValue(CommonUsages.grip,out float grip);
            right.TryGetFeatureValue(CommonUsages.triggerButton,out bool trigger);
            left.TryGetFeatureValue(CommonUsages.grip,out float leftGrip);
            var local=_rod.InverseTransformPoint(_left.transform.position)-_crank.localPosition;
            float angle=Mathf.Atan2(local.z,-local.y)*Mathf.Rad2Deg;
            bool wasHolding=HoldingRod;
            _input.Update(available,grip,trigger,Vector3.Distance(_right.transform.position,_rod.position)<.24f,leftGrip,
                Vector3.Distance(_left.transform.position,_knob.position)<.16f,angle);
            if(HoldingRod && !wasHolding)Haptic(right,.22f);
            if(_input.Activated)
            {
                bool action=_c.Session.Phase==CatchPhase.Bite?_c.Hook():_c.Session.Phase==CatchPhase.Landing?_c.Scoop():_c.Cast();
                if(action)Haptic(right,.3f);
            }
        }
        private static void Haptic(InputDevice device,float strength) { if(device.TryGetHapticCapabilities(out var capabilities) && capabilities.supportsImpulse)device.SendHapticImpulse(0,strength,.04f); }
        private static bool Tracked(InputDevice device)=>device.isValid && device.TryGetFeatureValue(CommonUsages.isTracked,out bool tracked) && tracked
            && device.TryGetFeatureValue(CommonUsages.devicePosition,out Vector3 p) && Finite(p.x) && Finite(p.y) && Finite(p.z)
            && device.TryGetFeatureValue(CommonUsages.deviceRotation,out Quaternion q) && Finite(q.x) && Finite(q.y) && Finite(q.z) && Finite(q.w) && Quaternion.Dot(q,q)>.5f;
        private static bool Finite(float value)=>!float.IsNaN(value) && !float.IsInfinity(value);

        private Vector3 _gestureTarget;
        public FishingGlove Left => _left;
        public FishingGlove Right => _right;
        public Transform Rod => _rod;
        public Transform Knob => _knob;
        public bool TrackedPresentation { get; private set; }
        public void Build(FishingPrototype c,Transform tip,Transform crank,Transform net)
        {
            _c=c;_rod=tip.parent;_crank=crank;_net=net;
            _left=Create("Left articulated fishing glove",true);_right=Create("Right articulated fishing glove",false);
            _knob=new GameObject("Left hand reel attachment").transform;_knob.SetParent(_crank,false);_knob.localPosition=new Vector3(-.085f,-.06f,0);
        }
        private FishingGlove Create(string label,bool left)
        {
            var go=new GameObject(label);go.transform.SetParent(transform,false);
            var glove=go.AddComponent<FishingGlove>();glove.Build(left);return glove;
        }
        public void CastGesture(){_cast=.45f;}
        public void ReachGesture(Vector3 target){_gesture=.55f;_gestureTarget=target;}
        public void Apply()
        {
            if(_c==null)return;
            bool xr=XRSettings.isDeviceActive;TrackedPresentation=xr;
            if(xr)
            {
                Track(_left,XRNode.LeftHand);Track(_right,XRNode.RightHand);
                StowRod();
                if(HoldingRod && _right.gameObject.activeSelf)
                {
                    var rotation=_right.transform.rotation*Quaternion.Euler(0,90,0);
                    _rod.SetPositionAndRotation(_right.transform.position-rotation*new Vector3(.058f,.012f,-.045f),rotation);
                }
                return;
            }
            _pickupTime=Mathf.Max(0,_pickupTime-Time.deltaTime);
            if(_pickup!=null)_pickup.SetActive(_pickupTime>0 && _c.Ashore && !_c.Paused);
            bool visible=_c.FirstPerson && !_c.Paused;
            _left.gameObject.SetActive(visible);_right.gameObject.SetActive(visible);
            if(_c.Paused){_gesture=0;_cast=0;return;}
            _gesture=Mathf.Max(0,_gesture-Time.deltaTime);_cast=Mathf.Max(0,_cast-Time.deltaTime);
            var camera=_c.viewCamera.transform;
            bool fishing=visible && _c.Anchored && !_c.Ashore;
            if(!fishing)StowRod();
            if(!visible)return;
            if(fishing)
            {
                _rod.gameObject.SetActive(true);
                _rod.SetPositionAndRotation(camera.TransformPoint(new Vector3(.19f,-.09f,.62f)),camera.rotation*Quaternion.Euler(-32f-Mathf.Sin(_cast/.45f*Mathf.PI)*24f,-12f,0));
                Pose(_right,_rod.TransformPoint(new Vector3(.058f,.012f,-.045f)),_rod.rotation*Quaternion.Euler(-35,-55,0),.86f);
                if(_c.Session.Phase==CatchPhase.Landing)
                {
                    _net.SetPositionAndRotation(camera.TransformPoint(new Vector3(-.28f,-.32f,.53f)),camera.rotation*Quaternion.Euler(0,-70f,0));
                    Pose(_left,_net.TransformPoint(new Vector3(.13f,.035f,0)),camera.rotation*Quaternion.Euler(-25,20,-15),.9f);
                }
                else Pose(_left,_knob.position+_rod.TransformVector(new Vector3(-.024f,.012f,-.045f)),_rod.rotation*Quaternion.Euler(0,15,0),.85f);
            }
            else
            {
                float sway=_c.Ashore?Mathf.Sin(Time.time*6)*Mathf.Min(_c.Shore.WalkSpeed,3)*.004f:0;
                Pose(_left,camera.TransformPoint(new Vector3(-.25f,-.23f+sway,.55f)),camera.rotation*Quaternion.Euler(-18,-12,-12),.2f);
                Pose(_right,camera.TransformPoint(new Vector3(.25f,-.23f-sway,.55f)),camera.rotation*Quaternion.Euler(-18,12,12),.2f);
                if(_gesture>0)
                {
                    float reach=Mathf.Sin((1-_gesture/.55f)*Mathf.PI);
                    var origin=camera.TransformPoint(new Vector3(.25f,-.23f,.55f));
                    var desired=Vector3.Lerp(origin,_gestureTarget,reach);
                    // Keep the limb human-sized; distant E interactions use a point, not a teleporting arm.
                    desired=camera.position+Vector3.ClampMagnitude(desired-camera.position,.78f);
                    Pose(_right,desired,Quaternion.LookRotation((_gestureTarget-camera.position).normalized,Vector3.up),.25f,true);
                }
            }
        }
        private void StowRod()
        {
            _rod.gameObject.SetActive(!_c.Ashore);
            _rod.localPosition=XRSettings.isDeviceActive && _c.Anchored?new Vector3(.48f,1.45f,.1f):new Vector3(.75f,.95f,.9f);
            _rod.localRotation=Quaternion.LookRotation(new Vector3(1.9f,1.7f,1.35f));
        }
        private static void Pose(FishingGlove hand,Vector3 position,Quaternion rotation,float curl,bool point=false)
        {hand.transform.SetPositionAndRotation(position,rotation);hand.Pose(curl,point);}
        private void Track(FishingGlove hand,XRNode node)
        {
            var device=InputDevices.GetDeviceAtXRNode(node);
            bool valid=!_c.Paused && Application.isFocused && Tracked(device);
            if(!valid || !device.TryGetFeatureValue(CommonUsages.devicePosition,out Vector3 p) || !device.TryGetFeatureValue(CommonUsages.deviceRotation,out Quaternion q)){hand.gameObject.SetActive(false);return;}
            var head=InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if(!Tracked(head) || !head.TryGetFeatureValue(CommonUsages.devicePosition,out Vector3 hp) || !head.TryGetFeatureValue(CommonUsages.deviceRotation,out Quaternion hq)){hand.gameObject.SetActive(false);return;}
            // Infer tracking-to-world from the tracked camera; do not assume its parent is an XR Origin.
            var camera=_c.viewCamera.transform;var world=camera.rotation*Quaternion.Inverse(hq);
            hand.gameObject.SetActive(true);device.TryGetFeatureValue(CommonUsages.grip,out float grip);
            Pose(hand,camera.position+world*(p-hp),world*q,grip);
        }
    }
}
