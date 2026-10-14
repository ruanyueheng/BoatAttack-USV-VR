using System;
using System.Collections.Generic;
using System.IO;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BoatAttack.Editor
{
    /// <summary>Offline, collider-independent, north-up visual geometry capture. Never regenerates/saves a scene.</summary>
    public static class FishingMapBaker
    {
        private const string TexturePath = "Assets/Resources/FishingMap.png";
        private const string DefinitionPath = "Assets/Resources/FishingMap.asset";
        private static readonly Color WaterColor = new Color(.10f,.24f,.28f,1);
        private static readonly Color LowColor = new Color(.73f,.69f,.54f,1);
        private static readonly Color LandColor = new Color(.35f,.46f,.36f,1);
        private static readonly Color HighColor = new Color(.66f,.68f,.66f,1);

        [MenuItem("Tools/Fishing/Map/Bake initialized scene (plus selected static roots)")]
        public static void BakeInitializedMenu() => BakeInitializedScene(Selection.transforms);

        /// <summary>Run after FishingPrototype initialization, preferably paused. Extra roots are explicit static
        /// geometry, e.g. the new shop/shore structures; never pass the player or the entire controller root.
        /// seaLevel is the mean plane, NOT water depth. The current fishing world uses world Y = 0.</summary>
        public static FishingMapDefinition BakeInitializedScene(Transform[] extraStaticRoots = null, float seaLevel = 0, int longEdge = 2048)
        {
            var scene = SceneManager.GetActiveScene();
            FishingPrototype controller = null;
            foreach (var root in scene.GetRootGameObjects())
                if (controller == null) controller = root.GetComponentInChildren<FishingPrototype>();
            if (!Application.isPlaying || controller == null || controller.Career == null
                || controller.GetComponent<FishingScenery>() == null)
                throw new InvalidOperationException("Enter the initialized fishing scene first. Do not call Generate. For base-only geometry use the separate base menu.");
            var runtimeRoots = new List<Transform>();
            if(controller.Home!=null)
            {
                runtimeRoots.Add(controller.Home.House);
                foreach(Transform child in controller.transform)
                    if(child.name.StartsWith("V027 Island",StringComparison.Ordinal) || child.name.StartsWith("Home ",StringComparison.Ordinal)
                        || child.name.StartsWith("Boardwalk ",StringComparison.Ordinal))runtimeRoots.Add(child);
            }
            foreach (Transform child in controller.transform)
                if (child.name.StartsWith("V03 habitat ", StringComparison.Ordinal)
                    || child.name == "V03 harbor lighthouse" || child.name == "Tide and Fin floating fish market")
                    runtimeRoots.Add(child);
            // 命名栈桥和公共设施也是初始化后生成的静态几何，不把整个人物控制器纳入烘焙。
            if(controller.Shore!=null)
                for(int i=0;i<controller.Shore.Sites.Length;i++)
                {
                    var root=controller.transform.Find(FishingExploration.SiteName(i));
                    if(root!=null)runtimeRoots.Add(root);
                }
            if(controller.WorkshopWorld!=null)
                foreach(var station in controller.WorkshopWorld.State.Stations)
                {
                    var root=controller.WorkshopWorld.StationRoot(station.id);if(root!=null)runtimeRoots.Add(root);
                }
            var result = Bake(scene, runtimeRoots, extraStaticRoots, seaLevel, longEdge, true);
            foreach (var hud in controller.GetComponents<FishingHUD>()) hud.ReleaseTerrainMap();
            return result;
        }

        [MenuItem("Tools/Fishing/Map/Bake open scene BASE ONLY (plus selected static roots)")]
        public static void BakeOpenSceneBaseOnly()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Use the initialized-scene menu while playing.");
            if (!EditorUtility.DisplayDialog("Base geometry only", "This excludes runtime reefs, market and shop additions. Re-bake after fishing initialization, selecting any extra static shop/shore roots. No scene will be saved.", "Bake base only", "Cancel")) return;
            Bake(SceneManager.GetActiveScene(), new List<Transform>(), Selection.transforms, 0, 2048, false);
        }

        private static FishingMapDefinition Bake(Scene sourceScene, List<Transform> runtimeRoots,
            Transform[] extras, float seaLevel, int longEdge, bool initialized)
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))
                throw new InvalidOperationException("FishingMapBaker requires the project's URP 14 renderer.");
            if (!FishingMapMath.Finite(seaLevel) || !sourceScene.IsValid() || !sourceScene.isLoaded)
                throw new ArgumentException("A loaded source scene and finite sea level are required.");
            var explicitRoots = new List<Transform>(runtimeRoots);
            if (extras != null)
                foreach (var root in extras)
                {
                    if (root == null) continue;
                    if (root.gameObject.scene != sourceScene) throw new ArgumentException("All extra roots must belong to the captured scene.");
                    if (root.GetComponent<FishingPrototype>() != null) throw new ArgumentException("Select only static shop/shore roots, not FishingPrototype itself.");
                    explicitRoots.Add(root);
                }
            longEdge = Mathf.NextPowerOfTwo(Mathf.Clamp(longEdge, 512, 4096));
            Texture2D pixels = null;
            var temporary = new List<Object>();
            var paths = new List<string>();
            var draws = new List<MapDraw>();
            var authoredRoots = new List<Transform>();
            int terrainCount = 0, rendererCount = 0, terrainVertices = 0;
            Bounds coverage = default;
            bool hasCoverage = false;
            try
            {
                foreach (var root in sourceScene.GetRootGameObjects())
                    foreach (var terrain in root.GetComponentsInChildren<Terrain>(true))
                    {
                        if (!terrain.enabled || !terrain.gameObject.activeInHierarchy || terrain.terrainData == null || !terrain.drawHeightmap) continue;
                        Mesh mesh = TerrainMesh(terrain);
                        temporary.Add(mesh);
                        terrainVertices += mesh.vertexCount;
                        draws.Add(new MapDraw(mesh));
                        Encapsulate(ref coverage, ref hasCoverage, mesh.bounds);
                        terrainCount++;
                        paths.Add("Terrain " + HierarchyPath(terrain.transform) + " | native heightmap " + terrain.terrainData.heightmapResolution);
                        for (var p = terrain.transform.parent; p != null; p = p.parent)
                            if (p.name == "TerrainGroup") { authoredRoots.Add(p); break; }
                    }

                foreach (var root in sourceScene.GetRootGameObjects())
                    foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        bool explicitSource = UnderAny(renderer.transform, explicitRoots);
                        if (!renderer.enabled || renderer.forceRenderingOff || (!renderer.gameObject.activeInHierarchy && !explicitSource)
                            || (!explicitSource && !renderer.gameObject.isStatic && !UnderAny(renderer.transform, authoredRoots))
                            || RejectedHierarchy(renderer.transform) || !HighestLod(renderer)) continue;
                        var filter = renderer.GetComponent<MeshFilter>();
                        if (filter == null || filter.sharedMesh == null || renderer.GetComponent<TextMesh>() != null) continue;
                        int firstDraw = draws.Count;
                        AppendRendererDraws(renderer, draws);
                        if (draws.Count == firstDraw) continue;
                        // A combined mesh's bounds includes OTHER renderers. Only this renderer owns these bounds.
                        Bounds bounds = RendererCoverage(renderer);
                        if (bounds.max.y < seaLevel)
                        {
                            draws.RemoveRange(firstDraw, draws.Count - firstDraw);
                            continue;
                        }
                        Encapsulate(ref coverage, ref hasCoverage, bounds);
                        rendererCount++;
                        paths.Add("Mesh " + HierarchyPath(renderer.transform) + " | " + filter.sharedMesh.name
                            + " | staticBatch=" + renderer.isPartOfStaticBatch + " firstSubmesh=" + renderer.subMeshStartIndex
                            + " opaqueDraws=" + (draws.Count - firstDraw) + " rendererBounds=" + bounds);
                    }

                if (!hasCoverage || coverage.size.x <= 0 || coverage.size.z <= 0 || terrainCount == 0)
                    throw new InvalidOperationException("No valid terrain coverage. Open the fishing island scene and inspect source selection.");
                int width = coverage.size.x >= coverage.size.z ? longEdge : Mathf.Max(64, Mathf.RoundToInt(longEdge * coverage.size.x / coverage.size.z));
                int height = coverage.size.z >= coverage.size.x ? longEdge : Mathf.Max(64, Mathf.RoundToInt(longEdge * coverage.size.z / coverage.size.x));
                pixels = CaptureDraws(draws, coverage, width, height, seaLevel);
                var resources=Path.Combine(Application.dataPath,"Resources");
                Directory.CreateDirectory(resources);
                File.WriteAllBytes(Path.Combine(resources,"FishingMap.png"), pixels.EncodeToPNG());
                AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.isReadable = false;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = longEdge;
                importer.SaveAndReimport();
                var definition = AssetDatabase.LoadAssetAtPath<FishingMapDefinition>(DefinitionPath);
                if (definition == null)
                {
                    definition = ScriptableObject.CreateInstance<FishingMapDefinition>();
                    AssetDatabase.CreateAsset(definition, DefinitionPath);
                }
                definition.version = FishingMapDefinition.CurrentVersion;
                definition.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
                definition.worldXZBounds = new Vector4(coverage.min.x, coverage.min.z, coverage.max.x, coverage.max.z);
                definition.seaLevel = seaLevel;
                definition.sourceScene = sourceScene.path;
                definition.bakedUtc = DateTime.UtcNow.ToString("O");
                definition.includesInitializedScenery = initialized;
                definition.terrainCount = terrainCount;
                definition.staticRendererCount = rendererCount;
                definition.terrainVertexCount = terrainVertices;
                definition.metersPerTexel = new Vector2(coverage.size.x / width, coverage.size.z / height);
                definition.captureNotes = "North = +Z; UV origin = SW. One-shot explicit CommandBuffer.DrawMesh, no camera render callbacks. Static batches use renderer.subMeshStartIndex + material slot, renderer.localToWorldMatrix and renderer.bounds; never clone the whole combined mesh. Native terrain heights/holes + eligible static visual meshes, no collider dependency. 真实世界高程从低岸沙色过渡到中段绿色、高岩灰色；不推断植被、水深、坡面阴影、净空或可通航路线。 Excludes transparent/cutout foliage, water, dynamic bodies, text, particles, lines, and collision-only objects. Coverage is a data envelope, NOT a playable boundary. "
                    + (initialized ? "Initialized scenery plus selected static roots captured; verify newly added shop/shore roots are selected." : "BASE ONLY: runtime reefs/market/shop absent; rebake after initialization.");
                paths.Sort(StringComparer.Ordinal);
                definition.sources = paths.ToArray();
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssetIfDirty(definition);
                Debug.Log("[FishingMap] Saved " + DefinitionPath + " | terrains=" + terrainCount + " static meshes=" + rendererCount + " bounds=" + definition.worldXZBounds + " m/texel=" + definition.metersPerTexel + " initialized=" + initialized + ". Inspect north/orientation and shoreline in Game view; this is not a navigation safety chart.");
                return definition;
            }
            finally
            {
                if (pixels != null) Object.DestroyImmediate(pixels);
                foreach (var item in temporary) if (item != null) Object.DestroyImmediate(item);
            }
        }

        /// <summary>GPU mesh reference plus ONE absolute submesh index and its actual rendering-space matrix.</summary>
        internal readonly struct MapDraw
        {
            internal readonly Mesh Mesh;
            internal readonly Matrix4x4 Matrix;
            internal readonly int Submesh;
            internal readonly MeshRenderer Source;
            internal MapDraw(Mesh terrain)
            { Mesh = terrain; Matrix = Matrix4x4.identity; Submesh = 0; Source = null; }
            internal MapDraw(MeshRenderer source, Mesh mesh, int submesh)
            { Mesh = mesh; Matrix = source.localToWorldMatrix; Submesh = submesh; Source = source; }
        }

        internal static Bounds RendererCoverage(MeshRenderer renderer) => renderer.bounds;

        internal static void AppendRendererDraws(MeshRenderer renderer, List<MapDraw> draws)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return;
            var mesh = filter.sharedMesh;
            var materials = renderer.sharedMaterials;
            // Unity 2022.3 MeshRenderer.subMeshStartIndex is the first subset owned by this renderer.
            // Renderer.localToWorldMatrix (NOT Transform's matrix) accounts for statically transformed vertices.
            // Neither metadata nor DrawMesh requires CPU vertex/index access, so GPU-only meshes remain valid.
            int first = renderer.subMeshStartIndex;
            int count = renderer.isPartOfStaticBatch ? materials.Length : Mathf.Min(materials.Length, mesh.subMeshCount - first);
            if (first < 0 || count < 0 || first + count > mesh.subMeshCount)
                throw new InvalidOperationException("Invalid static renderer submesh range: " + HierarchyPath(renderer.transform));
            for (int slot = 0; slot < count; slot++)
                if (OpaqueGeometry(materials[slot])) draws.Add(new MapDraw(renderer, mesh, first + slot));
        }

        /// <summary>Immediate offscreen draw, outside camera callbacks. No URP clear/final blit can overwrite it.
        /// Terrain and mesh subsets share the same explicit color/depth target, viewport and north-up projection.</summary>
        internal static Texture2D CaptureDraws(List<MapDraw> draws, Bounds coverage, int width, int height, float seaLevel)
        {
            if (Camera.current != null) throw new InvalidOperationException("Run map capture outside a camera render callback.");
            var shader = Resources.Load<Shader>("FishingMapBake");
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("FishingMapBake shader unavailable.");
            Scene preview = default;
            RenderTexture target = null;
            Texture2D pixels = null;
            Material material = null;
            CommandBuffer commands = null;
            var previousActive = RenderTexture.active;
            bool matrixPushed = false;
            try
            {
                preview = EditorSceneManager.NewPreviewScene();
                var camera = NewPreviewObject("Fishing map projection only (never rendered)", preview).AddComponent<Camera>();
                camera.enabled = false;
                camera.cameraType = CameraType.Preview;
                camera.scene = preview;
                camera.orthographic = true;
                camera.orthographicSize = coverage.size.z * .5f;
                camera.aspect = coverage.size.x / coverage.size.z;
                camera.transform.SetPositionAndRotation(new Vector3(coverage.center.x, coverage.max.y + 10, coverage.center.z), Quaternion.LookRotation(Vector3.down, Vector3.forward));
                camera.nearClipPlane = .1f;
                camera.farClipPlane = coverage.size.y + 20;
                target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                material.SetFloat("_SeaLevel", seaLevel);
                material.SetColor("_LowColor", QualitySettings.activeColorSpace == ColorSpace.Linear ? LowColor.linear : LowColor);
                material.SetColor("_LandColor", QualitySettings.activeColorSpace == ColorSpace.Linear ? LandColor.linear : LandColor);
                material.SetColor("_HighColor", QualitySettings.activeColorSpace == ColorSpace.Linear ? HighColor.linear : HighColor);
                commands = new CommandBuffer { name = "Fishing map renderer-owned submeshes" };
                commands.SetRenderTarget(target);
                commands.SetViewport(new Rect(0, 0, width, height));
                commands.ClearRenderTarget(true, true, QualitySettings.activeColorSpace == ColorSpace.Linear ? WaterColor.linear : WaterColor);
                // 与 URP 14 ScriptableRenderer.SetCameraMatrices 一致：此接口内部会转换 GPU 投影，不能重复转换。
                commands.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);
                foreach (var draw in draws) commands.DrawMesh(draw.Mesh, draw.Matrix, material, draw.Submesh, 0);
                GL.PushMatrix();
                matrixPushed = true;
                Graphics.ExecuteCommandBuffer(commands);
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                pixels.Apply(false, false);
                var result = pixels;
                pixels = null;
                return result;
            }
            finally
            {
                if (matrixPushed) GL.PopMatrix();
                RenderTexture.active = previousActive;
                try { if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview); }
                finally
                {
                    if (commands != null) commands.Release();
                    if (target != null) RenderTexture.ReleaseTemporary(target);
                    if (material != null) Object.DestroyImmediate(material);
                    if (pixels != null) Object.DestroyImmediate(pixels);
                }
            }
        }

        private static GameObject NewPreviewObject(string name, Scene scene)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave, layer = 0 };
            try { SceneManager.MoveGameObjectToScene(go, scene); return go; }
            catch { Object.DestroyImmediate(go); throw; }
        }

        internal static Mesh TerrainMesh(Terrain source)
        {
            var data = source.terrainData;
            int n = data.heightmapResolution;
            if ((long)n * n > 2000000) throw new InvalidOperationException("Native terrain exceeds the offline 2M-vertex guard: " + source.name + ". Split the source or explicitly implement a documented sampling policy; no silent downsampling.");
            float[,] heights = data.GetHeights(0, 0, n, n);
            int holesN = data.holesResolution;
            bool[,] holes = data.GetHoles(0, 0, holesN, holesN);
            var vertices = new Vector3[n * n];
            var indices = new List<int>((n - 1) * (n - 1) * 6);
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    vertices[z * n + x] = source.transform.TransformPoint(new Vector3(x * data.size.x / (n - 1), heights[z, x] * data.size.y, z * data.size.z / (n - 1)));
                    if (x == n - 1 || z == n - 1 || !holes[z * holesN / (n - 1), x * holesN / (n - 1)]) continue;
                    int i = z * n + x;
                    indices.Add(i); indices.Add(i + n); indices.Add(i + 1);
                    indices.Add(i + 1); indices.Add(i + n); indices.Add(i + n + 1);
                }
            var mesh = new Mesh { name = "Map native terrain " + source.name, indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.HideAndDontSave };
            try { mesh.vertices = vertices; mesh.SetTriangles(indices, 0); mesh.RecalculateBounds(); return mesh; }
            catch { Object.DestroyImmediate(mesh); throw; }
        }

        private static bool UnderAny(Transform item, List<Transform> roots)
        {
            foreach (var root in roots) if (root != null && (item == root || item.IsChildOf(root))) return true;
            return false;
        }

        private static bool RejectedHierarchy(Transform transform)
        {
            for (var p = transform; p != null; p = p.parent)
            {
                string name = p.name.ToLowerInvariant();
                if (name.Contains("invisible") || name.Contains("collision") || name.Contains("boundary") || name.Contains("racewall")
                    || name.Contains("weather") || name.Contains("cloud") || name.Contains("skybox")
                    || p.GetComponent<Rigidbody>() != null || p.GetComponent<Camera>() != null || p.GetComponent<WaterSystem.Water>() != null) return true;
            }
            return false;
        }

        private static bool OpaqueGeometry(Material material)
        {
            if (material == null || material.shader == null || material.renderQueue >= 2450) return false;
            string shader = material.shader.name.ToLowerInvariant();
            if (shader.Contains("water") || shader.Contains("invisible")) return false;
            string type = material.GetTag("RenderType", false, "Opaque");
            if (type == "Transparent" || type == "TransparentCutout") return false;
            if (material.HasProperty("_BaseColor") && material.GetColor("_BaseColor").a < .01f) return false;
            if (material.HasProperty("_Color") && material.GetColor("_Color").a < .01f) return false;
            return true;
        }

        private static bool HighestLod(MeshRenderer renderer)
        {
            var group = renderer.GetComponentInParent<LODGroup>(true);
            if (group == null) return true;
            var lods = group.GetLODs();
            bool inLowerLod = false;
            for (int i = 0; i < lods.Length; i++)
                foreach (var item in lods[i].renderers)
                    if (item == renderer) { if (i == 0) return true; inLowerLod = true; }
            return !inLowerLod;
        }

        private static void Encapsulate(ref Bounds coverage, ref bool exists, Bounds bounds)
        {
            if (!FishingMapMath.Finite(bounds.min.x) || !FishingMapMath.Finite(bounds.min.y) || !FishingMapMath.Finite(bounds.min.z)
                || !FishingMapMath.Finite(bounds.max.x) || !FishingMapMath.Finite(bounds.max.y) || !FishingMapMath.Finite(bounds.max.z))
                throw new InvalidOperationException("Non-finite source geometry bounds.");
            if (exists) coverage.Encapsulate(bounds); else { coverage = bounds; exists = true; }
        }

        private static string HierarchyPath(Transform item)
        {
            string path = item.name;
            for (var p = item.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return path;
        }
    }
}
