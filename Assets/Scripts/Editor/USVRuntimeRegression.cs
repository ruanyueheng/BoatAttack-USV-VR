using System;
using System.Reflection;
using BoatAttack.UI;
using TMPro;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    /// <summary>Tests real components in edit mode without changing or saving project assets.</summary>
    public static class USVRuntimeRegression
    {
        private static int _passed, _failed;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch-only regression runner.");
            _passed = _failed = 0;
            Check("Enum upper bound", () => WithEnum(s => { s.CurrentOption = 2; Require(s.CurrentOption == 1, "Must stop at last option."); }));
            Check("Enum negative bound", () => WithEnum(s => { s.CurrentOption = -9; Require(s.CurrentOption == 0, "Must stop at first option."); }));
            Check("Enum empty options", () => WithEnum(s => { s.options = new string[0]; s.NextOption(); Require(s.text.text == "", "Empty options should clear the label."); }));
            Check("Enum null options", () => WithEnum(s => { s.options = null; s.NextOption(); Require(s.CurrentOption == 0, "Null list must stay neutral."); }));
            Check("Enum initial option", () => WithEnum(s => { s.startOption = 20; Invoke(s, "Awake"); Require(s.CurrentOption == 1, "Initial option must be clamped too."); }));
            Check("Enum looping", () => WithEnum(s => { s.loop = true; s.CurrentOption = -1; Require(s.CurrentOption == 1, "Negative loop must wrap."); s.NextOption(); Require(s.CurrentOption == 0, "Upper loop must wrap."); }));
            Check("Color upper bound", () => WithColor(s => { ConstantData.GetPaletteColor(0); s.CurrentOption = ConstantData.ColorPalette.Length; Require(s.CurrentOption == ConstantData.ColorPalette.Length - 1, "Must stop at last color."); }));
            Check("Color cold palette", () => WithColor(s => { ConstantData.ColorPalette = null; s.CurrentOption = 0; Require(ConstantData.ColorPalette != null && ConstantData.ColorPalette.Length > 0, "Initialize palette before accessing its count."); }));
            Check("Color initial option", () => WithColor(s => { s.startOption = int.MaxValue; Invoke(s, "Awake"); Require(s.CurrentOption == ConstantData.ColorPalette.Length - 1, "Initial color must be clamped."); }));
            Check("Menu subscriptions are balanced", CheckMenuSubscriptions);
            Check("Engine accepts audio sources without clips", () => WithEngine(e => { e.engineSound = e.gameObject.AddComponent<AudioSource>(); e.waterSound = e.gameObject.AddComponent<AudioSource>(); Invoke(e, "Awake"); }));
            Check("Engine sampling survives re-enable", () => WithEngine(e => { Invoke(e, "Awake"); Invoke(e, "OnEnable"); Require(SamplesCreated(e), "Sample buffer not created."); Invoke(e, "OnDisable"); Invoke(e, "OnEnable"); Require(SamplesCreated(e), "Sample buffer was disposed and never recreated."); }));
            Check("Engine can update with optional audio absent", () => WithEngine(e => { e.RB = e.gameObject.AddComponent<Rigidbody>(); Invoke(e, "Awake"); Invoke(e, "OnEnable"); Invoke(e, "FixedUpdate"); }));
            Check("Engine steering uses one fixed timestep", () => WithEngine(e => {
                Field(e, "_yHeight").SetValue(e, -1f); // Do not apply forces in edit mode.
                var velocity = 0f;
                var expected = Mathf.SmoothDampAngle(0f, -60f, ref velocity, 0.5f, 10f, Time.fixedDeltaTime);
                e.Turn(1f);
                Require(Mathf.Abs(Mathf.DeltaAngle(expected, e.transform.localEulerAngles.y)) < 0.0001f, "Steering must not use total elapsed simulation time.");
            }));
            Debug.Log($"[USV-REGRESSION] TOTAL: {_passed} passed, {_failed} failed. Edit-mode component checks only.");
            if (_failed == 0) Debug.Log("[USV-REGRESSION] PASS: component regression checks.");
            EditorApplication.Exit(_failed == 0 ? 0 : 1);
        }

        private static void CheckMenuSubscriptions()
        {
            var root = NewInactive("Menu subscriptions");
            try
            {
                var menu = root.AddComponent<MainMenuHelper>();
                menu.levelSelector = root.AddComponent<EnumSelector>();
                menu.lapSelector = root.AddComponent<EnumSelector>();
                menu.reverseSelector = root.AddComponent<EnumSelector>();
                menu.boatHullSelector = root.AddComponent<EnumSelector>();
                menu.boatPrimaryColorSelector = root.AddComponent<ColorSelector>();
                menu.boatTrimColorSelector = root.AddComponent<ColorSelector>();
                for (int i = 0; i < 3; i++)
                {
                    Invoke(menu, "OnEnable");
                    Require(menu.levelSelector.updateVal.GetInvocationList().Length == 1
                        && menu.lapSelector.updateVal.GetInvocationList().Length == 1
                        && menu.reverseSelector.updateVal.GetInvocationList().Length == 1
                        && menu.boatHullSelector.updateVal.GetInvocationList().Length == 1
                        && menu.boatPrimaryColorSelector.updateVal.GetInvocationList().Length == 1
                        && menu.boatTrimColorSelector.updateVal.GetInvocationList().Length == 1, "Opening the panel duplicated handlers.");
                    Invoke(menu, "OnDisable");
                    Require(menu.levelSelector.updateVal == null && menu.lapSelector.updateVal == null
                        && menu.reverseSelector.updateVal == null && menu.boatHullSelector.updateVal == null
                        && menu.boatPrimaryColorSelector.updateVal == null && menu.boatTrimColorSelector.updateVal == null,
                        "Closing the panel retained handlers.");
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static GameObject NewInactive(string name)
        {
            var root = new GameObject(name);
            root.SetActive(false);
            return root;
        }

        private static void WithEnum(Action<EnumSelector> test)
        {
            var root = NewInactive("Enum regression");
            try
            {
                var label = new GameObject("Label", typeof(RectTransform));
                label.transform.SetParent(root.transform);
                var selector = root.AddComponent<EnumSelector>();
                selector.options = new[] { "First", "Last" };
                selector.text = label.AddComponent<TextMeshProUGUI>();
                test(selector);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void WithColor(Action<ColorSelector> test)
        {
            var palette = ConstantData.ColorPalette;
            var root = NewInactive("Color regression");
            try { test(root.AddComponent<ColorSelector>()); }
            finally { ConstantData.ColorPalette = palette; Object.DestroyImmediate(root); }
        }

        private static void WithEngine(Action<Engine> test)
        {
            var root = NewInactive("Engine regression");
            var engine = root.AddComponent<Engine>();
            try { test(engine); }
            finally
            {
                if (SamplesCreated(engine)) Invoke(engine, "OnDisable");
                Object.DestroyImmediate(root);
            }
        }

        private static bool SamplesCreated(Engine engine) => ((NativeArray<float3>)Field(engine, "_point").GetValue(engine)).IsCreated;
        private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, Private);
        private static void Invoke(object target, string name) => target.GetType().GetMethod(name, Private)?.Invoke(target, null);
        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void Check(string name, Action action)
        {
            string failure = null;
            Application.LogCallback capture = (message, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    failure = failure ?? message;
            };
            Application.logMessageReceived += capture;
            try { action(); }
            catch (Exception exception) { failure = exception.GetBaseException().Message; }
            finally { Application.logMessageReceived -= capture; }
            if (failure == null) { _passed++; Debug.Log("[USV-REGRESSION] PASS " + name); }
            else { _failed++; Debug.LogError("[USV-REGRESSION] FAIL " + name + ": " + failure); }
        }
    }
}
