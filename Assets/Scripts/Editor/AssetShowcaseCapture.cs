using System;
using System.Collections.Generic;
using System.IO;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    /// <summary>Edit-mode studio renders of the project's own finished 3-D assets, for coursework submission.
    /// Works in a throwaway empty scene with the real runtime meshes, shaders and materials;
    /// it never opens, edits or saves a project scene, prefab or asset.</summary>
    public static class AssetShowcaseCapture
    {
        private const string Marker = "[SHOWCASE]";
        private static readonly string Output =
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "Images", "AssetShowcase"));
        private static readonly List<Object> Owned = new List<Object>();
        private static GameObject _floor;
        private static readonly Color StudioAmbient = new Color(.52f, .54f, .58f);

        private struct Fish
        {
            public FishKind Kind;
            public bool Rare;
            public string File;
        }

        private static readonly Fish[] Catalogue =
        {
            new Fish { Kind = FishKind.Coral, File = "fish-01-coral" },
            new Fish { Kind = FishKind.Dart, File = "fish-02-dart" },
            new Fish { Kind = FishKind.Puffer, File = "fish-03-puffer" },
            new Fish { Kind = FishKind.Sailfish, File = "fish-04-sailfish" },
            new Fish { Kind = FishKind.Sailfish, Rare = true, File = "fish-05-sailfish-pearl" },
        };

        [MenuItem("BoatAttack/Fishing/Capture asset showcase")]
        public static void Run()
        {            var rendered = new List<string>();
            string error = "";
            try
            {
                if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before capturing.");
                Directory.CreateDirectory(Output);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var palette = Resources.Load<Shader>("FishingPalette");
                if (palette == null) throw new InvalidOperationException("Resources/FishingPalette shader missing.");
                var camera = Studio();

                foreach (var entry in Catalogue)
                {
                    var subject = new GameObject("Showcase subject").transform;
                    var fish = FishingArt.Fish(subject, Owned, palette, entry.Kind, entry.Rare);
                    // Authored nose-along-+Z; turn broadside so the whole silhouette reads.
                    fish.transform.localRotation = Quaternion.Euler(0, -90, 0);
                    Shoot(camera, subject, entry.File, 1600, 1000, -126f, 16f, 1.16f, .22f);
                    rendered.Add(entry.File);
                    Object.DestroyImmediate(subject.gameObject);
                }

                var school = new GameObject("Showcase subject").transform;
                // Spacing matches the real mesh sizes: sailfish 0.82 m, dart 0.32 m, coral 0.24 m, puffer 0.21 m.
                var shoal = new[]
                {
                    new KeyValuePair<FishKind, Vector3>(FishKind.Sailfish, new Vector3(0f, 0f, 0f)),
                    new KeyValuePair<FishKind, Vector3>(FishKind.Dart, new Vector3(-.25f, 0f, -.45f)),
                    new KeyValuePair<FishKind, Vector3>(FishKind.Coral, new Vector3(.18f, 0f, -.45f)),
                    new KeyValuePair<FishKind, Vector3>(FishKind.Puffer, new Vector3(-.05f, 0f, -.82f)),
                };
                foreach (var placement in shoal)
                {
                    var fish = FishingArt.Fish(school, Owned, palette, placement.Key, false);
                    fish.transform.localRotation = Quaternion.Euler(0, -90, 0);
                    fish.transform.localPosition = placement.Value;
                }
                Shoot(camera, school, "fish-06-species-set", 1800, 1250, -128f, 26f, 1.10f, .22f);
                rendered.Add("fish-06-species-set");
                Object.DestroyImmediate(school.gameObject);

                var buoyRoot = new GameObject("Showcase subject").transform;
                FishingArt.Buoy(buoyRoot, Owned, palette, Vector3.zero, 1f);
                Shoot(camera, buoyRoot, "props-11-navigation-buoy", 1050, 1450, -132f, 12f, 1.14f, 0f);
                rendered.Add("props-11-navigation-buoy");
                Object.DestroyImmediate(buoyRoot.gameObject);

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BeachTackleShopBuilder.PrefabPath);
                if (prefab == null) throw new InvalidOperationException("BeachTackleShop prefab missing.");
                var shopRoot = new GameObject("Showcase subject").transform;
                var shop = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                shop.transform.SetParent(shopRoot, false);
                // The walkway runs far out over the water; exclude it so the building itself fills the frame.
                var walkway = new[] { "Boardwalk", "Landing", "Access ramp" };
                shop.transform.localRotation = Quaternion.Euler(0, 24, 0);
                Shoot(camera, shopRoot, "shop-01-tide-and-tackle", 1800, 1250, -128f, 18f, 1.10f, 0f, walkway);
                rendered.Add("shop-01-tide-and-tackle");
                shop.transform.localRotation = Quaternion.Euler(0, 150, 0);
                Shoot(camera, shopRoot, "shop-02-tide-and-tackle-rear", 1800, 1250, -128f, 18f, 1.10f, 0f, walkway);
                rendered.Add("shop-02-tide-and-tackle-rear");
                shop.transform.localRotation = Quaternion.Euler(0, 24, 0);
                Shoot(camera, shopRoot, "shop-03-tide-and-tackle-boardwalk", 1800, 1100, -136f, 24f, 1.06f, 0f);
                rendered.Add("shop-03-tide-and-tackle-boardwalk");
                Object.DestroyImmediate(shopRoot.gameObject);
            }
            catch (Exception e)
            {
                error = e.ToString();
            }
            finally
            {
                foreach (var owned in Owned) if (owned != null) Object.DestroyImmediate(owned);
                Owned.Clear();
            }
            if (error == "")
                Debug.Log(Marker + " PASS: " + rendered.Count + " studio renders in " + Output +
                          "; edit-mode asset renders, not gameplay or VR evidence.");
            else Debug.LogError(Marker + " FAIL: " + error);
            if (Application.isBatchMode) EditorApplication.Exit(error == "" ? 0 : 1);
        }

        /// <summary>Pale sweep, two-light rig and an orthographic camera; identical exposure for every subject.</summary>
        private static Camera Studio()
        {
            // An unlit sweep is the only stable backdrop here: a Lit plane large enough for the 30 m shop
            // sits outside URP's shadow distance at the camera's pull-back and shades unpredictably.
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) throw new InvalidOperationException("URP Unlit shader missing.");
            var sweep = new Material(unlit) { name = "Showcase sweep" };
            sweep.SetColor("_BaseColor", new Color(.90f, .906f, .912f));
            Owned.Add(sweep);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Showcase sweep";
            floor.GetComponent<Renderer>().sharedMaterial = sweep;
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            _floor = floor;

            var key = new GameObject("Key light", typeof(Light)).GetComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.45f;
            key.color = new Color(1f, .98f, .94f);
            key.shadows = LightShadows.Soft;
            key.shadowStrength = .35f;
            key.transform.rotation = Quaternion.Euler(42f, -34f, 0f);
            var fill = new GameObject("Fill light", typeof(Light)).GetComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = .55f;
            fill.color = new Color(.90f, .94f, 1f);
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(18f, 128f, 0f);
            // A new scene keeps Default-Skybox, whose warm procedural sky tints every lit surface brown.
            // Ambient only switches to the flat colour once the environment SH is rebuilt.
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.52f, .54f, .58f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = false;
            DynamicGI.UpdateEnvironment();
            Debug.Log(Marker + " DIAG sweep=" + sweep.shader.name + " base=" + sweep.GetColor("_BaseColor") +
                      " space=" + QualitySettings.activeColorSpace +
                      " ambient=" + RenderSettings.ambientLight + "/" + RenderSettings.ambientMode +
                      " skybox=" + (RenderSettings.skybox == null ? "none" : RenderSettings.skybox.name));

            var camera = new GameObject("Showcase camera", typeof(Camera)).GetComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.90f, .906f, .912f);
            camera.nearClipPlane = .01f;
            camera.farClipPlane = 2000f;
            camera.enabled = false;
            return camera;
        }

        /// <summary>Renderer bounds, optionally ignoring sprawling parts so the subject fills the frame.</summary>
        private static Bounds WorldBounds(Transform subject, string[] exclude = null)
        {
            var bounds = new Bounds();
            bool first = true;
            foreach (var renderer in subject.GetComponentsInChildren<Renderer>())
            {
                if (exclude != null)
                {
                    bool skip = false;
                    foreach (var word in exclude) if (renderer.name.Contains(word)) { skip = true; break; }
                    if (skip) continue;
                }
                if (first) { bounds = renderer.bounds; first = false; }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (first) throw new InvalidOperationException("Subject has no renderer to frame: " + subject.name);
            return bounds;
        }

        /// <summary>Frames the silhouette exactly, then writes a supersampled PNG.</summary>
        private static void Shoot(Camera camera, Transform subject, string file, int width, int height,
                                 float azimuth, float elevation, float pad, float lift, string[] exclude = null)
        {
            // Runtime fish and buoys cast no shadow in game; a contact shadow is wanted in a product shot.
            foreach (var renderer in subject.GetComponentsInChildren<Renderer>())
                renderer.shadowCastingMode = ShadowCastingMode.On;
            // The shop prefab carries its own warm lamps, and a prefab's scripts may re-point the scene's
            // environment lighting. Re-assert the studio rig per shot so exposure never drifts.
            foreach (var light in subject.GetComponentsInChildren<Light>()) light.enabled = false;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = StudioAmbient;
            RenderSettings.fog = false;
            DynamicGI.UpdateEnvironment();
            var raw = WorldBounds(subject);
            subject.position += Vector3.up * (raw.size.magnitude * lift - raw.min.y);
            var bounds = WorldBounds(subject, exclude);

            int triangles = 0, meshes = 0;
            foreach (var filter in subject.GetComponentsInChildren<MeshFilter>())
                if (filter.sharedMesh != null) { triangles += filter.sharedMesh.triangles.Length / 3; meshes++; }
            foreach (var skin in subject.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (skin.sharedMesh != null) { triangles += skin.sharedMesh.triangles.Length / 3; meshes++; }
            var full = WorldBounds(subject);
            Debug.Log(Marker + " STAT " + file + " triangles=" + triangles + " meshes=" + meshes +
                      " metres=" + full.size.x.ToString("F2") + "x" + full.size.y.ToString("F2") +
                      "x" + full.size.z.ToString("F2") + " ambient=" + RenderSettings.ambientLight);

            // Keep the sweep just large enough for the subject: an oversized plane falls outside URP's
            // shadow distance at the camera's pull-back and then shades unpredictably.
            if (_floor != null && _floor.activeSelf)
            {
                float footprint = Mathf.Max(full.size.x, full.size.z, .2f) * 3f;
                _floor.transform.localScale = Vector3.one * (footprint / 10f);
                _floor.transform.position = new Vector3(full.center.x, full.min.y - footprint * .0015f, full.center.z);
            }

            var forward = Quaternion.Euler(elevation, azimuth, 0f) * Vector3.forward;
            camera.transform.SetPositionAndRotation(bounds.center - forward * (bounds.size.magnitude * 4f),
                                                    Quaternion.LookRotation(forward, Vector3.up));
            var right = camera.transform.right;
            var up = camera.transform.up;
            float minU = float.MaxValue, maxU = float.MinValue, minV = float.MaxValue, maxV = float.MinValue;
            for (int corner = 0; corner < 8; corner++)
            {
                var point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                    (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                float u = Vector3.Dot(point, right), v = Vector3.Dot(point, up);
                minU = Mathf.Min(minU, u); maxU = Mathf.Max(maxU, u);
                minV = Mathf.Min(minV, v); maxV = Mathf.Max(maxV, v);
            }
            float aspect = (float)width / height;
            camera.aspect = aspect;
            camera.orthographicSize = Mathf.Max(maxV - minV, (maxU - minU) / aspect) * .5f * pad;
            camera.transform.position += right * ((maxU + minU) * .5f - Vector3.Dot(bounds.center, right));
            camera.transform.position += up * ((maxV + minV) * .5f - Vector3.Dot(bounds.center, up));

            // Render at 2x and box-filter down; URP MSAA alone leaves hard edges on thin fins and railings.
            const int scale = 2;
            var rt = RenderTexture.GetTemporary(width * scale, height * scale, 24, RenderTextureFormat.ARGB32,
                                                RenderTextureReadWrite.Default, 8);
            var previousActive = RenderTexture.active;
            Texture2D large = null, small = null;
            try
            {
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                large = new Texture2D(width * scale, height * scale, TextureFormat.RGB24, false);
                large.ReadPixels(new Rect(0, 0, width * scale, height * scale), 0, 0);
                large.Apply();
                small = new Texture2D(width, height, TextureFormat.RGB24, false);
                var source = large.GetPixels();
                var target = new Color[width * height];
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        Color sum = default;
                        for (int dy = 0; dy < scale; dy++)
                            for (int dx = 0; dx < scale; dx++)
                                sum += source[(y * scale + dy) * width * scale + x * scale + dx];
                        target[y * width + x] = sum / (scale * scale);
                    }
                small.SetPixels(target);
                small.Apply();
                File.WriteAllBytes(Path.Combine(Output, file + ".png"), small.EncodeToPNG());
                Debug.Log(Marker + " IMAGE: " + file + ".png");
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(rt);
                if (large != null) Object.DestroyImmediate(large);
                if (small != null) Object.DestroyImmediate(small);
            }
        }
    }
}
