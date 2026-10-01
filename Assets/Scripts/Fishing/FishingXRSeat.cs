using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using Unity.XR.CoreUtils;
namespace BoatAttack.Fishing
{
    /// <summary>Stable vessel frame only. The tracked pose driver exclusively owns HMD local pose.</summary>
    public sealed class FishingXRSeat : MonoBehaviour
    {
        private FishingPrototype _c;private Transform _originalParent,_offset;private TrackedPoseDriver _driver;
        private InputAction _position,_rotation,_tracking;private bool _active;private float _height,_velocity;
        public XROrigin Origin { get; private set; }
        public void Build(FishingPrototype c)
        {
            _c=c;_originalParent=c.viewCamera.transform.parent;
            var root=new GameObject("Fishing XR stable vessel origin");root.transform.SetParent(transform,false);Origin=root.AddComponent<XROrigin>();
            _offset=new GameObject("Device-space eye height").transform;_offset.SetParent(root.transform,false);
            Origin.Camera=c.viewCamera;Origin.CameraFloorOffsetObject=_offset.gameObject;Origin.RequestedTrackingOriginMode=XROrigin.TrackingOriginMode.Device;Origin.CameraYOffset=1.65f;
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
                    _c.viewCamera.transform.SetParent(_offset,false);_c.viewCamera.transform.localPosition=Vector3.zero;_c.viewCamera.transform.localRotation=Quaternion.identity;
                    Origin.enabled=true;_position.Enable();_rotation.Enable();_tracking.Enable();_driver.enabled=true;
                }
                else
                {
                    _driver.enabled=false;_position.Disable();_rotation.Disable();_tracking.Disable();Origin.enabled=false;
                    _c.viewCamera.transform.SetParent(_originalParent,true);_c.View.ResetLook();
                }
            }
            if(!active)return;
            var boat=_c.Player.transform;
            if(Time.deltaTime>0)_height=Mathf.SmoothDamp(_height,boat.position.y,ref _velocity,1.5f);
            var yaw=Quaternion.Euler(0,boat.eulerAngles.y,0);
            Origin.transform.SetPositionAndRotation(new Vector3(boat.position.x,_height+.25f,boat.position.z)+yaw*new Vector3(0,0,-.25f),yaw);
        }
        private void OnDestroy(){if(_driver!=null)_driver.enabled=false;_position?.Dispose();_rotation?.Dispose();_tracking?.Dispose();}
    }
}
