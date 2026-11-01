using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingFieldWorld
    {
        // The source wooden shaft runs along +Y. Knuckles/fingers run along +Z of
        // the palm, across that shaft; controller forward is not the handle axis.
        public static readonly Quaternion TrackedPickPalmRotation=Quaternion.LookRotation(Vector3.forward,Vector3.right);
        // Match the displayed Bone2 hand while retaining the existing global palm calibration.
        public static readonly Vector3 TrackedPickPalmOffset=new Vector3(-.01976069f,.05073457f,-.04269347f);

        /// <summary>Mounts the supplied pick on the same calibrated palm used by the visible hand.</summary>
        public bool ApplyTrackedPickPose(Vector3 palm,Quaternion palmFrame)
        {
            if(_c==null || _held==null || !FishingUserTools.IsPick(PresentedItem) || !_c.Hands.CanPresentTools
                || !FinitePick(palm.x) || !FinitePick(palm.y) || !FinitePick(palm.z)
                || !FinitePick(palmFrame.x) || !FinitePick(palmFrame.y) || !FinitePick(palmFrame.z)
                || !FinitePick(palmFrame.w) || Quaternion.Dot(palmFrame,palmFrame)<.5f)return false;
            palmFrame=palmFrame.normalized;
            var rotation=palmFrame*Quaternion.Inverse(TrackedPickPalmRotation);
            var localPalm=TrackedPickPalmRotation*TrackedPickPalmOffset/PickScale;
            _held.localScale=Vector3.one*PickScale;
            _held.SetPositionAndRotation(palm-rotation*(localPalm*PickScale),rotation);
            _c.Hands.SetToolTarget(false,_held,localPalm,TrackedPickPalmRotation,.94f,fingers:PickFingers);
            _c.Hands.Right.SetGripSurface(_held.TransformPoint(new Vector3(0,-.155f,0)),
                _held.TransformPoint(new Vector3(0,.25f,0)),.018f*PickScale);
            return true;
        }
        private static bool FinitePick(float value)=>!float.IsNaN(value) && !float.IsInfinity(value);
    }
}
