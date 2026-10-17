using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingCharacter
    {
        public Vector3 HipPosition=>_body.position;
        public void ApplySleepPose(Vector3 feet,Quaternion rotation,float seated,Vector3 seatedHip,float resting)
        {
            if(!Ready)return;
            RestoreRigBase();
            seated=Mathf.Clamp01(seated);resting=Mathf.Clamp01(resting);
            var eyeLocal=_head.InverseTransformPoint(EyePosition);
            var delta=rotation*Quaternion.Inverse(transform.rotation);
            _model.SetPositionAndRotation(feet+delta*(_model.position-transform.position),delta*_model.rotation);
            if(seated>0)
            {
                var leftTarget=_leftAnkle.position;var rightTarget=_rightAnkle.position;
                var leftFootOffset=_leftFoot.position-leftTarget;var rightFootOffset=_rightFoot.position-rightTarget;
                _body.position=Vector3.Lerp(_body.position,seatedHip,seated);
                SleepLeg(_leftThigh,_leftShin,_leftAnkle,_leftFoot,leftTarget,leftFootOffset,rotation,seated);
                SleepLeg(_rightThigh,_rightShin,_rightAnkle,_rightFoot,rightTarget,rightFootOffset,rotation,seated);
            }
            _posedSleepEye=_head.TransformPoint(eyeLocal);
            SleepArm(_leftArm,rotation,seated,resting);SleepArm(_rightArm,rotation,seated,resting);
            CaptureArmBase(false);
        }
        private void SleepLeg(Transform thigh,Transform shin,Transform ankle,Transform foot,Vector3 standingTarget,Vector3 footOffset,Quaternion rotation,float seated)
        {
            float upper=Vector3.Distance(thigh.position,shin.position),lower=Vector3.Distance(shin.position,ankle.position);
            var forward=rotation*Vector3.forward;var up=rotation*Vector3.up;
            // Knees face out from the bed with the posed rig, not with the stationary collision capsule.
            var target=Vector3.Lerp(standingTarget,thigh.position+forward*(upper*.9f)-up*lower,seated);
            if(upper<.001f || lower<.001f)return;
            var delta=target-thigh.position;var axis=delta.sqrMagnitude>1e-8f?delta.normalized:-up;
            float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(upper-lower)+.001f,upper+lower-.001f);
            var reachable=thigh.position+axis*distance;var bend=Vector3.ProjectOnPlane(forward,axis).normalized;
            if(bend.sqrMagnitude<.01f)bend=Vector3.ProjectOnPlane(up,axis).normalized;
            float along=(upper*upper-lower*lower+distance*distance)/(2*distance);
            var knee=thigh.position+axis*along+bend*Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
            thigh.rotation=Quaternion.FromToRotation(shin.position-thigh.position,knee-thigh.position)*thigh.rotation;
            shin.rotation=Quaternion.FromToRotation(ankle.position-shin.position,reachable-shin.position)*shin.rotation;
            // The source has independent Foot bones under Root; keep each attached to its solved ankle.
            foot.position=ankle.position+footOffset;
        }
        private void SleepArm(Arm arm,Quaternion rotation,float seated,float resting)
        {
            if(resting<=0)return;
            var side=arm.Upper.position-HandReferencePosition;
            float reach=Vector3.Distance(arm.Upper.position,arm.Lower.position)+Vector3.Distance(arm.Lower.position,arm.Wrist.position);
            var relaxed=arm.Upper.position+rotation*new Vector3(0,-reach*.78f,.07f);
            var thigh=_body.position+side*.9f+rotation*new Vector3(0,.11f,.26f);
            var target=Vector3.Lerp(arm.Wrist.position,Vector3.Lerp(relaxed,thigh,seated),resting);
            SolveArm(arm,target,side.normalized*.65f-rotation*Vector3.up*.3f-rotation*Vector3.forward*.15f);
        }
        private Vector3 _posedSleepEye;
        public Vector3 SleepPoseEye=>_posedSleepEye;
    }
}
