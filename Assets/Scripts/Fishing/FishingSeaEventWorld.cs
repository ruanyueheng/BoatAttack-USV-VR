using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;
using Unity.Collections;
using float3 = Unity.Mathematics.float3;
using WaterSystem;

namespace BoatAttack.Fishing
{
    /// <summary>
    /// Presents the five random sea events. Guidance is a world-space light point plus spatial sound, never a screen popup;
    /// nothing here pushes the boat, so hull motion stays exactly as the water already makes it.
    /// </summary>
    public sealed class FishingSeaEventWorld : MonoBehaviour
    {
        public const int ParticleBudget = 150, PicoParticleBudget = 100;
        public static int Budget => Vr || Application.platform == RuntimePlatform.Android ? PicoParticleBudget : ParticleBudget;
        public static bool ForceVr;
        public static bool Vr => ForceVr || XRSettings.isDeviceActive;
        private FishingPrototype _c;
        public FishingSeaSchedule Schedule { get; private set; }
        /// <summary>Automated runs stay quiet unless a test opts in, so other suites never meet a surprise whale.</summary>
        public bool AutoSchedule;
        public SeaEventKind? Shown { get; private set; }
        public Vector3 Center { get; private set; }
        public int EventSpot { get; private set; } = -1;
        public int CuePlays { get; private set; }
        public Transform Beacon => _beacon;
        public Transform Whale => _whale;
        public Transform Fluke => _fluke;
        public float EventAge => _age;
        public Transform GlintMarker => _glint;
        public Transform Ghost => _ghost;
        public Transform Bottle => _bottle;
        public Transform Crate => _crate;
        public IReadOnlyList<Transform> Dolphins => _dolphins;
        public int ParticleCount => _fx == null ? 0 : _fx.particleCount;
        public int WhaleTriangles { get; private set; }
        private readonly List<Object> _owned = new List<Object>();
        private readonly List<Transform> _dolphins = new List<Transform>(), _gulls = new List<Transform>();
        private Transform _root, _beacon, _whale, _ghost, _bottle, _crate;
        private ParticleSystem _fx;
        private float _age, _nextCue, _approach;
        private bool _ghostMet, _whaleMet;
        private int _crateLetter = -1;

        public void Build(FishingPrototype c)
        {
            _c = c;
            Schedule = new FishingSeaSchedule(c.Automated ? 4401 : System.Environment.TickCount);
            AutoSchedule = !c.Automated;
            _root = new GameObject("V044 sea events").transform; _root.SetParent(transform, false);
            var palette = new Material(c.PaletteShader) { name = "Sea event vertex palette" }; _owned.Add(palette);
            _palette = palette;
            var glow = new Material(Resources.Load<Shader>("FishingNightSky")) { name = "Sea event guide light" }; _owned.Add(glow);
            glow.SetColor("_Tint", new Color(1.7f, 1.3f, .6f)); glow.SetFloat("_Solid", 1); glow.SetFloat("_Visibility", 1);
            _beacon = Shape("World guide light point", PrimitiveType.Sphere, glow, Vector3.one * .7f);

            // AI-modelled assets from model-polish win when present; procedural fallbacks keep the game whole without them.
            _whale = Supplied("Whale");
            if (_whale == null) { _whale = Mesh("Surfacing whale", WhaleMesh(), palette); var fluke = Mesh("Fluke", FlukeMesh(), palette); fluke.SetParent(_whale, false); fluke.localPosition = new Vector3(0, 0, -4.6f); }
            _fluke = FindDeep(_whale, "Fluke");
            if (WhaleTriangles == 0)
                foreach (var f in _whale.GetComponentsInChildren<MeshFilter>(true)) if (f.sharedMesh != null) for (int i = 0; i < f.sharedMesh.subMeshCount; i++) WhaleTriangles += (int)f.sharedMesh.GetIndexCount(i) / 3;
            for (int i = 0; i < 3; i++)
            {
                var dolphin = Supplied("Dolphin") ?? Mesh("Bow-riding dolphin " + i, FishingMeshBuilder.Animal(MarineKind.Dolphin), palette);
                dolphin.localScale = Vector3.one * 1.25f; _dolphins.Add(dolphin);
            }
            var gullMesh = FishingV029Art.Animal("Seagull");
            for (int i = 0; i < 3; i++)
            {
                var gull = Mesh("Circling shoal gull " + i, gullMesh != null ? gullMesh : FishingMeshBuilder.Animal(MarineKind.Gull), palette);
                if (gullMesh != null) _owned.Remove(gullMesh); // shared Resources asset, never destroy it
                FishingV029Art.Apply(gull.GetComponent<Renderer>(), gullMesh != null ? "Seagull" : null, _owned, c.PaletteShader); _gulls.Add(gull);
            }
            _ghost = Supplied("GhostShip") ?? Mesh("Old ship silhouette in fog", GhostShipMesh(), palette);
            var glass = new Material(Resources.Load<Shader>("FishingGlass")) { name = "Drift bottle glass" }; _owned.Add(glass);
            glass.SetColor("_BaseColor", new Color(.36f, .62f, .5f, .5f));
            _bottle = Supplied("DriftBottle");
            if (_bottle != null) { var glassPart = FindDeep(_bottle, "Glass"); if (glassPart != null) foreach (var r in glassPart.GetComponentsInChildren<Renderer>()) r.sharedMaterial = glass; }
            else
            {
                _bottle = Mesh("Drift bottle", BottleInnerMesh(), palette);
                var shell = Mesh("Glass", BottleGlassMesh(), glass); shell.SetParent(_bottle, false);
            }
            _bottle.localScale *= 1.4f; // readable from the helm at 20m+
            // A breathing pale-gold halo lying on the water: from afar the bottle reads as a glint on the sea.
            var halo = new Material(Resources.Load<Shader>("FishingSpray")) { name = "Drift bottle sea halo" }; _owned.Add(halo); halo.SetFloat("_Round", 1);
            halo.renderQueue = 3050; // after the water (Transparent-100)
            _haloBlend = halo; _haloAdd = new Material(Resources.Load<Shader>("FishingSeaGlow")) { name = "Drift bottle additive halo" }; _owned.Add(_haloAdd);
            var quad = new Mesh { name = "Bottle halo quad", vertices = new[] { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(1, 0, 1), new Vector3(-1, 0, 1) },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up }, colors = new Color[] { Glint, Glint, Glint, Glint }, triangles = new[] { 0, 2, 1, 0, 3, 2 } };
            quad.RecalculateBounds(); _halo = Mesh("Drift bottle halo", quad, halo);
            // Flat halos vanish at grazing angles over bright water; an upright four-point glint faces the viewer and survives distance.
            var star = new Material(halo) { name = "Drift bottle glint" }; _owned.Add(star); star.SetFloat("_Round", 2);
            // Two crossed quads read from any heading without a per-camera billboard.
            var glint = new Mesh { name = "Bottle glint cross", vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(.5f, .5f, 0), new Vector3(-.5f, .5f, 0),
                    new Vector3(0, -.5f, -.5f), new Vector3(0, -.5f, .5f), new Vector3(0, .5f, .5f), new Vector3(0, .5f, -.5f) },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector2.zero, Vector2.right, Vector2.one, Vector2.up },
                colors = new[] { GlintStar, GlintStar, GlintStar, GlintStar, GlintStar, GlintStar, GlintStar, GlintStar }, triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 } };
            glint.RecalculateBounds(); _glint = Mesh("Drift bottle glint", glint, star);
            AdditiveHalo = true;
            _crate = BuildTreasure(palette);

            var fx = new GameObject("Sea event particles (150 max)"); fx.transform.SetParent(_root, false);
            _fx = fx.AddComponent<ParticleSystem>(); _fx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _fx.main; main.maxParticles = ParticleBudget; main.startSpeed = 0; main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = .5f;
            var emission = _fx.emission; emission.enabled = false; var shape = _fx.shape; shape.enabled = false;
            var fade = _fx.colorOverLifetime; fade.enabled = true; var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .15f), new GradientAlphaKey(0, 1) }); fade.color = g;
            var spray = new Material(Resources.Load<Shader>("FishingSpray")) { name = "Sea event droplets" }; _owned.Add(spray);
            var fr = fx.GetComponent<ParticleSystemRenderer>(); fr.sharedMaterial = spray; fr.shadowCastingMode = ShadowCastingMode.Off; fr.receiveShadows = false;

            Hide();
            PlaceCrate();
        }

        private Transform _fluke, _halo, _glint;
        private Material _haloBlend, _haloAdd;
        /// <summary>The far-sight flare: about 0.6 m, fading out inside 8 m where the bottle itself reads.</summary>
        public void SizeGlint(float t)
        {
            float d = Vector3.Distance(_c.viewCamera.transform.position, _bottle.position);
            // Roughly constant on-screen size: 0.6 m near, 1.1 m at 20 m; dimmer at night where the dark sea already lets it pop.
            float day = _c.Career.Calendar.Daylight, size = Mathf.Lerp(.6f, 1.1f, Mathf.InverseLerp(8, 20, d)) * Mathf.Lerp(.6f, 1, day);
            _glint.localScale = Vector3.one * (size * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(4, 8, d)) * (.85f + .15f * Mathf.Sin(t * 2.3f)) + .001f);
            _halo.localScale = Vector3.one * Mathf.Lerp(.9f, 1.5f, Mathf.InverseLerp(0, 1, 1 - day * .5f));
        }
        /// <summary>Visual A/B for review: additive sea halo versus the alpha one.</summary>
        public bool AdditiveHalo { get => _halo.GetComponent<Renderer>().sharedMaterial == _haloAdd; set => _halo.GetComponent<Renderer>().sharedMaterial = value ? _haloAdd : _haloBlend; }
        private static readonly Color GlintStar = new Color(2.4f, 1.55f, .55f, .9f);
        // HDR vertex tint so desktop bloom lifts the halo off bright water; still a soft alpha disc on PICO.
        private static readonly Color Glint = new Color(1.2f, 1.05f, .65f, .4f); // halved so the bottle glass still reads up close
        private NativeArray<float3> _wavePoint;
        private float3[] _waveHeight = new float3[1], _waveNormal = new float3[1];
        /// <summary>Live Gerstner swell height, so floating props ride the wave instead of hiding under crests.</summary>
        private float SwellAt(Vector3 p)
        {
            if (!GerstnerWavesJobs.Initialized) return 0;
            if (!_wavePoint.IsCreated) _wavePoint = new NativeArray<float3>(1, Allocator.Persistent);
            _wavePoint[0] = p; GerstnerWavesJobs.UpdateSamplePoints(ref _wavePoint, GetInstanceID());
            GerstnerWavesJobs.GetData(GetInstanceID(), ref _waveHeight, ref _waveNormal);
            return _waveHeight[0].y;
        }
        private Material _palette;
        private Transform Supplied(string name)
        {
            var source = Resources.Load<GameObject>("FishingV044/Sea/" + name);
            if (source == null) return null;
            var t = Instantiate(source, _root).transform; t.name = name;
            // Hand-painted albedo when model-polish shipped one, otherwise the vertex-colour palette.
            if (Resources.Load<Texture2D>("FishingV044/Sea/" + name + "Albedo") != null) FishingPaintedArt.Apply(t, "FishingV044/Sea/" + name);
            else foreach (var r in t.GetComponentsInChildren<Renderer>()) r.sharedMaterial = _palette;
            foreach (var r in t.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            return t;
        }
        private static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }
        /// <summary>Model-polish's hinged chest afloat under a gold light pillar and a small buoy.</summary>
        private Transform BuildTreasure(Material palette)
        {
            var root = new GameObject("Drifting treasure chest").transform; root.SetParent(_root, false);
            var source = Resources.Load<GameObject>("FishingFeedbackTools/HomeChest");
            if (source != null)
            {
                var chest = Instantiate(source, root).transform; chest.name = "Treasure chest";
                FishingPaintedArt.Apply(chest, "FishingFeedbackTools/HomeChest");
                foreach (var r in chest.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                var bounds = new Bounds(chest.position, Vector3.zero); bool first = true;
                foreach (var r in chest.GetComponentsInChildren<Renderer>()) { if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds); }
                chest.localScale *= .9f / Mathf.Max(.01f, bounds.size.x);
                chest.localPosition = new Vector3(0, -.25f, 0);
            }
            var b = new FishingMeshBuilder();
            b.Tube(new Vector3(1.1f, -.3f, 0), new Vector3(1.1f, .25f, 0), .16f, .2f, new Color(.93f, .45f, .2f), 10);
            b.Tube(new Vector3(1.1f, .25f, 0), new Vector3(1.1f, .4f, 0), .2f, .12f, new Color(.95f, .93f, .86f), 10);
            b.Tube(new Vector3(1.1f, .4f, 0), new Vector3(1.1f, .75f, 0), .015f, .015f, new Color(.3f, .3f, .3f), 5);
            Mesh("Treasure buoy", b.Finish("Treasure buoy"), palette).SetParent(root, false);
            var beam = new Material(Resources.Load<Shader>("FishingLighthouseBeam")) { name = "Treasure light pillar" }; _owned.Add(beam);
            beam.SetColor("_Tint", new Color(3.2f, 2.4f, .9f, .35f)); beam.SetFloat("_Visibility", 1);
            var pillar = Shape("Gold light pillar", PrimitiveType.Cylinder, beam, new Vector3(.7f, 9, .7f)); pillar.SetParent(root, false); pillar.localPosition = Vector3.up * 8.6f;
            return root;
        }
        private Transform Shape(string name, PrimitiveType type, Material m, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(_root, false); go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; return go.transform;
        }
        private Transform Mesh(string name, Mesh mesh, Material m)
        {
            _owned.Add(mesh);
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(_root, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh; var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off; return go.transform;
        }

        private Mesh WhaleMesh()
        {
            var b = new FishingMeshBuilder(); var back = new Color(.2f, .27f, .34f); var belly = new Color(.66f, .7f, .7f);
            b.Ellipsoid(Vector3.zero, new Vector3(1.5f, 1.35f, 5.2f), back, 20, 12);
            b.Ellipsoid(new Vector3(0, -.55f, 1.2f), new Vector3(1.25f, .8f, 3.6f), belly, 18, 10);
            b.Ellipsoid(new Vector3(0, .1f, 4.4f), new Vector3(1.1f, .95f, 1.6f), back, 16, 10);
            // Ventral pleats and the humpback's long pale flippers carry the silhouette.
            for (int i = 0; i < 5; i++) b.Ellipsoid(new Vector3((i - 2) * .32f, -1.12f, 2.3f), new Vector3(.07f, .05f, 2.3f), new Color(.82f, .84f, .82f), 8, 4);
            foreach (float s in new[] { -1f, 1f })
            {
                b.Ellipsoid(new Vector3(s * 2.1f, -.85f, 1.6f), new Vector3(2.4f, .1f, .45f), new Color(.78f, .8f, .8f), 16, 6, Quaternion.Euler(0, s * 38, s * 28));
                b.Ellipsoid(new Vector3(s * .95f, .2f, 4.9f), Vector3.one * .1f, new Color(.05f, .06f, .08f), 8, 5);
            }
            b.Fin(new Vector3(0, 1.2f, -2.2f), new Vector3(0, 1.75f, -3.1f), new Vector3(0, 1.1f, -3.5f), back);
            var mesh = b.Finish("Procedural whale"); WhaleTriangles += mesh.triangles.Length / 3; return mesh;
        }
        /// <summary>Tail stock and flukes pivoting at the body's end, so the tail can beat and lift on a dive.</summary>
        private Mesh FlukeMesh()
        {
            var b = new FishingMeshBuilder(); var back = new Color(.2f, .27f, .34f);
            b.Tube(new Vector3(0, 0, .3f), new Vector3(0, .15f, -2.8f), .95f, .28f, back, 14);
            foreach (float s in new[] { -1f, 1f }) b.Ellipsoid(new Vector3(s * 1.4f, .2f, -3.1f), new Vector3(1.6f, .1f, .6f), back, 14, 6, Quaternion.Euler(0, s * -22, 0));
            var mesh = b.Finish("Procedural whale flukes"); WhaleTriangles += mesh.triangles.Length / 3; return mesh;
        }
        private static Mesh GhostShipMesh()
        {
            var b = new FishingMeshBuilder(); var hull = new Color(.3f, .3f, .32f); var sail = new Color(.72f, .74f, .74f);
            b.Ellipsoid(new Vector3(0, .4f, 0), new Vector3(2.4f, 1.3f, 8), hull, 16, 8);
            b.Ellipsoid(new Vector3(0, 1.6f, -6.2f), new Vector3(1.9f, .9f, 1.6f), hull, 12, 6);
            foreach (float z in new[] { 2.8f, -1.6f })
            {
                b.Tube(new Vector3(0, 1.4f, z), new Vector3(0, 12, z), .16f, .09f, hull, 8);
                for (int i = 0; i < 2; i++)
                {
                    // Fore-and-aft sails read as a ship from the side, which is how the player meets it.
                    float y = 3.2f + i * 3.8f, w = 2.6f - i * .6f;
                    b.Fin(new Vector3(0, y, z - w), new Vector3(0, y, z + w * .2f), new Vector3(0, y + 3.4f, z + w * .2f), sail);
                    b.Fin(new Vector3(0, y, z - w), new Vector3(0, y + 3.4f, z + w * .2f), new Vector3(0, y + 3.4f, z - w * .8f), sail);
                }
            }
            b.Tube(new Vector3(0, 1.4f, 7.5f), new Vector3(0, 3.2f, 12), .12f, .05f, hull, 6);
            return b.Finish("Old ship silhouette");
        }
        private static Mesh BottleGlassMesh()
        {
            var b = new FishingMeshBuilder(); var glass = Color.white;
            b.Tube(new Vector3(0, 0, -.2f), new Vector3(0, 0, .12f), .1f, .1f, glass, 16);
            b.Ellipsoid(new Vector3(0, 0, .12f), new Vector3(.1f, .1f, .09f), glass, 16, 8);
            b.Tube(new Vector3(0, 0, .18f), new Vector3(0, 0, .33f), .04f, .036f, glass, 12);
            return b.Finish("Old drift bottle glass");
        }
        private static Mesh BottleInnerMesh()
        {
            var b = new FishingMeshBuilder(); var paper = new Color(.93f, .87f, .72f);
            b.Tube(new Vector3(0, 0, .31f), new Vector3(0, 0, .39f), .039f, .043f, new Color(.6f, .45f, .3f), 10);
            b.Tube(new Vector3(0, .01f, -.15f), new Vector3(0, .01f, .1f), .045f, .045f, paper, 10);
            b.Tube(new Vector3(0, .01f, -.12f), new Vector3(0, .01f, -.08f), .048f, .048f, new Color(.7f, .2f, .16f), 10);
            return b.Finish("Rolled letter and cork");
        }

        private SeaConditions Conditions()
        {
            var cal = _c.Career.Calendar; var w = _c.Weather.Climate.Current;
            return new SeaConditions
            {
                AtSea = !_c.Ashore && !_c.Realms.Inside, Night = cal.Night, Mist = w == BayWeather.Mist, Rain = w == BayWeather.Rain,
                GlowNight = FishingSeaSchedule.GlowNight(cal.Hour < 6 ? cal.Day - 1 : cal.Day) || FishingFestival.On(cal.Hour < 6 ? cal.Day - 1 : cal.Day) == 1, NearHarbor = _c.HarborDistance < 90,
            };
        }
        /// <summary>Where the active event's subject is, for the photo codex; false when that event is not showing.</summary>
        public bool TryGetSubject(SeaEventKind kind, out Vector3 pos)
        {
            pos = kind == SeaEventKind.Whales ? _whale.position : kind == SeaEventKind.Fog ? _ghost.position : kind == SeaEventKind.Bottle ? _bottle.position : kind == SeaEventKind.Glow ? Boat : Center;
            return Shown == kind;
        }
        public void Force(SeaEventKind kind)
        {
            if (Schedule.Active.HasValue) Schedule.Finish();
            Schedule.Start(kind); Begin(kind);
        }

        private void Update()
        {
            if (_c == null || _c.Career == null || _c.Player == null || _c.Paused || _c.StoryBlocksGameplay) return;
            float dt = Time.deltaTime;
            var started = AutoSchedule ? Schedule.Tick(dt, Conditions()) : null;
            if (!AutoSchedule && Schedule.Active.HasValue) Schedule.Tick(dt, Conditions());
            if (started.HasValue) Begin(started.Value);
            if (Shown.HasValue && Schedule.Active != Shown) Hide();
            if (Shown.HasValue) Animate(Shown.Value, dt);
            TickCrate();
        }

        private Vector3 Boat => _c.Player.transform.position;
        private Vector3 Forward => Vector3.ProjectOnPlane(_c.Player.transform.forward, Vector3.up).normalized;
        private Vector3 Ahead(float distance, float clear = 5)
        {
            foreach (float scale in new[] { 1, .7f, .45f })
                for (int i = 0; i < 8; i++)
                {
                    var p = Boat + Quaternion.Euler(0, i * 45, 0) * Forward * distance * scale; p.y = 0;
                    if (OpenWater(p, clear)) return p;
                }
            var fallback = Boat + Forward * 8; fallback.y = 0; return fallback;
        }
        /// <summary>Deep water at the point and around it: the topmost non-water surface is below -1.2m.</summary>
        public static bool OpenWater(Vector3 p, float radius)
        {
            foreach (var o in new[] { Vector3.zero, Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                if (Physics.Raycast(new Vector3(p.x, 80, p.z) + o * radius, Vector3.down, out var hit, 200, ~((1 << 4) | (1 << 5)), QueryTriggerInteraction.Ignore) && hit.point.y > -1.2f) return false;
            return true;
        }

        private void Begin(SeaEventKind kind)
        {
            Hide(); Shown = kind; _age = 0; _nextCue = 0; _approach = 0; _ghostMet = _whaleMet = false; EventSpot = -1;
            switch (kind)
            {
                case SeaEventKind.Shoal:
                    float best = float.MaxValue;
                    for (int i = 0; i < _c.fishingSpots.Length; i++)
                    {
                        if (!FishingGrounds.AvailableForCareer(i, _c.Career)) continue;
                        float d = FishingPrototype.FlatDistance(Boat, _c.fishingSpots[i]);
                        if (d < best) { best = d; EventSpot = i; }
                    }
                    if (EventSpot < 0) EventSpot = 0;
                    Center = _c.fishingSpots[EventSpot]; Center = new Vector3(Center.x, 0, Center.z);
                    foreach (var gull in _gulls) gull.gameObject.SetActive(true);
                    break;
                case SeaEventKind.Bottle: Center = Ahead(28); _bottle.gameObject.SetActive(true); break;
                case SeaEventKind.Fog:
                    Center = _c.Home.LighthouseLens;
                    var ship = Ahead(45); ship.y = -.2f;
                    _ghost.position = ship; _ghost.rotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, ship - Boat).normalized);
                    _ghost.gameObject.SetActive(true); break;
                case SeaEventKind.Whales:
                    Center = Ahead(45, 20); _whale.gameObject.SetActive(true); // the whale's 12m orbit plus its body stays clear of rocks
                    foreach (var d in _dolphins) d.gameObject.SetActive(true);
                    break;
                default: Center = Boat; break;
            }
            _beacon.gameObject.SetActive(kind != SeaEventKind.Glow && kind != SeaEventKind.Bottle);
            _halo.gameObject.SetActive(kind == SeaEventKind.Bottle); _glint.gameObject.SetActive(kind == SeaEventKind.Bottle);
            // Spray arcs fall; glow motes and fog wisps float on the surface.
            var main = _fx.main; main.gravityModifier = kind == SeaEventKind.Glow || kind == SeaEventKind.Fog ? 0 : .5f;
            var fade = _fx.colorOverLifetime; var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, kind == SeaEventKind.Glow
                ? new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .08f), new GradientAlphaKey(.25f, .3f), new GradientAlphaKey(.8f, .45f), new GradientAlphaKey(.15f, .7f), new GradientAlphaKey(0, 1) }
                : new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .15f), new GradientAlphaKey(0, 1) });
            fade.color = g;
            _fx.Play();
            if (_c.Career.Lore.SetSeaFlag((int)kind)) { PayCommissions(); _saveDue = true; }
        }
        private static Color GlowTint() => Color.Lerp(new Color(.25f, .75f, 1, 1), new Color(.55f, 1, .9f, 1), Random.value);
        /// <summary>The fog-light event asks sky-weather for its thick fog; the weather owns the blend.</summary>
        private bool _fogAsked;
        private void LateUpdate()
        {
            bool want = Shown == SeaEventKind.Fog;
            if (_c == null || _c.Weather == null || want == _fogAsked) return;
            _fogAsked = want; _c.Weather.EventFog = want ? 1 : 0;
        }
        private bool _saveDue;
        private void Hide()
        {
            if (_saveDue && _c != null) { _saveDue = false; _c.Save(); }
            Shown = null;
            foreach (Transform t in _root) if (t != _crate) t.gameObject.SetActive(t == _fx?.transform);
            _fx?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void Cue(string key, Vector3 at, float gain)
        {
            CuePlays++;
            // World cues reach 24m; keep the direction but pull far sources to 18m from the listener.
            var ear = _c.viewCamera.transform.position; var d = at - ear;
            if (d.magnitude > 18) at = ear + d.normalized * 18;
            if (_c.WorldAudio != null) _c.WorldAudio.Play(key, at, gain, FishingAudioCategory.Ambient);
        }
        private void Emit(Vector3 at, Vector3 velocity, Color color, float size, float life)
            => _fx.Emit(new ParticleSystem.EmitParams { position = at, velocity = velocity, startColor = color, startSize = size, startLifetime = life }, 1);

        private void Animate(SeaEventKind kind, float dt)
        {
            _age += dt; float t = _age;
            if (kind != SeaEventKind.Glow)
            {
                var top = kind == SeaEventKind.Fog ? Center + Vector3.up * 3 : (kind == SeaEventKind.Whales && _whale != null ? new Vector3(_whale.position.x, 0, _whale.position.z) : Center) + Vector3.up * (5 + Mathf.Sin(t * 1.6f) * .25f);
                _beacon.position = top; _beacon.localScale = Vector3.one * ((kind == SeaEventKind.Fog ? 3.2f : .7f) * (1 + .15f * Mathf.Sin(t * 3)));
            }
            bool cue = t >= _nextCue; if (cue) _nextCue = t + (kind == SeaEventKind.Fog ? 12 : 9);
            float budget = Budget - _fx.particleCount;
            switch (kind)
            {
                case SeaEventKind.Shoal:
                    for (int i = 0; i < 4 && budget-- > 0; i++)
                    {
                        float a = Random.value * Mathf.PI * 2, r = 1 + Random.value * 7;
                        Emit(Center + new Vector3(Mathf.Cos(a) * r, .1f, Mathf.Sin(a) * r), new Vector3((Random.value - .5f) * .6f, 2 + Random.value * 2.5f, (Random.value - .5f) * .6f), new Color(.94f, .98f, 1, .95f), .3f + Random.value * .3f, 1.1f);
                    }
                    for (int i = 0; i < _gulls.Count; i++)
                    {
                        float a = t * .55f + i * 2.1f;
                        var p = Center + new Vector3(Mathf.Cos(a) * (7 + i), 7 + i * .8f + Mathf.Sin(t * 1.3f + i) * .5f, Mathf.Sin(a) * (7 + i));
                        _gulls[i].SetPositionAndRotation(p, Quaternion.LookRotation(new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a))) * Quaternion.Euler(0, 0, 18));
                    }
                    if (cue) { Cue("SeagullCall", Center + Vector3.up * 7, .9f); Cue("FishSplash", Center, .7f); }
                    break;
                case SeaEventKind.Bottle:
                    _bottle.SetPositionAndRotation(new Vector3(Center.x, SwellAt(Center), Center.z) + Vector3.up * (.06f + Mathf.Sin(t * 1.7f) * .05f), Quaternion.Euler(-12 + Mathf.Sin(t * 1.3f) * 8, t * 6, Mathf.Sin(t) * 10));
                    if (cue) Cue("RareShimmer", Center, .6f);
                    _halo.position = new Vector3(Center.x, _bottle.position.y + .02f, Center.z); _glint.SetPositionAndRotation(_bottle.position + Vector3.up * .5f, Quaternion.Euler(0, t * 12, 0));
                    SizeGlint(t);

                    // A faint star glint at the neck every 3 s so the bottle reads from afar.
                    if (Mathf.Repeat(t, 3) < Time.deltaTime) _c.Effects?.Splash(_bottle.position, .3f); // a widening ring reads further than the bottle itself
                    if (Mathf.Repeat(t, 3) < Time.deltaTime) _c.Effects?.Burst(_bottle.position + _bottle.forward * .45f + Vector3.up * .1f, new Color(1, .92f, .7f));
                    if (FishingPrototype.FlatDistance(Boat, Center) < 5) PickUpBottle();
                    break;
                case SeaEventKind.Fog:
                    if (cue) Cue("SeaFogHorn", Center, .6f);
                    if (_ghost.gameObject.activeSelf)
                    {
                        _ghost.position += _ghost.forward * 1.2f * dt + Vector3.up * Mathf.Sin(t * .6f) * .02f * dt;
                        if (!_ghostMet && FishingPrototype.FlatDistance(Boat, _ghost.position) < 14) MeetGhost();
                    }
                    if (_c.HarborDistance < 40) Schedule.Finish();
                    break;
                case SeaEventKind.Whales:
                    // 16s cycle: surface and blow, arch into a fluke-up dive, glide below, rise again.
                    float cycle = t % 16, orbit = t * .11f;
                    float depth = cycle < 5 ? -.55f : cycle < 9 ? Mathf.Lerp(-.55f, -5.5f, Mathf.SmoothStep(0, 1, (cycle - 5) / 4)) : cycle < 14 ? -5.5f : Mathf.Lerp(-5.5f, -.55f, (cycle - 14) / 2);
                    float pitch = cycle < 5 ? Mathf.Sin(t * .9f) * 2 : cycle < 9 ? Mathf.Sin((cycle - 5) / 4 * Mathf.PI) * 32 : cycle < 14 ? 0 : -12;
                    var heading = new Vector3(-Mathf.Sin(orbit), 0, Mathf.Cos(orbit));
                    _whale.SetPositionAndRotation(Center + new Vector3(Mathf.Cos(orbit), 0, Mathf.Sin(orbit)) * 12 + Vector3.up * depth, Quaternion.LookRotation(heading) * Quaternion.Euler(pitch, 0, Mathf.Sin(t * .4f) * 3));
                    if (_fluke != null) _fluke.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.7f) * 14 - (cycle > 6 && cycle < 9 ? 18 * Mathf.Sin((cycle - 6) / 3 * Mathf.PI) : 0), 0, 0);
                    if (cycle > 1.5f && cycle < 2.7f)
                        for (int i = 0; i < 4 && budget-- > 0; i++)
                            Emit(_whale.position + _whale.forward * 3.6f + Vector3.up * 1.2f, new Vector3((Random.value - .5f) * .8f, 4.5f + Random.value * 2, (Random.value - .5f) * .8f), new Color(.9f, .96f, 1, .85f), .25f + Random.value * .2f, 1.6f);
                    if (cue) Cue("SeaWhaleSpout", _whale.position, .9f);
                    var right = Vector3.Cross(Vector3.up, Forward).normalized;
                    for (int i = 0; i < _dolphins.Count; i++)
                    {
                        float phase = (t * .9f + i * .7f) % 3, leap = phase < 1.1f ? Mathf.Sin(phase / 1.1f * Mathf.PI) : 0;
                        var p = Boat + right * ((i - 1) * 3.5f + (i == 1 ? 0 : Mathf.Sign(i - 1) * 2)) + Forward * (6 + i * 1.5f);
                        p.y = -.25f + leap * 1.1f;
                        _dolphins[i].SetPositionAndRotation(p, Quaternion.LookRotation(Forward) * Quaternion.Euler(-Mathf.Cos(phase / 1.1f * Mathf.PI) * 30 * (leap > 0 ? 1 : 0), 0, 0));
                    }
                    bool slow = _c.Speed < 3, near = FishingPrototype.FlatDistance(Boat, Center) < 30;
                    _approach = slow && near ? _approach + dt : 0;
                    if (!_whaleMet && _approach > 1.5f) RecordWhales();
                    break;
                default:
                    // Bioluminescent motes on the swell and a cyan wake behind a moving hull.
                    // Like real dinoflagellates: sparse pin-pricks that flash and fade, brightest where the hull disturbs the water.
                    if (Random.value < .6f && budget-- > 0)
                    {
                        float a = Random.value * Mathf.PI * 2, r = 2.5f + Random.value * 16;
                        Emit(Boat + new Vector3(Mathf.Cos(a) * r, -Boat.y + .05f, Mathf.Sin(a) * r), Vector3.zero, GlowTint(), .05f + Random.value * .06f, 1.6f + Random.value * 1.4f);
                    }
                    var fwd = Forward; var side = Vector3.Cross(Vector3.up, fwd);
                    float churn = Mathf.Clamp01(_c.Speed / 4);
                    for (int i = 0; i < 1 + (int)(churn * 3) && budget-- > 0; i++)
                    {
                        // Stern wake plus the two bow-wave lines where the hull and oars cut the swell.
                        int lane = Random.Range(0, 3); float along = Random.value;
                        var at = lane == 0 ? Boat - fwd * (2.4f + along * 4 * churn) + side * (Random.value - .5f) * (.6f + along * 1.6f)
                            : Boat + fwd * (1.6f - along * 3.4f) + side * (lane == 1 ? 1 : -1) * (.9f + along * .5f);
                        at.y = .05f;
                        Emit(at, side * (lane == 0 ? 0 : (lane == 1 ? .25f : -.25f)), GlowTint() * new Color(1.2f, 1.2f, 1.2f, 1), .07f + Random.value * .08f * (.4f + churn), 1.1f + Random.value * 1.4f);
                    }
                    if (cue) Cue("SeaGlow", Boat, .4f);
                    break;
            }
        }

        /// <summary>Catch hook: a boiling shoal guarantees rare fish at its ground; a glowing night yields luminous fish anywhere.</summary>
        public void AdjustCatch(int spot, ref FishKind kind, ref bool rare, bool legend)
        {
            if (legend) return;
            if (_c.SeaMarket != null && _c.SeaMarket.BaitActive && Random.value < .3f) rare = true;
            if (!Shown.HasValue) return;
            if (Shown == SeaEventKind.Shoal && spot == EventSpot) rare = true;
            if (Shown == SeaEventKind.Glow && _c.Career.Calendar.Night) { kind = Random.value < .6f ? FishKind.Lanternfish : FishKind.Jellyfish; rare = true; }
        }

        private void Reward(string text, Vector3 at)
        {
            _c.Effects?.Celebrate(at);
            Cue("RareShimmer", at, .7f);
            if (!Vr) _c.Notice(text, 8);
        }
        private void PickUpBottle()
        {
            var lore = _c.Career.Lore;
            Schedule.Finish(); Hide();
            if (FishingSeaMarket.ReplyDue(lore.SeaPost, lore.SeaPostDay, _c.Career.Calendar.Day) && FishingSeaMarket.ReplyReady(FishingSeaMarket.Pending(lore.SeaPost) - 1, lore)) { DeliverReply(); return; }
            int id = FishingSeaLetters.NextLetter(i => lore.SeaFlag(FishingSeaLetters.LetterBit + i));
            if (id < 0) { int paid = _c.Career.AwardSeaCoins(10); Reward("漂流瓶里只有一张潮湿的空白纸。" + (paid > 0 ? " +10 金币（瓶子卖给了杂货铺）" : ""), Boat); _c.Save(); return; }
            lore.SetSeaFlag(FishingSeaLetters.LetterBit + id); PayCommissions(); _c.Save();
            Cue("BookOpen", Boat, .6f);
            if (_c.Letter != null && _c.Letter.CanRead) _c.Letter.Show(FishingSeaLetters.Titles[id], FishingSeaLetters.Senders[id], FishingSeaLetters.Bodies[id]);
            PlaceCrate();
        }
        /// <summary>A sent letter floats back as a reply from whoever its phrase reached.</summary>
        private void DeliverReply()
        {
            var lore = _c.Career.Lore; int phrase = FishingSeaMarket.Pending(lore.SeaPost) - 1;
            lore.SetSeaPost((lore.SeaPost & ~(7 << FishingSeaMarket.PendingShift)) | 1 << (FishingSeaMarket.ReplyBit + phrase), 0); _c.Save();
            Cue("BookOpen", Boat, .6f);
            if (_c.Letter != null && _c.Letter.CanRead) _c.Letter.Show("回信 · “" + FishingSeaMarket.Phrases[phrase] + "”", FishingSeaMarket.ReplySenders[phrase], FishingSeaMarket.Replies[phrase]);
        }
        public bool TryGetMarket(out Vector3 pos)
        {
            var m = _c.SeaMarket; pos = m != null && m.Visible ? m.Position : Vector3.zero;
            return m != null && m.Visible;
        }
        private void MeetGhost()
        {
            _ghostMet = true; _ghost.gameObject.SetActive(false);
            int paid = _c.Career.AwardSeaCoins(FishingSeaLetters.GhostReward); _c.Save();
            for (int i = 0; i < 20; i++) Emit(_ghost.position + Random.insideUnitSphere * 5 + Vector3.up * 4, Vector3.up * .4f, new Color(.8f, .9f, 1, .7f), .4f, 2.5f);
            Reward("旧船的影子在雾里散开，甲板上留下一只系着铃铛的钱袋。+" + paid + " 金币。循着灯塔光和雾笛回港吧。", Boat);
        }
        private void RecordWhales()
        {
            _whaleMet = true; var lore = _c.Career.Lore;
            bool whale = lore.SetSeaFlag(FishingSeaLetters.WhaleBit), dolphin = _c.Career.Observe(MarineKind.Dolphin);
            if (!whale && !dolphin) return;
            // Save once the event ends: a JSON save mid-encounter is a frame-time and GC spike.
            PayCommissions(); _saveDue = true;
            Reward("图鉴收录：" + (whale ? "湾外座头鲸" : "") + (whale && dolphin ? "、" : "") + (dolphin ? BayClimate.MarineName(MarineKind.Dolphin) : "") + "。保持慢速，别惊扰它们。", Center);
        }
        private void PayCommissions()
        {
            var lore = _c.Career.Lore;
            for (int id = 6; id < FishingSeaLetters.Count; id++)
            {
                int paidBit = FishingSeaLetters.CommissionBit + id - 6;
                if (!lore.SeaFlag(FishingSeaLetters.LetterBit + id) || !lore.SeaFlag(FishingSeaLetters.CommissionGoal(id)) || lore.SeaFlag(paidBit)) continue;
                if (_c.Career.AwardSeaCoins(FishingSeaLetters.CommissionReward) <= 0) continue;
                lore.SetSeaFlag(paidBit);
                Reward(FishingSeaLetters.Senders[id] + "的委托完成 +" + FishingSeaLetters.CommissionReward + " 金币。", Boat);
            }
        }

        public Vector3 TreasurePoint(int id)
        {
            var spot = _c.fishingSpots[Mathf.Clamp(id - 3, 0, _c.fishingSpots.Length - 1)];
            for (int i = 0; i < 8; i++)
            {
                var p = spot + Quaternion.Euler(0, i * 45, 0) * Vector3.forward * 24; p.y = 0;
                if (OpenWater(p, 2)) return p;
            }
            return new Vector3(spot.x, 0, spot.z + 24);
        }
        private void PlaceCrate()
        {
            var lore = _c.Career.Lore; _crateLetter = -1;
            for (int id = 3; id <= 5 && _crateLetter < 0; id++)
                if (lore.SeaFlag(FishingSeaLetters.LetterBit + id) && !lore.SeaFlag(FishingSeaLetters.TreasureBit + id - 3)) _crateLetter = id;
            _crate.gameObject.SetActive(_crateLetter >= 0);
            if (_crateLetter >= 0) _crate.position = TreasurePoint(_crateLetter);
        }
        private void TickCrate()
        {
            if (_crateLetter < 0) return;
            var p = _crate.position; p.y = .1f + Mathf.Sin(Time.time * 1.4f) * .06f; _crate.SetPositionAndRotation(p, Quaternion.Euler(Mathf.Sin(Time.time) * 5, Time.time * 4, 0));
            if (_c.Ashore || FishingPrototype.FlatDistance(Boat, p) > 5) return;
            int paid = _c.Career.AwardSeaCoins(FishingSeaLetters.TreasureReward);
            if (paid <= 0) return;
            _c.Career.Lore.SetSeaFlag(FishingSeaLetters.TreasureBit + _crateLetter - 3); _c.Save();
            Reward("捞起漂流木箱：水手们留下的旧工钱 +" + paid + " 金币。", p);
            PlaceCrate();
        }
        private void OnDestroy()
        {
            foreach (var o in _owned) if (o != null) Destroy(o);
            if (_wavePoint.IsCreated) _wavePoint.Dispose();
        }
    }

    public sealed partial class FishingCareer
    {
        /// <summary>Coins paid, or 0 when the purse is near its ceiling.</summary>
        public int AwardSeaCoins(int amount)
        {
            if (amount <= 0 || Coins > 100000000 - amount) return 0;
            Coins += amount; return amount;
        }
    }
}
