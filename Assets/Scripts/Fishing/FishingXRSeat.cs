using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.UI;
using Unity.XR.CoreUtils;
namespace BoatAttack.Fishing
{
    /// <summary>Stable vessel frame only. The tracked pose driver exclusively owns HMD local pose.</summary>
    public sealed class FishingXRSeat : MonoBehaviour
    {
        private FishingPrototype _c;private Transform _originalParent,_offset;private TrackedPoseDriver _driver;
        private InputAction _position,_rotation,_tracking;private bool _active,_wasAshore;private float _height,_velocity,_shoreYaw,_eyeOffset;
        public bool PhysicallyCrouched=>_active && _c.viewCamera.transform.localPosition.y+_eyeOffset<1.3f;
        public void Recenter()
        {
            if(!_active)return;
            _eyeOffset=1.65f-_c.viewCamera.transform.localPosition.y;
            _c.Notice("视线高度已校准为 1.65 米；可以坐姿或站姿游玩。",4);
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
            _headFade.color=new Color(0,0,0,Mathf.MoveTowards(_headFade.color.a,HeadBlocked?1:0,Time.unscaledDeltaTime*8));
        }
        public void Build(FishingPrototype c)
        {
            _c=c;_originalParent=c.viewCamera.transform.parent;
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
                    _height=_c.Player.transform.position.y;_velocity=0;
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
            _wasAshore=false;
            var boat=_c.Player.transform;
            if(Time.deltaTime>0)_height=Mathf.SmoothDamp(_height,boat.position.y,ref _velocity,1.5f);
            var yaw=Quaternion.Euler(0,boat.eulerAngles.y,0);
            Origin.transform.SetPositionAndRotation(new Vector3(boat.position.x,_height+.25f,boat.position.z)+yaw*new Vector3(0,0,-.25f),yaw);GuardHead();
        }
        private void OnDestroy(){if(_driver!=null)_driver.enabled=false;_position?.Dispose();_rotation?.Dispose();_tracking?.Dispose();}
    }
}
