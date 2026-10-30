using System;

namespace BoatAttack.VR
{
    /// <summary>Pure C# rules, shared by runtime and hardware-free regression tests.</summary>
    public sealed class USVInputProcessor
    {
        private USVInputState _previous;
        private USVInputSourceKind _source = USVInputSourceKind.None;

        public static USVInputSourceKind Resolve(USVInputMode mode, bool xrActive, bool focused)
        {
            if (!focused) return USVInputSourceKind.None;
            if (mode == USVInputMode.Desktop) return USVInputSourceKind.Desktop;
            if (xrActive) return USVInputSourceKind.XR;
            // An Android/XR session must not silently become a flat-screen game.
            return mode == USVInputMode.XR ? USVInputSourceKind.None : USVInputSourceKind.Desktop;
        }

        public USVInputState Process(USVInputState raw, USVInputSourceKind source, float deadzone)
        {
            if (source == USVInputSourceKind.None) { Reset(); return default(USVInputState); }
            if (_source != source) _previous = raw; // held buttons on reconnect are not clicks
            var state = raw;
            state.throttle = Axis(raw.throttle, deadzone);
            state.steering = Axis(raw.steering, deadzone);
            // Resting analog triggers can report tiny nonzero values that otherwise disable boost continuously.
            state.brake = Math.Max(0f, Axis(raw.brake, Math.Min(.03f,Math.Max(0f,Finite(deadzone)))));
            state.boost = raw.boost && state.throttle > 0f && state.brake == 0f;
            state.reset = raw.reset && !_previous.reset;
            state.pause = raw.pause && !_previous.pause;
            state.interact = raw.interact && !_previous.interact;
            state.map = raw.map && !_previous.map;
            _previous = raw;
            _source = source;
            return state;
        }

        public void Reset() { _source = USVInputSourceKind.None; _previous = default(USVInputState); }
        private static float Finite(float x) { return float.IsNaN(x) || float.IsInfinity(x) ? 0f : x; }
        private static float Axis(float x, float deadzone)
        {
            x = Math.Max(-1f, Math.Min(1f, Finite(x)));
            deadzone = Math.Max(0f, Math.Min(0.95f, Finite(deadzone)));
            if (Math.Abs(x) <= deadzone) return 0f;
            return Math.Sign(x) * (Math.Abs(x) - deadzone) / (1f - deadzone);
        }
    }
}
