using UnityEngine;

namespace BoatAttack.Fishing
{
    /// <summary>
    /// A meshless, frame-scoped palm goal, NOT a character bone. Root, Palm and transform
    /// all refer to this independent goal. +Z runs from wrist to knuckles; +Y is the back
    /// of the hand (curl goes toward -Y). Tools/controllers own goals; IK never moves them.
    /// </summary>
    public sealed class FishingHandTarget : MonoBehaviour
    {
        private int _frame=-1;
        private bool _available;
        public Transform Root=>transform;
        public Transform Palm=>transform;
        public bool IsLeft { get; private set; }
        public float Curl { get; private set; }
        public FishingFingerPose Fingers { get; private set; }
        public bool Pointing { get; private set; }
        public float ForearmRoll { get; private set; }
        public bool HasGripSurface { get; private set; }
        public bool AuthoredMotion { get; private set; }
        public Vector3 GripStart { get; private set; }
        public Vector3 GripEnd { get; private set; }
        public float GripRadius { get; private set; }
        // Visibility here means target participation, not renderer visibility.
        public bool Visible=>_available && _frame==Time.frameCount && isActiveAndEnabled;
        public void Build(bool left){IsLeft=left;Disable();}
        public void BeginFrame()=>Disable();
        public void Disable(){_available=false;_frame=-1;Curl=0;Fingers=FishingFingerPose.Uniform(0);Pointing=false;ForearmRoll=0;HasGripSurface=false;AuthoredMotion=false;}
        public void SetForearmRoll(float blend){if(Visible && Finite(blend))ForearmRoll=Mathf.Clamp01(blend);}
        public void FollowAuthoredMotion(){if(Visible)AuthoredMotion=true;}
        public void SetPose(Vector3 position,Quaternion rotation,float curl,bool pointing=false)
        {
            if(!Finite(position.x)||!Finite(position.y)||!Finite(position.z)||!Finite(rotation.x)||!Finite(rotation.y)
                ||!Finite(rotation.z)||!Finite(rotation.w)||Quaternion.Dot(rotation,rotation)<.5f || !Finite(curl))
            {Disable();return;}
            transform.SetPositionAndRotation(position,rotation.normalized);
            HasGripSurface=false;AuthoredMotion=false;
            Curl=Mathf.Clamp01(curl);Fingers=FishingFingerPose.Uniform(Curl);Pointing=pointing;_frame=Time.frameCount;_available=true;
        }
        /// <summary>Change grip on an already supplied goal; cannot revive expired tracking.</summary>
        public void Pose(float curl,bool pointing=false)
        {
            if(!Finite(curl)){Disable();return;}
            Curl=Mathf.Clamp01(curl);Fingers=FishingFingerPose.Uniform(Curl);Pointing=pointing;
        }
        public void SetFingers(FishingFingerPose pose)
        {
            for(int i=0;i<5;i++)if(!Finite(pose.Curl(i))){Disable();return;}
            Fingers=new FishingFingerPose(Mathf.Clamp01(pose.Curl(0)),Mathf.Clamp01(pose.Curl(1)),
                Mathf.Clamp01(pose.Curl(2)),Mathf.Clamp01(pose.Curl(3)),Mathf.Clamp01(pose.Thumb));
        }
        public void SetFromGrip(Transform grip,Vector3 localPalmPosition,Quaternion localPalmRotation,float curl,bool pointing=false)
        {
            if(grip==null || !grip.gameObject.activeInHierarchy){Disable();return;}
            SetPose(grip.TransformPoint(localPalmPosition),grip.rotation*localPalmRotation,curl,pointing);
        }
        /// <summary>The actual handle centreline, separate from the visible palm surface.</summary>
        public void SetGripSurface(Vector3 start,Vector3 end,float radius)
        {
            if(!Visible || !Finite(start.x)||!Finite(start.y)||!Finite(start.z)||!Finite(end.x)||!Finite(end.y)||!Finite(end.z)
                || !Finite(radius) || radius<=0 || (end-start).sqrMagnitude<1e-8f)return;
            GripStart=start;GripEnd=end;GripRadius=radius;HasGripSurface=true;
        }
        private static bool Finite(float value)=>!float.IsNaN(value) && !float.IsInfinity(value);
        private void OnDisable()=>Disable();
    }
}
