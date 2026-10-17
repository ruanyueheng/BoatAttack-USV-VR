using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace BoatAttack.Fishing
{
    /// <summary>Original, bounded-cost harbor dressing. No extra lights, particles or reflection cameras.</summary>
    public sealed class FishingScenery : MonoBehaviour
    {
        private FishingPrototype _controller;
        private readonly List<Transform> _zones = new List<Transform>();
        private float _nextCull;
        public int CombinedRenderers { get; private set; }
        public int CombinedVertices { get; private set; }

        public void Build(FishingPrototype c, List<Object> owned)
        {
            _controller = c;
            Material stone = new Material(c.PaletteShader) { name="Stratified reef palette", enableInstancing=true }; owned.Add(stone);
            Material coral = Palette("Terracotta coral", new Color(0.83f, 0.31f, 0.22f), owned);
            Material kelp = Palette("Shallow eelgrass", new Color(.19f,.38f,.27f), owned);
            Mesh rock = RockMesh(); owned.Add(rock);
            Transform port = Root("V03 harbor lighthouse", c.harbor + new Vector3(5.4f, 0, 2.5f));
            Rock(port, rock, stone, Vector3.zero, new Vector3(2.6f, 1.1f, 2.4f));
            Cylinder(port, "Beacon foundation", new Vector3(0, 0.9f, 0), new Vector3(2.4f, 0.25f, 2.4f), c.cream);
            for (int i = 0; i < 5; i++)
                Cylinder(port, "Painted lighthouse course", new Vector3(0, 1.7f + i * 0.95f, 0), new Vector3(1.7f - i * 0.12f, 0.48f, 1.7f - i * 0.12f), i % 2 == 0 ? c.cream : c.teal);
            Cylinder(port, "Lantern balcony", new Vector3(0, 6.15f, 0), new Vector3(2.2f, 0.12f, 2.2f), c.dark);
            Cylinder(port, "Warm lantern core", new Vector3(0, 6.65f, 0), new Vector3(0.62f, 0.45f, 0.62f), c.orange);
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = Quaternion.Euler(0, i * 45, 0) * new Vector3(0.8f, 0, 0);
                FishingArt.Pole("Lantern frame", port, p + Vector3.up * 6.2f, p + Vector3.up * 7.1f, 0.045f, c.dark);
                FishingArt.Pole("Balcony railing", port, p + Vector3.up * 6.2f, p + Vector3.up * 6.65f, 0.026f, c.cream);
            }
            Cylinder(port, "Lantern cap", new Vector3(0, 7.15f, 0), new Vector3(1.95f, 0.12f, 1.95f), c.teal);
            FishingArt.Pole("Weather mast", port, Vector3.up * 7.2f, Vector3.up * 8.1f, 0.04f, c.dark);
            FishingArt.Pole("Weather vane", port, new Vector3(-0.7f, 7.9f, 0), new Vector3(0.7f, 7.9f, 0), 0.035f, c.orange);
            Combine(port, owned);

            var market = c.transform.Find("Tide and Fin floating fish market");
            if (market != null)
            {
                for (int i = 0; i < 5; i++)
                {
                    float x = -2.6f + i * 1.3f;
                    FishingArt.Pole("Festoon drop", market, new Vector3(x, 3.1f, -2.3f), new Vector3(x, 2.95f - 0.10f * Mathf.Sin(i), -2.3f), 0.016f, c.dark);
                    Cylinder(market, "Unlit nautical lantern", new Vector3(x, 2.8f - 0.1f * Mathf.Sin(i), -2.3f), new Vector3(0.18f, 0.12f, 0.18f), c.orange);
                }
                for (int i = 0; i < 3; i++)
                {
                    Cylinder(market, "Supply barrel", new Vector3(-3.1f, 1.02f, -1.5f + i * 0.65f), new Vector3(0.5f, 0.34f, 0.5f), c.wood);
                    Cylinder(market, "Barrel hoop", new Vector3(-3.1f, 1.18f, -1.5f + i * 0.65f), new Vector3(0.53f, 0.025f, 0.53f), c.dark);
                }
                Combine(market, owned);
            }
            for (int zone = 0; zone < 3; zone++)
            {
                // Emergent reefs are physical hazards; the station's 9 m approach remains clear.
                Transform root = Root("V03 habitat " + zone, c.fishingSpots[zone]); _zones.Add(root);
                for (int i = 0; i < 3; i++)
                {
                    Vector3 p = Quaternion.Euler(0, 35 + i * 120, 0) * new Vector3(13.5f, 0, 0);
                    Rock(root, rock, stone, p + Vector3.down * 2.3f, new Vector3(2.7f, 1.2f + i * .12f, 2));
                    for (int j = 0; j < 4; j++)
                    {
                        Vector3 stem = p + new Vector3((j - 1.5f) * .35f,-.9f,.8f);
                        Vector3 tip = stem + new Vector3(.18f * (j % 2 == 0 ? 1 : -1), .4f + j * .1f, .14f);
                        FishingArt.Pole(zone == 0 ? "Submerged coral" : "Eelgrass blade", root, stem, tip,zone==0?.025f:.014f,zone==0?coral:kelp);
                        if (zone == 0) FishingArt.Pole("Coral branch", root, Vector3.Lerp(stem, tip, .6f), tip + new Vector3(.18f,-.14f,0),.018f,coral);
                    }
                }
                Combine(root, owned);
            }
            Debug.Log("[FISHING] V03 scenery combined: " + CombinedRenderers + " renderers, " + CombinedVertices + " vertices (market included).");
        }
        private Transform Root(string name, Vector3 position)
        {
            var t = new GameObject(name).transform; t.SetParent(_controller.transform, false); t.position = position; return t;
        }
        private void Update()
        {
            if (_controller == null || _controller.Player == null || _controller.Paused) return;
            if (Time.time > _nextCull)
            {
                _nextCull = Time.time + 0.4f;
                foreach (var zone in _zones) zone.gameObject.SetActive((_controller.ExplorerPosition - zone.position).sqrMagnitude < 240f * 240f);
            }
        }

        private static Material Palette(string name, Color color, List<Object> owned)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.name = name; mat.SetColor("_BaseColor", color); mat.SetFloat("_Smoothness", 0.18f); mat.enableInstancing = true; owned.Add(mat); return mat;
        }
        private static void Cylinder(Transform root, string name, Vector3 position, Vector3 scale, Material material)
            => FishingArt.Shape(name, PrimitiveType.Cylinder, root, position, scale, material);
        private static void Rock(Transform root, Mesh mesh, Material material, Vector3 position, Vector3 scale,bool solid=false)
        {
            var go = new GameObject("Faceted limestone", typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(root, false); go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if(solid){var collider=go.AddComponent<MeshCollider>();collider.sharedMesh=mesh;collider.convex=true;}
        }
        private static Mesh RockMesh() => FishingMeshBuilder.ReefRock();
        private void Combine(Transform root, List<Object> owned)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled || renderer.sharedMaterial == null || filter.sharedMesh == null || filter.GetComponent<TextMesh>() != null) continue;
                if (!groups.TryGetValue(renderer.sharedMaterial, out var list)) groups.Add(renderer.sharedMaterial, list = new List<CombineInstance>());
                list.Add(new CombineInstance { mesh = filter.sharedMesh, transform = root.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                renderer.enabled = false;
            }
            foreach (var group in groups)
            {
                var mesh = new Mesh { name = root.name + " batch", indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(group.Value.ToArray(), true, true); owned.Add(mesh);
                var go = new GameObject("Batched " + group.Key.name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(root, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = group.Key; renderer.shadowCastingMode = ShadowCastingMode.On;renderer.receiveShadows=true;
                CombinedRenderers++; CombinedVertices += mesh.vertexCount;
            }
        }
    }
}
