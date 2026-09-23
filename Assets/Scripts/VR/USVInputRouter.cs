using System;
using UnityEngine;
using UnityEngine.XR;

namespace BoatAttack.VR
{
    [DefaultExecutionOrder(-900)]
    public sealed class USVInputRouter : MonoBehaviour
    {
        public static USVInputRouter Instance { get; private set; }
        public USVInputState Current { get; private set; }
        public USVInputSourceKind ActiveSource { get; private set; }
        public USVRuntimeSettings Settings { get; private set; }
        public event Action InteractPressed;
        public event Action MapPressed;

        private readonly USVDesktopInputSource _desktop = new USVDesktopInputSource();
        private readonly USVXRInputSource _xr = new USVXRInputSource();
        private readonly USVInputProcessor _processor = new USVInputProcessor();
        private bool _focused = true;
        private bool _suspended;
        private bool _ownsSettings;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() { Instance = null; }

        public static USVInputRouter EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindObjectOfType<USVInputRouter>();
            if (existing != null) { existing.Initialize(); return existing; }
            return new GameObject("USV Input Router").AddComponent<USVInputRouter>();
        }

        private void Awake() { Initialize(); }
        private void Initialize()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Settings == null)
            {
                Settings = Resources.Load<USVRuntimeSettings>("USVRuntimeSettings");
                if (Settings == null)
                {
                    Settings = ScriptableObject.CreateInstance<USVRuntimeSettings>();
                    _ownsSettings = true;
                }
            }
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (Settings == null) return;
            var source = USVInputProcessor.Resolve(Settings.Mode, XRSettings.isDeviceActive, _focused && !_suspended);
            if (source != ActiveSource)
            {
                Debug.Log("[USV] Input: " + source + " (input only; does not switch cameras or XR loaders)");
                ActiveSource = source;
            }
            var raw = source == USVInputSourceKind.Desktop ? _desktop.Read()
                : source == USVInputSourceKind.XR ? _xr.Read() : default(USVInputState);
            Current = _processor.Process(raw, source, Settings.deadzone);
            if (Current.interact) InteractPressed?.Invoke();
            if (Current.map) MapPressed?.Invoke();
        }

        private void ClearInput()
        {
            Current = default(USVInputState);
            ActiveSource = USVInputSourceKind.None;
            _processor.Reset();
        }
        private void OnApplicationFocus(bool focused) { _focused = focused; ClearInput(); }
        private void OnApplicationPause(bool paused) { _suspended = paused; ClearInput(); }
        private void OnDisable() { ClearInput(); }
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_ownsSettings && Settings != null) Destroy(Settings);
        }
    }
}
