using UnityEngine;
using UnityEngine.InputSystem;

namespace BoatAttack.VR
{
    public sealed class USVDesktopInputSource : IUSVInputSource
    {
        public USVInputState Read()
        {
            var s = new USVInputState();
            var k = Keyboard.current;
            if (k != null)
            {
                // Preserve BoatAttack's Space = forward binding; Shift is the new brake.
                s.throttle = ((k.wKey.isPressed || k.upArrowKey.isPressed || k.spaceKey.isPressed) ? 1f : 0f)
                    - ((k.sKey.isPressed || k.downArrowKey.isPressed || k.zKey.isPressed) ? 1f : 0f);
                s.steering = ((k.dKey.isPressed || k.rightArrowKey.isPressed) ? 1f : 0f)
                    - ((k.aKey.isPressed || k.leftArrowKey.isPressed) ? 1f : 0f);
                s.brake = (k.leftShiftKey.isPressed || k.rightShiftKey.isPressed) ? 1f : 0f;
                s.reset = k.rKey.isPressed;
                s.pause = k.escapeKey.isPressed;
                s.interact = k.eKey.isPressed || k.enterKey.isPressed;
                s.map = k.mKey.isPressed;
            }
            var g = Gamepad.current;
            if (g != null)
            {
                var stick = g.leftStick.ReadValue();
                var throttle = g.rightTrigger.ReadValue() - g.leftTrigger.ReadValue();
                if (g.buttonSouth.isPressed) throttle = 1f;
                if (g.buttonEast.isPressed) throttle = -1f;
                if (Mathf.Abs(stick.y) > Mathf.Abs(throttle)) throttle = stick.y;
                if (Mathf.Abs(throttle) > Mathf.Abs(s.throttle)) s.throttle = throttle;
                if (Mathf.Abs(stick.x) > Mathf.Abs(s.steering)) s.steering = stick.x;
                s.brake = Mathf.Max(s.brake, g.leftShoulder.isPressed ? 1f : 0f);
                s.reset |= g.selectButton.isPressed;
                s.pause |= g.startButton.isPressed;
                s.interact |= g.buttonWest.isPressed;
                s.map |= g.buttonNorth.isPressed;
            }
            return s;
        }
    }
}
