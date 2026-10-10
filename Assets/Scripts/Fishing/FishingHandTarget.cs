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
        // Visibility here means target participation, not renderer visibility.
        public bool Visible=>_available && _frame==Time.frameCount && isActiveAndEnabled;
        public void Build(bool left){IsLeft=left;Disable();}
        public void BeginFrame()=>Disable();
        public void Disable(){_available=false;_frame=-1;Curl=0;Fingers=FishingFingerPose.Uniform(0);Pointing=false;}
        public void SetPose(Vector3 position,Quaternion rotation,float curl,bool pointing=false)
        {
            if(!Finite(position.x)||!Finite(position.y)||!Finite(position.z)||!Finite(rotation.x)||!Finite(rotation.y)
                ||!Finite(rotation.z)||!Finite(rotation.w)||Quaternion.Dot(rotation,rotation)<.5f || !Finite(curl))
            {Disable();return;}
            transform.SetPositionAndRotation(position,rotation.normalized);
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
        private static bool Finite(float value)=>!float.IsNaN(value) && !float.IsInfinity(value);
        private void OnDisable()=>Disable();
    }
}
