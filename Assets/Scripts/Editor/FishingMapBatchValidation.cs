using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BoatAttack.Editor
{
    /// <summary>Actual GPU/pixel regression for map subset ownership, not a color-count or UV-only smoke test.
    /// Call Run() from the existing Map suite, outside a camera render callback. Never saves author scenes/assets.</summary>
    public static class FishingMapBatchValidation
    {
        private static readonly Bounds Frame = new Bounds(new Vector3(0, 4, 0), new Vector3(64, 20, 64));
        private const int Resolution = 512;

        [MenuItem("Tools/Fishing/Map/Validate static-batch capture")]
        public static void Run()
        {
            int assertions = 0;
            void Check(bool condition, string text)
            {
                if (!condition) throw new InvalidOperationException("[FishingMapBatch] " + text);
                assertions++;
            }
            RunFixture(false, Check); // Scene-root batches: the real failing Combined Mesh (root: scene) case.
            RunFixture(true, Check);  // Nonidentity batch root: catches using Transform instead of render matrix.
            Debug.Log("[FishingMapBatch] PASS " + assertions + " structural/world-position/pixel assertions; scene-root and transformed-root batches, GPU-only meshes, inactive geometry, opaque subsets and terrain holes.");
        }

        private static void RunFixture(bool transformedRoot, Action<bool, string> check)
        {
            Scene scene = default;
            var owned = new List<Object>();
            var ownedMeshes = new HashSet<Mesh>();
            string mode = transformedRoot ? "transformed root" : "scene root";
            try
            {
                scene = EditorSceneManager.NewPreviewScene();
                var batchRoot = NewObject("Map batch fixture " + mode, scene);
                batchRoot.transform.SetPositionAndRotation(new Vector3(1, 0, -2), Quaternion.Euler(0, 17, 0));
                batchRoot.transform.localScale = new Vector3(1.2f, 1, .85f);
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) throw new InvalidOperationException("URP Unlit fixture shader unavailable.");
                var opaque = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, renderQueue = 2000 };
                var transparent = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, renderQueue = 3000 };
                owned.Add(opaque); owned.Add(transparent);
                var shared = TwoPanels(); owned.Add(shared); ownedMeshes.Add(shared);
                var batch = new MeshRenderer[3];
                batch[0] = Renderer("A", scene, batchRoot.transform, shared, new Vector3(-11, 0, -8), 13, new Vector3(1.3f, 1, 1), opaque, transparent);
                batch[1] = Renderer("B", scene, batchRoot.transform, shared, new Vector3(8, 0, 9), 72, new Vector3(.8f, 1, 1.3f), opaque, transparent);
                batch[2] = Renderer("C swapped slots", scene, batchRoot.transform, shared, new Vector3(10, 0, -8), -34, new Vector3(1.1f, 1, .7f), transparent, opaque);
                var positions = new Vector3[3];
                var excludedPositions = new Vector3[3];
                var expectedBounds = new Bounds[3];
                var expectedVertices = new Vector3[3][];
                for (int i = 0; i < batch.Length; i++)
                {
                    int opaqueSlot = i == 2 ? 1 : 0;
                    positions[i] = batch[i].transform.TransformPoint(PanelCenter(opaqueSlot));
                    excludedPositions[i] = batch[i].transform.TransformPoint(PanelCenter(1 - opaqueSlot));
                    expectedBounds[i] = FishingMapBaker.RendererCoverage(batch[i]);
                    expectedVertices[i] = Array.ConvertAll(shared.vertices, vertex => batch[i].transform.TransformPoint(vertex));
                }
                var gpuMesh = TwoPanels(); owned.Add(gpuMesh); ownedMeshes.Add(gpuMesh);
                var inactive = Renderer("Inactive GPU-only habitat", scene, null, gpuMesh, new Vector3(-15, 0, 12), -21, Vector3.one, opaque, transparent);
                inactive.gameObject.isStatic = false;
                inactive.gameObject.SetActive(false);
                gpuMesh.UploadMeshData(true);
                Vector3 inactivePosition = inactive.transform.TransformPoint(PanelCenter(0));
                check(!gpuMesh.isReadable && !inactive.gameObject.activeInHierarchy, mode + ": inactive GPU-only control is real");

                // Native heightmap/holes still use the same production terrain conversion and draw path.
                var terrainData = new TerrainData { heightmapResolution = 33, size = new Vector3(4, 4, 4) };
                owned.Add(terrainData);
                var heights = new float[33, 33];
                var holes = new bool[32, 32];
                for (int z = 0; z < 33; z++) for (int x = 0; x < 33; x++) heights[z, x] = .5f;
                for (int z = 0; z < 32; z++) for (int x = 0; x < 32; x++) holes[z, x] = !(x >= 12 && x < 20 && z >= 12 && z < 20);
                terrainData.SetHeights(0, 0, heights);
                terrainData.SetHoles(0, 0, holes);
                var terrain = NewObject("Terrain with hole", scene).AddComponent<Terrain>();
                terrain.terrainData = terrainData;
                terrain.transform.position = new Vector3(-3, 0, 11);
                var terrainMesh = FishingMapBaker.TerrainMesh(terrain); owned.Add(terrainMesh); ownedMeshes.Add(terrainMesh);
                var all = new[] { batch[0], batch[1], batch[2], inactive };
                var referenceAll = Capture(all, terrainMesh, owned);
                var referenceA = Capture(new[] { batch[0] }, null, owned);
                var referenceB = Capture(new[] { batch[1] }, null, owned);
                ValidatePositions(referenceAll, positions, excludedPositions, inactivePosition, true, -1, check, mode + " reference");
                check(LandAt(referenceAll, new Vector3(-2.5f, 2, 11.5f)) && !LandAt(referenceAll, new Vector3(-1, 2, 13)), mode + ": terrain surface and actual hole retained");

                StaticBatchingUtility.Combine(new[] { batch[0].gameObject, batch[1].gameObject, batch[2].gameObject }, transformedRoot ? batchRoot : null);
                bool offsetSeen = false, sharedCombinedSeen = false;
                for (int i = 0; i < batch.Length; i++)
                {
                    check(batch[i].isPartOfStaticBatch, mode + ": fixture renderer really belongs to a static batch " + i);
                    var mesh = batch[i].GetComponent<MeshFilter>().sharedMesh;
                    if (ownedMeshes.Add(mesh)) owned.Add(mesh);
                    offsetSeen |= batch[i].subMeshStartIndex > 0;
                    for (int j = 0; j < i; j++) sharedCombinedSeen |= mesh == batch[j].GetComponent<MeshFilter>().sharedMesh;
                    Bounds actual = FishingMapBaker.RendererCoverage(batch[i]);
                    // 非单位批处理根会重新计算保守 AABB，不能要求前后浮点边界完全相同。
                    // 验证它包含自己的真实几何且不吞入其他批成员；下方另做整幅像素等价检查。
                    var padded = actual;
                    padded.Expand(.005f);
                    foreach (var vertex in expectedVertices[i])
                        check(padded.Contains(vertex), mode + ": renderer coverage contains its geometry " + i
                            + " expected=" + expectedBounds[i] + " actual=" + actual + " vertex=" + vertex);
                    for (int j = 0; j < batch.Length; j++)
                        if (j != i) check(!padded.Contains(positions[j]), mode + ": coverage does not include another batch member " + i + " -> " + j
                            + " expected=" + expectedBounds[i] + " actual=" + actual);
                    if (mesh.isReadable) mesh.UploadMeshData(true);
                    check(!mesh.isReadable, mode + ": combined mesh is GPU-only " + i);
                    var subset = new List<FishingMapBaker.MapDraw>();
                    FishingMapBaker.AppendRendererDraws(batch[i], subset);
                    int slot = i == 2 ? 1 : 0;
                    check(subset.Count == 1 && subset[0].Submesh == batch[i].subMeshStartIndex + slot
                        && subset[0].Matrix == batch[i].localToWorldMatrix,
                        mode + ": exact opaque absolute subset and rendering-space matrix " + i);
                }
                check(offsetSeen && sharedCombinedSeen, mode + ": fixture exercises nonzero subset offsets in a shared combined mesh");
                var actualAll = Capture(all, terrainMesh, owned);
                var actualA = Capture(new[] { batch[0] }, null, owned);
                var actualB = Capture(new[] { batch[1] }, null, owned);
                ValidatePositions(actualAll, positions, excludedPositions, inactivePosition, true, -1, check, mode + " combined");
                ValidatePositions(actualA, positions, excludedPositions, inactivePosition, false, 0, check, mode + " only A");
                ValidatePositions(actualB, positions, excludedPositions, inactivePosition, false, 1, check, mode + " only B");
                Equivalent(referenceAll, actualAll, check, mode + ": entire batch retains exact footprint once");
                Equivalent(referenceA, actualA, check, mode + ": only A never brings along B/C or combined-mesh duplicates");
                Equivalent(referenceB, actualB, check, mode + ": only B honors its nonzero start index");
                check(Components(actualA) == 1 && Components(actualB) == 1 && Components(actualAll) == 5,
                    mode + ": one component per selected panel, plus inactive control and holed terrain; no repeated islands");
            }
            finally
            {
                try { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); }
                finally { foreach (var item in owned) if (item != null) Object.DestroyImmediate(item); }
            }
        }

        private static Texture2D Capture(MeshRenderer[] renderers, Mesh terrain, List<Object> owned)
        {
            var draws = new List<FishingMapBaker.MapDraw>();
            foreach (var renderer in renderers) FishingMapBaker.AppendRendererDraws(renderer, draws);
            if (terrain != null) draws.Add(new FishingMapBaker.MapDraw(terrain));
            var texture = FishingMapBaker.CaptureDraws(draws, Frame, Resolution, Resolution, 0);
            owned.Add(texture);
            return texture;
        }

        private static void ValidatePositions(Texture2D image, Vector3[] positions, Vector3[] excluded, Vector3 inactive,
            bool includeInactive, int only, Action<bool, string> check, string label)
        {
            for (int i = 0; i < positions.Length; i++)
            {
                check(LandAt(image, positions[i]) == (only < 0 || only == i), label + ": known world position " + i);
                check(!LandAt(image, excluded[i]), label + ": transparent submesh does not enter map " + i);
            }
            check(LandAt(image, inactive) == includeInactive, label + ": inactive GPU-only control appears iff requested; north-up position intact");
        }

        private static bool LandAt(Texture2D image, Vector3 world)
        {
            // Independent world-to-pixel expectation, with +Z at the TOP of the saved texture.
            int x = Mathf.Clamp(Mathf.FloorToInt((world.x - Frame.min.x) / Frame.size.x * image.width), 0, image.width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt((world.z - Frame.min.z) / Frame.size.z * image.height), 0, image.height - 1);
            return Different(image.GetPixel(x, y), image.GetPixel(0, 0));
        }

        private static bool Different(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) > .08f;

        private static bool[] Mask(Texture2D image)
        {
            var pixels = image.GetPixels();
            var mask = new bool[pixels.Length];
            for (int i = 0; i < mask.Length; i++) mask[i] = Different(pixels[i], pixels[0]);
            return mask;
        }

        private static void Equivalent(Texture2D expected, Texture2D actual, Action<bool, string> check, string label)
        {
            var a = Mask(expected); var b = Mask(actual);
            int mismatches = 0, foreground = 0;
            for (int i = 0; i < a.Length; i++) { if (a[i]) foreground++; if (a[i] != b[i]) mismatches++; }
            // Small edge rasterization tolerance only; not a percentage that could hide a small missing island.
            check(foreground > 30 && mismatches <= 12, label + " (different pixels=" + mismatches + ")");
        }

        private static int Components(Texture2D image)
        {
            var mask = Mask(image);
            int count = 0, w = image.width;
            var queue = new Queue<int>();
            for (int i = 0; i < mask.Length; i++)
            {
                if (!mask[i]) continue;
                count++; mask[i] = false; queue.Enqueue(i);
                while (queue.Count > 0)
                {
                    int p = queue.Dequeue();
                    Visit(p % w > 0 ? p - 1 : -1); Visit(p % w < w - 1 ? p + 1 : -1);
                    Visit(p >= w ? p - w : -1); Visit(p + w < mask.Length ? p + w : -1);
                }
            }
            return count;
            void Visit(int p) { if (p >= 0 && mask[p]) { mask[p] = false; queue.Enqueue(p); } }
        }

        private static MeshRenderer Renderer(string name, Scene scene, Transform parent, Mesh mesh, Vector3 position,
            float yaw, Vector3 scale, Material first, Material second)
        {
            var go = NewObject(name, scene);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            go.transform.localScale = scale;
            go.isStatic = true;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { first, second };
            return renderer;
        }

        private static GameObject NewObject(string name, Scene scene)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            try { SceneManager.MoveGameObjectToScene(go, scene); return go; }
            catch { Object.DestroyImmediate(go); throw; }
        }

        private static Vector3 PanelCenter(int slot) => new Vector3(slot == 0 ? -1.6f : 1.6f, 2, 0);

        private static Mesh TwoPanels()
        {
            var mesh = new Mesh { name = "Map fixture two disjoint material subsets", hideFlags = HideFlags.HideAndDontSave };
            var vertices = new Vector3[8];
            for (int i = 0; i < 2; i++)
            {
                Vector3 c = PanelCenter(i);
                vertices[i * 4] = c + new Vector3(-.8f, 0, -1);
                vertices[i * 4 + 1] = c + new Vector3(-.8f, 0, 1);
                vertices[i * 4 + 2] = c + new Vector3(.8f, 0, 1);
                vertices[i * 4 + 3] = c + new Vector3(.8f, 0, -1);
            }
            mesh.vertices = vertices;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.SetTriangles(new[] { 4, 5, 6, 4, 6, 7 }, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
