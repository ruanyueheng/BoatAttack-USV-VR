using System;
using BoatAttack.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    /// <summary>Batch-only real Play Mode flow; no synthetic input and no visual/device assertions.</summary>
    [InitializeOnLoad]
    public static class USVPlayModeSmoke
    {
        private const string Prefix = "BoatAttack.PlayModeSmoke.";
        private const string Menu = "Assets/scenes/main_menu.unity";
        private const string Island = "Assets/scenes/_levels/level_Island.unity";
        private static int _phase, _cycles;
        private static float _nextAction;
        private static Engine _engine;

        static USVPlayModeSmoke()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += Tick;
            Application.logMessageReceived += CaptureError;
        }

        private static bool Active => SessionState.GetBool(Prefix + "Active", false);

        public static void Run()
        {
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Start the smoke test in a closed, batch editor only.");
            SessionState.SetString(Prefix + "Failure", "");
            SessionState.SetBool(Prefix + "Stopping", false);
            SessionState.SetFloat(Prefix + "Deadline", (float)EditorApplication.timeSinceStartup + 240f);
            SessionState.SetBool(Prefix + "Active", true);
            EditorSceneManager.OpenScene(Menu);
            EditorApplication.EnterPlaymode();
        }

        private static void CaptureError(string message, string stack, LogType type)
        {
            if (!Active || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
            if (string.IsNullOrEmpty(SessionState.GetString(Prefix + "Failure", "")))
                SessionState.SetString(Prefix + "Failure", message);
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!Active) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                _phase = _cycles = 0;
                _engine = null;
                Debug.Log("[USV-PLAYMODE] Entered real Play Mode.");
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                if (!SessionState.GetBool(Prefix + "Stopping", false))
                    SessionState.SetString(Prefix + "Failure", "Play Mode ended before checks completed.");
                Finish();
            }
        }

        private static void Tick()
        {
            if (!Active || SessionState.GetBool(Prefix + "Stopping", false)) return;
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Prefix + "Deadline", 0f))
            {
                Fail("Timed out in smoke phase " + _phase);
                return;
            }
            if (!string.IsNullOrEmpty(SessionState.GetString(Prefix + "Failure", ""))) { Stop(); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
            try
            {
                var path = SceneManager.GetActiveScene().path;
                switch (_phase)
                {
                    case 0:
                        if (path != Menu || AppSettings.Instance == null || RaceManager.Instance == null) return;
                        var menus = Object.FindObjectsOfType<MainMenuHelper>(true);
                        if (menus.Length == 0) return;
                        _phase = 1;
                        menus[0].SetupSingleplayerGame();
                        menus[0].StartRace();
                        Debug.Log("[USV-PLAYMODE] Requested single-player race through MainMenuHelper.");
                        break;
                    case 1:
                        if (path != Island || !RaceManager.RaceStarted) return;
                        Require(RaceManager.RaceData.boats.Count == 4, "Expected the existing four-boat race.");
                        foreach (var boat in RaceManager.RaceData.boats)
                            Require(boat.Boat != null && boat.Boat.engine != null && boat.Boat.engine.RB != null,
                                "An Addressable boat did not initialize.");
                        var player = RaceManager.RaceData.boats[0].Boat;
                        Require(player.GetComponent<HumanController>() != null, "Player controller missing.");
                        _engine = player.engine;
                        _engine.enabled = false;
                        _nextAction = Time.realtimeSinceStartup + 0.1f;
                        _phase = 2;
                        Debug.Log("[USV-PLAYMODE] Island loaded, four boats initialized and countdown completed.");
                        break;
                    case 2:
                        if (Time.realtimeSinceStartup < _nextAction) return;
                        _engine.enabled = true;
                        _nextAction = Time.realtimeSinceStartup + 0.25f;
                        _phase = 3;
                        break;
                    case 3:
                        if (Time.realtimeSinceStartup < _nextAction) return;
                        Require(_engine != null && _engine.enabled, "Engine did not survive re-enable.");
                        Debug.Log("[USV-PLAYMODE] Engine disabled/re-enabled across live physics frames.");
                        _cycles++;
                        if (_cycles == 2) { Stop(); return; }
                        _phase = 4;
                        RaceManager.UnloadRace();
                        break;
                    case 4:
                        if (path == "Assets/scenes/demo_Island.unity")
                            throw new Exception("UnloadRace loaded demo_Island via build index 0, not the main menu.");
                        if (path != Menu) return;
                        Debug.Log("[USV-PLAYMODE] Returned to main menu; starting a second race.");
                        _phase = 0;
                        break;
                }
            }
            catch (Exception exception) { Fail(exception.ToString()); }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Fail(string message)
        {
            SessionState.SetString(Prefix + "Failure", message);
            Stop();
        }

        private static void Stop()
        {
            SessionState.SetBool(Prefix + "Stopping", true);
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            else if (!EditorApplication.isPlayingOrWillChangePlaymode) Finish();
        }

        private static void Finish()
        {
            var failure = SessionState.GetString(Prefix + "Failure", "");
            SessionState.SetBool(Prefix + "Active", false);
            if (string.IsNullOrEmpty(failure))
                Debug.Log("[USV-PLAYMODE] PASS: menu/race/menu/race, Addressable boats, countdown and engine re-enable; no visual, keyboard/gamepad, build or headset acceptance.");
            else Debug.LogError("[USV-PLAYMODE] FAIL: " + failure);
            EditorApplication.Exit(string.IsNullOrEmpty(failure) ? 0 : 1);
        }
    }
}
