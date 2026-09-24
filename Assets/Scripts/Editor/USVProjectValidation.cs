using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BoatAttack.VR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    /// <summary>Batch-only import/scene checks. This is not Play Mode, rendering or device validation.</summary>
    public static class USVProjectValidation
    {
        public static void Run()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Use Tools/Run-UnityValidation.ps1 with the interactive editor closed.");

            var errors = new List<string>();
            Application.LogCallback capture = (message, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    errors.Add(message);
            };
            Application.logMessageReceived += capture;
            try
            {
                var projectVersion = File.ReadAllText("ProjectSettings/ProjectVersion.txt");
                Require(projectVersion.Contains("m_EditorVersion: " + Application.unityVersion + "\n")
                    || projectVersion.Contains("m_EditorVersion: " + Application.unityVersion + "\r\n"),
                    "Editor does not match ProjectVersion.txt.");

                var settings = AssetDatabase.LoadAssetAtPath<USVRuntimeSettings>("Assets/Resources/USVRuntimeSettings.asset");
                Require(settings != null, "Missing shared input settings asset or script.");
                Require(settings.windowsMode == USVInputMode.Desktop, "Windows must work without a headset.");
                Require(settings.androidMode == USVInputMode.XR, "Android must not silently fall back to desktop.");
                Require(!float.IsNaN(settings.deadzone) && settings.deadzone >= 0f && settings.deadzone <= 0.5f,
                    "Default deadzone is outside the supported inspector range.");
                Require(!float.IsNaN(settings.brakeDeceleration) && !float.IsInfinity(settings.brakeDeceleration)
                    && settings.brakeDeceleration >= 0f, "Default braking must be finite and nonnegative.");
                Debug.Log("[USV-VALIDATION] Shared input settings loaded.");

                var buildScenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
                Require(buildScenes.Length > 0 && buildScenes[0] == "Assets/scenes/main_menu.unity",
                    "Desktop builds must start at the main menu, not a demo or benchmark.");
                Require(buildScenes.Contains("Assets/scenes/_levels/level_Island.unity"), "Island missing from enabled build scenes.");
                Debug.Log("[USV-VALIDATION] Desktop build scene entries include menu and island.");

                foreach (var path in new[] {
                    "Assets/scenes/main_menu.unity",
                    "Assets/scenes/demo_Island.unity",
                    "Assets/scenes/_levels/level_Island.unity"
                })
                {
                    Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null, "Scene asset not found: " + path);
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    Require(scene.IsValid() && scene.isLoaded, "Scene did not load: " + path);
                    int missing = 0;
                    foreach (var root in scene.GetRootGameObjects())
                        foreach (var child in root.GetComponentsInChildren<Transform>(true))
                            missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
                    Require(missing == 0, path + " contains " + missing + " missing script component(s).");
                    Debug.Log("[USV-VALIDATION] Scene opened without missing scripts: " + path);
                }
            }
            catch (Exception exception)
            {
                errors.Add(exception.ToString());
            }
            finally
            {
                Application.logMessageReceived -= capture;
            }

            if (errors.Count > 0)
            {
                foreach (var error in errors) Debug.LogError("[USV-VALIDATION] " + error);
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("[USV-VALIDATION] PASS: import, input settings and three scene structures only; no Play Mode, build, rendering or headset test.");
            EditorApplication.Exit(0);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
