using System;
using System.IO;
using System.Linq;
using BoatAttack.Fishing;
using Cinemachine;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    public static class FishingPrototypeEditor
    {
        public const string ScenePath="Assets/scenes/Fishing/fishing_Island.unity";
        [MenuItem("BoatAttack/Fishing/Open prototype")]
        public static void Open()
        {
            if(EditorApplication.isPlaying){Debug.LogWarning("Stop Play Mode first.");return;}
            if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(ScenePath);
        }
        [MenuItem("BoatAttack/Fishing/Generate prototype scene")]
        public static void Generate()
        {
            try
            {
                if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
                if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
                if(!Application.isBatchMode && File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Regenerate fishing prototype?", "This replaces custom edits to the generated fishing scene. The original island scene is not changed.", "Regenerate", "Cancel"))return;
                Directory.CreateDirectory("Assets/scenes/Fishing");Directory.CreateDirectory("Assets/Objects/FishingPrototype/Materials");AssetDatabase.Refresh();
                var scene=EditorSceneManager.OpenScene("Assets/scenes/_levels/level_Island.unity");
                var waypoints=Object.FindObjectOfType<WaypointGroup>();
                if(waypoints==null || waypoints.WPs.Count<5)throw new Exception("Original island waypoint data not available.");
                var start=waypoints.WPs[0];var points=waypoints.WPs.Select(w=>w.point).ToArray();
                foreach(var group in Object.FindObjectsOfType<WaypointGroup>(true))group.enabled=false;
                foreach(var demo in Object.FindObjectsOfType<DemoRun>(true))demo.enabled=false;
                foreach(var replay in Object.FindObjectsOfType<ReplayCamera>(true))replay.enabled=false;
                foreach(var controller in Object.FindObjectsOfType<CameraManager>(true))controller.enabled=false;
                foreach(var camera in Object.FindObjectsOfType<CinemachineVirtualCameraBase>(true))camera.gameObject.SetActive(false);
                foreach(var director in Object.FindObjectsOfType<PlayableDirector>(true)){director.playOnAwake=false;director.enabled=false;}
                foreach(var camera in Object.FindObjectsOfType<Camera>(true))camera.enabled=false;
                var main=new GameObject("Fishing Main Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();main.tag="MainCamera";
                main.nearClipPlane=0.1f;main.farClipPlane=1800f;main.fieldOfView=58f;main.allowHDR=true;main.clearFlags=CameraClearFlags.Skybox;
                var additional=main.GetUniversalAdditionalCameraData();additional.renderPostProcessing=false;additional.requiresDepthOption=CameraOverrideOption.On;additional.requiresColorOption=CameraOverrideOption.On;additional.SetRenderer(0);
                var mode=new GameObject("Fishing Prototype").AddComponent<FishingPrototype>();
                mode.boatPrefab=AssetDatabase.LoadAssetAtPath<Boat>("Assets/Objects/boats/renegade/_Renegade.prefab");
                if(mode.boatPrefab==null)throw new Exception("Boat prefab missing.");
                mode.viewCamera=main;mode.spawnRotation=start.rotation;
                Physics.SyncTransforms();
                mode.harbor=ClearWater(start.point+start.rotation*new Vector3(17,0,-4),18f,new Vector3[0]);
                mode.spawnPosition=mode.harbor-start.rotation*Vector3.right*14f+Vector3.up;
                Vector3 next=points.First(p=>Vector3.Distance(p,start.point)>70f);
                var reserved=new System.Collections.Generic.List<Vector3>{mode.harbor};
                var seeds=new[]{Vector3.Lerp(start.point,next,0.65f),points[2],points[4]};
                mode.fishingSpots=new Vector3[3];
                for(int i=0;i<3;i++){mode.fishingSpots[i]=ClearWater(seeds[i],15f,reserved.ToArray());reserved.Add(mode.fishingSpots[i]);}
                mode.fishingSpots=mode.fishingSpots.OrderBy(p=>FishingPrototype.FlatDistance(p,mode.spawnPosition)).ToArray();
                mode.wood=Material("Dock wood",new Color(0.36f,0.23f,0.12f));
                mode.cream=Material("Warm cream",new Color(0.90f,0.86f,0.72f));
                mode.dark=Material("Tool charcoal",new Color(0.055f,0.11f,0.14f));
                mode.orange=Material("Signal amber",new Color(0.90f,0.46f,0.10f));
                mode.teal=Material("Harbor teal",new Color(0.035f,0.42f,0.35f));
                mode.lineMaterial=Material("Fishing line",new Color(0.9f,0.95f,0.85f));
                mode.fishMaterials=new[]{Material("Coral fish",new Color(0.92f,0.22f,0.08f)),Material("Dart fish",new Color(0.045f,0.38f,0.61f)),Material("Puffer fish",new Color(0.74f,0.54f,0.12f))};
                EditorSceneManager.SaveScene(scene,ScenePath);
                var buildScenes=EditorBuildSettings.scenes.ToList();if(!buildScenes.Any(s=>s.path==ScenePath))buildScenes.Add(new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=buildScenes.ToArray();
                AssetDatabase.SaveAssets();
                Debug.Log("[FISHING] Anchors: spawn="+mode.spawnPosition+" harbor="+mode.harbor+" spots="+string.Join("; ",mode.fishingSpots.Select(v=>v.ToString())));
                Debug.Log("[FISHING] PASS: generated fishing scene and original material palette. Original island scene/prefab not saved.");
                if(Application.isBatchMode)EditorApplication.Exit(0);
            }
            catch(Exception ex){Debug.LogException(ex);if(Application.isBatchMode)EditorApplication.Exit(1);}
        }
        private static Vector3 ClearWater(Vector3 seed,float radius,Vector3[] reserved)
        {
            // Check actual collision geometry, not just a race waypoint near a cliff.
            for(int ring=0;ring<=12;ring++)for(int direction=0;direction<24;direction++)
            {
                float angle=direction*Mathf.PI*2f/24f;
                var p=new Vector3(seed.x+Mathf.Cos(angle)*ring*8f,0,seed.z+Mathf.Sin(angle)*ring*8f);
                if(reserved.Any(other=>FishingPrototype.FlatDistance(other,p)<42f))continue;
                bool blocked=false;
                // A large sphere also hits the seabed; only test obstacles above sea level.
                for(int x=-1;x<=1 && !blocked;x++)for(int z=-1;z<=1 && !blocked;z++)
                {
                    var top=p+new Vector3(x*radius*0.7f,200f,z*radius*0.7f);
                    blocked=Physics.RaycastAll(top,Vector3.down,199.5f,~(1<<4),QueryTriggerInteraction.Ignore)
                        .Any(hit=>hit.collider.GetComponentInParent<WaterSystem.Water>()==null);
                }
                for(int ray=0;ray<24 && !blocked;ray++)
                {
                    float a=ray*Mathf.PI*2f/24f;
                    blocked=Physics.RaycastAll(p+Vector3.up,new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),radius,~(1<<4),QueryTriggerInteraction.Ignore)
                        .Any(hit=>hit.collider.GetComponentInParent<WaterSystem.Water>()==null);
                }
                if(!blocked)return p;
            }
            throw new Exception("No clear fishing water found near "+seed);
        }
        private static Material Material(string name,Color color)
        {
            string path="Assets/Objects/FishingPrototype/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
            material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",name.Contains("fish")?0.4f:0.22f);EditorUtility.SetDirty(material);return material;
        }
        public static void Build()
        {
            try
            {
                if(!File.Exists(ScenePath))throw new Exception("Generate the fishing scene first.");
                FishingCashierValidation.ValidateAssets();
                FishingScriptBinding.ValidateScene();
                PlayerSettings.bundleVersion=FishingPrototype.Version;
                PlayerSettings.resizableWindow=true;PlayerSettings.allowFullscreenSwitch=true;
                AddressableAssetSettings.BuildPlayerContent(out var content);
                if(!string.IsNullOrEmpty(content.Error))throw new Exception("Addressables: "+content.Error);
                string output=Path.GetFullPath("Builds/FishingPrototype-v"+FishingPrototype.Version+"/FishingPrototype.exe");Directory.CreateDirectory(Path.GetDirectoryName(output));
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
                if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed: "+report.summary.result);
                Debug.Log("[FISHING] PASS: Windows prototype built: "+output);EditorApplication.Exit(0);
            }
            catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
        }
    }

    [InitializeOnLoad]
    public static class FishingPrototypeSmoke
    {
        private const string Key="BoatAttack.FishingSmoke.";
        static FishingPrototypeSmoke(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=State;Application.logMessageReceived+=Log;}
        public static void Run()
        {
            if(!Application.isBatchMode)throw new Exception("Batch-only smoke entry.");
            // The editor deadline must outlast the autoplay driver's own timeout so its message wins.
            SessionState.SetBool(Key+"active",true);SessionState.SetBool(Key+"stop",false);SessionState.SetString(Key+"error","");SessionState.SetFloat(Key+"deadline",(float)EditorApplication.timeSinceStartup+FishingAutoplay.TimeoutSeconds+20f);
            EditorSceneManager.OpenScene(FishingPrototypeEditor.ScenePath);EditorApplication.EnterPlaymode();
        }
        private static void Log(string message,string stack,LogType type)
        {
            if(SessionState.GetBool(Key+"active",false) && (type==LogType.Error || type==LogType.Exception || type==LogType.Assert) && SessionState.GetString(Key+"error","")=="")SessionState.SetString(Key+"error",message);
        }
        private static void Tick()
        {
            if(!SessionState.GetBool(Key+"active",false) || SessionState.GetBool(Key+"stop",false))return;
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+"deadline",0))SessionState.SetString(Key+"error","Fishing smoke timeout.");
            if(!string.IsNullOrEmpty(FishingAutoplay.Failure))SessionState.SetString(Key+"error",FishingAutoplay.Failure);
            if(FishingAutoplay.Completed || SessionState.GetString(Key+"error","")!="")
            {SessionState.SetBool(Key+"stop",true);EditorApplication.ExitPlaymode();}
        }
        private static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Key+"active",false) || state!=PlayModeStateChange.EnteredEditMode)return;
            string error=SessionState.GetString(Key+"error","");
            if(!SessionState.GetBool(Key+"stop",false))error="Play Mode stopped before validation completed.";
            SessionState.SetBool(Key+"active",false);
            if(error.Length==0)Debug.Log("[FISHING] PASS: live prototype smoke finished and exited Play Mode.");else Debug.LogError("[FISHING] FAIL: "+error);
            EditorApplication.Exit(error.Length==0?0:1);
        }
    }
}
