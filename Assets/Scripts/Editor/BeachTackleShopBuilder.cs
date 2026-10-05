using System;
using System.Collections.Generic;
using BoatAttack.Fishing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BoatAttack.EditorTools
{
    /// <summary>PR #17 (730de9e) Tide & Tackle art, retained under the repository's existing license.
    /// v11 installs the original editable prefab into the CURRENT fishing scene; it never regenerates that scene.</summary>
    public static class BeachTackleShopBuilder
    {
        public const string Folder = "Assets/Objects/BeachTackleShop";
        public const string PrefabPath = Folder + "/BeachTackleShop.prefab";
        // Surveyed placement from PR17, NOT its older fishing scene/controller.
        private static readonly Vector3 AuthoredPosition = new Vector3(-40, 3.5458403f, -86);
        private static readonly Quaternion AuthoredRotation = Quaternion.Euler(0, 210, 0);

        [MenuItem("BoatAttack/Fishing/Install or validate Tide and Tackle")]
        public static void Install()
        {
            try
            {
                if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before installing the shop.");
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                var scene = EditorSceneManager.OpenScene(FishingPrototypeEditor.ScenePath);
                var controller = Object.FindObjectOfType<FishingPrototype>();
                if (controller == null) throw new InvalidOperationException("Existing fishing controller missing; refusing to generate a scene.");
                var shops = Object.FindObjectsOfType<FishingTackleShop>(true);
                if (shops.Length > 1) throw new InvalidOperationException("Duplicate shop instances; resolve explicitly instead of deleting them.");
                FishingTackleShop shop;
                if (shops.Length == 1)
                {
                    shop = shops[0];
                    if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(shop.gameObject) != PrefabPath)
                        throw new InvalidOperationException("Existing shop is not the expected editable prefab; refusing to replace it.");
                }
                else
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                    if (prefab == null) throw new InvalidOperationException("Import the PR17 prefab and its metadata first.");
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    Undo.RegisterCreatedObjectUndo(instance, "Install Tide and Tackle");
                    instance.name = "BeachTackleShop";
                    instance.transform.SetPositionAndRotation(AuthoredPosition, AuthoredRotation);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
                    shop = instance.GetComponent<FishingTackleShop>();
                }
                shop.Bind(controller);
                if (!shop.Available) throw new InvalidOperationException("Shop must have active entrance, counter, mooring and landing deck references.");
                Physics.SyncTransforms();
                int hidden = HideIntersectingDecor(shop);
                Physics.SyncTransforms();
                ValidateRoute(shop);
                // No prefab Apply/SaveAssets, no other scene, no terrain or original prop assets are written.
                if (!EditorSceneManager.SaveScene(scene, FishingPrototypeEditor.ScenePath)) throw new Exception("Could not save target fishing scene.");
                Debug.Log("[SHOP-INSTALL] PASS: one editable PR17 prefab appended/validated at " + shop.Position
                    + "; " + hidden + " intersecting decoration instances hidden; original resources and v10 controller retained.");
                if (Application.isBatchMode) EditorApplication.Exit(0);
                else Selection.activeGameObject = shop.gameObject;
            }
            catch (Exception ex) { Debug.LogException(ex); if (Application.isBatchMode) EditorApplication.Exit(1); }
        }
        // Preserve the old editor entry point, but REMOVE its replace/regenerate behaviour.
        public static void Generate() => Install();

        private static int HideIntersectingDecor(FishingTackleShop shop)
        {
            var changed = new HashSet<GameObject>();
            var building = new Bounds(new Vector3(0, 1.5f, -.8f), new Vector3(12.4f, 9, 12.8f));
            float landingZ = shop.transform.InverseTransformPoint(shop.LandingPosition).z;
            var walkway = new Bounds(new Vector3(0, -1, (landingZ - 7) * .5f), new Vector3(3.6f, 6, -7 - landingZ + 3.4f));
            foreach (var renderer in Object.FindObjectsOfType<MeshRenderer>())
            {
                if (renderer.gameObject.scene != shop.gameObject.scene || renderer.transform.IsChildOf(shop.transform)) continue;
                var lod = renderer.GetComponentInParent<LODGroup>();
                var target = lod != null ? lod.gameObject : renderer.gameObject;
                var name = target.name;
                if (!(name.StartsWith("Palm", StringComparison.Ordinal) || name.StartsWith("Geo_Props_Chair", StringComparison.Ordinal)
                    || name.StartsWith("Geo_Props_Umbrella", StringComparison.Ordinal))) continue;
                var bounds = LocalBounds(renderer.bounds, shop.transform);
                if (!building.Intersects(bounds) && !walkway.Intersects(bounds)) continue;
                if (!changed.Add(target)) continue;
                Undo.RecordObject(target, "Clear shop access with scene-only override");
                target.SetActive(false); PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                Debug.Log("[SHOP-INSTALL] HIDDEN scene decoration: " + Path(target.transform) + " bounds=" + bounds);
            }
            return changed.Count;
        }
        private static Bounds LocalBounds(Bounds world, Transform root)
        {
            var result = new Bounds(root.InverseTransformPoint(world.center), Vector3.zero);
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                result.Encapsulate(root.InverseTransformPoint(world.center + Vector3.Scale(world.extents, new Vector3(x, y, z))));
            return result;
        }
        private static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
        public static void ValidateRoute(FishingTackleShop shop)
        {
            var start = shop.transform.InverseTransformPoint(shop.LandingPosition);
            var counter = shop.transform.InverseTransformPoint(shop.purchasePoint.position);
            for (float z = start.z; z <= counter.z; z += .3f)
            {
                float y = z < -7 ? Mathf.Lerp(start.y, 0, Mathf.InverseLerp(start.z + 1.5f, -7, z)) : 0;
                var seed = shop.transform.TransformPoint(new Vector3(0, y, z));
                bool found = false; Vector3 floor = seed;
                foreach (var hit in Physics.RaycastAll(seed + Vector3.up * .45f, Vector3.down, 1, ~(1 << 4), QueryTriggerInteraction.Ignore))
                {
                    if (FishingShore.NonSolidSurface(hit.collider) || hit.normal.y < .69f || hit.point.y < .6f) continue;
                    if (!found || hit.point.y > floor.y) { found = true; floor = hit.point; }
                }
                if (!found) throw new Exception("Shop route lacks real support at local z=" + z);
                foreach (var col in Physics.OverlapCapsule(floor + Vector3.up * .36f, floor + Vector3.up * 1.46f, .28f, ~(1 << 4), QueryTriggerInteraction.Ignore))
                    if (!FishingShore.NonSolidSurface(col)) throw new Exception("Shop route blocked by " + Path(col.transform) + " at local z=" + z);
            }
            Debug.Log("[SHOP-INSTALL] CHECK: continuous supported deck/ramp/door/counter route with character-height clearance.");
        }
        [MenuItem("BoatAttack/Fishing/Focus beach tackle shop")]
        public static void FocusShop()
        {
            var shop = Object.FindObjectOfType<FishingTackleShop>();
            if (shop == null) { Debug.LogWarning("Open the fishing scene and install its shop first."); return; }
            Selection.activeGameObject = shop.gameObject;
            SceneView.lastActiveSceneView?.LookAt(shop.Position + Vector3.up * 2, shop.transform.rotation * Quaternion.Euler(25,145,0), 22);
        }
    }
}
