using System;
using System.Collections;
using GameplayIngredients;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering.Universal;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
// ReSharper disable InconsistentNaming

namespace BoatAttack
{
    [ManagerDefaultPrefab(nameof(AppSettings))]
    public class AppSettings : Manager
    {
        public enum RenderRes
        {
            _Native,
            _1440p,
            _1080p,
            _720p
        }

        public enum Framerate
        {
            _30,
            _60,
            _120
        }

        public enum SpeedFormat
        {
            _Kph,
            _Mph
        }

        public static AppSettings Instance;
        private GameObject loadingScreenObject;
        public static Camera MainCamera;
        [Header("Resolution Settings")]
        public RenderRes maxRenderSize = RenderRes._720p;
        public bool variableResolution;
        [Range(0f, 1f)]
        public float axisBias = 0.5f;
        public float minScale = 0.5f;
        public Framerate targetFramerate = Framerate._30;
        private float currentDynamicScale = 1.0f;
        private float maxScale = 1.0f;
        public SpeedFormat speedFormat = SpeedFormat._Mph;

        [Header("Asset References")]
        public AssetReference loadingScreen;
        public AssetReference volumeManager;
        [Header("Prefabs")]
        public GameObject consoleCanvas;
        public static GameObject ConsoleCanvas;

        // Use this for initialization
        private void Awake()
        {
            if(Debug.isDebugBuild)
                Debug.Log("AppManager initializing");
            Initialize();
            CmdArgs();
            SetRenderScale();
            SceneManager.sceneLoaded += LevelWasLoaded;
        }
        
        private void Initialize()
        {
            Instance = this;
            ConsoleCanvas = Instantiate(consoleCanvas);
            DontDestroyOnLoad(ConsoleCanvas);
            MainCamera = Camera.main;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= LevelWasLoaded;
        }

        private static void LevelWasLoaded(Scene scene, LoadSceneMode mode)
        {
            CleanupCameras();
#if STATIC_EVERYTHING
            Utility.StaticObjects();
#endif
            Instance.Invoke(nameof(CleanupLoadingScreen), 0.5f);
        }

        private static void CleanupCameras()
        {
            foreach (var c in GameObject.FindGameObjectsWithTag("MainCamera"))
            {
                if (MainCamera != null && c != MainCamera.gameObject)
                {
                    Destroy(c);
                }
                else
                {
                    MainCamera = c.GetComponent<Camera>();
                }
            }
        }

        private void CleanupLoadingScreen()
        {
            if(loadingScreenObject) loadingScreen?.ReleaseInstance(loadingScreenObject);
        }

        private void SetRenderScale()
        {
            var res = maxRenderSize switch
            {
                RenderRes._720p => 1280f,
                RenderRes._1080p => 1920f,
                RenderRes._1440p => 2560f,
                _ => Screen.width
            };
            var renderScale = Mathf.Clamp(res / Screen.width, 0.1f, 1.0f);

            if(Debug.isDebugBuild)
                Debug.Log($"Settings render scale to {renderScale * 100}% based on {maxRenderSize.ToString()}");

            maxScale = renderScale;
#if !UNITY_EDITOR
            UniversalRenderPipeline.asset.renderScale = renderScale;
#endif
        }

        private void Update()
        {
#if !UNITY_EDITOR
            Utility.CheckQualityLevel(); //TODO - hoping to remove one day when we have a quality level callback
#endif

            if (!MainCamera) return;

            if (variableResolution)
            {
                MainCamera.allowDynamicResolution = true;

                // deltaTime is in seconds. Use unscaled time so pausing gameplay
                // does not make the renderer think frames are infinitely fast.
                var targetFps = targetFramerate switch
                {
                    Framerate._30 => 30,
                    Framerate._60 => 60,
                    Framerate._120 => 120,
                    _ => 30
                };
                var offset = DynamicResolutionPolicy.GetAdjustment(Time.unscaledDeltaTime, targetFps, 0.1f);

                currentDynamicScale = Mathf.Clamp(currentDynamicScale + offset, minScale, 1f);

                var offsetVec = new Vector2(Mathf.Lerp(1, currentDynamicScale, Mathf.Clamp01((1 - axisBias) * 2f)),
                    Mathf.Lerp(1, currentDynamicScale, Mathf.Clamp01(axisBias * 2f)));

                ScalableBufferManager.ResizeBuffers(offsetVec.x, offsetVec.y);
            }
            else
            {
                MainCamera.allowDynamicResolution = false;
            }
        }

        public void ToggleSRPBatcher(bool enabled)
        {
            UniversalRenderPipeline.asset.useSRPBatcher = enabled;
        }

        public static void LoadScene(int buildIndex, LoadSceneMode mode = LoadSceneMode.Single)
        {
            LoadScene(SceneUtility.GetScenePathByBuildIndex(buildIndex), mode);
        }

        public static void LoadScene(string scenePath, LoadSceneMode mode = LoadSceneMode.Single)
        {
            scenePath = NormalizeScenePath(scenePath);
            Application.backgroundLoadingPriority = ThreadPriority.Low;
            switch (mode)
            {
                case LoadSceneMode.Single:
                    Instance.StartCoroutine(LoadSceneInternal(scenePath));
                    break;
                case LoadSceneMode.Additive:
                    SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        private static string NormalizeScenePath(string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
                return scenePath;

            // ConstantData historically stored paths without the Assets/ prefix.
            // Unity 2022's EditorSceneManager requires a project-relative Assets path.
            if (scenePath.StartsWith("scenes/", StringComparison.OrdinalIgnoreCase))
                scenePath = "Assets/" + scenePath;

            if (!scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                scenePath += ".unity";

            return scenePath;
        }

        private static IEnumerator LoadSceneInternal(string scenePath)
        {
            scenePath = NormalizeScenePath(scenePath);
            var loadingScreenLoading = Instance.loadingScreen.InstantiateAsync();
            yield return loadingScreenLoading;
            Instance.loadingScreenObject = loadingScreenLoading.Result;
            Instance.loadingScreenObject.SendMessage("SetLoad", 0.0001f);
            DontDestroyOnLoad(Instance.loadingScreenObject);

            var buildIndex = SceneUtility.GetBuildIndexByScenePath(scenePath);
            if(Debug.isDebugBuild)
                Debug.Log($"loading scene {scenePath} at build index {buildIndex}");

            // get current scene and set a loading scene as active
            var currentScene = SceneManager.GetActiveScene();
            var loadingScene = SceneManager.CreateScene("Loading");
            SceneManager.SetActiveScene(loadingScene);

            // unload last scene
            var unload = SceneManager.UnloadSceneAsync(currentScene, UnloadSceneOptions.None);
            while (!unload.isDone)
            {
                Instance.loadingScreenObject.SendMessage("SetLoad", unload.progress * 0.5f);
                yield return null;
            }

            // clean up
            var clean = Resources.UnloadUnusedAssets();
            while (!clean.isDone) { yield return null; }

            // load new scene
            var load = new AsyncOperation();
#if UNITY_EDITOR
            if (buildIndex == -1)
            {
                load = EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath,
                    new LoadSceneParameters(LoadSceneMode.Single));
            }
            else
            {
                load = SceneManager.LoadSceneAsync(buildIndex);
            }
#else
            load = SceneManager.LoadSceneAsync(scenePath);
#endif
            if (load == null)
            {
                Debug.LogError($"[BoatAttack] Scene load returned null for '{scenePath}'. Check that the scene exists and is included in the active build settings.");
                yield break;
            }

            while (!load.isDone)
            {
                if (Instance.loadingScreenObject)
                    Instance.loadingScreenObject.SendMessage("SetLoad", load.progress * 0.5f + 0.5f);
                yield return null;
            }
        }

        private static IEnumerator LoadPrefab<T>(AssetReference assetRef, AsyncOperationHandle assetLoading, Transform parent = null)
        {
            if (typeof(T) == typeof(GameObject))
            {
                assetLoading = assetRef.InstantiateAsync(parent);
            }
            else
            {
                assetLoading = assetRef.LoadAssetAsync<T>();
            }
            yield return assetLoading;
        }

        public static void ExitGame(string s = "null")
        {
            if(s != "null")
                Debug.LogError(s);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }

        private static void CmdArgs()
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Length <= 0) return;
            foreach (var argRaw in args)
            {
                if(string.IsNullOrEmpty(argRaw) || argRaw[0] != '-') continue;
                var arg = argRaw.Split(':');

                switch (arg[0])
                {
                    case "-loadlevel":
                        LoadScene(arg[1]);
                        break;
                    case "-benchmarkFlythrough":
                        LoadScene("benchmark_island-flythrough");
                        break;
                }
            }
        }
    }

    public static class ConstantData
    {
        private static readonly string[] Levels =
        {
            "Island",
        };

        public static string GetLevelName(int level)
        {
            return $"Assets/scenes/_levels/level_{Levels[level]}.unity";
        }

        public static readonly string[] AiNames =
        {
            "Felipe",
            "Andre",
            "Elvar",
            "Jonas",
            "Erika",
            "Tim",
            "Florin",
            "Andy",
            "Hakeem",
            "Sophia",
            "Martin",
        };

        public static readonly int[] Laps =
        {
            1,
            3,
            6,
            9
        };

        public static int SeedNow
        {
            get
            {
                DateTime dt = DateTime.Now;
                return dt.Year + dt.Month + dt.Day + dt.Hour + dt.Minute + dt.Second;
            }
        }

        public static Color[] ColorPalette;
        private static Texture2D _colorPaletteRaw;

        public static int PaletteCount
        {
            get { GenerateColors(); return ColorPalette.Length; }
        }

        public static Color GetPaletteColor(int index)
        {
            GenerateColors();
            return ColorPalette[index];
        }

        public static Color GetRandomPaletteColor
        {
            get
            {
                GenerateColors();
                Random.InitState(SeedNow+Random.Range(0,1000));
                return ColorPalette[Random.Range(0, ColorPalette.Length)];
            }
        }

        private static void GenerateColors()
        {
            if (ColorPalette != null && ColorPalette.Length != 0) return;

            if (_colorPaletteRaw == null)
                _colorPaletteRaw = Resources.Load<Texture2D>("textures/colorSwatch");

            if (_colorPaletteRaw == null)
            {
                Debug.LogError("[BoatAttack] Resources/textures/colorSwatch.png could not be loaded. Using a fallback palette.");
                ColorPalette = new[]
                {
                    new Color(0.88f, 0.10f, 0.10f), new Color(0.11f, 0.87f, 0.18f),
                    new Color(0.10f, 0.90f, 0.87f), new Color(0.15f, 0.21f, 0.85f),
                    new Color(0.93f, 0.72f, 0.07f), new Color(0.87f, 0.87f, 0.87f),
                    new Color(0.14f, 0.14f, 0.14f), Color.white
                };
                return;
            }

            ColorPalette = _colorPaletteRaw.GetPixels();
            Debug.Log($"Found {ColorPalette.Length} colors.");
        }

    }
}
