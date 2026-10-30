using UnityEngine;
using UnityEngine.XR;
using BoatAttack.Fishing;

namespace BoatAttack.VR
{
    /// <summary>Provider-neutral Unity XR input. This does not install or initialize an XR loader.</summary>
    public sealed class USVXRInputSource : IUSVInputSource
    {
        private static bool Tracked(InputDevice d)
        {
            return d.isValid && d.TryGetFeatureValue(CommonUsages.isTracked, out var tracked) && tracked;
        }

        public USVInputState Read()
        {
            var s = new USVInputState();
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            // Both hands required for driving. Losing one stops propulsion rather than
            // leaving a moving boat without either throttle or steering control.
            var leftTracked = Tracked(left);
            var rightTracked = Tracked(right);
            if (leftTracked && rightTracked)
            {
                if (left.TryGetFeatureValue(CommonUsages.primary2DAxis, out var axis)) s.throttle = axis.y;
                if (right.TryGetFeatureValue(CommonUsages.primary2DAxis, out axis)) s.steering = axis.x;
                s.boost=FishingXRInput.StickClick(left)||FishingXRInput.Grip(left);
            }
            if (leftTracked)
            {
                left.TryGetFeatureValue(CommonUsages.trigger, out s.brake);
                left.TryGetFeatureValue(CommonUsages.primaryButton, out s.reset);
            }
            if (rightTracked)
            {
                s.interact=FishingXRInput.Trigger(right);
                right.TryGetFeatureValue(CommonUsages.primaryButton, out s.pause);
                right.TryGetFeatureValue(CommonUsages.secondaryButton, out s.map);
            }
            return s;
        }
    }
}
