using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BoatAttack.Fishing
{
    /// <summary>Opt-in desktop acceptance; never attached by a normal player launch.</summary>
    [DefaultExecutionOrder(150)]
    public sealed partial class FishingAcceptance : MonoBehaviour
    {
        public const float TimeoutSeconds = 180f;
        public FishingPrototype Controller;
        public static bool Completed { get; private set; }
        public static string Failure { get; private set; }
        public FishingAcceptanceCase CurrentCase { get; private set; }
        public bool FreezeScenario { get; private set; }
        public string Status { get; private set; } = string.Empty;
        public bool KeepCatchCardVisible => FreezeScenario && CurrentCase == FishingAcceptanceCase.Catch
            && Controller.Session.Phase == CatchPhase.Caught;
        private float _started;
        private FishingAcceptanceOptions Options => Controller.AcceptanceOptions;

        public static void ResetValidation() { Completed = false; Failure = null; }

        private void Start()
        {
            ResetValidation();
            _started = Time.realtimeSinceStartup;
            Application.logMessageReceived += CaptureError;
            try
            {
                if (Options == null || !Controller.HasIsolatedProfile || !Controller.View.OwnsCamera)
                    throw new InvalidOperationException("Desktop acceptance requires its own profile and a desktop camera.");
                if (Options.Automatic && !Controller.QuietMode) Controller.ToggleQuiet();
                SelectCase(Options.InitialCase);
                Debug.Log("[FISHING-ACCEPTANCE] Ready: session=" + Options.SessionId + " automatic=" + Options.Automatic
                    + " realInput=" + !Controller.Automated + " profile=" + Controller.SavePath);
                if (Options.Automatic) StartCoroutine(RunCases(Options.CaptureAll));
            }
            catch (Exception ex) { Fail(ex); }
        }

        private void Update()
        {
            if (Completed || Failure != null) return;
            try
            {
                if (Options.Automatic)
                {
                    if (Time.realtimeSinceStartup - _started > TimeoutSeconds)
                        throw new TimeoutException("Acceptance run exceeded its deadline at " + CurrentCase + ".");
                    return;
                }
                var k = Keyboard.current;
                if (!Application.isFocused || k == null || IsCapturing) return;
                if (k.f5Key.wasPressedThisFrame) SelectCase(FishingAcceptanceCases.Cycle(CurrentCase, -1));
                else if (k.f6Key.wasPressedThisFrame) SelectCase(FishingAcceptanceCases.Cycle(CurrentCase, 1));
                else if (k.f9Key.wasPressedThisFrame) SelectCase(FishingAcceptanceCase.FreePlay);
                if (k.f7Key.wasPressedThisFrame) ToggleFreeze();
                if (k.f8Key.wasPressedThisFrame) StartCoroutine(CaptureFrame());
            }
            catch (Exception ex) { Fail(ex); }
        }

        public void SelectCase(FishingAcceptanceCase selected)
        {
            FishingAcceptanceCases.Title(selected); // validate before changing the current profile
            CurrentCase = selected;
            FreezeScenario = selected != FishingAcceptanceCase.FreePlay;
            Controller.ApplyAcceptanceCase(selected);
            Status = selected == FishingAcceptanceCase.FreePlay
                ? "真实操作 · 验收进度可续玩；F5/F6 的预置场景使用另一份存档"
                : "预置场景 · F7 解除阶段定格后可实玩；F9 返回自己的验收进度";
            Debug.Log("[FISHING-ACCEPTANCE] Case: " + selected + " phase=" + Controller.Session.Phase
                + " profile=" + System.IO.Path.GetFileName(Controller.SavePath));
        }

        public void ToggleFreeze()
        {
            if (CurrentCase == FishingAcceptanceCase.FreePlay) return;
            FreezeScenario = !FreezeScenario;
            Status = FreezeScenario ? "阶段已定格 · 仅冻结捕捞计时，不代表手感验收" : "阶段已解除定格 · 使用真实按键继续当前场景";
        }

        private void Finish()
        {
            Completed = true;
            Debug.Log("[FISHING] PASS: desktop acceptance cases, isolated profiles and production-rule state checks completed. Physical input is not certified.");
            if (!Application.isEditor) Application.Quit(0);
        }

        private void Fail(Exception ex)
        {
            if (Failure != null) return;
            Failure = ex.ToString();
            Status = "验收失败，请查看日志；没有触碰普通玩家存档";
            Debug.LogError("[FISHING-ACCEPTANCE] FAIL: " + Failure);
            if (Options != null && Options.Automatic && !Application.isEditor) Application.Quit(1);
        }

        private void CaptureError(string message, string stack, LogType type)
        {
            if (!Completed && Failure == null && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                Fail(new InvalidOperationException(message));
        }

        private void OnDestroy() { Application.logMessageReceived -= CaptureError; }
    }
}
