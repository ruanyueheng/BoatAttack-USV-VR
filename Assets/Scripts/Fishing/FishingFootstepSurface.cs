using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BoatAttack.Fishing
{
    public enum FishingGroundAudio { Sand, Grass, Wood, Stone, Unknown }

    /// <summary>Read-only, short-range support sampling. Movement and cadence remain the caller's responsibility.</summary>
    public static class FishingFootstepSurface
    {
        private const float ProbeHeight = .4f, ProbeLength = .85f;
        private static readonly RaycastHit[] Hits = new RaycastHit[64];
        private static readonly Dictionary<Mesh, MeshData> Meshes = new Dictionary<Mesh, MeshData>();
        private static readonly Dictionary<Collider, SurfaceData> Surfaces = new Dictionary<Collider, SurfaceData>();
        private static readonly Dictionary<TerrainData, TerrainSample> Terrains = new Dictionary<TerrainData, TerrainSample>();

        private sealed class MeshData
        {
            public int[] Triangles;
            public Color[] Colors;
            public Vector2[] UV;
            public int[] SubmeshEnds;
        }

        private sealed class SurfaceData
        {
            public FishingGroundAudio Named = FishingGroundAudio.Unknown;
            public FishingGroundAudio[] Materials;
            public bool[] CoastalAtlas;
        }

        private sealed class TerrainSample
        {
            public FishingGroundAudio[] Layers;
            public int X = -1, Z = -1;
            public float[,,] Weights;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCaches()
        {
            ClearCaches();
            SceneManager.sceneUnloaded -= SceneUnloaded;
            SceneManager.sceneUnloaded += SceneUnloaded;
        }

        private static void SceneUnloaded(Scene scene) => ClearCaches();
        private static void ClearCaches() { Meshes.Clear(); Surfaces.Clear(); Terrains.Clear(); }

        public static FishingGroundAudio Probe(FishingPrototype c, Vector3 feet)
        {
            if (c == null || !Finite(feet.x) || !Finite(feet.y) || !Finite(feet.z)) return FishingGroundAudio.Unknown;
            int count = Physics.RaycastNonAlloc(feet + Vector3.up * ProbeHeight, Vector3.down, Hits,
                ProbeLength, ~((1 << 4) | (1 << 5)), QueryTriggerInteraction.Ignore);
            // NonAlloc overflow has no guaranteed ordering; never select an arbitrary truncated hit.
            if (count == Hits.Length) return FishingGroundAudio.Unknown;
            int best = -1; float bestScore = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var hit = Hits[i]; var col = hit.collider;
                if (col == null || hit.normal.y < .69f || FishingShore.NonSolidSurface(col)) continue;
                if (c.Player != null && col.transform.IsChildOf(c.Player.transform)) continue;
                if (c.Shore != null && c.Shore.Body != null &&
                    (col == c.Shore.Body || col.transform.IsChildOf(c.Shore.Actor))) continue;
                float gap = feet.y - hit.point.y;
                // A roof/table above the feet and remote lower storeys are not the current support.
                if (gap < -.08f || gap > .45f) continue;
                float distance = Mathf.Abs(gap);
                float score = distance + (distance <= .25f ? 0 : 1);
                if (score < bestScore) { bestScore = score; best = i; }
            }
            return best < 0 ? FishingGroundAudio.Unknown : Classify(Hits[best]);
        }

        private static FishingGroundAudio Classify(RaycastHit hit)
        {
            var col = hit.collider;
            var terrain = col as TerrainCollider;
            if (terrain != null && terrain.terrainData != null) return TerrainSurface(terrain, hit.point);
            var surface = Surface(col);
            if (surface.Named != FishingGroundAudio.Unknown) return surface.Named;
            var collider = col as MeshCollider;
            var mesh = collider == null ? null : collider.sharedMesh;
            var data = mesh == null ? null : ReadMesh(mesh);
            int submesh = 0, index = hit.triangleIndex * 3;
            if (data != null && index >= 0)
                while (submesh + 1 < data.SubmeshEnds.Length && index >= data.SubmeshEnds[submesh]) submesh++;
            if (surface.Materials != null && submesh < surface.Materials.Length &&
                surface.Materials[submesh] != FishingGroundAudio.Unknown) return surface.Materials[submesh];
            if (data == null || data.Triangles == null || index < 0 || index + 2 >= data.Triangles.Length)
                return FishingGroundAudio.Unknown;
            int a = data.Triangles[index], b = data.Triangles[index + 1], d = data.Triangles[index + 2];
            var weights = hit.barycentricCoordinate;
            if (surface.CoastalAtlas != null && submesh < surface.CoastalAtlas.Length && surface.CoastalAtlas[submesh]
                && data.UV != null && ValidVertices(data.UV.Length, a, b, d))
            {
                var uv = data.UV[a] * weights.x + data.UV[b] * weights.y + data.UV[d] * weights.z;
                var atlas = Atlas(uv);
                if (atlas != FishingGroundAudio.Unknown) return atlas;
            }
            if (data.Colors == null || !ValidVertices(data.Colors.Length, a, b, d)) return FishingGroundAudio.Unknown;
            var color = data.Colors[a] * weights.x + data.Colors[b] * weights.y + data.Colors[d] * weights.z;
            if (color.g > color.r * 1.07f && color.g > color.b * 1.08f) return FishingGroundAudio.Grass;
            if (color.r >= color.g * .98f && color.g > color.b * 1.15f) return FishingGroundAudio.Sand;
            float high = Mathf.Max(color.r, Mathf.Max(color.g, color.b)), low = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
            // White vertex RGB in V035 is a foliage/wind mask, not a stone surface.
            return high < .85f && high - low < .12f ? FishingGroundAudio.Stone : FishingGroundAudio.Unknown;
        }

        private static SurfaceData Surface(Collider col)
        {
            if (Surfaces.TryGetValue(col, out var cached)) return cached;
            var result = new SurfaceData();
            if (col.sharedMaterial != null) result.Named = Facility(col.sharedMaterial.name);
            // Inspect only specific facility names, never generic ancestors such as Home or Realm.
            int depth = 0;
            for (var t = col.transform; result.Named == FishingGroundAudio.Unknown && t != null && depth < 5; t = t.parent, depth++)
            {
                result.Named = Facility(t.name);
                if (result.Named != FishingGroundAudio.Unknown) break;
            }
            var renderer = col.GetComponent<Renderer>();
            if (renderer != null)
            {
                var materials = renderer.sharedMaterials;
                result.Materials = new FishingGroundAudio[materials.Length];
                result.CoastalAtlas = new bool[materials.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    result.Materials[i] = FishingGroundAudio.Unknown;
                    if (material == null) continue;
                    string name = material.name;
                    // Mixed land palettes contain words like sand/stone; sample their actual triangle instead.
                    bool mixed = Has(name, "palette") || Has(name, "coast") || Has(name, "terrain") || Has(name, "shore") || Has(name, "ground");
                    if (!mixed) result.Materials[i] = Facility(name);
                    var texture = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap")
                        : material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
                    result.CoastalAtlas[i] = texture != null && Has(texture.name, "CoastalAlbedo");
                }
            }
            Surfaces.Add(col, result); return result;
        }

        private static FishingGroundAudio Facility(string name)
        {
            if (Has(name, "boardwalk") || Has(name, "jetty") || Has(name, "gangway") || Has(name, "deck") ||
                Has(name, "plank") || Has(name, "timber") || Has(name, "cedar") || Has(name, "oak") || Has(name, "wood") ||
                (Has(name, "floor") && (Has(name, "Home ") || Has(name, "Cottage") || Has(name, "Porch"))) ||
                Has(name, "Home foundation") || Has(name, "Porch foundation") ||
                Has(name, "Home boarding platform") || Has(name, "RootBridge") || Has(name, "WindBridge") ||
                Has(name, "木地板") || Has(name, "木楼层") || Has(name, "木桥") || Has(name, "栈桥") || Has(name, "木阶"))
                return FishingGroundAudio.Wood;
            if (Has(name, "stone") || Has(name, "rock") || Has(name, "limestone") || Has(name, "masonry") ||
                Has(name, "chalk") || Has(name, "cobble") || Has(name, "slate") || Has(name, "paving") || Has(name, "roof") ||
                Has(name, "brick") || Has(name, "concrete") || Has(name, "granite") || Has(name, "marble") || Has(name, "gravel") ||
                Has(name, "石阶") || Has(name, "石板") || Has(name, "石桥") || Has(name, "砖地") || Has(name, "瓦顶"))
                return FishingGroundAudio.Stone;
            return FishingGroundAudio.Unknown;
        }

        private static MeshData ReadMesh(Mesh mesh)
        {
            if (Meshes.TryGetValue(mesh, out var cached)) return cached;
            var data = new MeshData { SubmeshEnds = new int[mesh.subMeshCount] };
            if (mesh.isReadable)
            {
                data.Triangles = mesh.triangles; data.Colors = mesh.colors; data.UV = mesh.uv;
            }
            int end = 0;
            for (int i = 0; i < data.SubmeshEnds.Length; i++)
            { end += (int)mesh.GetIndexCount(i); data.SubmeshEnds[i] = end; }
            Meshes.Add(mesh, data); return data;
        }

        private static FishingGroundAudio Atlas(Vector2 uv)
        {
            if (!Finite(uv.x) || !Finite(uv.y)) return FishingGroundAudio.Unknown;
            // ArtSource/V035/atlas-regions.json: 4x4 atlas, numbered left-to-right from the top.
            int x = Mathf.Clamp(Mathf.FloorToInt(Mathf.Repeat(uv.x, 1) * 4), 0, 3);
            int y = Mathf.Clamp(Mathf.FloorToInt((1 - Mathf.Repeat(uv.y, 1)) * 4), 0, 3);
            switch (y * 4 + x)
            {
                case 3: case 13: return FishingGroundAudio.Wood;
                case 7: case 8: case 9: return FishingGroundAudio.Grass;
                case 12: return FishingGroundAudio.Sand;
                case 0: case 1: case 2: case 14: return FishingGroundAudio.Stone;
                default: return FishingGroundAudio.Unknown;
            }
        }

        private static FishingGroundAudio TerrainSurface(TerrainCollider collider, Vector3 world)
        {
            var data = collider.terrainData;
            int width = data.alphamapWidth, height = data.alphamapHeight;
            if (width < 1 || height < 1 || data.alphamapLayers < 1) return FishingGroundAudio.Unknown;
            if (!Terrains.TryGetValue(data, out var sample) || sample.Layers.Length != data.alphamapLayers)
            {
                var layers = data.terrainLayers;
                sample = new TerrainSample { Layers = new FishingGroundAudio[data.alphamapLayers] };
                for (int i = 0; i < sample.Layers.Length; i++)
                    sample.Layers[i] = i < layers.Length && layers[i] != null ? TerrainLayer(layers[i].name) : FishingGroundAudio.Unknown;
                Terrains[data] = sample;
            }
            var local = collider.transform.InverseTransformPoint(world);
            int x = Mathf.Clamp(Mathf.FloorToInt(local.x / Mathf.Max(.001f, data.size.x) * width), 0, width - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(local.z / Mathf.Max(.001f, data.size.z) * height), 0, height - 1);
            const int tile = 8;
            int originX = x / tile * tile, originZ = z / tile * tile;
            if (sample.Weights == null || sample.X != originX || sample.Z != originZ)
            {
                sample.X = originX; sample.Z = originZ;
                sample.Weights = data.GetAlphamaps(originX, originZ, Mathf.Min(tile, width - originX), Mathf.Min(tile, height - originZ));
            }
            float strongest = -1; int layer = -1;
            for (int i = 0; i < sample.Layers.Length; i++)
            {
                float weight = sample.Weights[z - sample.Z, x - sample.X, i];
                if (weight > strongest) { strongest = weight; layer = i; }
            }
            return layer < 0 ? FishingGroundAudio.Unknown : sample.Layers[layer];
        }

        private static FishingGroundAudio TerrainLayer(string name)
        {
            // Existing terrain names include BeachSand[_wet], SandRocks, Bush and RockPlants.
            if (Has(name, "sand") || Has(name, "beach") || Has(name, "沙") || Has(name, "砂")) return FishingGroundAudio.Sand;
            if (Has(name, "rock") || Has(name, "stone") || Has(name, "cliff") || Has(name, "石")) return FishingGroundAudio.Stone;
            if (Has(name, "grass") || Has(name, "bush") || Has(name, "plant") || Has(name, "moss") || Has(name, "草")) return FishingGroundAudio.Grass;
            return FishingGroundAudio.Unknown;
        }

        private static bool ValidVertices(int length, int a, int b, int c) => a >= 0 && b >= 0 && c >= 0 && a < length && b < length && c < length;
        private static bool Has(string text, string value) => text != null && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
