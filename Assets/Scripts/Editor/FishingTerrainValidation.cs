using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BoatAttack.EditorTools
{
    /// <summary>Checks the binary terrain data that Windows text conversion previously damaged.</summary>
    public static class FishingTerrainValidation
    {
        public static void Run()
        {
            try
            {
                EditorSceneManager.OpenScene(FishingPrototypeEditor.ScenePath);
                Physics.SyncTransforms();
                var terrains = UnityEngine.Object.FindObjectsOfType<Terrain>();
                if (terrains.Length < 2) throw new Exception("Expected both island terrain tiles.");
                foreach (var terrain in terrains)
                {
                    var data = terrain.terrainData;
                    if (data == null || data.heightmapResolution < 33 || !terrain.drawHeightmap)
                        throw new Exception("Missing/disabled terrain height data: " + terrain.name);
                    var collider = terrain.GetComponent<TerrainCollider>();
                    if (collider == null || !collider.enabled || collider.terrainData != data)
                        throw new Exception("Missing terrain collision: " + terrain.name);
                    int aboveWater = 0;
                    for (int x = 1; x < 20; x++) for (int z = 1; z < 20; z++)
                    {
                        var p = terrain.transform.position + new Vector3(data.size.x * x / 20f, 0, data.size.z * z / 20f);
                        p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
                        if (p.y <= .25f) continue;
                        if (!collider.Raycast(new Ray(p + Vector3.up * 3f, Vector3.down), out var hit, 6f))
                            throw new Exception("Visible land has no solid ground: " + terrain.name);
                        if (Mathf.Abs(hit.point.y - p.y) > .15f) throw new Exception("Heightmap/collider mismatch.");
                        aboveWater++;
                    }
                    if (aboveWater == 0) throw new Exception("Terrain has no above-water land: " + terrain.name);
                    Debug.Log("[FISHING-TERRAIN] " + terrain.name + ": " + aboveWater + " dry ground samples, heightmap " + data.heightmapResolution);
                }
                Debug.Log("[FISHING-TERRAIN] PASS: both terrain tiles deserialize with enabled heightmaps and matching above-water collision.");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception ex) { Debug.LogException(ex); if (Application.isBatchMode) EditorApplication.Exit(1); }
        }
    }
}
