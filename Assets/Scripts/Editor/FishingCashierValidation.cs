using System;
using System.Linq;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    /// <summary>仅在显式调用时生成和检查店员资源，不重建或保存海岛场景及原商店。</summary>
    public static class FishingCashierValidation
    {
        public const string Folder = "Assets/Resources/FishingCashier/FantasyGirl/";
        public const string PrefabPath = Folder + "Cashier.prefab";
        private const string ShopPrefab = "Assets/Objects/BeachTackleShop/BeachTackleShop.prefab";

        public static void BuildAndValidate() => Execute(() => { BuildAssets(); ValidateAssets(); });
        public static void Run() => Execute(ValidateAssets);

        // Preserve prefab file IDs and nested shop references when only the FBXs change.
        public static void RefreshBoundsAndValidate() => Execute(() =>
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                root.GetComponent<LODGroup>().RecalculateBounds();
                Check(PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) != null, "updated LOD bounds saved without rebuilding the shop");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            ValidateAssets();
        });

        private static void Execute(Action work)
        {
            try
            {
                work();
                Debug.Log("[CASHIER] PASS: production cashier assets, placement and production deduplication checked. Actual Player/headset rendering is a separate acceptance step.");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        [MenuItem("BoatAttack/Fishing/Build FantasyGirl cashier assets")]
        public static void BuildAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before authoring cashier assets.");
            for (int i = 0; i < 2; i++) ConfigureModel(Folder + "FantasyGirl_LOD" + i + ".fbx");
            ConfigureTexture(Folder + "FantasyGirl_basecolor.jpg", false);
            ConfigureTexture(Folder + "FantasyGirl_normal.jpg", true);
            // 显式引用包内 Shader，避免冷导入时 Shader.Find 尚未注册。
            // 不修改工程级 URP 设置。
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader");
            Check(shader != null, "installed URP Lit shader exists");
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "FantasyGirl.mat");
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, Folder + "FantasyGirl.mat");
            }
            material.shader = shader;
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", Required<Texture2D>("FantasyGirl_basecolor.jpg"));
            material.SetTexture("_BumpMap", Required<Texture2D>("FantasyGirl_normal.jpg"));
            material.SetFloat("_BumpScale", 1f);
            material.SetTexture("_MetallicGlossMap", Required<Texture2D>("FantasyGirl_metallic_smoothness.png"));
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_Metallic", 1);
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_Surface", 0);
            material.SetFloat("_AlphaClip", 0);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetFloat("_ZWrite", 1);
            material.EnableKeyword("_NORMALMAP");
            foreach (var keyword in new[] { "_ALPHATEST_ON", "_SURFACE_TYPE_TRANSPARENT", "_ALPHAPREMULTIPLY_ON" })
                material.DisableKeyword(keyword);
            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = -1;
            material.enableInstancing = true;
            material.doubleSidedGI = true;
            EditorUtility.SetDirty(material);

            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("Cashier - FantasyGirl", typeof(FishingShopCashier), typeof(LODGroup));
                SceneManager.MoveGameObjectToScene(root, preview);
                var lods = new LOD[2];
                for (int i = 0; i < lods.Length; i++)
                {
                    var model = Object.Instantiate(Required<GameObject>("FantasyGirl_LOD" + i + ".fbx"), root.transform, false);
                    model.name = "FantasyGirl LOD" + i;
                    foreach (var child in model.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 0;
                    var renderers = model.GetComponentsInChildren<Renderer>(true);
                    Check(renderers.Length == 1, "one renderer in imported LOD " + i);
                    foreach (var renderer in renderers)
                    {
                        renderer.sharedMaterial = material;
                        renderer.enabled = true;
                        renderer.shadowCastingMode = ShadowCastingMode.On;
                        renderer.receiveShadows = true;
                    }
                    lods[i] = new LOD(i == 0 ? .16f : .012f, renderers);
                }
                var group = root.GetComponent<LODGroup>();
                group.SetLODs(lods);
                group.fadeMode = LODFadeMode.None;
                group.RecalculateBounds();
                Check(PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) != null, "cashier prefab saved");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            AssetDatabase.SaveAssetIfDirty(material);
            Debug.Log("[CASHIER] Built only CC0 cashier import settings, one URP material and Resources prefab. Island and shop prefab were not rewritten.");
        }

        private static void ConfigureModel(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            Check(importer != null, "derived static FBX exists: " + path);
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        private static void ConfigureTexture(string path, bool normal)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Check(importer != null, "cashier atlas exists: " + path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048;
            importer.anisoLevel = 2;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        [MenuItem("BoatAttack/Fishing/Validate production cashier assets")]
        public static void ValidateAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Use ValidateInScene in Play Mode; asset validation uses a separate preview scene.");
            Check(Resources.Load<GameObject>(FishingShopCashier.ResourcePath) != null, "production Resources path resolves");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShopPrefab);
            Check(prefab != null, "existing authored shop prefab exists");
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                var shop = root.GetComponent<FishingTackleShop>();
                Check(shop != null && shop.clerkSpaces.Length == 2 && shop.customerSpaces.Length == 4, "original clerk/customer anchors retained");
                Check(Vector3.Distance(shop.transform.InverseTransformPoint(shop.clerkSpaces[0].position), new Vector3(-1.4f, 0, 4)) < .001f, "original first clerk anchor retained");
                var cashier = shop.EnsureCashier();
                Check(cashier != null && shop.EnsureCashier() == cashier && FishingShopCashier.Create(shop) == cashier, "repeated production creation returns one attendant");
                ValidateInstance(shop, cashier);
                Check(shop.clerkSpaces[1].childCount == 0 && shop.customerSpaces.All(t => t.childCount == 0), "other five NPC reservations remain empty");
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        /// <summary>串行 Player 测试走到真实商店柜台后调用此入口。</summary>
        public static void ValidateInScene(FishingPrototype controller, bool requireInView = true)
        {
            Check(controller != null && controller.TackleShop != null, "production controller has the installed shop");
            var shop = controller.TackleShop;
            // 此处不能补建缺失角色，必须证明正式 Bind/Start 已经执行。
            Check(shop.Cashier != null, "production Bind/Start created the attendant");
            ValidateInstance(shop, shop.Cashier);
            if (!requireInView) return;
            var camera = controller.viewCamera;
            Check(camera != null, "production view camera exists");
            var face = shop.Cashier.FacePosition;
            var viewport = camera.WorldToViewportPoint(face);
            Check(viewport.z > camera.nearClipPlane && viewport.z < camera.farClipPlane
                && viewport.x > .05f && viewport.x < .95f && viewport.y > .05f && viewport.y < .95f,
                "attendant face lies inside the actual customer camera: " + viewport);
            Check((camera.cullingMask & (1 << shop.Cashier.gameObject.layer)) != 0, "actual camera includes the attendant layer");
            Check(controller.Shore.ClearSight(camera.transform.position, face), "counter/customer sightline to face is not blocked");
            Debug.Log("[CASHIER] SCENE CHECK: actual actor/camera/sightline passed; inspect the full Player screenshot for lighting and appearance.");
        }

        private static void ValidateInstance(FishingTackleShop shop, FishingShopCashier cashier)
        {
            Check(cashier != null && cashier.isActiveAndEnabled && cashier.gameObject.activeInHierarchy, "attendant is active");
            Check(shop.GetComponentsInChildren<FishingShopCashier>(true).Length == 1, "exactly one attendant under shop");
            Check(cashier.transform.parent == shop.clerkSpaces[0] && cashier.transform.localPosition.sqrMagnitude < .000001f,
                "attendant feet use the original anchor without an extra position offset");
            Check(Vector3.Dot(cashier.transform.forward, -shop.transform.forward) > .999f, "attendant faces customers, not the back wall");
            Check(cashier.GetComponentsInChildren<Collider>(true).Length == 0, "visual NPC adds no collision or trade authority");
            Check(cashier.GetComponentsInChildren<Animator>(true).Length == 0,
                "procedural gestures do not depend on a missing Animator controller");
            Check(cashier.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == (cashier.Animated?2:0),
                "runtime gestures skin both LODs; source prefab remains reusable");
            var group = cashier.GetComponent<LODGroup>();
            Check(group != null && group.enabled, "LOD group is enabled");
            var lods = group.GetLODs();
            Check(lods.Length == 2 && Mathf.Approximately(lods[0].screenRelativeTransitionHeight, .16f)
                && Mathf.Approximately(lods[1].screenRelativeTransitionHeight, .012f), "near/far LOD thresholds retained");
            Bounds bounds = default;
            bool haveBounds = false;
            for (int i = 0; i < lods.Length; i++)
            {
                Check(lods[i].renderers.Length == 1, "one draw renderer per LOD " + i);
                var renderer = lods[i].renderers[0];
                Check(renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy && !renderer.forceRenderingOff
                    && renderer.shadowCastingMode == ShadowCastingMode.On && renderer.gameObject.layer == 0, "LOD is a visible Default-layer mesh " + i);
                var filter = renderer.GetComponentInParent<MeshFilter>();
                Check(filter != null && filter.sharedMesh != null && filter.sharedMesh.subMeshCount == 1, "single real human mesh/submesh " + i);
                uint triangles = filter.sharedMesh.GetIndexCount(0) / 3;
                Check(triangles > 0 && triangles <= (i == 0 ? 70000u : 22000u), "mesh budget LOD " + i + ": " + triangles);
                Check(renderer.sharedMaterials.Length == 1, "one shared character material " + i);
                var material = renderer.sharedMaterial;
                Check(material != null && material.shader != null && material.shader.name == "Universal Render Pipeline/Lit"
                    && material.GetTexture("_BaseMap") != null && material.GetTexture("_BumpMap") != null && material.IsKeywordEnabled("_NORMALMAP"),
                    "URP albedo/normal bindings valid " + i);
                Check(material.GetTexture("_MetallicGlossMap") != null && material.IsKeywordEnabled("_METALLICSPECGLOSSMAP"),
                    "original metallic/smoothness texture retained " + i);
                Check(material.GetFloat("_Surface") == 0 && material.GetFloat("_ZWrite") == 1 && material.GetColor("_BaseColor").a > .99f,
                    "opaque depth-writing character, not an invisible transparent material " + i);
                var sourceBounds=filter.sharedMesh.bounds;
                var geometry=new Bounds(renderer.transform.TransformPoint(sourceBounds.center),Vector3.Scale(sourceBounds.size,renderer.transform.lossyScale));
                if (haveBounds) bounds.Encapsulate(geometry);
                else { bounds = geometry; haveBounds = true; }
            }
            Check(Mathf.Abs(bounds.size.y - FishingShopCashier.AdultHeight) < .03f && Mathf.Abs(bounds.min.y - cashier.transform.position.y) < .025f,
                "enlarged attendant stands approximately 1.98m with feet at anchor: " + bounds);
            Check(bounds.size.x < 1.05f, "arms rest in front of the abdomen, not a wide T-pose");
        }

        private static T Required<T>(string file) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(Folder + file);
            if (asset == null) throw new InvalidOperationException("Missing cashier asset: " + Folder + file);
            return asset;
        }
        private static void Check(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException("[CASHIER] " + message);
            Debug.Log("[CASHIER] CHECK: " + message);
        }
    }
}
