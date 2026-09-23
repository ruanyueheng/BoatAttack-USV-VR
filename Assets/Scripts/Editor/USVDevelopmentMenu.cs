using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using BoatAttack.VR;

namespace BoatAttack.EditorTools
{
    public static class USVDevelopmentMenu
    {
        private const string SettingsPath = "Assets/Resources/USVRuntimeSettings.asset";

        [MenuItem("BoatAttack/Development/Open Island (save prompt)")]
        private static void OpenIsland()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play Mode before opening the scene."); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene("Assets/scenes/demo_Island.unity");
        }

        [MenuItem("BoatAttack/Development/Select dual-mode input settings")]
        private static void SelectSettings()
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<USVRuntimeSettings>(SettingsPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        [MenuItem("BoatAttack/Development/Input/Desktop (no headset)")]
        private static void Desktop() { SetEditorMode(USVInputMode.Desktop); }
        [MenuItem("BoatAttack/Development/Input/XR (requires initialized loader and rig)")]
        private static void XR() { SetEditorMode(USVInputMode.XR); }
        [MenuItem("BoatAttack/Development/Input/Auto (input only)")]
        private static void Auto() { SetEditorMode(USVInputMode.Auto); }

        private static void SetEditorMode(USVInputMode mode)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Stop Play Mode before changing defaults."); return; }
            var settings = AssetDatabase.LoadAssetAtPath<USVRuntimeSettings>(SettingsPath);
            if (settings == null) { Debug.LogError("Missing dual-mode settings asset."); return; }
            Undo.RecordObject(settings, "Set editor input mode");
            settings.editorMode = mode;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("[USV] Editor input = " + mode + ". XR loader, rig and camera are NOT configured by this menu.");
        }
    }
}
