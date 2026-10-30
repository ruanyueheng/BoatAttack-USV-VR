using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace BoatAttack.Fishing
{
    /// <summary>
    /// Lamp watch on the home lighthouse: an outer ladder (the shared climb system) to a new gallery, three lamp chores
    /// (oil, wipe, turn), late boats guided home by holding the beam on them, and thank-you parcels on the home jetty.
    /// Runs after <see cref="FishingNightscape"/> so a manned lens overrides its idle sweep.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class FishingKeeperWorld : MonoBehaviour
    {
        public const float DeckInner = 1.8f, DeckOuter = 3.4f, RailRadius = 3.3f, LadderRadius = 3.75f;
        public const float OilSeconds = 2f, WipeMetres = 2.5f, TurnRate = 50;
        /// <summary>Tests run the watch without finishing the whole story first.</summary>
        public static bool ForceUnlock;
        public enum Chore { Oil, Wipe, Crank }
        public sealed class Boat
        {
            public int Id; public bool Repeat, Guided, Arrived; public float Progress;
            public Vector3 Anchor; public Transform Hull, Lamp;
        }

        private FishingPrototype _c;
        private readonly List<Object> _owned = new List<Object>();
        private readonly List<Boat> _boats = new List<Boat>();
        private readonly List<Transform> _crust = new List<Transform>();
        private readonly Transform[] _parcels = new Transform[FishingTide.BoatCount + 3];
        private readonly int[] _parcelBoat = new int[FishingTide.BoatCount + 3];
        private readonly bool[] _guidedTonight = new bool[FishingTide.BoatCount];
        private Transform _root, _oilCan, _crank, _bucket;
        private Collider _chain;
        private Material _glow;
        private int _night = -1, _countedNight = -1;
        private Vector3 _lastPalm; private bool _palmValid;
        public Vector3 Axis { get; private set; }
        public float DeckY { get; private set; }
        public float LadderYaw { get; private set; }
        public FishingClimbRoute Ladder { get; private set; }
        public Vector3 Harbor { get; private set; }
        public float Oil { get; private set; }
        public float Clean { get; private set; }
        public bool Lit => Oil >= 1 && Clean >= 1;
        public bool Manning { get; private set; }
        public float BeamYaw { get; private set; }
        public bool WatchActive => _night >= 0;
        public IReadOnlyList<Boat> Boats => _boats;
        public int Triangles { get; private set; }
        public bool Unlocked => ForceUnlock || _c.Career.Lore.GrandTreasureFound;
        public Transform Station(Chore chore) => chore == Chore.Oil ? _oilCan : chore == Chore.Wipe ? _bucket : _crank;
        public Transform Parcel(int slot) => _parcels[slot];
        public Vector3 DeckPoint(float yawOffset, float radius) => Axis + Quaternion.Euler(0, LadderYaw + yawOffset, 0) * Vector3.forward * radius + Vector3.up * DeckY;

        public void Build(FishingPrototype c)
        {
            _c = c;
            var lens = c.Home.LighthouseLens;
            Axis = new Vector3(lens.x, 0, lens.z); DeckY = lens.y - .64f;
            var toHouse = Vector3.ProjectOnPlane(c.Home.Origin - Axis, Vector3.up);
            LadderYaw = Quaternion.LookRotation(toHouse).eulerAngles.y;
            _root = new GameObject("V044 lighthouse keeper gallery").transform; _root.SetParent(transform, false);
            _root.SetPositionAndRotation(Axis + Vector3.up * DeckY, Quaternion.Euler(0, LadderYaw, 0));
            var palette = new Material(c.PaletteShader) { name = "Keeper gallery vertex palette" }; _owned.Add(palette);
            _glow = new Material(Resources.Load<Shader>("FishingNightSky")) { name = "Late boat mast lamp" }; _owned.Add(_glow);
            _glow.SetColor("_Tint", new Color(1.9f, 1.25f, .5f)); _glow.SetFloat("_Solid", 1); _glow.SetFloat("_Visibility", 1);

            var deck = Part("Gallery deck", DeckMesh(), palette, _root, Vector3.zero);
            deck.gameObject.AddComponent<MeshCollider>();
            Part("Gallery railing and brackets", RailMesh(), palette, _root, Vector3.zero);
            for (int i = 0; i < 24; i++)
            {
                float a0 = i * 15, a1 = a0 + 15;
                if (Mathf.Abs(Mathf.DeltaAngle(a0 + 7.5f, 0)) < 8) continue;
                var col = new GameObject("Rail collider " + i).AddComponent<BoxCollider>(); col.transform.SetParent(_root, false);
                col.transform.localPosition = Quaternion.Euler(0, a0 + 7.5f, 0) * Vector3.forward * RailRadius + Vector3.up * .55f;
                col.transform.localRotation = Quaternion.Euler(0, a0 + 7.5f, 0); col.size = new Vector3(2 * RailRadius * Mathf.Sin(7.5f * Mathf.Deg2Rad) + .05f, 1.1f, .1f);
            }
            var chain = new GameObject("Ladder gate safety chain").AddComponent<BoxCollider>(); chain.transform.SetParent(_root, false);
            chain.transform.localPosition = Vector3.forward * RailRadius + Vector3.up * .55f; chain.size = new Vector3(1.75f, 1.1f, .1f); _chain = chain;
            // Keeps walkers outside the lantern's eave so the camera never clips the roof.
            var core = new GameObject("Lantern clearance").AddComponent<CapsuleCollider>(); core.transform.SetParent(_root, false);
            core.center = Vector3.up * 1.5f; core.radius = 1.95f; core.height = 8;

            // Ladder hangs outside the deck rim on the house side; the climb ends beside the gate and steps onto the deck.
            float ground = GroundY(Axis + Quaternion.Euler(0, LadderYaw, 0) * Vector3.forward * (LadderRadius + .45f));
            Part("Iron outer ladder", LadderMesh(DeckY - ground), palette, _root, Vector3.zero);
            Ladder = new GameObject("Lighthouse outer ladder").AddComponent<FishingClimbRoute>(); Ladder.transform.SetParent(_root, false);
            Ladder.bottom = new Vector3(0, ground - DeckY, LadderRadius + .45f);
            Ladder.top = new Vector3(0, 0, 2.75f);
            Ladder.topOutset = new Vector3(0, .05f, LadderRadius + .45f - 2.75f);
            Ladder.rungOffset = Vector3.back * .45f; // the body clears the rails; hands grip the visible ladder itself
            c.Shore.RefreshClimbRoutes();

            _oilCan = Part("Lamp oil can", OilCanMesh(), palette, _root, Quaternion.Euler(0, 55, 0) * Vector3.forward * 3.0f);
            _bucket = Part("Lens cloth bucket", BucketMesh(), palette, _root, Quaternion.Euler(0, 115, 0) * Vector3.forward * 3.0f);
            var pedestal = Part("Lens crank pedestal", PedestalMesh(), palette, _root, Quaternion.Euler(0, 180, 0) * Vector3.forward * 2.95f);
            foreach (var prop in new[] { _oilCan, _bucket, pedestal }) { var box = prop.gameObject.AddComponent<BoxCollider>(); box.center = Vector3.up * .5f; box.size = new Vector3(.45f, 1, .45f); }
            _crank = new GameObject("GrabCrank").transform; _crank.SetParent(pedestal, false); _crank.localPosition = Vector3.up * 1.02f;
            var source = Resources.Load<GameObject>("FishingV044/GrabCrank");
            if (source != null)
            {
                Instantiate(source, _crank, false);
                var storybook = Resources.Load<Material>("FishingV044/RealmStorybook");
                foreach (var r in _crank.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial = storybook;
            }
            _flame = new GameObject("Lamp flame warm light").AddComponent<Light>(); _flame.transform.SetParent(_root, false); _flame.transform.localPosition = Vector3.up * .7f;
            _flame.type = LightType.Point; _flame.color = new Color(1, .72f, .38f); _flame.range = 14; _flame.shadows = LightShadows.None; _flame.enabled = false;
            for (int k = 0; k < 4; k++) _crust.Add(Part("Salt crust on lantern " + k, CrustMesh(k), palette, _root, Vector3.zero));

            var boatMesh = BoatMesh(); _owned.Add(boatMesh);
            for (int i = 0; i < 3; i++)
            {
                var hull = Part("Late boat " + i, boatMesh, palette, transform, Vector3.zero);
                var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere); lamp.name = "Mast lamp"; Destroy(lamp.GetComponent<Collider>());
                lamp.transform.SetParent(hull, false); lamp.transform.localPosition = new Vector3(0, 3.3f, .4f); lamp.transform.localScale = Vector3.one * .9f;
                var lr = lamp.GetComponent<Renderer>(); lr.sharedMaterial = _glow; lr.shadowCastingMode = ShadowCastingMode.Off;
                hull.gameObject.SetActive(false);
                _boats.Add(new Boat { Hull = hull, Lamp = lamp.transform });
            }
            _boats.Clear();
            var parcelMesh = ParcelMesh(); _owned.Add(parcelMesh);
            var right = Vector3.Cross(Vector3.up, Vector3.ProjectOnPlane(c.Home.Origin - c.Home.BoardwalkStart, Vector3.up).normalized);
            for (int i = 0; i < _parcels.Length; i++)
            {
                float side = i % 2 == 0 ? -1 : 1, along = (i / 2 - 2) * .6f;
                var p = c.Home.BoardwalkStart + right * side * 1.62f + Vector3.ProjectOnPlane(c.Home.Origin - c.Home.BoardwalkStart, Vector3.up).normalized * along;
                _parcels[i] = Part("Thank-you parcel " + i, parcelMesh, palette, transform, p); _parcels[i].rotation = Quaternion.Euler(0, i * 37, 0);
                _parcels[i].gameObject.SetActive(false); _parcelBoat[i] = -1;
            }
            Harbor = HarborPoint();
            Triangles = 0; foreach (var f in _root.GetComponentsInChildren<MeshFilter>()) if (f.sharedMesh != null && f.sharedMesh.isReadable) Triangles += (int)f.sharedMesh.GetIndexCount(0) / 3; // shortcut: imported non-readable meshes are skipped (Unity forbids index access); count is a budget diagnostic only
            if (boatMesh.isReadable) Triangles += (int)boatMesh.GetIndexCount(0) / 3 * 3;
            RefreshParcels();
        }

        private Transform Part(string name, Mesh mesh, Material m, Transform parent, Vector3 local)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent, false); go.transform.localPosition = local;
            go.GetComponent<MeshFilter>().sharedMesh = mesh; var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off;
            if (!_owned.Contains(mesh)) _owned.Add(mesh); return go.transform;
        }
        private static float GroundY(Vector3 p)
        {
            float top = -5;
            foreach (var h in Physics.RaycastAll(new Vector3(p.x, 60, p.z), Vector3.down, 120, ~((1 << 4) | (1 << 5)), QueryTriggerInteraction.Ignore))
                if (!FishingShore.NonSolidSurface(h.collider)) top = Mathf.Max(top, h.point.y);
            return top;
        }
        private Vector3 HarborPoint()
        {
            // Just off the home berth, on the open-water side.
            var berth = _c.Home.Berth; var away = Vector3.ProjectOnPlane(berth - _c.Home.Origin, Vector3.up).normalized;
            for (float d = 6; d < 40; d += 2) { var p = berth + away * d; if (FishingSeaEventWorld.OpenWater(p, 3)) return new Vector3(p.x, 0, p.z); }
            return new Vector3(berth.x, 0, berth.z) + away * 10;
        }

        // ---------- Meshes ----------
        private static Mesh DeckMesh()
        {
            var b = new FishingMeshBuilder(); const int n = 48;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * 360f / n, a1 = (i + 1) * 360f / n;
                var plank = i % 2 == 0 ? new Color(.55f, .38f, .22f) : new Color(.5f, .34f, .2f);
                Vector3 P(float a, float r, float y) => Quaternion.Euler(0, a, 0) * Vector3.forward * r + Vector3.up * y;
                b.Quad(P(a0, DeckInner, 0), P(a0, DeckOuter, 0), P(a1, DeckOuter, 0), P(a1, DeckInner, 0), plank);
                b.Quad(P(a1, DeckInner, -.14f), P(a1, DeckOuter, -.14f), P(a0, DeckOuter, -.14f), P(a0, DeckInner, -.14f), new Color(.3f, .22f, .15f));
                b.Quad(P(a0, DeckOuter, 0), P(a0, DeckOuter, -.14f), P(a1, DeckOuter, -.14f), P(a1, DeckOuter, 0), new Color(.36f, .26f, .17f));
            }
            return b.Finish("Lighthouse keeper gallery deck");
        }
        private static Mesh RailMesh()
        {
            var b = new FishingMeshBuilder(); var iron = new Color(.16f, .19f, .2f); var paint = new Color(.82f, .28f, .2f);
            Vector3 P(float a, float r, float y) => Quaternion.Euler(0, a, 0) * Vector3.forward * r + Vector3.up * y;
            for (int i = 0; i < 48; i++)
            {
                float a0 = i * 7.5f, a1 = a0 + 7.5f;
                bool gate = Mathf.Abs(Mathf.DeltaAngle(a0 + 3.75f, 0)) < 9;
                if (!gate)
                {
                    b.Tube(P(a0, RailRadius, 1.05f), P(a1, RailRadius, 1.05f), .035f, .035f, paint, 6);
                    b.Tube(P(a0, RailRadius, .55f), P(a1, RailRadius, .55f), .02f, .02f, iron, 5);
                }
                if (i % 2 == 0 && Mathf.Abs(Mathf.DeltaAngle(a0, 0)) > 8.5f) b.Tube(P(a0, RailRadius, 0), P(a0, RailRadius, 1.05f), .025f, .025f, iron, 5);
                if (i % 4 == 0) b.Tube(P(a0, 1.5f, -1.4f), P(a0, DeckOuter - .2f, -.14f), .045f, .035f, iron, 5);
            }
            foreach (float s in new[] { -1f, 1f }) b.Tube(P(s * 9, RailRadius, 0), P(s * 9, RailRadius, 1.15f), .04f, .04f, iron, 6);
            // Sagging gate chain across the ladder opening.
            for (int k = 0; k < 4; k++)
            {
                float t0 = k / 4f, t1 = (k + 1) / 4f;
                b.Tube(P(-9 + 18 * t0, RailRadius, .85f - .12f * Mathf.Sin(t0 * Mathf.PI)), P(-9 + 18 * t1, RailRadius, .85f - .12f * Mathf.Sin(t1 * Mathf.PI)), .015f, .015f, new Color(.7f, .6f, .35f), 4);
            }
            return b.Finish("Gallery railing, gate and brackets");
        }
        private static Mesh LadderMesh(float height)
        {
            var b = new FishingMeshBuilder(); var iron = new Color(.2f, .22f, .23f);
            foreach (float s in new[] { -.26f, .26f }) b.Tube(new Vector3(s, -height, LadderRadius), new Vector3(s, 1.0f, LadderRadius), .03f, .03f, iron, 6);
            for (float y = -height + .3f; y < .95f; y += .3f) b.Tube(new Vector3(-.26f, y, LadderRadius), new Vector3(.26f, y, LadderRadius), .018f, .018f, iron, 5);
            // Brackets tie the ladder back to the tapered tower (radius 2.15 at the plinth, 1.38 under the deck).
            for (float y = -height + 1.2f; y < -.4f; y += 2.4f)
            {
                float towerR = Mathf.Lerp(1.38f, 2.15f, Mathf.Clamp01(-y / (height - .5f)));
                foreach (float s in new[] { -.26f, .26f }) b.Tube(new Vector3(s, y, LadderRadius), new Vector3(s * .6f, y, towerR - .05f), .025f, .025f, iron, 5);
            }
            return b.Finish("Lighthouse outer iron ladder");
        }
        private static Mesh OilCanMesh()
        {
            var b = new FishingMeshBuilder(); var brass = new Color(.78f, .58f, .28f);
            b.Tube(Vector3.zero, new Vector3(0, .38f, 0), .15f, .13f, brass, 12);
            b.Tube(new Vector3(0, .38f, 0), new Vector3(0, .45f, 0), .05f, .04f, new Color(.3f, .25f, .2f), 8);
            b.Tube(new Vector3(0, .3f, .1f), new Vector3(0, .5f, .3f), .025f, .012f, brass, 6);
            b.Arc(new Vector3(0, .38f, 0), .12f, .015f, 0, 180, new Color(.25f, .22f, .2f), 8);
            return b.Finish("Brass lamp oil can");
        }
        private static Mesh BucketMesh()
        {
            var b = new FishingMeshBuilder();
            b.Tube(Vector3.zero, new Vector3(0, .32f, 0), .14f, .18f, new Color(.42f, .5f, .55f), 12);
            b.Ellipsoid(new Vector3(.02f, .34f, 0), new Vector3(.13f, .05f, .1f), new Color(.92f, .9f, .82f), 8, 4);
            b.Ellipsoid(new Vector3(.1f, .36f, .05f), new Vector3(.08f, .1f, .03f), new Color(.92f, .9f, .82f), 6, 4, Quaternion.Euler(0, 0, -30));
            return b.Finish("Cloth bucket");
        }
        private static Mesh PedestalMesh()
        {
            var b = new FishingMeshBuilder(); var iron = new Color(.2f, .23f, .25f); var brass = new Color(.78f, .58f, .28f);
            b.Tube(Vector3.zero, new Vector3(0, .06f, 0), .22f, .22f, iron, 10);
            b.Tube(new Vector3(0, .06f, 0), new Vector3(0, .95f, 0), .07f, .05f, iron, 8);
            b.Tube(new Vector3(0, .95f, 0), new Vector3(0, 1.0f, 0), .12f, .12f, brass, 10);
            return b.Finish("Lens crank pedestal");
        }
        private static Mesh CrustMesh(int k)
        {
            var b = new FishingMeshBuilder(); var random = new System.Random(77 + k);
            for (int i = 0; i < 14; i++)
            {
                float a = (float)random.NextDouble() * 360, y = .25f + (float)random.NextDouble() * 1.15f, s = .12f + (float)random.NextDouble() * .16f;
                var p = Quaternion.Euler(0, a, 0) * Vector3.forward * 1.46f + Vector3.up * y;
                b.Ellipsoid(p, new Vector3(s, s * .8f, .012f), new Color(.86f, .88f, .84f), 8, 4, Quaternion.Euler(0, a, 0));
            }
            return b.Finish("Salt crust patch " + k);
        }
        private static Mesh BoatMesh()
        {
            var b = new FishingMeshBuilder(); var hull = new Color(.24f, .2f, .17f); var trim = new Color(.75f, .7f, .6f);
            b.Ellipsoid(new Vector3(0, .1f, 0), new Vector3(1.1f, .55f, 3.2f), hull, 14, 8);
            b.Tube(new Vector3(0, .45f, -.8f), new Vector3(0, 1.25f, -.8f), .7f, .65f, trim, 4);
            b.Tube(new Vector3(0, .4f, .4f), new Vector3(0, 3.3f, .4f), .05f, .04f, hull, 6);
            return b.Finish("Late fishing boat");
        }
        /// <summary>Small slatted fish crate lashed with hemp rope, an oilcloth bundle and a sealed envelope on top.</summary>
        private static Mesh ParcelMesh()
        {
            var b = new FishingMeshBuilder(); var wood = new Color(.62f, .45f, .28f); var seam = new Color(.36f, .25f, .15f);
            var rope = new Color(.78f, .68f, .46f); var cloth = new Color(.55f, .5f, .32f);
            const float w = .36f, d = .26f, h = .2f;
            for (int i = 0; i < 3; i++)
            {
                float y = .01f + i * h / 3;
                // Three slats per side with dark gaps between them.
                foreach (float s in new[] { -1f, 1f })
                {
                    b.Box(new Vector3(0, y + h / 6, s * d / 2), new Vector3(w, h / 3 - .012f, .022f), i % 2 == 0 ? wood : wood * .92f);
                    b.Box(new Vector3(s * w / 2, y + h / 6, 0), new Vector3(.022f, h / 3 - .012f, d), i % 2 == 0 ? wood * .95f : wood);
                }
            }
            b.Box(new Vector3(0, .005f, 0), new Vector3(w, .01f, d), seam);
            foreach (float s in new[] { -1f, 1f }) foreach (float t in new[] { -1f, 1f }) b.Box(new Vector3(s * (w / 2 - .01f), h / 2, t * (d / 2 - .01f)), new Vector3(.035f, h + .01f, .035f), seam);
            b.Ellipsoid(new Vector3(0, h + .045f, 0), new Vector3(.15f, .055f, .1f), cloth, 10, 5);
            b.Tube(new Vector3(-w / 2 - .01f, h + .01f, .03f), new Vector3(w / 2 + .01f, h + .01f, .03f), .009f, .009f, rope, 5);
            b.Tube(new Vector3(0, h + .01f, -d / 2 - .01f), new Vector3(0, h + .01f, d / 2 + .01f), .009f, .009f, rope, 5);
            b.Tube(new Vector3(0, h + .09f, 0), new Vector3(.03f, h + .12f, .04f), .008f, .006f, rope, 5);
            b.Box(new Vector3(.07f, h + .105f, -.03f), new Vector3(.16f, .006f, .11f), new Color(.95f, .91f, .8f));
            b.Ellipsoid(new Vector3(.07f, h + .11f, -.03f), new Vector3(.018f, .006f, .018f), new Color(.7f, .16f, .12f), 8, 3);
            return b.Finish("Thank-you crate with letter");
        }

        // ---------- Night logic ----------
        private int NightKey(FishingCalendar cal) => cal.Hour < 6 ? cal.Day - 1 : cal.Day;
        private bool Mist(int night) => FishingCalendar.Forecast(Mathf.Max(0, night), 3) == BayWeather.Mist;
        private void Update()
        {
            if (_c == null || _c.Career == null) return;
            var cal = _c.Career.Calendar;
            int key = NightKey(cal);
            bool active = Unlocked && key >= 0 && FishingTide.WatchNight(key) && (cal.Hour >= 19 || cal.Hour < 5);
            if (active && _night != key) BeginNight(key);
            if (!active && _night >= 0) EndNight();
            _chain.enabled = !_c.Shore.Climbing;
            if (_c.Paused || _c.StoryBlocksGameplay) return;
            TickCeremony();
            TickVrParcel(); // parcels wait on the jetty by day too
            if (WatchActive) { TickChoresInput(); TickBoats(Time.deltaTime); }
        }
        private float _litAt = -99;
        private Light _flame;
        /// <summary>Dusk on a watch night: standing on the gallery before 19:00 runs the sky forward to nightfall.</summary>
        public bool Timelapse { get; private set; }
        public const float TimelapseScale = 12;
        private void TickCeremony()
        {
            var cal = _c.Career.Calendar;
            bool dusk = Unlocked && !WatchActive && FishingTide.WatchNight(cal.Day) && cal.Hour >= 16.5f && cal.Hour < 19 && OnDeck && !_c.Paused && !_c.StoryBlocksGameplay;
            if (dusk != Timelapse) { Timelapse = dusk; _c.Weather.TimeScale = dusk ? TimelapseScale : 1; if (dusk) _c.Notice("你在塔上等着天黑，晚归的船就要回来了。", 5); }
            if (_flame == null) return;
            // Bloom of warm light for two seconds when the wick catches, then a steady glow: the night's one warm light.
            float t = Time.time - _litAt, glow = WatchActive && Lit ? (t < 2 ? Mathf.Lerp(7, 2.2f, Mathf.SmoothStep(0, 1, t / 2)) * Mathf.Clamp01(t * 6) : 2.2f) : 0;
            _flame.enabled = glow > .01f; _flame.intensity = glow;
        }
        private void OnDisable() { if (Timelapse && _c != null) _c.Weather.TimeScale = 1; Timelapse = false; }
        private void LateUpdate()
        {
            var sweep = _c?.Nightscape?.LighthouseSweep; if (sweep == null) return;
            sweep.gameObject.SetActive(!WatchActive || Lit);
            if (WatchActive && Lit && Manning) sweep.rotation = Quaternion.Euler(0, BeamYaw, 0);
            else BeamYaw = sweep.eulerAngles.y;
            if (_crank != null) _crank.localRotation = Quaternion.Euler(0, 0, -BeamYaw * 2);
            for (int k = 0; k < _crust.Count; k++) _crust[k].gameObject.SetActive(WatchActive && Clean < (k + 1) / 4f);
            if (_oilCan != null) _oilCan.localRotation = Quaternion.Euler(_pouring ? 35 : 0, 0, 0);
        }
        public void BeginNight(int key)
        {
            _night = key; Oil = Clean = 0; Manning = false; _boats.Clear();
            for (int i = 0; i < _guidedTonight.Length; i++) _guidedTonight[i] = false;
            var lore = _c.Career.Lore; var ids = new List<int>();
            for (int i = 0; i < FishingTide.BoatCount; i++) if (!lore.KeeperFlag(FishingTide.ArrivedBit + i)) ids.Add(i);
            for (int i = 0; ids.Count < 3; i++) { int id = (key + i) % FishingTide.BoatCount; if (!ids.Contains(id)) ids.Add(id); }
            int count = FishingTide.BoatsTonight(key, Mist(key));
            var spots = SpawnSpots(key, count);
            for (int i = 0; i < count && i < spots.Count; i++)
            {
                int id = ids[i]; var hull = transform.Find("Late boat " + i);
                var boat = new Boat { Id = id, Repeat = lore.KeeperFlag(FishingTide.ArrivedBit + id), Anchor = spots[i], Hull = hull, Lamp = hull.Find("Mast lamp") };
                hull.position = spots[i]; hull.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(Harbor - spots[i], Vector3.up)); hull.gameObject.SetActive(true);
                _boats.Add(boat);
            }
            _c.Notice("今夜有 " + _boats.Count + " 条船晚归" + (Mist(key) ? "，海上起雾了" : "") + "：登上灯塔添灯油、擦镜片、转动透镜，把他们一艘艘照回港。", 10);
            _c.WorldAudio?.Play("SeaFogHorn", Axis + Vector3.up * DeckY, .5f, FishingAudioCategory.Ambient);
        }
        /// <summary>Open-water anchors 60–100 m off the harbour, in beam range of the lens, with a clear straight run home.</summary>
        public List<Vector3> SpawnSpots(int key, int count)
        {
            var found = new List<Vector3>(); var random = new System.Random(key * 7919 + 13);
            var angles = new List<float>(); for (int a = 0; a < 360; a += 8) angles.Add(a);
            for (int i = angles.Count - 1; i > 0; i--) { int j = random.Next(i + 1); var t = angles[i]; angles[i] = angles[j]; angles[j] = t; }
            foreach (float a in angles)
            {
                if (found.Count >= count) break;
                var dir = Quaternion.Euler(0, a, 0) * Vector3.forward; float r = 60 + (float)random.NextDouble() * 40;
                var p = Harbor + dir * r;
                if (FishingPrototype.FlatDistance(p, Axis) > 125 || FishingPrototype.FlatDistance(p, Axis) < 45) continue;
                bool clear = FishingSeaEventWorld.OpenWater(p, 6);
                for (float d = 12; clear && d < r; d += 8) clear = FishingSeaEventWorld.OpenWater(Harbor + dir * d, 2.5f);
                foreach (var other in found) clear &= Vector3.Distance(other, p) > 18;
                if (clear) found.Add(new Vector3(p.x, 0, p.z));
            }
            return found;
        }
        private void EndNight()
        {
            foreach (var boat in _boats) if (!boat.Arrived) Arrive(boat, false);
            int late = 0; foreach (var boat in _boats) if (!boat.Guided) late++;
            if (late > 0 && _c.Career.Calendar.Hour >= 5 && _c.Career.Calendar.Hour < 12) _c.Notice("昨夜没被照到的 " + late + " 条船，天亮才摸回了港。码头上有他们留下的东西。", 8);
            foreach (var boat in _boats) boat.Hull.gameObject.SetActive(false);
            _boats.Clear(); _night = -1; Manning = false; _pouring = false;
        }

        private void TickBoats(float dt)
        {
            bool mist = _c.Weather.Climate.Current == BayWeather.Mist;
            float tolerance = mist ? 5 : 8, hold = mist ? 4 : 2.5f;
            foreach (var boat in _boats)
            {
                if (boat.Arrived) continue;
                var p = boat.Hull.position;
                if (!boat.Guided)
                {
                    float bearing = Quaternion.LookRotation(Vector3.ProjectOnPlane(p - Axis, Vector3.up)).eulerAngles.y;
                    float off = Mathf.Min(Mathf.Abs(Mathf.DeltaAngle(bearing, BeamYaw)), Mathf.Abs(Mathf.DeltaAngle(bearing, BeamYaw + 180)));
                    bool inBeam = Lit && off < tolerance;
                    boat.Progress = inBeam ? boat.Progress + dt / hold : Mathf.Max(0, boat.Progress - dt * .25f);
                    if (boat.Progress >= 1) GuideHome(boat);
                    float t = Time.time + boat.Id;
                    boat.Hull.position = boat.Anchor + new Vector3(Mathf.Sin(t * .2f) * 2.5f, Mathf.Sin(t * 1.3f) * .08f, Mathf.Cos(t * .17f) * 2.5f);
                    boat.Hull.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(Harbor - p, Vector3.up)) * Quaternion.Euler(Mathf.Sin(t) * 3, 0, Mathf.Sin(t * .8f) * 5);
                    // Mist dims and flickers the mast lamp; a lamp in the beam pulses back.
                    float lamp = (mist ? .45f : 1) * (mist ? .7f + .3f * Mathf.Sin(t * 5.3f) * Mathf.Sin(t * 2.1f) : 1) * (1 + boat.Progress * .6f * Mathf.Abs(Mathf.Sin(t * 8)));
                    boat.Lamp.localScale = Vector3.one * .9f * lamp;
                }
                else
                {
                    var to = Vector3.ProjectOnPlane(Harbor - p, Vector3.up);
                    if (to.magnitude < 6) { Arrive(boat, true); continue; }
                    boat.Hull.SetPositionAndRotation(p + to.normalized * Mathf.Min(7 * dt, to.magnitude), Quaternion.LookRotation(to));
                    boat.Lamp.localScale = Vector3.one * (.9f + .25f * Mathf.Sin(Time.time * 6));
                }
            }
        }
        private void GuideHome(Boat boat)
        {
            boat.Guided = true; boat.Progress = 1; _guidedTonight[boat.Id] = true;
            _c.WorldAudio?.Play("SeaFogHorn", boat.Hull.position, .9f, FishingAudioCategory.Ambient);
            _c.Effects?.Celebrate(boat.Lamp.position);
            int left = 0; foreach (var b in _boats) if (!b.Guided) left++;
            _c.Notice(FishingTide.Boats[boat.Id] + " 看见了灯，鸣笛调头回港。" + (left > 0 ? "海上还有 " + left + " 盏桅灯。" : "今夜的船都照回来了。"), 6);
        }
        private void Arrive(Boat boat, bool guided)
        {
            boat.Arrived = true; boat.Hull.gameObject.SetActive(false);
            var lore = _c.Career.Lore;
            if (!boat.Repeat) lore.SetKeeperFlag(FishingTide.ArrivedBit + boat.Id);
            else for (int i = 0; i < _parcels.Length; i++)
                    if (_parcelBoat[i] < 0 && !_parcels[i].gameObject.activeSelf && i >= FishingTide.BoatCount) { _parcelBoat[i] = boat.Id; break; }
            if (guided) _c.WorldAudio?.Play("LootBag", Harbor, .6f);
            RefreshParcels(); _c.Save();
        }
        private void RefreshParcels()
        {
            var lore = _c.Career.Lore;
            for (int i = 0; i < FishingTide.BoatCount; i++)
                _parcels[i].gameObject.SetActive(lore.KeeperFlag(FishingTide.ArrivedBit + i) && !lore.KeeperFlag(FishingTide.ReadBit + i));
            for (int i = FishingTide.BoatCount; i < _parcels.Length; i++) _parcels[i].gameObject.SetActive(_parcelBoat[i] >= 0);
        }

        // ---------- Chores ----------
        private bool _pouring;
        private bool OnDeck => _c.Ashore && !_c.Shore.Climbing && Mathf.Abs(_c.Shore.Position.y - DeckY) < .6f && FishingPrototype.FlatDistance(_c.Shore.Position, Axis) < DeckOuter;
        private bool Near(Transform t, float reach) => OnDeck && FishingPrototype.FlatDistance(_c.Shore.Position, t.position) < reach;
        public Chore? NextChore => !WatchActive ? (Chore?)null : Oil < 1 ? Chore.Oil : Clean < 1 ? Chore.Wipe : Chore.Crank;
        /// <summary>Advances a chore by seconds of holding (oil), metres of wiping (wipe); refuses out of order.</summary>
        public bool Work(Chore chore, float amount)
        {
            if (!WatchActive || NextChore != chore || amount <= 0) return false;
            if (chore == Chore.Oil) { Oil = Mathf.Min(1, Oil + amount / OilSeconds); if (Oil >= 1) Done("灯油添满了。下一步：擦去镜片上的盐霜。"); }
            else if (chore == Chore.Wipe)
            {
                Clean = Mathf.Min(1, Clean + amount / WipeMetres);
                if (Clean >= 1)
                {
                    Done("镜片擦亮了，灯芯点着了。握住摇柄，把光转向海上的桅灯。");
                    // The lighting moment: a warm burst at the lens, the one warm light of the night.
                    _c.Effects?.Celebrate(Axis + Vector3.up * (DeckY + .7f)); _c.WorldAudio?.PlayStinger("Tender"); _litAt = Time.time;
                    if (_countedNight != _night) { _countedNight = _night; _c.Career.Lore.AddKeeperNight(); _c.Save(); }
                }
            }
            return true;
        }
        private void Done(string text) { _c.Notice(text, 6); _c.WorldAudio?.Play("MechanismClick", Axis + Vector3.up * DeckY, .7f); }
        public bool SetManning(bool on)
        {
            if (on && (!WatchActive || !Lit)) return false;
            if (on && !Manning) BeamYaw = _c.Nightscape.LighthouseSweep.eulerAngles.y;
            Manning = on; return true;
        }
        /// <summary>Turns the manned lens toward a heading at the crank's limited speed.</summary>
        public void Steer(float targetYaw, float dt)
        {
            if (Manning) BeamYaw = Mathf.MoveTowardsAngle(BeamYaw, targetYaw, TurnRate * dt);
        }

        public string NearbyHint
        {
            get
            {
                if (_c == null || !OnDeck) return ParcelHint();
                if (!WatchActive) return null;
                if (Near(_oilCan, 1.3f) && NextChore == Chore.Oil) return (_c.DesktopDisplay ? "按住 E 添灯油 " : "握住油壶 2 秒添灯油 ") + Mathf.RoundToInt(Oil * 100) + "%";
                if (Near(_bucket, 1.4f) && NextChore == Chore.Wipe) return (_c.DesktopDisplay ? "按住 E 擦拭镜片上的盐霜 " : "握拳贴着灯室玻璃来回擦 ") + Mathf.RoundToInt(Clean * 100) + "%";
                if (Near(_crank, 1.3f) && NextChore == Chore.Crank) return Manning ? (_c.DesktopDisplay ? "转动视角带动光束 · E 松开摇柄" : "手指向桅灯并稳住 · 松手放开摇柄") : (_c.DesktopDisplay ? "E 握住摇柄转动透镜" : "握住摇柄，用手指向海上的桅灯");
                if (Vector3.Distance(_c.Shore.Position, Ladder.Top) < 1.7f) return null; // leave the ladder's own hint
                return NextChore == Chore.Oil ? "值夜：先去油壶旁添灯油" : NextChore == Chore.Wipe ? "值夜：去水桶旁取布擦镜片" : Manning ? null : "值夜：去摇柄旁转动透镜";
            }
        }
        private string ParcelHint()
        {
            int i = NearParcel(); if (i < 0) return null;
            return "E 取回 " + FishingTide.Boats[ParcelBoat(i)] + " 留下的谢礼";
        }
        private int ParcelBoat(int slot) => slot < FishingTide.BoatCount ? slot : _parcelBoat[slot];
        private int NearParcel()
        {
            if (_c == null || !_c.Ashore) return -1;
            for (int i = 0; i < _parcels.Length; i++)
                if (_parcels[i].gameObject.activeSelf && FishingPrototype.FlatDistance(_parcels[i].position, _c.Shore.Position) < 1.4f && Mathf.Abs(_parcels[i].position.y - _c.Shore.Position.y) < 1.2f) return i;
            return -1;
        }
        public bool TryInteract()
        {
            int parcel = NearParcel(); if (parcel >= 0) return OpenParcel(parcel);
            if (!OnDeck || !WatchActive) return false;
            if (Near(_crank, 1.3f) && NextChore == Chore.Crank) return SetManning(!Manning);
            // Oil and wipe progress while E stays held (see TickChoresInput); the press just starts it.
            return (Near(_oilCan, 1.3f) && NextChore == Chore.Oil) || (Near(_bucket, 1.4f) && NextChore == Chore.Wipe);
        }
        public bool OpenParcel(int slot)
        {
            if (slot < 0 || slot >= _parcels.Length || !_parcels[slot].gameObject.activeSelf) return false;
            var lore = _c.Career.Lore; int id = ParcelBoat(slot); bool guided = _guidedTonight[id];
            int paid = _c.Career.AwardSeaCoins((slot < FishingTide.BoatCount ? FishingTide.ParcelReward : FishingTide.RepeatReward) + (guided ? FishingTide.GuideReward : 0));
            _c.WorldAudio?.Play("LootBag", _parcels[slot].position, .7f);
            if (slot < FishingTide.BoatCount)
            {
                lore.SetKeeperFlag(FishingTide.ReadBit + id);
                if (_c.Letter != null && _c.Letter.CanRead)
                    _c.Letter.Show(FishingTide.Boats[id] + "的来信", FishingTide.Skippers[id], FishingTide.Letters[id] + (guided ? FishingTide.GuidedPostscript : FishingTide.LatePostscript), _parcels[slot]);
            }
            else _parcelBoat[slot] = -1;
            _guidedTonight[id] = false;
            _c.Notice(FishingTide.Boats[id] + " 留下一包鱼干和 " + paid + " 金币。", 6);
            RefreshParcels(); _c.Save(); return true;
        }
        private void TickChoresInput()
        {
            _pouring = false;
            if (!OnDeck) { if (Manning) Manning = false; _palmValid = false; return; }
            float dt = Time.deltaTime; var keyboard = Keyboard.current;
            bool held = !_c.Automated && keyboard != null && keyboard.eKey.isPressed;
            if (held && Near(_oilCan, 1.3f) && Work(Chore.Oil, dt)) _pouring = true;
            if (held && Near(_bucket, 1.4f)) Work(Chore.Wipe, dt * 1.1f);
            if (Manning)
            {
                if (!Near(_crank, 1.8f)) Manning = false;
                // VR: the gripping hand points the beam; desktop: the view does.
                else if (_vrCrank && _c.Hands != null && _c.Hands.TryControllerPalm(false, out _, out var aim)) Steer(Quaternion.LookRotation(Vector3.ProjectOnPlane(aim * Vector3.forward, Vector3.up) + Vector3.forward * 1e-4f).eulerAngles.y, dt);
                else Steer(_c.viewCamera.transform.eulerAngles.y, dt);
            }
            if (_c.DesktopDisplay || _c.Hands == null) return;
            // VR: grip near the can to pour, rub the lantern glass with a gripped hand, grip the crank and look to aim.
            bool palm = _c.Hands.TryControllerPalm(false, out var p, out _);
            InputDevices.GetDeviceAtXRNode(XRNode.RightHand).TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip, out float grip);
            if (!palm || grip < .6f) { _palmValid = false; if (Manning && _vrCrank) { Manning = false; _vrCrank = false; } return; }
            if (Vector3.Distance(p, _oilCan.position + Vector3.up * .3f) < .45f && Work(Chore.Oil, dt)) _pouring = true;
            float axisDistance = FishingPrototype.FlatDistance(p, Axis);
            if (NextChore == Chore.Wipe && axisDistance < 1.9f && p.y > DeckY + .2f && p.y < DeckY + 1.6f)
            {
                if (_palmValid) Work(Chore.Wipe, Vector3.Distance(p, _lastPalm));
                _lastPalm = p; _palmValid = true;
            }
            else _palmValid = false;
            if (NextChore == Chore.Crank && !Manning && Vector3.Distance(p, _crank.position) < .4f) { SetManning(true); _vrCrank = true; }
        }
        private bool _vrCrank;
        /// <summary>VR: closing a hand on a jetty parcel opens it.</summary>
        private void TickVrParcel()
        {
            if (_c.DesktopDisplay || _c.Hands == null || !_c.Ashore) return;
            foreach (bool left in new[] { false, true })
            {
                if (!_c.Hands.TryControllerPalm(left, out var palm, out _)) continue;
                var device = InputDevices.GetDeviceAtXRNode(left ? XRNode.LeftHand : XRNode.RightHand);
                device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip, out float grip); if (grip < .6f) continue;
                for (int i = 0; i < _parcels.Length; i++)
                    if (_parcels[i].gameObject.activeSelf && Vector3.Distance(_parcels[i].position + Vector3.up * .15f, palm) < .35f && OpenParcel(i)) { device.SendHapticImpulse(0, .4f, .08f); return; }
            }
        }
        private void OnDestroy() { foreach (var o in _owned) if (o != null) Destroy(o); }
    }
}
