using System;

namespace BoatAttack.VR
{
    public enum USVInputMode { Auto, Desktop, XR }
    public enum USVInputSourceKind { None, Desktop, XR }

    [Serializable]
    public struct USVInputState
    {
        public float throttle, steering, brake;
        // Sources return held buttons; the processor converts them to one-frame presses.
        public bool reset, pause, interact, map;
    }

    public interface IUSVInputSource
    {
        USVInputState Read();
    }
}
