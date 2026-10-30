using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    /// <summary>ADB opt-in tour of the actual XR player, with a temporary save and scripted gameplay commands.</summary>
    public sealed class FishingPicoTour : MonoBehaviour
    {
        public const string IntentKey = "fishing-pico-tour";
        private static bool? _requested;
        public static bool Requested
        {
            get
            {
                if (_requested.HasValue) return _requested.Value;
                bool requested = false;
#if UNITY_ANDROID && !UNITY_EDITOR
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
                    requested = intent.Call<bool>("getBooleanExtra", IntentKey, false);
#endif
                return (_requested = requested).Value;
            }
        }

        private FishingPrototype _c;
        private IEnumerator _steps;
        private float _deadline;
        private bool _failed, _done;

        public void Begin(FishingPrototype c)
        {
            _c = c;
            Require(Requested && c.Automated && c.HasIsolatedProfile, "Tour requires explicit opt-in and isolation.");
            Require(Path.GetFullPath(c.SavePath).StartsWith(Path.GetFullPath(Application.temporaryCachePath) + Path.DirectorySeparatorChar,
                StringComparison.Ordinal), "Tour save must stay inside the temporary cache.");
            Require(c.AcceptanceOptions == null, "Do not combine test drivers.");
            Application.logMessageReceived += OnLog;
            _steps = Run();
            _deadline = Time.realtimeSinceStartup + 480;
            Debug.Log("[PICO-TOUR] BEGIN version=" + FishingPrototype.Version + " profile=" + c.SavePath
                + "; scripted commands, real XR rendering; physical controller input is not certified.");
        }

        private void Update()
        {
            if (_done || _failed || _steps == null) return;
            try
            {
                Require(Time.realtimeSinceStartup < _deadline, "Tour timed out; last stage is recorded above.");
                if (!_steps.MoveNext())
                {
                    _done = true;
                    _c.AutomationReel = false;
                    Debug.Log("[PICO-TOUR] PASS: XR notebook, three fishing grounds, catch settlement, duplicate prevention and save roundtrip.");
                }
            }
            catch (Exception ex) { Fail(ex.ToString()); }
        }

        private IEnumerator Run()
        {
            Stage("waiting-for-focused-XR-and-coastal-depth");
            while (!Application.isFocused || !XRSettings.isDeviceActive || !_c.XRSeat.Ready || !_c.CoastalDepthReady) yield return null;
            foreach (int page in new[] { 0, 1, 2, 3, 7, 9, 12, 14 })
            {
                Stage("notebook-" + page);
                _c.XRNotebook.Open(page);
                Require(_c.XRNotebook.IsOpen, "XR notebook did not open.");
                float until = Time.realtimeSinceStartup + 3;
                while (Time.realtimeSinceStartup < until) yield return null;
                _c.XRNotebook.Close();
            }
            for (int ground = 0; ground < 3; ground++)
            {
                Stage("ground-" + ground);
                _c.LeaveStation();
                _c.MoveToSpotForAutomation(ground);
                _c.Interact();
                Require(_c.Anchored && _c.Cast(), "Could not anchor/cast at ground " + ground);
                float deadline = Time.realtimeSinceStartup + 110;
                while (_c.Session.Phase != CatchPhase.Caught)
                {
                    Require(Application.isFocused && XRSettings.isDeviceActive, "XR focus was lost during a measured stage.");
                    Require(Time.realtimeSinceStartup < deadline, "Fishing timed out at ground " + ground + " phase=" + _c.Session.Phase);
                    Require(_c.Session.Phase != CatchPhase.Escaped, "Fish escaped: " + _c.Session.Failure);
                    if (_c.Session.Phase == CatchPhase.Bite) _c.Hook();
                    _c.AutomationReel = _c.Session.Tension < .6f;
                    if (_c.Session.Phase == CatchPhase.Landing && _c.Session.CanLand)
                    {
                        string before = JsonUtility.ToJson(_c.Career.Snapshot());
                        Require(_c.CatchRig.CaptureAndStoreForAutomation(), "Geometric net capture/storage failed.");
                        Require(!_c.HasPendingCatch, "Catch settlement left the player stuck.");
                        string settled = JsonUtility.ToJson(_c.Career.Snapshot());
                        Require(before != settled && !_c.CatchRig.CaptureAndStoreForAutomation()
                            && settled == JsonUtility.ToJson(_c.Career.Snapshot()), "Catch did not settle exactly once.");
                    }
                    yield return null;
                }
                _c.AutomationReel = false;
                Stage("ground-" + ground + "-settled");
                float until = Time.realtimeSinceStartup + 5;
                while (Time.realtimeSinceStartup < until) yield return null;
            }
            _c.ReturnToHarbor();
            FishingSaveStore.Save(_c.SavePath, _c.Career);
            var loaded = FishingSaveStore.Load(_c.SavePath, out string error);
            Require(string.IsNullOrEmpty(error) && JsonUtility.ToJson(loaded.Snapshot()) == JsonUtility.ToJson(_c.Career.Snapshot()),
                "Isolated save did not roundtrip.");
        }


        private static void Require(bool ok, string message)
        { if (!ok) throw new InvalidOperationException(message); }
        private static void Stage(string stage) { Debug.Log("[PICO-TOUR] STAGE " + stage); }
        private void OnLog(string message, string stack, LogType type)
        {
            if (!_done && !_failed && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                Fail(message + "\n" + stack);
        }
        private void Fail(string message)
        {
            if (_failed) return;
            _failed = true;
            _c.AutomationReel = false;
            _c.XRNotebook.Close();
            Debug.LogError("[PICO-TOUR] FAIL: " + message);
        }
        private void OnDestroy() { Application.logMessageReceived -= OnLog; }
    }
}
