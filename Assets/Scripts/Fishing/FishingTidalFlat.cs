using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    /// <summary>
    /// Low-tide reef path north of the home lighthouse. The whole flat (look and collision) rises and sinks with
    /// <see cref="FishingCalendar.TideLevel"/>; a player still on it when it floods is walked back to the beach.
    /// </summary>
    public sealed class FishingTidalFlat : MonoBehaviour
    {
        /// <summary>Walkable top above the sea at low water, and how far the flat sinks at high water.</summary>
        public const float Top = .65f, Sink = 1.6f, Length = 30;
        private FishingPrototype _c;
        private readonly List<Object> _owned = new List<Object>();
        private readonly List<Collider> _colliders = new List<Collider>();
        private readonly Transform[] _items = new Transform[FishingTide.ItemCount];
        private readonly Vector3[] _crabHomes = new Vector3[FishingTide.CrabCount];
        private readonly float[] _crabAngles = new float[FishingTide.CrabCount];
        private Rigidbody _body;
        private bool _solid, _warned, _wasExposed;
        public Transform Root { get; private set; }
        public Vector3 ShoreStart { get; private set; }
        public Vector3 Islet => Root.TransformPoint(new Vector3(.5f, 0, 35));
        public float Offset { get; private set; }
        public int Triangles { get; private set; }
        public Texture2D SandTexture { get; private set; }
        public bool Solid => _solid;
        public int Rescues { get; private set; }
        public int Warnings { get; private set; }
        public Transform Item(int i) => _items[i];
        public Vector3 PathPoint(float z) => Root.TransformPoint(new Vector3(Wiggle(z), 0, z));

        private static float Wiggle(float z) => .8f * Mathf.Sin(z * .21f);
        private static float Noise(float x, float z) => Mathf.Sin(x * 2.3f + z * 1.7f) * Mathf.Sin(z * 3.1f - x * .9f);
        private static readonly Vector3[] Pools = { new Vector3(-1.6f, 1.05f, 34.2f), new Vector3(2.1f, .8f, 36.6f) };
        /// <summary>Local height of the flat (0 = walkable top), path and islet merged, tide pools cut in.</summary>
        public static float Height(float x, float z)
        {
            float d = Mathf.Abs(x - Wiggle(z)), path = z < -3 || z > Length + 2 ? -9 : d < 1 ? 0 : -2.9f * Mathf.Pow(Mathf.Clamp01((d - 1) / 1.7f), 1.3f);
            float r = Vector2.Distance(new Vector2(x, z), new Vector2(.5f, 35)) / (1 + .08f * Mathf.Sin(Mathf.Atan2(z - 35, x - .5f) * 3));
            float islet = r < 4.6f ? .05f : -2.9f * Mathf.Pow(Mathf.Clamp01((r - 4.6f) / 2.6f), 1.2f);
            float h = Mathf.Max(path, islet);
            if (h > -.2f) h += .02f * Noise(x, z);
            foreach (var p in Pools)
            {
                float k = Vector2.Distance(new Vector2(x, z), new Vector2(p.x, p.z)) / p.y;
                if (k < 1) h -= .28f * (1 - k * k);
            }
            return h;
        }

        public void Build(FishingPrototype c)
        {
            _c = c;
            var lens = c.Home.LighthouseLens;
            // Walk north from the tower to the waterline; the path starts where the beach drops below 0.8 m.
            var start = new Vector3(lens.x, 0, lens.z + 8);
            for (int i = 0; i < 40 && GroundY(start) > .8f; i++) start.z += .5f;
            Root = new GameObject("V044 low-tide reef path").transform; Root.SetParent(transform, false);
            Root.position = new Vector3(start.x, Top, start.z);
            ShoreStart = new Vector3(start.x, 0, start.z - 2.5f); ShoreStart = new Vector3(ShoreStart.x, GroundY(ShoreStart), ShoreStart.z);
            _body = Root.gameObject.AddComponent<Rigidbody>(); _body.isKinematic = true; _body.interpolation = RigidbodyInterpolation.None;

            var palette = new Material(c.PaletteShader) { name = "Tidal flat vertex palette" }; _owned.Add(palette);
            var sandMaterial = new Material(c.PaletteShader) { name = "Tidal flat wet sand" }; _owned.Add(sandMaterial);
            SandTexture = WetSandTexture(); _owned.Add(SandTexture);
            sandMaterial.SetTexture("_BaseMap", SandTexture); sandMaterial.SetFloat("_Textured", 1); sandMaterial.SetFloat("_Gloss", .35f);
            // _Textured ignores vertex colour; pools are separate water discs.
            var flat = FlatMesh(); var ground = Part("Wet sand and reef top", flat, sandMaterial, Vector3.zero);
            _colliders.Add(ground.gameObject.AddComponent<MeshCollider>());
            var water = new Material(c.PaletteShader) { name = "Tide pool still water" }; water.SetFloat("_Gloss", .9f); water.SetFloat("_Rim", .3f); _owned.Add(water);
            Part("Tide pool water", PoolMesh(), water, Vector3.zero);
            var boulders = new[] { Boulder(1), Boulder(2), Boulder(3) }; var rock = boulders[0];
            var random = new System.Random(4410);
            for (int i = 0; i < 18; i++)
            {
                float z = 2 + i * 1.7f, side = i % 2 == 0 ? 1 : -1, s = .45f + (float)random.NextDouble() * .85f;
                var p = new Vector3(Wiggle(z) + side * (1.5f + s * .55f), -.25f - s * .25f, z + (float)random.NextDouble());
                var t = Part("Reef rock " + i, boulders[i % 3], palette, p); t.localScale = new Vector3(s * 1.15f, s * .75f, s); t.localRotation = Quaternion.Euler(0, i * 47, 0);
                // Edge rocks keep walkers off the sloping sides; convex hulls never snag a straight walk.
                var mc = t.gameObject.AddComponent<MeshCollider>(); mc.convex = true; _colliders.Add(mc);
            }
            var wreckSource = Resources.Load<GameObject>("FishingV044/TideWreck");
            if (wreckSource != null)
            {
                // Hunyuan skiff by scene-art: origin at the keel, bow +Z; sunk 0.4 m into the sand.
                var wreck = new GameObject("Half-buried skiff wreck").transform; wreck.SetParent(Root, false);
                wreck.localPosition = new Vector3(.4f, -.65f, 39.2f); wreck.localRotation = Quaternion.Euler(0, 18, -6);
                Instantiate(wreckSource, wreck, false); FishingV035Materials.Apply(wreck, false);
                var box = wreck.gameObject.AddComponent<BoxCollider>(); box.center = new Vector3(0, .7f, 0); box.size = new Vector3(1.9f, 1.4f, 4.4f); _colliders.Add(box);
            }
            else Part("Shallow skiff wreck", WreckMesh(), palette, new Vector3(.4f, -.3f, 39.2f)).localRotation = Quaternion.Euler(0, 18, -9);
            var tally = Part("Tally-marked reef rock", rock, palette, new Vector3(-2.8f, -.25f, 31.5f)); tally.localScale = new Vector3(1.4f, 1.1f, 1.3f);
            var tallyCollider = tally.gameObject.AddComponent<MeshCollider>(); tallyCollider.convex = true; _colliders.Add(tallyCollider);
            for (int i = 0; i < FishingTide.ItemCount; i++) _items[i] = Part(FishingTide.Names[i], ItemMesh(i), palette, ItemSpot(i));
            for (int i = 0; i < FishingTide.CrabCount; i++)
            {
                var item = _items[FishingTide.ShellCount + i]; _crabHomes[i] = item.localPosition;
                var crab = FishingArt.Fish(item, _owned, c.PaletteShader, FishKind.Crab);
                var bounds = crab.GetComponent<MeshFilter>().sharedMesh.bounds; float scale = (.13f + i * .03f) / Mathf.Max(bounds.size.x, bounds.size.z);
                crab.transform.localScale = Vector3.one * scale; crab.transform.localPosition = Vector3.up * (-bounds.min.y * scale);
                item.GetComponent<MeshRenderer>().enabled = false;
            }
            Triangles = 0; foreach (var f in Root.GetComponentsInChildren<MeshFilter>()) Triangles += (int)f.sharedMesh.GetIndexCount(0) / 3;
            Refresh(true);
        }
        private float GroundY(Vector3 p)
        {
            float top = -5;
            foreach (var h in Physics.RaycastAll(new Vector3(p.x, 60, p.z), Vector3.down, 120, ~((1 << 4) | (1 << 5)), QueryTriggerInteraction.Ignore))
                if (!FishingShore.NonSolidSurface(h.collider) && (Root == null || !h.transform.IsChildOf(Root))) top = Mathf.Max(top, h.point.y);
            return top;
        }
        private Transform Part(string name, Mesh mesh, Material m, Vector3 local)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(Root, false); go.transform.localPosition = local;
            go.GetComponent<MeshFilter>().sharedMesh = mesh; var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off;
            if (!_owned.Contains(mesh)) _owned.Add(mesh); return go.transform;
        }

        private static Vector3 ItemSpot(int i)
        {
            switch (i)
            {
                case 0: return new Vector3(Wiggle(6) + .5f, .03f, 6);
                case 1: return new Vector3(Wiggle(12) - .55f, .03f, 12);
                case 2: return new Vector3(Wiggle(18) + .6f, .03f, 18);
                case 3: return new Vector3(Wiggle(24) - .4f, .03f, 24);
                case 4: return new Vector3(2.8f, .08f, 33.2f);
                case 5: return new Vector3(-.6f, .08f, 37.8f);
                case 6: case 7: case 8:
                    var pool = Pools[i == 8 ? 1 : 0]; float a = i * 2.1f;
                    return new Vector3(pool.x + Mathf.Cos(a) * (pool.y + .2f), .06f, pool.z + Mathf.Sin(a) * (pool.y + .2f));
                case 9: return new Vector3(-1.25f, .05f, 37.2f); // beside the wreck's bow, outside its collider
                case 10: return new Vector3(-2.15f, .45f, 31.9f);
                default: return new Vector3(3.6f, .05f, 35.8f);
            }
        }

        /// <summary>1024² seamless wet-sand tile: ripple marks, damp patches, shell grit and weed flecks.</summary>
        private static Texture2D WetSandTexture()
        {
            const int n = 1024; var px = new Color32[n * n]; var random = new System.Random(4411);
            var sand = new Color(.78f, .69f, .52f); var damp = new Color(.52f, .45f, .35f);
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float u = x / (float)n, v = y / (float)n, tu = u * Mathf.PI * 2;
                // One seamless period in both directions; the whole tile is sand (pools are separate water discs).
                float sv = v * Mathf.PI * 2;
                float warp = .9f * Mathf.Sin(tu * 2 + Mathf.Sin(sv * 3)) + .5f * Mathf.Sin(sv * 2 - tu);
                float ripple = Mathf.Sin(sv * 14 + tu * 2 + warp * 2.2f);
                float wet = Mathf.Clamp01(.5f + .35f * Mathf.Sin(tu * 3 + sv) * Mathf.Sin(sv * 2 + tu * 2) + .25f * Mathf.Sin(tu * 7 - sv * 5));
                Color c = Color.Lerp(sand, damp, Mathf.SmoothStep(.35f, .75f, wet));
                c *= .88f + .1f * ripple + (ripple > .85f ? .07f : 0);
                c *= .94f + .06f * (float)random.NextDouble();
                c.a = 1; px[y * n + x] = c;
            }
            for (int i = 0; i < 900; i++)
            {
                int cx = random.Next(n), cy = 4 + random.Next(n - 8), r = 1 + random.Next(3);
                var c = i % 5 == 0 ? new Color(.22f, .32f, .2f) : new Color(.95f, .92f, .85f);
                for (int dy = -r; dy <= r; dy++) for (int dx = -r * 2; dx <= r * 2; dx++)
                        if (dx * dx / 4 + dy * dy <= r * r) px[(cy + dy) * n + (cx + dx + n) % n] = c;
            }
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "V044 wet sand tile", wrapMode = TextureWrapMode.Repeat, anisoLevel = 2 };
            t.SetPixels32(px); t.Apply(true, false); return t; // stays readable so the suite can export it for review
        }
        private static Mesh FlatMesh()
        {
            // Top surface tiles the sand band every 4 m; flanks and pools sample fixed tints in the lower band.
            const float cell = .4f; const int nx = 41, nz = 114;
            var v = new Vector3[nx * nz]; var uv = new Vector2[nx * nz];
            for (int j = 0; j < nz; j++) for (int i = 0; i < nx; i++)
            {
                float x = -8 + i * cell, z = -3 + j * cell, h = Mathf.Max(Height(x, z), -3.2f);
                v[j * nx + i] = new Vector3(x, h, z);
                // Continuous UVs everywhere; band switches made zig-zag seams, so pools get their own water discs instead.
                uv[j * nx + i] = new Vector2(x / 4, z / 4);
            }
            var tris = new List<int>();
            for (int j = 0; j < nz - 1; j++) for (int i = 0; i < nx - 1; i++)
            {
                int a = j * nx + i, b = a + nx, c = b + 1, d = a + 1;
                if (Mathf.Max(Mathf.Max(v[a].y, v[b].y), Mathf.Max(v[c].y, v[d].y)) < -2.8f) continue;
                tris.AddRange(new[] { a, b, c, a, c, d });
            }
            var mesh = new Mesh { name = "Tidal flat heightfield", vertices = v, uv = uv };
            mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        /// <summary>Round, squat reef boulder: dark wet skirt, grey-brown body, barnacle clusters and a weed cap.</summary>
        private static Mesh Boulder(int seed)
        {
            var b = new FishingMeshBuilder(); var random = new System.Random(seed * 131);
            b.Lumpy(Vector3.zero, new Vector3(1, .75f, .9f), seed, y => y < -.2f ? new Color(.15f, .19f, .18f) : y < .15f ? new Color(.33f, .32f, .29f) : y < .58f ? new Color(.47f, .45f, .4f) : new Color(.24f, .37f, .19f));
            for (int i = 0; i < 12; i++)
            {
                float a = (float)random.NextDouble() * Mathf.PI * 2, y = -.15f + (float)random.NextDouble() * .5f;
                float r = Mathf.Sqrt(Mathf.Max(0, 1 - y * y / (.75f * .75f)));
                var p = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r * .9f) * .97f; var n = new Vector3(p.x, p.y * 1.3f, p.z).normalized;
                b.Tube(p, p + n * .035f, .03f, .008f, new Color(.86f, .84f, .78f), 5);
            }
            return b.Finish("Barnacled reef boulder " + seed);
        }
        private static Mesh PoolMesh()
        {
            var b = new FishingMeshBuilder(); const int n = 28;
            foreach (var p in Pools)
                for (int k = 0; k < n; k++)
                {
                    float a0 = k * Mathf.PI * 2 / n, a1 = (k + 1) * Mathf.PI * 2 / n, r = p.y * .78f;
                    var c = new Vector3(p.x, -.09f, p.z);
                    b.Quad(c, c + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * r * (1 + .05f * Mathf.Sin(k + 1)), c + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * r * (1 + .05f * Mathf.Sin(k)), c, new Color(.16f, .38f, .4f));
                }
            return b.Finish("Tide pool water");
        }
        private static Mesh WreckMesh()
        {
            var b = new FishingMeshBuilder(); var wood = new Color(.36f, .27f, .19f); var pale = new Color(.52f, .44f, .33f);
            b.Tube(new Vector3(0, .1f, -2.4f), new Vector3(0, .1f, 2.2f), .1f, .08f, wood, 6);
            for (int i = 0; i < 7; i++)
            {
                float z = -2 + i * .65f, w = .95f * Mathf.Sin((i + .8f) / 7.6f * Mathf.PI);
                // Ribs on the seaward side are broken short, like a hull the tide has worked at for decades.
                b.Arc(new Vector3(0, .95f, z), w, .045f, 200, i % 3 == 1 ? 270 : 340, i % 2 == 0 ? wood : pale, 6);
            }
            // Strakes along both sides make the ribs read as a hull rather than loose sticks.
            foreach (float s in new[] { -1f, 1f }) foreach (float y in new[] { .22f, .45f })
                    b.Tube(new Vector3(s * .82f, y, -1.9f), new Vector3(s * .78f, y + .04f, s > 0 ? .3f : 1.7f), .05f, .05f, pale, 5);
            return b.Finish("Half-buried skiff ribs");
        }
        private static Mesh ItemMesh(int i)
        {
            var b = new FishingMeshBuilder();
            switch (i)
            {
                case 0:
                    for (int k = 0; k < 7; k++) b.Fin(Vector3.zero, Quaternion.Euler(0, -60 + k * 20, 0) * new Vector3(0, .03f, .1f), Quaternion.Euler(0, -50 + k * 20, 0) * new Vector3(0, .03f, .1f), k % 2 == 0 ? new Color(.95f, .62f, .48f) : new Color(.98f, .8f, .66f));
                    b.Ellipsoid(new Vector3(0, .01f, .04f), new Vector3(.08f, .025f, .07f), new Color(.95f, .7f, .55f), 10, 5); break;
                case 1: b.Ellipsoid(Vector3.zero + Vector3.up * .025f, new Vector3(.045f, .03f, .07f), new Color(.9f, .74f, .38f), 10, 6); b.Ellipsoid(new Vector3(0, .05f, 0), new Vector3(.03f, .008f, .05f), new Color(.98f, .92f, .7f), 8, 4); break;
                case 2: b.Tube(Vector3.up * .01f, new Vector3(0, .02f, .12f), .035f, .004f, new Color(.78f, .7f, .6f), 8); break;
                case 3: b.Ellipsoid(Vector3.up * .01f, new Vector3(.07f, .018f, .06f), new Color(.93f, .93f, .9f), 10, 5); break;
                case 4: for (int k = 0; k < 4; k++) b.Tube(Vector3.up * .02f, Quaternion.Euler(0, k * 72, 0) * new Vector3(0, .015f, .12f), .03f, .008f, new Color(.86f, .3f, .22f), 6); break;
                case 5: b.Ellipsoid(Vector3.up * .035f, new Vector3(.06f, .04f, .06f), new Color(.62f, .5f, .72f), 10, 6); break;
                case 6: case 7: case 8:
                    var shell = i == 6 ? new Color(.8f, .66f, .46f) : i == 7 ? new Color(.85f, .45f, .3f) : new Color(.3f, .52f, .58f);
                    if (i == 7) b.Tube(new Vector3(0, .02f, -.05f), new Vector3(0, .08f, -.12f), .06f, .005f, new Color(.82f, .72f, .58f), 8);
                    b.Ellipsoid(new Vector3(0, .04f, 0), new Vector3(.065f, .028f, .05f), shell, 10, 5);
                    for (int k = 0; k < 3; k++) foreach (float s in new[] { -1f, 1f })
                            b.Tube(new Vector3(s * .05f, .04f, -.025f + k * .025f), new Vector3(s * .11f, 0, -.04f + k * .035f), .008f, .006f, shell * .85f, 4);
                    foreach (float s in new[] { -1f, 1f })
                        b.Ellipsoid(new Vector3(s * .06f, .05f, .07f), (i == 8 && s > 0 ? 1.8f : 1) * new Vector3(.025f, .018f, .03f), i == 8 && s > 0 ? new Color(.95f, .9f, .78f) : shell, 8, 4);
                    break;
                case 9: b.Tube(Vector3.zero, new Vector3(0, .012f, 0), .17f, .17f, new Color(.93f, .88f, .74f), 4); b.Tube(new Vector3(0, .012f, 0), new Vector3(0, .014f, 0), .12f, .12f, new Color(.32f, .36f, .48f), 4); break;
                case 10:
                    for (int k = 0; k < 5; k++) b.Tube(new Vector3(-.2f + k * .1f, 0, 0), new Vector3(-.2f + k * .1f, .3f, 0), .012f, .012f, new Color(.9f, .88f, .8f), 4);
                    b.Tube(new Vector3(-.26f, .05f, 0), new Vector3(.26f, .25f, 0), .012f, .012f, new Color(.9f, .88f, .8f), 4); break;
                default:
                    b.Tube(Vector3.zero, new Vector3(0, .24f, 0), .075f, .05f, new Color(.55f, .78f, .8f), 10);
                    b.Tube(new Vector3(0, .02f, 0), new Vector3(0, .05f, 0), .08f, .08f, new Color(.2f, .2f, .2f), 10); break;
            }
            return b.Finish("Tide find " + FishingTide.Names[i]);
        }

        private void Update()
        {
            if (_c == null || _c.Career == null) return;
            Refresh(false);
            if (!_solid || _c.Paused || _c.StoryBlocksGameplay) return;
            float t = Time.time;
            for (int i = 0; i < FishingTide.CrabCount; i++)
            {
                var crab = _items[FishingTide.ShellCount + i]; if (!crab.gameObject.activeSelf) continue;
                var pool = Pools[i == 2 ? 1 : 0];
                // Crabs bolt round the pool from a standing walker; crouch and they settle.
                bool spooked = _c.Ashore && !_c.Shore.Crouched && Vector3.Distance(crab.position, _c.Shore.Position) < 2.5f;
                _crabAngles[i] += Time.deltaTime * (spooked ? 1.3f : .18f) * (i == 1 ? -1 : 1);
                float a = i * 2.1f + _crabAngles[i];
                var p = new Vector3(pool.x + Mathf.Cos(a) * (pool.y + .2f), .06f + Mathf.Abs(Mathf.Sin(t * 14 + i)) * .01f, pool.z + Mathf.Sin(a) * (pool.y + .2f));
                crab.localPosition = p; crab.localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0);
            }
            TickVrGrab();
        }

        /// <summary>Moves the flat to the current tide and handles flood warnings, rescue and boat clearance.</summary>
        public void Refresh(bool force)
        {
            var cal = _c.Career.Calendar; var lore = _c.Career.Lore;
            // Linear sink puts the top at sea level exactly when the path floods (level 0.30).
            Offset = -Sink * Mathf.Clamp01((cal.TideLevel - .18f) / .295f);
            bool exposed = FishingTide.Exposed(cal);
            if (exposed && OnFlat && FishingTide.MinutesToFlood(cal) <= FishingTide.WarnMinutes && !_warned)
            {
                _warned = true; Warnings++;
                _c.Notice("潮水在涨：礁石小路约 " + Mathf.CeilToInt((float)(FishingTide.MinutesToFlood(cal) / FishingCalendar.MinutesPerSecond)) + " 秒后会被淹没，往岸边走吧。", 8);
                _c.WorldAudio?.Play("WaterFinish", _c.Shore.Position, .8f, FishingAudioCategory.Ambient);
                Haptic(.5f, .3f);
            }
            if (!exposed && (_wasExposed || force) && OnFlat) Rescue();
            if (!exposed) _warned = false;
            _wasExposed = exposed;
            // A hull parked over the reef keeps the rocks soft until it leaves, so the flat never lifts a boat.
            bool boatOver = !_c.Ashore && _c.Player != null && OverFlat(_c.Player.transform.position);
            bool solid = exposed && !boatOver;
            if (solid != _solid || force) { _solid = solid; foreach (var col in _colliders) col.enabled = solid; }
            var target = new Vector3(Root.position.x, Top + Offset, Root.position.z);
            if (force) Root.position = target; else _body.MovePosition(target);
            for (int i = 0; i < FishingTide.ItemCount; i++) _items[i].gameObject.SetActive(!lore.TideFlag(i));
        }
        private bool OverFlat(Vector3 world)
        {
            var p = Root.InverseTransformPoint(world);
            return p.x > -8 && p.x < 8 && p.z > -1 && p.z < 42 && Height(p.x, p.z) > -2.6f;
        }
        /// <summary>The walker stands on the reef past the beach.</summary>
        public bool OnFlat
        {
            get
            {
                if (!_c.Ashore) return false;
                var p = Root.InverseTransformPoint(_c.Shore.Position);
                return p.z > .5f && p.z < 42 && Height(p.x, p.z) > -1.2f && p.y > -1.2f && p.y < 2.5f;
            }
        }
        private void Rescue()
        {
            Rescues++;
            var back = Quaternion.LookRotation(Vector3.ProjectOnPlane(ShoreStart - PathPoint(10), Vector3.up));
            if (!_c.Shore.Relocate(ShoreStart, back)) _c.Shore.Relocate(_c.Home.Entry, back);
            _c.Notice("潮水漫过了礁石，你踩着最后几块露出的石头回到了岸上。查看手账里的潮汐表，下次退潮再来。", 8);
            Haptic(.6f, .4f);
        }
        private static void Haptic(float amplitude, float seconds)
        {
            foreach (var node in new[] { XRNode.LeftHand, XRNode.RightHand }) InputDevices.GetDeviceAtXRNode(node).SendHapticImpulse(0, amplitude, seconds);
        }

        private int Nearest(Vector3 from, float reach)
        {
            int best = -1; float nearest = reach;
            for (int i = 0; i < FishingTide.ItemCount; i++)
            {
                var t = _items[i]; if (!t.gameObject.activeInHierarchy) continue;
                float d = FishingPrototype.FlatDistance(t.position, from);
                if (d < nearest && Mathf.Abs(t.position.y - from.y) < 1.4f) { nearest = d; best = i; }
            }
            return best;
        }
        public string NearbyHint
        {
            get
            {
                if (!_solid || !_c.Ashore || _c.Shore.Swimming) return null;
                int i = Nearest(_c.Shore.Position, 1.3f); if (i < 0) return null;
                bool crab = i >= FishingTide.ShellCount && i < FishingTide.ShellCount + FishingTide.CrabCount;
                return (crab ? "E 抓住 " : i >= FishingTide.ShellCount + FishingTide.CrabCount ? "E 查看 " : "E 拾起 ") + FishingTide.Names[i] + (crab ? " · 蹲下靠近它跑得慢" : "");
            }
        }
        public bool TryInteract()
        {
            if (NearbyHint == null) return false;
            return Collect(Nearest(_c.Shore.Position, 1.3f));
        }
        public bool Collect(int i)
        {
            if (i < 0 || i >= FishingTide.ItemCount || !_solid || !_c.Career.Lore.SetTideFlag(i)) return false;
            var at = _items[i].position; _items[i].gameObject.SetActive(false);
            int count = 0; for (int k = 0; k < FishingTide.ItemCount; k++) if (_c.Career.Lore.TideFlag(k)) count++;
            bool crab = i >= FishingTide.ShellCount && i < FishingTide.ShellCount + FishingTide.CrabCount;
            _c.WorldAudio?.Play(i >= FishingTide.ShellCount + FishingTide.CrabCount ? "StingerDiscover" : "RareShimmer", at, .6f);
            _c.Effects?.Celebrate(at);
            _c.Notice("潮间图鉴 " + count + " / " + FishingTide.ItemCount + " · " + FishingTide.Names[i] + "：" + FishingTide.Lines[i] + (crab ? "（记下后放回了潮池）" : ""), 9);
            _c.Save(); return true;
        }
        private void TickVrGrab()
        {
            if (_c.DesktopDisplay || _c.Hands == null || !_c.Ashore) return;
            foreach (bool left in new[] { false, true })
            {
                if (!_c.Hands.TryControllerPalm(left, out var palm, out _)) continue;
                InputDevices.GetDeviceAtXRNode(left ? XRNode.LeftHand : XRNode.RightHand).TryGetFeatureValue(CommonUsages.grip, out float grip);
                if (grip < .6f) continue;
                int i = -1; float nearest = .3f;
                for (int k = 0; k < FishingTide.ItemCount; k++)
                    if (_items[k].gameObject.activeInHierarchy && Vector3.Distance(_items[k].position, palm) < nearest) { nearest = Vector3.Distance(_items[k].position, palm); i = k; }
                if (i >= 0 && Collect(i)) InputDevices.GetDeviceAtXRNode(left ? XRNode.LeftHand : XRNode.RightHand).SendHapticImpulse(0, .4f, .08f);
            }
        }
        private void OnDestroy() { foreach (var o in _owned) if (o != null) Destroy(o); }
    }

    public sealed partial class FishingMeshBuilder
    {
        /// <summary>Noise-displaced ellipsoid coloured by local height, with a flattened sand-sunk base.</summary>
        public void Lumpy(Vector3 center, Vector3 scale, int seed, System.Func<float, Color> colorAt, int sides = 12, int rings = 8)
        {
            int start = _vertices.Count;
            for (int r = 0; r <= rings; r++) for (int j = 0; j < sides; j++)
            {
                float a = r * Mathf.PI / rings, t = j * Mathf.PI * 2 / sides;
                var d = new Vector3(Mathf.Sin(a) * Mathf.Cos(t), Mathf.Cos(a), Mathf.Sin(a) * Mathf.Sin(t));
                float bump = 1 + .12f * Mathf.Sin(t * 3 + seed) * Mathf.Sin(a * 2 + seed * .7f) + .06f * Mathf.Sin(t * 7 + a * 5 + seed);
                var p = Vector3.Scale(d * bump, scale); if (p.y < -.35f) p.y = -.35f + (p.y + .35f) * .3f;
                Add(center + p, colorAt(p.y));
            }
            for (int r = 0; r < rings; r++) for (int j = 0; j < sides; j++)
            { int a = start + r * sides + j, b = start + r * sides + (j + 1) % sides, c = a + sides, d = b + sides; Face(a, b, c); Face(b, d, c); }
        }
        /// <summary>One-sided quad; corners clockwise as seen from the front face.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            int start = _vertices.Count; Add(a, color); Add(b, color); Add(c, color); Add(d, color);
            Face(start, start + 1, start + 2); Face(start, start + 2, start + 3);
        }
    }
}
