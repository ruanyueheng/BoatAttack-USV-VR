using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.Universal;
using WaterSystem.Data;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace WaterSystem
{
    [ExecuteAlways]
    public class Water : MonoBehaviour
    {
        // Singleton
        private static Water _instance;
        public static Water Instance
        {
            get
            {
                if (_instance == null)
                    _instance = (Water)FindObjectOfType(typeof(Water));
                return _instance;
            }
        }
        // Script references
        private PlanarReflections _planarReflections;

        private bool _useComputeBuffer;
        public bool computeOverride;

        [SerializeField] RenderTexture _depthTex;
        public Texture bakedDepthTex;
        private Camera _depthCam;
        private Rect? _runtimeDepthBounds;
        private Texture2D _rampTexture;
        [SerializeField]
        public Wave[] _waves;
        [SerializeField]
        private ComputeBuffer waveBuffer;
        private float _maxWaveHeight;
        private float _waveHeight;

        [SerializeField]
        public WaterSettingsData settingsData;
        [SerializeField]
        public WaterSurfaceData surfaceData;
        [SerializeField]
        private WaterResources resources;

        private static readonly int CameraRoll = Shader.PropertyToID("_CameraRoll");
        private static readonly int InvViewProjection = Shader.PropertyToID("_InvViewProjection");
        private static readonly int WaterDepthMap = Shader.PropertyToID("_WaterDepthMap");
        private static readonly int FoamMap = Shader.PropertyToID("_FoamMap");
        private static readonly int SurfaceMap = Shader.PropertyToID("_SurfaceMap");
        private static readonly int WaveHeight = Shader.PropertyToID("_WaveHeight");
        private static readonly int MaxWaveHeight = Shader.PropertyToID("_MaxWaveHeight");
        private static readonly int MaxDepth = Shader.PropertyToID("_MaxDepth");
        private static readonly int WaveCount = Shader.PropertyToID("_WaveCount");
        private static readonly int CubemapTexture = Shader.PropertyToID("_CubemapTexture");
        private static readonly int WaveDataBuffer = Shader.PropertyToID("_WaveDataBuffer");
        private static readonly int WaveData = Shader.PropertyToID("waveData");
        private static readonly int AbsorptionScatteringRamp = Shader.PropertyToID("_AbsorptionScatteringRamp");
        private static readonly int DepthCamZParams = Shader.PropertyToID("_VeraslWater_DepthCamParams");
        private static readonly int WaterDepthBounds = Shader.PropertyToID("_WaterDepthBounds");

        private void OnEnable()
        {
            if (!computeOverride)
                _useComputeBuffer = SystemInfo.supportsComputeShaders &&
                                   Application.platform != RuntimePlatform.WebGLPlayer &&
                                   Application.platform != RuntimePlatform.Android;
            else
                _useComputeBuffer = false;
            Init();
            RenderPipelineManager.beginCameraRendering += BeginCameraRendering;

            if(resources == null)
            {
                resources = Resources.Load("WaterResources") as WaterResources;
            }
        }

        private void OnDisable() {
            Cleanup();
        }

        private void OnApplicationQuit()
        {
            Cleanup();
        }

        void Cleanup()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
            if (_depthCam)
            {
                _depthCam.targetTexture = null;
                SafeDestroy(_depthCam.gameObject);
                _depthCam = null;
            }
            if (_depthTex)
            {
                _depthTex.Release();
                SafeDestroy(_depthTex);
                _depthTex = null;
            }
            if (_rampTexture) { SafeDestroy(_rampTexture); _rampTexture = null; }
            waveBuffer?.Dispose();
            waveBuffer = null;
            if (_instance == this)
            {
                GerstnerWavesJobs.Cleanup();
                _instance = null;
            }
        }

        private void BeginCameraRendering(ScriptableRenderContext src, Camera cam)
        {
            if (cam.cameraType == CameraType.Preview) return;

            var roll = cam.transform.localEulerAngles.z;
            Shader.SetGlobalFloat(CameraRoll, roll);
            Shader.SetGlobalMatrix(InvViewProjection,
                (GL.GetGPUProjectionMatrix(cam.projectionMatrix, false) * cam.worldToCameraMatrix).inverse);

            // Water matrix
            const float quantizeValue = 6.25f;
            const float forwards = 10f;
            const float yOffset = -0.25f;

            var newPos = cam.transform.TransformPoint(Vector3.forward * forwards);
            newPos.y = yOffset;
            newPos.x = quantizeValue * (int) (newPos.x / quantizeValue);
            newPos.z = quantizeValue * (int) (newPos.z / quantizeValue);

            var matrix = Matrix4x4.TRS(newPos + transform.position, Quaternion.identity, transform.localScale); // transform.localToWorldMatrix;

            foreach (var mesh in resources.defaultWaterMeshes)
            {
                Graphics.DrawMesh(mesh,
                    matrix,
                    resources.defaultSeaMaterial,
                    gameObject.layer,
                    cam,
                    0,
                    null,
                    ShadowCastingMode.Off,
                    true,
                    null,
                    LightProbeUsage.Off,
                    null);
            }
        }

        private static void SafeDestroy(Object o)
        {
            if(Application.isPlaying)
                Destroy(o);
            else
                DestroyImmediate(o);
        }

        public void Init()
        {
            if (resources == null) resources = Resources.Load<WaterResources>("WaterResources");
            SetWaves();
            GenerateColorRamp();
            if (bakedDepthTex)
            {
                Shader.SetGlobalTexture(WaterDepthMap, bakedDepthTex);
                PublishDepthBounds(ConfiguredDepthBounds());
                Shader.SetGlobalVector(DepthCamZParams, new Vector4(transform.position.y + 4, surfaceData._waterMaxVisibility + 4, .01f, transform.position.y));
            }

            if (!gameObject.TryGetComponent(out _planarReflections))
            {
                _planarReflections = gameObject.AddComponent<PlanarReflections>();
            }
            _planarReflections.hideFlags = HideFlags.HideAndDontSave | HideFlags.HideInInspector;
            _planarReflections.m_settings = settingsData.planarSettings;
            _planarReflections.enabled = settingsData.refType == ReflectionType.PlanarReflection;

            if(resources == null)
            {
                resources = Resources.Load("WaterResources") as WaterResources;
            }
            if(Application.platform != RuntimePlatform.WebGLPlayer) // TODO - bug with Opengl depth
                CaptureDepthMap();
        }

        private void LateUpdate()
        {
            if (Application.isPlaying && GerstnerWavesJobs.Initialized) GerstnerWavesJobs.UpdateHeights();
        }

        public void FragWaveNormals(bool toggle)
        {
            var mat = GetComponent<Renderer>().sharedMaterial;
            if (toggle)
                mat.EnableKeyword("GERSTNER_WAVES");
            else
                mat.DisableKeyword("GERSTNER_WAVES");
        }

        private void SetWaves()
        {
            SetupWaves(surfaceData._customWaves);

            // set default resources
            Shader.SetGlobalTexture(FoamMap, resources.defaultFoamMap);
            Shader.SetGlobalTexture(SurfaceMap, resources.defaultSurfaceMap);

            _maxWaveHeight = 0f;
            foreach (var w in _waves)
            {
                _maxWaveHeight += w.amplitude;
            }
            _maxWaveHeight /= _waves.Length;

            _waveHeight = transform.position.y;

            Shader.SetGlobalFloat(WaveHeight, _waveHeight);
            Shader.SetGlobalFloat(MaxWaveHeight, _maxWaveHeight);
            Shader.SetGlobalFloat(MaxDepth, surfaceData._waterMaxVisibility);

            switch(settingsData.refType)
            {
                case ReflectionType.Cubemap:
                    Shader.EnableKeyword("_REFLECTION_CUBEMAP");
                    Shader.DisableKeyword("_REFLECTION_PROBES");
                    Shader.DisableKeyword("_REFLECTION_PLANARREFLECTION");
                    Shader.SetGlobalTexture(CubemapTexture, settingsData.cubemapRefType);
                    break;
                case ReflectionType.ReflectionProbe:
                    Shader.DisableKeyword("_REFLECTION_CUBEMAP");
                    Shader.EnableKeyword("_REFLECTION_PROBES");
                    Shader.DisableKeyword("_REFLECTION_PLANARREFLECTION");
                    break;
                case ReflectionType.PlanarReflection:
                    Shader.DisableKeyword("_REFLECTION_CUBEMAP");
                    Shader.DisableKeyword("_REFLECTION_PROBES");
                    Shader.EnableKeyword("_REFLECTION_PLANARREFLECTION");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            Shader.SetGlobalInt(WaveCount, _waves.Length);

            //GPU side
            if (_useComputeBuffer)
            {
                Shader.EnableKeyword("USE_STRUCTURED_BUFFER");
                waveBuffer?.Dispose();
                waveBuffer = new ComputeBuffer(10, (sizeof(float) * 6));
                waveBuffer.SetData(_waves);
                Shader.SetGlobalBuffer(WaveDataBuffer, waveBuffer);
            }
            else
            {
                Shader.DisableKeyword("USE_STRUCTURED_BUFFER");
                Shader.SetGlobalVectorArray(WaveData, GetWaveData());
            }
            //CPU side
            if(GerstnerWavesJobs.Initialized == false && Application.isPlaying)
                GerstnerWavesJobs.Init();
        }

        private Vector4[] GetWaveData()
        {
            var waveData = new Vector4[20];
            for (var i = 0; i < _waves.Length; i++)
            {
                waveData[i] = new Vector4(_waves[i].amplitude, _waves[i].direction, _waves[i].wavelength, _waves[i].onmiDir);
                waveData[i+10] = new Vector4(_waves[i].origin.x, _waves[i].origin.y, 0, 0);
            }
            return waveData;
        }

        private void SetupWaves(bool custom)
        {
            if(!custom)
            {
                //create basic waves based off basic wave settings
                var backupSeed = Random.state;
                Random.InitState(surfaceData.randomSeed);
                var basicWaves = surfaceData._basicWaveSettings;
                var a = basicWaves.amplitude;
                var d = basicWaves.direction;
                var l = basicWaves.wavelength;
                var numWave = basicWaves.numWaves;
                _waves = new Wave[numWave];

                var r = 1f / numWave;

                for (var i = 0; i < numWave; i++)
                {
                    var p = Mathf.Lerp(0.5f, 1.5f, i * r);
                    var amp = a * p * Random.Range(0.8f, 1.2f);
                    var dir = d + Random.Range(-90f, 90f);
                    var len = l * p * Random.Range(0.6f, 1.4f);
                    _waves[i] = new Wave(amp, dir, len, Vector2.zero, false);
                    Random.InitState(surfaceData.randomSeed + i + 1);
                }
                Random.state = backupSeed;
            }
            else
            {
                _waves = surfaceData._waves.ToArray();
            }
        }

        private void GenerateColorRamp()
        {
            if(_rampTexture == null)
                _rampTexture = new Texture2D(128, 4, GraphicsFormat.R8G8B8A8_SRGB, TextureCreationFlags.None);
            _rampTexture.wrapMode = TextureWrapMode.Clamp;

            var defaultFoamRamp = resources.defaultFoamRamp;

            var cols = new Color[512];
            for (var i = 0; i < 128; i++)
            {
                cols[i] = surfaceData._absorptionRamp.Evaluate(i / 128f);
            }
            for (var i = 0; i < 128; i++)
            {
                cols[i + 128] = surfaceData._scatterRamp.Evaluate(i / 128f);
            }
            for (var i = 0; i < 128; i++)
            {
                switch(surfaceData._foamSettings.foamType)
                {
                    case 0: // default
                        cols[i + 256] = defaultFoamRamp.GetPixelBilinear(i / 128f , 0.5f);
                        break;
                    case 1: // simple
                        cols[i + 256] = defaultFoamRamp.GetPixelBilinear(surfaceData._foamSettings.basicFoam.Evaluate(i / 128f) , 0.5f);
                        break;
                    case 2: // custom
                        cols[i + 256] = Color.black;
                        break;
                }
            }
            _rampTexture.SetPixels(cols);
            _rampTexture.Apply();
            Shader.SetGlobalTexture(AbsorptionScatteringRamp, _rampTexture);
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////Shoreline Depth Texture/////////////////////////////////////////////////
        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        [ContextMenu("Capture Depth")]
        public void CaptureDepthMap()
        {
            Rect bounds = ConfiguredDepthBounds();
            //Generate the camera
            if(_depthCam == null)
            {
                var go =
                    new GameObject("depthCamera") {hideFlags = HideFlags.HideAndDontSave}; //create the cameraObject
                _depthCam = go.AddComponent<Camera>();
            }

            var additionalCamData = _depthCam.GetUniversalAdditionalCameraData();
            additionalCamData.renderShadows = false;
            additionalCamData.requiresColorOption = CameraOverrideOption.Off;
            additionalCamData.requiresDepthOption = CameraOverrideOption.Off;

            var t = _depthCam.transform;
            var depthExtra = 4.0f;
            t.position = new Vector3(bounds.center.x, transform.position.y + depthExtra, bounds.center.y);
            t.up = Vector3.forward;//face the camera down

            _depthCam.enabled = false;
            _depthCam.orthographic = true;
            _depthCam.orthographicSize = bounds.height * .5f;
            _depthCam.aspect = bounds.width / bounds.height;
            _depthCam.nearClipPlane =0.01f;
            _depthCam.farClipPlane = surfaceData._waterMaxVisibility + depthExtra;
            _depthCam.allowHDR = false;
            _depthCam.allowMSAA = false;
            _depthCam.useOcclusionCulling = false;
            _depthCam.cullingMask = (1 << 10);
            // Preserve the original 1024 texels / 500 m density, within a bounded one-time depth allocation.
            const float texelsPerMetre = 1024f / 500f;
            int requestedWidth = Mathf.Max(16, Mathf.CeilToInt(bounds.width * texelsPerMetre));
            int requestedHeight = Mathf.Max(16, Mathf.CeilToInt(bounds.height * texelsPerMetre));
            int limit = Mathf.Min(4096, SystemInfo.maxTextureSize);
            long budget = (Application.platform == RuntimePlatform.Android ? 48L : 64L) * 1024 * 1024;
            double scale = Math.Min(1, Math.Min((double)limit / Math.Max(requestedWidth, requestedHeight),
                Math.Sqrt((double)budget / (4L * requestedWidth * requestedHeight))));
            int width = Math.Max(1, (int)Math.Floor(requestedWidth * scale));
            int height = Math.Max(1, (int)Math.Floor(requestedHeight * scale));
            if (!_depthTex || _depthTex.width != width || _depthTex.height != height)
            {
                if (_depthTex) { _depthTex.Release(); SafeDestroy(_depthTex); }
                _depthTex = new RenderTexture(width, height, 24, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear)
                { name = "WaterDepthMap", useMipMap = false, autoGenerateMips = false };
                if (!_depthTex.Create()) throw new InvalidOperationException("Cannot allocate shoreline depth texture.");
            }
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES2 || SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3)
            {
                _depthTex.filterMode = FilterMode.Point;
            }
            _depthTex.wrapMode = TextureWrapMode.Clamp;
            try
            {
                _depthCam.targetTexture = _depthTex;
                _depthCam.Render();
            }
            finally { _depthCam.enabled = false; _depthCam.targetTexture = null; }
            Shader.SetGlobalTexture(WaterDepthMap, _depthTex);
            PublishDepthBounds(bounds);
            Shader.SetGlobalVector(DepthCamZParams, new Vector4(t.position.y, _depthCam.farClipPlane, _depthCam.nearClipPlane, transform.position.y));
            Debug.Log("[WATER-DEPTH] " + bounds + "; " + width + "x" + height + "; estimated depth storage "
                + (4d * width * height / (1024 * 1024)).ToString("F2") + " MiB; metres/texel "
                + (bounds.width / width).ToString("F3") + " / " + (bounds.height / height).ToString("F3"));
            if (scale < .999) Debug.LogWarning("[WATER-DEPTH] Texture/platform memory limit reduced shoreline sample density; verify coast quality on this device.");
        }

        /// <summary>One runtime capture override; does not mutate the shared settings asset.</summary>
        public void CaptureDepthMap(Rect worldXZBounds)
        {
            if (!Finite(worldXZBounds.x) || !Finite(worldXZBounds.y) || !Finite(worldXZBounds.width) || !Finite(worldXZBounds.height)
                || worldXZBounds.width <= 0 || worldXZBounds.height <= 0 || worldXZBounds.width > 100000 || worldXZBounds.height > 100000)
                throw new ArgumentException("Shoreline depth capture requires finite positive world bounds.", nameof(worldXZBounds));
            _runtimeDepthBounds = worldXZBounds;
            CaptureDepthMap();
        }

        private Rect ConfiguredDepthBounds()
        {
            if (_runtimeDepthBounds.HasValue) return _runtimeDepthBounds.Value;
            var area = settingsData.originOffset;
            if (!Finite(area.x) || !Finite(area.y) || !Finite(area.z) || !Finite(area.w) || area.z <= 0 || area.w <= 0 || area.z > 100000 || area.w > 100000)
                throw new InvalidOperationException("Water settings have invalid shoreline depth bounds.");
            return new Rect(area.x - area.z * .5f, area.y - area.w * .5f, area.z, area.w);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void PublishDepthBounds(Rect bounds)
            => Shader.SetGlobalVector(WaterDepthBounds, new Vector4(bounds.xMin, bounds.yMin, 1 / bounds.width, 1 / bounds.height));

        [Serializable]
        public enum DebugMode { none, stationary, screen };
    }
}
