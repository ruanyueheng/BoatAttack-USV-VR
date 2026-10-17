using UnityEngine;
using UnityEngine.XR;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingCatchRig
    {
        private float _leftLiftTime,_leftPlaceTime;
        private int _leftPoseFrame=-1;
        private bool _placingLeft;
        private Vector3 _leftStartLocal;
        private Transform _leftGripRoot;
        private Vector3 _leftInspectionGrip;
        public bool LeftHandInspecting=>_c!=null && _c.HasPendingCatch && Location==Place.LeftHand;
        public bool PlacingInNet=>LeftHandInspecting && _placingLeft;
        public bool LeftInspectionSettled=>LeftHandInspecting && !_placingLeft && _leftLiftTime>=.65f;
        public bool RequestLiftForInspection()
        {
            if(XRSettings.isDeviceActive || _c.Paused || !_c.Anchored || _c.HasPendingCatch || !_c.Session.CanNet)return false;
            return _c.BeginHandCatch();
        }
        public void StartLeftInspection(Vector3 start)
        {
            if(!_c.HasPendingCatch || _c.Paused || XRSettings.isDeviceActive)return;
            if(_fishId!=_c.Career.PendingCatch.fish.id){_fishId=_c.Career.PendingCatch.fish.id;_c.RestorePendingFishVisual();}
            Location=Place.LeftHand;_placingLeft=false;_leftLiftTime=_leftPlaceTime=0;_leftPoseFrame=-1;
            _leftStartLocal=_c.Player.transform.InverseTransformPoint(start);_c.MeasureCatch();
        }
        public bool PlaceLeftFishInNet()
        {
            if(!LeftHandInspecting || _c.Paused || _placingLeft)return false;
            _placingLeft=true;_leftPlaceTime=0;_leftStartLocal=_c.Player.transform.InverseTransformPoint(_c.ActiveFish.position);
            return true;
        }
        public bool ToggleLeftInspection()
        {
            if(!_c.HasPendingCatch || _c.Paused || XRSettings.isDeviceActive)return false;
            if(LeftHandInspecting)return PlaceLeftFishInNet();
            StartLeftInspection(_c.ActiveFish.position);return true;
        }
        public Vector3 LeftInspectionGrip
        {
            get
            {
                var fish=_c.ActiveFish;if(_leftGripRoot==fish)return _leftInspectionGrip;
                _leftGripRoot=fish;var filter=fish.GetComponentInChildren<MeshFilter>();var b=filter.sharedMesh.bounds;
                var axis=b.size.y>b.size.z?Vector3.up:Vector3.forward;
                var point=b.center+Vector3.Scale(axis,b.extents)*.60f;
                point.x=b.center.x-b.extents.x*.8f;
                return _leftInspectionGrip=fish.InverseTransformPoint(FishingContactGeometry.ClosestPoint(fish,filter.transform.TransformPoint(point)));
            }
        }
        private void ApplyLeftInspection(float dt)
        {
            var fish=_c.ActiveFish;var character=_c.Shore.Character;
            if(_leftPoseFrame!=Time.frameCount){_leftPoseFrame=Time.frameCount;if(!_c.Paused){float step=Mathf.Clamp(dt,0,.05f);_leftLiftTime+=step;if(_placingLeft)_leftPlaceTime+=step;}}
            _c.Hands.GetToolFrame(out _,out var frame);
            var palmRotation=Quaternion.LookRotation(Vector3.right,Vector3.down);
            var grip=LeftInspectionGrip;
            if(_placingLeft)
            {
                float u=Mathf.Clamp01(_leftPlaceTime/.7f);
                var netPoint=_c.LandingNet.TransformPoint(new Vector3(FishingDetailArt.NetMouthOffset,-.13f,0));
                fish.position=Vector3.Lerp(_c.Player.transform.TransformPoint(_leftStartLocal),netPoint,Mathf.SmoothStep(0,1,u))+Vector3.up*Mathf.Sin(u*Mathf.PI)*.06f;
                fish.rotation=Quaternion.Slerp(fish.rotation,_c.LandingNet.rotation*InspectionRotation,u);
                if(u<.65f)
                {
                    var shoulder=character.ShoulderPosition(true);var palm=fish.TransformPoint(grip);
                    _c.Hands.Left.SetPose(shoulder+Vector3.ClampMagnitude(palm-shoulder,character.ArmReach(true)-.035f),frame*palmRotation,.5f*(1-u));
                }
                else _c.Hands.RestHand(true);
                if(u>=1){Location=Place.Net;_placingLeft=false;_c.Notice("活鱼已放入抄网。E 再次检视，F 入鱼箱，X 放生。",5);}
                return;
            }
            var bounds=fish.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
            var axis=bounds.size.y>bounds.size.z?Vector3.up:Vector3.forward;
            var rotation=frame*Quaternion.Euler(0,38,Mathf.Sin(_leftLiftTime*8)*2.5f)*Quaternion.FromToRotation(axis,Vector3.up);
            var palmTarget=character.ShoulderPosition(true)+frame*new Vector3(character.HandSide(true)*.025f,-.10f,.32f);
            var target=palmTarget-rotation*Vector3.Scale(grip,fish.lossyScale);
            fish.SetPositionAndRotation(Vector3.Lerp(_c.Player.transform.TransformPoint(_leftStartLocal),target,Mathf.SmoothStep(0,1,Mathf.Clamp01(_leftLiftTime/.65f))),rotation);
            // The independent fish is fitted to the real left arm; the right hand keeps the rod.
            _c.Hands.FitToolPose(fish,grip,Vector3.zero,palmRotation,Quaternion.identity,true,false);
            _c.Hands.SetToolTarget(true,fish,grip,palmRotation,.65f);
            _c.Hands.Left.SetFingers(new FishingFingerPose(.60f,.72f,.74f,.72f,.70f));
        }
    }
}
