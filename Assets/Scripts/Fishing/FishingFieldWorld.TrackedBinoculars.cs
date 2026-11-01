using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingFieldWorld
    {
        public const float TrackedBinocularScale=.22f; // Barrel centers are 66 mm apart in VR.
        public bool ApplyTrackedBinocularPose(Pose leftPalm,Pose rightPalm,bool leftHeld,bool rightHeld)
        {
            if(_held==null || PresentedItem!=FieldItem.Binoculars)return false;
            var gripFrame=_c.Hands.ControllerGripRotation(rightPalm.rotation);
            var separation=rightPalm.position-leftPalm.position;float distance=separation.magnitude;
            bool both=leftHeld && rightHeld && distance>.14f && distance<.21f;
            Quaternion rotation=gripFrame;
            if(both)
            {
                var right=separation/distance;var forward=Vector3.ProjectOnPlane(gripFrame*Vector3.forward,right);
                if(forward.sqrMagnitude<.01f)forward=Vector3.ProjectOnPlane(_c.viewCamera.transform.forward,right);
                if(forward.sqrMagnitude>.01f)rotation=Quaternion.LookRotation(forward.normalized,Vector3.Cross(forward.normalized,right));
                else both=false;
            }
            _held.localScale=Vector3.one*TrackedBinocularScale;
            var midpoint=(BinocularLeftPalm+BinocularRightPalm)*.5f;
            var position=both?(leftPalm.position+rightPalm.position)*.5f-rotation*(midpoint*TrackedBinocularScale)
                :rightPalm.position-rotation*(BinocularRightPalm*TrackedBinocularScale);
            _held.SetPositionAndRotation(position,rotation);
            SetBinocularHand(false,rightPalm);if(both)SetBinocularHand(true,leftPalm);return both;
        }
        private void SetBinocularHand(bool left,Pose palm)
        {
            var hand=left?_c.Hands.Left:_c.Hands.Right;
            // Supply the physical palm, rather than pulling a hand onto a nominal model mount.
            _c.Hands.SetToolTarget(left,_held,_held.InverseTransformPoint(palm.position),Quaternion.Inverse(_held.rotation)*palm.rotation,
                .5f,fingers:FishingFingerPose.Barrel);
            float side=left?-.15f:.15f;
            hand.SetGripSurface(_held.TransformPoint(new Vector3(side,0,-.15f)),_held.TransformPoint(new Vector3(side,0,.22f)),
                .14f*TrackedBinocularScale);
        }
    }
}
