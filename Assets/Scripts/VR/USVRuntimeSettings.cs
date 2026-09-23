using UnityEngine;

namespace BoatAttack.VR
{
    [CreateAssetMenu(menuName = "BoatAttack/Dual-mode settings")]
    public sealed class USVRuntimeSettings : ScriptableObject
    {
        public USVInputMode editorMode = USVInputMode.Desktop;
        public USVInputMode windowsMode = USVInputMode.Desktop;
        public USVInputMode androidMode = USVInputMode.XR;
        [Range(0f, 0.5f)] public float deadzone = 0.12f;
        [Min(0f)] public float brakeDeceleration = 3f;

        public USVInputMode Mode
        {
            get
            {
#if UNITY_EDITOR
                return editorMode;
#elif UNITY_ANDROID
                return androidMode;
#else
                return windowsMode;
#endif
            }
        }
    }
}
