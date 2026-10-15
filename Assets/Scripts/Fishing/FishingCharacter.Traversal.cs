using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingCharacter
    {
        public void ApplyTraversalPose(bool swimming, bool climbing, float speed, bool tracked, bool paused = false)
        {
            if (!Ready || (!swimming && !climbing)) return;
            float activity = Mathf.Clamp01(speed / 1.45f);
            float cycle = Time.time * (climbing ? 4 : 3.2f), wave = Mathf.Sin(cycle) * (swimming ? Mathf.Lerp(.18f, 1, activity) : speed > .1f ? 1 : 0);
            if (swimming)
            {
                // Rotate the whole source rig, including its independent foot bones, around a stable eye anchor.
                float pitch = Mathf.Lerp(68, 88, activity);
                var eye = EyePosition; var headEye = _head.InverseTransformPoint(eye);
                var tilt = Quaternion.AngleAxis(pitch, transform.right);
                _model.SetPositionAndRotation(eye + tilt * (_model.position - eye), tilt * _model.rotation);
                _head.rotation = Quaternion.AngleAxis(8 - pitch, transform.right) * _head.rotation;
                _model.position += eye - _head.TransformPoint(headEye);
                SwimKick(_leftThigh, _leftAnkle, _leftFoot, wave * 6);
                SwimKick(_rightThigh, _rightAnkle, _rightFoot, -wave * 6);
            }
            else _body.rotation = Quaternion.AngleAxis(-8, transform.right) * _body.rotation;
            if (!tracked)
            {
                var center = HandReferencePosition;
                foreach (bool left in new[] { true, false })
                {
                    var arm = left ? _leftArm : _rightArm; float sign = left ? -1 : 1;
                    var target = center + transform.right * sign * (climbing ? .23f : .31f) + transform.forward * (climbing ? .37f : .36f + wave * sign * .16f)
                        + transform.up * (climbing ? .15f + wave * sign * .18f : -.10f);
                    SolveArm(arm, target, transform.right * sign - transform.up * .5f);
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
