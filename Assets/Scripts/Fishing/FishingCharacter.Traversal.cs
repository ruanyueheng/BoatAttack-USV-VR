using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingCharacter
    {
        private float _swimWeight,_climbWeight,_traversalCycle,_traversalActivity;
        private int _traversalFrame=-1;
        public void ApplyTraversalPose(bool swimming, bool climbing, float speed, bool tracked, bool paused = false)
        {
            if (!Ready || (!swimming && !climbing && _swimWeight<=0 && _climbWeight<=0)) return;
            RestoreRigBase();
            if(_traversalFrame!=Time.frameCount)
            {
                float dt=paused?0:_presentationDelta;
                float amount=dt*5;
                _swimWeight=Mathf.MoveTowards(_swimWeight,swimming?1:0,amount);
                _climbWeight=Mathf.MoveTowards(_climbWeight,climbing?1:0,amount);
                if(_traversalFrame<0)_traversalActivity=Mathf.Clamp01(speed/1.45f);
                if(!paused)
                {
                    _traversalActivity=Mathf.MoveTowards(_traversalActivity,Mathf.Clamp01(speed/1.45f),dt*5);
                    _traversalCycle=Mathf.Repeat(_traversalCycle+dt*(climbing?4:3.2f)*Mathf.Max(_traversalActivity,swimming?.18f:0),Mathf.PI*2);
                }
                _traversalFrame=Time.frameCount;
            }
            float weight=Mathf.Max(_swimWeight,_climbWeight);
            if(weight<=0){CaptureArmBase(paused);return;}
            bool swim=_swimWeight>=_climbWeight;
            float activity = _traversalActivity;
            float wave = Mathf.Sin(_traversalCycle) * (swim ? Mathf.Lerp(.18f, 1, activity) : activity);
            if (swim)
            {
                // Rotate the whole source rig, including its independent foot bones, around a stable eye anchor.
                float pitch = Mathf.Lerp(68, 88, activity)*_swimWeight;
                var eye = EyePosition; var headEye = _head.InverseTransformPoint(eye);
                var tilt = Quaternion.AngleAxis(pitch, transform.right);
                _model.SetPositionAndRotation(eye + tilt * (_model.position - eye), tilt * _model.rotation);
                _head.rotation = Quaternion.AngleAxis(8*_swimWeight - pitch, transform.right) * _head.rotation;
                _model.position += eye - _head.TransformPoint(headEye);
                SwimKick(_leftThigh, _leftAnkle, _leftFoot, wave * 6*_swimWeight);
                SwimKick(_rightThigh, _rightAnkle, _rightFoot, -wave * 6*_swimWeight);
            }
            else
            {
                var leftTarget=_leftAnkle.position;var rightTarget=_rightAnkle.position;
                var leftOffset=_leftFoot.position-leftTarget;var rightOffset=_rightFoot.position-rightTarget;
                _body.rotation = Quaternion.AngleAxis(-8*_climbWeight, transform.right) * _body.rotation;
                SolveLeg(_leftThigh,_leftShin,_leftAnkle,leftTarget+transform.up*Mathf.Max(0,wave)*.12f*_climbWeight);
                SolveLeg(_rightThigh,_rightShin,_rightAnkle,rightTarget+transform.up*Mathf.Max(0,-wave)*.12f*_climbWeight);
                _leftFoot.position=_leftAnkle.position+leftOffset;_rightFoot.position=_rightAnkle.position+rightOffset;
            }
            if (!tracked)
            {
                var center = HandReferencePosition;
                foreach (bool left in new[] { true, false })
                {
                    var arm = left ? _leftArm : _rightArm; float sign = HandSide(left);
                    var target = center + transform.right * sign * (swim ? .31f : .23f) + transform.forward * (swim ? .36f + wave * sign * .16f : .37f)
                        + transform.up * (swim ? -.10f : .15f + wave * sign * .18f);
                    SolveArm(arm, Vector3.Lerp(arm.Wrist.position,target,weight), transform.right * sign - transform.up * .5f);
                }
            }
            CaptureArmBase(paused);
        }
        private void SwimKick(Transform thigh, Transform ankle, Transform foot, float degrees)
        {
            var before = ankle.position; var kick = Quaternion.AngleAxis(degrees, transform.right);
            thigh.rotation = kick * thigh.rotation;
            foot.SetPositionAndRotation(foot.position + ankle.position - before, kick * foot.rotation);
        }
    }
}
