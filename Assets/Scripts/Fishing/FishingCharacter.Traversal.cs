using UnityEngine;
namespace BoatAttack.Fishing
{
    public sealed partial class FishingCharacter
    {
        public void ApplyTraversalPose(bool swimming, bool climbing, float speed, bool tracked)
        {
            if (!Ready) return;
            float cycle = Time.time * (climbing ? 4 : 3.2f), wave = speed > .1f ? Mathf.Sin(cycle) : 0;
            var leftFoot = _leftAnkle.position; var rightFoot = _rightAnkle.position;
            _body.rotation = Quaternion.AngleAxis(swimming ? 22 : -8, transform.right) * _body.rotation;
            if (swimming)
            {
                SolveLeg(_leftThigh, _leftShin, _leftAnkle, leftFoot - transform.forward * (.22f + wave * .12f));
                SolveLeg(_rightThigh, _rightShin, _rightAnkle, rightFoot - transform.forward * (.22f - wave * .12f));
            }
            if (tracked) return;
            var center = HandReferencePosition;
            foreach (bool left in new[] { true, false })
            {
                var arm = left ? _leftArm : _rightArm; float sign = left ? -1 : 1;
                var target = center + transform.right * sign * (climbing ? .23f : .37f) + transform.forward * (climbing ? .37f : .43f + wave * sign * .13f)
                    + transform.up * (climbing ? .15f + wave * sign * .18f : -.32f);
                SolveArm(arm, target, transform.right * sign - transform.up * .5f);
            }
        }
    }
}
