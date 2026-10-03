using System;
using System.Collections;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    [InitializeOnLoad]
    public static class FishingShopValidation
    {
        private const string Key = "Fishing.ShopValidation.";
        private static IEnumerator _checks;
        private static double _next;
        private static int _lastCheckFrame=-1;
        static FishingShopValidation()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (SessionState.GetBool(Key + "active", false) && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                    SessionState.SetString(Key + "error", message);
            };
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key + "active", false)) return;
                SessionState.SetBool(Key + "active", false);
                string error = SessionState.GetString(Key + "error", "");
                if (!SessionState.GetBool(Key + "finished", false) && error == "") error = "Incomplete shop checks";
                if (error == "") Debug.Log("[BEACH-SHOP] PASS: real shop route, counter-only upgrades, input modal isolation, ten waypoints and v8/workshop save regression; simulated desktop input, no NPC AI or PICO hardware claim.");
                else Debug.LogError("[BEACH-SHOP] FAIL: " + error);
                EditorApplication.Exit(error == "" ? 0 : 1);
            };
        }
        public static void Run()
        {
            SessionState.SetBool(Key + "active", true); SessionState.SetBool(Key + "finished", false); SessionState.SetString(Key + "error", "");
            SessionState.SetFloat(Key + "deadline", (float)EditorApplication.timeSinceStartup + 300);
            FishingScriptBinding.ValidateScene();
            EditorSceneManager.OpenScene(FishingPrototypeEditor.ScenePath); EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            if (!SessionState.GetBool(Key + "active", false)) return;
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + "deadline", 0)) SessionState.SetString(Key + "error", "Shop validation timed out");
            if (SessionState.GetString(Key + "error", "") != "")
            {
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
                else if (!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.Exit(1);
                return;
            }
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < _next || Time.frameCount == _lastCheckFrame) return;
            var c = UnityEngine.Object.FindObjectOfType<FishingPrototype>();
            if (c == null || c.WorkshopWorld == null) return;
            try
            {
                if (_checks == null) _checks = FishingAutoplay.ValidateBeachShop(c);
                _next = EditorApplication.timeSinceStartup + .02;
                _lastCheckFrame = Time.frameCount;
                if (!_checks.MoveNext()) { SessionState.SetBool(Key + "finished", true); EditorApplication.ExitPlaymode(); }
            }
            catch (Exception ex)
            {
                SessionState.SetString(Key + "error", ex.ToString());
                (_checks as IDisposable)?.Dispose();
                EditorApplication.ExitPlaymode();
            }
        }
    }
}
