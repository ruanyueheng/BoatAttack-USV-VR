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
            var item=PresentedItem;
            if(_c==null || _held==null || (!FishingBackpack.IsGatherTool(item) && item!=FieldItem.Rake) || !_c.Hands.CanPresentTools
                || !FinitePick(palm.x) || !FinitePick(palm.y) || !FinitePick(palm.z)
                || !FinitePick(palmFrame.x) || !FinitePick(palmFrame.y) || !FinitePick(palmFrame.z)
                || !FinitePick(palmFrame.w) || Quaternion.Dot(palmFrame,palmFrame)<.5f)return false;
            palmFrame=palmFrame.normalized;
            var hand=item==FieldItem.Shovel?Quaternion.identity:TrackedPickPalmRotation;
            float scale=WorkToolScale(item);
            var rotation=palmFrame*Quaternion.Inverse(hand);
            var localPalm=WorkHandle(item)+hand*TrackedPickPalmOffset/scale;
            _held.localScale=Vector3.one*scale;
            _held.SetPositionAndRotation(palm-rotation*(localPalm*scale),rotation);
            _c.Hands.SetToolTarget(false,_held,localPalm,hand,.94f,fingers:PickFingers);
            ConfigureWorkGrip(item,scale);
            return true;
        }
        private void ConfigureWorkGrip(FieldItem item,float scale)
        {
            var center=WorkHandle(item);
            var axis=item==FieldItem.Shovel?Vector3.right:Vector3.up;
            float half=item==FieldItem.Shovel?.07f:.15f;
            bool pick=FishingUserTools.IsPick(item);
            _c.Hands.Right.SetGripSurface(_held.TransformPoint(pick?Vector3.down*.155f:center-axis*half),_held.TransformPoint(pick?Vector3.up*.25f:center+axis*half),
                (pick?.018f:item==FieldItem.Shovel?.016f:.022f)*scale);
        }
        private static bool FinitePick(float value)=>!float.IsNaN(value) && !float.IsInfinity(value);
    }
}
