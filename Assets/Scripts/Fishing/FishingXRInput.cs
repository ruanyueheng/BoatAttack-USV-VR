using UnityEngine;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    /// <summary>OpenXR/PICO controller reads with analog fallbacks used by the whole VR game.</summary>
    public static class FishingXRInput
    {
        public static bool Grip(InputDevice device, float threshold = .55f)
        {
            bool pressed = false;
            if (device.TryGetFeatureValue(CommonUsages.gripButton, out var button)) pressed |= button;
            if (device.TryGetFeatureValue(CommonUsages.grip, out var value)) pressed |= value >= threshold;
            return pressed;
        }

        public static bool Trigger(InputDevice device, float threshold = .55f)
        {
            bool pressed = false;
            if (device.TryGetFeatureValue(CommonUsages.triggerButton, out var button)) pressed |= button;
            if (device.TryGetFeatureValue(CommonUsages.trigger, out var value)) pressed |= value >= threshold;
            return pressed;
        }

        public static Vector2 Stick(InputDevice device)
        {
            return device.TryGetFeatureValue(CommonUsages.primary2DAxis, out var axis) ? axis : Vector2.zero;
        }
        public static bool StickClick(InputDevice device)
        {
            bool pressed=device.TryGetFeatureValue(CommonUsages.primary2DAxisClick,out bool primary)&&primary;
            if(device.TryGetFeatureValue(new InputFeatureUsage<bool>("thumbstickClick"),out bool named))pressed|=named;
            return pressed;
        }

        public static bool Menu(InputDevice device)
        {
            if (device.TryGetFeatureValue(CommonUsages.menuButton, out var menu) && menu) return true;
            return device.TryGetFeatureValue(new InputFeatureUsage<bool>("menu"), out var picoMenu) && picoMenu;
        }
    }
}
