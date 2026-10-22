using UnityEngine;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingHome
    {
        public const float SleepDuration=6.8f;
        private Vector3 _sleepStart,_bedEdge,_bedFeet,_seatedHip,_cameraStart;
        private Quaternion _sleepFacing,_seatedFacing,_cameraStartRotation,_lyingRotation;
        public float SleepProgress=>Sleeping?Mathf.Clamp01(_age/SleepDuration):0;
        public string SleepPhase=>!Sleeping?"起身":_age<.85f?"走到床边":_age<1.6f?"坐上床沿":_age<2.65f?"躺下":_age<4.45f?"休息":_age<5.55f?"坐起":"下床";
        public Vector3 SleepEyePosition { get; private set; }
        public float SleepFade=>_black!=null?_black.color.a:0;

        private void BeginSleepMotion()
        {
            _sleepStart=_c.Shore.Position;_sleepFacing=_c.Shore.Actor.rotation;
            _cameraStart=_c.viewCamera.transform.position;_cameraStartRotation=_c.viewCamera.transform.rotation;
            var bounds=new Bounds(Bed.position,Vector3.zero);bool first=true;
            foreach(var renderer in Bed.GetComponentsInChildren<Renderer>())
            {if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);}
            _bedEdge=WakePoint;
            bool alongZ=bounds.size.z>=bounds.size.x;
            var length=alongZ?Vector3.forward:Vector3.right;
            float mattress=bounds.min.y+bounds.size.y*.55f;
            foreach(var collider in Bed.GetComponentsInChildren<MeshCollider>())
                if(collider.Raycast(new Ray(new Vector3(bounds.center.x,bounds.max.y+1,bounds.center.z),Vector3.down),out var hit,bounds.size.y+2))mattress=hit.point.y;
            _seatedHip=new Vector3(Mathf.Clamp(_bedEdge.x,bounds.min.x+.18f,bounds.max.x-.18f),mattress+.14f,Mathf.Clamp(_bedEdge.z,bounds.min.z+.25f,bounds.max.z-.25f));
            _seatedFacing=Quaternion.LookRotation(Vector3.ProjectOnPlane(_bedEdge-_seatedHip,Vector3.up));
            _bedFeet=bounds.center+length*.72f;_bedFeet.y=mattress+.16f;
            _lyingRotation=Quaternion.Euler(0,alongZ?0:90,0)*Quaternion.Euler(-90,0,0);
            SleepEyePosition=_c.Shore.Character.EyePosition;
        }

        // The collision capsule stays on the clear bedside floor; only the original skinned rig climbs onto the mattress.
        public void PresentSleepPose()
        {
            if(Lounging){PresentLeisurePose();return;}
            if(!Sleeping || _c==null)return;
            float enter=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.85f,_age));
            float sit=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.85f,1.6f,_age));
            float recline=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.6f,2.65f,_age));
            float rise=Mathf.SmoothStep(0,1,Mathf.InverseLerp(4.45f,5.55f,_age));
            float leave=Mathf.SmoothStep(0,1,Mathf.InverseLerp(5.55f,SleepDuration,_age));
            float lying=recline*(1-rise),seated=sit*(1-recline)*(1-leave)+rise*(1-leave);
            var edge=Vector3.Lerp(_sleepStart,_bedEdge,enter);
            var feet=Vector3.Lerp(edge,_bedFeet,lying);feet=Vector3.Lerp(feet,_sleepStart,leave);
            var facing=Quaternion.Slerp(_sleepFacing,_seatedFacing,sit*(1-leave));
            var rotation=Quaternion.Slerp(facing,_lyingRotation,lying);
            _c.Shore.Character.ApplySleepPose(feet,rotation,Mathf.Clamp01(seated),_seatedHip,sit*(1-leave));
            SleepEyePosition=_c.Shore.Character.SleepPoseEye;
            if(XRSettings.isDeviceActive)return;
            var camera=_c.viewCamera;
            if(_c.FirstPerson)
            {
                camera.transform.position=Vector3.Lerp(_cameraStart,SleepEyePosition,enter*(1-leave));
                camera.transform.rotation=Quaternion.Slerp(_cameraStartRotation,rotation*Quaternion.Euler(13,0,0),sit*(1-leave));
            }
            else
            {
                var focus=Vector3.Lerp(_bedEdge+Vector3.up*.85f,_bedFeet+rotation*Vector3.up*.8f,lying);
                var position=_bedEdge+new Vector3(2.2f,2.5f,-3.3f);
                camera.transform.SetPositionAndRotation(Vector3.Lerp(_cameraStart,position,enter*(1-leave)),Quaternion.Slerp(_cameraStartRotation,Quaternion.LookRotation(focus-position),enter*(1-leave)));
            }
        }

        public Vector3 ComfortableSleepEye
        {
            get
            {
                // XR preserves physical head rotation; only eye height/position follows the bed, at a slow eased rate.
                float lower=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.85f,2.65f,_age));
                float raise=Mathf.SmoothStep(0,1,Mathf.InverseLerp(4.45f,SleepDuration,_age));
                return Vector3.Lerp(_cameraStart,_bedFeet+_lyingRotation*Vector3.up*1.40f+Vector3.up*.22f,lower*(1-raise));
            }
        }
        public Vector3 SleepEyeOffset=>Sleeping?ComfortableSleepEye-_cameraStart:Lounging?LoungeEyeOffset:Vector3.zero;
    }
}
