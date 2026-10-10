using System;
using System.IO;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    /// <summary>Explicit asset authoring, never an automatic scene rebuild.</summary>
    public static class FantasyCashierBuilder
    {
        private const string Folder = FantasyCashierImporter.Folder;
        public const string PrefabPath = Folder + "Cashier.prefab";

        [MenuItem("BoatAttack/Fishing/Legacy cashier/Build and nest in shop")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before authoring cashier assets.");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader missing.");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(Folder + "FantasyGirl.mat");
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, Folder + "FantasyGirl.mat"); }
            mat.shader = shader;
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BaseMap", Required<Texture2D>("FantasyGirl_basecolor.jpg"));
            mat.SetTexture("_BumpMap", Required<Texture2D>("FantasyGirl_normal.jpg"));
            mat.SetTexture("_MetallicGlossMap", Required<Texture2D>("FantasyGirl_metallic_smoothness.png"));
            mat.SetFloat("_Metallic", 1f); mat.SetFloat("_Smoothness", 1f);
            mat.SetFloat("_SmoothnessTextureChannel", 0f); mat.SetFloat("_BumpScale", 1f);
            mat.SetFloat("_Surface", 0f); mat.SetFloat("_Cull", (float)CullMode.Off);
            mat.EnableKeyword("_NORMALMAP"); mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            mat.enableInstancing = true; EditorUtility.SetDirty(mat);
            var root = new GameObject("Cashier - FantasyGirl", typeof(FishingShopCashier), typeof(LODGroup));
            try
            {
                var lods = new LOD[2];
                for (int i = 0; i < lods.Length; i++)
                {
                    var model = Object.Instantiate(Required<GameObject>("FantasyGirl_LOD" + i + ".fbx"), root.transform, false);
                    model.name = "FantasyGirl LOD" + i;
                    var renderers = model.GetComponentsInChildren<Renderer>();
                    foreach (var r in renderers) { r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.On; }
                    lods[i] = new LOD(i == 0 ? .16f : .012f, renderers);
                }
                var group = root.GetComponent<LODGroup>();group.SetLODs(lods);group.RecalculateBounds();
                Validate(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
            // Add a nested prefab at the existing marker. Preserve all authored shop geometry.
            var shopRoot = PrefabUtility.LoadPrefabContents(BeachTackleShopBuilder.PrefabPath);
            try
            {
                var shop = shopRoot.GetComponent<FishingTackleShop>();
                var existing = shop.GetComponentInChildren<FishingShopCashier>(true);
                if (existing == null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(Required<GameObject>("Cashier.prefab"), shop.clerkSpaces[0]);
                    instance.transform.localPosition = Vector3.zero;
                    instance.transform.localRotation = Quaternion.Euler(0, 180, 0);
                }
                PrefabUtility.SaveAsPrefabAsset(shopRoot, BeachTackleShopBuilder.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(shopRoot); }
            AssetDatabase.SaveAssets();
            Debug.Log("[CASHIER] PASS: URP textures, two mesh LODs and nested shop cashier authored; original blend untouched.");
        }
        private static T Required<T>(string file) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(Folder + file);
            if (asset == null) throw new InvalidOperationException("Missing cashier asset: " + file);
            return asset;
        }
        private static void Validate(GameObject root)
        {
            var filters = root.GetComponentsInChildren<MeshFilter>();
            if (filters.Length != 2 || root.GetComponentsInChildren<Collider>().Length != 0) throw new Exception("Cashier LOD/collider structure invalid.");
            int index = 0;
            foreach (var f in filters)
            {
                var mesh = f.sharedMesh;
                if (mesh == null || mesh.subMeshCount != 1 || mesh.GetIndexCount(0)/3 > (index++ == 0 ? 71000 : 23000)) throw new Exception("Cashier mesh budget invalid.");
                var b = f.GetComponent<Renderer>().bounds;
                if (Mathf.Abs(b.size.y - FishingShopCashier.AdultHeight) > .03f || Mathf.Abs(b.min.y) > .02f || b.size.x > 1.3f) throw new Exception("Cashier scale/orientation invalid: " + b);
            }
        }
        public static void BuildAndPreview()
        {
            try { Build(); Preview(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
        public static void Preview()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Unsaved preview is batch-only.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var shop = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BeachTackleShopBuilder.PrefabPath))).GetComponent<FishingTackleShop>();
            var cashier = shop.GetComponentInChildren<FishingShopCashier>();
            if (cashier == null || FishingShopCashier.Create(shop) != cashier || shop.GetComponentsInChildren<FishingShopCashier>().Length != 1) throw new Exception("Cashier missing or duplicated.");
            var local = shop.transform.InverseTransformPoint(cashier.transform.position);
            if (local.z < 3.5f || Vector3.Dot(cashier.transform.forward, -shop.transform.forward) < .99f) throw new Exception("Cashier not behind counter/facing customers.");
            RenderSettings.fog=false;RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.57f,.62f);
            var light = new GameObject("Preview key",typeof(Light)).GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.1f;light.transform.rotation=Quaternion.Euler(38,-25,0);
            var fill = new GameObject("Preview fill",typeof(Light)).GetComponent<Light>();fill.type=LightType.Directional;fill.intensity=.45f;fill.transform.rotation=Quaternion.Euler(25,145,0);
            var camera=new GameObject("Preview camera",typeof(Camera)).GetComponent<Camera>();camera.nearClipPlane=.05f;camera.farClipPlane=100;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.18f,.23f);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            ShaderUtil.allowAsyncCompilation=false;Directory.CreateDirectory("Logs/FantasyGirl");
            Shot(camera,new Vector3(-1.4f,1.58f,1.0f),new Vector3(-1.4f,1.58f,4),"counter.png",48);
            Shot(camera,new Vector3(0,1.65f,-2.8f),new Vector3(-.4f,1.4f,3.4f),"shop.png",64);
            var group=cashier.GetComponent<LODGroup>();group.ForceLOD(1);
            Shot(camera,new Vector3(-1.4f,1.58f,1.0f),new Vector3(-1.4f,1.58f,4),"counter-lod1.png",48);group.ForceLOD(-1);
            // Inspect the authored hands independently of the counter that hides the abdomen.
            shop.gameObject.SetActive(false);
            var studio = Object.Instantiate(Required<GameObject>("Cashier.prefab"));
            studio.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            cashier = studio.GetComponent<FishingShopCashier>();
            group = cashier.GetComponent<LODGroup>();
            Shot(camera,cashier.transform.TransformPoint(new Vector3(0,1.20f,2.8f)),cashier.transform.TransformPoint(new Vector3(0,1.15f,0)),"cashier-full.png",48);
            Shot(camera,cashier.transform.TransformPoint(new Vector3(.65f,1.45f,1.5f)),cashier.transform.TransformPoint(new Vector3(0,1.35f,.08f)),"cashier-hands.png",45);
            group.ForceLOD(1);
            Shot(camera,cashier.transform.TransformPoint(new Vector3(.65f,1.45f,1.5f)),cashier.transform.TransformPoint(new Vector3(0,1.35f,.08f)),"cashier-hands-lod1.png",45);
            group.ForceLOD(-1);
            Object.DestroyImmediate(studio);
            Debug.Log("[CASHIER] PASS: placement, deduplication and both LODs previewed. Not device/performance certification.");
        }
        private static void Shot(Camera camera,Vector3 eye,Vector3 target,string file,float fov)
        {
            camera.transform.position=eye;camera.transform.LookAt(target);camera.fieldOfView=fov;
            var rt=RenderTexture.GetTemporary(1400,1000,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var previous=RenderTexture.active;Texture2D image=null;
            try
            {
                camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;
                image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes("Logs/FantasyGirl/"+file,image.EncodeToPNG());
            }
            finally { camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);if(image!=null)Object.DestroyImmediate(image); }
        }
    }
}
