using System;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace BoatAttack.EditorTools
{
    /// <summary>Moving between an ASCII drive alias and the original path can leave Unity's partial-script import cache stale.</summary>
    [InitializeOnLoad]
    public static class FishingScriptBinding
    {
        static FishingScriptBinding(){EditorApplication.delayCall+=RepairIfNeeded;}
        private static void RepairIfNeeded()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(EditorApplication.isCompiling || EditorApplication.isUpdating){EditorApplication.delayCall+=RepairIfNeeded;return;}
            Ensure();
        }
        public static void Ensure()
        {
            // The legacy package welcome window cannot survive a hidden batch domain reload.
            // Close only this editor-only welcome surface in our batch helper, never interactive editor windows.
            if(Application.isBatchMode)foreach(var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                if(window.GetType().FullName=="GameplayIngredients.Editor.WelcomeScreen")window.Close();
            const string path="Assets/Scripts/Fishing/FishingPrototype.cs";
            var script=AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if(script!=null && script.GetClass()==typeof(FishingPrototype))return;
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
            script=AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if(script==null || script.GetClass()!=typeof(FishingPrototype))throw new InvalidOperationException("FishingPrototype MonoScript binding is missing; refusing a misleading build/test pass");
            Debug.Log("[FISHING] Refreshed stale controller script binding after project-path/platform change.");
        }
        public static void ValidateScene()
        {
            Ensure();var scene=EditorSceneManager.OpenScene(FishingPrototypeEditor.ScenePath);
            if(UnityEngine.Object.FindObjectOfType<FishingPrototype>()==null)throw new InvalidOperationException("Fishing scene has no bound controller");
            foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))
                if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)throw new InvalidOperationException("Missing script in fishing scene: "+t.name);
        }
    }
}
