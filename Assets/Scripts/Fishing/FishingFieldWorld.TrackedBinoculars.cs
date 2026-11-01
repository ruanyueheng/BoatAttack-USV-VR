using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingFieldWorld
    {
        public const float TrackedBinocularScale=.4f;
        public static readonly Vector3 TrackedBinocularLeftPalm=new Vector3(-.235f,0,.08f);
        public static readonly Vector3 TrackedBinocularRightPalm=new Vector3(.235f,0,.08f);
        private Mesh _trackedBinocularMesh;
        private float _trackedBinocularIpd;
        public bool BinocularEyeAlignment {get;private set;}
        public bool ApplyTrackedBinocularPose(Pose leftPalm,Pose rightPalm,bool leftHeld,bool rightHeld,bool alignAtEyes=false)
        {
            if(_held==null || PresentedItem!=FieldItem.Binoculars)return false;
            var gripFrame=_c.Hands.ControllerGripRotation(rightPalm.rotation);
            var separation=rightPalm.position-leftPalm.position;float distance=separation.magnitude;
            bool both=leftHeld && rightHeld && distance>.10f && distance<.32f;
            Quaternion rotation=gripFrame;
            if(both)
            {
                var right=separation/distance;var forward=Vector3.ProjectOnPlane(gripFrame*Vector3.forward,right);
                if(forward.sqrMagnitude<.01f)forward=Vector3.ProjectOnPlane(_c.viewCamera.transform.forward,right);
                if(forward.sqrMagnitude>.01f)rotation=Quaternion.LookRotation(forward.normalized,Vector3.Cross(forward.normalized,right));
                else both=false;
            }
            _held.localScale=Vector3.one*TrackedBinocularScale;
            float ipd=FishingXRBinoculars.EyeSeparation(_c.viewCamera);
            if(_trackedBinocularMesh==null || Mathf.Abs(ipd-_trackedBinocularIpd)>.0005f)
            {
                if(_trackedBinocularMesh!=null){_owned.Remove(_trackedBinocularMesh);Object.Destroy(_trackedBinocularMesh);}
                _trackedBinocularMesh=FishingFieldArt.TrackedBinoculars(ipd);_trackedBinocularIpd=ipd;_owned.Add(_trackedBinocularMesh);
            }
            _heldMesh.sharedMesh=_trackedBinocularMesh;
            var midpoint=(TrackedBinocularLeftPalm+TrackedBinocularRightPalm)*.5f;
            var position=both?(leftPalm.position+rightPalm.position)*.5f-rotation*(midpoint*TrackedBinocularScale)
                :rightPalm.position-rotation*(TrackedBinocularRightPalm*TrackedBinocularScale);
            var head=new Pose(_c.viewCamera.transform.position,_c.viewCamera.transform.rotation);
            BinocularEyeAlignment=both && alignAtEyes && FishingXRBinoculars.CanAlignAtEyes(head,leftPalm.position,rightPalm.position);
            if(BinocularEyeAlignment)
            {
                // A short, deliberate trigger hold seats the cups on the real HMD eye positions.
                // Physical palms stay tracked; the headset's camera and stereo projections never move.
                rotation=head.rotation;
                position=FishingXRBinoculars.EyeCentre(_c.viewCamera)+rotation*Vector3.forward*FishingXRBinoculars.ComfortEyeDistance
                    -rotation*(Vector3.forward*FishingXRBinoculars.RearLensZ*TrackedBinocularScale);
            }
            _held.SetPositionAndRotation(position,rotation);
            SetBinocularHand(false,rightPalm);if(both)SetBinocularHand(true,leftPalm);return both;
        }
        private void SetBinocularHand(bool left,Pose palm)
        {
            var hand=left?_c.Hands.Left:_c.Hands.Right;
            // Supply the physical palm, rather than pulling a hand onto a nominal model mount.
            _c.Hands.SetToolTarget(left,_held,_held.InverseTransformPoint(palm.position),Quaternion.Inverse(_held.rotation)*palm.rotation,
                .5f,fingers:FishingFingerPose.Barrel);
            float side=left?-1:1;
            float rear=_trackedBinocularIpd/(2*TrackedBinocularScale);
            hand.SetGripSurface(_held.TransformPoint(new Vector3(side*rear,0,-.15f)),_held.TransformPoint(new Vector3(side*.125f,0,.25f)),
                .11f*TrackedBinocularScale);
        }
    }
}
