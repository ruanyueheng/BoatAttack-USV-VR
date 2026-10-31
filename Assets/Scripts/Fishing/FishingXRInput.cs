using UnityEngine;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    /// <summary>OpenXR/PICO controller reads with analog fallbacks used by the whole VR game.</summary>
    public static class FishingXRInput
    {
        public const float HapticStrengthMultiplier=1.7f;
        public const float HapticDurationMultiplier=1.5f;
        public static float HapticAmplitude(float strength)=>float.IsNaN(strength) || float.IsInfinity(strength)?0:Mathf.Clamp(strength*HapticStrengthMultiplier,0,.95f);
        public static float HapticSeconds(float duration)=>float.IsNaN(duration) || float.IsInfinity(duration)?0:Mathf.Clamp(duration*HapticDurationMultiplier,0,.3f);
        public static bool Pulse(InputDevice device,float strength,float duration)
        {
            float amplitude=HapticAmplitude(strength),seconds=HapticSeconds(duration);
            if(amplitude<=0 || seconds<=0 || !Application.isFocused || !XRSettings.isDeviceActive
                || !device.TryGetHapticCapabilities(out var caps) || !caps.supportsImpulse)return false;
            return device.SendHapticImpulse(0,amplitude,seconds);
        }
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
