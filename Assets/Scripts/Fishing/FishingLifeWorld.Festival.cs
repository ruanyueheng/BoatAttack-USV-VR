using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BoatAttack.Fishing
{
    public sealed partial class FishingLifeWorld
    {
        public const float PoleRadius = 7, PoleHeight = 3.6f, PoleCollider = .08f;
        public Vector3 Plaza { get; private set; }
        /// <summary>Festival currently decorated (-1 none).</summary>
        public int FestivalShown { get; private set; } = -1;
        public int PoleCount { get; private set; }
        public IReadOnlyList<Vector3> PoleFeet => _poleFeet;
        private Transform _festivalRoot;
        private readonly Transform[] _festivals = new Transform[4];
        private readonly List<Vector3> _poleFeet = new List<Vector3>();
        private readonly List<Transform> _kites = new List<Transform>();
        private int _announcedDay = -1;

        private void BuildFestival()
        {
            _festivalRoot = new GameObject("Festival decorations").transform; _festivalRoot.SetParent(_root, false);
            Plaza = FindPlaza();
            // Each pole spot must be clear ground (no building/prop within 0.6 m) and within 1.2 m height of the plaza.
            for (int i = 0; i < 12 && _poleFeet.Count < 6; i++)
                foreach (float radius in new[] { PoleRadius, PoleRadius - 1.5f, PoleRadius + 1.5f, PoleRadius - 3 })
                {
                    float a = i * Mathf.PI * 2 / 12;
                    var seed = Plaza + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * radius;
                    if (!Floor(seed, Plaza.y, out var feet) || Mathf.Abs(feet.y - Plaza.y) > .7f || !ClearSpot(feet, .4f) || _shopZone.Contains(new Vector3(feet.x, _shopZone.center.y, feet.z))) continue;
                    bool spaced = true; foreach (var f in _poleFeet) if ((f - feet).magnitude < 3.5f) spaced = false;
                    if (spaced) { _poleFeet.Add(feet); break; }
                }
            PoleCount = _poleFeet.Count;
            Debug.Log("[LIFE] plaza " + Plaza + " zone " + _shopZone + " outward " + PlazaOutward + " poles " + PoleCount);
            foreach (var f in _poleFeet)
            {
                var pole = new GameObject("Festival pole collider", typeof(CapsuleCollider)); pole.transform.SetParent(_festivalRoot, false); pole.transform.position = f;
                var cc = pole.GetComponent<CapsuleCollider>(); cc.radius = PoleCollider; cc.height = PoleHeight; cc.center = Vector3.up * PoleHeight * .5f;
            }
            for (int k = 0; k < 4; k++) _festivals[k] = BuildDecoration(k);
            _festivalRoot.gameObject.SetActive(false);
        }
        /// <summary>Horizontal direction from the shop toward its porch: the plaza grows outward from the door, never through the building.</summary>
        public Vector3 PlazaOutward { get; private set; }
        private Bounds _shopZone;
        private Vector3 FindPlaza()
        {
            var shop = _c.TackleShop.transform; var porch = shop.TransformPoint(new Vector3(-2, .08f, -5.6f));
            _shopZone = shop.GetComponentsInChildren<Renderer>().Aggregate(new Bounds(shop.position, Vector3.zero), (b, r) => { b.Encapsulate(r.bounds); return b; });
            _shopZone.Expand(new Vector3(1.5f, 0, 1.5f)); // poles stay off the shop deck, its stairs and the walkway around it
            var shopCentre = _shopZone.center;
            var outward = Vector3.ProjectOnPlane(porch - shopCentre, Vector3.up); if (outward.sqrMagnitude < .01f) outward = -shop.forward;
            PlazaOutward = outward.normalized; var side = Vector3.Cross(Vector3.up, PlazaOutward);
            foreach (float d in new[] { 8f, 10f, 12f, 14f, 16f, 18f, 20f })
                foreach (float s in new[] { 0f, -4f, 4f, -8f, 8f })
                {
                    var seed = porch + PlazaOutward * d + side * s;
                    if (Floor(seed, porch.y, out var feet) && feet.y > porch.y - .9f && feet.y < porch.y + 1 && ClearSpot(feet, 1.5f) && _shopZone.SqrDistance(new Vector3(feet.x, _shopZone.center.y, feet.z)) > 4) return feet; // dry sand only: the tidal flat floods
                }
            return porch;
        }
        private readonly RaycastHit[] _floorHits = new RaycastHit[16];
        /// <summary>Walkable top surface near <paramref name="level"/>, cast from 1.4 m above it so porch roofs and awnings are never mistaken for ground.</summary>
        private bool Floor(Vector3 seed, float level, out Vector3 feet)
        {
            feet = seed; float top = float.MinValue; bool shopFloor = false;
            int n = Physics.RaycastNonAlloc(new Vector3(seed.x, level + 1.4f, seed.z), Vector3.down, _floorHits, 5, ~((1 << 4) | (1 << 5)), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var h = _floorHits[i];
                if (h.normal.y < .69f || h.point.y < .6f || h.point.y <= top || FishingShore.NonSolidSurface(h.collider) || h.collider.transform.IsChildOf(_c.Shore.Actor) || h.collider.transform.IsChildOf(_c.Player.transform)) continue;
                top = h.point.y; feet = h.point; shopFloor = h.collider.transform.IsChildOf(_c.TackleShop.transform);
            }
            return top > float.MinValue && !shopFloor; // never on the shop deck or its stairs
        }
        /// <summary>A bunting line between two pole tops must not pass through any solid collider.</summary>
        private bool ClearLine(Vector3 a, Vector3 b)
        {
            foreach (var h in Physics.SphereCastAll(a, .25f, (b - a).normalized, (b - a).magnitude, ~((1 << 4) | (1 << 5)), QueryTriggerInteraction.Ignore))
                if (!FishingShore.NonSolidSurface(h.collider) && !h.collider.transform.IsChildOf(_festivalRoot) && !h.collider.transform.IsChildOf(_c.Shore.Actor) && !h.collider.transform.IsChildOf(_c.Player.transform)) return false;
            return true;
        }
        /// <summary>No non-trigger collider (buildings, props, people) in a capsule from knee height to above the head.</summary>
        public bool ClearSpot(Vector3 feet, float radius)
        {
            foreach (var col in Physics.OverlapCapsule(feet + Vector3.up * (radius + .25f), feet + Vector3.up * (PoleHeight + .3f), radius, ~((1 << 4) | (1 << 5)), QueryTriggerInteraction.Ignore))
                if (!FishingShore.NonSolidSurface(col) && !col.transform.IsChildOf(_c.Shore.Actor) && !col.transform.IsChildOf(_c.Player.transform) && !col.transform.IsChildOf(_festivalRoot)) return false;
            return true;
        }

        private Transform BuildDecoration(int festival)
        {
            var root = new GameObject("Festival " + FishingFestival.Name(festival)).transform; root.SetParent(_festivalRoot, false);
            var body = new FishingLifeMesh(); var lights = new FishingLifeMesh();
            Color wood = new Color(.45f, .3f, .18f), rope = new Color(.82f, .74f, .58f);
            Color[] flags = festival == 0 ? new[] { new Color(.94f, .45f, .38f), new Color(.98f, .82f, .4f), new Color(.45f, .72f, .62f), new Color(.5f, .62f, .86f) }
                : new[] { new Color(.93f, .58f, .2f), new Color(.96f, .8f, .36f), new Color(.72f, .36f, .2f) };
            Color lamp = festival == 1 ? new Color(.45f, .9f, 1f) : new Color(1f, .42f, .28f);
            foreach (var f in _poleFeet)
            {
                body.Cylinder(f, f + Vector3.up * PoleHeight, .07f, .05f, wood, 8);
                body.Cylinder(f + Vector3.up * PoleHeight, f + Vector3.up * (PoleHeight + .12f), .07f, 0, festival == 3 ? lamp : rope, 8);
                if (festival == 2) // harvest: a pumpkin and a straw bale at each pole foot, inside the checked clear radius
                {
                    body.Cylinder(f + new Vector3(.3f, 0, 0), f + new Vector3(.3f, .22f, 0), .16f, .14f, new Color(.95f, .55f, .16f), 8);
                    body.Cylinder(f + new Vector3(.3f, .22f, 0), f + new Vector3(.3f, .28f, 0), .02f, .015f, new Color(.35f, .45f, .2f), 5);
                    body.Box(f + new Vector3(-.28f, .18f, .1f), new Vector3(.42f, .36f, .3f), new Color(.88f, .76f, .45f), Quaternion.Euler(0, 25, 0));
                }
            }
            for (int i = 0; i < _poleFeet.Count; i++)
            {
                Vector3 a = _poleFeet[i] + Vector3.up * (PoleHeight - .15f), b = _poleFeet[(i + 1) % _poleFeet.Count] + Vector3.up * (PoleHeight - .15f);
                if (_poleFeet.Count < 2 || (a - b).magnitude > PoleRadius * 1.6f || (_poleFeet.Count == 2 && i == 1) || !ClearLine(a, (a + b) * .5f + Vector3.down * .6f) || !ClearLine((a + b) * .5f + Vector3.down * .6f, b)) continue;
                int steps = Mathf.Max(4, Mathf.RoundToInt((a - b).magnitude / .6f));
                Vector3 Sag(float t) => Vector3.Lerp(a, b, t) + Vector3.down * (.6f * 4 * t * (1 - t));
                for (int s = 0; s < steps; s++)
                {
                    Vector3 p = Sag(s / (float)steps), q = Sag((s + 1) / (float)steps);
                    body.Cylinder(p, q, .008f, .008f, rope, 4);
                    if (s == 0) continue;
                    if (festival == 1 || festival == 3)
                    {
                        if (s % 2 == 1) continue;
                        body.Cylinder(p, p + Vector3.down * .08f, .004f, .004f, rope, 4);
                        lights.Cylinder(p + Vector3.down * .08f, p + Vector3.down * .36f, .1f, .1f, lamp, 8);
                        body.Cylinder(p + Vector3.down * .36f, p + Vector3.down * .4f, .07f, .07f, new Color(.2f, .15f, .1f), 8);
                    }
                    else
                    {
                        var dir = (q - p).normalized; var c = flags[s % flags.Length];
                        body.Triangle(p, p + dir * .38f, p + dir * .19f + Vector3.down * .34f, c);
                    }
                }
            }
            MeshObject("Festival props", body.Finish("Festival " + festival), _palette, root);
            if (festival == 1 || festival == 3) MeshObject("Festival lanterns (unlit glow)", lights.Finish("Festival lanterns " + festival), _glow, root);
            if (festival == 0)
                for (int i = 0; i < 3; i++)
                {
                    var kite = new FishingLifeMesh(); var c = flags[i];
                    kite.Triangle(new Vector3(0, .9f, 0), new Vector3(-.55f, 0, 0), new Vector3(.55f, 0, 0), c);
                    kite.Triangle(new Vector3(-.55f, 0, 0), new Vector3(0, -1.1f, 0), new Vector3(.55f, 0, 0), flags[(i + 1) % flags.Length]);
                    for (int t = 0; t < 5; t++) kite.Triangle(new Vector3(0, -1.1f - t * .35f, 0), new Vector3(-.12f, -1.3f - t * .35f, 0), new Vector3(.12f, -1.3f - t * .35f, 0), flags[(i + t) % flags.Length]);
                    var k = MeshObject("Festival kite " + i, kite.Finish("Kite " + i), _palette, root);
                    float a = i * 2.1f; k.position = Plaza + new Vector3(Mathf.Cos(a) * 5, 11 + i * 2, Mathf.Sin(a) * 5); _kites.Add(k);
                }
            root.gameObject.SetActive(false);
            return root;
        }

        private void TickFestival()
        {
            var cal = _c.Career.Calendar; int today = FishingFestival.On(cal.Day);
            if (today != FestivalShown)
            {
                FestivalShown = today;
                _festivalRoot.gameObject.SetActive(today >= 0);
                for (int k = 0; k < 4; k++) _festivals[k].gameObject.SetActive(k == today);
            }
            if (today >= 0 && _announcedDay != cal.Day && !_c.Automated && !_c.StoryBlocksGameplay)
            {
                _announcedDay = cal.Day;
                _c.Notice("今天是 · " + FishingFestival.Name(today) + "　" + FishingFestival.Blurb(today), 8);
            }
        }
        private void AnimateFestival()
        {
            if (FestivalShown != 0) return;
            for (int i = 0; i < _kites.Count; i++)
            {
                float t = Time.time * .6f + i * 1.7f, a = i * 2.1f;
                _kites[i].position = Plaza + new Vector3(Mathf.Cos(a) * 5 + Mathf.Sin(t) * .8f, 11 + i * 2 + Mathf.Sin(t * 1.3f) * .5f, Mathf.Sin(a) * 5);
                _kites[i].rotation = Quaternion.Euler(Mathf.Sin(t * 1.1f) * 8, a * Mathf.Rad2Deg, Mathf.Sin(t) * 12);
            }
        }
    }
}
