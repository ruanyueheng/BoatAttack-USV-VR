using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.UI;
using Unity.XR.CoreUtils;
using CommonUsages=UnityEngine.XR.CommonUsages;
namespace BoatAttack.Fishing
{
    /// <summary>Stable vessel frame only. The tracked pose driver exclusively owns HMD local pose.</summary>
    public sealed class FishingXRSeat : MonoBehaviour
    {
        private FishingPrototype _c;private Transform _originalParent,_offset;private TrackedPoseDriver _driver;
        private WaterSystem.Water _water;private WaterSystem.BuoyantObject _buoyancy;
        private InputAction _position,_rotation,_tracking;private bool _active,_wasAshore,_eyeCalibrated;private float _height,_boatHeight,_velocity,_shoreYaw,_eyeOffset;
        private Vector3 _lastBoatPosition;
        private TrackingOriginModeFlags _trackingOrigin;
        public const float EyeHeight=1.65f;
        public bool Ready=>_active && _eyeCalibrated;
        public float WaterlineHeight=>(_water!=null?_water.transform.position.y:0)+(_buoyancy!=null?_buoyancy.waterLevelOffset:0);
        public static float ComfortableBoatHeight(float restHeight,float boatHeight)=>restHeight+Mathf.Clamp((boatHeight-restHeight)*.08f,-.03f,.03f);
        public bool PhysicallyCrouched=>_active && _eyeCalibrated && _c.viewCamera.transform.localPosition.y+_eyeOffset<1.3f;
        public void Recenter()
        {
            if(!_active)return;
            if(!CalibrateEye()){_c.Notice("请等待头显恢复追踪后再校准眼高。",3);return;}
            _c.Notice("视线高度已校准为 1.65 米；可以坐姿或站姿游玩。",4);
        }
        private bool CalibrateEye()
        {
            if(!Application.isFocused)return false;
            var head=InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if(head.TryGetFeatureValue(CommonUsages.userPresence,out bool worn) && !worn)return false;
            if(!head.isValid || !head.TryGetFeatureValue(CommonUsages.isTracked,out bool tracked) || !tracked
                || !head.TryGetFeatureValue(CommonUsages.devicePosition,out var position) || float.IsNaN(position.y) || float.IsInfinity(position.y))return false;
            _eyeOffset=EyeHeight-position.y;_eyeCalibrated=true;return true;
        }
        public void SnapTurn(float degrees)
        {
            if(_c.Ashore && !_c.Paused)
            {
                var local=Vector3.ProjectOnPlane(_c.viewCamera.transform.localPosition,Vector3.up);
                _roomCorrection+=Quaternion.Euler(0,_shoreYaw,0)*local-Quaternion.Euler(0,_shoreYaw+degrees,0)*local;
                _shoreYaw+=degrees;
            }
        }
        public XROrigin Origin { get; private set; }
        private Vector3 _roomCorrection;
        private Image _headFade;private readonly Collider[] _headOverlaps=new Collider[24];
        public bool HeadBlocked { get; private set; }
        private void GuardHead()
        {
            int count=Physics.OverlapSphereNonAlloc(_c.viewCamera.transform.position,.10f,_headOverlaps,~((1<<4)|(1<<5)),QueryTriggerInteraction.Ignore);
            HeadBlocked=count==_headOverlaps.Length;
            for(int i=0;i<count;i++)if(_headOverlaps[i]!=_c.Shore.Body && !_headOverlaps[i].transform.IsChildOf(_c.Player.transform) && !FishingShore.NonSolidSurface(_headOverlaps[i])){HeadBlocked=true;break;}
            if(_headFade==null)
            {
                var root=new GameObject("XR wall proximity fade",typeof(RectTransform),typeof(Canvas));root.transform.SetParent(_c.viewCamera.transform,false);
                root.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;var rect=root.GetComponent<RectTransform>();rect.sizeDelta=Vector2.one*2;rect.localPosition=Vector3.forward*.06f;
                _headFade=root.AddComponent<Image>();_headFade.raycastTarget=false;_headFade.color=Color.clear;
            }
            _headFade.gameObject.SetActive(_active);
            float travel=_c.Realms==null?0:_c.Realms.Blackout;
            _headFade.color=new Color(0,0,0,Mathf.Max(travel,Mathf.MoveTowards(_headFade.color.a,HeadBlocked?1:0,Time.unscaledDeltaTime*8)));
        }
        public void Build(FishingPrototype c)
        {
            _c=c;_originalParent=c.viewCamera.transform.parent;
            _water=WaterSystem.Water.Instance;_buoyancy=c.Player.GetComponent<WaterSystem.BuoyantObject>();
            var root=new GameObject("Fishing XR stable vessel origin");root.transform.SetParent(transform,false);Origin=root.AddComponent<XROrigin>();
            _offset=new GameObject("Device-space eye height").transform;_offset.SetParent(root.transform,false);
            Origin.Camera=c.viewCamera;Origin.CameraFloorOffsetObject=_offset.gameObject;Origin.RequestedTrackingOriginMode=XROrigin.TrackingOriginMode.Floor;Origin.CameraYOffset=0;
            _position=new InputAction("Fishing HMD position",InputActionType.Value,"<XRHMD>/centerEyePosition");
            _rotation=new InputAction("Fishing HMD rotation",InputActionType.Value,"<XRHMD>/centerEyeRotation");
            _tracking=new InputAction("Fishing HMD tracking",InputActionType.Value,"<XRHMD>/trackingState");
            _driver=c.viewCamera.gameObject.AddComponent<TrackedPoseDriver>();_driver.enabled=false;
            _driver.positionInput=new InputActionProperty(_position);_driver.rotationInput=new InputActionProperty(_rotation);_driver.trackingStateInput=new InputActionProperty(_tracking);
            _driver.trackingType=TrackedPoseDriver.TrackingType.RotationAndPosition;_driver.updateType=TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            Origin.enabled=false;
        }
        public void Apply()
        {
            if(_c==null || _c.Player==null)return;
            bool active=XRSettings.isDeviceActive;
            if(active!=_active)
            {
                _active=active;_c.CatchRig.Recover();
                if(active)
                {
                    ResetVesselHeight();_lastBoatPosition=_c.Player.transform.position;_eyeCalibrated=false;_wasAshore=false;
                    _c.viewCamera.nearClipPlane=.04f;
                    _c.viewCamera.transform.SetParent(_offset,false);_c.viewCamera.transform.localPosition=Vector3.zero;_c.viewCamera.transform.localRotation=Quaternion.identity;
                    Origin.enabled=true;_position.Enable();_rotation.Enable();_tracking.Enable();_driver.enabled=true;
                }
                else
                {
                    _driver.enabled=false;_position.Disable();_rotation.Disable();_tracking.Disable();Origin.enabled=false;
                    _c.viewCamera.transform.SetParent(_originalParent,true);_c.View.ResetLook();
                }
            }
            if(!active){if(_headFade!=null)_headFade.gameObject.SetActive(false);HeadBlocked=false;return;}
            if(_trackingOrigin!=Origin.CurrentTrackingOriginMode){_trackingOrigin=Origin.CurrentTrackingOriginMode;_eyeCalibrated=false;}
            if(!_eyeCalibrated && _trackingOrigin!=TrackingOriginModeFlags.Unknown)CalibrateEye();
            _offset.localPosition=Vector3.up*_eyeOffset;
            if(_c.Ashore)
            {
                if(!_wasAshore)_shoreYaw=_c.Shore.Actor.eulerAngles.y;
                var shoreRotation=Quaternion.Euler(0,_shoreYaw,0);
                if(!_wasAshore)_roomCorrection=-Vector3.ProjectOnPlane(shoreRotation*_c.viewCamera.transform.localPosition,Vector3.up);
                _wasAshore=true;
                var room=Vector3.ProjectOnPlane(shoreRotation*_c.viewCamera.transform.localPosition,Vector3.up)+_roomCorrection;
                if(!_c.Paused && !_c.XRNotebook.IsOpen && room.magnitude>.015f)_roomCorrection-=_c.Shore.RoomStep(Vector3.ClampMagnitude(room,.08f));
                Origin.transform.SetPositionAndRotation(_c.Shore.Position+_roomCorrection,shoreRotation);GuardHead();return;
            }
            var boat=_c.Player.transform;
            if(_wasAshore || Vector3.Distance(boat.position,_lastBoatPosition)>12f)ResetVesselHeight();
            _wasAshore=false;_lastBoatPosition=boat.position;
            // Vessel heave is visual motion; retain physical head movement at full scale.
            float target=_c.ComfortMode?ComfortableBoatHeight(_boatHeight,boat.position.y):boat.position.y;
            if(Time.deltaTime>0)_height=Mathf.SmoothDamp(_height,target,ref _velocity,_c.ComfortMode?2.5f:1.5f);
            var yaw=Quaternion.Euler(0,boat.eulerAngles.y,0);
            Origin.transform.SetPositionAndRotation(new Vector3(boat.position.x,_height+.25f,boat.position.z)+yaw*new Vector3(0,0,-.25f),yaw);GuardHead();
        }
        private void ResetVesselHeight()
        {
            // Spawn and rescue lift the hull before buoyancy settles; use the ocean's rest plane.
            _boatHeight=WaterlineHeight;_height=_c.ComfortMode?_boatHeight:_c.Player.transform.position.y;_velocity=0;
        }
        private void OnDestroy(){if(_driver!=null)_driver.enabled=false;_position?.Dispose();_rotation?.Dispose();_tracking?.Dispose();}
    }
}
